using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Caller-provided, non-canonical LAB pricing policy for one versioned,
/// explicitly constructed terrestrial freight technology. Parameters are
/// monetary weights, not movement points or vehicle statistics.
/// No capacity ceiling, global fixed tariff or mode-to-technology lookup is
/// inferred by the model.
/// </summary>
public sealed class ExperimentalFreightTechnologyPricing
{
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public ulong RulesetVersion { get; }
    public ExperimentalLandInfrastructureKind RequiredInfrastructure { get; }
    public BigInteger DistanceWeight { get; }
    public BigInteger UphillWeight { get; }
    public BigInteger WetSurfaceWeight { get; }
    public BigInteger ServiceOverheadWeight { get; }
    public BigInteger OverheadSmoothingMicrograms { get; }
    public BigInteger SaturationWeight { get; }
    public BigInteger SaturationScaleMicrograms { get; }

    public ExperimentalFreightTechnologyPricing(
        ulong technologyTypeId, ulong technologyCatalogVersion,
        ulong rulesetVersion, ExperimentalLandInfrastructureKind requiredInfrastructure,
        BigInteger distanceWeight, BigInteger uphillWeight,
        BigInteger wetSurfaceWeight, BigInteger serviceOverheadWeight,
        BigInteger overheadSmoothingMicrograms, BigInteger saturationWeight,
        BigInteger saturationScaleMicrograms)
    {
        if (technologyTypeId == 0UL || technologyCatalogVersion == 0UL ||
            rulesetVersion == 0UL || !Enum.IsDefined(requiredInfrastructure))
            throw new ArgumentException("Freight policy needs a valid versioned technology and infrastructure kind.");
        if (distanceWeight.Sign < 0 || uphillWeight.Sign < 0 ||
            wetSurfaceWeight.Sign < 0 || serviceOverheadWeight.Sign < 0 ||
            saturationWeight.Sign < 0 || overheadSmoothingMicrograms.Sign <= 0 ||
            saturationScaleMicrograms.Sign <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(distanceWeight), "Prices must be nonnegative and smoothing scales strictly positive.");
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        RulesetVersion = rulesetVersion;
        RequiredInfrastructure = requiredInfrastructure;
        DistanceWeight = distanceWeight;
        UphillWeight = uphillWeight;
        WetSurfaceWeight = wetSurfaceWeight;
        ServiceOverheadWeight = serviceOverheadWeight;
        OverheadSmoothingMicrograms = overheadSmoothingMicrograms;
        SaturationWeight = saturationWeight;
        SaturationScaleMicrograms = saturationScaleMicrograms;
    }
}

/// <summary>
/// One immutable whole-itinerary monetary cost function. Base geography is
/// priced once, with exact BigInteger rational arithmetic. For forecast load q:
///
/// unit freight = geographic base / 1_000_000
///              + service overhead / (q + overhead smoothing)
///              + saturation coefficient * q^2 / saturation scale^2.
///
/// This is a smooth, unbounded congestion penalty (no hard capacity cliff),
/// combined with a softened per-service overhead that creates economies of
/// scale. All terms are raw money per physical gram, not unit movement speed.
/// </summary>
public sealed class ExperimentalAggregatedLandFreightCostFunction :
    IEconomicTurnFreightCostFunction
{
    public const long GeographicDenominator = 1_000_000L;
    public ulong RulesetVersion { get; }
    public BigInteger GeographicBaseNumerator { get; }
    public BigInteger OverheadNumerator { get; }
    public BigInteger OverheadSmoothingMicrograms { get; }
    public BigInteger SaturationNumerator { get; }
    public BigInteger SaturationScaleMicrograms { get; }

    internal ExperimentalAggregatedLandFreightCostFunction(
        ExperimentalFreightTechnologyPricing calibration,
        BigInteger geographicBaseNumerator)
    {
        if (geographicBaseNumerator.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(geographicBaseNumerator));
        RulesetVersion = calibration.RulesetVersion;
        GeographicBaseNumerator = geographicBaseNumerator;
        OverheadNumerator = calibration.ServiceOverheadWeight;
        OverheadSmoothingMicrograms = calibration.OverheadSmoothingMicrograms;
        SaturationNumerator = calibration.SaturationWeight;
        SaturationScaleMicrograms = calibration.SaturationScaleMicrograms;
    }

    public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
        BigInteger expectedLoadMicrograms)
    {
        if (expectedLoadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLoadMicrograms));

        var q = expectedLoadMicrograms;
        var d = new BigInteger(GeographicDenominator);
        var smooth = q + OverheadSmoothingMicrograms;
        var saturationSquared = SaturationScaleMicrograms * SaturationScaleMicrograms;
        var denominator = d * smooth * saturationSquared;
        var numerator = GeographicBaseNumerator * smooth * saturationSquared +
            OverheadNumerator * d * saturationSquared +
            SaturationNumerator * q * q * d * smooth;
        return new FoodTransportCostFraction(numerator, denominator);
    }
}

/// <summary>
/// Optional adapter for the EXISTING fine-grid commercial-service discovery
/// seam. A complete single-technology asset itinerary must be explicitly
/// offered, built, operational and connected by the current network snapshot.
/// This adapter only generates a price function; the existing economic solver
/// continues to own the actual quote locking and settlement.
/// </summary>
public sealed class ExperimentalGeographicFreightItineraryPricing :
    IEconomicFineTransportItineraryPricing
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalLandEdgeFactsSnapshot _facts;
    private readonly ExperimentalLandInfrastructureSnapshot _infrastructure;
    private readonly IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> _anchors;
    private readonly IReadOnlyDictionary<(ulong TypeId, ulong CatalogVersion),
        ExperimentalFreightTechnologyPricing> _policies;

    public ExperimentalGeographicFreightItineraryPricing(
        CanonicalPhysicalTacticalSession session,
        CanonicalLandEdgeFactsSnapshot facts,
        ExperimentalLandInfrastructureSnapshot infrastructure,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> finePointAnchors,
        IEnumerable<ExperimentalFreightTechnologyPricing> technologyPolicies)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        _infrastructure = infrastructure ??
            throw new ArgumentNullException(nameof(infrastructure));
        ArgumentNullException.ThrowIfNull(finePointAnchors);
        ArgumentNullException.ThrowIfNull(technologyPolicies);
        _anchors = new Dictionary<EconomicPointId, PhysicalTacticalTileId>(
            finePointAnchors);
        var entries = technologyPolicies.ToArray();
        if (entries.Any(x => x is null) ||
            entries.Select(x => (x.TechnologyTypeId, x.TechnologyCatalogVersion))
                .Distinct().Count() != entries.Length)
            throw new ArgumentException(
                "Freight price policies require unique versioned technologies.",
                nameof(technologyPolicies));
        _policies = entries.ToDictionary(
            x => (x.TechnologyTypeId, x.TechnologyCatalogVersion));
        if (_anchors.Any(x => !x.Key.IsValid || !x.Value.IsValid ||
            x.Value.FineGoldbergParameters !=
                session.FineWorld.StrategicTopology.Parameters))
            throw new ArgumentException(
                "Freight anchors must belong to the supplied fine topology.",
                nameof(finePointAnchors));
    }

    public IEconomicTurnFreightCostFunction CreateForItinerary(
        EconomicFineTransportServiceIntent intent,
        IReadOnlyList<EconomicTransportBuiltAsset> orderedAssets)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(orderedAssets);
        if (!_policies.TryGetValue(
            (intent.TechnologyTypeId, intent.TechnologyCatalogVersion),
            out var pricing))
            throw new InvalidOperationException(
                "Commercial freight requires an explicitly mapped pricing policy.");
        if (!_anchors.TryGetValue(intent.RouteKey.SourceEconomicPointId,
                out var current) ||
            !_anchors.TryGetValue(intent.RouteKey.DestinationEconomicPointId,
                out var destination))
            throw new InvalidOperationException(
                "A priced freight service requires mapped economic endpoints.");
        if (orderedAssets.Count == 0)
            throw new InvalidOperationException(
                "Commercial freight cannot create a route without built assets.");

        var resolution = _session.FineWorld.StrategicTopology.Parameters;
        var edges = _session.FineWorld.StrategicTopology.Edges;
        var seen = new HashSet<ulong>();
        BigInteger geographicBase = 0;
        foreach (var asset in orderedAssets)
        {
            if (asset is null || !seen.Add(asset.AssetId) ||
                !asset.IsOperational ||
                asset.TechnologyTypeId != pricing.TechnologyTypeId ||
                asset.TechnologyCatalogVersion != pricing.TechnologyCatalogVersion ||
                asset.FineGeographicEdgeId is not PhysicalTacticalEdgeId fine ||
                fine.FineGoldbergParameters != resolution ||
                asset.GeographicEdgeId.Value > (ulong)edges.Count)
                throw new InvalidOperationException(
                    "Freight pricing requires unique operational assets of the declared fine technology.");

            var edge = edges[checked((int)asset.GeographicEdgeId.Value - 1)];
            var nextCell = edge.IncidentCellIds[0] == current.FineStrategicCellId
                ? edge.IncidentCellIds[1]
                : edge.IncidentCellIds[1] == current.FineStrategicCellId
                    ? edge.IncidentCellIds[0]
                    : throw new InvalidOperationException(
                        "The economic itinerary must be contiguous at each logical node.");
            var next = new PhysicalTacticalTileId(resolution, nextCell);
            var facts = _facts.Find(current, next) ??
                throw new InvalidOperationException(
                    "An existing physical land edge is required to price freight.");
            var infrastructure = _infrastructure.At(_session, current, next);
            var supported = pricing.RequiredInfrastructure switch
            {
                ExperimentalLandInfrastructureKind.StoneRoad =>
                    infrastructure.RoadQuality >= ExperimentalLandRoadQuality.StoneRoad,
                ExperimentalLandInfrastructureKind.Highway =>
                    infrastructure.RoadQuality >= ExperimentalLandRoadQuality.Highway,
                ExperimentalLandInfrastructureKind.Railway => infrastructure.HasRailway,
                _ => false
            };
            if (!supported)
                throw new InvalidOperationException(
                    "The current network has no suitable built corridor for freight.");

            geographicBase += GeographicContribution(facts, pricing);
            current = next;
        }

        if (current != destination)
            throw new InvalidOperationException(
                "A complete freight price must reach its explicitly declared destination.");

        return new ExperimentalAggregatedLandFreightCostFunction(
            pricing, geographicBase);
    }

    /// <summary>
    /// Exactly the same additive edge term that the VS41 cost-optimal
    /// Dijkstra minimizes. Keep it shared with final full-itinerary pricing:
    /// any changed rounding here must also affect route selection.
    /// Fixed operating overhead and saturation are path independent for
    /// one declared service, technology and ex-ante expected load.
    /// </summary>
    internal static BigInteger GeographicContribution(
        CanonicalDirectedLandEdgeFacts facts,
        ExperimentalFreightTechnologyPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(pricing);
        var wet = (BigInteger)facts.AverageMoistureRaw *
            facts.UnitSphereChordMicroUnits / 1_000_000L;
        return (BigInteger)facts.UnitSphereChordMicroUnits * pricing.DistanceWeight +
            BigInteger.Max(0L, facts.SignedElevationDeltaRaw) * pricing.UphillWeight +
            wet * pricing.WetSurfaceWeight;
    }
}
