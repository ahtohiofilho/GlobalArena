using System;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Presentation-only. This is neither a new world biome classifier nor
/// a new economic terrain mode; it must never enter save, hash or journal.
/// </summary>
public enum CanonicalPhysicalVisualLayer : byte
{
    Biomes = 0,
    Relief = 1
}

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
    /// <summary>
    /// Hypsometric coloring of the *same* continuous 01C elevation used
    /// by movement and productivity. Sea level comes from that same 01C
    /// composition, never from an unrelated HD calibration experiment.
    /// The sphere/tactical polygon geometry remains unchanged.
    /// </summary>
    public static Color FromCanonicalElevation(
        long elevation, long seaLevel, long minElevation, long maxElevation,
        bool isWater, bool visibleIce)
    {
        if (minElevation > seaLevel || maxElevation < seaLevel ||
            elevation < minElevation || elevation > maxElevation)
            throw new ArgumentOutOfRangeException(nameof(elevation),
                "Elevation and sea level must share the same physical 01C range.");
        if (visibleIce)
            return new Color("#d9e7ec");

        static float Fraction(long numerator, long denominator) =>
            denominator <= 0L ? 0f :
            Mathf.Clamp((float)((double)numerator / denominator), 0f, 1f);

        if (isWater)
        {
            var depth = Fraction(seaLevel - elevation, seaLevel - minElevation);
            return new Color("#2a668e").Lerp(new Color("#132b55"), depth);
        }

        var altitude = Fraction(elevation - seaLevel, maxElevation - seaLevel);
        if (altitude < 0.35f)
            return new Color("#5fa079").Lerp(
                new Color("#a2b86d"), altitude / 0.35f);
        if (altitude < 0.70f)
            return new Color("#a2b86d").Lerp(
                new Color("#aa8159"), (altitude - 0.35f) / 0.35f);
        return new Color("#aa8159").Lerp(
            new Color("#e4e6e3"), (altitude - 0.70f) / 0.30f);
    }

    public static Color FromCanonicalCell(
        WorldGenerationV2PhysicalComposition terrain, int index,
        CanonicalPhysicalVisualLayer layer, long minElevation, long maxElevation)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        if (index < 0 || index >= terrain.Elevation.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var cell = terrain.Biomes[index];
        return layer switch
        {
            CanonicalPhysicalVisualLayer.Biomes =>
                FromVisibleBiome(cell.VisibleBiome),
            CanonicalPhysicalVisualLayer.Relief =>
                FromCanonicalElevation(
                    terrain.Elevation[index], terrain.SeaLevel,
                    minElevation, maxElevation,
                    cell.IsWater,
                    cell.VisibleBiome ==
                        ExperimentalProceduralBiomeClassifier.Biome.Ice),
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };
    }

}
