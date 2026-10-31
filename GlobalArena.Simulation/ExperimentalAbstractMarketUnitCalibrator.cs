using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit, versioned exchange of external ex-ante price and freight units
/// into the ABSTRACT purchasing unit supplied by a VS52 policy. These are
/// conversion factors, NOT money creation, FX trades, salaries or accounts.
/// </summary>
public sealed class ExperimentalAbstractMarketUnitCalibration
{
    public string CalibrationId { get; }
    public ulong Version { get; }
    public string PurchasingPolicyId { get; }
    public ulong PurchasingPolicyVersion { get; }
    public string AbstractUnitId { get; }
    public string CommodityQuoteUnitId { get; }
    public string FreightQuoteUnitId { get; }
    public FoodTransportCostFraction AbstractUnitsPerCommodityQuoteUnit { get; }
    public FoodTransportCostFraction AbstractUnitsPerFreightQuoteUnit { get; }

    public ExperimentalAbstractMarketUnitCalibration(
        string calibrationId, ulong version,
        string purchasingPolicyId, ulong purchasingPolicyVersion,
        string abstractUnitId, string commodityQuoteUnitId,
        string freightQuoteUnitId,
        FoodTransportCostFraction abstractUnitsPerCommodityQuoteUnit,
        FoodTransportCostFraction abstractUnitsPerFreightQuoteUnit)
    {
        if (new[] { calibrationId, purchasingPolicyId, abstractUnitId,
                commodityQuoteUnitId, freightQuoteUnitId }
                .Any(string.IsNullOrWhiteSpace) ||
            version == 0 || purchasingPolicyVersion == 0)
            throw new ArgumentException(
                "Calibrations require nonempty domain names and explicit positive versions.");
        AbstractUnitsPerCommodityQuoteUnit =
            abstractUnitsPerCommodityQuoteUnit ??
            throw new ArgumentNullException(nameof(abstractUnitsPerCommodityQuoteUnit));
        AbstractUnitsPerFreightQuoteUnit =
            abstractUnitsPerFreightQuoteUnit ??
            throw new ArgumentNullException(nameof(abstractUnitsPerFreightQuoteUnit));
        if (AbstractUnitsPerCommodityQuoteUnit.Numerator.Sign <= 0 ||
            AbstractUnitsPerFreightQuoteUnit.Numerator.Sign <= 0)
            throw new ArgumentException(
                "The monetary/abstract unit factors must be strictly positive.");
        CalibrationId = calibrationId;
        Version = version;
        PurchasingPolicyId = purchasingPolicyId;
        PurchasingPolicyVersion = purchasingPolicyVersion;
        AbstractUnitId = abstractUnitId;
        CommodityQuoteUnitId = commodityQuoteUnitId;
        FreightQuoteUnitId = freightQuoteUnitId;
    }

    public void Verify(ExperimentalAbstractPurchasingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (policy.PolicyId != PurchasingPolicyId ||
            policy.Version != PurchasingPolicyVersion)
            throw new InvalidOperationException(
                "A calibration cannot be applied to a different purchasing policy/version.");
    }
}

/// <summary>
/// External EX-ANTE service-specific freight estimate per WHOLE economic
/// flow unit, in the declared native freight quote unit. Its service must
/// exist in the supplied VS42 itinerary/lane for the exact commodity.
/// Price is NOT certified as the final jointly congested freight charge.
/// </summary>
public sealed record ExperimentalNativeFreightPriceEstimate(
    EconomicPointId Source, EconomicPointId Buyer, CommodityId Commodity,
    ulong ServiceId, ulong PricingRulesetVersion,
    FoodTransportCostFraction NativeFreightPerWholeFlowUnit);

public sealed record ExperimentalCalibratedMarketPrice(
    EconomicPointId Buyer, CommodityId Commodity,
    ExperimentalUnresolvedTransportOperator FreightService,
    FoodTransportCostFraction AbstractCommodityPerWholeFlowUnit,
    FoodTransportCostFraction AbstractFreightPerWholeFlowUnit,
    FoodTransportCostFraction AbstractDeliveredPerWholeFlowUnit);

public sealed record ExperimentalAbstractUnitMarketPreview(
    ExperimentalAbstractPurchasingProjection Purchasing,
    IReadOnlyList<ExperimentalCalibratedMarketPrice> DeliveredPrices,
    ExperimentalAlphaDemandPlan Alpha,
    string CalibrationId, ulong CalibrationVersion, string AbstractUnitId)
{
    public IReadOnlyList<ExperimentalUnresolvedTransportOperator>
        UnresolvedFreightBeneficiaries => Purchasing.UnresolvedCarriers;
}

/// <summary>
/// VS53: normalize native quoted goods/freight prices into the SAME
/// hypothetical abstract units as a population-derived temporary budget,
/// then run VS45 proportional alpha. No settlement, no inferred ownership,
/// no persistent wallets and no assertion that a quoted route is actually
/// built/available: the caller owns the VS42 offer-attestation boundary.
/// </summary>
public sealed class ExperimentalAbstractMarketUnitCalibrator
{
    public ExperimentalAbstractUnitMarketPreview Preview(
        CanonicalPhysicalTacticalSession session,
        EconomicProductionDemandResult flows,
        IEnumerable<ExperimentalAbstractBuyerAnchor> buyerAnchors,
        IEnumerable<ExperimentalAbstractProducerAnchor> producerAnchors,
        IEnumerable<ExperimentalSimultaneousFreightLane> offeredLanes,
        IEnumerable<ExperimentalBuyerCommodityPrice> nativeCommodityPrices,
        IEnumerable<ExperimentalNativeFreightPriceEstimate> nativeFreight,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        ExperimentalAbstractMarketUnitCalibration calibration)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(buyerAnchors);
        ArgumentNullException.ThrowIfNull(producerAnchors);
        ArgumentNullException.ThrowIfNull(offeredLanes);
        ArgumentNullException.ThrowIfNull(nativeCommodityPrices);
        ArgumentNullException.ThrowIfNull(nativeFreight);
        ArgumentNullException.ThrowIfNull(purchasingPolicy);
        ArgumentNullException.ThrowIfNull(calibration);
        calibration.Verify(purchasingPolicy);

        var requested = flows.DemandFlows
            .Select(x => (Buyer: x.EconomicPointId, Commodity: x.CommodityId))
            .ToHashSet();
        var goods = new Dictionary<(EconomicPointId, CommodityId),
            ExperimentalBuyerCommodityPrice>();
        foreach (var quote in nativeCommodityPrices)
        {
            if (quote is null ||
                !goods.TryAdd((quote.Buyer, quote.Commodity), quote))
                throw new ArgumentException(
                    "Native commodity quotes must be non-null and unique.",
                    nameof(nativeCommodityPrices));
        }
        if (!goods.Keys.ToHashSet().SetEquals(requested))
            throw new ArgumentException(
                "Every requested buyer/commodity requires exactly one native commodity quote.",
                nameof(nativeCommodityPrices));

        var lanes = offeredLanes.ToArray();
        if (lanes.Any(x => x is null))
            throw new ArgumentException(
                "Declared lanes cannot contain null.", nameof(offeredLanes));
        // Key is (source, buyer, commodity, service, pricing ruleset).
        // Duplicate economic service definitions must not silently replace
        // an alternative or permit a fraudulent freight quote.
        var available = new HashSet<(
            EconomicPointId Source, EconomicPointId Buyer, CommodityId Commodity,
            ulong Service, ulong Ruleset)>();
        foreach (var lane in lanes)
            foreach (var option in lane.Services)
            {
                var key = (lane.Supply.EconomicPointId,
                    lane.Demand.EconomicPointId, lane.Demand.CommodityId,
                    option.ServiceId, option.Pricing.RulesetVersion);
                if (!available.Add(key))
                    throw new ArgumentException(
                        "Every native route/ruleset must identify one unambiguous offered service.",
                        nameof(offeredLanes));
            }

        var estimates = new Dictionary<(EconomicPointId, CommodityId),
            ExperimentalNativeFreightPriceEstimate>();
        foreach (var quote in nativeFreight)
        {
            if (quote is null || !quote.Source.IsValid || !quote.Buyer.IsValid ||
                !quote.Commodity.IsValid || quote.ServiceId == 0 ||
                quote.PricingRulesetVersion == 0 ||
                quote.NativeFreightPerWholeFlowUnit is null ||
                !available.Contains((quote.Source, quote.Buyer, quote.Commodity,
                    quote.ServiceId, quote.PricingRulesetVersion)) ||
                !estimates.TryAdd((quote.Buyer, quote.Commodity), quote))
                throw new ArgumentException(
                    "Each item needs one sourced ex-ante freight quote for an explicitly offered, versioned service.",
                    nameof(nativeFreight));
        }
        if (!estimates.Keys.ToHashSet().SetEquals(requested))
            throw new ArgumentException(
                "Each buyer/commodity requires one freight estimate; never assume unquoted freight is free.",
                nameof(nativeFreight));

        FoodTransportCostFraction Convert(
            FoodTransportCostFraction native,
            FoodTransportCostFraction factor) =>
            ExperimentalCashMoney.Multiply(
                native, factor.Numerator, factor.Denominator);

        // VS52 accepts posted prices in the same units as the abstract
        // purchasing policy. Convert BEFORE creating its cash snapshot.
        var normalizedGoods = goods.Values
            .OrderBy(x => x.Buyer.Value).ThenBy(x => x.Commodity.Value)
            .Select(x => new ExperimentalBuyerCommodityPrice(
                x.Buyer, x.Commodity,
                Convert(x.PricePerWholeFlowUnit,
                    calibration.AbstractUnitsPerCommodityQuoteUnit)))
            .ToArray();
        var physical = new ExperimentalAbstractPurchasingPowerProjector()
            .Project(session, flows, buyerAnchors, producerAnchors, lanes,
                normalizedGoods, purchasingPolicy);

        var prices = new List<ExperimentalCalibratedMarketPrice>();
        var estimatesForAlpha = new List<ExperimentalDeliveredDemandEstimate>();
        foreach (var (key, good) in goods.OrderBy(x => x.Key.Item1.Value)
            .ThenBy(x => x.Key.Item2.Value))
        {
            var freight = estimates[key];
            var commodityAbstract = Convert(good.PricePerWholeFlowUnit,
                calibration.AbstractUnitsPerCommodityQuoteUnit);
            var freightAbstract = Convert(
                freight.NativeFreightPerWholeFlowUnit,
                calibration.AbstractUnitsPerFreightQuoteUnit);
            var carrier = new ExperimentalUnresolvedTransportOperator(
                freight.Source, freight.Buyer, freight.ServiceId);
            if (!physical.UnresolvedCarriers.Contains(carrier))
                throw new InvalidOperationException(
                    "The selected quote has no unresolved physical carrier identity.");
            prices.Add(new ExperimentalCalibratedMarketPrice(
                good.Buyer, good.Commodity, carrier,
                commodityAbstract, freightAbstract,
                ExperimentalCashMoney.Add(commodityAbstract, freightAbstract)));
            estimatesForAlpha.Add(new ExperimentalDeliveredDemandEstimate(
                good.Buyer, good.Commodity, commodityAbstract,
                freightAbstract));
        }
        var alpha = new ExperimentalCashDemandAlphaPlanner().Project(
            flows, physical.Buyers.Select(x => x.TemporaryCycleBudget),
            estimatesForAlpha);
        return new ExperimentalAbstractUnitMarketPreview(
            physical, Array.AsReadOnly(prices.ToArray()), alpha,
            calibration.CalibrationId, calibration.Version,
            calibration.AbstractUnitId);
    }
}
