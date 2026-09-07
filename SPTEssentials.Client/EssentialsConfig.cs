using BepInEx.Configuration;
using UnityEngine;

namespace SPTEssentials.Client;

internal sealed class EssentialsConfig
{
    internal ConfigEntry<bool> EnableRaidPause { get; }
    internal ConfigEntry<bool> KeepQuickReloadMagazines { get; }
    internal ConfigEntry<bool> EnableFieldArmorRepair { get; }
    internal ConfigEntry<bool> EnableSmartAirFilter { get; }
    internal ConfigEntry<bool> EnableExpandedSpecialSlots { get; }
    internal ConfigEntry<bool> EnableCompatibilityRules { get; }
    internal ConfigEntry<bool> EnableCompactHud { get; }

    internal ConfigEntry<KeyboardShortcut> PauseShortcut { get; }

    internal EssentialsConfig(ConfigFile config)
    {
        EnableFieldArmorRepair = config.Bind(
            "01 - Modules",
            "Enable Field Repair",
            true,
            "Allows the Field Armor Repair Kit to repair compatible armor during a raid. Restart SPT after changing this option."
        );
        KeepQuickReloadMagazines = config.Bind(
            "01 - Modules",
            "Enable Quickload Mag Saver",
            true,
            "Places the old magazine in a free inventory grid during quick reload. It still drops when no space is available."
        );
        EnableRaidPause = config.Bind(
            "01 - Modules",
            "Enable Raid Pause",
            true,
            "Allows a solo raid to be paused without consuming raid or world time."
        );
        EnableSmartAirFilter = config.Bind(
            "01 - Modules",
            "Enable Smart Air Filter",
            true,
            "Makes the Hideout air filter drain only during PMC raids. Restart SPT after changing this option."
        );
        EnableExpandedSpecialSlots = config.Bind(
            "01 - Modules",
            "Enable Expanded Special Slots",
            true,
            "Adds three Special Slots and arranges all six slots in a compact grid. Restart SPT after changing this option."
        );
        EnableCompatibilityRules = config.Bind(
            "01 - Modules",
            "Enable Compatibility Rules",
            true,
            "Applies the JSONC compatibility rules included with the server mod. Restart SPT after changing this option."
        );
        EnableCompactHud = config.Bind(
            "01 - Modules",
            "Enable Compact HUD",
            true,
            "Shows health, energy, hydration and ready-to-use grenades in raids and the Hideout."
        );
        PauseShortcut = config.Bind(
            "02 - Controls",
            "Pause Raid",
            new KeyboardShortcut(KeyCode.DownArrow),
            "Pauses or resumes the current solo raid."
        );
    }
}
