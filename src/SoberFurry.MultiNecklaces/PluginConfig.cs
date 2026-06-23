using BepInEx.Configuration;
using UnityEngine;

namespace SoberFurry.MultiNecklaces;

internal enum ButcherReturn
{
    PhysicalDropWithInventoryFallback,
    InventoryOnly
}

internal enum UnknownPolicy
{
    StoreAndWarn,
    Hide
}

internal sealed class PluginConfig
{
    public readonly ConfigEntry<bool> Enabled;
    public readonly ConfigEntry<int> MaximumUniqueNecklaces;
    public readonly ConfigEntry<bool> ConfirmRareUnequip;
    public readonly ConfigEntry<ButcherReturn> ButcherReturnMode;
    public readonly ConfigEntry<UnknownPolicy> UnknownNecklacePolicy;
    public readonly ConfigEntry<bool> EffectStacking;
    public readonly ConfigEntry<bool> VerboseLogging;
    public readonly ConfigEntry<KeyCode> PanelHotkey;
    public readonly ConfigEntry<bool> UseInGameSettings;

    public PluginConfig(ConfigFile cfg)
    {
        Enabled = cfg.Bind("General", "Enabled", true, "Master switch for the whole mod.");

        MaximumUniqueNecklaces = cfg.Bind("General", "MaximumUniqueNecklaces", 0,
            new ConfigDescription("Max different necklaces per follower. 0 = no artificial limit.",
                new AcceptableValueRange<int>(0, 50)));

        ConfirmRareUnequip = cfg.Bind("General", "ConfirmRareUnequip", true,
            "Require a confirmation click before unequipping rare/story necklaces.");

        ButcherReturnMode = cfg.Bind("General", "ButcherReturnMode", ButcherReturn.PhysicalDropWithInventoryFallback,
            "How remaining necklaces are returned when a body is butchered.");

        UnknownNecklacePolicy = cfg.Bind("General", "UnknownNecklacePolicy", UnknownPolicy.StoreAndWarn,
            "What to do with a stored necklace id unknown to this game build.");

        EffectStacking = cfg.Bind("Effects", "EffectStacking", true,
            "Apply the effects of HIDDEN necklaces too (currently: Demonic demon-level, Gold_Skull immortality).");

        VerboseLogging = cfg.Bind("Diagnostics", "VerboseLogging", false, "Extra diagnostic logging.");

        PanelHotkey = cfg.Bind("UI", "PanelHotkey", KeyCode.F8,
            "Toggles the necklace management panel for the follower nearest the player.");

        UseInGameSettings = cfg.Bind("UI", "UseInGameSettings", false,
            "Register settings into COTL_API's in-game 'Mods' tab. OFF by default: on some game builds " +
            "that tab can freeze the game. Settings are always editable here in this .cfg file.");
    }
}
