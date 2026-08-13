using BepInEx.Configuration;
using UnityEngine;

namespace RepoCheque;

/// <summary>
/// Every knob for the mod. Lives in BepInEx\config\RepoCheque.cfg.
/// Edit that file with Notepad and restart the game; no rebuild needed.
/// </summary>
internal static class ChequeConfig
{
    // --- master ---
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> DebugLogging = null!;
    internal static ConfigEntry<bool> CartProbeLogging = null!;

    // --- F1 appearance ---
    internal static ConfigEntry<float> ScaleMultiplier = null!;
    internal static ConfigEntry<float> Thickness = null!;
    internal static ConfigEntry<float> ColliderThickness = null!;
    internal static ConfigEntry<float> AspectRatioOverride = null!;
    internal static ConfigEntry<float> Mass = null!;
    internal static ConfigEntry<bool> FitToCart = null!;

    // --- F2 printed amount ---
    internal static ConfigEntry<bool> ShowPrintedAmount = null!;
    internal static ConfigEntry<float> TextSize = null!;
    internal static ConfigEntry<bool> TextAutoFit = null!;
    internal static ConfigEntry<float> TextBoxWidth = null!;
    internal static ConfigEntry<float> TextBoxHeight = null!;
    internal static ConfigEntry<string> TextSide = null!;
    internal static ConfigEntry<float> TextDepthOffset = null!;
    internal static ConfigEntry<string> TextColor = null!;
    internal static ConfigEntry<float> TextOffsetU = null!;
    internal static ConfigEntry<float> TextOffsetV = null!;

    // --- F4 spawn in cart ---
    internal static ConfigEntry<bool> SpawnInCart = null!;
    internal static ConfigEntry<float> CartSearchRadius = null!;

    // --- F5 indestructible ---
    internal static ConfigEntry<bool> Indestructible = null!;

    internal static void Init(ConfigFile cfg)
    {
        Enabled = cfg.Bind("1. General", "Enabled", true,
            "Master switch. If false the mod does nothing at all and the vanilla money bag comes back.");

        DebugLogging = cfg.Bind("1. General", "DebugLogging", false,
            "Write extra detail to BepInEx\\LogOutput.log. Turn this on if something looks wrong.");

        CartProbeLogging = cfg.Bind("1. General", "CartProbeLogging", false,
            "Very chatty diagnostic that logs the cheque's position every second while it is " +
            "inside a cart. Only switch this on if asked to - it floods the log.");

        ScaleMultiplier = cfg.Bind("2. Appearance", "ScaleMultiplier", 1.0f,
            new ConfigDescription(
                "Overall size of the cheque. 1.0 is the measured default (about twice the largest " +
                "dimension of an upgrade pack). Raise for a bigger cheque, lower for smaller.",
                new AcceptableValueRange<float>(0.2f, 4f)));

        Thickness = cfg.Bind("2. Appearance", "Thickness", 0.06f,
            new ConfigDescription(
                "How thick the paper LOOKS, as a fraction of the cheque's height.",
                new AcceptableValueRange<float>(0.005f, 0.3f)));

        ColliderThickness = cfg.Bind("2. Appearance", "ColliderThickness", 0.075f,
            new ConfigDescription(
                "How thick the INVISIBLE physics box is, in metres. This is deliberately thicker " +
                "than the paper looks: it stops the cheque resting exactly level with the floor " +
                "(which causes flickering) or sinking into a cart. Raise it if you still see " +
                "flickering, lower it if the cheque looks like it is hovering.",
                new AcceptableValueRange<float>(0.005f, 0.4f)));

        AspectRatioOverride = cfg.Bind("2. Appearance", "AspectRatioOverride", 0f,
            new ConfigDescription(
                "Width-to-height ratio of the cheque. Leave at 0 to match your cheque.png automatically " +
                "(recommended - then whatever you paint fits with no stretching). A real cheque is 2.2.",
                new AcceptableValueRange<float>(0f, 5f)));

        FitToCart = cfg.Bind("2. Appearance", "FitToCart", true,
            "Shrink the cheque if it is too big to lie flat inside a cart. A cheque wider than " +
            "the cart interior gets squeezed against the walls and pushed out through the cart, " +
            "which makes it look like it vanished.");

        Mass = cfg.Bind("2. Appearance", "Mass", 0.6f,
            new ConfigDescription(
                "How heavy the cheque is. Paper should be light. This is applied to every cheque " +
                "regardless of the surplus amount, so a $100k cheque weighs the same as a $5k one. " +
                "Set to 0 to keep the original money bag's weight.",
                new AcceptableValueRange<float>(0f, 20f)));

        ShowPrintedAmount = cfg.Bind("3. Printed Amount", "ShowPrintedAmount", true,
            "Draw the money value on the cheque.");

        TextAutoFit = cfg.Bind("3. Printed Amount", "TextAutoFit", true,
            "Scale the amount automatically so it always fills the box below without spilling out. " +
            "Turn off to use TextSize as a fixed size instead.");

        TextSize = cfg.Bind("3. Printed Amount", "TextSize", 4.0f,
            new ConfigDescription("Font size. Used as the maximum when TextAutoFit is on.",
                new AcceptableValueRange<float>(0.1f, 40f)));

        TextBoxWidth = cfg.Bind("3. Printed Amount", "TextBoxWidth", 0.56f,
            new ConfigDescription(
                "Width of the blank amount box in your artwork, as a fraction of the whole image.",
                new AcceptableValueRange<float>(0.05f, 1f)));

        TextBoxHeight = cfg.Bind("3. Printed Amount", "TextBoxHeight", 0.34f,
            new ConfigDescription(
                "Height of the blank amount box in your artwork, as a fraction of the whole image.",
                new AcceptableValueRange<float>(0.02f, 1f)));

        TextColor = cfg.Bind("3. Printed Amount", "TextColor", "#1A1A2E",
            "Colour of the printed amount, as a hex code like #1A1A2E.");

        TextOffsetU = cfg.Bind("3. Printed Amount", "TextOffsetU", 0.35f,
            new ConfigDescription(
                "Left-to-right position of the amount on the cheque face. 0 = far left edge, " +
                "1 = far right edge. Default sits over the blank amount box in the artwork.",
                new AcceptableValueRange<float>(0f, 1f)));

        TextOffsetV = cfg.Bind("3. Printed Amount", "TextOffsetV", 0.33f,
            new ConfigDescription(
                "Bottom-to-top position of the amount. 0 = bottom edge, 1 = top edge.",
                new AcceptableValueRange<float>(0f, 1f)));

        TextSide = cfg.Bind("3. Printed Amount", "TextSide", "Front",
            new ConfigDescription(
                "Which face the amount is printed on. If it shows up on the blank reverse " +
                "instead of inside your amount box, change this to Back.",
                new AcceptableValueList<string>("Front", "Back", "Both")));

        TextDepthOffset = cfg.Bind("3. Printed Amount", "TextDepthOffset", 0.004f,
            new ConfigDescription(
                "How far the text floats above the paper, in metres. Raise it if the number " +
                "flickers against the artwork, lower it if it looks detached.",
                new AcceptableValueRange<float>(0.0005f, 0.05f)));

        SpawnInCart = cfg.Bind("4. Cart Spawning", "SpawnInCart", true,
            "Spawn the cheque gently inside a nearby cart instead of dropping it from above. " +
            "Turn off if a future game update breaks this.");

        CartSearchRadius = cfg.Bind("4. Cart Spawning", "CartSearchRadius", 25f,
            new ConfigDescription("How far from the extraction point to look for a cart, in metres.",
                new AcceptableValueRange<float>(1f, 200f)));

        Indestructible = cfg.Bind("5. Durability", "Indestructible", true,
            "The cheque never loses value from impacts, drops, explosions or monsters.");
    }

    /// <summary>Parses <see cref="TextColor"/>, falling back to near-black if the user typed something odd.</summary>
    internal static Color GetTextColor()
    {
        if (ColorUtility.TryParseHtmlString(TextColor.Value, out Color parsed)) return parsed;

        RepoCheque.Logger.LogWarning(
            $"TextColor '{TextColor.Value}' is not a valid hex colour (expected something like #1A1A2E). Using dark blue.");
        return new Color32(0x1A, 0x1A, 0x2E, 0xFF);
    }
}
