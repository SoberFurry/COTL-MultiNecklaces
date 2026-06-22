using System;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Effect-stacking for the necklaces whose effect is read through a clean instance method on
/// FollowerInfo. The VISIBLE necklace is already applied by vanilla through the bridge field, so we
/// only add the contribution of HIDDEN necklaces (no double-apply).
///
/// Other necklace effects are inline field comparisons scattered across the codebase; in v1 those
/// apply only while the necklace is the visible one (documented limitation).
/// </summary>
[HarmonyPatch]
internal static class EffectPatches
{
    [HarmonyPatch(typeof(FollowerInfo), nameof(FollowerInfo.GetDemonLevel))]
    [HarmonyPostfix]
    private static void GetDemonLevel_Postfix(FollowerInfo __instance, ref int __result)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value || !Plugin.Cfg.EffectStacking.Value) return;
            // Vanilla already added +2 if the VISIBLE necklace is Demonic; add it for a HIDDEN one.
            if (__instance.Necklace != InventoryItem.ITEM_TYPE.Necklace_Demonic
                && NecklaceService.Instance.Has(__instance.ID, InventoryItem.ITEM_TYPE.Necklace_Demonic))
            {
                __result += 2;
            }
        }
        catch (Exception e) { Plugin.Log.LogError($"GetDemonLevel patch failed: {e}"); }
    }

    [HarmonyPatch(typeof(FollowerInfo), nameof(FollowerInfo.HasTraitFromNecklace))]
    [HarmonyPostfix]
    private static void HasTraitFromNecklace_Postfix(FollowerInfo __instance, FollowerTrait.TraitType trait, ref bool __result)
    {
        try
        {
            if (__result) return;
            if (!Plugin.Cfg.Enabled.Value || !Plugin.Cfg.EffectStacking.Value) return;
            if (trait == FollowerTrait.TraitType.Immortal
                && __instance.ID != 666 && __instance.ID != 10009
                && NecklaceService.Instance.Has(__instance.ID, InventoryItem.ITEM_TYPE.Necklace_Gold_Skull))
            {
                __result = true;
            }
        }
        catch (Exception e) { Plugin.Log.LogError($"HasTraitFromNecklace patch failed: {e}"); }
    }

    // Devotion bonus from Necklace_1 (vanilla adds +0.15 for the visible one; add it for a hidden one).
    [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.DevotionToGive), MethodType.Getter)]
    [HarmonyPostfix]
    private static void DevotionToGive_Postfix(FollowerBrain __instance, ref float __result)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value || !Plugin.Cfg.EffectStacking.Value) return;
            var info = __instance._directInfoAccess;
            if (info == null || __result < 1f) return; // <1 means Spy (0); don't touch
            if (info.Necklace != InventoryItem.ITEM_TYPE.Necklace_1
                && NecklaceService.Instance.Has(info.ID, InventoryItem.ITEM_TYPE.Necklace_1))
                __result += 0.15f;
        }
        catch (Exception e) { Plugin.Log.LogError($"DevotionToGive patch failed: {e}"); }
    }

    // Resource harvesting bonus from Necklace_4 ("nature" / gather more).
    [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.ResourceHarvestingMultiplier), MethodType.Getter)]
    [HarmonyPostfix]
    private static void ResourceHarvestingMultiplier_Postfix(FollowerBrain __instance, ref float __result)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value || !Plugin.Cfg.EffectStacking.Value) return;
            var info = __instance._directInfoAccess;
            if (info == null) return;
            if (info.Necklace != InventoryItem.ITEM_TYPE.Necklace_4
                && NecklaceService.Instance.Has(info.ID, InventoryItem.ITEM_TYPE.Necklace_4))
                __result += 0.25f;
        }
        catch (Exception e) { Plugin.Log.LogError($"ResourceHarvestingMultiplier patch failed: {e}"); }
    }

    // Freeze protection from Necklace_Frozen.
    [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.CanFreeze))]
    [HarmonyPostfix]
    private static void CanFreeze_Postfix(FollowerBrain __instance, ref bool __result)
    {
        try
        {
            if (!__result) return;
            if (!Plugin.Cfg.Enabled.Value || !Plugin.Cfg.EffectStacking.Value) return;
            var info = __instance._directInfoAccess;
            if (info != null && NecklaceService.Instance.Has(info.ID, InventoryItem.ITEM_TYPE.Necklace_Frozen))
                __result = false;
        }
        catch (Exception e) { Plugin.Log.LogError($"CanFreeze patch failed: {e}"); }
    }
}
