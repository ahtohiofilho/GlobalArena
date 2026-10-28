using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// ONE available, frozen, caller-quoted service for a single origin variety.
/// This does not prove construction, geographic access or a tariff institution:
/// such evidence must come from the already established fine-service gate.
/// </summary>
public sealed class EconomicCornerQuotedService
{
    public EconomicReclearingQuotedRoute Route { get; }
    public double FreightPerWholeUnit { get; }

    public EconomicCornerQuotedService(
        EconomicReclearingQuotedRoute route, double freightPerWholeUnit)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!double.IsFinite(freightPerWholeUnit) || freightPerWholeUnit < 0)
            throw new ArgumentOutOfRangeException(nameof(freightPerWholeUnit));
        Route = route;
        FreightPerWholeUnit = freightPerWholeUnit;
    }
}

/// <summary>
/// One complete declared service support. A buyer omitted from Services is
/// UNCONNECTED, not silently quoted as a free route; its demand is zero.
/// A connected buyer can independently choose zero consumption.
/// </summary>
public sealed class EconomicSingleVarietyCornerSupport
{
    public string Key { get; }
    public IReadOnlyList<EconomicCornerQuotedService> Services { get; }

    public EconomicSingleVarietyCornerSupport(
        string key, IEnumerable<EconomicCornerQuotedService> services)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(services);
        if (key.Length is < 1 or > 64 ||
            key.Any(c => !(c is >= 'a' and <= 'z') &&
                         !(c is >= '0' and <= '9') && c != '-'))
            throw new ArgumentException("Support requires a stable lowercase key.", nameof(key));
        var snapshot = services.ToArray();
        if (snapshot.Any(x => x is null))
            throw new ArgumentException("Support services cannot contain null entries.",
                nameof(services));
        Key = key;
        Services = Array.AsReadOnly(snapshot);
    }
}

public sealed class EconomicCornerBuyerObservation
{
    public EconomicPointId BuyerId { get; }
    public bool IsConnected { get; }
    public double Quantity { get; }
    public double MonetaryResidual { get; }
    public double DeliveredPrice { get; }
    public double Utility { get; }

    internal EconomicCornerBuyerObservation(
        EconomicPointId id, bool connected,
        double quantity, double residual, double price, double utility)
    {
        BuyerId = id;
        IsConnected = connected;
        Quantity = quantity;
        MonetaryResidual = residual;
        DeliveredPrice = price;
        Utility = utility;
    }
}

public sealed class EconomicAuditedCornerSupport
{
    public string Key { get; }
    public double OriginPrice { get; }
    public double Sold { get; }
    public double Unsold { get; }
    public double SellerReceipts { get; }
    public double FreightReceipts { get; }
    public IReadOnlyList<EconomicCornerBuyerObservation> Buyers { get; }
    public IReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger> LoadsMicrograms { get; }
    public FoodTransportCostFraction RealCostLower { get; }
    public FoodTransportCostFraction RealCostUpper { get; }

    internal EconomicAuditedCornerSupport(
        string key, double price, double sold, double unsold,
        double sellerReceipts, double freightReceipts,
        EconomicCornerBuyerObservation[] buyers,
        Dictionary<EconomicPhysicalResourceId, BigInteger> loads,
        FoodTransportCostFraction costLower, FoodTransportCostFraction costUpper)
    {
        Key = key;
        OriginPrice = price;
        Sold = sold;
        Unsold = unsold;
        SellerReceipts = sellerReceipts;
        FreightReceipts = freightReceipts;
        Buyers = Array.AsReadOnly(buyers);
        LoadsMicrograms = new ReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger>(
            loads.OrderBy(x => x.Key.Value).ToDictionary(x => x.Key, x => x.Value));
        RealCostLower = costLower;
        RealCostUpper = costUpper;
    }
}

/// <summary>
/// Realized opening of an initially DISCONNECTED buyer onto a previously
/// absent service. Finite batch is a SCREEN, not a command to buy or a
/// certificate that a cold route is economically preferable.
/// </summary>
public sealed class EconomicCornerOpeningObservation
{
    public EconomicAuditedCornerSupport Baseline { get; }
    public EconomicAuditedCornerSupport Candidate { get; }
    public EconomicPointId OpeningBuyerId { get; }
    public BigInteger ObservedOpeningLoadMicrograms { get; }
    public BigInteger RequestedMinimumBatchMicrograms { get; }
    public bool ObservedBatchMeetsMinimum =>
        ObservedOpeningLoadMicrograms >= RequestedMinimumBatchMicrograms;
    public IReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger> NewlyLoadedResources { get; }
    public IReadOnlyDictionary<EconomicPointId, double> BuyerUtilityChanges { get; }
    public double OriginReceiptChange => Candidate.SellerReceipts - Baseline.SellerReceipts;
    public BigInteger SignedRealCostLowerNumerator { get; }
    public BigInteger SignedRealCostLowerDenominator { get; }
    public BigInteger SignedRealCostUpperNumerator { get; }
    public BigInteger SignedRealCostUpperDenominator { get; }

    public EconomicDensityCostDeltaClassification RealCostSign =>
        SignedRealCostUpperNumerator.Sign < 0
            ? EconomicDensityCostDeltaClassification.CertifiedCostDecrease
            : SignedRealCostLowerNumerator.Sign > 0
                ? EconomicDensityCostDeltaClassification.CertifiedCostIncrease
                : EconomicDensityCostDeltaClassification.Indeterminate;

    internal EconomicCornerOpeningObservation(
        EconomicAuditedCornerSupport baseline,
        EconomicAuditedCornerSupport candidate,
        EconomicPointId openingBuyerId, BigInteger realized, BigInteger minimum,
        Dictionary<EconomicPhysicalResourceId, BigInteger> newlyLoaded,
        Dictionary<EconomicPointId, double> changes,
        (BigInteger N, BigInteger D) lower,
        (BigInteger N, BigInteger D) upper)
    {
        Baseline = baseline;
        Candidate = candidate;
        OpeningBuyerId = openingBuyerId;
        ObservedOpeningLoadMicrograms = realized;
        RequestedMinimumBatchMicrograms = minimum;
        NewlyLoadedResources = new ReadOnlyDictionary<EconomicPhysicalResourceId, BigInteger>(
            newlyLoaded.OrderBy(x => x.Key.Value).ToDictionary(x => x.Key, x => x.Value));
        BuyerUtilityChanges = new ReadOnlyDictionary<EconomicPointId, double>(
            changes.OrderBy(x => x.Key.Value).ToDictionary(x => x.Key, x => x.Value));
        SignedRealCostLowerNumerator = lower.N;
        SignedRealCostLowerDenominator = lower.D;
        SignedRealCostUpperNumerator = upper.N;
        SignedRealCostUpperDenominator = upper.D;
    }
}

/// <summary>
/// Complements Gate E4 for a very specific CORNER: one origin variety, some
/// buyers not connected at baseline, possible zero purchases and unsold
/// supply. Calls the already-tested n=1 endogenous-price solver independently
/// for each service support and reconstructs the results from scratch.
/// No arbitrary derivative at zero for density costs; physical costs are
/// evaluated on actual FINITE loads with rational bounds.
///
/// No all-variety complementarity, no route discovery, no freight fixed
/// point, no approval, no settlement and no financial credit.
/// </summary>
public sealed class EconomicSingleVarietyCornerSupportProbe
{
    private const double MicrogramsPerGram = 1_000_000.0;
    private const double MaximumSafeMicrograms = 9.0e15;
    private readonly EconomicSingleVarietyOriginClearing _market = new();
    private readonly EconomicFoodPreferenceEvaluator _preferences = new();

    public EconomicCornerOpeningObservation CompareOpening(
        ulong turnOrdinal,
        EconomicPointId originId,
        double supply,
        IEnumerable<EconomicMultivarietyBuyer> buyers,
        IEnumerable<EconomicDensityResourceCostProfile> profiles,
        ulong gramsPerWholeUnit,
        EconomicSingleVarietyCornerSupport baseline,
        EconomicSingleVarietyCornerSupport candidate,
        EconomicPointId openingBuyerId,
        BigInteger minimumBatchMicrograms,
        EconomicFoodPreferenceParameters preferences)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        if (!originId.IsValid || !openingBuyerId.IsValid ||
            !double.IsFinite(supply) || supply <= 0)
            throw new ArgumentException("Invalid origin, buyer or positive supply.");
        if (gramsPerWholeUnit == 0)
            throw new ArgumentOutOfRangeException(nameof(gramsPerWholeUnit));
        if (minimumBatchMicrograms <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumBatchMicrograms));
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(preferences);
        if (baseline.Key == candidate.Key)
            throw new ArgumentException("Compared supports must have distinct keys.");

        var population = buyers.ToArray();
        if (population.Length == 0 || population.Any(x => x is null))
            throw new ArgumentException("Buyer snapshot cannot be empty or contain null.", nameof(buyers));
        population = population.OrderBy(x => x.BuyerId.Value).ToArray();
        if (population.Select(x => x.BuyerId).Distinct().Count() != population.Length ||
            !population.Any(x => x.BuyerId == openingBuyerId))
            throw new ArgumentException("Buyer identities must be unique and include the opening buyer.");

        var byResource = new Dictionary<EconomicPhysicalResourceId, EconomicDensityResourceCostProfile>();
        foreach (var profile in profiles)
            if (profile is null || !byResource.TryAdd(profile.ResourceId, profile))
                throw new ArgumentException("Physical profiles must be distinct and non-null.",
                    nameof(profiles));

        Dictionary<EconomicPointId, EconomicCornerQuotedService> Services(
            EconomicSingleVarietyCornerSupport support)
        {
            var table = new Dictionary<EconomicPointId, EconomicCornerQuotedService>();
            foreach (var service in support.Services)
            {
                var route = service.Route.Quote;
                var key = route.RouteKey;
                if (route.TurnOrdinal != turnOrdinal ||
                    key.SourceEconomicPointId != originId ||
                    !population.Any(x => x.BuyerId == key.DestinationEconomicPointId) ||
                    !table.TryAdd(key.DestinationEconomicPointId, service))
                    throw new ArgumentException("Invalid, stale, unknown or duplicate service in support.");
                if (service.Route.PhysicalResources.Count == 0 &&
                    key.SourceEconomicPointId != key.DestinationEconomicPointId)
                    throw new ArgumentException("Nonlocal route cannot have a free empty physical path.");
                foreach (var resource in service.Route.PhysicalResources)
                    if (!byResource.ContainsKey(resource))
                        throw new ArgumentException("A physical resource has no cost profile.");
            }
            if (table.Count == 0)
                throw new ArgumentException("At least one connected buyer is required per support.");
            return table;
        }

        var former = Services(baseline);
        var opening = Services(candidate);
        if (former.ContainsKey(openingBuyerId) ||
            !opening.TryGetValue(openingBuyerId, out var newService) ||
            newService.Route.PhysicalResources.Count == 0)
            throw new ArgumentException(
                "Opening target must be disconnected at baseline and have a physical candidate route.");

        EconomicAuditedCornerSupport Resolve(
            EconomicSingleVarietyCornerSupport support,
            Dictionary<EconomicPointId, EconomicCornerQuotedService> table)
        {
            var connected = population
                .Where(x => table.ContainsKey(x.BuyerId))
                .Select(x => new EconomicSingleVarietyBuyer(
                    x.BuyerId, x.Population, x.Budget,
                    table[x.BuyerId].FreightPerWholeUnit)).ToArray();
            var result = _market.Resolve(supply, connected, preferences);
            var byBuyer = result.Buyers.ToDictionary(x => x.PointId);
            var account = new EconomicCornerBuyerObservation[population.Length];
            var loads = new Dictionary<EconomicPhysicalResourceId, BigInteger>();
            var revenue = 0.0;
            var freight = 0.0;
            var consumed = 0.0;
            for (var i = 0; i < population.Length; i++)
            {
                var person = population[i];
                var attached = table.TryGetValue(person.BuyerId, out var service);
                var q = attached ? byBuyer[person.BuyerId].PurchasedQuantity : 0.0;
                var delivered = attached ? result.OriginPrice + service!.FreightPerWholeUnit : 0.0;
                var h = person.Budget - q * delivered;
                if (!double.IsFinite(q) || q < 0 || !(h > 0) ||
                    !double.IsFinite(delivered))
                    throw new InvalidOperationException("Corner clearing violates goods or positive H.");
                var pref = _preferences.Evaluate(
                    person.Population, h, new[] { q }, preferences);
                if (attached)
                {
                    var purchase = byBuyer[person.BuyerId];
                    if (Math.Abs(purchase.MonetaryResidual - h) > 1e-8 ||
                        Math.Abs(purchase.Utility - pref.Utility) > 1e-8 ||
                        Math.Abs(purchase.DeliveredPrice - delivered) > 1e-8)
                        throw new InvalidOperationException("Independent consumer account mismatch.");
                    var wtp = pref.MarginalWillingnessToPay[0];
                    if (q == 0 && wtp > delivered + 1e-7)
                        throw new InvalidOperationException("Positive demand concealed at zero purchase.");
                    if (q > 1e-8 && q < supply - 1e-8 &&
                        Math.Abs(wtp - delivered) > 1e-6)
                        throw new InvalidOperationException("Interior buyer marginal condition fails.");
                }
                account[i] = new EconomicCornerBuyerObservation(
                    person.BuyerId, attached, q, h, delivered, pref.Utility);
                revenue += q * result.OriginPrice;
                if (attached)
                    freight += q * service!.FreightPerWholeUnit;
                consumed += q;
                if (!attached || q == 0)
                    continue;
                var micrograms = q * gramsPerWholeUnit * MicrogramsPerGram;
                if (!double.IsFinite(micrograms) ||
                    micrograms <= 0.5 || micrograms >= MaximumSafeMicrograms)
                    throw new InvalidOperationException(
                        "Unrepresentable positive physical purchase mass.");
                var mass = new BigInteger(Math.Round(micrograms, MidpointRounding.ToEven));
                if (mass <= 0)
                    throw new InvalidOperationException("Positive load rounded to zero.");
                foreach (var id in service!.Route.PhysicalResources)
                    loads[id] = loads.GetValueOrDefault(id) + mass;
            }

            var goodsTolerance = 1e-8 * Math.Max(1.0, supply);
            if (consumed > supply + goodsTolerance ||
                Math.Abs(consumed - result.Sold) > goodsTolerance ||
                (result.OriginPrice > 0 && Math.Abs(consumed - supply) > goodsTolerance) ||
                Math.Abs(revenue - result.SellerReceipts) > 1e-7 ||
                Math.Abs(freight - result.FreightReceipts) > 1e-7 ||
                Math.Abs(revenue + freight - result.TotalBuyerExpenditure) > 1e-7)
                throw new InvalidOperationException("Independent corner market accounts do not reconcile.");

            var lower = new FoodTransportCostFraction(BigInteger.Zero, BigInteger.One);
            var upper = lower;
            foreach (var (id, mass) in loads.OrderBy(x => x.Key.Value))
            {
                var cost = byResource[id].EvaluateTotalCostBounds(mass);
                lower = Add(lower, cost.Lower);
                upper = Add(upper, cost.Upper);
            }
            return new EconomicAuditedCornerSupport(
                support.Key, result.OriginPrice, result.Sold, result.Unsold,
                revenue, freight, account, loads, lower, upper);
        }

        var before = Resolve(baseline, former);
        var after = Resolve(candidate, opening);
        var newBuyer = after.Buyers.Single(x => x.BuyerId == openingBuyerId);
        var openingMass = new BigInteger(Math.Round(
            newBuyer.Quantity * gramsPerWholeUnit * MicrogramsPerGram,
            MidpointRounding.ToEven));
        if (openingMass.Sign < 0)
            throw new InvalidOperationException("Negative opening load.");

        var newlyLoaded = new Dictionary<EconomicPhysicalResourceId, BigInteger>();
        foreach (var (id, mass) in after.LoadsMicrograms)
            if (!before.LoadsMicrograms.TryGetValue(id, out var old) || old.IsZero)
                newlyLoaded[id] = mass;

        var differences = new Dictionary<EconomicPointId, double>();
        for (var i = 0; i < population.Length; i++)
            differences.Add(population[i].BuyerId,
                after.Buyers[i].Utility - before.Buyers[i].Utility);

        // Unchanged shared resources CANCEL exactly, even if each cost
        // interval encloses an irrational root.
        var low = (N: BigInteger.Zero, D: BigInteger.One);
        var high = low;
        foreach (var profile in byResource.Values.OrderBy(x => x.ResourceId.Value))
        {
            var old = before.LoadsMicrograms.GetValueOrDefault(profile.ResourceId);
            var next = after.LoadsMicrograms.GetValueOrDefault(profile.ResourceId);
            if (old == next)
                continue;
            var a = profile.EvaluateTotalCostBounds(old);
            var b = profile.EvaluateTotalCostBounds(next);
            low = Sum(low, Difference(b.Lower, a.Upper));
            high = Sum(high, Difference(b.Upper, a.Lower));
        }
        if (low.N * high.D > high.N * low.D)
            throw new InvalidOperationException("Invalid signed finite-batch real-cost interval.");

        return new EconomicCornerOpeningObservation(
            before, after, openingBuyerId, openingMass,
            minimumBatchMicrograms, newlyLoaded, differences, low, high);
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static (BigInteger N, BigInteger D) Difference(
        FoodTransportCostFraction after, FoodTransportCostFraction before) =>
        Canonical(after.Numerator * before.Denominator -
            before.Numerator * after.Denominator,
            after.Denominator * before.Denominator);

    private static (BigInteger N, BigInteger D) Sum(
        (BigInteger N, BigInteger D) a, (BigInteger N, BigInteger D) b) =>
        Canonical(a.N * b.D + b.N * a.D, a.D * b.D);

    private static (BigInteger N, BigInteger D) Canonical(
        BigInteger n, BigInteger d)
    {
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        return (n / gcd, d / gcd);
    }
}
