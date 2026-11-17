using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A stable one-fragment-at-a-time physical routing plan. The potential
/// is in CargoMicro * MicroMoneyPerWholeCargoUnit comparison units, NOT
/// settled currency, buyer prices or receipts. This is a discrete local
/// minimum for its explicit physical fragment granularity only.
/// </summary>
public sealed class FoundationPhysicalMarginalPathRebalancingPlan
{
    public FoundationPhysicalMultiPathCandidatePlan Freight { get; }
    public BigInteger InitialIntegratedCost { get; }
    public BigInteger FinalIntegratedCost { get; }
    public int ImprovementPasses { get; }
    public int ReassignedFragments { get; }
    public int PhysicalFragments { get; }
    public bool StableUnderFullMarginalPass { get; }

    internal FoundationPhysicalMarginalPathRebalancingPlan(
        FoundationPhysicalMultiPathCandidatePlan freight,
        BigInteger initial, BigInteger final, int passes, int changes,
        int fragments)
    {
        Freight = freight;
        InitialIntegratedCost = initial;
        FinalIntegratedCost = final;
        ImprovementPasses = passes;
        ReassignedFragments = changes;
        PhysicalFragments = fragments;
        StableUnderFullMarginalPass = true;
    }
}

/// <summary>
/// Strict coordinate improvement of an ALREADY AUDITED multipath worker
/// allocation (#394). Each fragment may change its physical route, never its
/// buyer, seller, commodity or sold quantity. For each physical edge e:
///
///   potential_e(L) = free_e * [L + ceil(L^5 / reference_e^4)]
///
/// All units are exact BigInteger-based cargo-micro x money-rate comparison
/// units, not rounded end-of-cycle money. Owner-exempt or external tariffs
/// add a LINEAR seller-specific per-crossing term.
///
/// After temporarily removing a fragment's load, Dijkstra finds the actual
/// CHEAPEST INCREMENTAL INTEGRATED COST to re-add that same fragment. A
/// reroute occurs ONLY on strict improvement; the combined physical and
/// toll potential must strictly decrease on each modified full pass.
/// A complete pass with no changes certifies only a DISCRETE local routing
/// optimum for the declared fragment budget. It does not certify continuous
/// Wardrop equilibrium, optimal markets, taxes or monetary settlement.
/// </summary>
public static class FoundationPhysicalMarginalPathRebalancer
{
    private sealed class Fragment
    {
        internal FoundationPhysicalMultiPathDeliveryPart Origin { get; }
        internal ulong Goods { get; }
        internal ulong Cargo { get; }
        internal PhysicalTacticalTileId[] Route { get; set; }

        internal Fragment(FoundationPhysicalMultiPathDeliveryPart origin,
            ulong goods, ulong cargo, PhysicalTacticalTileId[] route)
        {
            Origin = origin;
            Goods = goods;
            Cargo = cargo;
            Route = route;
        }
    }

    /// <summary>
    /// Integer, non-decreasing and non-capped physical edge potential.
    /// Its discrete derivative is used both to compare prospective paths
    /// and to verify the actual total objective after rerouting.
    /// </summary>
    internal static BigInteger IntegratedPhysicalCost(
        ulong freeRate, ulong cargo, ulong scale)
    {
        if (freeRate == 0UL || scale == 0UL)
            throw new ArgumentOutOfRangeException(nameof(scale),
                "Every physical edge needs an explicit positive rate and scale.");
        if (cargo == 0UL)
            return BigInteger.Zero;
        BigInteger load = cargo;
        BigInteger denominator = BigInteger.Pow(new BigInteger(scale), 4);
        BigInteger congestionCargo =
            (BigInteger.Pow(load, 5) + denominator - BigInteger.One) /
            denominator;
        return new BigInteger(freeRate) * (load + congestionCargo);
    }

    private static Dictionary<(ulong From, ulong To),
        FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge> IndexGraph(
        IReadOnlyDictionary<ulong, List<
            FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge>> graph)
    {
        var lookup = new Dictionary<(ulong, ulong),
            FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge>();
        foreach (var (from, links) in graph)
            foreach (var link in links)
                if (!lookup.TryAdd((from, link.Other), link))
                    throw new InvalidOperationException(
                        "Duplicate directed physical border in the fine graph.");
        return lookup;
    }

    private static IEnumerable<
        FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge> Crossings(
        IReadOnlyList<PhysicalTacticalTileId> route,
        IReadOnlyDictionary<(ulong From, ulong To),
            FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge> edges)
    {
        for (int i = 1; i < route.Count; i++)
        {
            var key = (route[i - 1].FineStrategicCellId.Value,
                route[i].FineStrategicCellId.Value);
            if (!edges.TryGetValue(key, out var crossing))
                throw new InvalidOperationException(
                    "A path attempts a crossing absent from declared physical roads.");
            yield return crossing;
        }
    }

    private static PhysicalTacticalTileId[] CheapestIncrementalRoute(
        Fragment fragment, int cellCount,
        PhysicalTacticalIncidenceMap incidence,
        IReadOnlyDictionary<ulong, List<
            FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge>> graph,
        Func<FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge,
            BigInteger> cost)
    {
        var start = fragment.Origin.Delivery.SourceTile;
        var goal = fragment.Origin.Delivery.BuyerTile;
        if (start == goal)
            return new[] { start };

        var distance = new BigInteger?[cellCount + 1];
        var hops = Enumerable.Repeat(int.MaxValue, cellCount + 1).ToArray();
        var predecessor = new ulong[cellCount + 1];
        int origin = checked((int)start.FineStrategicCellId.Value);
        int target = checked((int)goal.FineStrategicCellId.Value);
        distance[origin] = BigInteger.Zero;
        hops[origin] = 0;
        var queue = new PriorityQueue<ulong, (BigInteger Cost,
            int Hops, ulong Node)>();
        queue.Enqueue(start.FineStrategicCellId.Value,
            (BigInteger.Zero, 0, start.FineStrategicCellId.Value));
        while (queue.TryDequeue(out var at, out var priority))
        {
            var index = checked((int)at);
            if (distance[index] != priority.Cost ||
                hops[index] != priority.Hops)
                continue;
            if (at == goal.FineStrategicCellId.Value)
                break;
            if (!graph.TryGetValue(at, out var outgoing))
                continue;
            foreach (var link in outgoing)
            {
                int next = checked((int)link.Other);
                var proposed = priority.Cost + cost(link);
                int hopCount = checked(priority.Hops + 1);
                bool better = !distance[next].HasValue ||
                    proposed < distance[next] ||
                    proposed == distance[next] &&
                    (hopCount < hops[next] ||
                     hopCount == hops[next] &&
                     (predecessor[next] == 0UL ||
                      at < predecessor[next]));
                if (!better)
                    continue;
                bool enqueue = !distance[next].HasValue ||
                    proposed != distance[next] ||
                    hopCount != hops[next];
                distance[next] = proposed;
                hops[next] = hopCount;
                predecessor[next] = at;
                if (enqueue)
                    queue.Enqueue(link.Other,
                        (proposed, hopCount, link.Other));
            }
        }
        if (!distance[target].HasValue)
            throw new InvalidOperationException(
                "Real buyer became unreachable over declared physical edges.");
        var route = new List<PhysicalTacticalTileId>();
        ulong cursor = goal.FineStrategicCellId.Value;
        for (int i = 0; i <= cellCount; i++)
        {
            route.Add(new PhysicalTacticalTileId(
                incidence.Refinement.FineParameters,
                new StrategicCellId(cursor)));
            if (cursor == start.FineStrategicCellId.Value)
            {
                route.Reverse();
                return route.ToArray();
            }
            cursor = predecessor[checked((int)cursor)];
            if (cursor == 0UL)
                break;
        }
        throw new InvalidOperationException(
            "Marginal path search returned an invalid predecessor chain.");
    }

    public static FoundationPhysicalMarginalPathRebalancingPlan Rebalance(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        FoundationPhysicalMultiPathCandidatePlan initial,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> freeFlowEdgeCosts,
        IEnumerable<FoundationPhysicalEdgeCarryingScale> carryingScales,
        int maxImprovementPasses,
        int maxFragmentsPerPart)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(freeFlowEdgeCosts);
        ArgumentNullException.ThrowIfNull(carryingScales);
        if (maxImprovementPasses < 1)
            throw new ArgumentOutOfRangeException(nameof(maxImprovementPasses));
        if (maxFragmentsPerPart < 1)
            throw new ArgumentOutOfRangeException(nameof(maxFragmentsPerPart));

        var roads = freeFlowEdgeCosts
            .OrderBy(edge => edge?.Edge.FineStrategicEdgeId.Value ?? 0UL)
            .ToArray();
        var (graph, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, roads);
        var directed = IndexGraph(graph);
        var byEdge = roads.ToDictionary(edge => edge.Edge);
        var capacities = new Dictionary<PhysicalTacticalEdgeId, ulong>();
        foreach (var line in carryingScales)
        {
            if (line is null || !line.Edge.IsValid ||
                line.Edge.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                line.CargoMicroAtReferenceLoad == 0UL ||
                !capacities.TryAdd(line.Edge, line.CargoMicroAtReferenceLoad))
                throw new ArgumentException(
                    "Each declared physical crossing needs exactly one " +
                    "positive, same-topology cargo reference scale.",
                    nameof(carryingScales));
        }
        if (capacities.Count != roads.Length ||
            roads.Any(edge => !capacities.ContainsKey(edge.Edge)))
            throw new ArgumentException(
                "Cargo scales must cover exactly the real traversable edges.",
                nameof(carryingScales));

        // Never trust previously returned prices, paths, markets, or loads
        // without rechecking the FULL physical worker-backed supply audit.
        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            initial.Parts.Select(p => p.Delivery), ownership);
        if (audit.TotalAllocatedGoodsMicro !=
                initial.AllocationAudit.TotalAllocatedGoodsMicro ||
            audit.TotalAvailableGoodsMicro !=
                initial.AllocationAudit.TotalAvailableGoodsMicro ||
            audit.DeliveryCount != initial.Parts.Count)
            throw new InvalidOperationException(
                "Input route parts disagree with authoritative physical workers.");
        var parentById = initial.MarketAllocation.Candidates
            .ToDictionary(p => p.DeliveryOrdinal);
        var parentTotals = new Dictionary<ulong, BigInteger>();
        foreach (var part in initial.Parts)
        {
            if (!parentById.TryGetValue(part.ParentDeliveryOrdinal,
                    out var parent) ||
                part.Delivery.Seller != parent.Seller ||
                part.Delivery.SourceOccupation != parent.SourceOccupation ||
                part.Delivery.SourceTile != parent.SourceTile ||
                part.Delivery.Buyer != parent.Buyer ||
                part.Delivery.BuyerTile != parent.BuyerTile ||
                part.Delivery.Commodity != parent.Commodity ||
                conversionPolicy.CargoMicro(part.Delivery.Commodity,
                    part.Delivery.GoodsMicro) != part.CargoMicro)
                throw new InvalidOperationException(
                    "A route part changed its original producer or buyer.");
            parentTotals[part.ParentDeliveryOrdinal] =
                parentTotals.GetValueOrDefault(part.ParentDeliveryOrdinal) +
                part.Delivery.GoodsMicro;
        }
        if (parentById.Any(pair =>
            parentTotals.GetValueOrDefault(pair.Key) !=
                pair.Value.GoodsMicro))
            throw new InvalidOperationException(
                "Route parts did not preserve every market sale exactly.");

        var fragments = new List<Fragment>();
        foreach (var part in initial.Parts.OrderBy(x => x.ParentDeliveryOrdinal)
                     .ThenBy(x => x.Delivery.DeliveryOrdinal))
        {
            ulong wholeCargo = conversionPolicy.CargoMicro(
                part.Delivery.Commodity, 1_000_000UL);
            ulong quantum = 1_000_000UL /
                FoundationPhysicalMarginalMarketDistributor.Gcd(
                    1_000_000UL, wholeCargo);
            ulong units = part.Delivery.GoodsMicro / quantum;
            if (units == 0UL || part.Delivery.GoodsMicro % quantum != 0UL)
                throw new InvalidOperationException(
                    "Physical part cannot be split at exact cargo precision.");
            int count = checked((int)Math.Min(units,
                (ulong)maxFragmentsPerPart));
            ulong baseUnits = units / (ulong)count;
            ulong extra = units % (ulong)count;
            for (int i = 0; i < count; i++)
            {
                ulong goods = checked((baseUnits +
                    ((ulong)i < extra ? 1UL : 0UL)) * quantum);
                fragments.Add(new Fragment(part, goods,
                    conversionPolicy.CargoMicro(
                        part.Delivery.Commodity, goods),
                    part.Delivery.RouteTiles.ToArray()));
            }
        }
        var loads = byEdge.Keys.ToDictionary(edge => edge, _ => 0UL);
        foreach (var fragment in fragments)
            foreach (var edge in Crossings(fragment.Route, directed))
                loads[edge.Edge] =
                    checked(loads[edge.Edge] + fragment.Cargo);
        var originalAuditLoads = audit.Freight.UsedEdges.ToDictionary(
            x => x.Edge, x => x.TotalCargoMicro);
        if (loads.Any(pair =>
            pair.Value != originalAuditLoads.GetValueOrDefault(pair.Key)))
            throw new InvalidOperationException(
                "Input path fragments disagree with independently audited loads.");

        BigInteger Objective()
        {
            BigInteger value = BigInteger.Zero;
            foreach (var road in roads)
                value += IntegratedPhysicalCost(
                    road.PhysicalCostMicroPerWholeCargoUnit,
                    loads[road.Edge], capacities[road.Edge]);
            foreach (var fragment in fragments)
                foreach (var edge in Crossings(fragment.Route, directed))
                    if (edge.InfrastructureOwner.HasValue &&
                        edge.InfrastructureOwner.Value !=
                            fragment.Origin.Delivery.Seller)
                        value += new BigInteger(edge.ExternalTariffRate) *
                            fragment.Cargo;
            return value;
        }

        var initialPotential = Objective();
        BigInteger lastPotential = initialPotential;
        int passes = 0;
        int changedTotal = 0;
        bool stable = false;
        int cells = incidence.FineTopology.Cells.Count;
        for (int pass = 1; pass <= maxImprovementPasses; pass++)
        {
            passes = pass;
            int changed = 0;
            foreach (var fragment in fragments)
            {
                if (fragment.Route.Length <= 1) continue;
                var existing = Crossings(fragment.Route, directed).ToArray();
                foreach (var edge in existing)
                    loads[edge.Edge] = checked(
                        loads[edge.Edge] - fragment.Cargo);

                var incrementalCache =
                    new Dictionary<PhysicalTacticalEdgeId, BigInteger>();
                BigInteger Cost(FoundationPhysicalTradeRouteCandidatePlanner.GraphEdge edge)
                {
                    if (!incrementalCache.TryGetValue(edge.Edge,
                            out var physical))
                    {
                        var free = byEdge[edge.Edge];
                        var current = loads[edge.Edge];
                        physical = IntegratedPhysicalCost(
                            free.PhysicalCostMicroPerWholeCargoUnit,
                            checked(current + fragment.Cargo),
                            capacities[edge.Edge]) -
                            IntegratedPhysicalCost(
                                free.PhysicalCostMicroPerWholeCargoUnit,
                                current, capacities[edge.Edge]);
                        incrementalCache.Add(edge.Edge, physical);
                    }
                    if (edge.InfrastructureOwner.HasValue &&
                        edge.InfrastructureOwner.Value !=
                            fragment.Origin.Delivery.Seller)
                        physical += new BigInteger(edge.ExternalTariffRate) *
                            fragment.Cargo;
                    return physical;
                }

                BigInteger existingCost = existing.Aggregate(
                    BigInteger.Zero, (sum, edge) => sum + Cost(edge));
                var cheapest = CheapestIncrementalRoute(
                    fragment, cells, incidence, graph, Cost);
                BigInteger cheapestCost = Crossings(cheapest, directed)
                    .Aggregate(BigInteger.Zero,
                        (sum, edge) => sum + Cost(edge));
                if (cheapestCost < existingCost)
                {
                    fragment.Route = cheapest;
                    changed = checked(changed + 1);
                }
                foreach (var edge in Crossings(fragment.Route, directed))
                    loads[edge.Edge] =
                        checked(loads[edge.Edge] + fragment.Cargo);
            }

            changedTotal = checked(changedTotal + changed);
            var currentPotential = Objective();
            if (changed > 0 && currentPotential >= lastPotential)
                throw new InvalidOperationException(
                    "A marginal reroute failed to strictly reduce the " +
                    "integrated physical and external-tariff objective.");
            lastPotential = currentPotential;
            if (changed == 0)
            {
                stable = true;
                break;
            }
        }
        if (!stable)
            throw new InvalidOperationException(
                "Marginal route improvement did not reach a full-pass " +
                "discrete local fixed point inside the declared budget.");

        var grouped = fragments
            .GroupBy(f => (
                f.Origin.ParentDeliveryOrdinal,
                Route: string.Join(",", f.Route.Select(
                    x => x.FineStrategicCellId.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)))))
            .OrderBy(group => group.Key.ParentDeliveryOrdinal)
            .ThenBy(group => group.Key.Route, StringComparer.Ordinal)
            .ToArray();
        var parts = new List<FoundationPhysicalMultiPathDeliveryPart>();
        foreach (var group in grouped)
        {
            var representative = group.First();
            ulong goods = group.Aggregate(0UL, (sum, f) =>
                checked(sum + f.Goods));
            var candidate = representative.Origin.Delivery with
            {
                DeliveryOrdinal = checked((ulong)parts.Count + 1UL),
                GoodsMicro = goods,
                RouteTiles = Array.AsReadOnly(representative.Route)
            };
            parts.Add(new FoundationPhysicalMultiPathDeliveryPart(
                representative.Origin.ParentDeliveryOrdinal, candidate,
                conversionPolicy.CargoMicro(candidate.Commodity, goods)));
        }
        var finalAudit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            parts.Select(p => p.Delivery), ownership);
        var finalUsed = finalAudit.Freight.UsedEdges.ToDictionary(
            x => x.Edge, x => x.TotalCargoMicro);
        if (finalAudit.TotalAllocatedGoodsMicro !=
                audit.TotalAllocatedGoodsMicro ||
            finalAudit.Freight.TotalDeliveredCargoMicro !=
                audit.Freight.TotalDeliveredCargoMicro ||
            loads.Any(pair =>
                pair.Value != finalUsed.GetValueOrDefault(pair.Key)))
            throw new InvalidOperationException(
                "Final marginal paths do not reconcile to the worker market " +
                "or independent physical crossing audit.");

        var readings = roads.Select(edge =>
            new FoundationPhysicalCongestedEdgeReading(
                edge.Edge, loads[edge.Edge],
                capacities[edge.Edge],
                edge.PhysicalCostMicroPerWholeCargoUnit,
                FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                    edge.PhysicalCostMicroPerWholeCargoUnit,
                    loads[edge.Edge], capacities[edge.Edge]),
                edge.OwnershipKind, edge.InfrastructureOwner,
                edge.ExternalTariffMicroPerWholeCargoUnit)).ToArray();
        var resulting = new FoundationPhysicalMultiPathCandidatePlan(
            initial.MarketAllocation, parts.ToArray(), finalAudit,
            readings, initial.RoutingBatches);
        return new FoundationPhysicalMarginalPathRebalancingPlan(
            resulting, initialPotential, lastPotential, passes,
            changedTotal, fragments.Count);
    }
}
