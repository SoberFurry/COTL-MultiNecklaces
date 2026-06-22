using System;
using System.Security.Permissions;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using SoberFurry.MultiNecklaces.Core;
using SoberFurry.MultiNecklaces.UI;
using UnityEngine;

#pragma warning disable CS0618
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace SoberFurry.MultiNecklaces;

[BepInPlugin(Guid, "SoberFurry MultiNecklaces", "1.1.0")]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.soberfurry.cultofthelamb.multinecklaces";
    private const string LogPrefix = "SoberFurry.MultiNecklaces";

    internal static ManualLogSource Log = null!;
    internal static PluginConfig Cfg = null!;
    internal static bool Verbose => Cfg != null && Cfg.VerboseLogging.Value;

    private Harmony _harmony = null!;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"[{LogPrefix}] starting v1.1.0");
        try
        {
            Cfg = new PluginConfig(base.Config);
            Localizer.Init(Log);

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly); // discovers all [HarmonyPatch] classes (incl. effect-stacking IL patches)

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
