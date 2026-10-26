using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Opt-in, deterministic, successive marginal-price clearing of all
/// registered food commodities TOGETHER against smooth strategic-edge
/// congestion costs. Unlike legacy EconomicMarketAllocationResolver, this
/// solver never reads EconomicTransferSignal.RawEffectiveCapacity and never
/// caps the quantity on an edge at a characteristic transport scale.
///
/// Bids and route offers are explicit caller inputs. Each decision selects
/// the greatest current positive marginal-return lane, advances by at most
/// the caller's NUMERICAL decision quantum (not an edge capacity), and uses
/// binary search to avoid proceeding beyond positive ending marginal return.
/// All candidate comparisons and congestion cost integration are exact
/// BigInteger rationals; no price/inventory is invented.
///
/// This is a deterministic successive-marginal approximation rather than a
/// certificate of global optimum for the continuous multi-commodity problem.
/// MaxDecisions throws rather than returning a truncated market.
/// </summary>
public sealed class EconomicCongestionPricedFoodMarketResolver
{
    public EconomicCongestionMarketResult Resolve(
        WorldGenerationResult generatedWorld,
        WorldState worldState,
        StrategicEdgeAccessSnapshot edgeAccess,
        EconomicProductionDemandResult flows,
        IEnumerable<EconomicCongestionDemandBid> bids,
        IEnumerable<EconomicCongestionRouteOffer> offers,
        FoodCommodityCatalog catalog,
        IEnumerable<FoodStrategicEdgeCongestionProfile> profiles,
        EconomicCongestionMarketSearchPolicy searchPolicy)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(worldState);
        ArgumentNullException.ThrowIfNull(edgeAccess);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(bids);
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(searchPolicy);

        if (worldState.WorldBinding is null ||
            worldState.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "Congestion-priced market must be bound to the supplied generated world.");

        var graphEdges = generatedWorld.StrategicTopology.Edges
            .Select(edge => edge.Id).ToHashSet();

        var edgeProfiles = new Dictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile>();
        foreach (var profile in Snapshot(profiles, nameof(profiles)))
        {
            if (!graphEdges.Contains(profile.StrategicEdgeId))
                throw new InvalidOperationException(
                    "Congestion profile references an edge outside the generated world.");
            if (!edgeProfiles.TryAdd(profile.StrategicEdgeId, profile))
                throw new ArgumentException(
                    "Duplicate congestion profile strategic edge.", nameof(profiles));
        }

        foreach (var supply in flows.SupplyFlows)
            if (!catalog.Contains(supply.CommodityId))
                throw new InvalidOperationException(
                    "Joint physical-mass market requires a registered food scale for all supply flows.");
        foreach (var demand in flows.DemandFlows)
            if (!catalog.Contains(demand.CommodityId))
                throw new InvalidOperationException(
                    "Joint physical-mass market requires a registered food scale for all demand flows.");

        var demands = flows.DemandFlows.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId),
            row => new DemandBalance(row));
        var supplies = flows.SupplyFlows.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId, row.Activity),
            row => new SupplyBalance(row));

        var bidTable = new Dictionary<
            (EconomicPointId, CommodityId), EconomicCongestionDemandBid>();
        foreach (var bid in Snapshot(bids, nameof(bids)))
        {
            var key = (bid.EconomicPointId, bid.CommodityId);
            if (!demands.ContainsKey(key))
                throw new ArgumentException(
                    "A congestion market bid must match an explicit demand flow.", nameof(bids));
            if (!bidTable.TryAdd(key, bid))
                throw new ArgumentException("Duplicate point/food bids.", nameof(bids));
        }

        if (bidTable.Count != demands.Count)
            throw new ArgumentException(
                "Every explicit demand flow requires an explicit unit-value bid.", nameof(bids));

        var routeOffers = new Dictionary<
            StrategicEconomicRouteKey, EconomicCongestionRouteOffer>();
        var supplyPoints = supplies.Keys.Select(k => k.Item1).ToHashSet();
        var demandPoints = demands.Keys.Select(k => k.Item1).ToHashSet();

        foreach (var offer in Snapshot(offers, nameof(offers)))
        {
            if (!supplyPoints.Contains(offer.RouteKey.SourceEconomicPointId) ||
                !demandPoints.Contains(offer.RouteKey.DestinationEconomicPointId))
                throw new ArgumentException(
                    "Route offer must connect a point with supply to a point with demand.",
                    nameof(offers));
            if (!routeOffers.TryAdd(offer.RouteKey, offer))
                throw new ArgumentException("Duplicate directional route offers.", nameof(offers));
        }

        var router = new StrategicEconomicRouteResolver(
            generatedWorld.StrategicSurfaceGraph);
        var paths = new Dictionary<StrategicEconomicRouteKey, StrategicEconomicRoute>();
        var lanes = new List<Lane>();

        foreach (var supply in supplies.Values
                     .OrderBy(s => s.Flow.CommodityId.Value)
                     .ThenBy(s => s.Flow.EconomicPointId.Value)
                     .ThenBy(s => (byte)s.Flow.Activity))
        {
            var food = catalog.Commodities.Single(x => x.CommodityId == supply.Flow.CommodityId);
            foreach (var demand in demands.Values
                         .Where(d => d.Flow.CommodityId == supply.Flow.CommodityId)
                         .OrderBy(d => d.Flow.EconomicPointId.Value))
            {
                var key = new StrategicEconomicRouteKey(
                    supply.Flow.EconomicPointId, demand.Flow.EconomicPointId);
                if (!routeOffers.TryGetValue(key, out var offer))
                    continue;

                if (!paths.TryGetValue(key, out var route))
                {
                    var resolved = router.ResolveDetailed(
                        worldState.Economy, key, edgeAccess);
                    if (!resolved.IsReachable)
                        continue; // An explicit geopolitical closure is not a congestion limit.

                    route = resolved.Route!;
                    paths.Add(key, route);
                }

                foreach (var edgeId in route.EdgePath)
                    if (!edgeProfiles.ContainsKey(edgeId))
                        throw new InvalidOperationException(
                            "A used strategic edge requires an explicit marginal congestion cost profile.");

                lanes.Add(new Lane(
                    supply, demand, bidTable[(demand.Flow.EconomicPointId, demand.Flow.CommodityId)],
                    offer, food, route));
            }
        }

        var load = new Dictionary<StrategicEdgeId, BigInteger>();
        var selections = new List<EconomicCongestionMarketSelection>();

        while (true)
        {
            Lane? chosen = null;
            SignedMargin best = default;
            ulong chosenQuantity = 0UL;

            // All food commodities compete on the same currently loaded
            // strategic edges. Canonical lane order breaks exact ties.
            foreach (var lane in lanes)
            {
                var remaining = Math.Min(
                    lane.Supply.Remaining, lane.Demand.Remaining);
                if (remaining == 0UL)
                    continue;

                var current = MarginalReturn(lane, 0UL, load, edgeProfiles);
                if (current.Numerator <= 0)
                    continue;

                var profitable = LargestProfitableQuantity(
                    lane, remaining, load, edgeProfiles);
                if (profitable == 0UL)
                    continue;

                if (chosen is null || CompareMargins(current, best) > 0)
                {
                    chosen = lane;
                    best = current;
                    chosenQuantity = Math.Min(
                        profitable, searchPolicy.RawDecisionQuantum);
                }
            }

            if (chosen is null)
                break;

            if (selections.Count >= searchPolicy.MaxDecisions)
                throw new InvalidOperationException(
                    "Marginal market search exhausted its explicit decision budget; " +
                    "increase search budget/quantum rather than silently truncating trade.");

            // Exact integrated incremental shared congestion expense per
            // decision. A shared edge is repriced before the next commodity.
            FoodTransportCostFraction incremental = new(BigInteger.Zero, BigInteger.One);
            var changedLoad = new List<(StrategicEdgeId Edge, BigInteger Next)>();

            var extraMicrograms =
                (BigInteger)chosenQuantity * chosen.Food.GramsPerFlowUnit;
            foreach (var edgeId in chosen.Route.EdgePath)
            {
                var previous = load.TryGetValue(edgeId, out var old)
                    ? old : BigInteger.Zero;
                var next = previous + extraMicrograms;
                var profile = edgeProfiles[edgeId];

                incremental = Add(
                    incremental,
                    Subtract(
                        profile.EvaluateTotalCost(next),
                        profile.EvaluateTotalCost(previous)));
                changedLoad.Add((edgeId, next));
            }

            // Construction requires strictly profitable exact settlement:
            // it never pretends convex cost is the legacy flat unit tariff.
            var selection = new EconomicCongestionMarketSelection(
                (ulong)selections.Count + 1UL,
                chosen.Supply.Flow.EconomicPointId,
                chosen.Supply.Flow.Activity,
                chosen.Demand.Flow.EconomicPointId,
                chosen.Food.CommodityId,
                chosenQuantity,
                chosen.Bid.RawUnitValue,
                chosen.Offer.RawBaseUnitTransferCost,
                incremental);

            selections.Add(selection);
            chosen.Supply.Remaining -= chosenQuantity;
            chosen.Demand.Remaining -= chosenQuantity;
            foreach (var (edge, next) in changedLoad)
                load[edge] = next;
        }

        return new EconomicCongestionMarketResult(
            selections,
            new FoodStrategicEdgeCongestionResult(
                load.Select(row =>
                    new FoodStrategicEdgeCongestionEntry(
                        edgeProfiles[row.Key], row.Value))));
    }

    private static T[] Snapshot<T>(IEnumerable<T> source, string argument)
        where T : class
    {
        var rows = source.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Economic market input cannot contain null entries.", argument);
        return rows;
    }

    private static ulong LargestProfitableQuantity(
        Lane lane,
        ulong maxQuantity,
        Dictionary<StrategicEdgeId, BigInteger> load,
        Dictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile> profiles)
    {
        ulong lower = 0UL;
        ulong upper = maxQuantity;
        while (lower < upper)
        {
            var difference = upper - lower;
            var midpoint = lower + difference / 2UL + difference % 2UL;
            if (MarginalReturn(lane, midpoint, load, profiles).Numerator > 0)
                lower = midpoint;
            else
                upper = midpoint - 1UL;
        }
        return lower;
    }

    private static SignedMargin MarginalReturn(
        Lane lane,
        ulong candidateQuantity,
        Dictionary<StrategicEdgeId, BigInteger> load,
        Dictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile> profiles)
    {
        BigInteger numerator =
            (BigInteger)lane.Bid.RawUnitValue - lane.Offer.RawBaseUnitTransferCost;
        BigInteger denominator = BigInteger.One;
        BigInteger extraMass = (BigInteger)candidateQuantity * lane.Food.GramsPerFlowUnit;

        foreach (var edge in lane.Route.EdgePath)
        {
            var current = load.TryGetValue(edge, out var mass)
                ? mass : BigInteger.Zero;
            var marginal = profiles[edge].EvaluateMarginalCostPerGram(current + extraMass);

            numerator = numerator * marginal.Denominator
                - denominator * marginal.Numerator * lane.Food.GramsPerFlowUnit;
            denominator *= marginal.Denominator;

            var divisor = BigInteger.GreatestCommonDivisor(
                BigInteger.Abs(numerator), denominator);
            numerator /= divisor;
            denominator /= divisor;
        }

        return new SignedMargin(numerator, denominator);
    }

    private static int CompareMargins(SignedMargin left, SignedMargin right) =>
        (left.Numerator * right.Denominator)
            .CompareTo(right.Numerator * left.Denominator);

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction left, FoodTransportCostFraction right) =>
        new(left.Numerator * right.Denominator
                + right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);

    private static FoodTransportCostFraction Subtract(
        FoodTransportCostFraction greater, FoodTransportCostFraction smaller)
    {
        var numerator = greater.Numerator * smaller.Denominator
            - smaller.Numerator * greater.Denominator;
        if (numerator < 0)
            throw new InvalidOperationException("Congestion cost must be monotonic.");

        return new FoodTransportCostFraction(
            numerator, greater.Denominator * smaller.Denominator);
    }

    private readonly record struct SignedMargin(
        BigInteger Numerator, BigInteger Denominator);

    private sealed class SupplyBalance(EconomicSupplyFlow flow)
    {
        public EconomicSupplyFlow Flow { get; } = flow;
        public ulong Remaining { get; set; } = flow.RawQuantity;
    }

    private sealed class DemandBalance(EconomicDemandFlow flow)
    {
        public EconomicDemandFlow Flow { get; } = flow;
        public ulong Remaining { get; set; } = flow.RawQuantity;
    }

    private sealed class Lane(
        SupplyBalance supply,
        DemandBalance demand,
        EconomicCongestionDemandBid bid,
        EconomicCongestionRouteOffer offer,
        FoodCommodityDefinition food,
        StrategicEconomicRoute route)
    {
        public SupplyBalance Supply { get; } = supply;
        public DemandBalance Demand { get; } = demand;
        public EconomicCongestionDemandBid Bid { get; } = bid;
        public EconomicCongestionRouteOffer Offer { get; } = offer;
        public FoodCommodityDefinition Food { get; } = food;
        public StrategicEconomicRoute Route { get; } = route;
    }
}
