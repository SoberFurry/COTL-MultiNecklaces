#if COTL_API
using System;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using COTL_API.CustomSettings;
using Lamb.UI;

namespace SoberFurry.MultiNecklaces;

/// <summary>
/// OPTIONAL integration with COTL_API: if COTL_API is installed, the mod's settings are added to the
/// in-game "Mods" settings tab. If COTL_API is absent, this is never called and the mod simply uses
/// its BepInEx .cfg file. Compiled only when COTL_API is available at build time (COTL_API constant).
/// </summary>
internal static class CotlApiIntegration
{
    private const string ApiGuid = "io.github.xhayper.COTL_API";

    public static void TryRegisterSettings()
    {
        try
        {
            if (Plugin.Cfg == null || !Plugin.Cfg.UseInGameSettings.Value) return; // off by default (Mods tab can freeze on some builds)
            if (!Chainloader.PluginInfos.ContainsKey(ApiGuid)) return; // COTL_API not loaded -> cfg only
            Register();
            Plugin.Log.LogInfo("Settings registered in the in-game COTL_API \"Mods\" tab.");
        }
        catch (Exception e)
        {
            Plugin.Log.LogWarning($"COTL_API settings registration failed (the .cfg still works): {e.Message}");
        }
    }

    private static void Register()
    {
        var c = Plugin.Cfg;
        string cat = Localizer.Get("settings.category");
        CustomSettingsManager.AddBepInExConfig(cat, Localizer.Get("settings.enabled"), c.Enabled);
        CustomSettingsManager.AddBepInExConfig(cat, Localizer.Get("settings.effectstacking"), c.EffectStacking);
        CustomSettingsManager.AddBepInExConfig(cat, Localizer.Get("settings.confirmunequip"), c.ConfirmRareUnequip);
        CustomSettingsManager.AddBepInExConfig(cat, Localizer.Get("settings.verbose"), c.VerboseLogging);
        CustomSettingsManager.AddBepInExConfig(cat, Localizer.Get("settings.maxunique"),
            c.MaximumUniqueNecklaces, 1, MMSlider.ValueDisplayFormat.RawValue);
        AddEnumDropdown(cat, Localizer.Get("settings.butcher"), c.ButcherReturnMode);
        AddEnumDropdown(cat, Localizer.Get("settings.unknownpolicy"), c.UnknownNecklacePolicy);
    }

    private static void AddEnumDropdown<T>(string category, string name, ConfigEntry<T> entry) where T : struct, Enum
    {
        var values = (T[])Enum.GetValues(typeof(T));
        var names = Enum.GetNames(typeof(T));
        CustomSettingsManager.AddDropdown(category, name, entry.Value.ToString(), names, idx =>
        {
            if (idx >= 0 && idx < values.Length) entry.Value = values[idx];
        });
    }
}
#endif
