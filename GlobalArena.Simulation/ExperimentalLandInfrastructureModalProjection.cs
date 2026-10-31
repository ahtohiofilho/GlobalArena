using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Reference terrestrial infrastructure classifications. These are NOT
/// technology IDs: the existing economic asset carries its own versioned
/// (TechnologyTypeId, TechnologyCatalogVersion). The caller explicitly
/// supplies that mapping and no ID has a built-in meaning.
/// </summary>
public enum ExperimentalLandInfrastructureKind
{
    StoneRoad = 1,
    Highway = 2,
    Railway = 3
}

public enum ExperimentalLandRoadQuality
{
    NaturalTrail = 0,
    StoneRoad = 1,
    Highway = 2
}

/// <summary>
/// A railroad can coexist with a road on the same abstract crossing.
/// The road tier replaces its predecessor; road and railway are not stacked
/// as multiple additive movement bonuses.
/// </summary>
public sealed record ExperimentalLandCrossingInfrastructure(
    ExperimentalLandRoadQuality RoadQuality, bool HasRailway)
{
    public static ExperimentalLandCrossingInfrastructure Natural { get; } =
        new(ExperimentalLandRoadQuality.NaturalTrail, false);
}

/// <summary>
/// A separate, immutable snapshot of INSTALLED, OPERATIONAL infrastructure,
/// keyed to directed cell-to-cell crossings in the same fine Goldberg graph
/// as the immutable land facts. Does not invent a road at every adjacency.
/// An economic edge access closure disables the built asset in this quote,
/// but does not erase pre-existing natural pedestrian connectivity.
/// </summary>
public sealed class ExperimentalLandInfrastructureSnapshot
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalLandEdgeFactsSnapshot _facts;
    private readonly Dictionary<
        (PhysicalTacticalTileId From, PhysicalTacticalTileId To),
        ExperimentalLandCrossingInfrastructure> _byDirection;

    public ulong NetworkRevision { get; }
    public int CrossingsWithBuiltInfrastructure => _byDirection.Count / 2;

    public ExperimentalLandInfrastructureSnapshot(
        CanonicalPhysicalTacticalSession session,
        CanonicalLandEdgeFactsSnapshot facts,
        IEnumerable<EconomicTransportBuiltAsset> assets,
        IReadOnlyDictionary<(ulong TechnologyTypeId, ulong CatalogVersion),
            ExperimentalLandInfrastructureKind> technologyMapping,
        StrategicEdgeAccessSnapshot access,
        ulong networkRevision)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(technologyMapping);
        ArgumentNullException.ThrowIfNull(access);
        if (networkRevision == 0UL)
            throw new ArgumentOutOfRangeException(nameof(networkRevision));
        if (session.CanonicalTerrain is null)
            throw new InvalidOperationException(
                "Infrastructure quote requires the existing 01C world.");
        if (facts.Edges.Count > 0)
        {
            var example = facts.Edges[0];
            _ = facts.CostPoints(session, example.From, example.To);
        }
        NetworkRevision = networkRevision;

        var topology = session.FineWorld.StrategicTopology;
        var resolution = topology.Parameters;
        var uniqueAssets = new HashSet<ulong>();
        var built = new Dictionary<
            (PhysicalTacticalTileId, PhysicalTacticalTileId),
            ExperimentalLandCrossingInfrastructure>();

        foreach (var asset in assets)
        {
            if (asset is null || !uniqueAssets.Add(asset.AssetId))
                throw new ArgumentException("Infrastructure assets need unique non-null IDs.",
                    nameof(assets));
            var edgeId = asset.FineGeographicEdgeId;
            if (edgeId is not PhysicalTacticalEdgeId fine ||
                fine.FineGoldbergParameters != resolution ||
                asset.GeographicEdgeId.Value > (ulong)topology.Edges.Count)
                throw new ArgumentException(
                    "Infrastructure requires the correct qualified fine crossing.",
                    nameof(assets));
            if (!technologyMapping.TryGetValue(
                    (asset.TechnologyTypeId, asset.TechnologyCatalogVersion),
                    out var kind) ||
                !Enum.IsDefined(kind))
                throw new ArgumentException(
                    "Installed transport technology must be explicitly mapped.",
                    nameof(technologyMapping));

            if (!asset.IsOperational || !access.IsOpen(asset.GeographicEdgeId))
                continue;

            var edge = topology.Edges[checked((int)asset.GeographicEdgeId.Value - 1)];
            var a = new PhysicalTacticalTileId(resolution, edge.IncidentCellIds[0]);
            var b = new PhysicalTacticalTileId(resolution, edge.IncidentCellIds[1]);
            if (facts.Find(a, b) is null || facts.Find(b, a) is null)
                throw new InvalidOperationException(
                    "VS39 cannot project a built land corridor over water or unwalkable terrain.");
            var old = built.TryGetValue((a, b), out var previous) ?
                previous : ExperimentalLandCrossingInfrastructure.Natural;
            var updated = kind switch
            {
                ExperimentalLandInfrastructureKind.StoneRoad =>
                    old with { RoadQuality = (ExperimentalLandRoadQuality)Math.Max(
                        (int)old.RoadQuality, (int)ExperimentalLandRoadQuality.StoneRoad) },
                ExperimentalLandInfrastructureKind.Highway =>
                    old with { RoadQuality = ExperimentalLandRoadQuality.Highway },
                ExperimentalLandInfrastructureKind.Railway =>
                    old with { HasRailway = true },
                _ => throw new ArgumentOutOfRangeException(nameof(technologyMapping))
            };
            built[(a, b)] = updated;
            built[(b, a)] = updated;
        }

        _byDirection = built;
    }

    public ExperimentalLandCrossingInfrastructure At(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Infrastructure snapshot cannot be borrowed by another game session.");
        // Validates that the underlying geographic edge actually exists.
        _ = _facts.CostPoints(session, from, to);
        return _byDirection.TryGetValue((from, to), out var infrastructure) ?
            infrastructure : ExperimentalLandCrossingInfrastructure.Natural;
    }
}

/// <summary>
/// Modal classes for an OPTIONAL read-only forecast. Modes are not built
/// objects, technologies or commercial service IDs. No modal transfer
/// happens automatically at a node.
/// </summary>
public enum ExperimentalLandTransportMode
{
    Walking = 1,
    PackAnimals = 2,
    RoadVehicle = 3,
    RailVehicle = 4
}

/// <summary>
/// Explicit laboratory parameters for the cost-as-points interpretation.
/// Values are caller-owned percentages of the VS37 walking quote, NOT
/// economic tariffs, physical maximum speeds or canonical unit stats.
/// </summary>
public sealed record ExperimentalLandMobilityCalibration
{
    public uint NaturalTrailPercent { get; }
    public uint StoneRoadPercent { get; }
    public uint HighwayPercent { get; }
    public uint RailwayPercent { get; }

    public ExperimentalLandMobilityCalibration(
        uint naturalTrailPercent, uint stoneRoadPercent,
        uint highwayPercent, uint railwayPercent)
    {
        if (new[] { naturalTrailPercent, stoneRoadPercent,
            highwayPercent, railwayPercent }.Any(x => x is < 1U or > 1000U))
            throw new ArgumentOutOfRangeException(
                nameof(naturalTrailPercent), "Trial modal percentages must be between 1 and 1000.");
        NaturalTrailPercent = naturalTrailPercent;
        StoneRoadPercent = stoneRoadPercent;
        HighwayPercent = highwayPercent;
        RailwayPercent = railwayPercent;
    }
}

/// <summary>
/// One fixed, explicitly selected mode on a whole VS36 route. A real economic
/// multi-modal itinerary must supply transfers and full-service prices
/// elsewhere; an inaccessible edge is NOT made expensive or freely crossed.
/// </summary>
public sealed class ExperimentalModalLandEdgeCostProvider :
    IConstrainedScalarPhysicalEdgeCostProvider
{
    private readonly CanonicalLandEdgeFactsSnapshot _facts;
    private readonly ExperimentalLandInfrastructureSnapshot _infrastructure;
    private readonly ExperimentalLandTransportMode _mode;
    private readonly ExperimentalLandMobilityCalibration _calibration;

    public ExperimentalModalLandEdgeCostProvider(
        CanonicalLandEdgeFactsSnapshot facts,
        ExperimentalLandInfrastructureSnapshot infrastructure,
        ExperimentalLandTransportMode mode,
        ExperimentalLandMobilityCalibration calibration)
    {
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        _infrastructure = infrastructure ??
            throw new ArgumentNullException(nameof(infrastructure));
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        _mode = mode;
        _calibration = calibration ?? throw new ArgumentNullException(nameof(calibration));
    }

    public bool CanTraverse(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to)
    {
        var infrastructure = _infrastructure.At(session, from, to);
        return _mode switch
        {
            ExperimentalLandTransportMode.Walking or
                ExperimentalLandTransportMode.PackAnimals => true,
            ExperimentalLandTransportMode.RoadVehicle =>
                infrastructure.RoadQuality >= ExperimentalLandRoadQuality.StoneRoad,
            ExperimentalLandTransportMode.RailVehicle => infrastructure.HasRailway,
            _ => false
        };
    }

    public ulong CostPoints(
        CanonicalPhysicalTacticalSession session,
        PhysicalTacticalTileId from,
        PhysicalTacticalTileId to)
    {
        if (!CanTraverse(session, from, to))
            throw new InvalidOperationException(
                "This mode cannot cross the edge without suitable constructed infrastructure.");
        var baseCost = _facts.CostPoints(session, from, to);
        var installed = _infrastructure.At(session, from, to);
        var percent = _mode == ExperimentalLandTransportMode.RailVehicle ?
            _calibration.RailwayPercent :
            installed.RoadQuality switch
            {
                ExperimentalLandRoadQuality.Highway => _calibration.HighwayPercent,
                ExperimentalLandRoadQuality.StoneRoad => _calibration.StoneRoadPercent,
                _ => _calibration.NaturalTrailPercent
            };
        return checked((ulong)(((UInt128)baseCost * percent + 99U) / 100U));
    }
}
