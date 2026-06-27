using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NoBarrelShadow
{
    [BepInPlugin("com.vultify.nobarrelshadow", "No Barrel Shadow", "1.0.0")]
    public class NoBarrelShadowPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            new Harmony("com.vultify.nobarrelshadow").PatchAll(Assembly.GetExecutingAssembly());
            Log.LogInfo("No Barrel Shadow loaded — flashlight shadows disabled.");
        }
    }

    [HarmonyPatch(typeof(TacticalComboVisualController), nameof(TacticalComboVisualController.Init))]
    public static class DisableFlashlightShadowsInitPatch
    {
        [HarmonyPostfix]
        public static void Postfix(TacticalComboVisualController __instance)
        {
            ShadowHelper.DisableShadows(__instance);
        }
    }

    [HarmonyPatch(typeof(TacticalComboVisualController), nameof(TacticalComboVisualController.UpdateBeams))]
    public static class DisableFlashlightShadowsUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(TacticalComboVisualController __instance)
        {
            ShadowHelper.DisableShadows(__instance);
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
                    light.shadows = LightShadows.None;
                }
            }
        }
    }
}
