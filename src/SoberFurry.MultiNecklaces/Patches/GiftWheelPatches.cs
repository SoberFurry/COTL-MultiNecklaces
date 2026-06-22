using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;
using UnityEngine;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Makes the normal follower interaction wheel support multiple necklaces.
///
/// Vanilla <c>interaction_FollowerInteraction.GiveItemRoutine</c> refuses with "AlreadyHaveNecklace"
/// when the follower already wears one, and <c>RemoveNecklaceRoutine</c> clears the single vanilla
/// field. We:
///   - on giving a necklace to a follower that already has one: equip it as an extra (hidden) via the
///     service (consumes 1 from inventory), instead of the vanilla refusal;
///   - on removing: drop the VISIBLE necklace and promote the next one, keeping our data in sync.
/// The first necklace on a bare follower still goes through vanilla (then the sync imports it).
/// </summary>
[HarmonyPatch]
internal static class GiftWheelPatches
{
    private static readonly MethodBase? CloseMethod =
        AccessTools.Method(typeof(interaction_FollowerInteraction), "Close");

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "GiveItemRoutine")]
    [HarmonyPrefix]
    private static bool GiveItemRoutine_Prefix(interaction_FollowerInteraction __instance,
        InventoryItem.ITEM_TYPE itemToGive, ref IEnumerator __result)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value) return true;
            if (!NecklaceTypes.IsNecklace(itemToGive)) return true;

            var f = __instance.follower;
            var info = f?.Brain?._directInfoAccess;
            if (info == null) return true;

            // First necklace on a bare follower -> let vanilla equip it (sync will import it).
            if (info.Necklace == InventoryItem.ITEM_TYPE.NONE) return true;

            // Same type already equipped -> let vanilla refuse with its "AlreadyHaveNecklace" dialog.
            if (NecklaceService.Instance.Has(info.ID, itemToGive)) return true;

            // A different necklace -> add it as an additional one through the service.
            __result = MultiEquipRoutine(__instance, f!, info, itemToGive);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"GiveItemRoutine prefix failed (vanilla allowed): {e}");
            return true;
        }
    }

    private static IEnumerator MultiEquipRoutine(interaction_FollowerInteraction inst, Follower f,
        FollowerInfo info, InventoryItem.ITEM_TYPE type)
    {
        var r = NecklaceService.Instance.Equip(info, type, fromInventory: true);
        Plugin.Log.LogInfo($"[Gift] extra necklace {type} on follower {info.ID}: ok={r.Ok} ({r.MessageKey})");
        if (r.Ok)
        {
            try { AudioManager.Instance.PlayOneShot("event:/followers/gain_loyalty", f.transform.position); } catch { }
            NecklaceRewards.OnEquip(f, info, type); // loyalty/adoration like vanilla (first necklace only)
        }
        yield return new WaitForSeconds(0.1f);
        CloseInteraction(inst);
    }

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "RemoveNecklaceRoutine")]
    [HarmonyPrefix]
    private static bool RemoveNecklaceRoutine_Prefix(interaction_FollowerInteraction __instance, ref IEnumerator __result)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value) return true;
            var f = __instance.follower;
            var info = f?.Brain?._directInfoAccess;
            if (info == null) return true;

            // Only take over when WE track this follower's necklaces (else let vanilla do its thing).
            if (NecklaceService.Instance.Count(info.ID) < 1) return true;
            var visible = NecklaceService.Instance.GetVisible(info.ID);
            if (!visible.HasValue) return true;

            __result = RemoveVisibleRoutine(__instance, f!, info, visible.Value);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"RemoveNecklaceRoutine prefix failed (vanilla allowed): {e}");
            return true;
        }
    }

    private static IEnumerator RemoveVisibleRoutine(interaction_FollowerInteraction inst, Follower f,
        FollowerInfo info, InventoryItem.ITEM_TYPE visible)
    {
        try { InventoryItem.Spawn(visible, 1, inst.transform.position + Vector3.back * 0.5f); } catch { }
        // Remove from loadout and promote the next necklace as visible (no inventory change here:
        // we already spawned the physical pickup, mirroring vanilla RemoveNecklace).
        var r = NecklaceService.Instance.Unequip(info, visible, toInventory: false);
        if (r.Ok) NecklaceRewards.OnRemove(info.ID); // negative thought like vanilla remove
        Plugin.Log.LogInfo($"[Remove] visible necklace {visible} from follower {info.ID}: ok={r.Ok}. Remaining={NecklaceService.Instance.Count(info.ID)}");
        try { f.UpdateOutfit(); } catch { }
        yield return new WaitForSeconds(0.2f);
        CloseInteraction(inst);
    }

    private static void CloseInteraction(interaction_FollowerInteraction inst)
    {
        try { CloseMethod?.Invoke(inst, null); }
        catch (Exception e) { Plugin.Log.LogWarning($"Could not close interaction cleanly: {e.Message}"); }
    }
}
