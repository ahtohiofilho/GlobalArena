using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Temporary CIVILIZATION identity tokens for the visual client only.
/// RGB primary colors (red, green, blue) are a TEST HYPOTHESIS, NOT
/// diplomatic ownership, unit occupations, or simulation state.
/// Owner numeric IDs come exclusively from the authoritative census.
/// </summary>
public static class GaCivilizationVisualIdentity
{
    public static Color ColorFor(ulong civilizationId) => civilizationId switch
    {
        1UL => new Color("#ed5252"), // civilization 1: RED
        2UL => new Color("#398bf2"), // civilization 2: BLUE
        3UL => new Color("#43ca63"), // civilization 3: GREEN
        _ => GaVisualFoundation.Muted // more civilizations may be added later
    };

    public static string EnglishNameFor(ulong civilizationId) =>
        civilizationId switch
        {
            1UL => "RED",
            2UL => "BLUE",
            3UL => "GREEN",
            _ => "UNASSIGNED"
        };
}
