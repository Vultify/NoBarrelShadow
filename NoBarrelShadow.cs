using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace NoBarrelShadow
{
    [BepInPlugin("com.vultify.nobarrelshadow", "No Barrel Shadow", "1.1.0")]
    public class NoBarrelShadowPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        public static ConfigEntry<bool> DebugLogging;

        // Category node IDs from the item database — every flashlight-type
        // attachment in the game falls under one of these three.
        private const string CategoryFlashlight = "55818b084bdc2d5b648b4571";
        private const string CategoryLightLaser = "55818b0e4bdc2dde698b456e";
        private const string CategoryTacticalCombo = "55818b164bdc2ddc698b456c";

        // Laser designators / IR-only illuminators have no visible light beam to boost.
        // Matched by codename FRAGMENT rather than a fixed template ID, since some
        // installs have alternate color-variant IDs (e.g. an "_blk" AN/PEQ-15) that
        // don't match a single hardcoded ID but still share the same core codename.
        private static readonly string[] ExcludedCodenameFragments =
        {
            "anpeq",     // AN/PEQ-15, AN/PEQ-2 (and any color variants) — visible laser + IR illuminator/searchlight only
            "la5",       // L3Harris LA-5B/PEQ (real codename is "insight_la5", not "la5b") — visible laser + IR searchlight only
            "dbal",      // Steiner DBAL series EXCEPT the DBAL-PL, handled separately below (it has a real LED flashlight)
            "perst",     // Zenit Perst-3 — visible laser + IR searchlight only
            "mawl",      // B.E. Meyers MAWL-C1+ — visible laser + IR illuminator only
            "ls321",     // Holosun LS321 — visible laser + IR searchlight only
            "dlp",       // TT DLP Laser Sight — laser only, no light
            "ncstar",    // NcSTAR Tactical Blue Laser — laser only, no light
            "raptar",    // Wilcox RAPTAR ES — rangefinder + lasers, no light
            "2irs",      // Zenit Klesch-2IRS (real codename, not "2iks") — IR illuminator
        };

        // The DBAL-PL genuinely has a visible LED flashlight alongside its lasers/IR
        // illuminator, so it must not be caught by the general "dbal" fragment above.
        private static bool IsExcludedByCodename(string codename)
        {
            string lower = codename.ToLowerInvariant();
            if (lower.Contains("dbal_pl") || lower.Contains("dbalpl")) return false;

            foreach (var fragment in ExcludedCodenameFragments)
            {
                if (lower.Contains(fragment)) return true;
            }
            return false;
        }

        // Tracks the template dictionary's size rather than a one-shot "done" flag,
        // since content mods (e.g. WTT-ContentBackport) can inject additional items
        // into ItemTemplates after our first check — a single catalog pass would
        // permanently miss anything added later in the loading sequence.
        private int _lastCataloguedCount = -1;

        // Per-item multiplier config entries, keyed by item template ID.
        internal static readonly Dictionary<string, ConfigEntry<float>> RangeMultipliers = new Dictionary<string, ConfigEntry<float>>();
        internal static readonly Dictionary<string, ConfigEntry<float>> IntensityMultipliers = new Dictionary<string, ConfigEntry<float>>();

        // Precomputed offline (real ShortName + variant disambiguation, generated
        // against the item/locale database directly) so display names don't depend
        // on the live client locale system being ready, which it isn't this early in
        // the loading sequence. Anything not in this file (e.g. modded lights) falls
        // back to the codename-cleanup naming instead.
        private static Dictionary<string, string> _knownNames;

        // Names learned live from the locale system once the player is actually in a
        // raid (locale is reliably populated by then). Saved to disk so a name only
        // ever needs to be learned once — every future launch loads it immediately
        // alongside the shipped list, without waiting to enter a raid again.
        private static Dictionary<string, string> _learnedNames;
        private static readonly string LearnedNamesFileName = "light_names_learned.json";
        private bool _hasAttemptedLearning;

        // Exposed so other mods' lights can be registered via NoBarrelShadowAPI
        // regardless of plugin load order (see NoBarrelShadowAPI.RegisterLight).
        internal static ConfigFile PluginConfig;

        private void Awake()
        {
            Log = Logger;
            PluginConfig = Config;

            DebugLogging = Config.Bind(
                "1. Settings",
                "Debug Logging",
                false,
                "Enable detailed debug logging to BepInEx/LogOutput.log — use when reporting bugs");

            LoadKnownNames();
            LoadLearnedNames();

            new Harmony("com.vultify.nobarrelshadow").PatchAll(Assembly.GetExecutingAssembly());
            NoBarrelShadowAPI.FlushPending();
            Log.LogInfo("No Barrel Shadow loaded — flashlight shadows disabled.");
        }

        // Binds the Range/Intensity config entries for a single item ID, shared by
        // both the automatic vanilla/WTT catalog pass and third-party API registrations.
        internal static void RegisterLightConfig(string id, string displayName)
        {
            if (RangeMultipliers.ContainsKey(id)) return;

            try
            {
                RangeMultipliers[id] = PluginConfig.Bind(
                    "2. Range Multipliers",
                    $"{displayName} — Range",
                    1.0f,
                    new ConfigDescription(
                        $"Multiplier applied to {displayName}'s light range (1.0 = default, unchanged)",
                        new AcceptableValueRange<float>(0.1f, 5.0f)));

                IntensityMultipliers[id] = PluginConfig.Bind(
                    "3. Intensity Multipliers",
                    $"{displayName} — Intensity",
                    1.0f,
                    new ConfigDescription(
                        $"Multiplier applied to {displayName}'s light intensity (1.0 = default, unchanged)",
                        new AcceptableValueRange<float>(0.1f, 5.0f)));
            }
            catch (System.Exception ex)
            {
                Log.LogWarning($"[NBS] Failed to bind config for '{displayName}' (id={id}): {ex.Message}");
            }
        }

        private static string ModDirectory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";

        private void LoadKnownNames()
        {
            try
            {
                string path = Path.Combine(ModDirectory, "light_names.json");

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _knownNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                    Log.LogInfo($"[NBS] Loaded {_knownNames.Count} known light name(s) from light_names.json");
                }
                else
                {
                    _knownNames = new Dictionary<string, string>();
                    Log.LogWarning("[NBS] light_names.json not found — falling back to codename cleanup for all lights.");
                }
            }
            catch (System.Exception ex)
            {
                _knownNames = new Dictionary<string, string>();
                Log.LogWarning($"[NBS] Failed to load light_names.json: {ex.Message}");
            }
        }

        private void LoadLearnedNames()
        {
            try
            {
                string path = Path.Combine(ModDirectory, LearnedNamesFileName);
                _learnedNames = File.Exists(path)
                    ? (JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>())
                    : new Dictionary<string, string>();

                if (_learnedNames.Count > 0)
                    Log.LogInfo($"[NBS] Loaded {_learnedNames.Count} previously learned light name(s).");
            }
            catch (System.Exception ex)
            {
                _learnedNames = new Dictionary<string, string>();
                Log.LogWarning($"[NBS] Failed to load {LearnedNamesFileName}: {ex.Message}");
            }
        }

        private void Update()
        {
            if (!Singleton<ItemFactoryClass>.Instantiated) return;

            int currentCount = Singleton<ItemFactoryClass>.Instance.ItemTemplates.Count;
            if (currentCount != _lastCataloguedCount)
            {
                _lastCataloguedCount = currentCount;
                CatalogueLights();
            }

            // Only attempt a live locale lookup once we're actually in a raid, where
            // the locale system is reliably populated — attempting this at the same
            // early point as CatalogueLights() is what produced raw echoed-key text.
            if (!_hasAttemptedLearning && Singleton<GameWorld>.Instantiated && Singleton<GameWorld>.Instance.MainPlayer != null)
            {
                _hasAttemptedLearning = true;
                TryLearnUnknownNames();
            }
        }

        private void TryLearnUnknownNames()
        {
            var templates = Singleton<ItemFactoryClass>.Instance.ItemTemplates;
            var unknown = new List<(string Id, string Codename, string ShortName)>();

            foreach (var kvp in templates)
            {
                var template = kvp.Value;
                if (!template.ParentId.HasValue) continue;

                string parentId = template.ParentId.Value.ToString();
                bool isLightCategory = parentId == CategoryFlashlight
                    || parentId == CategoryLightLaser
                    || parentId == CategoryTacticalCombo;
                if (!isLightCategory) continue;

                string codename = template._name;
                if (string.IsNullOrEmpty(codename)) continue;
                if (IsExcludedByCodename(codename)) continue;

                string id = template.StringId;
                if (_knownNames.ContainsKey(id) || _learnedNames.ContainsKey(id)) continue;

                string shortName = template._id.LocalizedShortName();

                // A failed lookup echoes the raw key back (e.g. "<id> ShortName")
                // instead of returning null/empty — treat that as "not ready yet".
                bool looksUnresolved = string.IsNullOrEmpty(shortName)
                    || shortName.Contains(id)
                    || shortName.EndsWith(" ShortName", System.StringComparison.OrdinalIgnoreCase);

                if (looksUnresolved) continue;

                unknown.Add((id, codename, shortName));
            }

            if (unknown.Count == 0) return;

            var shortNameCounts = new Dictionary<string, int>();
            foreach (var u in unknown)
            {
                shortNameCounts.TryGetValue(u.ShortName, out var n);
                shortNameCounts[u.ShortName] = n + 1;
            }

            int learned = 0;
            foreach (var u in unknown)
            {
                string finalName;
                if (shortNameCounts[u.ShortName] == 1)
                {
                    finalName = u.ShortName;
                }
                else
                {
                    string variant = ExtractVariantWord(u.Codename);
                    if (variant == null) continue; // can't safely disambiguate — leave it on codename fallback
                    finalName = $"{u.ShortName} ({variant})";
                }

                _learnedNames[u.Id] = finalName;
                Log.LogInfo($"[NBS] Learned name for '{u.Codename}': {finalName}");
                learned++;
            }

            if (learned > 0)
            {
                try
                {
                    string path = Path.Combine(ModDirectory, LearnedNamesFileName);
                    File.WriteAllText(path, JsonConvert.SerializeObject(_learnedNames, Formatting.Indented));
                    Log.LogInfo($"[NBS] Saved {learned} newly learned light name(s) to {LearnedNamesFileName} — they'll be used starting next launch.");
                }
                catch (System.Exception ex)
                {
                    Log.LogWarning($"[NBS] Failed to save {LearnedNamesFileName}: {ex.Message}");
                }
            }
        }

        private static readonly string[] VariantWords =
        {
            "tan", "black", "blk", "fde", "od", "khaki", "coyote", "foliage",
            "gray", "grey", "green", "grn", "olive", "sand", "digital", "arid",
            "wine", "multicam", "ranger", "desert", "urban"
        };

        // Pulls a recognizable color/variant word out of a codename, e.g.
        // "tactical_all_olight_baldr_pro_tan" -> "Tan". Used only to disambiguate
        // items whose learned ShortName collided with another item's.
        private static string ExtractVariantWord(string codename)
        {
            var tokens = codename.ToLowerInvariant().Split('_', '-');
            foreach (var token in tokens)
            {
                foreach (var word in VariantWords)
                {
                    if (token == word)
                        return char.ToUpper(word[0]) + word.Substring(1);
                }
            }
            return null;
        }

        private void CatalogueLights()
        {
            var templates = Singleton<ItemFactoryClass>.Instance.ItemTemplates;
            int found = 0;
            var usedKeys = new HashSet<string>();

            foreach (var kvp in templates)
            {
                var template = kvp.Value;

                // Diagnostic-only: log anything that looks like a flashlight by name
                // but doesn't match our category filter, so a missing item (like the
                // M600) can be traced to its real parent category instead of guessing.
                if (!string.IsNullOrEmpty(template._name)
                    && template._name.ToLowerInvariant().Contains("m600"))
                {
                    NoBarrelShadowPlugin.DebugLog(
                        $"Non-matching candidate check: name={template._name}, id={template.StringId}, parent={template.ParentId?.ToString() ?? "null"}");
                }

                if (!template.ParentId.HasValue) continue;

                string parentId = template.ParentId.Value.ToString();
                bool isLightCategory = parentId == CategoryFlashlight
                    || parentId == CategoryLightLaser
                    || parentId == CategoryTacticalCombo;

                if (!isLightCategory) continue;

                string id = template.StringId;

                // The internal codename is NOT always unique — a content mod can clone
                // a base item (e.g. the M600 cloning WMX200) without renaming its
                // internal _name field, so two genuinely different items can share one.
                // Only used here for exclusion-fragment matching and the codename
                // fallback name; the template ID is the real uniqueness anchor below.
                string uniqueKey = template._name;

                if (string.IsNullOrEmpty(uniqueKey)) continue;
                if (IsExcludedByCodename(uniqueKey)) continue;

                // Prefer the shipped precomputed name, then anything learned live in a
                // previous session, falling back to codename cleanup only if neither
                // source has this item on file yet.
                string displayName;
                if (_knownNames != null && _knownNames.TryGetValue(id, out var knownName))
                    displayName = knownName;
                else if (_learnedNames != null && _learnedNames.TryGetValue(id, out var learnedName))
                    displayName = learnedName;
                else
                    displayName = CleanCodename(uniqueKey);

                NoBarrelShadowPlugin.DebugLog($"Cataloguing candidate: id={id}, name={displayName}, codename={uniqueKey}, parent={parentId}");

                if (!usedKeys.Add(id))
                {
                    Log.LogWarning($"[NBS] Skipped a flashlight item — duplicate item id '{id}'.");
                    continue;
                }

                RegisterLightConfig(id, displayName);
                found++;
            }

            Log.LogInfo($"[NBS] Catalogued {found} flashlight-type item(s) for range/intensity adjustment.");
        }

        private static readonly string[] CodenamePrefixes =
        {
            "tactical_all_", "tactical_base_", "tactical_",
            "flashlight_base_", "flashlight_all_", "flashlight_",
            "scope_all_", "scope_base_", "base_"
        };

        // Turns an internal codename like "tactical_all_olight_baldr_pro_tan" into a
        // readable label like "Olight Baldr Pro Tan" for the F12 menu. Loops since
        // some codenames stack multiple prefix fragments (e.g. "flashlight_base_...").
        private static string CleanCodename(string codename)
        {
            string name = codename;

            bool strippedSomething;
            do
            {
                strippedSomething = false;
                foreach (var prefix in CodenamePrefixes)
                {
                    if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        name = name.Substring(prefix.Length);
                        strippedSomething = true;
                        break;
                    }
                }
            } while (strippedSomething);

            name = name.Replace('_', ' ').Replace('-', ' ');

            var words = name.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            }

            return string.Join(" ", words);
        }

        internal static void DebugLog(string message)
        {
            // Logged at Info, not Debug — BepInEx's default LogLevels filter excludes
            // Debug severity from disk output, which would silently drop these.
            if (DebugLogging.Value)
                Log.LogInfo($"[NBS Debug] {message}");
        }
    }

    // Public hook for other mods to add their own light-type items to No Barrel
    // Shadow's range/intensity F12 sliders, using their own known display name
    // instead of relying on this mod's codename guessing/exclusion list. Call
    // NoBarrelShadowAPI.RegisterLight(templateId, displayName) from your own
    // plugin's Awake() — safe to call regardless of plugin load order.
    public static class NoBarrelShadowAPI
    {
        private static readonly List<(string Id, string DisplayName)> Pending = new List<(string, string)>();

        public static void RegisterLight(string templateId, string displayName)
        {
            if (string.IsNullOrEmpty(templateId) || string.IsNullOrEmpty(displayName)) return;

            if (NoBarrelShadowPlugin.PluginConfig == null)
            {
                Pending.Add((templateId, displayName));
                return;
            }

            NoBarrelShadowPlugin.RegisterLightConfig(templateId, displayName);
        }

        internal static void FlushPending()
        {
            foreach (var entry in Pending)
                NoBarrelShadowPlugin.RegisterLightConfig(entry.Id, entry.DisplayName);
            Pending.Clear();
        }
    }

    [HarmonyPatch(typeof(TacticalComboVisualController), nameof(TacticalComboVisualController.Init))]
    public static class DisableFlashlightShadowsInitPatch
    {
        [HarmonyPostfix]
        public static void Postfix(TacticalComboVisualController __instance)
        {
            var player = __instance.GetComponentInParent<Player>();
            NoBarrelShadowPlugin.DebugLog($"Init called — player={player?.name ?? "none"}, isYourPlayer={player?.IsYourPlayer}");

            if (player != null && player.IsYourPlayer)
            {
                ShadowHelper.DisableShadows(__instance);
                LightRangeHelper.ApplyMultipliers(__instance);
                NoBarrelShadowPlugin.DebugLog("Shadows disabled and range/intensity applied on Init for player flashlight.");
            }
        }
    }

    [HarmonyPatch(typeof(TacticalComboVisualController), nameof(TacticalComboVisualController.UpdateBeams))]
    public static class DisableFlashlightShadowsPatch
    {
        [HarmonyPostfix]
        public static void Postfix(TacticalComboVisualController __instance, bool isYourPlayer)
        {
            NoBarrelShadowPlugin.DebugLog($"UpdateBeams called — isYourPlayer={isYourPlayer}, instance={__instance?.name ?? "null"}");

            if (isYourPlayer)
            {
                ShadowHelper.DisableShadows(__instance);
                LightRangeHelper.ApplyMultipliers(__instance);
                NoBarrelShadowPlugin.DebugLog("Shadows disabled and range/intensity applied for player flashlight.");
            }
            else
            {
                NoBarrelShadowPlugin.DebugLog("Skipped — not player's weapon.");
            }
        }
    }

    public static class ShadowHelper
    {
        private static readonly FieldInfo LightsField = AccessTools.Field(typeof(TacticalComboVisualController), "light_0");

        public static void DisableShadows(TacticalComboVisualController instance)
        {
            if (LightsField == null) return;

            var lights = LightsField.GetValue(instance) as Light[];
            if (lights == null) return;

            foreach (var light in lights)
            {
                if (light != null && light.shadows != LightShadows.None)
                {
                    NoBarrelShadowPlugin.DebugLog($"Disabling shadows on light '{light.name}' (was {light.shadows})");
                    light.shadows = LightShadows.None;
                }
            }
        }
    }

    public static class LightRangeHelper
    {
        private static readonly FieldInfo LightsField = AccessTools.Field(typeof(TacticalComboVisualController), "light_0");

        // Cached true base values, keyed by the Light component's instance ID, so
        // repeated calls always multiply off the real original rather than
        // compounding on top of an already-adjusted value.
        private static readonly Dictionary<int, float> BaseRange = new Dictionary<int, float>();
        private static readonly Dictionary<int, float> BaseIntensity = new Dictionary<int, float>();

        public static void ApplyMultipliers(TacticalComboVisualController instance)
        {
            if (LightsField == null) return;

            var itemId = instance.LightMod?.Item?.StringTemplateId;
            if (string.IsNullOrEmpty(itemId)) return;

            bool hasRange = NoBarrelShadowPlugin.RangeMultipliers.TryGetValue(itemId, out var rangeEntry);
            bool hasIntensity = NoBarrelShadowPlugin.IntensityMultipliers.TryGetValue(itemId, out var intensityEntry);
            if (!hasRange && !hasIntensity) return;

            var lights = LightsField.GetValue(instance) as Light[];
            if (lights == null) return;

            foreach (var light in lights)
            {
                if (light == null) continue;
                int id = light.GetInstanceID();

                if (!BaseRange.TryGetValue(id, out var baseRange))
                {
                    baseRange = light.range;
                    BaseRange[id] = baseRange;
                }

                if (!BaseIntensity.TryGetValue(id, out var baseIntensity))
                {
                    baseIntensity = light.intensity;
                    BaseIntensity[id] = baseIntensity;
                }

                if (hasRange)
                    light.range = baseRange * rangeEntry.Value;

                if (hasIntensity)
                    light.intensity = baseIntensity * intensityEntry.Value;
            }
        }
    }
}
