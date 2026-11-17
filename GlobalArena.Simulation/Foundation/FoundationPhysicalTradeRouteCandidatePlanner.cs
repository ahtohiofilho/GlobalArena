using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Caller-supplied *planning* unit valuation in micro-money per one WHOLE
/// commodity unit. Not a cleared market price, buyer wallet or transaction.
/// </summary>
public sealed record FoundationCommodityMarketPlanningQuote(
    PhysicalTacticalTileId BuyerTile, CivilizationId Buyer,
    CommodityId Commodity, ulong QuoteMicroPerWholeCommodityUnit);

/// <summary>
/// An explicit traversable fine edge and its per-whole-cargo-unit monetary
/// physical freight and external-owner tariff rates. No prices, roads,
/// capacities or policies are inferred for missing edges.
/// </summary>
public sealed record FoundationPhysicalRoutingEdgeCost(
    PhysicalTacticalEdgeId Edge,
    FoundationInfrastructureOwnershipKind OwnershipKind,
    CivilizationId? InfrastructureOwner,
    ulong PhysicalCostMicroPerWholeCargoUnit,
    ulong ExternalTariffMicroPerWholeCargoUnit);

/// <summary>
/// Fully traceable, uncommitted, *whole-lot* economic planning choice.
/// The quoted value is a comparison signal, NOT a price or realized revenue.
/// Freight costs are inputs to route choice, NOT treasury events.
/// </summary>
public sealed record FoundationPhysicalDeliveryPlanningChoice(
    FoundationPhysicalCommodityDeliveryCandidate Candidate,
    ulong QuotedValueMicro,
    ulong PhysicalFreightCostMicro,
    ulong ExternalTariffCostMicro,
    Int128 NetPlanningScoreMicro);

/// <summary>
/// Reconciled candidates plus every part of their scoring evidence. Calling
/// this does NOT publish a closed cycle, make sales or charge any tariff.
/// </summary>
public sealed class FoundationPhysicalTradeRouteCandidatePlan
{
    public IReadOnlyList<FoundationPhysicalDeliveryPlanningChoice> Choices { get; }
    public FoundationPhysicalDeliveryAllocationAudit AllocationAudit { get; }
    public ulong SumQuotedValueMicro { get; }
    public ulong SumPhysicalFreightCostMicro { get; }
    public ulong SumExternalTariffCostMicro { get; }

    internal FoundationPhysicalTradeRouteCandidatePlan(
        FoundationPhysicalDeliveryPlanningChoice[] choices,
        FoundationPhysicalDeliveryAllocationAudit audit,
        ulong quoted, ulong physical, ulong tariff)
    {
        Choices = Array.AsReadOnly(choices);
        AllocationAudit = audit;
        SumQuotedValueMicro = quoted;
        SumPhysicalFreightCostMicro = physical;
        SumExternalTariffCostMicro = tariff;
    }
}

/// <summary>
/// First deterministic destination+route candidate selector on the real fine
/// topology. It chooses one buyer per productive lot using EXTERNAL quotes
/// and freight/tariff rates, maximizing full-lot nominal value minus shortest
/// seller-specific path cost. This is NOT market clearing, marginal pricing,
/// congestion equilibrium or water-filling; all of those remain future gates.
/// </summary>
public static class FoundationPhysicalTradeRouteCandidatePlanner
{
    private sealed record GraphEdge(
        ulong Other, PhysicalTacticalEdgeId Edge, ulong PhysicalRate,
        ulong ExternalTariffRate, CivilizationId? InfrastructureOwner);

    private sealed class RouteTree
    {
        internal readonly ulong[] TotalRate;
        internal readonly ulong[] PhysicalRate;
        internal readonly ulong[] TariffRate;
        internal readonly int[] Hops;
        internal readonly ulong[] Previous;

        internal RouteTree(int cellCount)
        {
            TotalRate = Enumerable.Repeat(ulong.MaxValue, cellCount + 1).ToArray();
            PhysicalRate = new ulong[cellCount + 1];
            TariffRate = new ulong[cellCount + 1];
            Hops = Enumerable.Repeat(int.MaxValue, cellCount + 1).ToArray();
            Previous = new ulong[cellCount + 1];
        }
    }

    private static ulong ExactScaledMicro(ulong unitsMicro, ulong rateMicro)
    {
        var product = (UInt128)unitsMicro * rateMicro;
        if (product % 1_000_000U != 0U)
            throw new InvalidOperationException(
                "Planning money is below a representable micro-unit. " +
                "No hidden rounding or price floor is applied.");
        var scaled = product / 1_000_000U;
        if (scaled > ulong.MaxValue)
            throw new OverflowException("Planning value or cost exceeds ulong micro.");
        return (ulong)scaled;
    }

    private static RouteTree RouteFrom(
        ulong from, CivilizationId seller,
        IReadOnlyDictionary<ulong, List<GraphEdge>> graph, int count)
    {
        var tree = new RouteTree(count);
        tree.TotalRate[checked((int)from)] = 0UL;
        tree.Hops[checked((int)from)] = 0;
        var queue = new PriorityQueue<ulong, (ulong Cost, int Hops, ulong Node)>();
        queue.Enqueue(from, (0UL, 0, from));
        while (queue.TryDequeue(out var at, out var priority))
        {
            var current = checked((int)at);
            if (tree.TotalRate[current] != priority.Cost ||
                tree.Hops[current] != priority.Hops)
                continue;
            if (!graph.TryGetValue(at, out var links))
                continue;
            foreach (var link in links)
            {
                var next = checked((int)link.Other);
                // The owner is exempt only from that edge's EXTERNAL toll.
                // Physical cost remains for everybody.
                var toll = link.InfrastructureOwner.HasValue &&
                    link.InfrastructureOwner.Value != seller
                    ? link.ExternalTariffRate : 0UL;
                ulong rate = checked(link.PhysicalRate + toll);
                ulong proposed = checked(tree.TotalRate[current] + rate);
                int hops = checked(tree.Hops[current] + 1);
                bool better = proposed < tree.TotalRate[next] ||
                    proposed == tree.TotalRate[next] &&
                    (hops < tree.Hops[next] ||
                     hops == tree.Hops[next] &&
                     (tree.Previous[next] == 0UL ||
                      at < tree.Previous[next]));
                if (!better)
                    continue;
                bool needsQueue = proposed != tree.TotalRate[next] ||
                    hops != tree.Hops[next];
                tree.TotalRate[next] = proposed;
                tree.PhysicalRate[next] = checked(
                    tree.PhysicalRate[current] + link.PhysicalRate);
                tree.TariffRate[next] = checked(
                    tree.TariffRate[current] + toll);
                tree.Hops[next] = hops;
                tree.Previous[next] = at;
                if (needsQueue)
                    queue.Enqueue(link.Other, (proposed, hops, link.Other));
            }
        }
        return tree;
    }

    private static PhysicalTacticalTileId[] Reconstruct(
        PhysicalTacticalTileId source, PhysicalTacticalTileId destination,
        RouteTree tree)
    {
        var backward = new List<PhysicalTacticalTileId>();
        var at = destination.FineStrategicCellId.Value;
        for (var hops = 0; hops <= tree.Previous.Length; hops++)
        {
            backward.Add(new PhysicalTacticalTileId(
                source.FineGoldbergParameters, new StrategicCellId(at)));
            if (at == source.FineStrategicCellId.Value)
            {
                backward.Reverse();
                return backward.ToArray();
            }
            at = tree.Previous[checked((int)at)];
            if (at == 0UL) break;
        }
        throw new InvalidOperationException(
            "Routing tree contains a missing or cyclic physical predecessor.");
    }

    public static FoundationPhysicalTradeRouteCandidatePlan Plan(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(edgeCosts);

        var market = FoundationPhysicalCommodityDemandProjector.Project(
            census, demandPolicy);
        if (!market.Commodities.SequenceEqual(conversionPolicy.Commodities))
            throw new ArgumentException(
                "Planning requires identical demand and cargo commodity catalogues.",
                nameof(conversionPolicy));

        var actualMarkets = market.DemandByCivilization.ToDictionary(
            x => (x.Tile, x.Owner, x.Commodity));
        var quoteByKey = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Owner,
             CommodityId Commodity), FoundationCommodityMarketPlanningQuote>();
        foreach (var quote in quotes)
        {
            if (quote is null || quote.QuoteMicroPerWholeCommodityUnit == 0UL ||
                !quote.BuyerTile.IsValid || !quote.Buyer.IsValid ||
                !quote.Commodity.IsValid)
                throw new ArgumentException("Planning quotes must be positive " +
                    "and identify a real fine-tile market.", nameof(quotes));
            var key = (quote.BuyerTile, quote.Buyer, quote.Commodity);
            if (!actualMarkets.ContainsKey(key) || !quoteByKey.TryAdd(key, quote))
                throw new ArgumentException(
                    "Duplicate quote or quote without a real physical buyer.",
                    nameof(quotes));
        }
        if (quoteByKey.Count != actualMarkets.Count)
            throw new ArgumentException(
                "Every positive-demand market needs its own explicit quote, " +
                "with no default market value.", nameof(quotes));

        var fine = incidence.Refinement.FineParameters;
        int count = incidence.FineTopology.Cells.Count;
        var graph = new Dictionary<ulong, List<GraphEdge>>();
        var canonicalEdges = incidence.FineTopology.Edges.ToDictionary(
            e => e.Id, e => e);
        var ownership = new List<FoundationTransportEdgeOwnershipEvidence>();
        var existing = new HashSet<PhysicalTacticalEdgeId>();
        foreach (var input in edgeCosts)
        {
            if (input is null || !input.Edge.IsValid ||
                input.Edge.FineGoldbergParameters != fine ||
                !canonicalEdges.TryGetValue(
                    input.Edge.FineStrategicEdgeId, out var geometric) ||
                !Enum.IsDefined(input.OwnershipKind) ||
                input.PhysicalCostMicroPerWholeCargoUnit == 0UL ||
                (input.OwnershipKind == FoundationInfrastructureOwnershipKind.Owned
                    ? input.InfrastructureOwner is not { IsValid: true }
                    : input.InfrastructureOwner.HasValue ||
                      input.ExternalTariffMicroPerWholeCargoUnit != 0UL) ||
                !existing.Add(input.Edge))
                throw new ArgumentException(
                    "Each traversable edge needs a unique real physical identity, " +
                    "positive physical cost and valid ownership/toll evidence.",
                    nameof(edgeCosts));
            // Never silently treat a missing cost/ownership entry as a free
            // natural road. Only explicitly supplied edges are traversable.
            ownership.Add(new FoundationTransportEdgeOwnershipEvidence(
                input.Edge, input.OwnershipKind, input.InfrastructureOwner));
            var a = geometric.IncidentCellIds[0].Value;
            var b = geometric.IncidentCellIds[1].Value;
            if (!graph.TryGetValue(a, out var fromA))
                graph.Add(a, fromA = new List<GraphEdge>());
            if (!graph.TryGetValue(b, out var fromB))
                graph.Add(b, fromB = new List<GraphEdge>());
            fromA.Add(new GraphEdge(
                b, input.Edge, input.PhysicalCostMicroPerWholeCargoUnit,
                input.ExternalTariffMicroPerWholeCargoUnit,
                input.InfrastructureOwner));
            fromB.Add(new GraphEdge(
                a, input.Edge, input.PhysicalCostMicroPerWholeCargoUnit,
                input.ExternalTariffMicroPerWholeCargoUnit,
                input.InfrastructureOwner));
        }
        foreach (var edges in graph.Values)
            edges.Sort((a, b) =>
            {
                int byTile = a.Other.CompareTo(b.Other);
                return byTile != 0 ? byTile :
                    a.Edge.FineStrategicEdgeId.Value.CompareTo(
                        b.Edge.FineStrategicEdgeId.Value);
            });

        var quotesByCommodity = quoteByKey.Values
            .GroupBy(x => x.Commodity)
            .ToDictionary(x => x.Key, x => x
                .OrderBy(q => q.BuyerTile.FineStrategicCellId.Value)
                .ThenBy(q => q.Buyer.Value).ToArray());
        var cache = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Seller), RouteTree>();
        var chosen = new List<FoundationPhysicalDeliveryPlanningChoice>();
        ulong quotedTotal = 0UL, physicalTotal = 0UL, tariffTotal = 0UL;
        foreach (var lot in market.SupplyLots)
        {
            if (lot.Tile.FineGoldbergParameters != fine ||
                lot.Tile.FineStrategicCellId.Value > (ulong)count)
                throw new InvalidOperationException(
                    "Productive lot does not belong to the supplied real fine topology.");
            var sourceKey = (lot.Tile, lot.Owner);
            if (!cache.TryGetValue(sourceKey, out var routing))
            {
                routing = RouteFrom(lot.Tile.FineStrategicCellId.Value,
                    lot.Owner, graph, count);
                cache.Add(sourceKey, routing);
            }

            // Compare full-lot quoted nominal score; no implicit price,
            // fixed demand cap, buyer wallet, or production left unsold.
            var cargo = conversionPolicy.CargoMicro(
                lot.Commodity, lot.CapacityMicroPerTurn);
            FoundationCommodityMarketPlanningQuote? winner = null;
            Int128 bestScore = Int128.MinValue;
            ulong bestQuoted = 0UL, bestPhysical = 0UL, bestTariff = 0UL;
            foreach (var quote in quotesByCommodity[lot.Commodity])
            {
                var index = checked((int)quote.BuyerTile.FineStrategicCellId.Value);
                if (quote.BuyerTile.FineGoldbergParameters != fine ||
                    index == 0 || index > count)
                    throw new InvalidOperationException(
                        "Buyer market is not on the source's fine topology.");
                if (routing.TotalRate[index] == ulong.MaxValue)
                    continue; // Missing explicit edges = impassable.
                var quoted = ExactScaledMicro(lot.CapacityMicroPerTurn,
                    quote.QuoteMicroPerWholeCommodityUnit);
                var physical = ExactScaledMicro(cargo, routing.PhysicalRate[index]);
                var tariff = ExactScaledMicro(cargo, routing.TariffRate[index]);
                var score = (Int128)quoted - physical - tariff;
                if (winner is not null && score <= bestScore)
                    continue; // Canonical buyer order resolves exact ties.
                winner = quote;
                bestScore = score;
                bestQuoted = quoted;
                bestPhysical = physical;
                bestTariff = tariff;
            }
            if (winner is null)
                throw new InvalidOperationException(
                    "No buyer with positive real demand is physically " +
                    "reachable under the supplied explicit edge policy.");
            ulong ordinal = checked((ulong)chosen.Count + 1UL);
            var route = Reconstruct(lot.Tile, winner.BuyerTile, routing);
            var candidate = new FoundationPhysicalCommodityDeliveryCandidate(
                ordinal, lot.Owner, lot.Occupation, lot.Tile,
                winner.Buyer, winner.BuyerTile, lot.Commodity,
                lot.CapacityMicroPerTurn, Array.AsReadOnly(route));
            chosen.Add(new FoundationPhysicalDeliveryPlanningChoice(
                candidate, bestQuoted, bestPhysical, bestTariff, bestScore));
            quotedTotal = checked(quotedTotal + bestQuoted);
            physicalTotal = checked(physicalTotal + bestPhysical);
            tariffTotal = checked(tariffTotal + bestTariff);
        }

        // Reuse the Gate 01B.2A final structural auditor; a planner cannot
        // bypass worker conservation or manufacture freight edge loads.
        var audited = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            chosen.Select(x => x.Candidate), ownership);
        return new FoundationPhysicalTradeRouteCandidatePlan(
            chosen.ToArray(), audited, quotedTotal, physicalTotal, tariffTotal);
    }
}
