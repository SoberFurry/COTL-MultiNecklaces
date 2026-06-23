using System;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;

namespace SoberFurry.MultiNecklaces.Patches;

/// <summary>Loads our per-slot data when the game loads a save slot.</summary>
[HarmonyPatch]
internal static class SaveLoadPatches
{
    [HarmonyPatch(typeof(SaveAndLoad), nameof(SaveAndLoad.Load), new[] { typeof(int) })]
    [HarmonyPostfix]
    private static void Load_Postfix(int saveSlot)
    {
        try
        {
            NecklaceService.Instance.Load();
            // One-time reconcile of already-loaded followers (no per-frame polling needed afterwards).
            try
            {
                var dm = DataManager.Instance;
                if (dm?.Followers != null)
                    foreach (var info in dm.Followers)
                        if (info != null) NecklaceService.Instance.ApplyVisibleToVanilla(info);
            }
            catch { }
            Plugin.Log.LogInfo($"Mod loadouts loaded for save slot {saveSlot}.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"SaveAndLoad.Load postfix failed: {e}");
        }
    }

    [HarmonyPatch(typeof(SaveAndLoad), nameof(SaveAndLoad.Save), new[] { typeof(string) })]
    [HarmonyPostfix]
    private static void Save_Postfix()
    {
        try { NecklaceService.Instance.Persist(); }
        catch (Exception e) { Plugin.Log.LogError($"SaveAndLoad.Save postfix failed: {e}"); }
    }
}
