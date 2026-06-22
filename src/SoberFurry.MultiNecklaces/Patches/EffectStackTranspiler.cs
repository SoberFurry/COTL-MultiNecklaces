using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Generic IL rewrite that makes the game's inline necklace checks consider ALL equipped necklaces
/// (visible + hidden), so hidden-necklace effects stack.
///
/// The game reads the necklace two ways:
///   A) <c>ldfld FollowerInfo.Necklace</c>            (direct field)
///   B) <c>callvirt FollowerBrainInfo.get_Necklace</c> (property wrapper — the common case)
/// In both cases the instance is on the stack before the load. We turn the load into a Nop (leaving
/// the instance) and turn the following comparison (<c>ceq | beq | bne.un</c>) into a call to a helper
/// that returns true when the follower has that necklace equipped — visible OR hidden. This covers
/// both <c>== X</c> and <c>!= X</c>.
///
/// Fully defensive: methods without the pattern are left untouched; honours the EffectStacking toggle.
/// </summary>
internal static class NecklaceEffectIL
{
    public static bool HasTypeInt(FollowerInfo info, int typeValue)
    {
        try
        {
            if (info == null) return false;
            var type = (InventoryItem.ITEM_TYPE)typeValue;
            if (info.Necklace == type) return true;
            if (!Stacking) return false;
            return NecklaceService.Instance.Has(info.ID, type);
        }
        catch { return info != null && info.Necklace == (InventoryItem.ITEM_TYPE)typeValue; }
    }

    public static bool HasTypeBrain(FollowerBrainInfo bi, int typeValue)
    {
        try
        {
            if (bi == null) return false;
            var type = (InventoryItem.ITEM_TYPE)typeValue;
            if (bi.Necklace == type) return true;
            if (!Stacking) return false;
            return NecklaceService.Instance.Has(bi.ID, type);
        }
        catch { return bi != null && bi.Necklace == (InventoryItem.ITEM_TYPE)typeValue; }
    }

    private static bool Stacking =>
        Plugin.Cfg != null && Plugin.Cfg.Enabled.Value && Plugin.Cfg.EffectStacking.Value;

    private static readonly FieldInfo NecklaceField = AccessTools.Field(typeof(FollowerInfo), nameof(FollowerInfo.Necklace));
    private static readonly MethodInfo NecklaceGetter = AccessTools.PropertyGetter(typeof(FollowerBrainInfo), nameof(FollowerBrainInfo.Necklace));
    private static readonly MethodInfo HelperInt = AccessTools.Method(typeof(NecklaceEffectIL), nameof(HasTypeInt));
    private static readonly MethodInfo HelperBrain = AccessTools.Method(typeof(NecklaceEffectIL), nameof(HasTypeBrain));

    private static bool IsLdcI4(CodeInstruction ci)
    {
        var op = ci.opcode;
        return op == OpCodes.Ldc_I4 || op == OpCodes.Ldc_I4_S
            || op == OpCodes.Ldc_I4_0 || op == OpCodes.Ldc_I4_1 || op == OpCodes.Ldc_I4_2
            || op == OpCodes.Ldc_I4_3 || op == OpCodes.Ldc_I4_4 || op == OpCodes.Ldc_I4_5
            || op == OpCodes.Ldc_I4_6 || op == OpCodes.Ldc_I4_7 || op == OpCodes.Ldc_I4_8
            || op == OpCodes.Ldc_I4_M1;
    }

    public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
    {
        var list = new List<CodeInstruction>(instructions);
        int rewritten = 0;

        for (int i = 0; i + 2 < list.Count; i++)
        {
            MethodInfo? helper = null;
            if (NecklaceField != null && list[i].opcode == OpCodes.Ldfld && Equals(list[i].operand, NecklaceField))
                helper = HelperInt;
            else if (NecklaceGetter != null && (list[i].opcode == OpCodes.Callvirt || list[i].opcode == OpCodes.Call)
                     && Equals(list[i].operand, NecklaceGetter))
                helper = HelperBrain;
            if (helper == null) continue;

            if (!IsLdcI4(list[i + 1])) continue;

            var cmp = list[i + 2];
            bool isCeq = cmp.opcode == OpCodes.Ceq;
            bool isBeq = cmp.opcode == OpCodes.Beq || cmp.opcode == OpCodes.Beq_S;
            bool isBne = cmp.opcode == OpCodes.Bne_Un || cmp.opcode == OpCodes.Bne_Un_S;
            if (!isCeq && !isBeq && !isBne) continue;

            // necklace load -> Nop (instance stays on the stack); labels preserved.
            list[i].opcode = OpCodes.Nop;
            list[i].operand = null;

            if (isCeq)
            {
                cmp.opcode = OpCodes.Call;
                cmp.operand = helper;
            }
            else
            {
                var target = cmp.operand; // branch Label
                cmp.opcode = OpCodes.Call;
                cmp.operand = helper;
                list.Insert(i + 3, new CodeInstruction(isBeq ? OpCodes.Brtrue : OpCodes.Brfalse, target));
            }
            rewritten++;
        }

        if (rewritten > 0) Plugin.Log.LogInfo($"[EffectIL] rewrote {rewritten} necklace check(s).");
        return list;
    }
}

// ---- Methods that hold inline necklace effects ----

[HarmonyPatch(typeof(Follower), "UpdateMovement")]                                       // speed (Necklace_2)
internal static class Patch_Follower_UpdateMovement { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrain), "Tick", new[] { typeof(float) })]                   // Weird (rot), Winter (snowman)
internal static class Patch_FollowerBrain_Tick { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrain), "Die", new[] { typeof(NotificationCentre.NotificationType) })] // Deaths_Door
internal static class Patch_FollowerBrain_Die { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrain), "MakeDissenter", new[] { typeof(bool) })]           // Loyalty
internal static class Patch_FollowerBrain_MakeDissenter { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrain), "GetPersonalTask", new[] { typeof(FollowerLocation) })] // sleep (Necklace_5)
internal static class Patch_FollowerBrain_GetPersonalTask { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrain), "RandomAvailableBrainToFreeze")]                    // Targeted
internal static class Patch_FollowerBrain_RandomFreeze { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrain), "GetAvailableBrainsWithNecklaceTargeted")]          // Targeted
internal static class Patch_FollowerBrain_TargetedList { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrainInfo), "LifeExpectancy", MethodType.Getter)]           // lifespan x2 (Necklace_3)
internal static class Patch_FollowerBrainInfo_LifeExpectancy { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }

[HarmonyPatch(typeof(FollowerBrainInfo), "ProductivityMultiplier", MethodType.Getter)]   // Winter productivity
internal static class Patch_FollowerBrainInfo_Productivity { [HarmonyTranspiler] static IEnumerable<CodeInstruction> T(IEnumerable<CodeInstruction> i) => NecklaceEffectIL.Rewrite(i); }
