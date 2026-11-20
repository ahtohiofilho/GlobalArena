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
/// Explicit MONETARY capital cost for constructing natural infrastructure.
/// No material inventory, worker allocation or resource consumption exists.
/// For terrestrial crossings, elevation delta produces a quadratic surcharge.
/// All numbers are money MICRO-units (1e-6 money), never labor units.
/// </summary>
public sealed class FoundationMonetaryConstructionPolicy
{
    public ulong LandBaseCostMoneyMicro { get; }
    public ulong CoastBaseCostMoneyMicro { get; }
    public ulong SeaBaseCostMoneyMicro { get; }
    public ulong LandHeightSquaredCostMoneyMicro { get; }

    public FoundationMonetaryConstructionPolicy(
        ulong landBaseCostMoneyMicro,
        ulong coastBaseCostMoneyMicro,
        ulong seaBaseCostMoneyMicro,
        ulong landHeightSquaredCostMoneyMicro)
    {
        if (landBaseCostMoneyMicro == 0UL ||
            coastBaseCostMoneyMicro == 0UL ||
            seaBaseCostMoneyMicro == 0UL)
            throw new ArgumentOutOfRangeException(
                nameof(landBaseCostMoneyMicro),
                "Physical infrastructure needs explicitly positive monetary " +
                "costs. The quadratic surcharge may be zero.");
        LandBaseCostMoneyMicro = landBaseCostMoneyMicro;
        CoastBaseCostMoneyMicro = coastBaseCostMoneyMicro;
        SeaBaseCostMoneyMicro = seaBaseCostMoneyMicro;
        LandHeightSquaredCostMoneyMicro = landHeightSquaredCostMoneyMicro;
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
    /// Existing v12 natural transport path, preserved byte-for-byte.
    /// </summary>
    public static FoundationNaturalTransportSeedEvidence Create(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationNaturalTransportSeedingPolicy policy) =>
        CreateCore(generatedWorld, incidence, policy, null);

    /// <summary>
    /// Opt-in v13: independently seed physical operating friction and
    /// MONEY required to invest in a natural crossing. No labor ledger.
    /// </summary>
    public static FoundationNaturalTransportSeedEvidence Create(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationNaturalTransportSeedingPolicy operatingPolicy,
        FoundationMonetaryConstructionPolicy monetaryPolicy)
    {
        ArgumentNullException.ThrowIfNull(monetaryPolicy);
        return CreateCore(generatedWorld, incidence,
            operatingPolicy, monetaryPolicy);
    }

    /// <summary>
    /// R82 explicitly opt-in tactical-first transport preview. This creates
    /// only proposed edge records; it MUST NOT be deployed to a legacy V1
    /// hashed WorldState until a versioned worldgen/migration gate exists.
    /// </summary>
    public static FoundationNaturalTransportSeedEvidence PreviewFineFirst(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fineGeography,
        FoundationNaturalTransportSeedingPolicy operatingPolicy,
        FoundationMonetaryConstructionPolicy? monetaryPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(fineGeography);
        if (fineGeography.Seed != generatedWorld.Request.Seed ||
            fineGeography.FineParameters != incidence.Refinement.FineParameters ||
            fineGeography.CoarseParameters != incidence.Refinement.CoarseParameters ||
            fineGeography.FineElevationRaw.Count != incidence.TileIncidences.Count)
            throw new InvalidOperationException(
                "Fine-first preview must be bound to the same seed and scales.");
        return CreateCore(generatedWorld, incidence,
            operatingPolicy, monetaryPolicy, fineGeography);
    }

    private static FoundationNaturalTransportSeedEvidence CreateCore(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationNaturalTransportSeedingPolicy policy,
        FoundationMonetaryConstructionPolicy? monetaryPolicy,
        FoundationFineFirstPhysicalGeography? fineGeography = null)
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

        // Legacy V1 remains byte-compatible by default. The R82 explicit
        // preview reverses the physical dependency: fine elevation and fine
        // sea-level are generated first; coarse geography is never sampled.
        long seaLevel = fineGeography?.SeaLevelRaw ??
            generatedWorld.StrategicPhysicalFields.SeaLevel.RawValue;
        var elevation = generatedWorld.StrategicPhysicalFields.Elevation;
        int count = incidence.TileIncidences.Count;
        var fineElevation = new long[count];
        var isLand = new bool[count];
        int landTiles = 0;
        for (int i = 0; i < count; i++)
        {
            if (fineGeography is not null)
                fineElevation[i] = fineGeography.FineElevationRaw[i];
            else
            {
                var tile = incidence.TileIncidences[i];
                long total = 0L;
                foreach (var coarse in tile.IncidentCoarseCellIds)
                    total = checked(total + elevation.GetRawValue(coarse));
                fineElevation[i] = total / tile.IncidentCoarseCellIds.Count;
            }
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
            var surfaceCrossing = fineGeography?.CrossingAtIndices(a, b);
            bool groundCrossing = surfaceCrossing ==
                FoundationFineTransportCrossingKind.Ground;
            bool portCrossing = surfaceCrossing ==
                FoundationFineTransportCrossingKind.Port ||
                surfaceCrossing ==
                    FoundationFineTransportCrossingKind.BlockedIceSeaBoundary;
            bool openSeaCrossing = surfaceCrossing ==
                FoundationFineTransportCrossingKind.OpenSea;
            FoundationNaturalTransportCostBand band;
            ulong cost;
            ulong? constructionMoney = null;
            if (fineGeography is null ? firstLand && secondLand : groundCrossing)
            {
                landCrossings++;
                band = policy.Land;
                // No float operations, no owner multiplier and no invented
                // demand. UInt128 protects even large explicit slope policies.
                Int128 delta = (Int128)fineElevation[a] - fineElevation[b];
                if (delta < 0) delta = -delta;
                UInt128 scale = (UInt128)StrategicScalarField.Denominator;
                UInt128 heightDelta = (UInt128)delta;
                UInt128 operating = (UInt128)policy.LandSlopeCostMicroPerNormalizedElevation *
                    heightDelta / scale;
                cost = checked((ulong)((UInt128)band.FreePhysicalCostMicroPerWholeCargoUnit +
                    operating));
                if (monetaryPolicy is not null)
                {
                    UInt128 construction = (UInt128)monetaryPolicy.LandHeightSquaredCostMoneyMicro *
                        heightDelta * heightDelta / (scale * scale);
                    constructionMoney = checked((ulong)(
                        (UInt128)monetaryPolicy.LandBaseCostMoneyMicro +
                        construction));
                }
            }
            else if (fineGeography is null ? firstLand || secondLand : portCrossing)
            {
                coastCrossings++;
                band = policy.Coast;
                cost = band.FreePhysicalCostMicroPerWholeCargoUnit;
                constructionMoney = monetaryPolicy?.CoastBaseCostMoneyMicro;
            }
            else
            {
                if (fineGeography is not null && !openSeaCrossing)
                    throw new InvalidOperationException(
                        "Every fine crossing requires a canonical surface mode.");
                seaCrossings++;
                band = policy.Sea;
                cost = band.FreePhysicalCostMicroPerWholeCargoUnit;
                constructionMoney = monetaryPolicy?.SeaBaseCostMoneyMicro;
            }
            // Open sea has no physical congestion bottleneck, regardless
            // of epoch or freight volume. Its *positive* free operating cost
            // remains; coast/port transfers retain their finite K.
            // The explicit unbounded marker is canonically stored in the
            // existing hashed cargo-capacity field, without schema changes.
            ulong capacity = (fineGeography is null
                    ? !firstLand && !secondLand : openSeaCrossing)
                ? FoundationPhysicalEndogenousCongestionFeedback
                    .UnboundedNaturalCargoCapacity
                : band.CargoMicroAtReferenceLoad;
            rows[i] = new PhysicalTransportEdgeRuntimeRecord(
                new PhysicalTacticalEdgeId(fine, edge.Id),
                PhysicalTransportOwnershipKind.NoInfrastructure,
                null, cost, capacity, 0UL,
                constructionMoney);
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
