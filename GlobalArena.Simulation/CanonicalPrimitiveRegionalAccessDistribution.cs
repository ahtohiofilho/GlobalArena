using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Planned VS67 freight coverage of ONE physical demand flow. A zero offer
/// count means no selected, institutionally authorized freight candidate;
/// it does NOT imply the resident cannot consume locally or self-produce.
/// </summary>
public sealed record CanonicalPrimitiveBuyerCommodityAccess(
    EconomicPointId Buyer,
    CommodityId Commodity,
    ulong PhysicalDemandRaw,
    ulong PlannedFreightRaw,
    int AuthorizedFreightOffers,
    FoodTransportCostFraction PlannedDemandCoverage);

/// <summary>
/// No stored wallet, wage, actual consumption, stock or settled payment.
/// Every number is a read-only, one-cycle hypothetical projection.
/// </summary>
public sealed record CanonicalPrimitiveBuyerAccessRow(
    EconomicPointId Buyer,
    PhysicalTacticalTileId Tile,
    ulong ResidentWorkers,
    FoodTransportCostFraction AppliedPurchasingFactor,
    FoodTransportCostFraction GrossPurchasing,
    FoodTransportCostFraction ProtectedH,
    FoodTransportCostFraction SpendablePurchasing,
    FoodTransportCostFraction PlannedCommitment,
    FoodTransportCostFraction MeanCommodityFreightCoverage,
    IReadOnlyList<CanonicalPrimitiveBuyerCommodityAccess> Commodities);

/// <summary>
/// Aggregate indicators are dimensionless means of per buyer/commodity
/// coverage; raw quantities of distinct goods are NEVER summed.
/// </summary>
public sealed record CanonicalPrimitiveRegionalAccessScenario(
    string ScenarioId,
    IReadOnlyList<CanonicalPrimitiveBuyerAccessRow> Buyers,
    FoodTransportCostFraction MeanBuyerCommodityFreightCoverage,
    FoodTransportCostFraction TotalGrossPurchasing,
    FoodTransportCostFraction TotalProtectedH,
    FoodTransportCostFraction TotalSpendablePurchasing,
    FoodTransportCostFraction TotalPlannedCommitment,
    FoodTransportCostFraction MinimumGrossPerResident,
    FoodTransportCostFraction MaximumGrossPerResident,
    int BuyerCommodityPairsWithoutAuthorizedFreight,
    int BuyerCommodityPairsWithoutSelectedFreight);

/// <summary>
/// VS71 is a diagnostic bridge, NOT a new economic allocation engine.
/// Reuses VS70 physical purchasing, VS68 counterfactual comparison,
/// VS67/VS66 independently verified lot selection and VS65 buyer budgets.
/// It only explains distribution and planned freight access, without
/// pretending freight equals total consumption or ranking social welfare.
/// </summary>
public sealed class CanonicalPrimitiveRegionalAccessDistribution
{
    private readonly CanonicalPrimitiveFreightPortfolio _portfolio;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveFreightCandidateUse[] _candidateUses;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalPrimitiveRegionalProductivityProjection _regional;

    public CanonicalPrimitiveFreightScenarioComparison Comparison { get; }
    public CanonicalPrimitiveRegionalAccessScenario Baseline { get; }
    public CanonicalPrimitiveRegionalAccessScenario Regional { get; }
    public int FreightCoverageDirectionVsBaseline =>
        Math.Sign(Regional.MeanBuyerCommodityFreightCoverage.CompareTo(
            Baseline.MeanBuyerCommodityFreightCoverage));
    public int SpendablePowerDirectionVsBaseline =>
        Math.Sign(Regional.TotalSpendablePurchasing.CompareTo(
            Baseline.TotalSpendablePurchasing));
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;

    private CanonicalPrimitiveRegionalAccessDistribution(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveRegionalProductivityProjection regional,
        CanonicalPrimitiveFreightCandidateUse[] candidates,
        CanonicalPrimitiveFreightScenarioComparison comparison,
        CanonicalPrimitiveRegionalAccessScenario baseline,
        CanonicalPrimitiveRegionalAccessScenario calibrated)
    {
        _portfolio = portfolio;
        _session = session;
        _sites = portfolio.CurrentSites;
        _regional = regional;
        _candidateUses = (CanonicalPrimitiveFreightCandidateUse[])candidates.Clone();
        Comparison = comparison;
        Baseline = baseline;
        Regional = calibrated;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "Regional access diagnostics belong to a different physical session.");
        _portfolio.RequireCurrent(session);
        _sites.RequireCurrent(session);
        _regional.RequireCurrent(session);
        Comparison.RequireCurrent(session);
    }

    private static FoodTransportCostFraction Zero => new(0, 1);

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static FoodTransportCostFraction DivideByCount(
        FoodTransportCostFraction sum, int count)
    {
        if (count <= 0) throw new InvalidOperationException("Empty buyer/commodity scope.");
        return new FoodTransportCostFraction(sum.Numerator,
            sum.Denominator * count);
    }

    private CanonicalPrimitiveRegionalAccessScenario Build(
        CanonicalPrimitiveFreightScenarioOutcome outcome)
    {
        var projection = new ExperimentalAbstractPurchasingPowerProjector()
            .Project(_session, _sites.Flows, _sites.BuyerAnchors,
                _sites.ProducerAnchors,
                Array.Empty<ExperimentalSimultaneousFreightLane>(),
                outcome.Scenario.PostedPrices, outcome.Scenario.PurchasingPolicy);
        var budgetByBuyer = projection.Buyers.ToDictionary(x => x.Buyer);
        var planned = outcome.Selection.Lots
            .GroupBy(x => (x.Candidate.Buyer, x.Candidate.Commodity))
            .ToDictionary(x => x.Key, x => x.Aggregate(
                BigInteger.Zero, (sum, lot) => sum + lot.RawQuantity));
        var charged = outcome.Selection.Preview?.Buyers.Buyers.ToDictionary(
            x => x.Buyer, x => x.TotalCommitment);
        var rows = new List<CanonicalPrimitiveBuyerAccessRow>();
        var totalGross = Zero;
        var totalH = Zero;
        var totalSpendable = Zero;
        var totalCommitment = Zero;
        var totalCoverage = Zero;
        FoodTransportCostFraction? minGrossPerWorker = null;
        FoodTransportCostFraction? maxGrossPerWorker = null;
        var withoutOffer = 0;
        var withoutDelivery = 0;
        var pairCount = 0;
        foreach (var site in _sites.Sites.OrderBy(x => x.Point.Value))
        {
            var buyer = budgetByBuyer[site.Point];
            var cash = buyer.TemporaryCycleBudget;
            if (site.ResidentWorkers != buyer.ResidentWorkers || site.Tile != buyer.Tile)
                throw new InvalidOperationException(
                    "Regional access buyer has a nonphysical residency identity.");
            var grossPerWorker = new FoodTransportCostFraction(
                cash.GrossCash.Numerator,
                cash.GrossCash.Denominator * site.ResidentWorkers);
            if (minGrossPerWorker is null ||
                grossPerWorker.CompareTo(minGrossPerWorker) < 0)
                minGrossPerWorker = grossPerWorker;
            if (maxGrossPerWorker is null ||
                grossPerWorker.CompareTo(maxGrossPerWorker) > 0)
                maxGrossPerWorker = grossPerWorker;
            var commodityRows = new List<CanonicalPrimitiveBuyerCommodityAccess>();
            var buyerCoverage = Zero;
            foreach (var demand in site.Demand.OrderBy(x => x.CommodityId.Value))
            {
                var key = (site.Point, demand.CommodityId);
                planned.TryGetValue(key, out var delivered);
                if (delivered < 0 || delivered > demand.RawQuantity)
                    throw new InvalidOperationException(
                        "Selected freight violates physical resident commodity demand.");
                var offers = _candidateUses.Count(x =>
                    x.Buyer == site.Point && x.Commodity == demand.CommodityId);
                if (offers == 0) withoutOffer++;
                if (delivered == 0) withoutDelivery++;
                var share = new FoodTransportCostFraction(delivered,
                    demand.RawQuantity);
                commodityRows.Add(new(
                    site.Point, demand.CommodityId, demand.RawQuantity,
                    checked((ulong)delivered), offers, share));
                buyerCoverage = Add(buyerCoverage, share);
                totalCoverage = Add(totalCoverage, share);
                pairCount++;
            }
            if (commodityRows.Count == 0)
                throw new InvalidOperationException(
                    "Inhabited residents must retain declared physical demand.");
            var commitment = charged is not null &&
                charged.TryGetValue(site.Point, out var charge) ? charge : Zero;
            if (commitment.CompareTo(cash.SpendableCash) > 0)
                throw new InvalidOperationException(
                    "Planned resident purchases exceed protected H budget.");
            totalGross = Add(totalGross, cash.GrossCash);
            totalH = Add(totalH, cash.ProtectedResidual);
            totalSpendable = Add(totalSpendable, cash.SpendableCash);
            totalCommitment = Add(totalCommitment, commitment);
            rows.Add(new(site.Point, site.Tile, site.ResidentWorkers,
                buyer.ProductivityFactor, cash.GrossCash, cash.ProtectedResidual,
                cash.SpendableCash, commitment,
                DivideByCount(buyerCoverage, commodityRows.Count),
                Array.AsReadOnly(commodityRows.ToArray())));
        }
        if (rows.Count == 0 || pairCount != _sites.Flows.DemandFlows.Count ||
            projection.ResidentWorkersInProjectedBuyers != _sites.ResidentWorkers ||
            totalGross.CompareTo(projection.TotalTemporaryPurchasingUnits) != 0 ||
            totalCommitment.CompareTo(outcome.Selection.TotalBuyerCommitment) != 0 ||
            planned.Keys.Any(x => !_sites.Flows.DemandFlows.Any(
                d => d.EconomicPointId == x.Buyer && d.CommodityId == x.Commodity)))
            throw new InvalidOperationException(
                "Regional access distribution fails exact resident, demand or budget reconciliation.");

        return new CanonicalPrimitiveRegionalAccessScenario(
            outcome.Scenario.ScenarioId, Array.AsReadOnly(rows.ToArray()),
            DivideByCount(totalCoverage, pairCount), totalGross, totalH,
            totalSpendable, totalCommitment, minGrossPerWorker!,
            maxGrossPerWorker!, withoutOffer, withoutDelivery);
    }

    private static bool SameMoney(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        a.CompareTo(b) == 0;

    private static bool SameScenario(
        CanonicalPrimitiveRegionalAccessScenario a,
        CanonicalPrimitiveRegionalAccessScenario b)
    {
        if (a.ScenarioId != b.ScenarioId ||
            a.Buyers.Count != b.Buyers.Count ||
            a.BuyerCommodityPairsWithoutAuthorizedFreight !=
                b.BuyerCommodityPairsWithoutAuthorizedFreight ||
            a.BuyerCommodityPairsWithoutSelectedFreight !=
                b.BuyerCommodityPairsWithoutSelectedFreight ||
            !SameMoney(a.MeanBuyerCommodityFreightCoverage,
                b.MeanBuyerCommodityFreightCoverage) ||
            !SameMoney(a.TotalGrossPurchasing, b.TotalGrossPurchasing) ||
            !SameMoney(a.TotalProtectedH, b.TotalProtectedH) ||
            !SameMoney(a.TotalSpendablePurchasing, b.TotalSpendablePurchasing) ||
            !SameMoney(a.TotalPlannedCommitment, b.TotalPlannedCommitment) ||
            !SameMoney(a.MinimumGrossPerResident, b.MinimumGrossPerResident) ||
            !SameMoney(a.MaximumGrossPerResident, b.MaximumGrossPerResident))
            return false;
        foreach (var (x, y) in a.Buyers.Zip(b.Buyers))
        {
            if (x.Buyer != y.Buyer || x.Tile != y.Tile ||
                x.ResidentWorkers != y.ResidentWorkers ||
                x.Commodities.Count != y.Commodities.Count ||
                !SameMoney(x.AppliedPurchasingFactor, y.AppliedPurchasingFactor) ||
                !SameMoney(x.GrossPurchasing, y.GrossPurchasing) ||
                !SameMoney(x.ProtectedH, y.ProtectedH) ||
                !SameMoney(x.SpendablePurchasing, y.SpendablePurchasing) ||
                !SameMoney(x.PlannedCommitment, y.PlannedCommitment) ||
                !SameMoney(x.MeanCommodityFreightCoverage,
                    y.MeanCommodityFreightCoverage))
                return false;
            foreach (var (left, right) in x.Commodities.Zip(y.Commodities))
                if (left.Buyer != right.Buyer ||
                    left.Commodity != right.Commodity ||
                    left.PhysicalDemandRaw != right.PhysicalDemandRaw ||
                    left.PlannedFreightRaw != right.PlannedFreightRaw ||
                    left.AuthorizedFreightOffers != right.AuthorizedFreightOffers ||
                    !SameMoney(left.PlannedDemandCoverage,
                        right.PlannedDemandCoverage))
                    return false;
        }
        return true;
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        Comparison.VerifyRecomputed();
        var baseline = Build(Comparison.Baseline);
        var region = Build(Comparison.Outcomes.Single(x =>
            x.Scenario.ScenarioId == "regional-productivity"));
        if (!SameScenario(baseline, Baseline) ||
            !SameScenario(region, Regional))
            throw new InvalidOperationException(
                "Regional buyer/commodity access diagnostic failed independent replay.");
        foreach (var (a, b) in baseline.Buyers.Zip(region.Buyers))
            if (a.Buyer != b.Buyer || a.ResidentWorkers != b.ResidentWorkers ||
                !SameMoney(a.ProtectedH, b.ProtectedH) ||
                !a.Commodities.Select(x => (x.Commodity, x.PhysicalDemandRaw,
                    x.AuthorizedFreightOffers))
                .SequenceEqual(b.Commodities.Select(x =>
                    (x.Commodity, x.PhysicalDemandRaw, x.AuthorizedFreightOffers))))
                throw new InvalidOperationException(
                    "Shadow regional purchasing changed residents, protected H or physical demand.");
        RequireCurrent(_session);
    }

    public static CanonicalPrimitiveRegionalAccessDistribution FromPortfolio(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> candidateUses,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy baselinePolicy,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        CanonicalPrimitiveRegionalProductivityProjection regional,
        ulong rawQuantum, int maximumQuantaPerCandidate = 1,
        int maximumConfigurationsPerComponent = 4096)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidateUses);
        ArgumentNullException.ThrowIfNull(postedPrices);
        ArgumentNullException.ThrowIfNull(baselinePolicy);
        ArgumentNullException.ThrowIfNull(congestionPolicy);
        ArgumentNullException.ThrowIfNull(regional);
        portfolio.RequireCurrent(session);
        regional.RequireCurrent(session);
        var candidates = candidateUses.ToArray();
        var prices = postedPrices.ToArray();
        var comparison = portfolio.CompareRegionalPurchasing(
            candidates, prices, baselinePolicy, congestionPolicy,
            regional, rawQuantum, maximumQuantaPerCandidate,
            maximumConfigurationsPerComponent);
        var provisional = new CanonicalPrimitiveRegionalAccessDistribution(
            portfolio, session, regional, candidates, comparison,
            null!, null!);
        var baseline = provisional.Build(comparison.Baseline);
        var calibrated = provisional.Build(comparison.Outcomes.Single(x =>
            x.Scenario.ScenarioId == "regional-productivity"));
        var report = new CanonicalPrimitiveRegionalAccessDistribution(
            portfolio, session, regional, candidates, comparison,
            baseline, calibrated);
        report.VerifyIndependent();
        return report;
    }
}
