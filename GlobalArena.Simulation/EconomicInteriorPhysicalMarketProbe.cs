using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A declared buyer/origin OD incidence on an existing hypothetical route.
/// The raw EconomicTurnFreightQuote is NOT silently converted to GA-PROP-001
/// numeraire: initialFreightPerWholeUnit is an explicit same-unit seed.
/// The route must be independently certified by Gate D1 before any future
/// authoritative use; this reference cannot prove geographic reachability.
/// </summary>
public sealed class EconomicInteriorPhysicalMarketIncidence
{
    public EconomicReclearingQuotedRoute Route { get; }
    public ulong GramsPerWholeUnit { get; }
    public double InitialFreightPerWholeUnit { get; }

    public EconomicInteriorPhysicalMarketIncidence(
        EconomicReclearingQuotedRoute route, ulong gramsPerWholeUnit,
        double initialFreightPerWholeUnit)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (gramsPerWholeUnit == 0 ||
            !double.IsFinite(initialFreightPerWholeUnit) ||
            initialFreightPerWholeUnit < 0)
            throw new ArgumentOutOfRangeException(nameof(gramsPerWholeUnit));
        Route = route;
        GramsPerWholeUnit = gramsPerWholeUnit;
        InitialFreightPerWholeUnit = initialFreightPerWholeUnit;
    }
}

/// <summary>
/// A finite, stationary-reference observation, not a tariff command or
/// a production, inventory, freight or financial settlement.
/// </summary>
public sealed class EconomicInteriorPhysicalMarketResult
{
    public EconomicMultivarietyInteriorClearingResult Market { get; }
    public IReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger> ResourceLoadsMicrograms { get; }
    public FoodTransportCostFraction RealResourceCostLower { get; }
    public FoodTransportCostFraction RealResourceCostUpper { get; }
    public double MaximumQuotedVsImpliedFareResidual { get; }
    public double FreightRevenueMinusRealCostMidpoint { get; }
    public int OuterIterations { get; }

    internal EconomicInteriorPhysicalMarketResult(
        EconomicMultivarietyInteriorClearingResult market,
        IReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger> loads,
        FoodTransportCostFraction costLower,
        FoodTransportCostFraction costUpper,
        double residual,
        double accountingDifference,
        int iterations)
    {
        Market = market;
        ResourceLoadsMicrograms = loads;
        RealResourceCostLower = costLower;
        RealResourceCostUpper = costUpper;
        MaximumQuotedVsImpliedFareResidual = residual;
        FreightRevenueMinusRealCostMidpoint = accountingDifference;
        OuterIterations = iterations;
    }
}

/// <summary>
/// Hypothetical fixed-support freight/market feedback:
///     frozen OD fares -> multivariety endogenous prices/quantities ->
///     physical microgram loads -> shared resource costs ->
///     per-resource average-cost fares -> repeat.
///
/// Average-cost recovery is an EXPLICIT synthetic policy, not a historical
/// institution, financial permission, nor a claim that commercial freight
/// normally equals operating cost. A positive load pays C_r(L)/L once for
/// each resource it uses. No free price at a cold zero-load resource is
/// inferred: a strictly interior support is required.
///
/// This is a bounded local fixed-point PROBE, not an equilibrium existence,
/// uniqueness or welfare proof. Damping, initial fares, corner constraints,
/// convergence failures and route validity remain visible.
/// </summary>
public sealed class EconomicInteriorPhysicalMarketProbe
{
    private const double MicrogramsPerGram = 1_000_000.0;
    private const double MaximumPreciselyRepresentableMass = 9.0e15;
    private const int MaximumOuterIterations = 160;
    private const double FareTolerance = 2.0e-8;
    private const double Damping = 0.35;
    private readonly EconomicMultivarietyInteriorClearing _market = new();

    public EconomicInteriorPhysicalMarketResult Evaluate(
        IEnumerable<EconomicMultivarietyOrigin> origins,
        IEnumerable<EconomicMultivarietyBuyer> buyers,
        IEnumerable<EconomicInteriorPhysicalMarketIncidence> incidences,
        IEnumerable<EconomicDensityResourceCostProfile> profiles,
        EconomicFoodPreferenceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(incidences);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(parameters);

        var sortedOrigins = origins.ToArray();
        var sortedBuyers = buyers.ToArray();
        if (sortedOrigins.Length < 2 || sortedBuyers.Length == 0 ||
            sortedOrigins.Any(x => x is null) || sortedBuyers.Any(x => x is null))
            throw new ArgumentException("Physical market requires at least two origins and one buyer.");
        sortedOrigins = sortedOrigins.OrderBy(o => o.OriginId.Value).ToArray();
        sortedBuyers = sortedBuyers.OrderBy(b => b.BuyerId.Value).ToArray();
        if (sortedOrigins.Select(o => o.OriginId).Distinct().Count() != sortedOrigins.Length ||
            sortedBuyers.Select(b => b.BuyerId).Distinct().Count() != sortedBuyers.Length)
            throw new ArgumentException("Duplicate market identity.");

        var declared = incidences.ToArray();
        if (declared.Any(x => x is null))
            throw new ArgumentException("Null physical route.", nameof(incidences));
        var byOd = new Dictionary<(EconomicPointId Buyer, EconomicPointId Origin),
            EconomicInteriorPhysicalMarketIncidence>();
        foreach (var incidence in declared)
        {
            var key = incidence.Route.Quote.RouteKey;
            if (!byOd.TryAdd((key.DestinationEconomicPointId, key.SourceEconomicPointId),
                    incidence))
                throw new ArgumentException("Duplicate declared physical OD.", nameof(incidences));
        }

        var costProfiles = new Dictionary<EconomicPhysicalResourceId, EconomicDensityResourceCostProfile>();
        foreach (var profile in profiles)
            if (profile is null || !costProfiles.TryAdd(profile.ResourceId, profile))
                throw new ArgumentException("Null or duplicate cost resource.", nameof(profiles));

        var m = sortedBuyers.Length;
        var n = sortedOrigins.Length;
        var incidenceMatrix = new EconomicInteriorPhysicalMarketIncidence[m, n];
        var currentFares = new double[m, n];
        if (declared.Length != m * n)
            throw new ArgumentException("Exactly one OD incidence is required for every buyer/origin pair.",
                nameof(incidences));
        for (var i = 0; i < m; i++)
            for (var j = 0; j < n; j++)
            {
                var key = (sortedBuyers[i].BuyerId, sortedOrigins[j].OriginId);
                if (!byOd.TryGetValue(key, out var incidence))
                    throw new ArgumentException("Missing buyer/origin physical route.", nameof(incidences));
                foreach (var id in incidence.Route.PhysicalResources)
                    if (!costProfiles.ContainsKey(id))
                        throw new ArgumentException("Physical route uses an undeclared real-cost resource.",
                            nameof(profiles));
                incidenceMatrix[i, j] = incidence;
                currentFares[i, j] = incidence.InitialFreightPerWholeUnit;
            }

        for (var iteration = 0; iteration < MaximumOuterIterations; iteration++)
        {
            var quotes = new EconomicMultivarietyDeliveredFreight[m * n];
            for (var i = 0; i < m; i++)
                for (var j = 0; j < n; j++)
                    quotes[i * n + j] = new EconomicMultivarietyDeliveredFreight(
                        sortedBuyers[i].BuyerId, sortedOrigins[j].OriginId,
                        currentFares[i, j]);

            // The existing E2 solver independently audits every local
            // interior stationary candidate and fails closed at corners.
            var market = _market.Resolve(sortedOrigins, sortedBuyers, quotes, parameters);
            var physicalLoads = new Dictionary<EconomicPhysicalResourceId, BigInteger>();
            for (var i = 0; i < m; i++)
                for (var j = 0; j < n; j++)
                {
                    var route = incidenceMatrix[i, j];
                    if (route.Route.PhysicalResources.Count == 0)
                        continue;
                    var q = market.Buyers[i].QuantitiesByOrigin[j];
                    var rawMass = q * route.GramsPerWholeUnit * MicrogramsPerGram;
                    if (!double.IsFinite(rawMass) ||
                        rawMass <= 0.5 || rawMass >= MaximumPreciselyRepresentableMass)
                        throw new InvalidOperationException(
                            "Strict interior physical mass exceeds safe deterministic precision.");
                    var grams = new BigInteger(Math.Round(rawMass, MidpointRounding.ToEven));
                    if (grams.Sign <= 0)
                        throw new InvalidOperationException("Positive traded mass rounded to zero.");
                    foreach (var id in route.Route.PhysicalResources)
                        physicalLoads[id] = physicalLoads.GetValueOrDefault(id, BigInteger.Zero) + grams;
                }

            var costLower = new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One);
            var costUpper = costLower;
            var averageByResource = new Dictionary<EconomicPhysicalResourceId, double>();
            foreach (var (id, mass) in physicalLoads.OrderBy(x => x.Key.Value))
            {
                var bound = costProfiles[id].EvaluateTotalCostBounds(mass);
                costLower = Add(costLower, bound.Lower);
                costUpper = Add(costUpper, bound.Upper);
                var lower = ToFiniteDouble(bound.Lower);
                var upper = ToFiniteDouble(bound.Upper);
                var average = (lower / 2.0 + upper / 2.0) /
                    ((double)mass / MicrogramsPerGram);
                if (!double.IsFinite(average) || average < 0)
                    throw new InvalidOperationException(
                        "Real resource average cost is outside the supported numerical range.");
                averageByResource.Add(id, average);
            }

            var implied = new double[m, n];
            var maxDifference = 0.0;
            var maximumFare = 0.0;
            for (var i = 0; i < m; i++)
                for (var j = 0; j < n; j++)
                {
                    var route = incidenceMatrix[i, j];
                    var rate = 0.0;
                    foreach (var id in route.Route.PhysicalResources)
                        rate += averageByResource[id];
                    implied[i, j] = route.GramsPerWholeUnit * rate;
                    if (!double.IsFinite(implied[i, j]))
                        throw new InvalidOperationException("Implied freight quote overflow.");
                    maxDifference = Math.Max(maxDifference,
                        Math.Abs(implied[i, j] - currentFares[i, j]));
                    maximumFare = Math.Max(maximumFare, implied[i, j]);
                }

            if (maxDifference <= FareTolerance * Math.Max(1.0, maximumFare))
            {
                var lower = ToFiniteDouble(costLower);
                var upper = ToFiniteDouble(costUpper);
                var costMidpoint = lower / 2.0 + upper / 2.0;
                var accountingResidual = market.FreightReceipts - costMidpoint;
                // These are floating reference accounting units, and q-to-
                // microgram conversion is rounded. It is NOT exact settlement.
                if (!double.IsFinite(accountingResidual) ||
                    Math.Abs(accountingResidual) >
                    2.0e-6 * Math.Max(1.0, Math.Abs(costMidpoint)))
                    throw new InvalidOperationException(
                        "Average-cost recovery failed real-resource cost reconciliation.");

                return new EconomicInteriorPhysicalMarketResult(
                    market,
                    new ReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger>(
                        physicalLoads.OrderBy(x => x.Key.Value)
                            .ToDictionary(x => x.Key, x => x.Value)),
                    costLower, costUpper, maxDifference,
                    accountingResidual, iteration + 1);
            }

            // Only hypothetical prices are updated. No turn-locked quote
            // object, physical asset, inventory or money is ever mutated.
            for (var i = 0; i < m; i++)
                for (var j = 0; j < n; j++)
                    currentFares[i, j] =
                        (1.0 - Damping) * currentFares[i, j] +
                        Damping * implied[i, j];
        }
        throw new InvalidOperationException(
            "Physical freight and endogenous-origin-price reference did not converge.");
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction left, FoodTransportCostFraction right) =>
        new(left.Numerator * right.Denominator + right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);

    private static double ToFiniteDouble(FoodTransportCostFraction fraction)
    {
        var value = (double)fraction.Numerator / (double)fraction.Denominator;
        if (!double.IsFinite(value) || value < 0)
            throw new InvalidOperationException("Physical cost exceeds reference floating precision.");
        return value;
    }
}
