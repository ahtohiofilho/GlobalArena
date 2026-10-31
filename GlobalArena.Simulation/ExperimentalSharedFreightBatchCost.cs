using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A physical fine-grid crossing qualified by ONE transport technology and
/// catalog version. Road and rail on the same Goldberg edge do not share a
/// fictitious congestion resource merely because their geometry overlaps.
/// Parallel assets of the SAME technology on one crossing share the corridor.
/// </summary>
public readonly record struct ExperimentalSharedFreightCorridorKey(
    PhysicalTacticalEdgeId FineCrossing,
    ulong TechnologyTypeId,
    ulong TechnologyCatalogVersion)
{
    public bool IsValid => FineCrossing.IsValid &&
        TechnologyTypeId != 0UL && TechnologyCatalogVersion != 0UL;
}

/// <summary>
/// Exactly one declared, externally validated single-mode commercial route.
/// Its VS40 pricing function supplies geographic and operating costs, but
/// its standalone saturation term is NOT charged in this VS42 model: instead
/// all routes using the same physical/technology corridor contribute to ONE
/// shared, smooth congestion term. This prevents double charging congestion.
/// </summary>
public sealed class ExperimentalSharedFreightOption
{
    public ulong ServiceId { get; }
    public StrategicEconomicRouteKey RouteKey { get; }
    public ExperimentalAggregatedLandFreightCostFunction Pricing { get; }
    public IReadOnlyList<ExperimentalSharedFreightCorridorKey> Corridors { get; }

    public ExperimentalSharedFreightOption(
        ulong serviceId, StrategicEconomicRouteKey routeKey,
        ExperimentalAggregatedLandFreightCostFunction pricing,
        IEnumerable<ExperimentalSharedFreightCorridorKey> corridors)
    {
        if (serviceId == 0UL || !routeKey.SourceEconomicPointId.IsValid ||
            !routeKey.DestinationEconomicPointId.IsValid)
            throw new ArgumentException("Shared freight requires a real service identity and economic endpoints.");
        Pricing = pricing ?? throw new ArgumentNullException(nameof(pricing));
        ArgumentNullException.ThrowIfNull(corridors);
        var route = corridors.ToArray();
        if (route.Length == 0 || route.Any(x => !x.IsValid) ||
            route.Distinct().Count() != route.Length)
            throw new ArgumentException(
                "A freight route requires unique valid crossing/technology resources.",
                nameof(corridors));
        if (route.Any(x => x.TechnologyCatalogVersion !=
                route[0].TechnologyCatalogVersion ||
            x.TechnologyTypeId != route[0].TechnologyTypeId))
            throw new ArgumentException("A single-mode option cannot perform undeclared modal transfers.",
                nameof(corridors));
        ServiceId = serviceId;
        RouteKey = routeKey;
        Corridors = Array.AsReadOnly(route);
    }

    /// <summary>
    /// Turn a VS41/VS40 offer into a physical-resource option ONLY after
    /// checking complete contiguous travel, declared endpoints, operational
    /// constructed assets and CURRENT access. No free service or bridge.
    /// This is still a read-only laboratory projection, not settlement.
    /// </summary>
    public static ExperimentalSharedFreightOption FromBuiltOffer(
        EconomicTransportServiceOffer offer,
        CanonicalPhysicalTacticalSession session,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> anchors,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        StrategicEdgeAccessSnapshot access)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(access);
        if (!offer.IsAvailable || offer.TransferAfterLeg.Count != 0 ||
            offer.CostFunction is not ExperimentalAggregatedLandFreightCostFunction pricing)
            throw new InvalidOperationException(
                "Shared freight only accepts offered single-mode VS40 services.");
        if (!anchors.TryGetValue(offer.RouteKey.SourceEconomicPointId, out var start) ||
            !anchors.TryGetValue(offer.RouteKey.DestinationEconomicPointId, out var end))
            throw new ArgumentException("The commercial service must have fine-grid endpoints.",
                nameof(anchors));
        var topology = session.FineWorld.StrategicTopology;
        if (start.FineGoldbergParameters != topology.Parameters ||
            end.FineGoldbergParameters != topology.Parameters)
            throw new ArgumentException("Commercial anchors must match fine topology.",
                nameof(anchors));
        var byId = new Dictionary<ulong, EconomicTransportBuiltAsset>();
        foreach (var asset in installedAssets)
        {
            if (asset is null || !byId.TryAdd(asset.AssetId, asset))
                throw new ArgumentException("Installed assets need unique non-null IDs.",
                    nameof(installedAssets));
        }
        var cursor = start;
        var keys = new List<ExperimentalSharedFreightCorridorKey>();
        ulong? mode = null, catalog = null;
        foreach (var assetId in offer.AssetPath)
        {
            if (!byId.TryGetValue(assetId, out var asset) || !asset.IsOperational ||
                !access.IsOpen(asset.GeographicEdgeId) ||
                asset.FineGeographicEdgeId is not PhysicalTacticalEdgeId qualified ||
                qualified.FineGoldbergParameters != topology.Parameters ||
                asset.GeographicEdgeId.Value > (ulong)topology.Edges.Count)
                throw new InvalidOperationException(
                    "The service must use current, constructed and open fine assets.");
            if (mode is not null && (mode.Value != asset.TechnologyTypeId ||
                catalog!.Value != asset.TechnologyCatalogVersion))
                throw new InvalidOperationException(
                    "Shared freight requires a single versioned technology.");
            mode = asset.TechnologyTypeId;
            catalog = asset.TechnologyCatalogVersion;
            var edge = topology.Edges[checked((int)asset.GeographicEdgeId.Value - 1UL)];
            var nextId = edge.IncidentCellIds[0] == cursor.FineStrategicCellId
                ? edge.IncidentCellIds[1]
                : edge.IncidentCellIds[1] == cursor.FineStrategicCellId
                    ? edge.IncidentCellIds[0]
                    : throw new InvalidOperationException(
                        "The commercial route is not physically contiguous.");
            var next = new PhysicalTacticalTileId(topology.Parameters, nextId);
            if (!session.IsPhysicalWorkableLand(cursor.FineStrategicCellId) ||
                !session.IsPhysicalWorkableLand(nextId))
                throw new InvalidOperationException(
                    "A land-service route may not traverse water.");
            keys.Add(new ExperimentalSharedFreightCorridorKey(
                qualified, asset.TechnologyTypeId,
                asset.TechnologyCatalogVersion));
            cursor = next;
        }
        if (cursor != end)
            throw new InvalidOperationException(
                "A commercial freight route must reach its declared destination.");
        return new ExperimentalSharedFreightOption(
            offer.ServiceId, offer.RouteKey, pricing, keys);
    }
}

/// <summary>
/// A finite, indivisible forecast lot. This is not a dispatched physical
/// caravan: the lots only compare alternative whole-service offers in a
/// hypothetical economic cycle, with the same destination and physical load.
/// </summary>
public sealed class ExperimentalSharedFreightLot
{
    public ulong LotId { get; }
    public BigInteger ExpectedLoadMicrograms { get; }
    public IReadOnlyList<ExperimentalSharedFreightOption> Alternatives { get; }

    public ExperimentalSharedFreightLot(
        ulong lotId, BigInteger expectedLoadMicrograms,
        IEnumerable<ExperimentalSharedFreightOption> alternatives)
    {
        if (lotId == 0UL) throw new ArgumentOutOfRangeException(nameof(lotId));
        if (expectedLoadMicrograms.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLoadMicrograms));
        ArgumentNullException.ThrowIfNull(alternatives);
        var ordered = alternatives.ToArray();
        if (ordered.Length == 0 || ordered.Any(x => x is null) ||
            ordered.Select(x => x.ServiceId).Distinct().Count() != ordered.Length ||
            ordered.Any(x => x.RouteKey != ordered[0].RouteKey))
            throw new ArgumentException(
                "A lot requires unique offered services for the same commercial relation.",
                nameof(alternatives));
        LotId = lotId;
        ExpectedLoadMicrograms = expectedLoadMicrograms;
        Alternatives = Array.AsReadOnly(
            ordered.OrderBy(x => x.ServiceId).ToArray());
    }
}

/// <summary>
/// An explicit *trial* of shared corridor saturation. No hard cap or
/// capacity cliff. Money per gram from shared corridor volume Q is
/// weight * (Q / smoothingScale)^2. Aggregate monetary cost is
/// weight * Q^3 / (1_000_000 * smoothingScale^2).
/// This replaces, rather than adds to, standalone VS40 saturation.
/// </summary>
public sealed record ExperimentalSharedFreightCongestionPolicy
{
    public BigInteger SaturationWeight { get; }
    public BigInteger SaturationScaleMicrograms { get; }

    public ExperimentalSharedFreightCongestionPolicy(
        BigInteger saturationWeight, BigInteger saturationScaleMicrograms)
    {
        if (saturationWeight.Sign < 0 ||
            saturationScaleMicrograms.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(saturationWeight));
        SaturationWeight = saturationWeight;
        SaturationScaleMicrograms = saturationScaleMicrograms;
    }
}

public sealed record ExperimentalSharedFreightBatchEvaluation(
    FoodTransportCostFraction TotalCost,
    IReadOnlyDictionary<ExperimentalSharedFreightCorridorKey, BigInteger> CorridorLoads);

public static class ExperimentalSharedFreightBatchCost
{
    private static readonly BigInteger MicrogramsPerGram = 1_000_000;

    public static ExperimentalSharedFreightBatchEvaluation Evaluate(
        IReadOnlyList<ExperimentalSharedFreightLot> lots,
        IReadOnlyList<int> selectedAlternativeIndices,
        ExperimentalSharedFreightCongestionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(lots);
        ArgumentNullException.ThrowIfNull(selectedAlternativeIndices);
        ArgumentNullException.ThrowIfNull(policy);
        if (lots.Count != selectedAlternativeIndices.Count)
            throw new ArgumentException("Every lot requires exactly one selected service.",
                nameof(selectedAlternativeIndices));
        var loads = new Dictionary<ExperimentalSharedFreightCorridorKey, BigInteger>();
        var cost = new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One);
        for (var i = 0; i < lots.Count; i++)
        {
            var lot = lots[i];
            var selected = selectedAlternativeIndices[i];
            if (selected < 0 || selected >= lot.Alternatives.Count)
                throw new ArgumentOutOfRangeException(nameof(selectedAlternativeIndices));
            var option = lot.Alternatives[selected];
            cost = Add(cost, UncongestedCost(lot.ExpectedLoadMicrograms, option.Pricing));
            foreach (var corridor in option.Corridors)
            {
                loads.TryGetValue(corridor, out var q);
                loads[corridor] = q + lot.ExpectedLoadMicrograms;
            }
        }
        foreach (var (_, q) in loads)
        {
            cost = Add(cost, new FoodTransportCostFraction(
                policy.SaturationWeight * BigInteger.Pow(q, 3),
                MicrogramsPerGram * BigInteger.Pow(
                    policy.SaturationScaleMicrograms, 2)));
        }
        return new ExperimentalSharedFreightBatchEvaluation(
            cost, new System.Collections.ObjectModel.ReadOnlyDictionary<
                ExperimentalSharedFreightCorridorKey, BigInteger>(loads));
    }

    public static FoodTransportCostFraction UncongestedCost(
        BigInteger loadMicrograms,
        ExperimentalAggregatedLandFreightCostFunction pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);
        if (loadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(loadMicrograms));
        // Convert from raw monetary cost PER GRAM to a cost for Q micrograms.
        // Apply the VS40 geographic and smoothed operating terms; intentionally
        // exclude VS40 standalone q^2 saturation to avoid double charging.
        var geographical = new FoodTransportCostFraction(
            loadMicrograms * pricing.GeographicBaseNumerator,
            MicrogramsPerGram * ExperimentalAggregatedLandFreightCostFunction.GeographicDenominator);
        var operations = new FoodTransportCostFraction(
            loadMicrograms * pricing.OverheadNumerator,
            MicrogramsPerGram *
                (loadMicrograms + pricing.OverheadSmoothingMicrograms));
        return Add(geographical, operations);
    }

    internal static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);
}
