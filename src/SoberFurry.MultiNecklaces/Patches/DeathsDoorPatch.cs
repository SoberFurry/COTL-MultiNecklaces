using System;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Keeps the one-shot "Death's Door" necklace (Necklace_Deaths_Door) one-shot.
///
/// Vanilla <c>FollowerBrain.Die</c> saves the follower from death if their VISIBLE necklace is
/// Death's Door, then consumes it (sets Necklace = NONE). We let that happen (Die is excluded from
/// hidden-effect stacking so it only triggers for the visible one — avoids consuming the wrong
/// necklace), and here we remove the consumed Death's Door from our loadout so it isn't re-applied
/// (which would otherwise make it an infinite save + duplicate).
/// </summary>
[HarmonyPatch]
internal static class DeathsDoorPatch
{
    [HarmonyPatch(typeof(FollowerBrain), "Die", new[] { typeof(NotificationCentre.NotificationType) })]
    [HarmonyPrefix]
    private static void Die_Prefix(FollowerBrain __instance, out bool __state)
    {
        __state = false;
        try
        {
            var info = __instance?._directInfoAccess;
            __state = info != null && info.Necklace == InventoryItem.ITEM_TYPE.Necklace_Deaths_Door;
        }
        catch { }
    }

    [HarmonyPatch(typeof(FollowerBrain), "Die", new[] { typeof(NotificationCentre.NotificationType) })]
    [HarmonyPostfix]
    private static void Die_Postfix(FollowerBrain __instance, bool __state)
    {
        try
        {
            if (!__state) return;
            var info = __instance?._directInfoAccess;
            if (info == null) return;
            // Vanilla saved the follower and cleared the necklace -> consume Death's Door from our data
            // and promote the next necklace as visible (do not drop it; it was spent).
            if (info.Necklace == InventoryItem.ITEM_TYPE.NONE)
            {
                NecklaceService.Instance.Unequip(info, InventoryItem.ITEM_TYPE.Necklace_Deaths_Door, toInventory: false);
                Plugin.Log.LogInfo($"[Deaths_Door] consumed on follower {info.ID} (one-shot).");
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"Deaths_Door consume failed: {e.Message}"); }
    }
}
