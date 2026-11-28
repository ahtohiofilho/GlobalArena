using Godot;

namespace GlobalArena.Godot;

// UI-only tokens; logical pixels and seconds. No simulation dependencies.
internal enum GaHudPlane { Atmosphere, Structure, Object, Portrait, Footer }
internal static class GaHudVisualTokens
{
    internal static readonly Color Canvas = GaGraphiteTokens.Base;
    internal static readonly Color Structure = GaGraphiteTokens.Base;
    internal static readonly Color Object = GaGraphiteTokens.Panel;
    internal static readonly Color Portrait = GaGraphiteTokens.Recess;
    internal static readonly Color Footer = GaGraphiteTokens.Panel;
    internal static readonly Color Rim = GaGraphiteTokens.Border;
    internal static readonly Color RecessRim = GaGraphiteTokens.SubtleBorder;
    internal static readonly Color Action = GaGraphiteTokens.Raised;
    internal static readonly Color Hover = GaGraphiteTokens.Hover;
    internal static readonly Color Pressed = GaGraphiteTokens.Pressed;
    internal static readonly Color Disabled = GaGraphiteTokens.DisabledSurface;
    internal static readonly Color Focus = GaGraphiteTokens.Focus;
    internal static readonly Color Shadow = new(0f, 0f, 0f, 0.50f);
    internal const int RadiusSmall = 3, RadiusMedium = 5, RimWidth = 1, ActionPadding = 3;
    internal const int ContactShadow = 2, ObjectShadow = 4, FooterShadow = 6;
    internal const int LightTextureWidth = 128, LightTextureHeight = 128;
    internal const float LightInset = 2f;
    internal const double HoverSeconds = 0.13, PressSeconds = 0.065, ReleaseSeconds = 0.15,
        ExitSeconds = 0.10, RevealSeconds = 0.18;
    internal const float HoverTint = 1.08f, PressTint = 0.86f, RevealAlpha = 0.82f;
}
