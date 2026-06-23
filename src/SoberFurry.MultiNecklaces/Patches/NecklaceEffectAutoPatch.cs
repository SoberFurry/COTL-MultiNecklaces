using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Mono.Cecil;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>
/// Blanket effect-stacking: scans Assembly-CSharp for every method that reads the necklace
/// (the <c>FollowerInfo.Necklace</c> field or <c>FollowerBrainInfo.Necklace</c> getter) and applies
/// the defensive <see cref="NecklaceEffectIL.Rewrite"/> transpiler to each. This makes ALL inline
/// necklace effect checks consider hidden necklaces too, no matter where they live — instead of a
/// hand-picked list that inevitably misses some (e.g. the several sleep checks).
///
/// The transpiler only rewrites real <c>== / !=</c> comparisons against a necklace constant, so
/// methods that merely store/render the necklace are left untouched.
/// </summary>
internal static class NecklaceEffectAutoPatch
{
    public static void ApplyAll(Harmony harmony)
    {
        int patched = 0, scanned = 0, skipped = 0;
        try
        {
            var asm = typeof(FollowerInfo).Assembly;
            var module = asm.ManifestModule;

            string path = asm.Location;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                path = Path.Combine(Paths.ManagedPath, "Assembly-CSharp.dll");
            if (!File.Exists(path)) { Plugin.Log.LogError("[AutoPatch] Assembly-CSharp.dll not found; effects won't fully stack."); return; }

            var rewrite = AccessTools.Method(typeof(NecklaceEffectIL), nameof(NecklaceEffectIL.Rewrite));
            var transpiler = new HarmonyMethod(rewrite);

            var tokens = new List<int>();
            using (var def = AssemblyDefinition.ReadAssembly(path))
            {
                foreach (var type in def.MainModule.GetTypes())
                {
                    foreach (var m in type.Methods)
                    {
                        if (!m.HasBody) continue;
                        if (m.Name == "get_Necklace" || m.Name == "set_Necklace") continue;
                        scanned++;
                        if (!ReadsNecklace(m)) continue;
                        // Skip methods covered by deterministic postfixes/prefixes (no double-count).
                        if (HandledNames.Contains((m.DeclaringType?.Name ?? "") + "." + m.Name)) { skipped++; continue; }
                        tokens.Add(m.MetadataToken.ToInt32());
                    }
                }
            }

            foreach (int tok in tokens)
            {
                try
                {
                    if (module.ResolveMethod(tok) is not MethodInfo mi) continue;
                    if (mi.IsAbstract || mi.ContainsGenericParameters) continue;
                    if (mi.GetMethodBody() == null) continue;
                    harmony.Patch(mi, transpiler: transpiler);
                    patched++;
                }
                catch (Exception e)
                {
                    if (Plugin.Verbose) Plugin.Log.LogWarning($"[AutoPatch] skip token {tok}: {e.Message}");
                }
            }

            Plugin.Log.LogInfo($"[AutoPatch] hidden-effect stacking applied to {patched} method(s); {skipped} handled deterministically.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[AutoPatch] failed (effects may not fully stack): {e}");
        }
    }

    // Methods covered by deterministic postfixes/prefixes (EffectPatches + GuaranteedSleepPatch).
    // Matched by Cecil's own DeclaringType.Name + "." + method name (getters are "get_X").
    private static readonly HashSet<string> HandledNames = new()
    {
        "FollowerInfo.GetDemonLevel",
        "FollowerInfo.HasTraitFromNecklace",
        "FollowerInfo.get_WorkThroughNight",
        "FollowerBrain.get_DevotionToGive",
        "FollowerBrain.get_ResourceHarvestingMultiplier",
        "FollowerBrain.CanFreeze",
        "FollowerBrain.MakeDissenter",
        "FollowerBrainInfo.get_LifeExpectancy",
        "FollowerBrainInfo.get_ProductivityMultiplier",
    };

    private static bool ReadsNecklace(MethodDefinition m)
    {
        foreach (var ins in m.Body.Instructions)
        {
            switch (ins.Operand)
            {
                case FieldReference fr when fr.Name == "Necklace" && fr.DeclaringType?.Name == "FollowerInfo":
                    return true;
                case MethodReference mr when mr.Name == "get_Necklace" && mr.DeclaringType?.Name == "FollowerBrainInfo":
                    return true;
            }
        }
        return false;
    }
}
