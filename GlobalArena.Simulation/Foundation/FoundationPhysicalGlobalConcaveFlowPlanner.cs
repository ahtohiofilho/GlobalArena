using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Read-only physical candidates from an OPT-IN globally coupled,
/// piecewise-concave min-cost flow. The flow optimum is for the explicitly
/// segmented surrogate, not a certificate of the continuous square-root
/// optimum, congestion fixed point or economically settled trades.
/// </summary>
public sealed class FoundationPhysicalGlobalConcaveFlowPlan
{
    public IReadOnlyList<FoundationPhysicalCommodityDeliveryCandidate> Candidates { get; }
    public IReadOnlyList<FoundationPhysicalMarketAllocationTotal> MarketTotals { get; }
    public FoundationPhysicalDeliveryAllocationAudit AllocationAudit { get; }
    public int ProductiveLotCount { get; }
    public int RealMarketCount { get; }
    public int FlowAugmentations { get; }
    public int SegmentsPerAverageMarket { get; }
    public BigInteger SegmentedNetPotentialScaled { get; }
    public BigInteger IntegratedNetPotentialScaled { get; }
    /// <summary>
    /// The nonnegative amount by which actual integrated utility on THIS
    /// allocation exceeds its piecewise chord objective; NOT a bound on
    /// the distance to the true continuous optimum.
    /// </summary>
    public BigInteger SegmentationUnderstatementScaled =>
        IntegratedNetPotentialScaled - SegmentedNetPotentialScaled;

    internal FoundationPhysicalGlobalConcaveFlowPlan(
        FoundationPhysicalCommodityDeliveryCandidate[] candidates,
        FoundationPhysicalMarketAllocationTotal[] markets,
        FoundationPhysicalDeliveryAllocationAudit audit,
        int lots, int count, int augmentations, int segments,
        BigInteger surrogate, BigInteger actual)
    {
        Candidates = Array.AsReadOnly(candidates);
        MarketTotals = Array.AsReadOnly(markets);
        AllocationAudit = audit;
        ProductiveLotCount = lots;
        RealMarketCount = count;
        FlowAugmentations = augmentations;
        SegmentsPerAverageMarket = segments;
        SegmentedNetPotentialScaled = surrogate;
        IntegratedNetPotentialScaled = actual;
    }
}

/// <summary>
/// Explicit research/validation path for a global seller-market flow using
/// real worker lots, real buyer demand, shortest owner-aware physical routes,
/// and no demand ceilings. Does NOT mutate the global coordinate planner,
/// its default path, the host snapshot, treasury, freight load feedback,
/// Godot overlays, or the last closed economic cycle.
/// </summary>
public static class FoundationPhysicalGlobalConcaveFlowPlanner
{
    private const ulong Micro = FoundationPhysicalMarginalMarketDistributor.Micro;
    private const int FractionalBits = 64;

    public static FoundationPhysicalGlobalConcaveFlowPlan Coordinate(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts,
        int segmentsPerAverageMarket = 16,
        int maxFlowAugmentations = 10000) =>
        CoordinateWithSearchWorkspace(census, incidence, demandPolicy,
            conversionPolicy, quotes, edgeCosts, segmentsPerAverageMarket,
            maxFlowAugmentations, reuseSearchWorkspace: true);

    // Research-only reference mode. The public API always takes the reused
    // workspace path; tests can compare the exact same real worker market
    // with the previous allocation-per-augmentation implementation.
    internal static FoundationPhysicalGlobalConcaveFlowPlan CoordinateWithSearchWorkspace(
        PhysicalWorkerRuntimeState census,
        PhysicalTacticalIncidenceMap incidence,
        FoundationActivityCommodityDemandPolicy demandPolicy,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts,
        int segmentsPerAverageMarket,
        int maxFlowAugmentations,
        bool reuseSearchWorkspace,
        bool useSequentialSeedReference = false)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(edgeCosts);
        if (segmentsPerAverageMarket < 1 || maxFlowAugmentations < 1)
            throw new ArgumentOutOfRangeException(
                "Positive segmentation and augmentation budgets are mandatory.");

        // The sequential marginal response used to be fully SOLVED and
        // audited here solely to retrieve worker-market inputs and quotes.
        // It never constrained this global solver. Reuse the exact complete
        // worker/quote preflight instead and keep the original slow seed
        // as an independently executable A/B oracle.
        var quoteList = quotes.ToArray();
        var costList = edgeCosts.ToArray();
        FoundationPhysicalCommodityMarketInputs inputs;
        Dictionary<(PhysicalTacticalTileId Tile, CivilizationId Owner,
            CommodityId Commodity), FoundationCommodityMarketPlanningQuote>
            quoteMap;
        if (useSequentialSeedReference)
        {
            var seed = FoundationPhysicalMarginalMarketDistributor.Distribute(
                census, incidence, demandPolicy, conversionPolicy,
                quoteList, costList);
            inputs = seed.AllocationAudit.MarketInputs;
            quoteMap = quoteList.ToDictionary(
                quote => (quote.BuyerTile, quote.Buyer, quote.Commodity));
        }
        else
        {
            (inputs, quoteMap) =
                FoundationPhysicalMarginalMarketDistributor
                    .ValidateMarketAndQuotes(
                        census, demandPolicy, conversionPolicy, quoteList);
        }
        var (graph, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, costList);
        int fineCount = incidence.FineTopology.Cells.Count;

        var routesBySource = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Seller),
            FoundationPhysicalTradeRouteCandidatePlanner.RouteTree>();
        FoundationPhysicalTradeRouteCandidatePlanner.RouteTree Routes(
            FoundationPhysicalSupplyLot lot)
        {
            var key = (lot.Tile, lot.Owner);
            if (routesBySource.TryGetValue(key, out var cached))
                return cached;
            if (lot.Tile.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                lot.Tile.FineStrategicCellId.Value == 0UL ||
                lot.Tile.FineStrategicCellId.Value > (ulong)fineCount)
                throw new InvalidOperationException(
                    "A producer does not belong to the real fine topology.");
            var tree = FoundationPhysicalTradeRouteCandidatePlanner.RouteFrom(
                lot.Tile.FineStrategicCellId.Value, lot.Owner, graph, fineCount);
            routesBySource.Add(key, tree);
            return tree;
        }

        var lots = inputs.SupplyLots.ToArray();
        var demand = inputs.DemandByCivilization.ToArray();
        var marketTotals = demand.ToDictionary(
            line => (line.Tile, line.Owner, line.Commodity), _ => 0UL);
        var candidates = new List<FoundationPhysicalCommodityDeliveryCandidate>();
        BigInteger piecewisePotential = BigInteger.Zero;
        BigInteger integratedPotential = BigInteger.Zero;
        int augmentations = 0;

        foreach (var commodity in lots.Select(lot => lot.Commodity).Distinct())
        {
            var commodityLots = lots.Where(lot =>
                lot.Commodity == commodity).ToArray();
            var commodityMarkets = demand.Where(line =>
                line.Commodity == commodity).ToArray();
            if (commodityMarkets.Length == 0)
                throw new InvalidOperationException(
                    "Real production cannot be sold without real worker demand.");

            ulong cargoPerWhole = conversionPolicy.CargoMicro(commodity, Micro);
            ulong quantum = Micro /
                FoundationPhysicalMarginalMarketDistributor.Gcd(
                    Micro, cargoPerWhole);
            var buyers = commodityMarkets.Select(line =>
                new FoundationPhysicalConcaveCommodityFlow.Market(
                    quoteMap[(line.Tile, line.Owner, commodity)]
                        .QuoteMicroPerWholeCommodityUnit,
                    line.DemandMicro)).ToArray();

            var trees = commodityLots.Select(Routes).ToArray();
            var sources = new List<FoundationPhysicalConcaveCommodityFlow.Seller>();
            for (int i = 0; i < commodityLots.Length; i++)
            {
                var lot = commodityLots[i];
                if (lot.CapacityMicroPerTurn % quantum != 0UL)
                    throw new InvalidOperationException(
                        "Real producer supply violates its cargo representability quantum.");
                var costs = new BigInteger?[commodityMarkets.Length];
                for (int j = 0; j < commodityMarkets.Length; j++)
                {
                    var line = commodityMarkets[j];
                    if (line.Tile.FineGoldbergParameters !=
                            incidence.Refinement.FineParameters ||
                        line.Tile.FineStrategicCellId.Value == 0UL ||
                        line.Tile.FineStrategicCellId.Value > (ulong)fineCount)
                        throw new InvalidOperationException(
                            "A buyer does not belong to the real fine topology.");
                    int destination = checked((int)line.Tile
                        .FineStrategicCellId.Value);
                    if (trees[i].TotalRate[destination] == ulong.MaxValue)
                        continue;
                    // EXACTLY the existing integrated objective's units:
                    // cost per goods-micro * goods quantum * 2^64.
                    costs[j] = (new BigInteger(cargoPerWhole) *
                        (new BigInteger(trees[i].PhysicalRate[destination]) +
                         trees[i].TariffRate[destination]) * quantum)
                        << FractionalBits;
                }
                sources.Add(new FoundationPhysicalConcaveCommodityFlow.Seller(
                    lot.CapacityMicroPerTurn / quantum, costs));
            }

            int remaining = maxFlowAugmentations - augmentations;
            if (remaining < 1)
                throw new InvalidOperationException(
                    "Global concave-flow planning exhausted its total budget.");
            var flow = FoundationPhysicalConcaveCommodityFlow.Solve(
                sources, buyers, quantum, segmentsPerAverageMarket, remaining,
                reuseSearchWorkspace);
            augmentations = checked(augmentations + flow.Augmentations);
            piecewisePotential += flow.SegmentedNetPotentialScaled;
            integratedPotential += flow.IntegratedNetPotentialScaled;

            for (int j = 0; j < commodityMarkets.Length; j++)
            {
                var line = commodityMarkets[j];
                marketTotals[(line.Tile, line.Owner, commodity)] =
                    checked(flow.MarketQuanta[j] * quantum);
            }
            for (int i = 0; i < commodityLots.Length; i++)
            {
                var lot = commodityLots[i];
                for (int j = 0; j < commodityMarkets.Length; j++)
                {
                    ulong quantity = flow.AllocationQuanta[i][j];
                    if (quantity == 0UL) continue;
                    var buyer = commodityMarkets[j];
                    var route = FoundationPhysicalTradeRouteCandidatePlanner
                        .Reconstruct(lot.Tile, buyer.Tile, trees[i]);
                    candidates.Add(new FoundationPhysicalCommodityDeliveryCandidate(
                        checked((ulong)candidates.Count + 1UL),
                        lot.Owner, lot.Occupation, lot.Tile,
                        buyer.Owner, buyer.Tile, commodity,
                        checked(quantity * quantum), Array.AsReadOnly(route)));
                }
            }
        }

        // All shipment edges, cargo and goods per producer and all ownership
        // are checked again by the authoritative physical candidate auditor.
        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            candidates, ownership);
        var totals = demand.Select(line =>
            new FoundationPhysicalMarketAllocationTotal(
                line.Tile, line.Owner, line.Commodity, line.DemandMicro,
                marketTotals[(line.Tile, line.Owner, line.Commodity)])).ToArray();
        if (totals.Aggregate(BigInteger.Zero,
                (sum, line) => sum + line.AllocatedGoodsMicro) !=
            audit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException(
                "Global concave-flow buyer totals do not reconcile to the physical auditor.");
        return new FoundationPhysicalGlobalConcaveFlowPlan(
            candidates.ToArray(), totals, audit,
            lots.Length, demand.Length, augmentations,
            segmentsPerAverageMarket, piecewisePotential, integratedPotential);
    }
}
