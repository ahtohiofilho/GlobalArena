using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// An exact dimensionless relative-price/revenue INDEX, not an amount of
/// preallocated cash or a buyer's monetary purchasing budget.
/// </summary>
public readonly record struct CanonicalEndogenousPriceIndex(
    BigInteger Numerator, BigInteger Denominator) :
    IComparable<CanonicalEndogenousPriceIndex>
{
    public int CompareTo(CanonicalEndogenousPriceIndex other)
    {
        if (Denominator <= 0 || other.Denominator <= 0)
            throw new InvalidOperationException("Economic index denominator must be positive.");
        return (Numerator * other.Denominator).CompareTo(
            other.Numerator * Denominator);
    }

    public static CanonicalEndogenousPriceIndex Zero => new(0, 1);
    public static CanonicalEndogenousPriceIndex Add(
        CanonicalEndogenousPriceIndex a, CanonicalEndogenousPriceIndex b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    public CanonicalEndogenousPriceIndex Times(BigInteger n) =>
        new(Numerator * n, Denominator);

    public CanonicalEndogenousPriceIndex DividedBy(BigInteger n)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        return new(Numerator, Denominator * n);
    }
}

/// <summary>
/// Two deliberately UNRATIFIED sensitivity variants of a price FUNCTION.
/// Neither has cash budgets, hard demand quantities or exogenous revenues.
/// </summary>
public enum CanonicalEndogenousCurveVariant
{
    GentleDyadicSupplyDemand = 1,
    SteepDyadicSupplyDemand = 2
}

/// <summary>
/// Hypothetically PRODUCED output, not just potential capacity. Retention is
/// an explicitly specified source quantity, never inferred from unsold output.
/// </summary>
public sealed record CanonicalEndogenousProductionDeclaration(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong ProducedRaw,
    ulong RetainedAtSourceRaw);

/// <summary>
/// A hypothetical ADDITION to the demand SIGNAL, not money, a quantity of
/// acquired inventory or an executable storage command.
/// </summary>
public sealed record CanonicalEndogenousDemandSignalAddition(
    EconomicPointId Buyer, CommodityId Commodity,
    ulong AdditionalDemandPressureRaw);

public sealed record CanonicalEndogenousPhysicalSourceRow(
    EconomicPointId Source, CommodityId Commodity,
    EconomicActivityKind Activity, ulong PhysicalPotentialRaw,
    ulong HypotheticallyProducedRaw, ulong ExplicitRetentionRaw,
    ulong OfferedAndLocallyAbsorbedRaw);

public sealed record CanonicalEndogenousPriceCommodityRow(
    EconomicPointId LocalMarket, CommodityId Commodity,
    BigInteger EffectiveOfferRaw, BigInteger BaseDemandPressureRaw,
    BigInteger AdditionalDemandPressureRaw,
    CanonicalEndogenousPriceIndex AveragePriceIndex,
    CanonicalEndogenousPriceIndex LastMarginalPriceIndex,
    CanonicalEndogenousPriceIndex ResultingGrossRevenueIndex);

public sealed record CanonicalEndogenousPriceCurveSnapshot(
    CanonicalEndogenousCurveVariant Variant,
    IReadOnlyList<CanonicalEndogenousPriceCommodityRow> Commodities,
    CanonicalEndogenousPriceIndex AggregateGrossRevenueIndex);

internal sealed record CanonicalEndogenousPriceReconstruction(
    IReadOnlyList<CanonicalEndogenousPhysicalSourceRow> Sources,
    CanonicalEndogenousPriceCurveSnapshot Gentle,
    CanonicalEndogenousPriceCurveSnapshot Steep);

/// <summary>
/// VS78 R2: quantity-independent-of-budget local macro-absorption lab.
/// Price and THEN revenue follow real source supply and resident commodity
/// demand intensity. No exogenous cash B, H, per-product wallet, invented
/// freight service or buyer. Local-only; remote settlement is NOT modeled.
/// </summary>
public sealed class CanonicalEndogenousLocalPriceComparison
{
    public const int MaximumDeclarations = 12;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalEndogenousProductionDeclaration[] _productions;
    private readonly CanonicalEndogenousDemandSignalAddition[] _additions;

    public IReadOnlyList<CanonicalEndogenousPhysicalSourceRow> Sources { get; }
    public CanonicalEndogenousPriceCurveSnapshot Gentle { get; }
    public CanonicalEndogenousPriceCurveSnapshot Steep { get; }
    public IReadOnlyList<CanonicalEndogenousDemandSignalAddition> DemandSignalAdditions { get; }
    public bool UsesAnExogenousMonetaryBudget => false;
    public bool AppliesPhysicalDemandAsConsumptionCap => false;
    public bool IsRemoteFreightPriceCalibrated => false;
    public bool IsActualConsumption => false;
    public bool CreatesActualInventory => false;
    public bool CanAuthorizeRealSettlement => false;

    private CanonicalEndogenousLocalPriceComparison(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalEndogenousProductionDeclaration[] productions,
        CanonicalEndogenousDemandSignalAddition[] additions,
        CanonicalEndogenousPriceReconstruction reconstructed)
    {
        _session = session;
        _sites = sites;
        _productions = (CanonicalEndogenousProductionDeclaration[])productions.Clone();
        _additions = (CanonicalEndogenousDemandSignalAddition[])additions.Clone();
        DemandSignalAdditions = Array.AsReadOnly(_additions);
        Sources = reconstructed.Sources;
        Gentle = reconstructed.Gentle;
        Steep = reconstructed.Steep;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "Endogenous-price reference belongs to a different physical session.");
        _sites.RequireCurrent(session);
    }

    /// <summary>
    /// Exact O(log(q)) piecewise marginal price on doubling quantity bands.
    /// p_marginal = demandPressure / (demandPressure + sensitivity * bandStart).
    /// Revenue is INTEGRATED over produced offered quantity, NOT a predefined
    /// budget, and is unbounded as quantity grows when demandPressure > 0.
    /// Both alternatives are experimental and use no monetary source inputs.
    /// </summary>
    public static (CanonicalEndogenousPriceIndex Last,
        CanonicalEndogenousPriceIndex Average,
        CanonicalEndogenousPriceIndex Revenue)
        Calculate(BigInteger offered, BigInteger demandPressure, int sensitivity)
    {
        if (offered < 0 || demandPressure < 0 ||
            (sensitivity != 1 && sensitivity != 2))
            throw new ArgumentOutOfRangeException(nameof(offered));
        if (offered.IsZero)
            return (CanonicalEndogenousPriceIndex.Zero,
                CanonicalEndogenousPriceIndex.Zero,
                CanonicalEndogenousPriceIndex.Zero);
        if (demandPressure.IsZero)
            return (CanonicalEndogenousPriceIndex.Zero,
                CanonicalEndogenousPriceIndex.Zero,
                CanonicalEndogenousPriceIndex.Zero);

        var current = BigInteger.Zero;
        var bandSize = BigInteger.One;
        var revenue = CanonicalEndogenousPriceIndex.Zero;
        var last = CanonicalEndogenousPriceIndex.Zero;
        while (current < offered)
        {
            var count = BigInteger.Min(bandSize, offered - current);
            last = new CanonicalEndogenousPriceIndex(
                demandPressure, demandPressure + sensitivity * current);
            revenue = CanonicalEndogenousPriceIndex.Add(
                revenue, last.Times(count));
            current += count;
            bandSize *= 2;
        }
        return (last, revenue.DividedBy(offered), revenue);
    }

    private static CanonicalEndogenousPriceReconstruction Rebuild(
        CanonicalPrimitiveSiteDiscovery sites,
        IReadOnlyList<CanonicalEndogenousProductionDeclaration> productions,
        IReadOnlyList<CanonicalEndogenousDemandSignalAddition> additions)
    {
        var physical = sites.Flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity),
            x => x.RawQuantity);
        var residentDemand = sites.Flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        var extra = additions.ToDictionary(
            x => (x.Buyer, x.Commodity), x => x.AdditionalDemandPressureRaw);
        var rows = new List<CanonicalEndogenousPhysicalSourceRow>();
        foreach (var x in productions)
        {
            var key = (x.Source, x.Commodity, x.Activity);
            var localMarket = sites.At(x.Source);
            if (!physical.TryGetValue(key, out var potential) ||
                localMarket is null || localMarket.ResidentWorkers == 0UL ||
                x.ProducedRaw > potential || x.RetainedAtSourceRaw > x.ProducedRaw)
                throw new ArgumentException(
                    "Production and retention require a real inhabited source and cannot exceed physical production.",
                    nameof(productions));
            rows.Add(new(x.Source, x.Commodity, x.Activity,
                potential, x.ProducedRaw, x.RetainedAtSourceRaw,
                x.ProducedRaw - x.RetainedAtSourceRaw));
        }
        if (extra.Keys.Any(k =>
            !residentDemand.ContainsKey(k) || sites.At(k.Buyer) is null))
            throw new ArgumentException(
                "A demand-pressure addition requires an authenticated resident and commodity.",
                nameof(additions));

        var ordered = rows.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity).ToArray();
        var grouped = ordered.GroupBy(x => (x.Source, x.Commodity))
            .OrderBy(x => x.Key.Source.Value)
            .ThenBy(x => x.Key.Commodity.Value)
            .ToArray();

        CanonicalEndogenousPriceCurveSnapshot BuildCurve(
            CanonicalEndogenousCurveVariant variant, int sensitivity)
        {
            var byCommodity = new List<CanonicalEndogenousPriceCommodityRow>();
            var aggregate = CanonicalEndogenousPriceIndex.Zero;
            foreach (var group in grouped)
            {
                var key = group.Key;
                var offered = group.Aggregate(BigInteger.Zero,
                    (sum, x) => sum + x.OfferedAndLocallyAbsorbedRaw);
                residentDemand.TryGetValue(key, out var baseDemand);
                extra.TryGetValue(key, out var extraDemand);
                var pressure = new BigInteger(baseDemand) + extraDemand;
                var computed = Calculate(offered, pressure, sensitivity);
                byCommodity.Add(new(key.Source, key.Commodity,
                    offered, baseDemand, extraDemand,
                    computed.Average, computed.Last, computed.Revenue));
                aggregate = CanonicalEndogenousPriceIndex.Add(
                    aggregate, computed.Revenue);
            }
            return new(variant, Array.AsReadOnly(byCommodity.ToArray()), aggregate);
        }
        return new(Array.AsReadOnly(ordered),
            BuildCurve(CanonicalEndogenousCurveVariant.GentleDyadicSupplyDemand, 1),
            BuildCurve(CanonicalEndogenousCurveVariant.SteepDyadicSupplyDemand, 2));
    }

    private static bool SameIndex(CanonicalEndogenousPriceIndex a,
        CanonicalEndogenousPriceIndex b) => a.CompareTo(b) == 0;

    private static bool SameCurve(
        CanonicalEndogenousPriceCurveSnapshot a,
        CanonicalEndogenousPriceCurveSnapshot b)
    {
        if (a.Variant != b.Variant || a.Commodities.Count != b.Commodities.Count ||
            !SameIndex(a.AggregateGrossRevenueIndex,
                b.AggregateGrossRevenueIndex)) return false;
        for (var i = 0; i < a.Commodities.Count; i++)
        {
            var x = a.Commodities[i];
            var y = b.Commodities[i];
            if (x.LocalMarket != y.LocalMarket || x.Commodity != y.Commodity ||
                x.EffectiveOfferRaw != y.EffectiveOfferRaw ||
                x.BaseDemandPressureRaw != y.BaseDemandPressureRaw ||
                x.AdditionalDemandPressureRaw != y.AdditionalDemandPressureRaw ||
                !SameIndex(x.AveragePriceIndex, y.AveragePriceIndex) ||
                !SameIndex(x.LastMarginalPriceIndex, y.LastMarginalPriceIndex) ||
                !SameIndex(x.ResultingGrossRevenueIndex,
                    y.ResultingGrossRevenueIndex)) return false;
        }
        return true;
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = Rebuild(_sites, _productions, _additions);
        if (!Sources.SequenceEqual(replay.Sources) ||
            !SameCurve(Gentle, replay.Gentle) ||
            !SameCurve(Steep, replay.Steep))
            throw new InvalidOperationException(
                "Endogenous price and production reference failed independent replay.");
        RequireCurrent(_session);
    }

    public static CanonicalEndogenousLocalPriceComparison FromSites(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalEndogenousProductionDeclaration> hypotheticalOutput,
        IEnumerable<CanonicalEndogenousDemandSignalAddition>? demandSignalAdditions = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(hypotheticalOutput);
        sites.RequireCurrent(session);
        var productions = hypotheticalOutput.ToArray();
        var additions = demandSignalAdditions?.ToArray() ??
            Array.Empty<CanonicalEndogenousDemandSignalAddition>();
        if (productions.Length is < 1 or > MaximumDeclarations ||
            productions.Any(x => x is null || !x.Source.IsValid ||
                !x.Commodity.IsValid || x.ProducedRaw == 0UL) ||
            productions.Select(x => (x.Source, x.Commodity, x.Activity))
                .Distinct().Count() != productions.Length)
            throw new ArgumentException(
                "Price study requires 1..12 unique, positive, physical production declarations.",
                nameof(hypotheticalOutput));
        if (additions.Length > MaximumDeclarations ||
            additions.Any(x => x is null || x.AdditionalDemandPressureRaw == 0UL ||
                !x.Buyer.IsValid || !x.Commodity.IsValid) ||
            additions.Select(x => (x.Buyer, x.Commodity))
                .Distinct().Count() != additions.Length)
            throw new ArgumentException(
                "Demand-pressure additions must be unique positive resident commodity signals.",
                nameof(demandSignalAdditions));

        var normalized = productions.OrderBy(x => x.Source.Value)
            .ThenBy(x => x.Commodity.Value).ThenBy(x => x.Activity).ToArray();
        var added = additions.OrderBy(x => x.Buyer.Value)
            .ThenBy(x => x.Commodity.Value).ToArray();
        var snapshot = Rebuild(sites, normalized, added);
        var result = new CanonicalEndogenousLocalPriceComparison(
            session, sites, normalized, added, snapshot);
        result.VerifyIndependent();
        return result;
    }
}
