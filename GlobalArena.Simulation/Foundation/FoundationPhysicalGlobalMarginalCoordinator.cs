using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Diagnostics for a globally coupled, exact-quantity producer rebalancing
/// FIXED POINT. It is NOT a proof of global optimality or economic clearing.
/// </summary>
public sealed class FoundationPhysicalGlobalMarginalCoordinationPlan
{
    public IReadOnlyList<FoundationPhysicalCommodityDeliveryCandidate> Candidates { get; }
    public IReadOnlyList<FoundationPhysicalMarketAllocationTotal> InitialMarketTotals { get; }
    public IReadOnlyList<FoundationPhysicalMarketAllocationTotal> MarketTotals { get; }
    public FoundationPhysicalDeliveryAllocationAudit AllocationAudit { get; }
    public int ProductiveLotCount { get; }
    public int Passes { get; }
    public ulong TotalReallocatedGoodsMicro { get; }
    public bool StableUnderOneFullCanonicalPass { get; }

    internal FoundationPhysicalGlobalMarginalCoordinationPlan(
        FoundationPhysicalCommodityDeliveryCandidate[] candidates,
        FoundationPhysicalMarketAllocationTotal[] initial,
        FoundationPhysicalMarketAllocationTotal[] final,
        FoundationPhysicalDeliveryAllocationAudit audit,
        int lotCount, int passes, ulong totalMoved)
    {
        Candidates = Array.AsReadOnly(candidates);
        InitialMarketTotals = Array.AsReadOnly(initial);
        MarketTotals = Array.AsReadOnly(final);
        AllocationAudit = audit;
        ProductiveLotCount = lotCount;
        Passes = passes;
        TotalReallocatedGoodsMicro = totalMoved;
        StableUnderOneFullCanonicalPass = true;
    }
}

/// <summary>
/// Cyclic coordinate rebalancing of ALL real productive lots against shared
/// physical market deliveries. Each producer can revise a previous choice
/// after other producers have delivered, using marginal P0*sqrt(D/Q) and its
/// OWN shortest owner-aware physical freight path. A full pass with zero
/// changed quanta is a deterministic fixed point of this discrete best-response
/// rule. This is not a proof of simultaneous global equilibrium; capacity,
/// volume-dependent congestion, endogenous prices and financial settlement
/// remain future gates.
/// </summary>
public static class FoundationPhysicalGlobalMarginalCoordinator
{
    private const ulong Micro = FoundationPhysicalMarginalMarketDistributor.Micro;
    private static readonly BigInteger QuoteScale =
        FoundationPhysicalMarginalMarketDistributor.ScoreScale;

    private sealed class SourceLot
    {
        internal FoundationPhysicalSupplyLot Lot { get; }
        internal FoundationPhysicalMarginalMarketDistributor.Buyer[] Buyers { get; }
        internal ulong[] GoodsQuanta { get; }
        internal ulong CargoQuantum { get; }

        internal SourceLot(FoundationPhysicalSupplyLot lot,
            FoundationPhysicalMarginalMarketDistributor.Buyer[] buyers,
            ulong[] quanta, ulong cargoQuantum)
        {
            Lot = lot;
            Buyers = buyers;
            GoodsQuanta = quanta;
            CargoQuantum = cargoQuantum;
        }
    }

    public static FoundationPhysicalGlobalMarginalCoordinationPlan Coordinate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts,
        int maxPasses)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(edgeCosts);
        if (maxPasses < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPasses),
                "A positive, explicitly budgeted fixed-point pass count is required.");

        // The existing 01B.2C-1 planner seeds only real worker-backed,
        // globally reconciled deliveries, so no phantom initial Q or route.
        var quoteList = quotes.ToArray();
        var costList = edgeCosts.ToArray();
        var initial = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, incidence, demandPolicy, conversionPolicy,
            quoteList, costList);
        var inputs = initial.AllocationAudit.MarketInputs;
        var marketKeys = inputs.DemandByCivilization
            .Select(line => (line.Tile, line.Owner, line.Commodity))
            .ToArray();
        var global = initial.MarketTotals.ToDictionary(
            line => (line.Tile, line.Buyer, line.Commodity),
            line => line.AllocatedGoodsMicro);
        var starting = initial.MarketTotals.ToArray();
        var quoteMap = quoteList.ToDictionary(
            quote => (quote.BuyerTile, quote.Buyer, quote.Commodity));
        var (graph, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, costList);

        var previousBySource = initial.Candidates
            .GroupBy(x => (x.SourceTile, x.Seller, x.SourceOccupation,
                x.Commodity))
            .ToDictionary(group => group.Key,
                group => group.ToDictionary(
                    candidate => (candidate.BuyerTile, candidate.Buyer),
                    candidate => candidate.GoodsMicro));

        var sources = new List<SourceLot>(inputs.SupplyLots.Count);
        var routingCache = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Owner),
            FoundationPhysicalTradeRouteCandidatePlanner.RouteTree>();
        int fineCount = incidence.FineTopology.Cells.Count;
        foreach (var lot in inputs.SupplyLots)
        {
            if (lot.Tile.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                lot.Tile.FineStrategicCellId.Value == 0UL ||
                lot.Tile.FineStrategicCellId.Value > (ulong)fineCount)
                throw new InvalidOperationException(
                    "Supply lot comes from a foreign or invalid fine tile.");
            var routeKey = (lot.Tile, lot.Owner);
            if (!routingCache.TryGetValue(routeKey, out var routes))
            {
                routes = FoundationPhysicalTradeRouteCandidatePlanner.RouteFrom(
                    lot.Tile.FineStrategicCellId.Value, lot.Owner,
                    graph, fineCount);
                routingCache.Add(routeKey, routes);
            }

            ulong cargoPerWhole = conversionPolicy.CargoMicro(lot.Commodity, Micro);
            ulong quantum = Micro /
                FoundationPhysicalMarginalMarketDistributor.Gcd(
                    Micro, cargoPerWhole);
            var key = (lot.Tile, lot.Owner, lot.Occupation, lot.Commodity);
            if (!previousBySource.TryGetValue(key, out var previous))
                throw new InvalidOperationException(
                    "The initial progressive plan omitted a real source lot.");
            var eligible = new List<FoundationPhysicalMarginalMarketDistributor.Buyer>();
            var quanta = new List<ulong>();
            foreach (var line in inputs.DemandByCivilization)
            {
                if (line.Commodity != lot.Commodity) continue;
                var target = checked((int)line.Tile.FineStrategicCellId.Value);
                if (target < 1 || target > fineCount ||
                    line.Tile.FineGoldbergParameters !=
                        incidence.Refinement.FineParameters)
                    throw new InvalidOperationException(
                        "A buyer belongs to an incompatible fine topology.");
                if (routes.TotalRate[target] == ulong.MaxValue) continue;
                var quote = quoteMap[(line.Tile, line.Owner, line.Commodity)];
                var costScaled = new BigInteger(cargoPerWhole) *
                    (new BigInteger(routes.PhysicalRate[target]) +
                     routes.TariffRate[target]);
                eligible.Add(new FoundationPhysicalMarginalMarketDistributor.Buyer(
                    line, quote, routes,
                    new BigInteger(quote.QuoteMicroPerWholeCommodityUnit) *
                        QuoteScale,
                    costScaled, 0UL, quantum));
                ulong already = previous.GetValueOrDefault(
                    (line.Tile, line.Owner));
                if (already % quantum != 0UL)
                    throw new InvalidOperationException(
                        "Initial source-market delivery violates cargo granularity.");
                quanta.Add(already / quantum);
            }
            if (quanta.Aggregate(BigInteger.Zero,
                    (sum, n) => sum + n) * quantum != lot.CapacityMicroPerTurn)
                throw new InvalidOperationException(
                    "Initial physical lot allocation is not conserved.");
            sources.Add(new SourceLot(lot, eligible.ToArray(),
                quanta.ToArray(), quantum));
        }

        int passes = 0;
        ulong movedGoods = 0UL;
        bool fixedPoint = false;
        for (int pass = 0; pass < maxPasses; pass++)
        {
            passes = checked(pass + 1);
            ulong passMoved = 0UL;
            foreach (var source in sources)
            {
                var opponents = new FoundationPhysicalMarginalMarketDistributor
                    .Buyer[source.Buyers.Length];
                for (int i = 0; i < opponents.Length; i++)
                {
                    var buyer = source.Buyers[i];
                    var key = (buyer.Demand.Tile, buyer.Demand.Owner,
                        buyer.Demand.Commodity);
                    var own = checked(
                        source.GoodsQuanta[i] * source.CargoQuantum);
                    var total = global[key];
                    if (own > total)
                        throw new InvalidOperationException(
                            "Source allocation exceeds shared market delivery.");
                    opponents[i] = buyer with
                    {
                        PreviousGoodsMicro = total - own
                    };
                }

                var proposed =
                    FoundationPhysicalMarginalMarketDistributor.DistributeOneLot(
                        opponents, source.Lot.CapacityMicroPerTurn);
                for (int i = 0; i < proposed.Length; i++)
                {
                    var oldQuanta = source.GoodsQuanta[i];
                    var newQuanta = proposed[i];
                    if (oldQuanta == newQuanta) continue;
                    var buyer = source.Buyers[i];
                    var key = (buyer.Demand.Tile, buyer.Demand.Owner,
                        buyer.Demand.Commodity);
                    var oldGoods = checked(oldQuanta * source.CargoQuantum);
                    var newGoods = checked(newQuanta * source.CargoQuantum);
                    global[key] = checked(global[key] - oldGoods + newGoods);
                    if (oldQuanta > newQuanta)
                        passMoved = checked(passMoved + checked(
                            (oldQuanta - newQuanta) * source.CargoQuantum));
                    source.GoodsQuanta[i] = newQuanta;
                }
            }
            movedGoods = checked(movedGoods + passMoved);
            if (passMoved == 0UL)
            {
                fixedPoint = true;
                break;
            }
        }
        if (!fixedPoint)
            throw new InvalidOperationException(
                "The global marginal candidate planner has not reached a " +
                "stable full-pass fixed point inside its explicit pass budget. " +
                "Do not publish unstable candidate deliveries as a settled market.");

        var finalCandidates = new List<FoundationPhysicalCommodityDeliveryCandidate>();
        foreach (var source in sources)
        {
            for (int i = 0; i < source.Buyers.Length; i++)
            {
                ulong quanta = source.GoodsQuanta[i];
                if (quanta == 0UL) continue;
                var buyer = source.Buyers[i];
                var route = FoundationPhysicalTradeRouteCandidatePlanner.Reconstruct(
                    source.Lot.Tile, buyer.Demand.Tile, buyer.Route);
                finalCandidates.Add(new FoundationPhysicalCommodityDeliveryCandidate(
                    checked((ulong)finalCandidates.Count + 1UL),
                    source.Lot.Owner, source.Lot.Occupation, source.Lot.Tile,
                    buyer.Demand.Owner, buyer.Demand.Tile,
                    source.Lot.Commodity,
                    checked(quanta * source.CargoQuantum),
                    Array.AsReadOnly(route)));
            }
        }

        // Re-verify every seller, buyer and physical edge on the FINAL pass.
        // Candidate planning never issues a settlement receipt or ledger entry.
        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            finalCandidates, ownership);
        var final = inputs.DemandByCivilization.Select(line =>
            new FoundationPhysicalMarketAllocationTotal(
                line.Tile, line.Owner, line.Commodity, line.DemandMicro,
                global[(line.Tile, line.Owner, line.Commodity)])).ToArray();
        if (final.Aggregate(BigInteger.Zero,
                (sum, line) => sum + line.AllocatedGoodsMicro) !=
            audit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException(
                "Globally adjusted market totals do not reconcile to physical sales.");
        if (marketKeys.Length != final.Length)
            throw new InvalidOperationException(
                "A real buyer disappeared during global marginal coordination.");

        return new FoundationPhysicalGlobalMarginalCoordinationPlan(
            finalCandidates.ToArray(), starting, final,
            audit, sources.Count, passes, movedGoods);
    }
}
