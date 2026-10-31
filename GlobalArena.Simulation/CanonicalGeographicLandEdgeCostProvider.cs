using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Transparent, dimensionless VS37 cost breakdown. Distances are quantized
/// unit-sphere CHORDS (not kilometers or promises of a planetary radius).
/// Elevation and moisture are existing 01C procedural raw fields.
/// </summary>
public sealed record GeographicLandEdgeCostQuote(
    ulong ChordMicroUnits,
    ulong DistancePoints,
    long SignedElevationChangeRaw,
    uint GradePenaltyPercent,
    uint SurfacePenaltyPercent,
    ulong TotalPoints);

/// <summary>
/// Experimental, read-only terrestrial movement calibration. Quantizing
/// canonical spherical centers before measuring chords makes the point cost
/// integer and auditable. No fake meter scale, terrain re-generation, troop
/// stats, navigation, equipment or military effectiveness is introduced.
/// </summary>
public static class GeographicLandEdgeCostCalculator
{
    public const long GeometryScale = 1_000_000L;
    public const ulong ChordMicroUnitsPerPoint = 1_000UL;
    public const uint MaximumGradePenaltyPercent = 200U;
    public const uint ForestSurfacePenaltyPercent = 15U;

    public static GeographicLandEdgeCostQuote Quote(
        SphericalPoint3 from,
        SphericalPoint3 to,
        long fromElevationRaw,
        long toElevationRaw,
        long fromMoistureRaw,
        long toMoistureRaw,
        bool fromForest,
        bool toForest)
    {
        if (!from.IsValid || !to.IsValid)
            throw new ArgumentException("Physical movement requires normalized spherical centers.");
        if (fromElevationRaw is < -1_000_000L or > 1_000_000L ||
            toElevationRaw is < -1_000_000L or > 1_000_000L ||
            fromMoistureRaw is < 0L or > 1_000_000L ||
            toMoistureRaw is < 0L or > 1_000_000L)
            throw new ArgumentOutOfRangeException(
                nameof(fromElevationRaw), "01C fields must be within canonical raw ranges.");

        static long Quantize(double value) =>
            checked((long)Math.Round(value * GeometryScale, MidpointRounding.AwayFromZero));

        var dx = Quantize(to.X) - Quantize(from.X);
        var dy = Quantize(to.Y) - Quantize(from.Y);
        var dz = Quantize(to.Z) - Quantize(from.Z);
        var squared = checked((ulong)(dx * dx + dy * dy + dz * dz));
        if (squared == 0UL)
            throw new ArgumentException("Separate physical tiles require distinct quantized centers.");

        // Correct the floating-point sqrt candidate using exact integer
        // squares: output is the FLOOR of the integer chord distance.
        var root = checked((ulong)Math.Sqrt(squared));
        while (root * root > squared) root--;
        while ((root + 1UL) * (root + 1UL) <= squared) root++;
        if (root == 0UL)
            throw new ArgumentException("The physical chord is below the quantization resolution.");

        var distancePoints = checked((root - 1UL) /
            ChordMicroUnitsPerPoint + 1UL);
        var delta = checked(toElevationRaw - fromElevationRaw);
        var rise = Math.Abs(delta);
        // Relative elevation differences are dimensionless proxies, NOT
        // physical slopes in degrees. Both ascent and descent cost effort,
        // with descent less demanding in this isolated trial calibration.
        var gradeRatio = checked((ulong)rise * 10UL / root);
        var capped = Math.Min((ulong)MaximumGradePenaltyPercent, gradeRatio);
        var gradePercent = (uint)(delta > 0 ? capped : capped / 2UL);

        // Moisture is a soft-ground proxy, while forest is an obstruction
        // proxy. Use MAX rather than summing correlated land penalties.
        var averageMoisture = (fromMoistureRaw + toMoistureRaw) / 2L;
        var wetPercent = averageMoisture > 600_000L
            ? (uint)((averageMoisture - 600_000L) / 20_000L) : 0U;
        var surfacePercent = Math.Max(wetPercent,
            fromForest || toForest ? ForestSurfacePenaltyPercent : 0U);
        var totalPercent = 100UL + gradePercent + surfacePercent;
        var total = checked((distancePoints * totalPercent + 99UL) / 100UL);
        return new GeographicLandEdgeCostQuote(root, distancePoints,
            delta, gradePercent, surfacePercent, total);
    }
}

/// <summary>
/// Geography-backed provider for the OPTIONAL VS36 scalar route preview.
/// It never alters orders, saves, economy or land/water connectivity.
/// No implicit raft or maritime path can be created by high movement speed.
/// </summary>
public sealed class CanonicalGeographicLandEdgeCostProvider :
    IScalarPhysicalEdgeCostProvider
{
    public ulong CostPoints(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to) => Explain(session, from, to).TotalPoints;

    public GeographicLandEdgeCostQuote Explain(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to)
    {
        ArgumentNullException.ThrowIfNull(session);
        var terrain = session.CanonicalTerrain ??
            throw new InvalidOperationException(
                "VS37 physical land costs require existing canonical 01C geography.");
        if (!session.IsPhysicalWorkableLand(from.FineStrategicCellId) ||
            !session.IsPhysicalWorkableLand(to.FineStrategicCellId) ||
            !session.AdjacentWorkable(from).Contains(to))
            throw new InvalidOperationException(
                "A geographic land edge must be adjacent and physically walkable.");

        var fromId = from.FineStrategicCellId;
        var toId = to.FineStrategicCellId;
        var fromIndex = checked((int)fromId.Value - 1);
        var toIndex = checked((int)toId.Value - 1);
        var forest = ExperimentalProceduralBiomeClassifier.Biome.Forest;
        return GeographicLandEdgeCostCalculator.Quote(
            terrain.Geometry.GetCell(fromId).Center,
            terrain.Geometry.GetCell(toId).Center,
            terrain.Elevation[fromIndex], terrain.Elevation[toIndex],
            terrain.Moisture[fromIndex], terrain.Moisture[toIndex],
            terrain.Biomes[fromIndex].UnderlyingBiome == forest,
            terrain.Biomes[toIndex].UnderlyingBiome == forest);
    }
}
