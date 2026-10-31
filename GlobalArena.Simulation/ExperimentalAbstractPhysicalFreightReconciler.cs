using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A selected hypothetical whole-service shipment. Physical load is computed
/// from the canonical commodity catalog, never from a caller's arbitrary
/// payload or troop movement points. The selected service must already have
/// been declared in the VS52 physical service provenance.
/// </summary>
public sealed record ExperimentalAbstractPhysicalFreightDelivery(
    ulong LotId,
    EconomicPointId Source,
    EconomicActivityKind Activity,
    EconomicPointId Buyer,
    CommodityId Commodity,
    ulong RawQuantity,
    ExperimentalSharedFreightOption Service);

public sealed record ExperimentalAbstractPhysicalFreightCharge(
    ulong LotId,
    EconomicPointId Source,
    EconomicPointId Buyer,
    CommodityId Commodity,
    ulong ServiceId,
    ulong RawQuantity,
    BigInteger PhysicalLoadMicrograms,
    FoodTransportCostFraction NominalUncongestedFreight,
    FoodTransportCostFraction NominalSharedSaturationFreight,
    FoodTransportCostFraction AbstractPhysicalFreight,
    FoodTransportCostFraction AbstractCommodityCharge);

public sealed record ExperimentalAbstractPhysicalFreightBuyer(
    EconomicPointId Buyer,
    FoodTransportCostFraction GrossAbstractPurchasing,
    FoodTransportCostFraction ProtectedInvisibleH,
    FoodTransportCostFraction SpendableAbstractPurchasing,
    FoodTransportCostFraction AbstractCommodityCharge,
    FoodTransportCostFraction AbstractEstimatedFreightForSelectedLots,
    FoodTransportCostFraction AbstractPhysicalFreight,
    FoodTransportCostFraction AbstractDeliveredCharge,
    bool CanCoverHypotheticalCharge,
    FoodTransportCostFraction RemainingIfAffordable);

public sealed record ExperimentalAbstractPhysicalFreightReconciliation(
    ExperimentalAbstractPriceFreightCalibrationResult Calibration,
    IReadOnlyList<ExperimentalAbstractPhysicalFreightCharge> Deliveries,
    IReadOnlyList<ExperimentalAbstractPhysicalFreightBuyer> Buyers,
    IReadOnlyDictionary<ExperimentalSharedFreightCorridorKey, BigInteger> CorridorLoads,
    FoodTransportCostFraction NominalPhysicalFreight,
    FoodTransportCostFraction AbstractPhysicalFreight,
    FoodTransportCostFraction AbstractCommodityCharges,
    FoodTransportCostFraction AbstractEstimatedFreightForSelectedLots,
    FoodTransportCostFraction AbstractDeliveredCharges,
    bool AllBuyersCanCoverHypotheticalCharges)
{
    /// <summary>
    /// Neither a quote, a hypothetical affordability test nor a selected
    /// built route proves legal ownership of a freight service or a real
    /// account to credit. No authorization or actual SETTLE is permitted.
    /// </summary>
    public bool CanAuthorizeRealSettlement => false;
}

/// <summary>
/// Reconciles the ACTUAL VS42 batch algorithm (VS40 geography + overhead,
/// ONCE-per-shared-corridor saturation) against a common VS53 abstract quote
/// unit. This is an ex-post projection of a caller-selected hypothetical
/// batch, NOT a dispatch, a market-clearing result or a financial settlement.
/// Real offers must have passed VS42 FromBuiltOffer externally; synthetic
/// options remain acceptable ONLY for deterministic laboratory fixtures.
/// </summary>
public sealed class ExperimentalAbstractPhysicalFreightReconciler
{
    private static readonly BigInteger RawUnit =
        EconomicTransferPolicy.FixedPointDenominator;
    private static readonly BigInteger MicrogramsPerGram = 1_000_000;

    public ExperimentalAbstractPhysicalFreightReconciliation Project(
        ExperimentalAbstractPurchasingProjection purchasing,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog commodityCatalog,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> nominalQuotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy calibrationPolicy,
        string physicalFreightNominalUnitId,
        IEnumerable<ExperimentalAbstractPhysicalFreightDelivery> selectedDeliveries,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy)
    {
        ArgumentNullException.ThrowIfNull(purchasing);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(commodityCatalog);
        ArgumentNullException.ThrowIfNull(nominalQuotes);
        ArgumentNullException.ThrowIfNull(calibrationPolicy);
        ArgumentNullException.ThrowIfNull(selectedDeliveries);
        ArgumentNullException.ThrowIfNull(congestionPolicy);

        // VS40/VS42 pricing carries no native currency tag. Requiring the
        // caller's explicit cost-basis ID prevents SILENT unit assumptions;
        // it does not attest that external numeric calibration is truthful.
        if (string.IsNullOrWhiteSpace(physicalFreightNominalUnitId) ||
            !string.Equals(physicalFreightNominalUnitId,
                calibrationPolicy.NominalQuoteUnitId, StringComparison.Ordinal))
            throw new ArgumentException(
                "Physical freight costs and quoted prices must explicitly share the same nominal unit.",
                nameof(physicalFreightNominalUnitId));

        // Recompute the VS53 projection from original inputs to prevent a
        // detached or forged partial quote/alpha result from being re-used.
        var calibration = new ExperimentalAbstractPriceFreightCalibrator()
            .Project(purchasing, flows, nominalQuotes, calibrationPolicy);

        var supply = flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity),
            x => new BigInteger(x.RawQuantity));
        var demand = flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId),
            x => new BigInteger(x.RawQuantity));
        var knownCarriers = purchasing.UnresolvedCarriers
            .Select(x => (x.Source, x.Destination, x.ServiceId)).ToHashSet();
        if (knownCarriers.Count != purchasing.UnresolvedCarriers.Count)
            throw new ArgumentException(
                "The physical provenance cannot contain duplicate service identities.",
                nameof(purchasing));

        var quotes = calibration.Quotes.ToDictionary(
            x => (x.Buyer, x.Commodity));
        var batches = new List<ExperimentalSharedFreightLot>();
        var shipmentRows = new List<(ExperimentalAbstractPhysicalFreightDelivery Delivery,
            BigInteger Load, FoodTransportCostFraction Commodity,
            FoodTransportCostFraction EstimatedFreight)>();
        var usedSupply = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), BigInteger>();
        var usedDemand = new Dictionary<(EconomicPointId, CommodityId), BigInteger>();
        var lotIds = new HashSet<ulong>();

        foreach (var shipment in selectedDeliveries)
        {
            if (shipment is null || shipment.LotId == 0 ||
                !lotIds.Add(shipment.LotId) || shipment.RawQuantity == 0 ||
                shipment.Service is null ||
                !shipment.Source.IsValid || !shipment.Buyer.IsValid ||
                !shipment.Commodity.IsValid ||
                !knownCarriers.Contains((
                    shipment.Source, shipment.Buyer, shipment.Service.ServiceId)) ||
                shipment.Service.RouteKey.SourceEconomicPointId != shipment.Source ||
                shipment.Service.RouteKey.DestinationEconomicPointId != shipment.Buyer ||
                !quotes.TryGetValue((shipment.Buyer, shipment.Commodity), out var quote) ||
                !commodityCatalog.TryGet(shipment.Commodity, out var commodity) ||
                commodity is null || commodity.GramsPerFlowUnit == 0)
                throw new ArgumentException(
                    "Every selected lot needs a unique ID, declared buyer/source/service, matched commodity and physical mass.",
                    nameof(selectedDeliveries));

            var sourceKey = (shipment.Source, shipment.Commodity, shipment.Activity);
            var buyerKey = (shipment.Buyer, shipment.Commodity);
            if (!supply.TryGetValue(sourceKey, out var available) ||
                !demand.TryGetValue(buyerKey, out var requested))
                throw new ArgumentException(
                    "Selected physical freight cannot invent offered supply or requested demand.",
                    nameof(selectedDeliveries));
            usedSupply.TryGetValue(sourceKey, out var sourced);
            usedDemand.TryGetValue(buyerKey, out var consumed);
            sourced += shipment.RawQuantity;
            consumed += shipment.RawQuantity;
            if (sourced > available || consumed > requested)
                throw new ArgumentException(
                    "Physical freight cannot over-allocate supply or buyer demand.",
                    nameof(selectedDeliveries));
            usedSupply[sourceKey] = sourced;
            usedDemand[buyerKey] = consumed;

            // RawQuantity is fixed-point; grams/unit * RawQuantity gives
            // whole physical micrograms without a rounding conversion.
            var load = (BigInteger)shipment.RawQuantity * commodity.GramsPerFlowUnit;
            batches.Add(new ExperimentalSharedFreightLot(
                shipment.LotId, load, new[] { shipment.Service }));
            shipmentRows.Add((shipment, load,
                ExperimentalCashMoney.Multiply(
                    quote.CommodityUnitPriceAbstract, shipment.RawQuantity, RawUnit),
                ExperimentalCashMoney.Multiply(
                    quote.EstimatedFreightPerWholeUnitAbstract,
                    shipment.RawQuantity, RawUnit)));
        }

        // Stable lot ID order makes the diagnostic transcript independent of
        // enumerator ordering; VS42 itself aggregates corridor loads exactly.
        shipmentRows.Sort((a, b) => a.Delivery.LotId.CompareTo(b.Delivery.LotId));
        batches.Sort((a, b) => a.LotId.CompareTo(b.LotId));
        var actual = ExperimentalSharedFreightBatchCost.Evaluate(
            batches, new int[batches.Count], congestionPolicy);

        FoodTransportCostFraction ToAbstract(FoodTransportCostFraction nominal) =>
            ExperimentalCashMoney.Multiply(nominal,
                calibrationPolicy.AbstractUnitsPerNominalQuoteUnit.Numerator,
                calibrationPolicy.AbstractUnitsPerNominalQuoteUnit.Denominator);

        var charges = new List<ExperimentalAbstractPhysicalFreightCharge>();
        var actualNominalSum = ExperimentalCashMoney.Zero;
        var actualAbstractSum = ExperimentalCashMoney.Zero;
        var goodsSum = ExperimentalCashMoney.Zero;
        var estimatedSum = ExperimentalCashMoney.Zero;
        foreach (var row in shipmentRows)
        {
            var baseFreight = ExperimentalSharedFreightBatchCost.UncongestedCost(
                row.Load, row.Delivery.Service.Pricing);
            var congestion = ExperimentalCashMoney.Zero;
            foreach (var crossing in row.Delivery.Service.Corridors)
            {
                if (!actual.CorridorLoads.TryGetValue(crossing, out var sharedLoad) ||
                    sharedLoad < row.Load)
                    throw new InvalidOperationException(
                        "A selected shipment must contribute to its shared corridor's full physical load.");
                // Exactly q_i / Q of ONE C(Q); equivalently weight*q_i*Q^2.
                // Every crossing is charged once per shipment, no independent
                // VS40 q^2 saturation is added on top of VS42 congestion.
                var share = new FoodTransportCostFraction(
                    congestionPolicy.SaturationWeight * row.Load *
                        sharedLoad * sharedLoad,
                    MicrogramsPerGram *
                        congestionPolicy.SaturationScaleMicrograms *
                        congestionPolicy.SaturationScaleMicrograms);
                congestion = ExperimentalCashMoney.Add(congestion, share);
            }
            var nominalFreight = ExperimentalCashMoney.Add(baseFreight, congestion);
            var abstractFreight = ToAbstract(nominalFreight);
            actualNominalSum = ExperimentalCashMoney.Add(actualNominalSum, nominalFreight);
            actualAbstractSum = ExperimentalCashMoney.Add(actualAbstractSum, abstractFreight);
            goodsSum = ExperimentalCashMoney.Add(goodsSum, row.Commodity);
            estimatedSum = ExperimentalCashMoney.Add(estimatedSum, row.EstimatedFreight);
            charges.Add(new ExperimentalAbstractPhysicalFreightCharge(
                row.Delivery.LotId, row.Delivery.Source, row.Delivery.Buyer,
                row.Delivery.Commodity, row.Delivery.Service.ServiceId,
                row.Delivery.RawQuantity, row.Load, baseFreight, congestion,
                abstractFreight, row.Commodity));
        }
        if (actualNominalSum.CompareTo(actual.TotalCost) != 0 ||
            actualAbstractSum.CompareTo(ToAbstract(actual.TotalCost)) != 0)
            throw new InvalidOperationException(
                "Distributed freight charges must equal the VS42 one-time corridor aggregate.");

        var buyers = new List<ExperimentalAbstractPhysicalFreightBuyer>();
        var allAffordable = true;
        foreach (var buyer in purchasing.Buyers.OrderBy(x => x.Buyer.Value))
        {
            var item = ExperimentalCashMoney.Zero;
            var shipping = ExperimentalCashMoney.Zero;
            var estimate = ExperimentalCashMoney.Zero;
            foreach (var charge in charges.Where(x => x.Buyer == buyer.Buyer))
            {
                item = ExperimentalCashMoney.Add(item, charge.AbstractCommodityCharge);
                shipping = ExperimentalCashMoney.Add(shipping, charge.AbstractPhysicalFreight);
            }
            foreach (var shipment in shipmentRows.Where(
                         x => x.Delivery.Buyer == buyer.Buyer))
                estimate = ExperimentalCashMoney.Add(
                    estimate, shipment.EstimatedFreight);
            var total = ExperimentalCashMoney.Add(item, shipping);
            var budget = buyer.TemporaryCycleBudget;
            var affordable = total.CompareTo(budget.SpendableCash) <= 0;
            allAffordable &= affordable;
            buyers.Add(new ExperimentalAbstractPhysicalFreightBuyer(
                buyer.Buyer, budget.GrossCash, budget.ProtectedResidual,
                budget.SpendableCash, item, estimate, shipping, total,
                affordable, affordable
                    ? ExperimentalCashMoney.Subtract(budget.SpendableCash, total)
                    : ExperimentalCashMoney.Zero));
        }

        var totalDelivered = ExperimentalCashMoney.Add(goodsSum, actualAbstractSum);
        var recomputedGoods = ExperimentalCashMoney.Zero;
        var recomputedFreight = ExperimentalCashMoney.Zero;
        foreach (var row in buyers)
        {
            recomputedGoods = ExperimentalCashMoney.Add(
                recomputedGoods, row.AbstractCommodityCharge);
            recomputedFreight = ExperimentalCashMoney.Add(
                recomputedFreight, row.AbstractPhysicalFreight);
        }
        if (recomputedGoods.CompareTo(goodsSum) != 0 ||
            recomputedFreight.CompareTo(actualAbstractSum) != 0)
            throw new InvalidOperationException(
                "Per-buyer charges must reconcile without loss or duplicate freight.");

        return new ExperimentalAbstractPhysicalFreightReconciliation(
            calibration, Array.AsReadOnly(charges.ToArray()),
            Array.AsReadOnly(buyers.ToArray()), actual.CorridorLoads,
            actual.TotalCost, actualAbstractSum, goodsSum, estimatedSum,
            totalDelivered, allAffordable);
    }
}
