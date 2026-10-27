using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// One explicitly constructed physical asset installed on a known geographic
/// strategic edge. Parallel assets may share an edge while retaining separate
/// identities and technology types. The snapshot is NOT authoritative
/// WorldState persistence and includes no anonymous investment upgrade.
/// </summary>
public sealed class EconomicTransportBuiltAsset
{
    public ulong AssetId { get; }
    public ulong TechnologyTypeId { get; }
    public ulong TechnologyCatalogVersion { get; }
    public StrategicEdgeId GeographicEdgeId { get; }
    public bool IsOperational { get; }

    public EconomicTransportBuiltAsset(
        ulong assetId, ulong technologyTypeId,
        StrategicEdgeId geographicEdgeId, ulong technologyCatalogVersion,
        bool isOperational = true)
    {
        if (assetId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(assetId));
        if (technologyTypeId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyTypeId));
        if (!geographicEdgeId.IsValid)
            throw new ArgumentException("Physical corridor edge must be valid.",
                nameof(geographicEdgeId));
        if (technologyCatalogVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(technologyCatalogVersion));
        AssetId = assetId;
        TechnologyTypeId = technologyTypeId;
        TechnologyCatalogVersion = technologyCatalogVersion;
        GeographicEdgeId = geographicEdgeId;
        IsOperational = isOperational;
    }
}

/// <summary>
/// A deliberately offered, directional, complete service itinerary, not an
/// inferred shortest path or permission to use all geographic adjacencies.
/// The caller supplies a function that prices the entire itinerary, including
/// every explicitly declared technology transfer/handling term.
/// Asset path order is the travel order; each transfer boundary is a zero-
/// based leg index after which technology changes. No hidden free transfer.
/// </summary>
public sealed class EconomicTransportServiceOffer
{
    private readonly IReadOnlyList<ulong> _assetPath;
    private readonly IReadOnlyList<int> _transferAfterLeg;

    public ulong ServiceId { get; }
    public StrategicEconomicRouteKey RouteKey { get; }
    public IReadOnlyList<ulong> AssetPath => _assetPath;
    public IReadOnlyList<int> TransferAfterLeg => _transferAfterLeg;
    public BigInteger ExpectedLoadMicrograms { get; }
    public IEconomicTurnFreightCostFunction CostFunction { get; }
    public bool IsAvailable { get; }

    public EconomicTransportServiceOffer(
        ulong serviceId, StrategicEconomicRouteKey routeKey,
        IEnumerable<ulong> assetPath,
        IEnumerable<int> transferAfterLeg,
        BigInteger expectedLoadMicrograms,
        IEconomicTurnFreightCostFunction costFunction,
        bool isAvailable = true)
    {
        if (serviceId == 0UL)
            throw new ArgumentOutOfRangeException(nameof(serviceId));
        if (!routeKey.SourceEconomicPointId.IsValid ||
            !routeKey.DestinationEconomicPointId.IsValid)
            throw new ArgumentException("Service requires valid economic point anchors.",
                nameof(routeKey));
        ArgumentNullException.ThrowIfNull(assetPath);
        ArgumentNullException.ThrowIfNull(transferAfterLeg);
        ArgumentNullException.ThrowIfNull(costFunction);
        if (expectedLoadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLoadMicrograms));
        if (costFunction.RulesetVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(costFunction));

        var legs = assetPath.ToArray();
        if (legs.Length == 0 || legs.Any(x => x == 0UL) ||
            legs.Distinct().Count() != legs.Length)
            throw new ArgumentException("An explicit service path needs unique valid asset IDs.",
                nameof(assetPath));
        var changes = transferAfterLeg.ToArray();
        if (changes.Any(x => x < 0 || x >= legs.Length - 1) ||
            changes.Distinct().Count() != changes.Length)
            throw new ArgumentException("Invalid or duplicate service transfer boundary.",
                nameof(transferAfterLeg));

        ServiceId = serviceId;
        RouteKey = routeKey;
        _assetPath = Array.AsReadOnly(legs);
        _transferAfterLeg = Array.AsReadOnly(changes.Order().ToArray());
        ExpectedLoadMicrograms = expectedLoadMicrograms;
        CostFunction = costFunction;
        IsAvailable = isAvailable;
    }
}

/// <summary>
/// A chosen offer and its stable physical provenance. Quotes can be supplied
/// directly to the existing economic allocator; this sidecar records which
/// service and built assets generated the route quote. Selection is commercial,
/// not a physical delivery attestation.
/// </summary>
public sealed class EconomicTransportServiceSelection
{
    private readonly IReadOnlyList<ulong> _assetIds;
    private readonly IReadOnlyList<ulong> _technologyIds;

    public ulong TechnologyCatalogVersion { get; }
    public ulong ServiceId { get; }
    public EconomicTurnFreightQuote Quote { get; }
    public IReadOnlyList<ulong> AssetIds => _assetIds;
    public IReadOnlyList<ulong> TechnologyTypeIds => _technologyIds;

    internal EconomicTransportServiceSelection(
        EconomicTransportServiceOffer offer,
        EconomicTurnFreightQuote quote,
        IReadOnlyList<EconomicTransportBuiltAsset> assets)
    {
        ServiceId = offer.ServiceId;
        Quote = quote;
        TechnologyCatalogVersion = assets[0].TechnologyCatalogVersion;
        _assetIds = Array.AsReadOnly(assets.Select(x => x.AssetId).ToArray());
        _technologyIds = Array.AsReadOnly(
            assets.Select(x => x.TechnologyTypeId).ToArray());
    }
}

/// <summary>
/// First non-authoritative adapter between geographic topology, explicitly
/// built assets, intentionally offered transport services and the existing
/// economic market. Evaluates each eligible service's continuous pre-turn
/// unit-price function exactly once; selects cheapest service per directional
/// economic-point pair with ServiceId as stable tie-breaker.
///
/// No route search, free topology travel, implicit modal switch, fixed modal
/// tariff, physical capacity cliff, persistent state or market-price freeze.
/// </summary>
public sealed class EconomicTransportServiceQuoteResolver
{
    public IReadOnlyList<EconomicTransportServiceSelection> Resolve(
        ulong turnOrdinal,
        StrategicSurfaceGraph geography,
        EconomyRuntimeState economy,
        EconomicProductionDemandResult flows,
        StrategicEdgeAccessSnapshot access,
        IEnumerable<EconomicTransportBuiltAsset> installedAssets,
        IEnumerable<EconomicTransportServiceOffer> offeredServices)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(installedAssets);
        ArgumentNullException.ThrowIfNull(offeredServices);

        var edgeById = geography.StrategicTopology.Edges.ToDictionary(x => x.Id);
        foreach (var closed in access.ClosedEdgeIds)
            if (!edgeById.ContainsKey(closed))
                throw new ArgumentException(
                    "Closed geographic edge is outside the supplied topology.",
                    nameof(access));

        var assets = new Dictionary<ulong, EconomicTransportBuiltAsset>();
        foreach (var asset in installedAssets)
        {
            if (asset is null || !edgeById.ContainsKey(asset.GeographicEdgeId) ||
                !assets.TryAdd(asset.AssetId, asset))
                throw new ArgumentException(
                    "Built asset must be unique and use an existing geographic edge.",
                    nameof(installedAssets));
        }

        var anchorCells = economy.EconomicPoints.ToDictionary(
            x => x.Id, x => x.StrategicCellId);
        foreach (var point in anchorCells.Values)
            geography.GetNodeIndex(point);

        var sourceFoods = flows.SupplyFlows
            .GroupBy(x => x.EconomicPointId)
            .ToDictionary(g => g.Key,
                g => g.Select(x => x.CommodityId).ToHashSet());
        var destinationFoods = flows.DemandFlows
            .GroupBy(x => x.EconomicPointId)
            .ToDictionary(g => g.Key,
                g => g.Select(x => x.CommodityId).ToHashSet());

        foreach (var point in sourceFoods.Keys.Concat(destinationFoods.Keys))
            if (!anchorCells.ContainsKey(point))
                throw new ArgumentException(
                    "Market economic point is missing its geographic anchor.",
                    nameof(economy));

        var serviceIds = new HashSet<ulong>();
        var best = new Dictionary<StrategicEconomicRouteKey,
            (EconomicTransportServiceOffer Offer, FoodTransportCostFraction Cost,
             EconomicTransportBuiltAsset[] Assets)>();

        foreach (var offer in offeredServices)
        {
            if (offer is null || !serviceIds.Add(offer.ServiceId))
                throw new ArgumentException(
                    "Service identity must be unique and non-null.",
                    nameof(offeredServices));
            if (!anchorCells.TryGetValue(offer.RouteKey.SourceEconomicPointId,
                    out var start) ||
                !anchorCells.TryGetValue(offer.RouteKey.DestinationEconomicPointId,
                    out var finish))
                throw new ArgumentException(
                    "Offered service must reference known economic points.",
                    nameof(offeredServices));

            var orderedAssets = new EconomicTransportBuiltAsset[offer.AssetPath.Count];
            var cursor = start;
            var expectedTransfers = new List<int>();
            var eligible = offer.IsAvailable;
            for (var i = 0; i < orderedAssets.Length; i++)
            {
                if (!assets.TryGetValue(offer.AssetPath[i], out var asset))
                    throw new ArgumentException(
                        "Service references an asset that was not built.",
                        nameof(offeredServices));
                orderedAssets[i] = asset;
                if (i > 0 && orderedAssets[0].TechnologyCatalogVersion !=
                    asset.TechnologyCatalogVersion)
                    throw new ArgumentException(
                        "One service cannot mix technology catalogue versions.",
                        nameof(offeredServices));
                var edge = edgeById[asset.GeographicEdgeId];
                var ends = edge.IncidentCellIds;
                if (cursor == ends[0])
                    cursor = ends[1];
                else if (cursor == ends[1])
                    cursor = ends[0];
                else
                    throw new ArgumentException(
                        "Service assets do not form a contiguous directed itinerary.",
                        nameof(offeredServices));

                if (i > 0 && orderedAssets[i - 1].TechnologyTypeId !=
                    asset.TechnologyTypeId)
                    expectedTransfers.Add(i - 1);
                if (!asset.IsOperational || !access.IsOpen(asset.GeographicEdgeId))
                    eligible = false;
            }

            if (cursor != finish ||
                !expectedTransfers.SequenceEqual(offer.TransferAfterLeg))
                throw new ArgumentException(
                    "Service endpoint or declared technology handoffs do not match its assets.",
                    nameof(offeredServices));

            if (!eligible || !sourceFoods.TryGetValue(
                    offer.RouteKey.SourceEconomicPointId, out var sources) ||
                !destinationFoods.TryGetValue(
                    offer.RouteKey.DestinationEconomicPointId, out var destinations) ||
                !sources.Overlaps(destinations))
                continue;

            // The function prices the *whole* declared itinerary, not a
            // hardcoded technology tariff or a sum of unchecked live costs.
            var cost = offer.CostFunction.EvaluateRawUnitCostPerGram(
                offer.ExpectedLoadMicrograms);
            if (cost is null)
                throw new InvalidOperationException(
                    "Eligible transport service returned no unit cost.");

            if (!best.TryGetValue(offer.RouteKey, out var previous) ||
                cost.CompareTo(previous.Cost) < 0 ||
                (cost.CompareTo(previous.Cost) == 0 &&
                 offer.ServiceId < previous.Offer.ServiceId))
                best[offer.RouteKey] = (offer, cost, orderedAssets);
        }

        var result = best
            .OrderBy(x => x.Key.SourceEconomicPointId.Value)
            .ThenBy(x => x.Key.DestinationEconomicPointId.Value)
            .Select(x => new EconomicTransportServiceSelection(
                x.Value.Offer,
                EconomicTurnFreightQuote.FromPreparedServiceUnitCost(
                    turnOrdinal, x.Key,
                    x.Value.Offer.CostFunction.RulesetVersion,
                    x.Value.Offer.ExpectedLoadMicrograms,
                    x.Value.Cost),
                x.Value.Assets))
            .ToArray();

        return Array.AsReadOnly(result);
    }
}
