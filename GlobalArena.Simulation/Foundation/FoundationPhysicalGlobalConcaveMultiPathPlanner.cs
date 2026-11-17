using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Candidate-only coupling of GLOBAL segmented producer/market quantities
/// with a bounded physical multipath routing heuristic. All per-worker,
/// buyer, item and goods quantities remain unchanged. The integrated
/// physical network + third-party tariff potential is compared against
/// the original one-path allocation, and any non-improving routing proposal
/// is discarded. This is not a coupled market/congestion equilibrium.
/// </summary>
public sealed class FoundationPhysicalGlobalConcaveMultiPathPlan
{
    public FoundationPhysicalGlobalConcaveFlowPlan MarketAllocation { get; }
    public IReadOnlyList<FoundationPhysicalMultiPathDeliveryPart> Parts { get; }
    public FoundationPhysicalDeliveryAllocationAudit AllocationAudit { get; }
    public IReadOnlyList<FoundationPhysicalCongestedEdgeReading> ProjectedEdges { get; }
    public int RouteBatchDecisions { get; }
    public bool AppliedStrictImprovement { get; }
    public BigInteger BaselineIntegratedFreightCost { get; }
    public BigInteger FinalIntegratedFreightCost { get; }

    internal FoundationPhysicalGlobalConcaveMultiPathPlan(
        FoundationPhysicalGlobalConcaveFlowPlan market,
        FoundationPhysicalMultiPathDeliveryPart[] parts,
        FoundationPhysicalDeliveryAllocationAudit audit,
        FoundationPhysicalCongestedEdgeReading[] projected,
        int batchDecisions, bool improved,
        BigInteger baseline, BigInteger final)
    {
        MarketAllocation = market;
        Parts = Array.AsReadOnly(parts);
        AllocationAudit = audit;
        ProjectedEdges = Array.AsReadOnly(projected);
        RouteBatchDecisions = batchDecisions;
        AppliedStrictImprovement = improved;
        BaselineIntegratedFreightCost = baseline;
        FinalIntegratedFreightCost = final;
    }
}

/// <summary>
/// New opt-in adapter for the global #398 flow result, independent of the
/// legacy #391 global coordinator and its #394 multipath plan type.
/// Within fixed market destinations, routes each exact-quantity batch by
/// prospective quartic edge cost, then checks the COMPLETE INTEGRATED
/// network potential on the independently audited final shipments.
/// It never accepts a routing result that fails strict improvement.
/// </summary>
public static class FoundationPhysicalGlobalConcaveMultiPathPlanner
{
    private const ulong Micro = FoundationPhysicalMarginalMarketDistributor.Micro;

    private sealed class Bucket
    {
        internal PhysicalTacticalTileId[] Route { get; }
        internal ulong Goods { get; set; }
        internal Bucket(PhysicalTacticalTileId[] route, ulong goods)
        {
            Route = route;
            Goods = goods;
        }
    }

    private static FoundationPhysicalTradeRouteCandidatePlanner.RouteTree
        ProspectiveTree(
            PhysicalTacticalTileId source, CivilizationId seller,
            ulong cargo, int cells,
            IReadOnlyDictionary<ulong, List<
                FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge>> graph,
            IReadOnlyDictionary<PhysicalTacticalEdgeId, ulong> load,
            IReadOnlyDictionary<PhysicalTacticalEdgeId, ulong> scale)
    {
        var tree = new FoundationPhysicalTradeRouteCandidatePlanner.RouteTree(cells);
        ulong origin = source.FineStrategicCellId.Value;
        tree.TotalRate[checked((int)origin)] = 0UL;
        tree.Hops[checked((int)origin)] = 0;
        var costs = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        var queue = new PriorityQueue<ulong, (ulong Rate, int Hops, ulong Node)>();
        queue.Enqueue(origin, (0UL, 0, origin));

        while (queue.TryDequeue(out ulong at, out var priority))
        {
            int index = checked((int)at);
            if (tree.TotalRate[index] != priority.Rate ||
                tree.Hops[index] != priority.Hops)
                continue;
            if (!graph.TryGetValue(at, out var links))
                continue;
            foreach (var edge in links)
            {
                if (!costs.TryGetValue(edge.Edge, out ulong congested))
                {
                    congested = FoundationPhysicalEndogenousCongestionFeedback
                        .CalculateCongestedRate(edge.PhysicalRate,
                            checked(load[edge.Edge] + cargo), scale[edge.Edge]);
                    costs.Add(edge.Edge, congested);
                }
                ulong tariff = edge.InfrastructureOwner.HasValue &&
                    edge.InfrastructureOwner.Value != seller
                    ? edge.ExternalTariffRate : 0UL;
                ulong rate = checked(congested + tariff);
                ulong nextRate = checked(tree.TotalRate[index] + rate);
                int hops = checked(tree.Hops[index] + 1);
                int next = checked((int)edge.Other);
                bool better = nextRate < tree.TotalRate[next] ||
                    nextRate == tree.TotalRate[next] &&
                    (hops < tree.Hops[next] ||
                     hops == tree.Hops[next] &&
                     (tree.Previous[next] == 0UL ||
                      at < tree.Previous[next]));
                if (!better)
                    continue;
                bool enqueue = nextRate != tree.TotalRate[next] ||
                    hops != tree.Hops[next];
                tree.TotalRate[next] = nextRate;
                tree.Hops[next] = hops;
                tree.Previous[next] = at;
                if (enqueue)
                    queue.Enqueue(edge.Other, (nextRate, hops, edge.Other));
            }
        }
        return tree;
    }

    public static FoundationPhysicalGlobalConcaveMultiPathPlan Plan(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int segmentsPerAverageMarket,
        int maxFlowAugmentations,
        int maxRouteBatchesPerParent,
        int maxTotalRouteBatches)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (segmentsPerAverageMarket < 1 || maxFlowAugmentations < 1 ||
            maxRouteBatchesPerParent < 1 || maxTotalRouteBatches < 1)
            throw new ArgumentOutOfRangeException(
                "Explicit positive flow and routing budgets are required.");

        var roads = freeFlowEdgeCosts.ToArray();
        var scales = carryingScales.ToArray();
        var market = FoundationPhysicalGlobalConcaveFlowPlanner.Coordinate(
            census, incidence, demandPolicy, conversionPolicy,
            quotes.ToArray(), roads, segmentsPerAverageMarket,
            maxFlowAugmentations);
        return RouteFixedMarket(census, incidence, demandPolicy,
            conversionPolicy, market, roads, scales,
            maxRouteBatchesPerParent, maxTotalRouteBatches);
    }

    /// <summary>
    /// Opt-in routing step for an already globally allocated, uncommitted
    /// market response. The entire response is reaudited against the current
    /// worker census and its physical edges; no market is silently changed.
    /// </summary>
    internal static FoundationPhysicalGlobalConcaveMultiPathPlan RouteFixedMarket(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        FoundationPhysicalGlobalConcaveFlowPlan market,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int maxRouteBatchesPerParent,
        int maxTotalRouteBatches)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (maxRouteBatchesPerParent < 1 || maxTotalRouteBatches < 1)
            throw new ArgumentOutOfRangeException(
                "Routing batch budgets must be positive.");

        var roads = freeFlowEdgeCosts.OrderBy(
            road => road?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        var (graph, owners) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(incidence, roads);
        var byEdge = roads.ToDictionary(road => road.Edge);
        var directed = new Dictionary<(ulong From, ulong To),
            FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge>();
        foreach (var (from, links) in graph)
            foreach (var link in links)
                if (!directed.TryAdd((from, link.Other), link))
                    throw new InvalidOperationException(
                        "Duplicate directed crossing in physical topology.");

        var scales = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        foreach (var line in carryingScales)
        {
            if (line is null || !line.Edge.IsValid ||
                line.Edge.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                line.CargoMicroAtReferenceLoad == 0UL ||
                !scales.TryAdd(line.Edge, line.CargoMicroAtReferenceLoad))
                throw new ArgumentException(
                    "Every traversable physical edge needs exactly one " +
                    "positive, same-topology reference cargo scale.",
                    nameof(carryingScales));
        }
        if (scales.Count != roads.Length ||
            roads.Any(road => !scales.ContainsKey(road.Edge)))
            throw new ArgumentException(
                "Carrying scales must cover ALL and ONLY declared roads.",
                nameof(carryingScales));

        var parents = market.Candidates.OrderBy(p => p.DeliveryOrdinal).ToArray();

        var baseline = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy, parents, owners);
        if (baseline.TotalAllocatedGoodsMicro !=
                market.AllocationAudit.TotalAllocatedGoodsMicro ||
            baseline.TotalAvailableGoodsMicro !=
                market.AllocationAudit.TotalAvailableGoodsMicro)
            throw new InvalidOperationException(
                "Global market quantities disagree with the worker-backed audit.");

        IEnumerable<FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge>
            Crossings(IReadOnlyList<PhysicalTacticalTileId> route)
        {
            for (int i = 1; i < route.Count; i++)
            {
                var key = (route[i - 1].FineStrategicCellId.Value,
                    route[i].FineStrategicCellId.Value);
                if (!directed.TryGetValue(key, out var edge))
                    throw new InvalidOperationException(
                        "A candidate attempts an undeclared physical crossing.");
                yield return edge;
            }
        }

        var loads = roads.ToDictionary(road => road.Edge, _ => 0UL);
        var parts = new List<FoundationPhysicalMultiPathDeliveryPart>();
        int totalBatches = 0;
        int cells = incidence.FineTopology.Cells.Count;
        foreach (var parent in parents)
        {
            ulong wholeCargo = conversionPolicy.CargoMicro(
                parent.Commodity, Micro);
            ulong quantum = Micro /
                FoundationPhysicalMarginalMarketDistributor.Gcd(
                    Micro, wholeCargo);
            if (parent.GoodsMicro % quantum != 0UL)
                throw new InvalidOperationException(
                    "Global market goods violate exact cargo granularity.");
            ulong available = parent.GoodsMicro / quantum;
            if (available == 0UL)
                throw new InvalidOperationException(
                    "Positive parent delivery has no cargo quantum.");
            int count = checked((int)Math.Min(
                available, (ulong)maxRouteBatchesPerParent));
            if (totalBatches > maxTotalRouteBatches - count)
                throw new InvalidOperationException(
                    "Global multipath exceeded the declared total batch budget; " +
                    "no partially routed market is published.");
            totalBatches = checked(totalBatches + count);
            ulong even = available / (ulong)count;
            ulong remainder = available % (ulong)count;
            var buckets = new List<Bucket>();
            var bySignature = new Dictionary<string, int>();

            for (int part = 0; part < count; part++)
            {
                ulong goods = checked((even +
                    ((ulong)part < remainder ? 1UL : 0UL)) * quantum);
                ulong cargo = conversionPolicy.CargoMicro(parent.Commodity, goods);
                PhysicalTacticalTileId[] route;
                if (parent.SourceTile == parent.BuyerTile)
                    route = new[] { parent.SourceTile };
                else
                {
                    var routes = ProspectiveTree(
                        parent.SourceTile, parent.Seller, cargo, cells,
                        graph, loads, scales);
                    int destination = checked((int)parent.BuyerTile
                        .FineStrategicCellId.Value);
                    if (routes.TotalRate[destination] == ulong.MaxValue)
                        throw new InvalidOperationException(
                            "A real parent delivery became physically unreachable.");
                    route = FoundationPhysicalTradeRouteCandidatePlanner
                        .Reconstruct(parent.SourceTile, parent.BuyerTile, routes);
                }

                foreach (var edge in Crossings(route))
                    loads[edge.Edge] = checked(loads[edge.Edge] + cargo);
                string signature = string.Join(",", route.Select(tile =>
                    tile.FineStrategicCellId.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
                if (!bySignature.TryGetValue(signature, out int index))
                {
                    bySignature.Add(signature, buckets.Count);
                    buckets.Add(new Bucket(route, goods));
                }
                else
                    buckets[index].Goods = checked(buckets[index].Goods + goods);
            }
            foreach (var bucket in buckets)
            {
                var delivery = parent with
                {
                    DeliveryOrdinal = checked((ulong)parts.Count + 1UL),
                    GoodsMicro = bucket.Goods,
                    RouteTiles = Array.AsReadOnly(bucket.Route)
                };
                parts.Add(new FoundationPhysicalMultiPathDeliveryPart(
                    parent.DeliveryOrdinal, delivery,
                    conversionPolicy.CargoMicro(delivery.Commodity,
                        delivery.GoodsMicro)));
            }
        }

        var parentById = parents.ToDictionary(p => p.DeliveryOrdinal);
        var grouped = parts.GroupBy(p => p.ParentDeliveryOrdinal)
            .ToDictionary(g => g.Key, g => g.ToArray());
        foreach (var (id, parent) in parentById)
        {
            if (!grouped.TryGetValue(id, out var children) ||
                children.Aggregate(BigInteger.Zero,
                    (sum, item) => sum + item.Delivery.GoodsMicro) !=
                parent.GoodsMicro ||
                children.Any(child =>
                    child.Delivery.Seller != parent.Seller ||
                    child.Delivery.SourceTile != parent.SourceTile ||
                    child.Delivery.SourceOccupation != parent.SourceOccupation ||
                    child.Delivery.Buyer != parent.Buyer ||
                    child.Delivery.BuyerTile != parent.BuyerTile ||
                    child.Delivery.Commodity != parent.Commodity))
                throw new InvalidOperationException(
                    "Multipath routing changed a real parent delivery.");
        }
        if (grouped.Count != parents.Length)
            throw new InvalidOperationException(
                "Multipath invented a parent delivery.");

        var splitAudit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            parts.Select(part => part.Delivery), owners);
        if (splitAudit.TotalAllocatedGoodsMicro !=
                baseline.TotalAllocatedGoodsMicro ||
            splitAudit.TotalAvailableGoodsMicro !=
                baseline.TotalAvailableGoodsMicro ||
            splitAudit.Freight.TotalDeliveredCargoMicro !=
                baseline.Freight.TotalDeliveredCargoMicro)
            throw new InvalidOperationException(
                "Multipath freight failed complete physical conservation.");

        BigInteger Objective(
            IReadOnlyList<FoundationPhysicalCommodityDeliveryCandidate> deliveries,
            out Dictionary<PhysicalTacticalEdgeId, ulong> edgeLoads)
        {
            edgeLoads = roads.ToDictionary(road => road.Edge, _ => 0UL);
            BigInteger tolls = BigInteger.Zero;
            foreach (var candidate in deliveries)
            {
                ulong cargo = conversionPolicy.CargoMicro(
                    candidate.Commodity, candidate.GoodsMicro);
                foreach (var edge in Crossings(candidate.RouteTiles))
                {
                    edgeLoads[edge.Edge] =
                        checked(edgeLoads[edge.Edge] + cargo);
                    if (edge.InfrastructureOwner.HasValue &&
                        edge.InfrastructureOwner.Value != candidate.Seller)
                        tolls += new BigInteger(edge.ExternalTariffRate) * cargo;
                }
            }
            BigInteger total = tolls;
            foreach (var road in roads)
                total += FoundationPhysicalMarginalPathRebalancer.IntegratedPhysicalCost(
                    road.PhysicalCostMicroPerWholeCargoUnit,
                    edgeLoads[road.Edge], scales[road.Edge]);
            return total;
        }

        BigInteger baselineCost = Objective(parents, out var baselineLoads);
        BigInteger proposedCost = Objective(
            parts.Select(p => p.Delivery).ToArray(), out var routedLoads);
        var observed = splitAudit.Freight.UsedEdges.ToDictionary(
            x => x.Edge, x => x.TotalCargoMicro);
        if (routedLoads.Any(p =>
                p.Value != observed.GetValueOrDefault(p.Key)) ||
            loads.Any(p => p.Value != routedLoads[p.Key]))
            throw new InvalidOperationException(
                "Multipath edge loads conflict with independent freight audit.");

        // The batch heuristic alone may make locally greedy, globally
        // unprofitable route changes. Never accept that regression.
        bool improved = proposedCost < baselineCost;
        if (!improved)
        {
            parts = parents.Select(parent =>
                new FoundationPhysicalMultiPathDeliveryPart(
                    parent.DeliveryOrdinal, parent,
                    conversionPolicy.CargoMicro(
                        parent.Commodity, parent.GoodsMicro))).ToList();
            routedLoads = baselineLoads;
        }
        BigInteger finalCost = improved ? proposedCost : baselineCost;
        var acceptedAudit = improved ? splitAudit : baseline;
        var readings = roads.Select(road =>
            new FoundationPhysicalCongestedEdgeReading(
                road.Edge, routedLoads[road.Edge], scales[road.Edge],
                road.PhysicalCostMicroPerWholeCargoUnit,
                FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                    road.PhysicalCostMicroPerWholeCargoUnit,
                    routedLoads[road.Edge], scales[road.Edge]),
                road.OwnershipKind, road.InfrastructureOwner,
                road.ExternalTariffMicroPerWholeCargoUnit)).ToArray();
        if (readings.Aggregate(BigInteger.Zero,
                (sum, edge) => sum + edge.CargoMicro) !=
            acceptedAudit.Freight.TotalEdgeTraversalCargoMicro)
            throw new InvalidOperationException(
                "Selected route parts disagree with audited shared cargo loads.");

        return new FoundationPhysicalGlobalConcaveMultiPathPlan(
            market, parts.ToArray(), acceptedAudit, readings,
            totalBatches, improved, baselineCost, finalCost);
    }
}
