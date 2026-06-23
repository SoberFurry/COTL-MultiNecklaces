using System;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Deterministic guarantee for the "moon" necklace (Necklace_5 = works through the night / no sleep):
/// forces <c>WorkThroughNight</c> true when the follower has Necklace_5 equipped even while HIDDEN.
/// This is a belt-and-suspenders over the generic IL effect-stacking (in case the rewrite missed this
/// exact getter), using the same equipped-set lookup.
/// </summary>
[HarmonyPatch(typeof(FollowerInfo), nameof(FollowerInfo.WorkThroughNight), MethodType.Getter)]
internal static class GuaranteedSleepPatch
{
    [HarmonyPostfix]
    private static void Postfix(FollowerInfo __instance, ref bool __result)
    {
        try
        {
            if (__result) return; // already "works through night"
            if (Plugin.Cfg == null || !Plugin.Cfg.Enabled.Value || !Plugin.Cfg.EffectStacking.Value) return;
            if (__instance == null) return;
            if (__instance.Necklace != InventoryItem.ITEM_TYPE.Necklace_5
                && NecklaceService.Instance.Has(__instance.ID, InventoryItem.ITEM_TYPE.Necklace_5))
            {
                __result = true;
                if (Plugin.Verbose)
                    Plugin.Log.LogInfo($"[Sleep] follower {__instance.ID}: hidden moon necklace -> works through night.");
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"WorkThroughNight postfix failed: {e.Message}"); }
    }
}
