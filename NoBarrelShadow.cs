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

        // every flashlight-type attachment falls under one of these three parents
        private const string CategoryFlashlight = "55818b084bdc2d5b648b4571";
        private const string CategoryLightLaser = "55818b0e4bdc2dde698b456e";
        private const string CategoryTacticalCombo = "55818b164bdc2ddc698b456c";

        // laser/IR-only units, no beam to boost; fragments not ids, color variants get their own ids
        private static readonly string[] ExcludedCodenameFragments =
        {
            "anpeq",     // AN/PEQ-15, AN/PEQ-2
            "la5",       // LA-5B, internally "insight_la5" not "la5b"
            "dbal",      // DBAL series minus the DBAL-PL, handled below
            "perst",     // Perst-3
            "mawl",      // MAWL-C1+
            "ls321",     // Holosun LS321
            "dlp",       // TT DLP, laser only
            "ncstar",    // NcSTAR blue laser
            "raptar",    // RAPTAR ES rangefinder
            "2irs",      // Klesch-2IRS, internally "2irs" not "2iks"
        };

        // dbal_pl has a real LED flashlight, keep it out of the dbal fragment
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

        // content mods add items late, recheck when the count changes
        private int _lastCataloguedCount = -1;

        internal static readonly Dictionary<string, ConfigEntry<float>> RangeMultipliers = new Dictionary<string, ConfigEntry<float>>();
        internal static readonly Dictionary<string, ConfigEntry<float>> IntensityMultipliers = new Dictionary<string, ConfigEntry<float>>();

        // precomputed offline, the locale system isn't ready this early
        private static Dictionary<string, string> _knownNames;

        // learned in-raid where locale actually works, saved so each name is learned once ever
        private static Dictionary<string, string> _learnedNames;
        private static readonly string LearnedNamesFileName = "light_names_learned.json";
        private bool _hasAttemptedLearning;

        // NoBarrelShadowAPI registrations, any plugin load order
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

        // shared by the catalog pass and API registrations
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

            // locale lookups only resolve in-raid, earlier attempts echo raw keys
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

                // failed lookup echoes the key back instead of returning null
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
                    if (variant == null) continue; // no safe disambiguator, stays on codename fallback
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

        // color word from the codename, only for splitting shortname collisions
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

                // diagnostic, the m600 once hid under an unexpected parent category
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

                // codenames aren't unique, clones keep the donor's _name, the id is the anchor
                string uniqueKey = template._name;

                if (string.IsNullOrEmpty(uniqueKey)) continue;
                if (IsExcludedByCodename(uniqueKey)) continue;

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

        // codename to F12 label; loops because some codenames stack prefixes
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
            // Info not Debug, the default BepInEx filter drops Debug from disk
            if (DebugLogging.Value)
                Log.LogInfo($"[NBS Debug] {message}");
        }
    }

    // other mods register their lights here with their own display name, call from Awake, any load order
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

        // always multiply off the true original, never compound
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
