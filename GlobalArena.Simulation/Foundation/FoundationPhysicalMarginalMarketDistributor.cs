using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A real worker-backed market's allocated quantity after one uncommitted
/// progressive distribution pass. D and Q are commodity micro-units;
/// no price, wallet, realized transaction or treasury entry is created.
/// </summary>
public sealed record FoundationPhysicalMarketAllocationTotal(
    PhysicalTacticalTileId Tile, CivilizationId Buyer, CommodityId Commodity,
    ulong DemandMicro, ulong AllocatedGoodsMicro);

/// <summary>
/// Multi-destination CANDIDATE distribution reconciled against PR #388.
/// All amounts are exact; positive deliveries are routed and audited on
/// canonical fine edges. Nothing here is an economic-cycle settlement.
/// </summary>
public sealed class FoundationPhysicalMarginalMarketDistributionPlan
{
    public IReadOnlyList<FoundationPhysicalCommodityDeliveryCandidate> Candidates { get; }
    public IReadOnlyList<FoundationPhysicalMarketAllocationTotal> MarketTotals { get; }
    public FoundationPhysicalDeliveryAllocationAudit AllocationAudit { get; }
    public int ProductiveLotCount { get; }
    public int BuyerCount { get; }

    internal FoundationPhysicalMarginalMarketDistributionPlan(
        FoundationPhysicalCommodityDeliveryCandidate[] candidates,
        FoundationPhysicalMarketAllocationTotal[] totals,
        FoundationPhysicalDeliveryAllocationAudit audit,
        int lotCount, int buyerCount)
    {
        Candidates = Array.AsReadOnly(candidates);
        MarketTotals = Array.AsReadOnly(totals);
        AllocationAudit = audit;
        ProductiveLotCount = lotCount;
        BuyerCount = buyerCount;
    }
}

/// <summary>
/// Fixed-point, deterministic, progressive marginal allocator on the real
/// Goldberg graph. Every producing LOT can split among multiple real buyers.
/// Per-buyer marginal planning signal: P0 * sqrt(D / Q) for Q > 0, where
/// P0 is an EXPLICIT external quote and D is real worker demand. Q=0 is
/// unpriced, NOT seeded with fictional goods. Each lot is solved in canonical
/// sequence with previous deliveries affecting subsequent marginal values.
/// This is NOT a certified simultaneous global equilibrium, final price,
/// congestion-aware route flow, or monetary settlement.
/// </summary>
public static class FoundationPhysicalMarginalMarketDistributor
{
    internal const ulong Micro = 1_000_000UL;
    internal static readonly BigInteger ScoreScale = new(Micro);

    internal sealed record Buyer(
        FoundationPhysicalCommodityDemandLine Demand,
        FoundationCommodityMarketPlanningQuote Quote,
        FoundationPhysicalTradeRouteCandidatePlanner.RouteTree Route,
        BigInteger BaseQuoteScaled,
        BigInteger CostScaled,
        ulong PreviousGoodsMicro,
        ulong CargoQuantum);

    internal static ulong Gcd(ulong a, ulong b)
    {
        while (b != 0UL)
            (a, b) = (b, a % b);
        return a;
    }

    /// <summary>
    /// Exact maximally admissible NEW transport quanta at a threshold lambda:
    /// P0*sqrt(D/(Qprevious+Qnew)) - freight >= lambda.
    /// Squaring the strictly-positive side removes all floats/square roots.
    /// </summary>
    private static ulong AtThreshold(
        Buyer buyer, BigInteger lambda, ulong supplyQuanta)
    {
        var right = lambda + buyer.CostScaled;
        if (right <= 0)
            return supplyQuanta;
        BigInteger numerator = buyer.BaseQuoteScaled *
            buyer.BaseQuoteScaled * buyer.Demand.DemandMicro;
        BigInteger maximumGoodsMicro = numerator / (right * right);
        var availableGoodsMicro = maximumGoodsMicro -
            buyer.PreviousGoodsMicro;
        if (availableGoodsMicro <= 0)
            return 0UL;
        var quanta = availableGoodsMicro / buyer.CargoQuantum;
        if (quanta >= supplyQuanta)
            return supplyQuanta;
        return checked((ulong)quanta);
    }

    internal static ulong[] DistributeOneLot(
        IReadOnlyList<Buyer> buyers, ulong quantityMicro,
        IReadOnlyList<ulong>? incumbentQuanta = null)
    {
        if (buyers.Count == 0)
            throw new InvalidOperationException(
                "Real commodity output has no physically reachable buyer.");

        // All destinations of this commodity use the SAME exact cargo
        // representability quantum, determined by its explicit conversion.
        if (incumbentQuanta is not null &&
            incumbentQuanta.Count != buyers.Count)
            throw new ArgumentException(
                "Every real eligible buyer needs an incumbent quota.",
                nameof(incumbentQuanta));
        ulong quantum = buyers[0].CargoQuantum;
        if (quantityMicro % quantum != 0UL)
            throw new InvalidOperationException(
                "The source lot cannot be partitioned exactly in the " +
                "declared common cargo micro-unit.");
        ulong supplyQuanta = quantityMicro / quantum;
        if (supplyQuanta == 0UL)
            throw new InvalidOperationException("Positive supply is below cargo quantum.");

        // Lambda is measured in micro-money * 1e6. This extra comparison
        // precision handles fractional physical/toll costs without rounding.
        // High is above the highest first-micro price, low is below all costs:
        // the two endpoints strictly bracket every finite positive allocation.
        BigInteger maxScaledQuoteDemand = BigInteger.Zero;
        BigInteger maxCost = BigInteger.Zero;
        foreach (var buyer in buyers)
        {
            BigInteger high = buyer.BaseQuoteScaled *
                (new BigInteger(buyer.Demand.DemandMicro) + BigInteger.One);
            if (high > maxScaledQuoteDemand)
                maxScaledQuoteDemand = high;
            if (buyer.CostScaled > maxCost)
                maxCost = buyer.CostScaled;
        }
        BigInteger low = -maxCost - BigInteger.One;
        BigInteger highBound = maxScaledQuoteDemand + BigInteger.One;

        BigInteger SumAt(BigInteger lambda)
        {
            BigInteger sum = BigInteger.Zero;
            foreach (var buyer in buyers)
                sum += AtThreshold(buyer, lambda, supplyQuanta);
            return sum;
        }

        if (SumAt(low) < supplyQuanta || SumAt(highBound) != BigInteger.Zero)
            throw new InvalidOperationException(
                "Marginal allocation price bracket failed its conservation checks.");

        // Integer bisection, not iteration per produced micro-unit. Find the
        // highest lambda whose combined desired quantities cover this lot.
        while (highBound - low > BigInteger.One)
        {
            BigInteger middle = (low + highBound) / 2;
            if (SumAt(middle) >= supplyQuanta)
                low = middle;
            else
                highBound = middle;
        }
        var allocations = buyers.Select(buyer =>
            AtThreshold(buyer, highBound, supplyQuanta)).ToArray();
        BigInteger sumInitial = allocations.Aggregate(BigInteger.Zero,
            (sum, value) => sum + value);
        if (sumInitial >= supplyQuanta)
            throw new InvalidOperationException("Nonconservative marginal bracket.");
        ulong remaining = checked((ulong)(supplyQuanta - sumInitial));
        var capacities = buyers.Select((buyer, index) =>
            AtThreshold(buyer, low, supplyQuanta) - allocations[index]).ToArray();
        BigInteger totalCapacity = capacities.Aggregate(BigInteger.Zero,
            (sum, value) => sum + value);
        if (totalCapacity < remaining)
            throw new InvalidOperationException(
                "Marginal allocation cannot distribute the full real production.");

        // A multi-seller response can optionally preserve the seller's
        // previous holdings ONLY within the EXACT threshold plateau. This
        // prevents unnecessary cross-producer churn caused by canonical
        // proportional tie-breaking; it never overrides a quantity forced
        // by the higher marginal threshold or invents new demand.
        // The historical single-pass solver passes null and is unchanged.
        if (incumbentQuanta is not null)
        {
            for (int i = 0; i < allocations.Length && remaining > 0UL; i++)
            {
                ulong keep = incumbentQuanta[i] > allocations[i]
                    ? Math.Min(capacities[i],
                        incumbentQuanta[i] - allocations[i])
                    : 0UL;
                keep = Math.Min(keep, remaining);
                allocations[i] = checked(allocations[i] + keep);
                capacities[i] -= keep;
                remaining -= keep;
            }
        }

        // Remaining exact-threshold quanta use proportional division and
        // canonical tail; only a marginal PRICE TIE is resolved differently.
        ulong assigned = 0UL;
        for (int i = 0; i < allocations.Length; i++)
        {
            ulong share = checked((ulong)(
                (new BigInteger(remaining) * capacities[i]) / totalCapacity));
            allocations[i] = checked(allocations[i] + share);
            capacities[i] -= share;
            assigned = checked(assigned + share);
        }
        ulong tail = remaining - assigned;
        for (int i = 0; i < allocations.Length && tail > 0UL; i++)
        {
            if (capacities[i] == 0UL) continue;
            allocations[i] = checked(allocations[i] + 1UL);
            tail--;
        }
        if (tail != 0UL ||
            allocations.Aggregate(BigInteger.Zero,
                (sum, value) => sum + value) != supplyQuanta)
            throw new InvalidOperationException(
                "Marginal allocator did not conserve the complete real lot.");
        return allocations;
    }

    public static FoundationPhysicalMarginalMarketDistributionPlan Distribute(
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
                "Marginal distribution requires identical demand/cargo catalogs.",
                nameof(conversionPolicy));

        var lines = market.DemandByCivilization.ToDictionary(
            line => (line.Tile, line.Owner, line.Commodity));
        var quoteMap = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Owner,
             CommodityId Commodity), FoundationCommodityMarketPlanningQuote>();
        foreach (var quote in quotes)
        {
            if (quote is null || quote.QuoteMicroPerWholeCommodityUnit == 0UL ||
                !quote.BuyerTile.IsValid || !quote.Buyer.IsValid ||
                !quote.Commodity.IsValid ||
                !lines.ContainsKey((quote.BuyerTile, quote.Buyer, quote.Commodity)) ||
                !quoteMap.TryAdd((quote.BuyerTile, quote.Buyer, quote.Commodity),
                    quote))
                throw new ArgumentException(
                    "Every real buyer/commodity needs one unique explicit " +
                    "positive marginal scaling quote.", nameof(quotes));
        }
        if (quoteMap.Count != lines.Count)
            throw new ArgumentException(
                "Every real physical demand line needs a scaling quote.",
                nameof(quotes));

        var (graph, ownership) =
            FoundationPhysicalTradeRouteCandidatePlanner.BuildGraph(
                incidence, edgeCosts);
        int cellCount = incidence.FineTopology.Cells.Count;
        var routes = new Dictionary<
            (PhysicalTacticalTileId Tile, CivilizationId Seller),
            FoundationPhysicalTradeRouteCandidatePlanner.RouteTree>();
        var delivered = lines.Keys.ToDictionary(key => key, _ => 0UL);
        var candidates = new List<FoundationPhysicalCommodityDeliveryCandidate>();

        foreach (var lot in market.SupplyLots)
        {
            if (lot.Tile.FineGoldbergParameters !=
                    incidence.Refinement.FineParameters ||
                lot.Tile.FineStrategicCellId.Value == 0UL ||
                lot.Tile.FineStrategicCellId.Value > (ulong)cellCount)
                throw new InvalidOperationException(
                    "A worker produces outside the current fine topology.");
            var routeKey = (lot.Tile, lot.Owner);
            if (!routes.TryGetValue(routeKey, out var tree))
            {
                tree = FoundationPhysicalTradeRouteCandidatePlanner.RouteFrom(
                    lot.Tile.FineStrategicCellId.Value, lot.Owner, graph,
                    cellCount);
                routes.Add(routeKey, tree);
            }

            ulong cargoPerWholeGood = conversionPolicy.CargoMicro(
                lot.Commodity, Micro);
            ulong quantum = Micro / Gcd(Micro, cargoPerWholeGood);
            var eligible = new List<Buyer>();
            foreach (var line in market.DemandByCivilization)
            {
                if (line.Commodity != lot.Commodity) continue;
                var idx = checked((int)line.Tile.FineStrategicCellId.Value);
                if (line.Tile.FineGoldbergParameters !=
                        incidence.Refinement.FineParameters ||
                    idx < 1 || idx > cellCount)
                    throw new InvalidOperationException(
                        "Buyer location is outside the current fine topology.");
                if (tree.TotalRate[idx] == ulong.MaxValue)
                    continue;
                var quote = quoteMap[(line.Tile, line.Owner, line.Commodity)];
                // Both terms are micro-money per whole cargo unit and must
                // be converted to a comparable goods rate. BigInteger
                // preserves the exact fractional micro-money comparison.
                BigInteger goodsCargoFactor = new(cargoPerWholeGood);
                BigInteger costScaled = goodsCargoFactor *
                    (new BigInteger(tree.PhysicalRate[idx]) +
                     tree.TariffRate[idx]);
                eligible.Add(new Buyer(line, quote, tree,
                    new BigInteger(quote.QuoteMicroPerWholeCommodityUnit) *
                        ScoreScale, costScaled,
                    delivered[(line.Tile, line.Owner, line.Commodity)], quantum));
            }
            ulong[] allocation = DistributeOneLot(
                eligible, lot.CapacityMicroPerTurn);

            for (int i = 0; i < eligible.Count; i++)
            {
                ulong quantity = checked(allocation[i] * quantum);
                if (quantity == 0UL) continue;
                var buyer = eligible[i];
                var key = (buyer.Demand.Tile, buyer.Demand.Owner,
                    buyer.Demand.Commodity);
                delivered[key] = checked(delivered[key] + quantity);
                var route =
                    FoundationPhysicalTradeRouteCandidatePlanner.Reconstruct(
                        lot.Tile, buyer.Demand.Tile, buyer.Route);
                var ordinal = checked((ulong)candidates.Count + 1UL);
                candidates.Add(new FoundationPhysicalCommodityDeliveryCandidate(
                    ordinal, lot.Owner, lot.Occupation, lot.Tile,
                    buyer.Demand.Owner, buyer.Demand.Tile, lot.Commodity,
                    quantity, Array.AsReadOnly(route)));
            }
        }

        // The hard conservation, exact cargo conversion, physical adjacency
        // and ownership gates of PR #388 are mandatory, not best-effort.
        var audit = FoundationPhysicalDeliveryAllocationAuditor.Audit(
            census, incidence, demandPolicy, conversionPolicy,
            candidates, ownership);
        var totals = market.DemandByCivilization.Select(line =>
            new FoundationPhysicalMarketAllocationTotal(
                line.Tile, line.Owner, line.Commodity,
                line.DemandMicro,
                delivered[(line.Tile, line.Owner, line.Commodity)])).ToArray();
        if (totals.Aggregate(BigInteger.Zero,
                (sum, line) => sum + line.AllocatedGoodsMicro) !=
            audit.TotalAllocatedGoodsMicro)
            throw new InvalidOperationException(
                "Market delivery totals lost or duplicated real produced goods.");

        return new FoundationPhysicalMarginalMarketDistributionPlan(
            candidates.ToArray(), totals, audit, market.SupplyLots.Count,
            market.DemandByCivilization.Count);
    }
}
