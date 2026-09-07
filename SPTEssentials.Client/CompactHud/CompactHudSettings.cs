using BepInEx.Configuration;
using UnityEngine;

namespace SPTEssentials.Client.CompactHud
{
    internal enum HudOrientation
    {
        Horizontal,
        Vertical
    }

    internal static class CompactHudSettings
    {
        internal const float DefaultPositionX = 24f;
        internal const float DefaultPositionY = 170f;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<HudOrientation> Orientation;
        internal static ConfigEntry<float> PositionX;
        internal static ConfigEntry<float> PositionY;
        internal static ConfigEntry<float> Scale;
        internal static ConfigEntry<float> Gap;
        internal static ConfigEntry<float> BackgroundOpacity;
        internal static ConfigEntry<KeyboardShortcut> EditModeKey;
        internal static ConfigEntry<KeyboardShortcut> ResetPositionKey;

        internal static void Bind(ConfigFile config, ConfigEntry<bool> enabled)
        {
            Enabled = enabled;

            Orientation = config.Bind(
                "03 - Compact HUD",
                "HUD Orientation",
                HudOrientation.Horizontal,
                "Arrange the four HUD elements horizontally or vertically.");

            PositionX = config.Bind(
                "03 - Compact HUD",
                "HUD Position X",
                DefaultPositionX,
                new ConfigDescription(
                    "Horizontal screen position in pixels.",
                    new AcceptableValueRange<float>(0f, 8000f)));

            PositionY = config.Bind(
                "03 - Compact HUD",
                "HUD Position Y",
                DefaultPositionY,
                new ConfigDescription(
                    "Vertical screen position in pixels.",
                    new AcceptableValueRange<float>(0f, 8000f)));

            Scale = config.Bind(
                "03 - Compact HUD",
                "HUD Scale",
                1f,
                new ConfigDescription(
                    "Scale the complete HUD.",
                    new AcceptableValueRange<float>(0.65f, 1.5f)));

            Gap = config.Bind(
                "03 - Compact HUD",
                "HUD Gap",
                8f,
                new ConfigDescription(
                    "Space between HUD elements in pixels before scaling.",
                    new AcceptableValueRange<float>(0f, 32f)));

            BackgroundOpacity = config.Bind(
                "03 - Compact HUD",
                "HUD Background Opacity",
                0.86f,
                new ConfigDescription(
                    "Opacity of the dark card background.",
                    new AcceptableValueRange<float>(0.25f, 1f)));

            EditModeKey = config.Bind(
                "02 - Controls",
                "Compact HUD Edit Mode",
                new KeyboardShortcut(KeyCode.F10),
                "Toggle layout edit mode. Drag the HUD with the left mouse button while editing.");

            ResetPositionKey = config.Bind(
                "02 - Controls",
                "Reset Compact HUD Position",
                new KeyboardShortcut(KeyCode.F9),
                "Restore the default HUD position.");
        }
    }
}
