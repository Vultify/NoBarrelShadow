using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace NoBarrelShadow
{
    [BepInPlugin("com.vultify.nobarrelshadow", "No Barrel Shadow", "1.0.1")]
    public class NoBarrelShadowPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        public static ConfigEntry<bool> DebugLogging;

        private void Awake()
        {
            Log = Logger;

            DebugLogging = Config.Bind(
                "Settings",
                "Debug Logging",
                false,
                "Enable detailed debug logging to BepInEx/LogOutput.log — use when reporting bugs");

            new Harmony("com.vultify.nobarrelshadow").PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("No Barrel Shadow loaded — flashlight shadows disabled.");
        }

        internal static void DebugLog(string message)
        {
            if (DebugLogging.Value)
                Log.LogDebug($"[NBS Debug] {message}");
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
                NoBarrelShadowPlugin.DebugLog("Shadows disabled on Init for player flashlight.");
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
                NoBarrelShadowPlugin.DebugLog("Shadows disabled for player flashlight.");
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
}
