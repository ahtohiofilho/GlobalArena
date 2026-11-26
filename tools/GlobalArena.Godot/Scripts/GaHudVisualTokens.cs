using Godot;

namespace GlobalArena.Godot;

// UI-only tokens; logical pixels and seconds. No simulation dependencies.
internal enum GaHudPlane { Atmosphere, Structure, Object, Portrait, Footer }
internal static class GaHudVisualTokens
{
    internal static readonly Color Canvas = new("#111111");
    internal static readonly Color Structure = new("#141414");
    internal static readonly Color Object = new("#222222");
    internal static readonly Color Portrait = new("#171717");
    internal static readonly Color Footer = new("#1d1d1d");
    internal static readonly Color Rim = new("#424242");
    internal static readonly Color RecessRim = new("#303030");
    internal static readonly Color Action = new("#303030");
    internal static readonly Color Hover = new("#414141");
    internal static readonly Color Pressed = new("#202020");
    internal static readonly Color Disabled = new("#1b1b1b");
    internal static readonly Color Focus = new("#a4a4a4");
    internal static readonly Color Shadow = new(0f, 0f, 0f, 0.50f);
    internal const int RadiusSmall = 3, RadiusMedium = 5, RimWidth = 1, ActionPadding = 3;
    internal const int ContactShadow = 2, ObjectShadow = 4, FooterShadow = 6;
    internal const int LightTextureWidth = 128, LightTextureHeight = 128;
    internal const float LightInset = 2f;
    internal const double HoverSeconds = 0.13, PressSeconds = 0.065, ReleaseSeconds = 0.15,
        ExitSeconds = 0.10, RevealSeconds = 0.18;
    internal const float HoverTint = 1.08f, PressTint = 0.86f, RevealAlpha = 0.82f;
}
