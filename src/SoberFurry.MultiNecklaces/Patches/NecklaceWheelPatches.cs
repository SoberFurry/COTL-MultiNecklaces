using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lamb.UI.FollowerInteractionWheel;
using SoberFurry.MultiNecklaces.Core;
using UnityEngine;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Necklace management inside the follower's radial command wheel.
///
/// L1: "Necklaces" entry (icon = visible necklace, or the last shown one if all are hidden).
/// L2: "Gift a necklace" (pinned first) + one slice per equipped necklace (icon = necklace,
///     sub-icon = eye if visible / crossed-eye if hidden; hover = name + effect).
/// L3: that necklace's actions — Make visible / Hide (for the visible one) / Remove.
///
/// Every item gets a non-None Command id (the wheel hides the title/centre text for Command==None),
/// so hover text works. Leaf actions are decoded in OnFollowerCommandFinalized.
/// </summary>
[HarmonyPatch]
internal static class NecklaceWheelPatches
{
    private const int ShowBase = 920000;
    private const int RemoveBase = 1020000;
    private const int HideBase = 1120000;
    private const int GroupBase = 1220000; // groups: non-None, never finalized (they open sub-wheels)
    private const int RangeSize = 100000;

    private static readonly MethodInfo? CloseMethod =
        AccessTools.Method(typeof(interaction_FollowerInteraction), "Close", new[] { typeof(bool), typeof(bool), typeof(bool) });

    private static string L(string key) => Localizer.Get(key);

    private sealed class WheelLeaf : CommandItem
    {
        public string Title = "";
        public string Desc = "";
        public string Icon = "";
        public override string GetTitle(Follower follower) => Title;
        public override string GetDescription(Follower follower) => Desc;
        public override string GetIcon() => Icon;
    }

    private sealed class WheelGroup : CommandItem
    {
        public InventoryItem.ITEM_TYPE IconType = InventoryItem.ITEM_TYPE.NONE;
        public string Title = "";
        public string Desc = "";
        public override string GetTitle(Follower follower) => Title;
        public override string GetDescription(Follower follower) => Desc;
        public override string GetIcon() =>
            IconType == InventoryItem.ITEM_TYPE.NONE ? base.GetIcon() : FontImageNames.GetIconByType(IconType);
        // SubCommand (inherited) drives GetSubIcon() -> eye / crossed-eye overlay.
    }

    [HarmonyPatch(typeof(FollowerCommandGroups), nameof(FollowerCommandGroups.DefaultCommands))]
    [HarmonyPostfix]
    private static void DefaultCommands_Postfix(Follower follower, ref List<CommandItem> __result)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value) return;
            var info = follower?.Brain?._directInfoAccess;
            if (info == null || info.ID <= 0 || __result == null) return;

            var svc = NecklaceService.Instance;
            svc.EnsureImported(info);
            svc.ApplyVisibleToVanilla(info); // reconcile model to our data right before showing the menu
            var entries = svc.GetEntries(info.ID);
            if (entries.Count == 0) return;

            __result.RemoveAll(c => c is FollowerCommandItems.RemoveNecklaceCommandItem
                                 || c is FollowerCommandItems.HideNecklaceCommandItem
                                 || c is FollowerCommandItems.ShowNecklaceCommandItem);

            var visible = svc.GetVisible(info.ID);
            var perNecklace = new List<CommandItem>();

            // "Gift a necklace" pinned first.
            perNecklace.Add(new WheelGroup
            {
                Command = (FollowerCommands)(GroupBase + 1),
                IconType = InventoryItem.ITEM_TYPE.GIFT_SMALL,
                Title = L("wheel.gift"),
                Desc = L("wheel.giftdesc"),
                SubCommands = FollowerCommandGroups.GiftCommands(follower)
            });

            foreach (var e in entries)
            {
                if (!NecklaceTypes.TryParse(e.NecklaceId, out var t)) continue;
                bool isVis = visible.HasValue && visible.Value == t;
                string name = SafeName(t);
                string effect = SafeDesc(t);
                string status = isVis ? L("wheel.visible") : L("wheel.hidden");

                var actions = new List<CommandItem>();
                if (isVis)
                {
                    actions.Add(new WheelLeaf
                    {
                        Command = (FollowerCommands)(HideBase + (int)t),
                        Icon = FontImageNames.IconForCommand(FollowerCommands.Hide),
                        Title = L("wheel.hide"),
                        Desc = $"{name}: {effect}"
                    });
                }
                else
                {
                    actions.Add(new WheelLeaf
                    {
                        Command = (FollowerCommands)(ShowBase + (int)t),
                        Icon = FontImageNames.IconForCommand(FollowerCommands.Show),
                        Title = L("wheel.show"),
                        Desc = $"{name}: {effect}"
                    });
                }
                actions.Add(new WheelLeaf
                {
                    Command = (FollowerCommands)(RemoveBase + (int)t),
                    Icon = FontImageNames.IconForCommand(FollowerCommands.RemoveItem),
                    Title = L("wheel.remove"),
                    Desc = $"{name}: {effect}"
                });

                perNecklace.Add(new WheelGroup
                {
                    Command = (FollowerCommands)(GroupBase + 100 + (int)t),
                    IconType = t,
                    SubCommand = isVis ? FollowerCommands.Show : FollowerCommands.Hide,
                    Title = $"{name} [{status}]",
                    Desc = $"{name}: {effect}",
                    SubCommands = actions
                });
            }

            __result.Add(new WheelGroup
            {
                Command = (FollowerCommands)GroupBase,
                IconType = svc.GetVisibleOrLast(info.ID) ?? FirstType(entries),
                Title = L("wheel.title"),
                Desc = L("wheel.rootdesc"),
                SubCommands = perNecklace
            });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"DefaultCommands postfix failed (wheel left vanilla): {ex}");
        }
    }

    [HarmonyPatch(typeof(interaction_FollowerInteraction), "OnFollowerCommandFinalized")]
    [HarmonyPrefix]
    private static bool OnFollowerCommandFinalized_Prefix(interaction_FollowerInteraction __instance,
        FollowerCommands[] followerCommands)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value || followerCommands == null || followerCommands.Length == 0) return true;
            int id = (int)followerCommands[0];
            bool isShow = id >= ShowBase && id < ShowBase + RangeSize;
            bool isRemove = id >= RemoveBase && id < RemoveBase + RangeSize;
            bool isHide = id >= HideBase && id < HideBase + RangeSize;
            if (!isShow && !isRemove && !isHide) return true;

            var f = __instance.follower;
            var info = f?.Brain?._directInfoAccess;
            if (info == null) return true;
            var svc = NecklaceService.Instance;

            if (isShow)
            {
                var type = (InventoryItem.ITEM_TYPE)(id - ShowBase);
                var r = svc.SetVisible(info, type);
                Plugin.Log.LogInfo($"[Wheel] show {type} on {info.ID}: ok={r.Ok}");
            }
            else if (isHide)
            {
                var r = svc.HideVisible(info);
                try { f!.UpdateOutfit(); } catch { }
                Plugin.Log.LogInfo($"[Wheel] hide all on {info.ID}: ok={r.Ok}");
            }
            else
            {
                var type = (InventoryItem.ITEM_TYPE)(id - RemoveBase);
                try { InventoryItem.Spawn(type, 1, __instance.transform.position + Vector3.back * 0.5f); } catch { }
                var r = svc.Unequip(info, type, toInventory: false);
                if (r.Ok) NecklaceRewards.OnRemove(info.ID); // negative thought like vanilla remove
                try { f!.UpdateOutfit(); } catch { }
                Plugin.Log.LogInfo($"[Wheel] remove {type} on {info.ID}: ok={r.Ok}, remaining={svc.Count(info.ID)}");
            }

            CloseWheel(__instance);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"OnFollowerCommandFinalized prefix failed (vanilla allowed): {ex}");
            return true;
        }
    }

    private static void CloseWheel(interaction_FollowerInteraction inst)
    {
        try { CloseMethod?.Invoke(inst, new object[] { false, false, true }); }
        catch (Exception e) { Plugin.Log.LogWarning($"Could not close wheel: {e.Message}"); }
    }

    private static string SafeName(InventoryItem.ITEM_TYPE t)
    {
        try { var n = InventoryItem.LocalizedName(t); if (!string.IsNullOrWhiteSpace(n)) return n; } catch { }
        return t.ToString().Replace("Necklace_", "").Replace("_", " ");
    }

    private static string SafeDesc(InventoryItem.ITEM_TYPE t)
    {
        try { var d = InventoryItem.LocalizedDescription(t); if (!string.IsNullOrWhiteSpace(d)) return d; } catch { }
        return "";
    }

    private static InventoryItem.ITEM_TYPE FirstType(IReadOnlyList<Models.NecklaceEntry> entries)
    {
        foreach (var e in entries)
            if (NecklaceTypes.TryParse(e.NecklaceId, out var t)) return t;
        return InventoryItem.ITEM_TYPE.NONE;
    }
}
