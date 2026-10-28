using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A named, complete, hypothetical logistics support. Route incidence and
/// physical profiles are immutable snapshots, not build orders. A route is
/// not proven accessible here: its source must be a separately audited
/// transport service (Gate D1) before any authoritative integration.
/// </summary>
public sealed class EconomicInteriorLogisticSupport
{
    public string Key { get; }
    public IReadOnlyList<EconomicInteriorPhysicalMarketIncidence> Routes { get; }
    public IReadOnlyList<EconomicDensityResourceCostProfile> Profiles { get; }

    public EconomicInteriorLogisticSupport(
        string key,
        IEnumerable<EconomicInteriorPhysicalMarketIncidence> routes,
        IEnumerable<EconomicDensityResourceCostProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(profiles);
        if (key.Length is < 1 or > 64 ||
            key.Any(c => !(c is >= 'a' and <= 'z') &&
                         !(c is >= '0' and <= '9') && c != '-'))
            throw new ArgumentException("Support needs a stable lower-case ASCII key.", nameof(key));
        var r = routes.ToArray();
        var p = profiles.ToArray();
        if (r.Any(x => x is null) || p.Any(x => x is null))
            throw new ArgumentException("Support cannot contain null route or profile entries.");
        Key = key;
        Routes = Array.AsReadOnly(r);
        Profiles = Array.AsReadOnly(p);
    }
}

/// <summary>
/// Independent arithmetic audit of one solved physical-market support.
/// No authority to approve a route, pricing policy or actual payment.
/// </summary>
public sealed class EconomicAuditedInteriorSupport
{
    public string Key { get; }
    public EconomicInteriorPhysicalMarketResult Observation { get; }
    public IReadOnlyDictionary<EconomicPointId, double> BuyerUtility { get; }
    public IReadOnlyDictionary<EconomicPointId, double> OriginReceipts { get; }
    public double MaximumIndependentResidual { get; }

    internal EconomicAuditedInteriorSupport(
        string key, EconomicInteriorPhysicalMarketResult observation,
        IReadOnlyDictionary<EconomicPointId, double> buyerUtility,
        IReadOnlyDictionary<EconomicPointId, double> originReceipts,
        double maximumResidual)
    {
        Key = key;
        Observation = observation;
        BuyerUtility = buyerUtility;
        OriginReceipts = originReceipts;
        MaximumIndependentResidual = maximumResidual;
    }
}

/// <summary>
/// Observation relative to an explicitly named baseline, NOT selection.
/// Utility includes the H opportunity cost; receipts are seller payments,
/// not producer net profit. "Nonworsening" is a descriptive indicator
/// under fixed budgets and supplies, not an institutional eligibility rule.
/// </summary>
public sealed class EconomicInteriorSupportComparison
{
    public string BaselineKey { get; }
    public string CandidateKey { get; }
    public BigInteger SignedCostLowerNumerator { get; }
    public BigInteger CostLowerDenominator { get; }
    public BigInteger SignedCostUpperNumerator { get; }
    public BigInteger CostUpperDenominator { get; }
    public IReadOnlyDictionary<EconomicPointId, double> BuyerUtilityChanges { get; }
    public IReadOnlyDictionary<EconomicPointId, double> OriginReceiptChanges { get; }

    public EconomicDensityCostDeltaClassification RealCostSign =>
        SignedCostUpperNumerator.Sign < 0
            ? EconomicDensityCostDeltaClassification.CertifiedCostDecrease
            : SignedCostLowerNumerator.Sign > 0
                ? EconomicDensityCostDeltaClassification.CertifiedCostIncrease
                : EconomicDensityCostDeltaClassification.Indeterminate;

    public bool NoBuyerUtilityLossWithinTolerance { get; }
    public bool NoOriginReceiptLossWithinTolerance { get; }

    internal EconomicInteriorSupportComparison(
        string baselineKey, string candidateKey,
        (BigInteger N, BigInteger D) lower,
        (BigInteger N, BigInteger D) upper,
        IReadOnlyDictionary<EconomicPointId, double> buyerChanges,
        IReadOnlyDictionary<EconomicPointId, double> originChanges,
        bool noBuyerLoss, bool noOriginLoss)
    {
        BaselineKey = baselineKey;
        CandidateKey = candidateKey;
        SignedCostLowerNumerator = lower.N;
        CostLowerDenominator = lower.D;
        SignedCostUpperNumerator = upper.N;
        CostUpperDenominator = upper.D;
        BuyerUtilityChanges = buyerChanges;
        OriginReceiptChanges = originChanges;
        NoBuyerUtilityLossWithinTolerance = noBuyerLoss;
        NoOriginReceiptLossWithinTolerance = noOriginLoss;
    }
}

public sealed class EconomicInteriorSupportExplorationResult
{
    public EconomicAuditedInteriorSupport Baseline { get; }
    public IReadOnlyList<EconomicAuditedInteriorSupport> AuditedSupports { get; }
    public IReadOnlyList<EconomicInteriorSupportComparison> Comparisons { get; }

    internal EconomicInteriorSupportExplorationResult(
        EconomicAuditedInteriorSupport baseline,
        EconomicAuditedInteriorSupport[] supports,
        EconomicInteriorSupportComparison[] comparisons)
    {
        Baseline = baseline;
        AuditedSupports = Array.AsReadOnly(supports);
        Comparisons = Array.AsReadOnly(comparisons);
    }
}

/// <summary>
/// Pure orchestration of INDEPENDENT fixed-support market resolutions.
/// Reconstructs masses, exact real costs, budget identities and GA-PROP-001
/// marginal conditions separately from the solver that returned the
/// candidate. No candidate is automatically preferred, authorized or
/// committed. All supports use the SAME buyer and origin snapshots.
/// </summary>
public sealed class EconomicInteriorSupportExplorer
{
    private const double Micrograms = 1_000_000.0;
    private readonly EconomicInteriorPhysicalMarketProbe _probe = new();
    private readonly EconomicFoodPreferenceEvaluator _preferences = new();

    public EconomicInteriorSupportExplorationResult Explore(
        string baselineKey,
        IEnumerable<EconomicMultivarietyOrigin> origins,
        IEnumerable<EconomicMultivarietyBuyer> buyers,
        IEnumerable<EconomicInteriorLogisticSupport> supports,
        EconomicFoodPreferenceParameters preferences)
    {
        ArgumentNullException.ThrowIfNull(baselineKey);
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(supports);
        ArgumentNullException.ThrowIfNull(preferences);
        var supply = origins.ToArray();
        var demand = buyers.ToArray();
        var possibilities = supports.ToArray();
        if (possibilities.Length < 2 || possibilities.Any(x => x is null))
            throw new ArgumentException("Compare at least two nonnull supports.", nameof(supports));
        var canonical = possibilities.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        if (canonical.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count() != canonical.Length)
            throw new ArgumentException("Support keys must be unique.", nameof(supports));
        if (!canonical.Any(x => x.Key == baselineKey))
            throw new ArgumentException("Explicit baseline key is absent.", nameof(baselineKey));

        var sortedSources = supply.OrderBy(x => x.OriginId.Value).ToArray();
        var sortedBuyers = demand.OrderBy(x => x.BuyerId.Value).ToArray();
        if (sortedSources.Any(x => x is null) || sortedBuyers.Any(x => x is null))
            throw new ArgumentException("Market snapshots cannot contain null entries.");

        // Observe each complete candidate without passing result quantities
        // from the previous support. A failed interior solution is NOT
        // silently converted into an eligible economic state.
        var audited = new List<EconomicAuditedInteriorSupport>();
        foreach (var support in canonical)
        {
            var observation = _probe.Evaluate(
                sortedSources, sortedBuyers, support.Routes,
                support.Profiles, preferences);
            audited.Add(Audit(support, sortedSources, sortedBuyers,
                preferences, observation));
        }
        var baseline = audited.Single(x => x.Key == baselineKey);
        var comparisons = audited.Where(x => x.Key != baselineKey)
            .Select(candidate => Compare(baseline, candidate)).ToArray();
        return new EconomicInteriorSupportExplorationResult(
            baseline, audited.ToArray(), comparisons);
    }

    private EconomicAuditedInteriorSupport Audit(
        EconomicInteriorLogisticSupport support,
        EconomicMultivarietyOrigin[] source,
        EconomicMultivarietyBuyer[] consumers,
        EconomicFoodPreferenceParameters parameters,
        EconomicInteriorPhysicalMarketResult result)
    {
        var market = result.Market;
        var n = source.Length;
        var m = consumers.Length;
        if (market.OriginOrder.Count != n ||
            market.Buyers.Count != m ||
            market.OriginPrices.Count != n)
            throw new InvalidOperationException("Candidate market shape differs from its immutable snapshot.");

        var routeTable = support.Routes.ToDictionary(
            x => (x.Route.Quote.RouteKey.DestinationEconomicPointId,
                x.Route.Quote.RouteKey.SourceEconomicPointId));
        var resourceTable = support.Profiles.ToDictionary(x => x.ResourceId);
        var reconstructed = new Dictionary<EconomicPhysicalResourceId, BigInteger>();
        var utility = new Dictionary<EconomicPointId, double>();
        var receipts = new Dictionary<EconomicPointId, double>();
        var revenue = 0.0;
        var freight = 0.0;
        var buyerSpending = 0.0;
        var maxResidual = 0.0;

        for (var j = 0; j < n; j++)
        {
            if (market.OriginOrder[j] != source[j].OriginId ||
                !(market.OriginPrices[j] > 0) ||
                !double.IsFinite(market.OriginPrices[j]))
                throw new InvalidOperationException("Origin identity/price mismatch.");
        }

        for (var i = 0; i < m; i++)
        {
            var consumer = consumers[i];
            var buyer = market.Buyers[i];
            if (buyer.BuyerId != consumer.BuyerId ||
                buyer.QuantitiesByOrigin.Count != n ||
                buyer.DeliveredPricesByOrigin.Count != n ||
                buyer.MarginalWillingnessByOrigin.Count != n ||
                !double.IsFinite(buyer.MonetaryResidual) ||
                buyer.MonetaryResidual <= 0)
                throw new InvalidOperationException("Buyer identity, dimensionality or H is invalid.");

            var quantities = buyer.QuantitiesByOrigin.ToArray();
            var recomputedPreference = _preferences.Evaluate(
                consumer.Population, buyer.MonetaryResidual,
                quantities, parameters);
            var spent = 0.0;
            for (var j = 0; j < n; j++)
            {
                var origin = source[j];
                var q = quantities[j];
                var delivered = buyer.DeliveredPricesByOrigin[j];
                var p = market.OriginPrices[j];
                if (!double.IsFinite(q) || q <= 0 ||
                    !double.IsFinite(delivered) || delivered <= 0)
                    throw new InvalidOperationException("Non-interior buyer/price in supposed interior market.");
                var route = routeTable[(consumer.BuyerId, origin.OriginId)];
                var tariff = delivered - p;
                if (tariff < -1e-9)
                    throw new InvalidOperationException("Buyer has negative implied freight charge.");
                spent += q * delivered;
                revenue += q * p;
                freight += q * tariff;
                maxResidual = Math.Max(maxResidual,
                    Math.Abs(recomputedPreference.MarginalWillingnessToPay[j] - delivered));
                if (Math.Abs(buyer.MarginalWillingnessByOrigin[j] -
                    recomputedPreference.MarginalWillingnessToPay[j]) >
                    1e-7 * Math.Max(1.0, Math.Abs(delivered)))
                    throw new InvalidOperationException("Returned marginal value does not match canonical utility.");

                if (route.Route.PhysicalResources.Count == 0) continue;
                var rawMass = q * route.GramsPerWholeUnit * Micrograms;
                if (!double.IsFinite(rawMass) ||
                    rawMass <= 0.5 || rawMass >= 9.0e15)
                    throw new InvalidOperationException("Unsafe or unrepresentable physical mass.");
                var mass = new BigInteger(Math.Round(rawMass, MidpointRounding.ToEven));
                foreach (var id in route.Route.PhysicalResources)
                    reconstructed[id] = reconstructed.GetValueOrDefault(id, BigInteger.Zero) + mass;
            }
            maxResidual = Math.Max(maxResidual,
                Math.Abs(buyer.MonetaryResidual + spent - consumer.Budget));
            maxResidual = Math.Max(maxResidual,
                Math.Abs(buyer.Utility - recomputedPreference.Utility));
            utility.Add(consumer.BuyerId, recomputedPreference.Utility);
            buyerSpending += spent;
        }

        for (var j = 0; j < n; j++)
        {
            var volume = 0.0;
            for (var i = 0; i < m; i++)
                volume += market.Buyers[i].QuantitiesByOrigin[j];
            maxResidual = Math.Max(maxResidual,
                Math.Abs(volume - source[j].AvailableQuantity));
            receipts.Add(source[j].OriginId, volume * market.OriginPrices[j]);
        }

        if (result.ResourceLoadsMicrograms.Count != reconstructed.Count ||
            reconstructed.Any(x =>
                !result.ResourceLoadsMicrograms.TryGetValue(x.Key, out var actual) ||
                actual != x.Value))
            throw new InvalidOperationException("Physical resource loads do not reconcile.");

        var realLower = new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One);
        var realUpper = realLower;
        foreach (var (id, physicalMass) in reconstructed.OrderBy(x => x.Key.Value))
        {
            if (!resourceTable.TryGetValue(id, out var profile))
                throw new InvalidOperationException("A physical cost resource lacks a profile.");
            var bounds = profile.EvaluateTotalCostBounds(physicalMass);
            realLower = Add(realLower, bounds.Lower);
            realUpper = Add(realUpper, bounds.Upper);
        }
        if (realLower.CompareTo(result.RealResourceCostLower) != 0 ||
            realUpper.CompareTo(result.RealResourceCostUpper) != 0)
            throw new InvalidOperationException("Aggregate physical real cost does not reconcile.");

        maxResidual = Math.Max(maxResidual,
            Math.Abs(revenue - market.SellerReceipts));
        maxResidual = Math.Max(maxResidual,
            Math.Abs(freight - market.FreightReceipts));
        maxResidual = Math.Max(maxResidual,
            Math.Abs(buyerSpending - market.BuyerExpenditure));
        maxResidual = Math.Max(maxResidual,
            Math.Abs(buyerSpending - revenue - freight));
        if (!double.IsFinite(maxResidual) || maxResidual >
            1.0e-7 * Math.Max(1.0, buyerSpending))
            throw new InvalidOperationException("Independent budget/price/supply/cost audit failed.");

        return new EconomicAuditedInteriorSupport(
            support.Key, result,
            new ReadOnlyDictionary<EconomicPointId, double>(utility),
            new ReadOnlyDictionary<EconomicPointId, double>(receipts),
            maxResidual);
    }

    private static EconomicInteriorSupportComparison Compare(
        EconomicAuditedInteriorSupport baseline,
        EconomicAuditedInteriorSupport candidate)
    {
        var b = baseline.Observation;
        var a = candidate.Observation;
        var lower = Subtract(
            a.RealResourceCostLower, b.RealResourceCostUpper);
        var upper = Subtract(
            a.RealResourceCostUpper, b.RealResourceCostLower);
        if (lower.N * upper.D > upper.N * lower.D)
            throw new InvalidOperationException("Invalid resource-cost interval.");

        var utility = new SortedDictionary<EconomicPointId, double>(
            Comparer<EconomicPointId>.Create(
                (x, y) => x.Value.CompareTo(y.Value)));
        var receipts = new SortedDictionary<EconomicPointId, double>(
            Comparer<EconomicPointId>.Create(
                (x, y) => x.Value.CompareTo(y.Value)));
        var noUtilityLoss = true;
        var noSellerLoss = true;
        foreach (var (id, value) in baseline.BuyerUtility)
        {
            var after = candidate.BuyerUtility[id];
            var difference = after - value;
            utility.Add(id, difference);
            if (difference < -1e-8 * Math.Max(1.0, Math.Abs(value)))
                noUtilityLoss = false;
        }
        foreach (var (id, value) in baseline.OriginReceipts)
        {
            var after = candidate.OriginReceipts[id];
            var difference = after - value;
            receipts.Add(id, difference);
            if (difference < -1e-8 * Math.Max(1.0, Math.Abs(value)))
                noSellerLoss = false;
        }
        return new EconomicInteriorSupportComparison(
            baseline.Key, candidate.Key, lower, upper,
            new ReadOnlyDictionary<EconomicPointId, double>(utility),
            new ReadOnlyDictionary<EconomicPointId, double>(receipts),
            noUtilityLoss, noSellerLoss);
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static (BigInteger N, BigInteger D) Subtract(
        FoodTransportCostFraction a, FoodTransportCostFraction b)
    {
        var n = a.Numerator * b.Denominator -
            b.Numerator * a.Denominator;
        var d = a.Denominator * b.Denominator;
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        return (n / gcd, d / gcd);
    }
}
