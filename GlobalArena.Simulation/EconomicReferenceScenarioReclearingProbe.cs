using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A single pre-turn, frozen freight quote and its caller-declared physical
/// resource incidence. The resource path is not proof of graph reachability.
/// Different complete scenarios may quote the same OD differently, but each
/// scenario has only one path per OD in the current reference market.
/// </summary>
public sealed class EconomicReclearingQuotedRoute
{
    public EconomicTurnFreightQuote Quote { get; }
    public IReadOnlyList<EconomicPhysicalResourceId> PhysicalResources { get; }

    public EconomicReclearingQuotedRoute(
        EconomicTurnFreightQuote quote,
        IEnumerable<EconomicPhysicalResourceId> physicalResources)
    {
        Quote = quote ?? throw new ArgumentNullException(nameof(quote));
        ArgumentNullException.ThrowIfNull(physicalResources);
        var resourceIds = physicalResources.ToArray();
        if (resourceIds.Any(id => !id.IsValid) ||
            resourceIds.Distinct().Count() != resourceIds.Length)
            throw new ArgumentException(
                "A declared service path cannot contain invalid or duplicated resource identities.",
                nameof(physicalResources));
        PhysicalResources = Array.AsReadOnly(resourceIds);
    }
}

/// <summary>
/// Observation from an opt-in, deterministic reference market resolution.
/// The consumed resource loads are reconstructed from actual traded quantities,
/// not assumed to be the initial forecast or the tentative finite lot.
/// This carries no liquidity, consent or settlement certification.
/// </summary>
public sealed class EconomicReclearedReferenceScenario
{
    public IReadOnlyList<EconomicTurnLockedFreightTrade> Trades { get; }
    public IReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger> ResourceLoadsMicrograms { get; }
    public BigInteger RawTradedQuantity { get; }
    public FoodTransportCostFraction RealCostLower { get; }
    public FoodTransportCostFraction RealCostUpper { get; }

    internal EconomicReclearedReferenceScenario(
        IReadOnlyList<EconomicTurnLockedFreightTrade> trades,
        IReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger> loads,
        BigInteger rawTradedQuantity,
        FoodTransportCostFraction realCostLower,
        FoodTransportCostFraction realCostUpper)
    {
        Trades = trades;
        ResourceLoadsMicrograms = loads;
        RawTradedQuantity = rawTradedQuantity;
        RealCostLower = realCostLower;
        RealCostUpper = realCostUpper;
    }
}

/// <summary>
/// Difference in real operating cost from *two individually recleared markets*.
/// The signed interval can be positive, negative or unresolved. It is NEVER
/// automatically a justification for a market switch or financial settlement.
/// </summary>
public sealed class EconomicReferenceScenarioComparison
{
    public EconomicReclearedReferenceScenario Baseline { get; }
    public EconomicReclearedReferenceScenario Proposed { get; }

    public BigInteger SignedRealCostDeltaLowerNumerator { get; }
    public BigInteger RealCostDeltaLowerDenominator { get; }
    public BigInteger SignedRealCostDeltaUpperNumerator { get; }
    public BigInteger RealCostDeltaUpperDenominator { get; }

    public EconomicDensityCostDeltaClassification RealCostSign =>
        SignedRealCostDeltaUpperNumerator.Sign < 0
            ? EconomicDensityCostDeltaClassification.CertifiedCostDecrease
            : SignedRealCostDeltaLowerNumerator.Sign > 0
                ? EconomicDensityCostDeltaClassification.CertifiedCostIncrease
                : EconomicDensityCostDeltaClassification.Indeterminate;

    internal EconomicReferenceScenarioComparison(
        EconomicReclearedReferenceScenario baseline,
        EconomicReclearedReferenceScenario proposed,
        (BigInteger Numerator, BigInteger Denominator) lower,
        (BigInteger Numerator, BigInteger Denominator) upper)
    {
        Baseline = baseline;
        Proposed = proposed;
        SignedRealCostDeltaLowerNumerator = lower.Numerator;
        RealCostDeltaLowerDenominator = lower.Denominator;
        SignedRealCostDeltaUpperNumerator = upper.Numerator;
        RealCostDeltaUpperDenominator = upper.Denominator;
    }
}

/// <summary>
/// Pure bridge between the EXISTING deterministic, turn-locked reference
/// allocator and the opt-in density-economy real-cost function.
///
/// Each scenario resolves all commodity demand value curves afresh from zero,
/// with its own caller-provided turn-locked freight quotes. For every traded
/// tranche we independently audit physical supply/demand, incremental
/// commodity value, freight charge, producer reservation cost and net return.
/// Real resource costs are recomputed from the actually resolved route flows.
///
/// The reference allocator does NOT solve GA-PROP-001 origin prices, utility
/// budgets or competitive equilibria; its reservation prices and ex-ante
/// freight quotes remain caller-supplied. No WorldState is read or written.
/// </summary>
public sealed class EconomicReferenceScenarioReclearingProbe
{
    private const ulong RawDenominator = EconomicTransferPolicy.FixedPointDenominator;
    private readonly EconomicTurnLockedFreightMarketResolver _market = new();

    public EconomicReferenceScenarioComparison Evaluate(
        ulong turnOrdinal,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<EconomicDensityResourceCostProfile> realCostProfiles,
        IEnumerable<EconomicReclearingQuotedRoute> baselineRoutes,
        IEnumerable<EconomicReclearingQuotedRoute> proposedRoutes,
        IEnumerable<EconomicTurnCommodityValueOffer> commodityValues,
        IEnumerable<EconomicTurnSourceReservation> reservations,
        EconomicCongestionMarketSearchPolicy searchPolicy)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(realCostProfiles);
        ArgumentNullException.ThrowIfNull(baselineRoutes);
        ArgumentNullException.ThrowIfNull(proposedRoutes);
        ArgumentNullException.ThrowIfNull(commodityValues);
        ArgumentNullException.ThrowIfNull(reservations);
        ArgumentNullException.ThrowIfNull(searchPolicy);

        var profiles = new Dictionary<EconomicPhysicalResourceId, EconomicDensityResourceCostProfile>();
        foreach (var profile in realCostProfiles)
            if (profile is null || !profiles.TryAdd(profile.ResourceId, profile))
                throw new ArgumentException("Real resource profiles must be nonnull and distinct.",
                    nameof(realCostProfiles));

        var values = Snapshot(commodityValues, nameof(commodityValues));
        var sourceReservations = Snapshot(reservations, nameof(reservations));
        var sourceCosts = sourceReservations.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId, row.Activity),
            row => row.RawUnitReservationCost);
        var destinationValues = values.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId), row => row.Curve);

        var baseline = ResolveScenario(turnOrdinal, flows, catalog,
            profiles, Snapshot(baselineRoutes, nameof(baselineRoutes)),
            values, sourceReservations, sourceCosts, destinationValues, searchPolicy);
        var proposed = ResolveScenario(turnOrdinal, flows, catalog,
            profiles, Snapshot(proposedRoutes, nameof(proposedRoutes)),
            values, sourceReservations, sourceCosts, destinationValues, searchPolicy);

        // Recompute the *net actual* resource change. In particular, do not
        // reuse the lot initially suggested by a cold-start path probe:
        // scenario-specific market-cleared quantities can be different.
        var lower = (Numerator: BigInteger.Zero, Denominator: BigInteger.One);
        var upper = lower;
        foreach (var profile in profiles.Values.OrderBy(p => p.ResourceId.Value))
        {
            var id = profile.ResourceId;
            var q0 = baseline.ResourceLoadsMicrograms.GetValueOrDefault(id, BigInteger.Zero);
            var q1 = proposed.ResourceLoadsMicrograms.GetValueOrDefault(id, BigInteger.Zero);
            if (q0 == q1)
                continue; // Unchanged physical resource cancels exactly.

            var a = profile.EvaluateTotalCostBounds(q0);
            var b = profile.EvaluateTotalCostBounds(q1);
            lower = Add(lower, Subtract(b.Lower, a.Upper));
            upper = Add(upper, Subtract(b.Upper, a.Lower));
        }
        if (lower.Numerator * upper.Denominator >
            upper.Numerator * lower.Denominator)
            throw new InvalidOperationException("Invalid recleared cost interval.");

        return new EconomicReferenceScenarioComparison(baseline, proposed, lower, upper);
    }

    private EconomicReclearedReferenceScenario ResolveScenario(
        ulong turnOrdinal,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IReadOnlyDictionary<EconomicPhysicalResourceId, EconomicDensityResourceCostProfile> profiles,
        EconomicReclearingQuotedRoute[] routes,
        EconomicTurnCommodityValueOffer[] values,
        EconomicTurnSourceReservation[] reservations,
        IReadOnlyDictionary<(EconomicPointId, CommodityId, EconomicActivityKind), ulong> sourceCosts,
        IReadOnlyDictionary<(EconomicPointId, CommodityId), IEconomicTurnCommodityValueCurve> destinationValues,
        EconomicCongestionMarketSearchPolicy searchPolicy)
    {
        var byRoute = new Dictionary<StrategicEconomicRouteKey, EconomicReclearingQuotedRoute>();
        foreach (var route in routes)
        {
            if (route.Quote.TurnOrdinal != turnOrdinal)
                throw new ArgumentException("Candidate quote belongs to another turn.");
            if (!byRoute.TryAdd(route.Quote.RouteKey, route))
                throw new ArgumentException("Candidate routes must have unique directional OD keys.");
            foreach (var id in route.PhysicalResources)
                if (!profiles.ContainsKey(id))
                    throw new ArgumentException("A quoted itinerary references an unpriced real resource.");
        }

        var quotes = routes.Select(row => row.Quote).ToArray();
        var trades = _market.Resolve(
            turnOrdinal, flows, catalog, quotes, values, reservations, searchPolicy);

        var supply = flows.SupplyFlows.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId, row.Activity),
            row => (BigInteger)row.RawQuantity);
        var demand = flows.DemandFlows.ToDictionary(
            row => (row.EconomicPointId, row.CommodityId),
            row => (BigInteger)row.RawQuantity);
        var allocatedAtDestinations = new Dictionary<(EconomicPointId, CommodityId), ulong>();
        var actualLoads = new Dictionary<EconomicPhysicalResourceId, BigInteger>();
        var rawTotal = BigInteger.Zero;
        var ordinal = 0UL;

        foreach (var trade in trades)
        {
            ordinal++;
            if (trade.DecisionOrdinal != ordinal || trade.TurnOrdinal != turnOrdinal ||
                trade.RawQuantity == 0UL)
                throw new InvalidOperationException("Invalid market decision identity.");

            var src = (trade.SourceEconomicPointId, trade.CommodityId, trade.SourceActivity);
            var dst = (trade.DestinationEconomicPointId, trade.CommodityId);
            var routeKey = new StrategicEconomicRouteKey(
                trade.SourceEconomicPointId, trade.DestinationEconomicPointId);
            if (!supply.TryGetValue(src, out var remainingSupply) ||
                !demand.TryGetValue(dst, out var remainingDemand) ||
                remainingSupply < trade.RawQuantity ||
                remainingDemand < trade.RawQuantity)
                throw new InvalidOperationException("Market exhausted declared goods or demand.");

            if (!byRoute.TryGetValue(routeKey, out var route) ||
                !ReferenceEquals(route.Quote, trade.FreightQuote) ||
                !catalog.TryGet(trade.CommodityId, out var food) || food is null ||
                !sourceCosts.TryGetValue(src, out var unitReservation) ||
                !destinationValues.TryGetValue(dst, out var valueCurve))
                throw new InvalidOperationException("Market trade uses an undeclared scenario input.");

            var allocated = allocatedAtDestinations.GetValueOrDefault(dst);
            var next = checked(allocated + trade.RawQuantity);
            var expectedMarginal = valueCurve.EvaluateMarginalRawUnitValue(allocated);
            var before = valueCurve.EvaluateIntegratedRawValue(allocated);
            var after = valueCurve.EvaluateIntegratedRawValue(next);
            if (expectedMarginal is null || before is null || after is null ||
                before.CompareTo(after) > 0)
                throw new InvalidOperationException("Destination market value curve is invalid.");

            CheckFraction(trade.RawMarginalCommodityUnitValueAtDecision, expectedMarginal);
            CheckSigned(trade.RawCommodityValue, Subtract(after, before));
            CheckFraction(trade.RawFreightCharge,
                route.Quote.ChargeForRawQuantity(
                    trade.RawQuantity, food.GramsPerFlowUnit));
            CheckFraction(trade.RawProducerReservationCost,
                new FoodTransportCostFraction(
                    (BigInteger)trade.RawQuantity * unitReservation, RawDenominator));
            CheckSigned(trade.RawNetReturn, Subtract(
                Subtract(
                    (trade.RawCommodityValue.Numerator, trade.RawCommodityValue.Denominator),
                    (trade.RawFreightCharge.Numerator, trade.RawFreightCharge.Denominator)),
                (trade.RawProducerReservationCost.Numerator,
                    trade.RawProducerReservationCost.Denominator)));
            if (trade.RawNetReturn.Numerator.Sign <= 0)
                throw new InvalidOperationException("The reference market accepted a nonpositive trade.");

            supply[src] = remainingSupply - trade.RawQuantity;
            demand[dst] = remainingDemand - trade.RawQuantity;
            allocatedAtDestinations[dst] = next;
            rawTotal += trade.RawQuantity;
            var mass = (BigInteger)trade.RawQuantity * food.GramsPerFlowUnit;
            foreach (var resource in route.PhysicalResources)
                actualLoads[resource] = actualLoads.GetValueOrDefault(resource) + mass;
        }

        var costLower = new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One);
        var costUpper = costLower;
        foreach (var (id, mass) in actualLoads.OrderBy(row => row.Key.Value))
        {
            var bounds = profiles[id].EvaluateTotalCostBounds(mass);
            costLower = AddNonnegative(costLower, bounds.Lower);
            costUpper = AddNonnegative(costUpper, bounds.Upper);
        }

        return new EconomicReclearedReferenceScenario(
            Array.AsReadOnly(trades.ToArray()),
            new ReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger>(
                actualLoads.OrderBy(row => row.Key.Value)
                    .ToDictionary(row => row.Key, row => row.Value)),
            rawTotal, costLower, costUpper);
    }

    private static T[] Snapshot<T>(IEnumerable<T> items, string param) where T : class
    {
        var rows = items.ToArray();
        if (rows.Any(item => item is null))
            throw new ArgumentException("Candidate input cannot contain null rows.", param);
        return rows;
    }

    private static FoodTransportCostFraction AddNonnegative(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static void CheckFraction(
        FoodTransportCostFraction reported, FoodTransportCostFraction expected)
    {
        if (reported.CompareTo(expected) != 0)
            throw new InvalidOperationException("Candidate market account does not reconcile.");
    }

    private static void CheckSigned(
        FoodTransportCostFraction reported, (BigInteger Numerator, BigInteger Denominator) expected)
    {
        if (expected.Numerator.Sign < 0 ||
            reported.Numerator * expected.Denominator !=
            expected.Numerator * reported.Denominator)
            throw new InvalidOperationException("Candidate market value/cost arithmetic does not reconcile.");
    }

    private static (BigInteger Numerator, BigInteger Denominator) Subtract(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        Subtract((a.Numerator, a.Denominator), (b.Numerator, b.Denominator));

    private static (BigInteger Numerator, BigInteger Denominator) Subtract(
        (BigInteger Numerator, BigInteger Denominator) a,
        (BigInteger Numerator, BigInteger Denominator) b) =>
        Normalize(a.Numerator * b.Denominator - b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static (BigInteger Numerator, BigInteger Denominator) Add(
        (BigInteger Numerator, BigInteger Denominator) a,
        (BigInteger Numerator, BigInteger Denominator) b) =>
        Normalize(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static (BigInteger Numerator, BigInteger Denominator) Normalize(
        BigInteger numerator, BigInteger denominator)
    {
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        return (numerator / gcd, denominator / gcd);
    }
}
