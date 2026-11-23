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
    public bool PreferIncumbentThresholdTies { get; }
    /// <summary>
    /// Physical goods micro-units moved away from old buyer quotas by pass,
    /// including a terminal zero on successful convergence. Read-only
    /// diagnostics; never an economic trade receipt or settled delivery.
    /// </summary>
    public IReadOnlyList<ulong> MovedGoodsByPass { get; }
    public bool StrictIntegratedPotentialAcceptance { get; }
    public int RejectedNonImprovingSellerProposals { get; }
    /// <summary>
    /// Positive accepted increases of the fixed-point integer integrated
    /// utility comparison objective. NOT micro-money collected or a receipt.
    /// </summary>
    public BigInteger AcceptedPotentialGainScaled { get; }

    internal FoundationPhysicalGlobalMarginalCoordinationPlan(
        FoundationPhysicalCommodityDeliveryCandidate[] candidates,
        FoundationPhysicalMarketAllocationTotal[] initial,
        FoundationPhysicalMarketAllocationTotal[] final,
        FoundationPhysicalDeliveryAllocationAudit audit,
        int lotCount, int passes, ulong totalMoved,
        bool preferIncumbentTies, ulong[] movementByPass,
        bool strictPotentialAcceptance, int rejectedProposals,
        BigInteger acceptedGainScaled)
    {
        Candidates = Array.AsReadOnly(candidates);
        InitialMarketTotals = Array.AsReadOnly(initial);
        MarketTotals = Array.AsReadOnly(final);
        AllocationAudit = audit;
        ProductiveLotCount = lotCount;
        Passes = passes;
        TotalReallocatedGoodsMicro = totalMoved;
        StableUnderOneFullCanonicalPass = true;
        PreferIncumbentThresholdTies = preferIncumbentTies;
        MovedGoodsByPass = Array.AsReadOnly(movementByPass);
        StrictIntegratedPotentialAcceptance = strictPotentialAcceptance;
        RejectedNonImprovingSellerProposals = rejectedProposals;
        AcceptedPotentialGainScaled = acceptedGainScaled;
    }
}

/// <summary>
/// An explicit, inspectable failure to certify the opted-in global-seller
/// candidate policy within the declared full-pass budget. A partial
/// allocation is deliberately NOT returned, audited as a settled trade,
/// published to the last economic cycle or exposed to the Godot client.
/// </summary>
public sealed class FoundationPhysicalGlobalMarginalBudgetExceededException
    : InvalidOperationException
{
    public int PassBudget { get; }
    public IReadOnlyList<ulong> LastPassMovesMicro { get; }
    public int RejectedNonImprovingProposals { get; }
    public BigInteger AcceptedPotentialGainScaled { get; }

    internal FoundationPhysicalGlobalMarginalBudgetExceededException(
        int budget, ulong[] recentMoves, int rejected, BigInteger gain)
        : base("The opted-in same-commodity seller policy did NOT stabilize " +
            "within the explicit pass budget. Last pass movements (goods " +
            "micro-units): " + string.Join(",", recentMoves) +
            ". No economic settlement or candidate trade was published.")
    {
        PassBudget = budget;
        LastPassMovesMicro = Array.AsReadOnly(recentMoves);
        RejectedNonImprovingProposals = rejected;
        AcceptedPotentialGainScaled = gain;
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
/// <summary>
/// Diagnostic-only physical-market observation. NEVER a settled ledger,
/// never a new price rule, and absent from all production calls.
/// </summary>
internal sealed record FoundationMarginalPassObservation(
    int PassIndex,
    IReadOnlyDictionary<(PhysicalTacticalTileId Tile, CommodityId Commodity), ulong>
        MarketGoodsMicro,
    BigInteger PhysicalFreightScaled,
    ulong MovedGoodsMicro);

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
        int maxPasses,
        bool preferIncumbentThresholdTies = false,
        bool acceptOnlyStrictIntegratedGain = false)
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

        var market = FoundationPhysicalCommodityDemandProjector.Project(
            census, demandPolicy);
        return Coordinate(market, incidence, conversionPolicy, quotes,
            edgeCosts, maxPasses, preferIncumbentThresholdTies,
            acceptOnlyStrictIntegratedGain);
    }

    /// <summary>Same canonical global response solver with native temporal
    /// market supply/demand, not simulated partial workers.</summary>
    public static FoundationPhysicalGlobalMarginalCoordinationPlan Coordinate(
        FoundationPhysicalCommodityMarketInputs market,
        PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts,
        int maxPasses,
        bool preferIncumbentThresholdTies = false,
        bool acceptOnlyStrictIntegratedGain = false) =>
        CoordinateAudited(market, incidence, conversionPolicy, quotes,
            edgeCosts, maxPasses, preferIncumbentThresholdTies,
            acceptOnlyStrictIntegratedGain, null);

    /// <summary>
    /// R175 diagnostic-only observer receives immutable copies after each
    /// completed global pass. No callbacks or extra projections exist in
    /// the production solver path (observer null). A pass-zero reading
    /// captures the unchanged historical progressive allocation.
    /// </summary>
    internal static FoundationPhysicalGlobalMarginalCoordinationPlan CoordinateAudited(
        FoundationPhysicalCommodityMarketInputs market,
        PhysicalTacticalIncidenceMap incidence,
        FoundationCommodityCargoConversionPolicy conversionPolicy,
        IEnumerable<FoundationCommodityMarketPlanningQuote> quotes,
        IEnumerable<FoundationPhysicalRoutingEdgeCost> edgeCosts,
        int maxPasses,
        bool preferIncumbentThresholdTies,
        bool acceptOnlyStrictIntegratedGain,
        Action<FoundationMarginalPassObservation>? observer)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(conversionPolicy);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(edgeCosts);
        if (maxPasses < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPasses));
        // The existing 01B.2C-1 planner seeds only real worker-backed,
        // globally reconciled deliveries, so no phantom initial Q or route.
        var quoteList = quotes.ToArray();
        var costList = edgeCosts.ToArray();
        var initial = FoundationPhysicalMarginalMarketDistributor.Distribute(
            market, incidence, conversionPolicy, quoteList, costList);
        var inputs = initial.AllocationAudit.MarketInputs;
        var ownersByMarket = FoundationPhysicalMarginalMarketDistributor
            .IndexRealBuyers(inputs);
        var marketKeys = inputs.MarketDemand
            .Select(line => (line.Tile, line.Commodity))
            .ToArray();
        var global = initial.MarketTotals
            .GroupBy(line => (line.Tile, line.Commodity))
            .ToDictionary(group => group.Key,
                group => group.Aggregate(0UL,
                    (sum, line) => checked(sum + line.AllocatedGoodsMicro)));
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
                group => group.GroupBy(candidate => candidate.BuyerTile)
                    .ToDictionary(destination => destination.Key,
                        destination => destination.Aggregate(0UL,
                            (sum, candidate) =>
                                checked(sum + candidate.GoodsMicro))));

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
            foreach (var physical in inputs.MarketDemand)
            {
                if (physical.Commodity != lot.Commodity) continue;
                var line = FoundationPhysicalMarginalMarketDistributor
                    .SharedDemand(physical, ownersByMarket);
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
                ulong already = previous.GetValueOrDefault(line.Tile);
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

        // Physical freight is read from each real source's cached route tree.
        // Tariffs are intentionally excluded, mirroring the exact accounting
        // separation: the observer is for physical freights only.
        void Observe(int pass, ulong movement)
        {
            if (observer is null) return;
            BigInteger physicalFreight = BigInteger.Zero;
            foreach (var source in sources)
            {
                for (int i = 0; i < source.Buyers.Length; i++)
                {
                    ulong goods = checked(source.GoodsQuanta[i] *
                        source.CargoQuantum);
                    if (goods == 0UL) continue;
                    var buyer = source.Buyers[i];
                    var target = checked((int)buyer.Demand.Tile
                        .FineStrategicCellId.Value);
                    ulong cargo = conversionPolicy.CargoMicro(
                        source.Lot.Commodity, goods);
                    physicalFreight += (BigInteger)cargo *
                        buyer.Route.PhysicalRate[target] *
                        FoundationPhysicalEconomicAccountingProposal.BasisPoints;
                }
            }
            observer(new FoundationMarginalPassObservation(
                pass,
                new System.Collections.ObjectModel.ReadOnlyDictionary<
                    (PhysicalTacticalTileId Tile, CommodityId Commodity), ulong>(
                    new Dictionary<
                        (PhysicalTacticalTileId Tile, CommodityId Commodity), ulong>(
                        global)),
                physicalFreight, movement));
        }
        Observe(0, 0UL);

        int passes = 0;
        ulong movedGoods = 0UL;
        var movementByPass = new List<ulong>(Math.Min(maxPasses, 1024));
        int rejectedNonImproving = 0;
        BigInteger acceptedPotentialGain = BigInteger.Zero;
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
                    var key = (buyer.Demand.Tile, buyer.Demand.Commodity);
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
                        opponents, source.Lot.CapacityMicroPerTurn,
                        preferIncumbentThresholdTies
                            ? source.GoodsQuanta : null);
                if (acceptOnlyStrictIntegratedGain &&
                    !proposed.SequenceEqual(source.GoodsQuanta))
                {
                    var gain = FoundationPhysicalMarginalIntegratedUtility
                        .ProposalGain(source.Buyers, source.GoodsQuanta,
                            proposed, source.CargoQuantum, global);
                    if (gain <= BigInteger.Zero)
                    {
                        rejectedNonImproving =
                            checked(rejectedNonImproving + 1);
                        continue;
                    }
                    acceptedPotentialGain += gain;
                }
                for (int i = 0; i < proposed.Length; i++)
                {
                    var oldQuanta = source.GoodsQuanta[i];
                    var newQuanta = proposed[i];
                    if (oldQuanta == newQuanta) continue;
                    var buyer = source.Buyers[i];
                    var key = (buyer.Demand.Tile, buyer.Demand.Commodity);
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
            movementByPass.Add(passMoved);
            Observe(pass + 1, passMoved);
            if (passMoved == 0UL)
            {
                fixedPoint = true;
                break;
            }
        }
        if (!fixedPoint)
        {
            if (acceptOnlyStrictIntegratedGain ||
                preferIncumbentThresholdTies)
                throw new FoundationPhysicalGlobalMarginalBudgetExceededException(
                    maxPasses, movementByPass
                        .Skip(Math.Max(0, movementByPass.Count - 12))
                        .ToArray(), rejectedNonImproving,
                    acceptedPotentialGain);
            throw new InvalidOperationException(
                "The global marginal candidate planner has not reached a " +
                "stable full-pass fixed point inside its explicit pass budget. " +
                "Do not publish unstable candidate deliveries as a settled market.");
        }

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
                foreach (var share in FoundationPhysicalMarginalMarketDistributor
                    .AttributeRealBuyers(ownersByMarket, buyer.Demand.Tile,
                        source.Lot.Commodity,
                        checked(quanta * source.CargoQuantum),
                        source.CargoQuantum))
                {
                    if (share.GoodsMicro == 0UL) continue;
                    finalCandidates.Add(new FoundationPhysicalCommodityDeliveryCandidate(
                        checked((ulong)finalCandidates.Count + 1UL),
                        source.Lot.Owner, source.Lot.Occupation, source.Lot.Tile,
                        share.Owner, buyer.Demand.Tile, source.Lot.Commodity,
                        share.GoodsMicro, Array.AsReadOnly(route)));
                }
            }
        }

        // Re-verify every seller, buyer and physical edge on the FINAL pass.
        // Candidate planning never issues a settlement receipt or ledger entry.
        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            market, incidence, conversionPolicy,
            finalCandidates, ownership);
        var ownerTotals = finalCandidates
            .GroupBy(candidate =>
                (candidate.BuyerTile, candidate.Buyer, candidate.Commodity))
            .ToDictionary(group => group.Key,
                group => group.Aggregate(0UL, (sum, candidate) =>
                    checked(sum + candidate.GoodsMicro)));
        var final = inputs.DemandByCivilization.Select(line =>
            new FoundationPhysicalMarketAllocationTotal(
                line.Tile, line.Owner, line.Commodity, line.DemandMicro,
                ownerTotals.GetValueOrDefault(
                    (line.Tile, line.Owner, line.Commodity)))).ToArray();
        if (final.Aggregate(BigInteger.Zero,
                (sum, line) => sum + line.AllocatedGoodsMicro) !=
            audit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException(
                "Globally adjusted market totals do not reconcile to physical sales.");
        var attributed = final
            .GroupBy(line => (line.Tile, line.Commodity))
            .ToDictionary(group => group.Key,
                group => group.Aggregate(0UL, (sum, line) =>
                    checked(sum + line.AllocatedGoodsMicro)));
        if (marketKeys.Length != global.Count ||
            inputs.MarketDemand.Any(market =>
                global[(market.Tile, market.Commodity)] !=
                attributed[(market.Tile, market.Commodity)]))
            throw new InvalidOperationException(
                "A physical market disappeared or its owner attributions " +
                "failed to conserve the shared delivered quantity.");

        return new FoundationPhysicalGlobalMarginalCoordinationPlan(
            finalCandidates.ToArray(), starting, final,
            audit, sources.Count, passes, movedGoods,
            preferIncumbentThresholdTies, movementByPass.ToArray(),
            acceptOnlyStrictIntegratedGain, rejectedNonImproving,
            acceptedPotentialGain);
    }
}
