using System;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Shared presentation colors for the SAME canonical physical 01C biome
/// identity on the tactical chart and global G(18,0) polyhedral mesh.
/// Never generate or classify an independent second map for the globe.
/// </summary>
internal static class CanonicalPhysicalTerrainPalette
{
    public static Color FromVisibleBiome(
        ExperimentalProceduralBiomeClassifier.Biome biome) => biome switch
    {
        ExperimentalProceduralBiomeClassifier.Biome.Ocean => new Color("#245b86"),
        ExperimentalProceduralBiomeClassifier.Biome.Desert => new Color("#d2bd75"),
        ExperimentalProceduralBiomeClassifier.Biome.Steppe => new Color("#a4a16b"),
        ExperimentalProceduralBiomeClassifier.Biome.Prairie => new Color("#7bad72"),
        ExperimentalProceduralBiomeClassifier.Biome.Forest => new Color("#336e53"),
        ExperimentalProceduralBiomeClassifier.Biome.Hills => new Color("#898b66"),
        ExperimentalProceduralBiomeClassifier.Biome.Mountains => new Color("#777f8a"),
        ExperimentalProceduralBiomeClassifier.Biome.Ice => new Color("#bddae8"),
        _ => throw new ArgumentOutOfRangeException(nameof(biome))
    };

    /// <summary>
    /// Compatibility with legacy physical sessions that have not promoted
    /// 01C. Their actual V1 land and biome fields remain the authority.
    /// </summary>
    public static Color FromLegacyWorld(WorldGenerationResult world, StrategicCellId cell)
    {
        if (world.StrategicPhysicalFields.LandWater.GetKind(cell) ==
            StrategicLandWaterKind.Water)
            return new Color("#245b86");
        return world.StrategicBiomes.GetKind(cell) switch
        {
            StrategicBiomeKind.PolarIce => new Color("#bddae8"),
            StrategicBiomeKind.Tundra => new Color("#9db7a4"),
            StrategicBiomeKind.Highland => new Color("#898b66"),
            StrategicBiomeKind.Wetland => new Color("#5b9a78"),
            StrategicBiomeKind.Desert => new Color("#d2bd75"),
            StrategicBiomeKind.Rainforest => new Color("#285d49"),
            StrategicBiomeKind.Forest => new Color("#336e53"),
            StrategicBiomeKind.Grassland => new Color("#7bad72"),
            _ => throw new InvalidOperationException("Unsupported V1 canonical biome.")
        };
    }
}
