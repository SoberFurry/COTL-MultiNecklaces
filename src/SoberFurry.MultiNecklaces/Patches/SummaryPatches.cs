using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Lamb.UI;
using SoberFurry.MultiNecklaces.Core;
using TMPro;
using UnityEngine;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Shows EVERY equipped necklace (visible and hidden) in the "Read thoughts" / follower summary
/// screen, instead of only the single visible one. Effects of hidden necklaces are applied via
/// EffectPatches; this makes them visible to the player too.
/// </summary>
[HarmonyPatch]
internal static class SummaryPatches
{
    private static readonly FieldInfo? FollowerField =
        AccessTools.Field(typeof(UIFollowerSummaryMenuController), "_follower");
    private static readonly FieldInfo? NecklaceTextField =
        AccessTools.Field(typeof(UIFollowerSummaryMenuController), "Necklace");

    [HarmonyPatch(typeof(UIFollowerSummaryMenuController), "OnShowStarted")]
    [HarmonyPostfix]
    private static void OnShowStarted_Postfix(UIFollowerSummaryMenuController __instance)
    {
        try
        {
            if (!Plugin.Cfg.Enabled.Value || FollowerField == null || NecklaceTextField == null) return;

            var follower = FollowerField.GetValue(__instance) as Follower;
            var info = follower?.Brain?._directInfoAccess;
            if (info == null) return;
            if (NecklaceTextField.GetValue(__instance) is not TextMeshProUGUI text) return;

            var entries = NecklaceService.Instance.GetEntries(info.ID);
            if (entries.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var e in entries)
            {
                if (!NecklaceTypes.TryParse(e.NecklaceId, out var t)) continue;
                string icon = FontImageNames.GetIconByType(t);
                string name = InventoryItem.LocalizedName(t);
                string desc = InventoryItem.LocalizedDescription(t);
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"{icon} {name}: {desc}");
            }
            if (sb.Length == 0) return;

            text.gameObject.SetActive(true);
            text.text = sb.ToString();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Summary necklace list patch failed: {e}");
        }
    }
}
