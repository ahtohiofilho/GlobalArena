using System;
using System.Collections.Generic;
using System.Linq;

namespace GlobalArena.World;

/// <summary>
/// R82: independent tactical-first physical geography proposal.
/// The seed and G(fine) spherical geometry generate altitude, moisture and
/// temperature BEFORE any strategic projection. No strategic elevation,
/// strategic sea-level, biome category, owner or market price is read.
/// This is a candidate contract until world hash/save and economic seeding
/// are versioned to consume it together.
/// </summary>
/// <summary>Functional movement surface, distinct from geological land/sea.</summary>
public enum FoundationFineSurfaceKind : byte
{
    Land = 1,
    OpenWater = 2,
    FirmSeaIce = 3
}

public sealed class FoundationFineFirstPhysicalGeography
{
    public const int InitialLandTargetPercent = 35;
    // R91: stable gameplay thermal threshold (raw physical temperature).
    // This is not a biome classifier. Ocean below this limit is a
    // land-traversable frozen surface; its geological identity remains sea.
    public const long FirmSeaIceTemperatureRaw = -700_000L;

    public FoundationFineSurfaceKind SurfaceAtIndex(int index)
    {
        if ((uint)index >= (uint)FineElevationRaw.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (FineElevationRaw[index] > SeaLevelRaw)
            return FoundationFineSurfaceKind.Land;
        return FineTemperatureRaw[index] <= FirmSeaIceTemperatureRaw
            ? FoundationFineSurfaceKind.FirmSeaIce
            : FoundationFineSurfaceKind.OpenWater;
    }

    public FoundationFineSurfaceKind SurfaceAt(PhysicalTacticalTileId id)
    {
        if (!id.IsValid || id.FineGoldbergParameters != FineParameters ||
            id.FineStrategicCellId.Value > (ulong)FineElevationRaw.Count)
            throw new ArgumentOutOfRangeException(nameof(id));
        return SurfaceAtIndex(checked((int)id.FineStrategicCellId.Value - 1));
    }

    public bool CanTraverseOnFoot(int index) =>
        SurfaceAtIndex(index) != FoundationFineSurfaceKind.OpenWater;

    public bool CanNavigateByShip(int index) =>
        SurfaceAtIndex(index) == FoundationFineSurfaceKind.OpenWater;



    public WorldSeed Seed { get; }
    public GoldbergParameters FineParameters { get; }
    public GoldbergParameters CoarseParameters { get; }
    public long SeaLevelRaw { get; }
    public IReadOnlyList<long> FineElevationRaw { get; }
    public IReadOnlyList<long> FineMoistureRaw { get; }
    public IReadOnlyList<long> FineTemperatureRaw { get; }
    public IReadOnlyList<long> StrategicMeanElevationRaw { get; }
    public IReadOnlyList<int> StrategicLandTileCount { get; }
    public IReadOnlyList<int> StrategicIncidentTileCount { get; }
    public int LandTileCount { get; }

    private FoundationFineFirstPhysicalGeography(
        WorldSeed seed, GoldbergParameters fine, GoldbergParameters coarse,
        long sea, IReadOnlyList<long> elevation,
        IReadOnlyList<long> moisture, IReadOnlyList<long> temperature,
        long[] strategicHeights, int[] strategicLand, int[] strategicCounts,
        int landCount)
    {
        Seed = seed;
        FineParameters = fine;
        CoarseParameters = coarse;
        SeaLevelRaw = sea;
        FineElevationRaw = Array.AsReadOnly(elevation.ToArray());
        FineMoistureRaw = Array.AsReadOnly(moisture.ToArray());
        FineTemperatureRaw = Array.AsReadOnly(temperature.ToArray());
        StrategicMeanElevationRaw = Array.AsReadOnly(strategicHeights);
        StrategicLandTileCount = Array.AsReadOnly(strategicLand);
        StrategicIncidentTileCount = Array.AsReadOnly(strategicCounts);
        LandTileCount = landCount;
    }

    public bool IsLand(PhysicalTacticalTileId id)
    {
        if (!id.IsValid || id.FineGoldbergParameters != FineParameters ||
            id.FineStrategicCellId.Value > (ulong)FineElevationRaw.Count)
            throw new ArgumentOutOfRangeException(nameof(id));
        return FineElevationRaw[checked((int)id.FineStrategicCellId.Value - 1)] >
            SeaLevelRaw;
    }

    public static FoundationFineFirstPhysicalGeography Generate(
        WorldGenerationResult strategicWorld,
        PhysicalTacticalIncidenceMap incidence,
        int landTargetPercent = InitialLandTargetPercent)
    {
        ArgumentNullException.ThrowIfNull(strategicWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        if (incidence.Refinement.CoarseParameters !=
                strategicWorld.Request.StrategicParameters ||
            incidence.CoarseTopology.Cells.Count !=
                strategicWorld.StrategicTopology.Cells.Count ||
            landTargetPercent < 25 || landTargetPercent > 45 ||
            landTargetPercent % 5 != 0)
            throw new ArgumentException(
                "Tactical physical field needs the real coarse/fine topology " +
                "and an explicitly supported land coverage.", nameof(incidence));

        // Fine geometry generated independently from the seed and G(fine).
        // No coarse physical field is consulted: this direction is essential.
        var fineWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(strategicWorld.Request.Seed,
                strategicWorld.Request.Version, incidence.Refinement.FineParameters));
        if (fineWorld.StrategicTopology.Cells.Count !=
            incidence.FineTopology.Cells.Count)
            throw new InvalidOperationException("Fine topology does not match.");

        var geometry = fineWorld.StrategicSphericalGeometry;
        var elevation = TacticalIndependentScaleGenerator.GenerateWeighted(
            geometry, strategicWorld.Request.Seed.Value, 0.5, 2.0, 3.0);
        var moisture = ExperimentalRandomMoisturePatchGenerator.Generate(
            geometry, strategicWorld.Request.Seed.Value);
        var temperature = ExperimentalIceTemperatureComposer.Generate(
            geometry, elevation, moisture);
        int count = incidence.TileIncidences.Count;
        if (elevation.Count != count || moisture.Count != count ||
            temperature.Count != count)
            throw new InvalidOperationException(
                "Each continuous physical field must cover every fine tile.");

        // A single sea level derived FROM fine elevation; never imported
        // from the coarse V1 world. Same percentile rule as 01C.
        var sorted = elevation.OrderBy(x => x).ToArray();
        int cutoff = Math.Clamp((int)Math.Floor(
            count * (100.0 - landTargetPercent) / 100.0), 0, count - 1);
        long sea = sorted[cutoff];

        // Strategic display values are read-only aggregates of tactical
        // values. Shared fine incidences contribute once to each associated
        // strategic region, which is NOT a partition of the fine topology.
        int coarseCount = incidence.CoarseTopology.Cells.Count;
        var totals = new Int128[coarseCount];
        var counts = new int[coarseCount];
        var landCounts = new int[coarseCount];
        int land = 0;
        for (int i = 0; i < count; i++)
        {
            long h = elevation[i];
            bool isLand = h > sea;
            if (isLand) land++;
            var tile = incidence.TileIncidences[i];
            if (tile.PhysicalTacticalTileId.FineStrategicCellId.Value !=
                (ulong)i + 1UL)
                throw new InvalidOperationException(
                    "Fine physical identity must be canonical and ordered.");
            foreach (var coarseId in tile.IncidentCoarseCellIds)
            {
                int index = checked((int)coarseId.Value - 1);
                totals[index] += h;
                counts[index] = checked(counts[index] + 1);
                if (isLand) landCounts[index] =
                    checked(landCounts[index] + 1);
            }
        }
        var averages = new long[coarseCount];
        for (int i = 0; i < coarseCount; i++)
        {
            if (counts[i] <= 0)
                throw new InvalidOperationException(
                    "Strategic region must have fine physical incidences.");
            averages[i] = checked((long)(totals[i] / counts[i]));
        }
        return new FoundationFineFirstPhysicalGeography(
            strategicWorld.Request.Seed, incidence.Refinement.FineParameters,
            incidence.Refinement.CoarseParameters, sea, elevation, moisture,
            temperature, averages, landCounts, counts, land);
    }
}
