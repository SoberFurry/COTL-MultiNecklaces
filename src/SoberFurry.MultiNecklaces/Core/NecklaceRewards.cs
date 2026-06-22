using System;

namespace SoberFurry.MultiNecklaces.Core;

/// <summary>
/// Mirrors the vanilla "gift necklace" / "remove necklace" loyalty effects for our extra-necklace
/// paths: giving a necklace grants the follower adoration + a positive cult thought; removing one
/// adds the negative "removed necklace" thought.
/// </summary>
internal static class NecklaceRewards
{
    public static void OnEquip(Follower follower, InventoryItem.ITEM_TYPE type)
    {
        try { follower?.Brain?.AddAdoration(FollowerBrain.AdorationActions.Necklace, () => { }); }
        catch (Exception e) { Plugin.Log.LogWarning($"AddAdoration failed: {e.Message}"); }
        try { CultFaithManager.AddThought(Thought.Cult_GaveFollowerItem, -1, 7f, InventoryItem.LocalizedName(type)); }
        catch (Exception e) { Plugin.Log.LogWarning($"GaveFollowerItem thought failed: {e.Message}"); }
    }

    public static void OnRemove(int followerId)
    {
        try { CultFaithManager.AddThought(Thought.Cult_RemovedFollowerNecklace, followerId, 1f); }
        catch (Exception e) { Plugin.Log.LogWarning($"RemovedFollowerNecklace thought failed: {e.Message}"); }
    }
}
