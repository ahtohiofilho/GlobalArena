using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Two ALTERNATIVE accounting interpretations of one unchanged VS74 physical
/// trial. The market-priced interpretation uses the ALREADY posted buyer price,
/// with zero local freight. The subsistence interpretation applies zero local
/// cash debit. Neither asserts that any local consumption was actually paid for.
/// </summary>
public sealed record CanonicalPrimitiveLocalBudgetShadowBuyer(
    EconomicPointId Buyer,
    FoodTransportCostFraction SpendablePurchasing,
    FoodTransportCostFraction ProtectedH,
    FoodTransportCostFraction AlreadySelectedFreightCommitment,
    FoodTransportCostFraction LocalMarketPriceQuote,
    FoodTransportCostFraction MarketPricedCombinedCommitment,
    bool MarketPricedCaseFitsH,
    bool NoLocalCashDebitCaseFitsH);

public sealed record CanonicalPrimitiveLocalBudgetShadowSnapshot(
    string ScenarioId,
    IReadOnlyList<CanonicalPrimitiveLocalBudgetShadowBuyer> Buyers,
    FoodTransportCostFraction TotalMarketPricedLocalQuote,
    FoodTransportCostFraction TotalSelectedFreightCommitment,
    FoodTransportCostFraction TotalMarketPricedCombinedCommitment,
    FoodTransportCostFraction SumFulfilledDemandFractions,
    FoodTransportCostFraction MeanFulfilledDemandFraction,
    FoodTransportCostFraction MinimumFulfilledDemandFraction,
    int BuyersWhoseMarketPricedCaseExceedsH);

/// <summary>
/// VS75: READ-ONLY counterfactual local accounting on an explicitly supplied
/// VS74 trial. No financial or social policy is approved: both local cash
/// interpretations are reported, not selected. H is never mutated. The
/// VS66 fulfilled-fraction metric is only observed, never optimized here.
/// </summary>
public sealed class CanonicalPrimitiveLocalBudgetShadowDiagnostics
{
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveJointPhysicalUsePreview _trial;
    private readonly CanonicalPrimitiveSourceReservationDiagnostics _sourceAudit;

    public CanonicalPrimitiveLocalBudgetShadowSnapshot Snapshot { get; }
    public bool IsMarketAccountingSelected => false;
    public bool IsFreeLocalUseAuthorized => false;
    public bool IsCoverageScoreAnApprovedSocialPolicy => false;
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;
    public bool IsActualConsumption => false;

    private CanonicalPrimitiveLocalBudgetShadowDiagnostics(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveJointPhysicalUsePreview trial,
        CanonicalPrimitiveSourceReservationDiagnostics sourceAudit,
        CanonicalPrimitiveLocalBudgetShadowSnapshot snapshot)
    {
        _session = session;
        _trial = trial;
        _sourceAudit = sourceAudit;
        Snapshot = snapshot;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Shadow local accounting belongs to another physical session.");
        _trial.RequireCurrent(session);
        _sourceAudit.RequireCurrent(session);
    }

    private static FoodTransportCostFraction Zero => new(0, 1);

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private CanonicalPrimitiveLocalBudgetShadowSnapshot Rebuild()
    {
        var regionalAccess = _sourceAudit.RegionalFreightAccess;
        var outcome = regionalAccess.Comparison.Outcomes.Single(x =>
            x.Scenario.ScenarioId == _trial.ScenarioId);
        var access = _trial.ScenarioId == regionalAccess.Baseline.ScenarioId
            ? regionalAccess.Baseline : regionalAccess.Regional;
        if (access.ScenarioId != _trial.ScenarioId)
            throw new InvalidOperationException(
                "Budget shadow must use one unmodified authenticated VS71 scenario.");

        var buyers = access.Buyers.ToDictionary(x => x.Buyer);
        var prices = outcome.Scenario.PostedPrices.ToDictionary(
            x => (x.Buyer, x.Commodity), x => x.PricePerWholeFlowUnit);
        var localRows = _trial.Buyers;
        if (localRows.Count != prices.Count ||
            localRows.Count != buyers.Values.Sum(x => x.Commodities.Count))
            throw new InvalidOperationException(
                "Budget shadow requires one posted price for each physical demand.");

        var quoteByBuyer = new Dictionary<EconomicPointId, FoodTransportCostFraction>();
        var sumCoverage = Zero;
        FoodTransportCostFraction? minimumCoverage = null;
        foreach (var row in localRows)
        {
            if (!buyers.TryGetValue(row.Buyer, out var original) ||
                !prices.TryGetValue((row.Buyer, row.Commodity), out var price) ||
                !original.Commodities.Any(x => x.Commodity == row.Commodity &&
                    x.PhysicalDemandRaw == row.PhysicalDemandRaw &&
                    x.PlannedFreightRaw == row.SelectedInboundFreightRaw) ||
                row.PhysicalDemandRaw == 0UL)
                throw new InvalidOperationException(
                    "Budget shadow lost live physical demand, posted price or selected freight.");

            var localQuote = ExperimentalCashMoney.Multiply(
                price, new BigInteger(row.ProposedLocalUseRaw),
                new BigInteger(EconomicTransferPolicy.FixedPointDenominator));
            quoteByBuyer.TryGetValue(row.Buyer, out var previous);
            quoteByBuyer[row.Buyer] = Add(previous ?? Zero, localQuote);

            var coverage = new FoodTransportCostFraction(
                checked(row.ProposedLocalUseRaw + row.SelectedInboundFreightRaw),
                row.PhysicalDemandRaw);
            sumCoverage = Add(sumCoverage, coverage);
            if (minimumCoverage is null ||
                coverage.CompareTo(minimumCoverage) < 0)
                minimumCoverage = coverage;
        }

        var rows = new List<CanonicalPrimitiveLocalBudgetShadowBuyer>();
        var quoteSum = Zero;
        var freightSum = Zero;
        var combinedSum = Zero;
        var overH = 0;
        foreach (var buyer in buyers.Values.OrderBy(x => x.Buyer.Value))
        {
            quoteByBuyer.TryGetValue(buyer.Buyer, out var localQuote);
            localQuote ??= Zero;
            var combined = Add(buyer.PlannedCommitment, localQuote);
            var fitsMarket = combined.CompareTo(buyer.SpendablePurchasing) <= 0;
            var fitsNoDebit = buyer.PlannedCommitment.CompareTo(
                buyer.SpendablePurchasing) <= 0;
            if (!fitsNoDebit)
                throw new InvalidOperationException(
                    "Previously audited VS71 freight exceeds protected H.");
            if (!fitsMarket) overH++;
            rows.Add(new(buyer.Buyer, buyer.SpendablePurchasing,
                buyer.ProtectedH, buyer.PlannedCommitment, localQuote,
                combined, fitsMarket, fitsNoDebit));
            quoteSum = Add(quoteSum, localQuote);
            freightSum = Add(freightSum, buyer.PlannedCommitment);
            combinedSum = Add(combinedSum, combined);
        }

        if (rows.Count != buyers.Count || localRows.Count == 0 ||
            sumCoverage.CompareTo(outcome.Selection.FulfilledDemandScore) < 0)
            throw new InvalidOperationException(
                "VS75 shadow coverage cannot erase already selected freight.");
        return new CanonicalPrimitiveLocalBudgetShadowSnapshot(
            _trial.ScenarioId, Array.AsReadOnly(rows.ToArray()), quoteSum,
            freightSum, combinedSum, sumCoverage,
            new FoodTransportCostFraction(sumCoverage.Numerator,
                sumCoverage.Denominator * localRows.Count),
            minimumCoverage!, overH);
    }

    private static bool SameMoney(
        FoodTransportCostFraction x, FoodTransportCostFraction y) =>
        x.CompareTo(y) == 0;

    private static bool SameSnapshot(
        CanonicalPrimitiveLocalBudgetShadowSnapshot x,
        CanonicalPrimitiveLocalBudgetShadowSnapshot y)
    {
        if (x.ScenarioId != y.ScenarioId ||
            x.Buyers.Count != y.Buyers.Count ||
            x.BuyersWhoseMarketPricedCaseExceedsH !=
                y.BuyersWhoseMarketPricedCaseExceedsH ||
            !SameMoney(x.TotalMarketPricedLocalQuote, y.TotalMarketPricedLocalQuote) ||
            !SameMoney(x.TotalSelectedFreightCommitment, y.TotalSelectedFreightCommitment) ||
            !SameMoney(x.TotalMarketPricedCombinedCommitment,
                y.TotalMarketPricedCombinedCommitment) ||
            !SameMoney(x.SumFulfilledDemandFractions, y.SumFulfilledDemandFractions) ||
            !SameMoney(x.MeanFulfilledDemandFraction,
                y.MeanFulfilledDemandFraction) ||
            !SameMoney(x.MinimumFulfilledDemandFraction,
                y.MinimumFulfilledDemandFraction))
            return false;

        foreach (var (a, b) in x.Buyers.Zip(y.Buyers))
            if (a.Buyer != b.Buyer ||
                !SameMoney(a.SpendablePurchasing, b.SpendablePurchasing) ||
                !SameMoney(a.ProtectedH, b.ProtectedH) ||
                !SameMoney(a.AlreadySelectedFreightCommitment,
                    b.AlreadySelectedFreightCommitment) ||
                !SameMoney(a.LocalMarketPriceQuote, b.LocalMarketPriceQuote) ||
                !SameMoney(a.MarketPricedCombinedCommitment,
                    b.MarketPricedCombinedCommitment) ||
                a.MarketPricedCaseFitsH != b.MarketPricedCaseFitsH ||
                a.NoLocalCashDebitCaseFitsH != b.NoLocalCashDebitCaseFitsH)
                return false;
        return true;
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        _trial.VerifyIndependent();
        _sourceAudit.VerifyIndependent();
        if (!SameSnapshot(Snapshot, Rebuild()))
            throw new InvalidOperationException(
                "Hypothetical local budget diagnostic failed independent replay.");
        RequireCurrent(_session);
    }

    internal static CanonicalPrimitiveLocalBudgetShadowDiagnostics FromTrial(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveJointPhysicalUsePreview trial,
        CanonicalPrimitiveSourceReservationDiagnostics sourceAudit)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(trial);
        ArgumentNullException.ThrowIfNull(sourceAudit);
        trial.VerifyIndependent();
        sourceAudit.VerifyIndependent();
        var provisional = new CanonicalPrimitiveLocalBudgetShadowDiagnostics(
            session, trial, sourceAudit, null!);
        var snapshot = provisional.Rebuild();
        var result = new CanonicalPrimitiveLocalBudgetShadowDiagnostics(
            session, trial, sourceAudit, snapshot);
        result.VerifyIndependent();
        return result;
    }
}
