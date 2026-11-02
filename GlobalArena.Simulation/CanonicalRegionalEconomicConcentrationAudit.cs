using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Exact nonnegative rational used only as a share/concentration diagnostic.
/// No nominal currency, household wallet, or state treasury is represented.
/// </summary>
public readonly record struct CanonicalRegionalShare(
    BigInteger Numerator, BigInteger Denominator) :
    IComparable<CanonicalRegionalShare>
{
    public static CanonicalRegionalShare Zero => new(0, 1);
    public static CanonicalRegionalShare One => new(1, 1);

    public static CanonicalRegionalShare Of(BigInteger numerator, BigInteger denominator)
    {
        if (denominator <= 0 || numerator < 0)
            throw new ArgumentOutOfRangeException(nameof(denominator),
                "Regional shares require nonnegative numerator and positive denominator.");
        if (numerator.IsZero) return Zero;
        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        return new(numerator / gcd, denominator / gcd);
    }

    public int CompareTo(CanonicalRegionalShare other)
    {
        if (Denominator <= 0 || other.Denominator <= 0)
            throw new InvalidOperationException(
                "Invalid regional concentration rational denominator.");
        return (Numerator * other.Denominator).CompareTo(
            other.Numerator * Denominator);
    }

    public static CanonicalRegionalShare Add(
        CanonicalRegionalShare a, CanonicalRegionalShare b) =>
        Of(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    public CanonicalRegionalShare Squared() =>
        Of(Numerator * Numerator, Denominator * Denominator);
}

/// <summary>
/// One inhabited economic market, a spatial proxy only: NOT a political
/// region, tax jurisdiction, ownership ledger or household cash account.
/// Shares apply WITHIN ONE COMMODITY and one sensitivity only.
/// </summary>
public sealed record CanonicalRegionalMarketShareRow(
    EconomicPointId Market,
    CommodityId Commodity,
    BigInteger HypotheticallyAbsorbedRaw,
    BigInteger ResidentDemandSignalRaw,
    CanonicalRegionalShare PhysicalShare,
    CanonicalRegionalShare GentleGrossIndexShare,
    CanonicalRegionalShare SteepGrossIndexShare);

public sealed record CanonicalRegionalCommodityConcentrationRow(
    CommodityId Commodity,
    EconomicActivityKind Activity,
    int InhabitedMarketCount,
    BigInteger TotalAbsorbedRaw,
    BigInteger BasalDemandSignalRaw,
    CanonicalRegionalShare PhysicalHerfindahlIndex,
    CanonicalRegionalShare GentleGrossHerfindahlIndex,
    CanonicalRegionalShare SteepGrossHerfindahlIndex,
    EconomicPointId? LargestPhysicalMarket,
    EconomicPointId? LargestGentleGrossIndexMarket,
    EconomicPointId? LargestSteepGrossIndexMarket,
    CanonicalEndogenousPriceIndex GentleTotalGrossRevenueIndex,
    CanonicalEndogenousPriceIndex SteepTotalGrossRevenueIndex);

internal sealed record CanonicalRegionalConcentrationReconstruction(
    CanonicalRegionalMarketShareRow[] Markets,
    CanonicalRegionalCommodityConcentrationRow[] Commodities);

/// <summary>
/// VS97: deterministic, cycle-bound, read-only *regional proxy* diagnostic
/// derived solely from VS96 physical absorption and unratified dimensionless
/// marginal revenue curves. NEVER adds values across different commodities,
/// converts an index into monetary revenue, or introduces any consumer cash.
/// The planned future source of state monetary income is abstract and
/// function-regulated, not a debit from households.
/// </summary>
public sealed class CanonicalRegionalEconomicConcentrationAudit
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitiveSimultaneousAbsorptionTrial _absorption96;
    private readonly CanonicalRegionalMarketShareRow[] _markets;
    private readonly CanonicalRegionalCommodityConcentrationRow[] _commodities;

    public IReadOnlyList<CanonicalRegionalMarketShareRow> Markets { get; }
    public IReadOnlyList<CanonicalRegionalCommodityConcentrationRow> Commodities { get; }
    public ulong CompletedEconomicCycleMovementTurn =>
        _absorption96.CompletedEconomicCycleMovementTurn;

    public bool UsesOnlyCertifiedVs96PhysicalMarkets => true;
    public bool ComparesWithinEachCommodityOnly => true;
    public bool UsesSpatialMarketProxiesNotPoliticalRegions => true;
    public bool PreservesAbstractFunctionRegulatedMoneySource => true;
    public bool DebitsHouseholdOrPopulationCash => false;
    public bool CreatesBuyerBudgetsOrCredits => false;
    public bool ConvertsIndicesToMoney => false;
    public bool PostsStateTreasuryRevenue => false;
    public bool ClaimsExecutedWoodStoneSales => false;
    public bool MutatesWorldOrSave => false;
    public bool CanAuthorizeSettlement => false;

    private CanonicalRegionalEconomicConcentrationAudit(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveSimultaneousAbsorptionTrial absorption96,
        CanonicalRegionalConcentrationReconstruction result)
    {
        _session = session;
        _sites = sites;
        _absorption96 = absorption96;
        _markets = result.Markets;
        _commodities = result.Commodities;
        Markets = Array.AsReadOnly(_markets);
        Commodities = Array.AsReadOnly(_commodities);
    }

    internal void RequireSameAbsorption(
        CanonicalPrimitiveSimultaneousAbsorptionTrial absorption)
    {
        if (!ReferenceEquals(_absorption96, absorption))
            throw new InvalidOperationException(
                "Abstract revenue must match the VS97-certified VS96 absorption.");
        RequireCurrent(_session);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "VS97 concentration audit is bound to another physical session.");
        _sites.RequireCurrent(session);
        _absorption96.RequireCurrent(session);
        _absorption96.RequireSameSites(_sites);
    }

    private static CanonicalRegionalShare FromRelativeIndex(
        CanonicalEndogenousPriceIndex observed,
        CanonicalEndogenousPriceIndex total)
    {
        if (observed.Denominator <= 0 || observed.Numerator < 0 ||
            total.Denominator <= 0 || total.Numerator < 0)
            throw new InvalidOperationException(
                "VS97 requires nonnegative authentic relative revenue curves.");
        if (total.Numerator.IsZero)
        {
            if (!observed.Numerator.IsZero)
                throw new InvalidOperationException(
                    "Positive component cannot belong to zero relative revenue.");
            return CanonicalRegionalShare.Zero;
        }
        return CanonicalRegionalShare.Of(
            observed.Numerator * total.Denominator,
            observed.Denominator * total.Numerator);
    }

    private static CanonicalRegionalConcentrationReconstruction Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveSimultaneousAbsorptionTrial absorption96)
    {
        sites.RequireCurrent(session);
        absorption96.RequireCurrent(session);
        absorption96.RequireSameSites(sites);
        absorption96.VerifyIndependent();

        var demand = sites.Flows.DemandFlows.ToDictionary(
            x => (Market: x.EconomicPointId, Commodity: x.CommodityId),
            x => x.RawQuantity);
        var reference = absorption96.Commodities.ToDictionary(x => x.Commodity);
        var markets = absorption96.Markets.OrderBy(x => x.Market.Value)
            .ThenBy(x => x.Commodity.Value).ToArray();
        if (reference.Count != 3 ||
            markets.Length != demand.Count ||
            markets.Select(x => (x.Market, x.Commodity)).Distinct().Count() !=
                markets.Length ||
            !markets.All(x =>
                demand.TryGetValue((x.Market, x.Commodity), out var pressure) &&
                x.BasalResidentDemandSignalRaw == pressure &&
                sites.At(x.Market)?.ResidentWorkers > 0UL &&
                reference.ContainsKey(x.Commodity)))
            throw new InvalidOperationException(
                "VS97 requires one VS96 market row per authenticated inhabited demand site.");

        var all = new List<CanonicalRegionalMarketShareRow>();
        var totals = new List<CanonicalRegionalCommodityConcentrationRow>();
        foreach (var commodity in absorption96.Commodities
            .OrderBy(x => x.Commodity.Value))
        {
            var group = markets.Where(x => x.Commodity == commodity.Commodity)
                .OrderBy(x => x.Market.Value).ToArray();
            var q = group.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.TotalHypotheticallyAbsorbedRaw);
            var signal = group.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.BasalResidentDemandSignalRaw);
            var gentleTotal = group.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.GentleGrossRevenueIndex));
            var steepTotal = group.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.SteepGrossRevenueIndex));
            if (q != commodity.LocallyAbsorbedRaw + commodity.RemotelyAbsorbedRaw ||
                signal != commodity.ProjectedDemandSignalRaw ||
                gentleTotal.CompareTo(commodity.GentleGrossRevenueIndex) != 0 ||
                steepTotal.CompareTo(commodity.SteepGrossRevenueIndex) != 0 ||
                q + commodity.NeverExtractedRaw != commodity.PotentialRaw)
                throw new InvalidOperationException(
                    "VS97 cannot change certified physical volume, nonextracted material or within-commodity revenue indices.");

            var physicalSum = CanonicalRegionalShare.Zero;
            var gentleSum = CanonicalRegionalShare.Zero;
            var steepSum = CanonicalRegionalShare.Zero;
            var physicalHhi = CanonicalRegionalShare.Zero;
            var gentleHhi = CanonicalRegionalShare.Zero;
            var steepHhi = CanonicalRegionalShare.Zero;

            EconomicPointId? largestPhysical = null;
            EconomicPointId? largestGentle = null;
            EconomicPointId? largestSteep = null;
            var maxPhysical = CanonicalRegionalShare.Zero;
            var maxGentle = CanonicalRegionalShare.Zero;
            var maxSteep = CanonicalRegionalShare.Zero;

            foreach (var x in group)
            {
                var share = q.IsZero
                    ? CanonicalRegionalShare.Zero
                    : CanonicalRegionalShare.Of(
                        x.TotalHypotheticallyAbsorbedRaw, q);
                var mild = FromRelativeIndex(
                    x.GentleGrossRevenueIndex, gentleTotal);
                var steep = FromRelativeIndex(
                    x.SteepGrossRevenueIndex, steepTotal);
                if (share.CompareTo(CanonicalRegionalShare.One) > 0 ||
                    mild.CompareTo(CanonicalRegionalShare.One) > 0 ||
                    steep.CompareTo(CanonicalRegionalShare.One) > 0)
                    throw new InvalidOperationException(
                        "One region cannot account for more than all of its commodity.");

                all.Add(new(x.Market, x.Commodity,
                    x.TotalHypotheticallyAbsorbedRaw,
                    x.BasalResidentDemandSignalRaw, share, mild, steep));
                physicalSum = CanonicalRegionalShare.Add(physicalSum, share);
                gentleSum = CanonicalRegionalShare.Add(gentleSum, mild);
                steepSum = CanonicalRegionalShare.Add(steepSum, steep);
                physicalHhi = CanonicalRegionalShare.Add(
                    physicalHhi, share.Squared());
                gentleHhi = CanonicalRegionalShare.Add(
                    gentleHhi, mild.Squared());
                steepHhi = CanonicalRegionalShare.Add(
                    steepHhi, steep.Squared());

                // Stable ascending point IDs break equal-share ties.
                if (share.Numerator > 0 &&
                    (largestPhysical is null || share.CompareTo(maxPhysical) > 0))
                {
                    maxPhysical = share;
                    largestPhysical = x.Market;
                }
                if (mild.Numerator > 0 &&
                    (largestGentle is null || mild.CompareTo(maxGentle) > 0))
                {
                    maxGentle = mild;
                    largestGentle = x.Market;
                }
                if (steep.Numerator > 0 &&
                    (largestSteep is null || steep.CompareTo(maxSteep) > 0))
                {
                    maxSteep = steep;
                    largestSteep = x.Market;
                }
            }

            if (physicalSum.CompareTo(q.IsZero
                    ? CanonicalRegionalShare.Zero : CanonicalRegionalShare.One) != 0 ||
                gentleSum.CompareTo(gentleTotal.Numerator.IsZero
                    ? CanonicalRegionalShare.Zero : CanonicalRegionalShare.One) != 0 ||
                steepSum.CompareTo(steepTotal.Numerator.IsZero
                    ? CanonicalRegionalShare.Zero : CanonicalRegionalShare.One) != 0 ||
                physicalHhi.CompareTo(CanonicalRegionalShare.One) > 0 ||
                gentleHhi.CompareTo(CanonicalRegionalShare.One) > 0 ||
                steepHhi.CompareTo(CanonicalRegionalShare.One) > 0)
                throw new InvalidOperationException(
                    "Every commodity's exact physical and relative-revenue shares must fully reconcile.");

            totals.Add(new(commodity.Commodity, commodity.Activity,
                group.Length, q, signal, physicalHhi, gentleHhi, steepHhi,
                largestPhysical, largestGentle, largestSteep,
                gentleTotal, steepTotal));
        }

        sites.RequireCurrent(session);
        absorption96.RequireCurrent(session);
        return new(
            all.OrderBy(x => x.Market.Value).ThenBy(x => x.Commodity.Value).ToArray(),
            totals.ToArray());
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = Reconstruct(_session, _sites, _absorption96);
        if (!_markets.SequenceEqual(replay.Markets) ||
            !_commodities.SequenceEqual(replay.Commodities))
            throw new InvalidOperationException(
                "VS97 independent regional concentration reconstruction diverged.");
        RequireCurrent(_session);
    }

    public static CanonicalRegionalEconomicConcentrationAudit FromCertifiedAbsorption(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery currentSites,
        CanonicalPrimitiveSimultaneousAbsorptionTrial certifiedVs96)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(currentSites);
        ArgumentNullException.ThrowIfNull(certifiedVs96);
        var computed = Reconstruct(session, currentSites, certifiedVs96);
        var audit = new CanonicalRegionalEconomicConcentrationAudit(
            session, currentSites, certifiedVs96, computed);
        audit.VerifyIndependent();
        return audit;
    }
}
