using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A positive physical part of an already selected source/buyer allocation.
/// The parent retains its exact quantity and economic destination, while
/// every child follows one explicit fine-topology route. Never a sale.
/// </summary>
public sealed record FoundationPhysicalMultiPathDeliveryPart(
    ulong ParentDeliveryOrdinal,
    FoundationPhysicalCommodityDeliveryCandidate Delivery,
    ulong CargoMicro);

/// <summary>
/// Reconciled physical route parts for a fixed worker-backed market allocation.
/// End-of-batch congestion rates are a ROUTING HEURISTIC, not certified
/// simultaneous congestion equilibrium or realized financial settlement.
/// </summary>
public sealed class FoundationPhysicalMultiPathCandidatePlan
{
    public FoundationPhysicalGlobalMarginalCoordinationPlan MarketAllocation { get; }
    public IReadOnlyList<FoundationPhysicalMultiPathDeliveryPart> Parts { get; }
    public FoundationPhysicalDeliveryAllocationAudit AllocationAudit { get; }
    public IReadOnlyList<FoundationPhysicalCongestedEdgeReading> ProjectedEdgeCosts { get; }
    public int RoutingBatches { get; }

    internal FoundationPhysicalMultiPathCandidatePlan(
        FoundationPhysicalGlobalMarginalCoordinationPlan market,
        FoundationPhysicalMultiPathDeliveryPart[] parts,
        FoundationPhysicalDeliveryAllocationAudit audit,
        FoundationPhysicalCongestedEdgeReading[] edgeReadings,
        int batches)
    {
        MarketAllocation = market;
        Parts = Array.AsReadOnly(parts);
        AllocationAudit = audit;
        ProjectedEdgeCosts = Array.AsReadOnly(edgeReadings);
        RoutingBatches = batches;
    }
}

/// <summary>
/// Deterministic bounded multi-path ROUTE PLANNING after the global buyer
/// allocation has been selected. Each existing delivery is partitioned into
/// exact comparable-cargo quanta, and each batch is routed on the real graph
/// using the prospective congested edge rate for CURRENT LOAD + THIS BATCH.
/// Repeated batches on the same path are coalesced before strict auditing.
///
/// This is a deliberately provisional (greedy, unsplittable batch) planner:
/// it can represent two alternative physical paths for the SAME market
/// delivery, but cannot prove Wardrop/KKT equilibrium or global freight
/// optimality, and does not couple changed loads back to buyer prices.
/// </summary>
public static class FoundationPhysicalMultiPathFreightPlanner
{
    private sealed class PathBucket
    {
        internal readonly PhysicalTacticalTileId[] Tiles;
        internal ulong GoodsMicro;

        internal PathBucket(PhysicalTacticalTileId[] tiles, ulong goods)
        {
            Tiles = tiles;
            GoodsMicro = goods;
        }
    }

    private static FoundationPhysicalTradeRouteCandidatePlanner.RouteTree
        ProspectivelyCheapest(
            PhysicalTacticalTileId source,
            CivilizationId seller,
            ulong batchCargoMicro,
            IReadOnlyDictionary<ulong,
                List<FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge>> graph,
            IReadOnlyDictionary<PhysicalTacticalEdgeId, ulong> scales,
            IReadOnlyDictionary<PhysicalTacticalEdgeId, ulong> loads,
            int nodeCount)
    {
        var tree = new FoundationPhysicalTradeRouteCandidatePlanner.RouteTree(
            nodeCount);
        var start = source.FineStrategicCellId.Value;
        var startIndex = checked((int)start);
        tree.TotalRate[startIndex] = 0UL;
        tree.Hops[startIndex] = 0;
        var queue = new PriorityQueue<ulong, (ulong Cost, int Hops, ulong Node)>();
        queue.Enqueue(start, (0UL, 0, start));

        // Each undirected crossing has the same prospective load in BOTH
        // directions. Recompute only when the next batch has been assigned.
        var prospective = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        while (queue.TryDequeue(out var at, out var priority))
        {
            int current = checked((int)at);
            if (tree.TotalRate[current] != priority.Cost ||
                tree.Hops[current] != priority.Hops)
                continue;
            if (!graph.TryGetValue(at, out var links))
                continue;
            foreach (var link in links)
            {
                if (!prospective.TryGetValue(link.Edge, out var physical))
                {
                    var combined = checked(
                        loads.GetValueOrDefault(link.Edge) + batchCargoMicro);
                    physical =
                        FoundationPhysicalEndogenousCongestionFeedback
                            .CalculateCongestedRate(
                                link.PhysicalRate, combined,
                                scales[link.Edge]);
                    prospective.Add(link.Edge, physical);
                }

                var toll = link.InfrastructureOwner.HasValue &&
                    link.InfrastructureOwner.Value != seller
                    ? link.ExternalTariffRate : 0UL;
                var rate = checked(physical + toll);
                var candidate = checked(tree.TotalRate[current] + rate);
                var hops = checked(tree.Hops[current] + 1);
                int next = checked((int)link.Other);
                bool better = candidate < tree.TotalRate[next] ||
                    candidate == tree.TotalRate[next] &&
                    (hops < tree.Hops[next] ||
                     hops == tree.Hops[next] &&
                     (tree.Previous[next] == 0UL ||
                      at < tree.Previous[next]));
                if (!better)
                    continue;
                bool queueNeeded = candidate != tree.TotalRate[next] ||
                    hops != tree.Hops[next];
                tree.TotalRate[next] = candidate;
                tree.Hops[next] = hops;
                tree.Previous[next] = at;
                if (queueNeeded)
                    queue.Enqueue(link.Other, (candidate, hops, link.Other));
            }
        }
        return tree;
    }

    internal static FoundationPhysicalMultiPathCandidatePlan Split(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        FoundationPhysicalGlobalMarginalCoordinationPlan allocation,
        FoundationPhysicalRoutingEdgeCost[] freeEdges,
        FoundationPhysicalEdgeCarryingScale[] scales,
        int maxBatchesPerDelivery)
    {
        if (maxBatchesPerDelivery < 1)
            throw new ArgumentOutOfRangeException(nameof(maxBatchesPerDelivery),
                "A strictly positive route-batch computational budget is required.");

        var (graph, owners) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, freeEdges);
        var byEdge = freeEdges.ToDictionary(line => line.Edge);
        var scaleMap = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        foreach (var scale in scales)
        {
            if (scale is null || !scale.Edge.IsValid ||
                scale.Edge.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                scale.CargoMicroAtReferenceLoad == 0UL ||
                !scaleMap.TryAdd(scale.Edge, scale.CargoMicroAtReferenceLoad))
                throw new ArgumentException(
                    "A real physical crossing needs one positive common-unit " +
                    "reference carrying scale.", nameof(scales));
        }
        if (scaleMap.Count != byEdge.Count ||
            byEdge.Keys.Any(edge => !scaleMap.ContainsKey(edge)))
            throw new ArgumentException(
                "Carrying scales must cover exactly the declared road graph.",
                nameof(scales));

        // Revalidate the entire parent plan against the SAME real workers,
        // demand catalog, exact mass conversions, path and ownership policy.
        // Clients cannot pass a forged amount or fictitious demand destination.
        var parents = allocation.Candidates
            .OrderBy(x => x.DeliveryOrdinal).ToArray();
        var parentAudit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            parents, owners);
        if (parentAudit.TotalAllocatedGoodsMicro !=
                allocation.AllocationAudit.TotalAllocatedGoodsMicro ||
            parentAudit.DeliveryCount != allocation.Candidates.Count)
            throw new InvalidOperationException(
                "Source market plan disagrees with the current physical census.");

        var loads = byEdge.Keys.ToDictionary(edge => edge, _ => 0UL);
        var parts = new List<FoundationPhysicalMultiPathDeliveryPart>();
        int totalBatches = 0;
        var fine = incidence.Refinement.FineParameters;
        int cellCount = incidence.FineTopology.Cells.Count;

        foreach (var original in parents)
        {
            if (original.SourceTile.FineGoldbergParameters != fine ||
                original.BuyerTile.FineGoldbergParameters != fine ||
                original.SourceTile.FineStrategicCellId.Value > (ulong)cellCount ||
                original.BuyerTile.FineStrategicCellId.Value > (ulong)cellCount)
                throw new ArgumentException(
                    "Route split cannot use another world's fine topology.");

            ulong perWhole = conversionPolicy.CargoMicro(
                original.Commodity, 1_000_000UL);
            ulong quantum = 1_000_000UL /
                FoundationPhysicalMarginalMarketDistributor.Gcd(
                    1_000_000UL, perWhole);
            if (original.GoodsMicro % quantum != 0UL)
                throw new InvalidOperationException(
                    "Parent's goods are indivisible in comparable cargo micro-units.");
            ulong available = original.GoodsMicro / quantum;
            int batches = checked((int)Math.Min(
                available, (ulong)maxBatchesPerDelivery));
            if (batches == 0)
                throw new InvalidOperationException(
                    "Positive physical delivery has no representable transport batch.");
            ulong baseQuanta = available / (ulong)batches;
            ulong remainder = available % (ulong)batches;
            totalBatches = checked(totalBatches + batches);

            var grouped = new List<PathBucket>();
            var routeIndex = new Dictionary<string, int>();
            for (int i = 0; i < batches; i++)
            {
                ulong goods = checked((baseQuanta +
                    ((ulong)i < remainder ? 1UL : 0UL)) * quantum);
                ulong cargo = conversionPolicy.CargoMicro(
                    original.Commodity, goods);

                PhysicalTacticalTileId[] route;
                if (original.SourceTile == original.BuyerTile)
                    route = new[] { original.SourceTile };
                else
                {
                    var tree = ProspectivelyCheapest(
                        original.SourceTile, original.Seller,
                        cargo, graph, scaleMap, loads, cellCount);
                    int buyerIndex = checked((int)original.BuyerTile
                        .FineStrategicCellId.Value);
                    if (tree.TotalRate[buyerIndex] == ulong.MaxValue)
                        throw new InvalidOperationException(
                            "No real traversable route connects this worker " +
                            "source to the selected real buyer.");
                    route =
                        FoundationPhysicalTradeRouteCandidatePlanner.Reconstruct(
                            original.SourceTile, original.BuyerTile, tree);
                }

                for (int j = 1; j < route.Length; j++)
                {
                    ulong from = route[j - 1].FineStrategicCellId.Value;
                    ulong to = route[j].FineStrategicCellId.Value;
                    var crossing = graph[from].Single(link =>
                        link.Other == to).Edge;
                    loads[crossing] = checked(loads[crossing] + cargo);
                }

                // Coalesce batches with identical routes, without coalescing
                // deliveries from different producers/buyers or commodities.
                var key = string.Join(",", route.Select(tile =>
                    tile.FineStrategicCellId.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
                if (!routeIndex.TryGetValue(key, out var group))
                {
                    group = grouped.Count;
                    routeIndex.Add(key, group);
                    grouped.Add(new PathBucket(route, goods));
                }
                else
                    grouped[group].GoodsMicro =
                        checked(grouped[group].GoodsMicro + goods);
            }
            if (grouped.Aggregate(BigInteger.Zero,
                    (sum, path) => sum + path.GoodsMicro) !=
                original.GoodsMicro)
                throw new InvalidOperationException(
                    "Route partition did not conserve the parent's goods.");

            foreach (var group in grouped)
            {
                var candidate = original with
                {
                    DeliveryOrdinal = checked((ulong)parts.Count + 1UL),
                    GoodsMicro = group.GoodsMicro,
                    RouteTiles = Array.AsReadOnly(group.Tiles)
                };
                parts.Add(new FoundationPhysicalMultiPathDeliveryPart(
                    original.DeliveryOrdinal, candidate,
                    conversionPolicy.CargoMicro(
                        candidate.Commodity, candidate.GoodsMicro)));
            }
        }

        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            parts.Select(x => x.Delivery), owners);
        if (audit.TotalAllocatedGoodsMicro !=
                parentAudit.TotalAllocatedGoodsMicro ||
            parts.Aggregate(BigInteger.Zero,
                (sum, item) => sum + item.CargoMicro) !=
                audit.Freight.TotalDeliveredCargoMicro)
            throw new InvalidOperationException(
                "Multi-path goods or cargo do not reconcile to the original market.");

        var actual = audit.Freight.UsedEdges.ToDictionary(
            edge => edge.Edge, edge => edge.TotalCargoMicro);
        var readings = freeEdges.OrderBy(edge =>
            edge.Edge.FineStrategicEdgeId.Value).Select(edge =>
            new FoundationPhysicalCongestedEdgeReading(
                edge.Edge, actual.GetValueOrDefault(edge.Edge),
                scaleMap[edge.Edge], edge.PhysicalCostMicroPerWholeCargoUnit,
                FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                    edge.PhysicalCostMicroPerWholeCargoUnit,
                    actual.GetValueOrDefault(edge.Edge),
                    scaleMap[edge.Edge]),
                edge.OwnershipKind, edge.InfrastructureOwner,
                edge.ExternalTariffMicroPerWholeCargoUnit)).ToArray();
        if (readings.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.CargoMicro) !=
            audit.Freight.TotalEdgeTraversalCargoMicro ||
            loads.Any(pair =>
                pair.Value != actual.GetValueOrDefault(pair.Key)))
            throw new InvalidOperationException(
                "Prospective multi-path cargo differs from the final audited " +
                "per-edge total and therefore cannot be used by the overlay.");

        return new FoundationPhysicalMultiPathCandidatePlan(
            allocation, parts.ToArray(), audit, readings, totalBatches);
    }

    /// <summary>
    /// Trusted candidate-only entry point. Buyer/commodity quantities come
    /// from the existing multi-producer solver; this method only selects and
    /// splits physical paths. It neither liquidates money nor publishes a
    /// completed economic cycle. No global congestion fixed point is claimed.
    /// </summary>
    public static FoundationPhysicalMultiPathCandidatePlan Plan(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int maxProducerPasses,
        int maxBatchesPerDelivery,
        bool preferIncumbentThresholdTies = false)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (maxProducerPasses <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxProducerPasses));
        if (maxBatchesPerDelivery <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBatchesPerDelivery));
        var quoteList = quotes.ToArray();
        var edges = freeFlowEdgeCosts.OrderBy(
            x => x?.Edge.FineStrategicEdgeId.Value ?? 0UL).ToArray();
        var scales = carryingScales.ToArray();
        var market = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            census, incidence, demandPolicy, conversionPolicy,
            quoteList, edges, maxProducerPasses,
            preferIncumbentThresholdTies);
        return Split(census, incidence, demandPolicy, conversionPolicy,
            market, edges, scales, maxBatchesPerDelivery);
    }
}
