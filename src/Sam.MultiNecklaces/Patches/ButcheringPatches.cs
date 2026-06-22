using System;
using HarmonyLib;
using Sam.MultiNecklaces.Core;
using UnityEngine;

namespace Sam.MultiNecklaces.Patches;

/// <summary>
/// Drops every remaining (hidden) necklace once butchering of a body completes.
///
/// Vanilla's butchering coroutine (Interaction_HarvestMeat.HarvestMeatIE) drops the single visible
/// necklace and then calls the non-coroutine <c>RemoveTraitGivenByItem()</c> at exactly that point,
/// before setting Necklace = NONE. We postfix RemoveTraitGivenByItem: the visible necklace is already
/// dropped, so we drop the rest exactly once (idempotency journal guards repeated callbacks).
/// </summary>
[HarmonyPatch]
internal static class ButcheringPatches
{
    [HarmonyPatch(typeof(Interaction_HarvestMeat), "RemoveTraitGivenByItem")]
    [HarmonyPostfix]
    private static void RemoveTraitGivenByItem_Postfix(Interaction_HarvestMeat __instance)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value) return;
            var dead = __instance.DeadWorshipper;
            var info = dead != null ? dead.followerInfo : null;
            if (info == null) return;

            // info.Necklace still holds the just-dropped visible necklace (NONE is set after this call).
            Vector3 pos = __instance.transform.position;
            NecklaceService.Instance.OnButcherComplete(info, info.Necklace, pos);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Butchering postfix failed: {e}");
        }
    }
}
