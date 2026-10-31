using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Scenario-specific reference unit. Exactly ONE positive conversion factor
/// applies to posted commodity prices AND estimated freight. No V14 wallet,
/// exchange account, salary or physical freight tariff is modified.
/// </summary>
public sealed class ExperimentalAbstractPriceFreightCalibrationPolicy
{
    public string CalibrationId { get; }
    public ulong Version { get; }
    public string PurchasingPolicyId { get; }
    public ulong PurchasingPolicyVersion { get; }
    public string NominalQuoteUnitId { get; }
    public FoodTransportCostFraction AbstractUnitsPerNominalQuoteUnit { get; }

    public ExperimentalAbstractPriceFreightCalibrationPolicy(
        string calibrationId, ulong version,
        string purchasingPolicyId, ulong purchasingPolicyVersion,
        string nominalQuoteUnitId,
        FoodTransportCostFraction abstractUnitsPerNominalQuoteUnit)
    {
        if (string.IsNullOrWhiteSpace(calibrationId) || version == 0 ||
            string.IsNullOrWhiteSpace(purchasingPolicyId) ||
            purchasingPolicyVersion == 0 ||
            string.IsNullOrWhiteSpace(nominalQuoteUnitId))
            throw new ArgumentException(
                "Calibration and purchasing policies and quote units need explicit stable IDs/versions.");
        AbstractUnitsPerNominalQuoteUnit =
            abstractUnitsPerNominalQuoteUnit ??
            throw new ArgumentNullException(nameof(abstractUnitsPerNominalQuoteUnit));
        if (AbstractUnitsPerNominalQuoteUnit.Numerator.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(abstractUnitsPerNominalQuoteUnit),
                "The common nominal-to-abstract unit factor must be positive.");

        CalibrationId = calibrationId;
        Version = version;
        PurchasingPolicyId = purchasingPolicyId;
        PurchasingPolicyVersion = purchasingPolicyVersion;
        NominalQuoteUnitId = nominalQuoteUnitId;
    }
}

/// <summary>
/// Ex-ante reference quote, not an actual cargo booking or a certified tariff.
/// Freight includes the anticipated full delivered cost PER whole flow unit;
/// shared corridor congestion can change at simultaneous allocation time.
/// </summary>
public sealed record ExperimentalAbstractNominalDeliveredQuote(
    EconomicPointId Buyer,
    CommodityId Commodity,
    FoodTransportCostFraction NominalCommodityUnitPrice,
    FoodTransportCostFraction NominalEstimatedFreightPerWholeUnit,
    string NominalQuoteUnitId);

public sealed record ExperimentalAbstractCalibratedDeliveredQuote(
    EconomicPointId Buyer,
    CommodityId Commodity,
    FoodTransportCostFraction CommodityUnitPriceAbstract,
    FoodTransportCostFraction EstimatedFreightPerWholeUnitAbstract);

public sealed record ExperimentalAbstractPriceFreightCalibrationResult(
    string CalibrationId,
    ulong CalibrationVersion,
    string PurchasingPolicyId,
    ulong PurchasingPolicyVersion,
    string NominalQuoteUnitId,
    FoodTransportCostFraction AbstractUnitsPerNominalQuoteUnit,
    IReadOnlyList<ExperimentalAbstractCalibratedDeliveredQuote> Quotes,
    ExperimentalAlphaDemandPlan AlphaDemand,
    FoodTransportCostFraction TotalBaselineCommodityUnits,
    FoodTransportCostFraction TotalBaselineFreightUnits,
    FoodTransportCostFraction TotalBaselineDeliveredUnits,
    FoodTransportCostFraction TotalProtectedInvisibleUnits,
    FoodTransportCostFraction TotalSpendablePurchasingUnits,
    int UnresolvedTransportServiceCount)
{
    // VS53 is a read-only scenario comparator, not a financial authorization.
    // Even zero unresolved services does NOT make a quote a settlement contract.
    public bool CanAuthorizeRealSettlement => false;
}

/// <summary>
/// Reconciles physical-resident-derived purchasing units (VS52) with one
/// declared nominal quote basis used consistently for goods and freight.
/// Converts prices, NOT purchasing power into monetary property. VS45 alpha
/// still forecasts demand; VS44/VS46/VS47/VS48 physical clearing and SETTLE
/// are neither invoked nor authorized. All arithmetic is exact.
/// </summary>
public sealed class ExperimentalAbstractPriceFreightCalibrator
{
    private static readonly BigInteger RawUnit =
        EconomicTransferPolicy.FixedPointDenominator;

    public ExperimentalAbstractPriceFreightCalibrationResult Project(
        ExperimentalAbstractPurchasingProjection purchasing,
        EconomicProductionDemandResult flows,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> nominalQuotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(purchasing);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(nominalQuotes);
        ArgumentNullException.ThrowIfNull(policy);

        if (!string.Equals(purchasing.PolicyId, policy.PurchasingPolicyId,
                StringComparison.Ordinal) ||
            purchasing.PolicyVersion != policy.PurchasingPolicyVersion)
            throw new ArgumentException(
                "Calibration must be pinned to the originating abstract purchasing policy/version.",
                nameof(policy));

        var demandKeys = flows.DemandFlows
            .Select(x => (x.EconomicPointId, x.CommodityId)).ToHashSet();
        var buyers = flows.DemandFlows.Select(x => x.EconomicPointId).ToHashSet();
        if (!purchasing.Buyers.Select(x => x.Buyer).ToHashSet().SetEquals(buyers) ||
            purchasing.Buyers.Select(x => x.Buyer).Distinct().Count() !=
                purchasing.Buyers.Count)
            throw new ArgumentException(
                "Calibrated demand must use exactly the anchored VS52 buyers.",
                nameof(flows));

        var byKey = new Dictionary<(EconomicPointId, CommodityId),
            ExperimentalAbstractNominalDeliveredQuote>();
        foreach (var quote in nominalQuotes)
        {
            if (quote is null || !quote.Buyer.IsValid ||
                !quote.Commodity.IsValid ||
                quote.NominalCommodityUnitPrice is null ||
                quote.NominalEstimatedFreightPerWholeUnit is null ||
                !string.Equals(quote.NominalQuoteUnitId,
                    policy.NominalQuoteUnitId, StringComparison.Ordinal) ||
                !byKey.TryAdd((quote.Buyer, quote.Commodity), quote))
                throw new ArgumentException(
                    "Every nominal quote requires a unique buyer/item and the declared common unit.",
                    nameof(nominalQuotes));
        }
        if (!byKey.Keys.ToHashSet().SetEquals(demandKeys))
            throw new ArgumentException(
                "Nominal goods and freight estimates must cover every demand item, without extras.",
                nameof(nominalQuotes));

        FoodTransportCostFraction Convert(FoodTransportCostFraction nominal) =>
            ExperimentalCashMoney.Multiply(nominal,
                policy.AbstractUnitsPerNominalQuoteUnit.Numerator,
                policy.AbstractUnitsPerNominalQuoteUnit.Denominator);

        var estimates = new List<ExperimentalDeliveredDemandEstimate>();
        var calibrated = new List<ExperimentalAbstractCalibratedDeliveredQuote>();
        var goods = ExperimentalCashMoney.Zero;
        var freight = ExperimentalCashMoney.Zero;
        foreach (var key in byKey.Keys.OrderBy(x => x.Item1.Value)
                     .ThenBy(x => x.Item2.Value))
        {
            var quote = byKey[key];
            var commodity = Convert(quote.NominalCommodityUnitPrice);
            var shipping = Convert(quote.NominalEstimatedFreightPerWholeUnit);
            calibrated.Add(new ExperimentalAbstractCalibratedDeliveredQuote(
                quote.Buyer, quote.Commodity, commodity, shipping));
            estimates.Add(new ExperimentalDeliveredDemandEstimate(
                quote.Buyer, quote.Commodity, commodity, shipping));
        }

        foreach (var request in flows.DemandFlows)
        {
            var quote = byKey[(request.EconomicPointId, request.CommodityId)];
            goods = ExperimentalCashMoney.Add(goods, ExperimentalCashMoney.Multiply(
                Convert(quote.NominalCommodityUnitPrice),
                request.RawQuantity, RawUnit));
            freight = ExperimentalCashMoney.Add(freight, ExperimentalCashMoney.Multiply(
                Convert(quote.NominalEstimatedFreightPerWholeUnit),
                request.RawQuantity, RawUnit));
        }

        var budgets = purchasing.Buyers
            .Select(x => x.TemporaryCycleBudget).ToArray();
        var gross = ExperimentalCashMoney.Zero;
        var protectedH = ExperimentalCashMoney.Zero;
        var spendable = ExperimentalCashMoney.Zero;
        foreach (var budget in budgets)
        {
            gross = ExperimentalCashMoney.Add(gross, budget.GrossCash);
            protectedH = ExperimentalCashMoney.Add(protectedH, budget.ProtectedResidual);
            spendable = ExperimentalCashMoney.Add(spendable, budget.SpendableCash);
        }
        if (gross.CompareTo(purchasing.TotalTemporaryPurchasingUnits) != 0 ||
            gross.CompareTo(ExperimentalCashMoney.Add(protectedH, spendable)) != 0)
            throw new InvalidOperationException(
                "Abstract purchasing projection is not internally conserved.");

        var total = ExperimentalCashMoney.Add(goods, freight);
        var alpha = new ExperimentalCashDemandAlphaPlanner()
            .Project(flows, budgets, estimates);
        var alphaBaseline = ExperimentalCashMoney.Zero;
        foreach (var buyer in alpha.Buyers)
            alphaBaseline = ExperimentalCashMoney.Add(
                alphaBaseline, buyer.BaselineEstimatedSpending);
        if (alphaBaseline.CompareTo(total) != 0)
            throw new InvalidOperationException(
                "Calibrated delivered quotes did not reconcile to alpha baseline.");

        return new ExperimentalAbstractPriceFreightCalibrationResult(
            policy.CalibrationId, policy.Version,
            policy.PurchasingPolicyId, policy.PurchasingPolicyVersion,
            policy.NominalQuoteUnitId, policy.AbstractUnitsPerNominalQuoteUnit,
            Array.AsReadOnly(calibrated.ToArray()), alpha,
            goods, freight, total, protectedH, spendable,
            purchasing.UnresolvedCarriers.Count);
    }
}
