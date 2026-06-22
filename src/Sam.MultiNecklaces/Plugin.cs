using System;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Sam.MultiNecklaces.Core;
using Sam.MultiNecklaces.UI;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Sam.MultiNecklaces;

[BepInPlugin(Guid, "Sam MultiNecklaces", "1.0.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.sam.cultofthelamb.multinecklaces";
    private const string LogPrefix = "Sam.MultiNecklaces";

    internal static ManualLogSource Log = null!;
    internal static PluginConfig Cfg = null!;
    internal static bool Verbose => Cfg != null && Cfg.VerboseLogging.Value;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"[{LogPrefix}] starting v1.0.0");
        try
        {
            Cfg = new PluginConfig(base.Config);
            Localizer.Init(Log);

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Patches.EffectPatches));
            _harmony.PatchAll(typeof(Patches.ButcheringPatches));
            _harmony.PatchAll(typeof(Patches.SaveLoadPatches));
            _harmony.PatchAll(typeof(Patches.GiftWheelPatches));
            _harmony.PatchAll(typeof(Patches.NecklaceWheelPatches));
            _harmony.PatchAll(typeof(Patches.SummaryPatches));

            ManagementUI.Create();

            // Persist on quit as a safety net.
            Application.quitting += () => { try { NecklaceService.Instance.Persist(); } catch { } };

            int patched = 0;
            foreach (var _ in _harmony.GetPatchedMethods()) patched++;
            Log.LogInfo($"[{LogPrefix}] loaded. Enabled={Cfg.Enabled.Value}, EffectStacking={Cfg.EffectStacking.Value}, " +
                        $"MaxUnique={Cfg.MaximumUniqueNecklaces.Value}, PanelHotkey={Cfg.PanelHotkey.Value}, patchedMethods={patched}. " +
                        $"Necklace types in build: {NecklaceTypes.All.Count}.");
        }
        catch (Exception e)
        {
            Log.LogError($"[{LogPrefix}] FATAL during Awake (mod inert, vanilla game unaffected): {e}");
        }
    }

    private void OnDestroy()
    {
        try { NecklaceService.Instance.Persist(); } catch { }
        try { _harmony?.UnpatchSelf(); } catch { }
    }
}
