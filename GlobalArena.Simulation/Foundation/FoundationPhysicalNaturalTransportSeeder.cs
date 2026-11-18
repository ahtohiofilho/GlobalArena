using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicit economic units for one NATURAL crossing class. This is a
/// balancing input, not a hardcoded historical price or inferred road asset.
/// </summary>
public sealed record FoundationNaturalTransportCostBand(
    ulong FreePhysicalCostMicroPerWholeCargoUnit,
    ulong CargoMicroAtReferenceLoad);

/// <summary>
/// The caller explicitly supplies costs and reference capacities for land,
/// coast and sea crossings. Slope penalty applies ONLY between land tiles,
/// per one full normalized elevation difference (raw 1_000_000).
/// There are deliberately no silently chosen gameplay coefficients.
/// </summary>
public sealed class FoundationNaturalTransportSeedingPolicy
{
    public FoundationNaturalTransportCostBand Land { get; }
    public FoundationNaturalTransportCostBand Coast { get; }
    public FoundationNaturalTransportCostBand Sea { get; }
    public ulong LandSlopeCostMicroPerNormalizedElevation { get; }

    public FoundationNaturalTransportSeedingPolicy(
        FoundationNaturalTransportCostBand land,
        FoundationNaturalTransportCostBand coast,
        FoundationNaturalTransportCostBand sea,
        ulong landSlopeCostMicroPerNormalizedElevation)
    {
        static void RequireValid(FoundationNaturalTransportCostBand? band,
            string name)
        {
            if (band is null ||
                band.FreePhysicalCostMicroPerWholeCargoUnit == 0UL ||
                band.CargoMicroAtReferenceLoad == 0UL)
                throw new ArgumentException(
                    "Natural crossing cost and reference capacity must be " +
                    "explicit, strictly positive numbers.", name);
        }
        RequireValid(land, nameof(land));
        RequireValid(coast, nameof(coast));
        RequireValid(sea, nameof(sea));
        Land = land;
        Coast = coast;
        Sea = sea;
        LandSlopeCostMicroPerNormalizedElevation =
            landSlopeCostMicroPerNormalizedElevation;
    }
}

/// <summary>
/// The future investment/construction phase will consume PHYSICAL labor,
/// not money or a synthetic market bid. Baselines are supplied explicitly
/// for each crossing class; the extra land component is quadratic in the
/// two endpoints' altitude difference. No geology/roughness/gradient field.
/// </summary>
public sealed class FoundationNaturalConstructionSeedingPolicy
{
    public ulong LandBaseLaborMicro { get; }
    public ulong CoastBaseLaborMicro { get; }
    public ulong SeaBaseLaborMicro { get; }
    public ulong LandHeightSquaredLaborMicro { get; }

    public FoundationNaturalConstructionSeedingPolicy(
        ulong landBaseLaborMicro,
        ulong coastBaseLaborMicro,
        ulong seaBaseLaborMicro,
        ulong landHeightSquaredLaborMicro)
    {
        if (landBaseLaborMicro == 0UL ||
            coastBaseLaborMicro == 0UL ||
            seaBaseLaborMicro == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(landBaseLaborMicro),
                "All physical construction baselines must be positive. " +
                "The quadratic penalty may be zero.");
        LandBaseLaborMicro = landBaseLaborMicro;
        CoastBaseLaborMicro = coastBaseLaborMicro;
        SeaBaseLaborMicro = seaBaseLaborMicro;
        LandHeightSquaredLaborMicro = landHeightSquaredLaborMicro;
    }
}

/// <summary>
/// Inspectable seed evidence. Classification is based on an interpolated,
/// coarse-derived fine elevation, not a falsely claimed independent tactical
/// altitude field. The inventory is still UNCOMMITTED until WorldState
/// validates and deploys it against the generated-world signature.
/// </summary>
public sealed record FoundationNaturalTransportSeedEvidence(
    PhysicalTransportInfrastructureRuntimeState Transport,
    int LandTileCount, int SeaTileCount,
    int LandCrossingCount, int CoastCrossingCount,
    int SeaCrossingCount);

/// <summary>
/// Projects EXISTING canonical V1 strategic elevation to each fine tile via
/// the exact arithmetic mean of that tile's 1–3 incident coarse cells. This
/// deliberately conservative interpolation uses no new random draws,
/// political borders, movement demands, market prices or phantom roads.
/// Every real dual-graph edge receives a NATURAL (unbuilt, untolled) cost
/// and reference capacity from its physical crossing class.
/// </summary>
public static class FoundationPhysicalNaturalTransportSeeder
{
    /// <summary>
    /// Backward-compatible v12 transport seeding: operation cost only.
    /// Existing inputs and their SHA-256 wire formats are unchanged.
    /// </summary>
    public static FoundationNaturalTransportSeedEvidence Create(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationNaturalTransportSeedingPolicy policy) =>
        CreateCore(generatedWorld, incidence, policy, null);

    /// <summary>
    /// Opt-in v13 seed with both linear operating freight and quadratic
    /// construction labor. Both are per EDGE, from endpoint delta-height.
    /// </summary>
    public static FoundationNaturalTransportSeedEvidence Create(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationNaturalTransportSeedingPolicy operatingPolicy,
        FoundationNaturalConstructionSeedingPolicy constructionPolicy)
    {
        ArgumentNullException.ThrowIfNull(constructionPolicy);
        return CreateCore(generatedWorld, incidence,
            operatingPolicy, constructionPolicy);
    }

    private static FoundationNaturalTransportSeedEvidence CreateCore(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationNaturalTransportSeedingPolicy policy,
        FoundationNaturalConstructionSeedingPolicy? constructionPolicy)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(policy);

        if (generatedWorld.Request.Version != WorldGenerationVersion.Initial)
            throw new NotSupportedException(
                "Natural transport seeding currently requires the canonical " +
                "V1 physical world; V2 requires its own approved fine adapter.");
        if (incidence.Refinement.CoarseParameters !=
                generatedWorld.Request.StrategicParameters ||
            incidence.CoarseTopology.Cells.Count !=
                generatedWorld.StrategicTopology.Cells.Count)
            throw new InvalidOperationException(
                "Natural transport must derive from the same generated " +
                "strategic world and the declared fine refinement.");

        // This array is indexed only by the canonical fine one-based tile ID.
        // The coarse elevation and sea level are part of the signed worldgen.
        long seaLevel = generatedWorld.StrategicPhysicalFields.SeaLevel.RawValue;
        var elevation = generatedWorld.StrategicPhysicalFields.Elevation;
        int count = incidence.TileIncidences.Count;
        var fineElevation = new long[count];
        var isLand = new bool[count];
        int landTiles = 0;
        for (int i = 0; i < count; i++)
        {
            var tile = incidence.TileIncidences[i];
            long total = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                total = checked(total + elevation.GetRawValue(coarse));
            fineElevation[i] = total / tile.IncidentCoarseCellIds.Count;
            isLand[i] = fineElevation[i] > seaLevel;
            if (isLand[i]) landTiles++;
        }

        int landCrossings = 0, coastCrossings = 0, seaCrossings = 0;
        var rows = new PhysicalTransportEdgeRuntimeRecord[
            incidence.FineTopology.Edges.Count];
        var fine = incidence.Refinement.FineParameters;
        for (int i = 0; i < rows.Length; i++)
        {
            var edge = incidence.FineTopology.Edges[i];
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            bool firstLand = isLand[a], secondLand = isLand[b];
            FoundationNaturalTransportCostBand band;
            ulong cost;
            ulong? constructionLabor = null;
            if (firstLand && secondLand)
            {
                landCrossings++;
                band = policy.Land;
                // No float operations, no owner multiplier and no invented
                // demand. UInt128 protects even large explicit slope policies.
                Int128 delta = (Int128)fineElevation[a] - fineElevation[b];
                if (delta < 0) delta = -delta;
                UInt128 heightDelta = (UInt128)delta;
                UInt128 scale = (UInt128)StrategicScalarField.Denominator;
                UInt128 operatingPenalty =
                    (UInt128)policy.LandSlopeCostMicroPerNormalizedElevation *
                    heightDelta / scale;
                cost = checked((ulong)(
                    (UInt128)band.FreePhysicalCostMicroPerWholeCargoUnit +
                    operatingPenalty));
                if (constructionPolicy is not null)
                {
                    UInt128 constructionPenalty =
                        (UInt128)constructionPolicy.LandHeightSquaredLaborMicro *
                        heightDelta * heightDelta / (scale * scale);
                    constructionLabor = checked((ulong)(
                        (UInt128)constructionPolicy.LandBaseLaborMicro +
                        constructionPenalty));
                }
            }
            else if (firstLand || secondLand)
            {
                coastCrossings++;
                band = policy.Coast;
                cost = band.FreePhysicalCostMicroPerWholeCargoUnit;
                constructionLabor = constructionPolicy?.CoastBaseLaborMicro;
            }
            else
            {
                seaCrossings++;
                band = policy.Sea;
                cost = band.FreePhysicalCostMicroPerWholeCargoUnit;
                constructionLabor = constructionPolicy?.SeaBaseLaborMicro;
            }
            rows[i] = new PhysicalTransportEdgeRuntimeRecord(
                new PhysicalTacticalEdgeId(fine, edge.Id),
                PhysicalTransportOwnershipKind.NoInfrastructure,
                null, cost, band.CargoMicroAtReferenceLoad, 0UL,
                constructionLabor);
        }
        if (landCrossings + coastCrossings + seaCrossings != rows.Length)
            throw new InvalidOperationException(
                "Natural physical seeding omitted a real fine crossing.");

        return new FoundationNaturalTransportSeedEvidence(
            PhysicalTransportInfrastructureRuntimeState.CreateInitial(rows),
            landTiles, count - landTiles,
            landCrossings, coastCrossings, seaCrossings);
    }
}
