using System;
using System.Collections.Generic;
using System.Linq;
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

    // Presence only, never sovereignty. A mixed node has no single owner.
    internal static readonly Color MixedSiteColor = new("#a3a3a3");

    internal static Color SiteColor(IEnumerable<ulong> civilizationIds)
    {
        var owners = civilizationIds.Distinct().Take(2).ToArray();
        if (owners.Length == 0) throw new ArgumentException("An occupied site needs a civilization.");
        return owners.Length == 1 ? ColorFor(owners[0]) : MixedSiteColor;
    }

    internal static void ExerciseSiteColorContract()
    {
        if (SiteColor(new ulong[] { 1, 1, 1 }) != new Color("#ed5252") ||
            SiteColor(new ulong[] { 2 }) != new Color("#398bf2") ||
            SiteColor(new ulong[] { 3, 3 }) != new Color("#43ca63") ||
            SiteColor(new ulong[] { 1, 2 }) != MixedSiteColor ||
            SiteColor(new ulong[] { 2, 1, 1 }) != MixedSiteColor ||
            SiteColor(new ulong[] { 2, 3 }) != MixedSiteColor)
            throw new InvalidOperationException("Civilization site color contract failed.");
    }

    public static string EnglishNameFor(ulong civilizationId) =>
        civilizationId switch
        {
            1UL => "RED",
            2UL => "BLUE",
            3UL => "GREEN",
            _ => "UNASSIGNED"
        };
}
