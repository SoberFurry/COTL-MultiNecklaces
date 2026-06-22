using System;

namespace SoberFurry.MultiNecklaces.Core;

/// <summary>
/// Mirrors the vanilla "gift necklace" / "remove necklace" loyalty effects for our extra-necklace
/// paths: giving a necklace grants the follower adoration + a positive cult thought; removing one
/// adds the negative "removed necklace" thought.
/// </summary>
internal static class NecklaceRewards
{
    public static void OnEquip(Follower follower, FollowerInfo info, InventoryItem.ITEM_TYPE type)
    {
        // Vanilla grants follower adoration (XP / level-up) ONLY for the first necklace a follower
        // ever receives (the HasReceivedNecklace flag). Without this gate, give -> remove -> give
        // would farm levels. Mirror the vanilla rule to prevent that exploit.
        try
        {
            if (info != null && !info.HasReceivedNecklace)
            {
                follower?.Brain?.AddAdoration(FollowerBrain.AdorationActions.Necklace, () => { });
                info.HasReceivedNecklace = true;
            }
        }
        catch (Exception e) { Plugin.Log.LogWarning($"AddAdoration failed: {e.Message}"); }

        // The small cult-faith thought for giving an item is fine to keep (it is not follower XP).
        try { CultFaithManager.AddThought(Thought.Cult_GaveFollowerItem, -1, 7f, InventoryItem.LocalizedName(type)); }
        catch (Exception e) { Plugin.Log.LogWarning($"GaveFollowerItem thought failed: {e.Message}"); }
    }

    public static void OnRemove(int followerId)
    {
        try { CultFaithManager.AddThought(Thought.Cult_RemovedFollowerNecklace, followerId, 1f); }
        catch (Exception e) { Plugin.Log.LogWarning($"RemovedFollowerNecklace thought failed: {e.Message}"); }
    }
}
