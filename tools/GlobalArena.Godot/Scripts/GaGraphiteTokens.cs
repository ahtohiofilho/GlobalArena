using Godot;
namespace GlobalArena.Godot;

// R237.0 Graphite contract. Reference dimensions are not applied to the
// current 1280x720 logical shell until its migration.
internal static class GaGraphiteTokens
{
    internal static readonly Color Base = new("#181A1C");
    internal static readonly Color Panel = new("#292C2F");
    internal static readonly Color Raised = new("#393D40");
    internal static readonly Color Recess = new("#111315");
    internal static readonly Color PrimaryText = new("#DFE1E3");
    internal static readonly Color SecondaryText = new("#A1A5A9");
    internal static readonly Color Positive = new("#89AB87");
    internal static readonly Color Attention = new("#D5AD66");
    internal static readonly Color Negative = new("#C47470");
    internal static readonly Color Border = new("#505458");
    internal static readonly Color SubtleBorder = new("#303234");
    internal static readonly Color Hover = new("#4D5155");
    internal static readonly Color Pressed = new("#151719");
    internal static readonly Color Focus = new("#C3C7CA");
    internal static readonly Color DisabledSurface = new("#242628");
    internal static readonly Color DisabledText = new("#777B7E");
    internal static readonly Color SelectedSurface = new("#474238");
    internal const int Space = 4;
    internal const int ReferenceTopBar = 56, ReferenceBottomBar = 36, ReferenceRightColumn = 320;
    internal const int ReferenceBodyFont = 14, ReferenceCaptionFont = 12, ReferenceHeadingFont = 18;
}
