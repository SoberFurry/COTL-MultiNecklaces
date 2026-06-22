using System;
using System.Collections.Generic;
using System.Linq;
using SoberFurry.MultiNecklaces.Models;
using UnityEngine;

namespace SoberFurry.MultiNecklaces.Core;

internal readonly struct OpResult
{
    public readonly bool Ok;
    public readonly string MessageKey;
    private OpResult(bool ok, string key) { Ok = ok; MessageKey = key; }
    public static OpResult Success(string key = "op.ok") => new(true, key);
    public static OpResult Fail(string key) => new(false, key);
}

/// <summary>
/// Central in-memory authority for multi-necklace loadouts. The vanilla <c>FollowerInfo.Necklace</c>
/// field is kept equal to the VISIBLE necklace (the bridge), so vanilla rendering and the visible
/// necklace's native effect keep working with no extra patching (double-apply protection by design).
/// </summary>
internal sealed class NecklaceService
{
    public static NecklaceService Instance { get; } = new();

    private readonly SaveRepository _repo = new();
    private MultiNecklacesSave _save = new();
    private int _slot;
    private long _equipCounter;
    private bool _loaded;

    public InventoryItem.ITEM_TYPE NoneType => InventoryItem.ITEM_TYPE.NONE;

    public void Load()
    {
        _slot = _repo.CurrentSlot;
        _save = _repo.LoadOrCreate(_slot);
        _repo.BackupVanillaSlotOnce(_slot);
        _equipCounter = _save.Followers.Values
            .SelectMany(l => l.Equipped)
            .Select(e => e.EquipOrder)
            .DefaultIfEmpty(0).Max();
        _loaded = true;
        Plugin.Log.LogInfo($"Loaded loadouts for slot {_slot}: {_save.Followers.Count} followers, revision {_save.Revision}.");
    }

    public void Persist()
    {
        if (!_loaded) return;
        _repo.Save(_save, _slot);
    }

    private static bool ValidId(int id) => id > 0;

    private NecklaceLoadout? Find(int id) =>
        _save.Followers.TryGetValue(id.ToString(), out var l) ? l : null;

    private NecklaceLoadout GetOrCreate(int id)
    {
        string key = id.ToString();
        if (!_save.Followers.TryGetValue(key, out var l))
        {
            l = new NecklaceLoadout();
            _save.Followers[key] = l;
        }
        return l;
    }

    // ---- Queries ------------------------------------------------------------

    public bool Has(int id, InventoryItem.ITEM_TYPE type)
    {
        var l = Find(id);
        if (l == null) return false;
        string name = NecklaceTypes.Name(type);
        return l.Equipped.Any(e => e.NecklaceId == name && e.State != EntryState.PendingConsumption);
    }

    public IReadOnlyList<InventoryItem.ITEM_TYPE> GetEquipped(int id)
    {
        var l = Find(id);
        if (l == null) return Array.Empty<InventoryItem.ITEM_TYPE>();
        var result = new List<InventoryItem.ITEM_TYPE>();
        foreach (var e in l.Equipped.OrderBy(e => e.EquipOrder))
            if (NecklaceTypes.TryParse(e.NecklaceId, out var t)) result.Add(t);
        return result;
    }

    public IReadOnlyList<NecklaceEntry> GetEntries(int id) =>
        Find(id)?.Equipped.OrderBy(e => e.EquipOrder).ToList() ?? new List<NecklaceEntry>();

    public InventoryItem.ITEM_TYPE? GetVisible(int id)
    {
        var l = Find(id);
        if (l?.VisibleNecklaceId == null) return null;
        return NecklaceTypes.TryParse(l.VisibleNecklaceId, out var t) ? t : null;
    }

    /// <summary>Visible necklace if any, else the last-shown one, else the first equipped — for the wheel icon.</summary>
    public InventoryItem.ITEM_TYPE? GetVisibleOrLast(int id)
    {
        var l = Find(id);
        if (l == null) return null;
        if (l.VisibleNecklaceId != null && NecklaceTypes.TryParse(l.VisibleNecklaceId, out var v)) return v;
        if (l.LastVisibleNecklaceId != null && NecklaceTypes.TryParse(l.LastVisibleNecklaceId, out var lv)
            && l.Equipped.Exists(e => e.NecklaceId == l.LastVisibleNecklaceId)) return lv;
        foreach (var e in l.Equipped)
            if (NecklaceTypes.TryParse(e.NecklaceId, out var f)) return f;
        return null;
    }

    public int Count(int id) => Find(id)?.Equipped.Count ?? 0;

    // ---- Import / sync with vanilla ----------------------------------------

    /// <summary>If vanilla holds a necklace we don't track, import it (first = visible) without
    /// removing hidden ones. Handles fresh saves and other mods changing the vanilla field.</summary>
    public void EnsureImported(FollowerInfo info)
    {
        if (info == null || !ValidId(info.ID)) return;
        var vanilla = info.Necklace;
        if (!NecklaceTypes.IsNecklace(vanilla)) return;

        var l = GetOrCreate(info.ID);
        string name = NecklaceTypes.Name(vanilla);
        if (l.Equipped.All(e => e.NecklaceId != name))
        {
            l.Equipped.Add(new NecklaceEntry { NecklaceId = name, EquipOrder = ++_equipCounter });
            Touch(l);
            Plugin.Log.LogInfo($"[Import] follower {info.ID}: imported vanilla necklace {name} (loadout now {l.Equipped.Count}).");
            Persist();
        }
        if (string.IsNullOrEmpty(l.VisibleNecklaceId))
        {
            l.VisibleNecklaceId = name;
            Touch(l);
        }
    }

    // ---- Mutations ----------------------------------------------------------

    public OpResult Equip(FollowerInfo info, InventoryItem.ITEM_TYPE type, bool fromInventory)
    {
        if (info == null || !ValidId(info.ID)) return OpResult.Fail("err.badFollower");
        if (!NecklaceTypes.IsNecklace(type)) return OpResult.Fail("err.notNecklace");
        if (Has(info.ID, type)) return OpResult.Fail("err.duplicate"); // uniqueness BEFORE consuming

        int max = Plugin.Cfg.MaximumUniqueNecklaces.Value;
        if (max > 0 && Count(info.ID) >= max) return OpResult.Fail("err.limit");

        if (fromInventory)
        {
            if (Inventory.GetItemQuantity(type) <= 0) return OpResult.Fail("err.noItem");
            Inventory.ChangeItemQuantity(type, -1);
        }

        var l = GetOrCreate(info.ID);
        l.Equipped.Add(new NecklaceEntry { NecklaceId = NecklaceTypes.Name(type), EquipOrder = ++_equipCounter });

        // First necklace becomes visible.
        if (string.IsNullOrEmpty(l.VisibleNecklaceId))
            SetVisibleInternal(info, l, type);

        Touch(l);
        Persist();
        return OpResult.Success("op.equipped");
    }

    public OpResult Unequip(FollowerInfo info, InventoryItem.ITEM_TYPE type, bool toInventory)
    {
        if (info == null || !ValidId(info.ID)) return OpResult.Fail("err.badFollower");
        var l = Find(info.ID);
        string name = NecklaceTypes.Name(type);
        var entry = l?.Equipped.FirstOrDefault(e => e.NecklaceId == name);
        if (l == null || entry == null) return OpResult.Fail("err.notEquipped");

        l.Equipped.Remove(entry);
        if (toInventory) Inventory.ChangeItemQuantity(type, 1);

        // If we removed the visible one, pick a new visible (last by order, else none).
        if (l.VisibleNecklaceId == name)
        {
            var next = l.Equipped.OrderBy(e => e.EquipOrder).LastOrDefault();
            if (next != null && NecklaceTypes.TryParse(next.NecklaceId, out var nt))
                SetVisibleInternal(info, l, nt);
            else
                SetVisibleInternal(info, l, InventoryItem.ITEM_TYPE.NONE);
        }

        Touch(l);
        Persist();
        return OpResult.Success("op.unequipped");
    }

    public OpResult SetVisible(FollowerInfo info, InventoryItem.ITEM_TYPE type)
    {
        if (info == null || !ValidId(info.ID)) return OpResult.Fail("err.badFollower");
        var l = Find(info.ID);
        if (l == null || !Has(info.ID, type)) return OpResult.Fail("err.notEquipped");
        SetVisibleInternal(info, l, type);
        Touch(l);
        Persist();
        return OpResult.Success("op.visible");
    }

    /// <summary>Hide whatever is currently shown so nothing renders on the model (loadout untouched).</summary>
    public OpResult HideVisible(FollowerInfo info)
    {
        if (info == null || !ValidId(info.ID)) return OpResult.Fail("err.badFollower");
        var l = Find(info.ID);
        if (l == null) return OpResult.Fail("err.notEquipped");
        SetVisibleInternal(info, l, InventoryItem.ITEM_TYPE.NONE);
        Touch(l);
        Persist();
        return OpResult.Success("op.hidden");
    }

    private void SetVisibleInternal(FollowerInfo info, NecklaceLoadout l, InventoryItem.ITEM_TYPE type)
    {
        l.VisibleNecklaceId = type == InventoryItem.ITEM_TYPE.NONE ? null : NecklaceTypes.Name(type);
        if (type != InventoryItem.ITEM_TYPE.NONE) l.LastVisibleNecklaceId = NecklaceTypes.Name(type);
        // Bridge: vanilla field always mirrors the visible necklace.
        info.Necklace = type;
        info.ShowingNecklace = type != InventoryItem.ITEM_TYPE.NONE;
        RefreshVisual(info);
    }

    /// <summary>Re-asserts the vanilla bridge field from our data (used on load / after sync).</summary>
    public void ApplyVisibleToVanilla(FollowerInfo info)
    {
        if (info == null || !ValidId(info.ID)) return;
        var vis = GetVisible(info.ID);
        if (vis.HasValue)
        {
            info.Necklace = vis.Value;
            info.ShowingNecklace = true;
        }
    }

    private static void RefreshVisual(FollowerInfo info)
    {
        try
        {
            var follower = Follower.Followers?.FirstOrDefault(f => f != null && f.Brain != null && f.Brain.Info != null && f.Brain.Info.ID == info.ID);
            if (follower != null && follower.Spine != null && follower.Spine.Skeleton != null)
                FollowerBrain.SetFollowerCostume(follower.Spine.Skeleton, info, hooded: false, forceUpdate: true);
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"RefreshVisual failed for {info.ID} (visual will update on next costume refresh): {e.Message}");
        }
    }

    // ---- Butchering (drop all remaining, idempotent) ------------------------

    /// <summary>
    /// Called when butchering of <paramref name="info"/> completes. Vanilla already dropped the
    /// visible necklace and set FollowerInfo.Necklace = NONE; we drop every remaining (hidden)
    /// necklace exactly once, clear the record, and journal it so a repeated callback is a no-op.
    /// </summary>
    public void OnButcherComplete(FollowerInfo info, InventoryItem.ITEM_TYPE alreadyDroppedVisible, Vector3 position)
    {
        if (info == null || !ValidId(info.ID)) return;
        string key = info.ID.ToString();

        var l = Find(info.ID);
        if (l == null) return;

        // Idempotency: if we already committed a butcher for this follower at this loadout, skip.
        if (_save.ButcherJournal.TryGetValue(key, out var prior) && prior.Status == "Committed")
        {
            Plugin.Log.LogInfo($"[Butcher] duplicate callback for {info.ID} ignored (already committed).");
            return;
        }

        // Snapshot what to drop = everything still equipped that wasn't the already-dropped visible.
        string visibleName = NecklaceTypes.Name(alreadyDroppedVisible);
        var toDrop = l.Equipped
            .Where(e => e.State != EntryState.PendingConsumption)
            .Select(e => e.NecklaceId)
            .ToList();

        // Remove exactly one occurrence of the visible (vanilla already dropped it).
        if (alreadyDroppedVisible != InventoryItem.ITEM_TYPE.NONE)
            toDrop.Remove(visibleName);

        var journal = new OperationJournalEntry
        {
            FollowerId = info.ID,
            Revision = _save.Revision,
            NecklaceIds = toDrop.ToList(),
            Status = "Started"
        };
        _save.ButcherJournal[key] = journal;
        Persist();

        int dropped = 0;
        foreach (var name in toDrop)
        {
            if (!NecklaceTypes.TryParse(name, out var t)) continue;
            try
            {
                var spread = position + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(-0.4f, 0.4f), -0.5f);
                bool spawned = false;
                try { InventoryItem.Spawn(t, 1, spread); spawned = true; }
                catch (Exception spawnEx) { Plugin.Log.LogWarning($"[Butcher] physical drop failed for {name}: {spawnEx.Message}"); }
                if (!spawned)
                {
                    // Fallback: put it straight into the inventory so nothing is lost.
                    Inventory.ChangeItemQuantity(t, 1);
                }
                dropped++;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Butcher] failed to drop {name} for {info.ID}: {e}");
            }
        }

        // Clear the loadout for the butchered follower and commit the journal.
        _save.Followers.Remove(key);
        journal.Status = "Committed";
        _save.ButcherJournal[key] = journal;
        Persist();
        Plugin.Log.LogInfo($"[Butcher] follower {info.ID}: dropped {dropped} hidden necklace(s) (visible already dropped by vanilla). Record cleared.");
    }

    // ---- Removal-prep command ----------------------------------------------

    /// <summary>Prepare for uninstalling the mod: return every HIDDEN necklace to the inventory and
    /// leave only the visible one on each follower, so the vanilla save stays valid afterwards.</summary>
    public int PrepareForRemoval()
    {
        int returned = 0;
        foreach (var kv in _save.Followers.ToList())
        {
            var l = kv.Value;
            string? vis = l.VisibleNecklaceId;
            foreach (var e in l.Equipped.ToList())
            {
                if (e.NecklaceId == vis) continue;
                if (NecklaceTypes.TryParse(e.NecklaceId, out var t))
                {
                    Inventory.ChangeItemQuantity(t, 1);
                    returned++;
                }
                l.Equipped.Remove(e);
            }
            Touch(l);
        }
        Persist();
        Plugin.Log.LogInfo($"PrepareForRemoval: returned {returned} hidden necklace(s) to inventory.");
        return returned;
    }

    private static void Touch(NecklaceLoadout l) => l.LastUpdatedUtc = DateTime.UtcNow.ToString("o");
}
