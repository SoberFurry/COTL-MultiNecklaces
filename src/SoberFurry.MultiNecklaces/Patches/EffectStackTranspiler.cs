using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Transpiler helper used by <see cref="NecklaceEffectAutoPatch"/>. Rewrites every necklace check
/// so it considers ALL equipped necklaces (visible + hidden), making hidden-necklace effects stack.
///
/// Reads happen two ways:
///   A) <c>ldfld FollowerInfo.Necklace</c>             (direct field)
///   B) <c>callvirt FollowerBrainInfo.get_Necklace</c> (property wrapper)
/// In both cases the instance is on the stack before the load. We turn the load into a Nop (leaving
/// the instance) and turn the following comparison (<c>ceq | beq | bne.un</c>) into a call to a helper
/// returning true when the follower has that necklace equipped — visible OR hidden. Covers == and !=.
///
/// Defensive: methods without the pattern are unchanged; honours the EffectStacking config toggle.
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

            list[i].opcode = OpCodes.Nop;
            list[i].operand = null;

            if (isCeq)
            {
                cmp.opcode = OpCodes.Call;
                cmp.operand = helper;
            }
            else
            {
                var target = cmp.operand;
                cmp.opcode = OpCodes.Call;
                cmp.operand = helper;
                list.Insert(i + 3, new CodeInstruction(isBeq ? OpCodes.Brtrue : OpCodes.Brfalse, target));
            }
        }
        return list;
    }
}
