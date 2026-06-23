using System;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Deterministic, reliable effect-stacking for the necklaces whose effect lives in a clean method or
/// property — adds the HIDDEN necklace's contribution directly (no IL guesswork). The generic IL
/// auto-patch (<see cref="NecklaceEffectAutoPatch"/>) skips these methods to avoid double-counting and
/// covers only the remaining inline-coded effects (speed, rot, death-save, targeting, etc.).
/// </summary>
[HarmonyPatch]
internal static class EffectPatches
{
    private static bool On => Plugin.Cfg != null && Plugin.Cfg.Enabled.Value && Plugin.Cfg.EffectStacking.Value;

    private static bool HiddenInfo(FollowerInfo i, InventoryItem.ITEM_TYPE t)
        => i != null && i.Necklace != t && NecklaceService.Instance.Has(i.ID, t);

    private static bool HiddenBrain(FollowerBrainInfo b, InventoryItem.ITEM_TYPE t)
        => b != null && b.Necklace != t && NecklaceService.Instance.Has(b.ID, t);

    // +2 demon level — Necklace_Demonic
    [HarmonyPatch(typeof(FollowerInfo), nameof(FollowerInfo.GetDemonLevel))]
    [HarmonyPostfix]
    private static void DemonLevel(FollowerInfo __instance, ref int __result)
    {
        try { if (On && HiddenInfo(__instance, InventoryItem.ITEM_TYPE.Necklace_Demonic)) __result += 2; }
        catch (Exception e) { Plugin.Log.LogWarning($"DemonLevel: {e.Message}"); }
    }

    // Immortality — Necklace_Gold_Skull
    [HarmonyPatch(typeof(FollowerInfo), nameof(FollowerInfo.HasTraitFromNecklace))]
    [HarmonyPostfix]
    private static void Immortal(FollowerInfo __instance, FollowerTrait.TraitType trait, ref bool __result)
    {
        try
        {
            if (__result || !On) return;
            if (trait == FollowerTrait.TraitType.Immortal && __instance.ID != 666 && __instance.ID != 10009
                && HiddenInfo(__instance, InventoryItem.ITEM_TYPE.Necklace_Gold_Skull))
                __result = true;
        }
        catch (Exception e) { Plugin.Log.LogWarning($"Immortal: {e.Message}"); }
    }

    // +devotion — Necklace_1
    [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.DevotionToGive), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Devotion(FollowerBrain __instance, ref float __result)
    {
        try { if (On && __result >= 1f && HiddenInfo(__instance._directInfoAccess, InventoryItem.ITEM_TYPE.Necklace_1)) __result += 0.15f; }
        catch (Exception e) { Plugin.Log.LogWarning($"Devotion: {e.Message}"); }
    }

    // +resource gathering — Necklace_4
    [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.ResourceHarvestingMultiplier), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Harvest(FollowerBrain __instance, ref float __result)
    {
        try { if (On && HiddenInfo(__instance._directInfoAccess, InventoryItem.ITEM_TYPE.Necklace_4)) __result += 0.25f; }
        catch (Exception e) { Plugin.Log.LogWarning($"Harvest: {e.Message}"); }
    }

    // No freezing — Necklace_Frozen
    [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.CanFreeze))]
    [HarmonyPostfix]
    private static void Freeze(FollowerBrain __instance, ref bool __result)
    {
        try { if (__result && On && HiddenInfo(__instance._directInfoAccess, InventoryItem.ITEM_TYPE.Necklace_Frozen)) __result = false; }
        catch (Exception e) { Plugin.Log.LogWarning($"Freeze: {e.Message}"); }
    }

    // Loyalty: do not become a dissenter — Necklace_Loyalty
    [HarmonyPatch(typeof(FollowerBrain), nameof(FollowerBrain.MakeDissenter))]
    [HarmonyPrefix]
    private static bool MakeDissenter(FollowerBrain __instance, bool ignoreImmunity)
    {
        try
        {
            if (!On || ignoreImmunity) return true;
            if (HiddenInfo(__instance._directInfoAccess, InventoryItem.ITEM_TYPE.Necklace_Loyalty))
                return false; // immune via hidden loyalty necklace, like the vanilla visible case
        }
        catch (Exception e) { Plugin.Log.LogWarning($"MakeDissenter: {e.Message}"); }
        return true;
    }

    // Lifespan x2 — Necklace_3
    [HarmonyPatch(typeof(FollowerBrainInfo), nameof(FollowerBrainInfo.LifeExpectancy), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Lifespan(FollowerBrainInfo __instance, ref int __result)
    {
        try { if (On && HiddenBrain(__instance, InventoryItem.ITEM_TYPE.Necklace_3)) __result *= 2; }
        catch (Exception e) { Plugin.Log.LogWarning($"Lifespan: {e.Message}"); }
    }

    // Productivity in winter — Necklace_Winter
    [HarmonyPatch(typeof(FollowerBrainInfo), nameof(FollowerBrainInfo.ProductivityMultiplier), MethodType.Getter)]
    [HarmonyPostfix]
    private static void Productivity(FollowerBrainInfo __instance, ref float __result)
    {
        try
        {
            if (On && SeasonsManager.CurrentSeason == SeasonsManager.Season.Winter
                && HiddenBrain(__instance, InventoryItem.ITEM_TYPE.Necklace_Winter))
                __result += 0.5f;
        }
        catch (Exception e) { Plugin.Log.LogWarning($"Productivity: {e.Message}"); }
    }
}
