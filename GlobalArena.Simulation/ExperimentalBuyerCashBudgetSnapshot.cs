using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Hypothetical spendable cash for one economic destination. Money must be
/// explicitly supplied in the SAME raw monetary numeraire as the VS40/VS42
/// laboratory freight pricing and commodity posted prices. Neither balances
/// nor income are inferred from future sales or from the value-utility curve.
/// H is a protected, invisible consumption residual, NOT a bank account.
/// </summary>
public sealed class ExperimentalBuyerCashBudget
{
    public EconomicPointId Buyer { get; }
    public FoodTransportCostFraction GrossCash { get; }
    public FoodTransportCostFraction ProtectedResidual { get; }
    public FoodTransportCostFraction SpendableCash { get; }

    public ExperimentalBuyerCashBudget(
        EconomicPointId buyer, FoodTransportCostFraction grossCash,
        FoodTransportCostFraction protectedResidual)
    {
        if (!buyer.IsValid)
            throw new ArgumentException("An explicit buyer identity is required.", nameof(buyer));
        GrossCash = grossCash ?? throw new ArgumentNullException(nameof(grossCash));
        ProtectedResidual = protectedResidual ??
            throw new ArgumentNullException(nameof(protectedResidual));
        if (grossCash.CompareTo(protectedResidual) < 0)
            throw new ArgumentException("Protected cash cannot exceed the gross balance.");
        Buyer = buyer;
        SpendableCash = ExperimentalCashMoney.Subtract(grossCash, protectedResidual);
    }
}

/// <summary>
/// Ex-ante, nonnegative POSTED buyer-side commodity purchase price per WHOLE
/// flow unit. It is not the integrated marginal utility/value curve; the
/// latter remains solely an objective term in VS43.
/// </summary>
public sealed class ExperimentalBuyerCommodityPrice
{
    public EconomicPointId Buyer { get; }
    public CommodityId Commodity { get; }
    public FoodTransportCostFraction PricePerWholeFlowUnit { get; }

    public ExperimentalBuyerCommodityPrice(
        EconomicPointId buyer, CommodityId commodity,
        FoodTransportCostFraction pricePerWholeFlowUnit)
    {
        if (!buyer.IsValid || !commodity.IsValid)
            throw new ArgumentException("Posted commodity price needs valid economic identities.");
        Buyer = buyer;
        Commodity = commodity;
        PricePerWholeFlowUnit = pricePerWholeFlowUnit ??
            throw new ArgumentNullException(nameof(pricePerWholeFlowUnit));
    }
}

public sealed record ExperimentalBuyerCashDelivery(
    EconomicPointId Buyer,
    CommodityId Commodity,
    ulong RawQuantity,
    BigInteger PhysicalLoadMicrograms,
    ExperimentalSharedFreightOption Service);

public sealed record ExperimentalBuyerCashLedger(
    EconomicPointId Buyer,
    FoodTransportCostFraction GrossCash,
    FoodTransportCostFraction ProtectedResidual,
    FoodTransportCostFraction SpendableCash,
    FoodTransportCostFraction CommodityCharges,
    FoodTransportCostFraction FreightCharges,
    FoodTransportCostFraction TotalCommitment,
    FoodTransportCostFraction RemainingSpendableCash);

public sealed record ExperimentalBuyerCashEvaluation(
    bool CanSettleAtSight,
    IReadOnlyList<ExperimentalBuyerCashLedger> Buyers,
    FoodTransportCostFraction TotalBuyerFreight);

internal static class ExperimentalCashMoney
{
    public static FoodTransportCostFraction Zero => new(0, 1);

    public static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        ExperimentalSharedFreightBatchCost.Add(a, b);

    public static FoodTransportCostFraction Multiply(
        FoodTransportCostFraction value, BigInteger numerator,
        BigInteger denominator)
    {
        if (numerator.Sign < 0 || denominator.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        return new FoodTransportCostFraction(
            value.Numerator * numerator, value.Denominator * denominator);
    }

    public static FoodTransportCostFraction Subtract(
        FoodTransportCostFraction left, FoodTransportCostFraction right)
    {
        var numerator = left.Numerator * right.Denominator -
            right.Numerator * left.Denominator;
        if (numerator.Sign < 0)
            throw new InvalidOperationException("A cash debit cannot create a negative balance.");
        return new FoodTransportCostFraction(
            numerator, left.Denominator * right.Denominator);
    }
}

/// <summary>
/// Immutable cash-budget gate for a hypothetical VS43 batch. At most one
/// full purchase/freight debit is attributed to the destination buyer, using
/// exact rational prices and proportional allocation of every shared-corridor
/// congestion charge by each delivery's PHYSICAL load contribution.
///
/// This explicitly assumes buyers bear the delivered freight cost; the
/// economic incidence is a policy hypothesis, not final gameplay balance.
/// The gate does NOT create or settle cash, award producer revenue, issue
/// credits, mortgage future income or alter VS43 buyer utility.
/// </summary>
public sealed class ExperimentalBuyerCashSnapshot
{
    private static readonly BigInteger RawUnit =
        EconomicTransferPolicy.FixedPointDenominator;
    private static readonly BigInteger MicrogramsPerGram = 1_000_000;
    private readonly IReadOnlyDictionary<EconomicPointId, ExperimentalBuyerCashBudget> _budgets;
    private readonly IReadOnlyDictionary<(EconomicPointId, CommodityId),
        ExperimentalBuyerCommodityPrice> _prices;

    public ExperimentalBuyerCashSnapshot(
        IEnumerable<ExperimentalBuyerCashBudget> budgets,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices)
    {
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(postedPrices);
        var items = new Dictionary<EconomicPointId, ExperimentalBuyerCashBudget>();
        foreach (var b in budgets)
            if (b is null || !items.TryAdd(b.Buyer, b))
                throw new ArgumentException("Cash budgets must be non-null and unique by buyer.",
                    nameof(budgets));
        var p = new Dictionary<(EconomicPointId, CommodityId),
            ExperimentalBuyerCommodityPrice>();
        foreach (var price in postedPrices)
            if (price is null || !p.TryAdd((price.Buyer, price.Commodity), price))
                throw new ArgumentException(
                    "Buyer commodity prices must be non-null and unique per buyer/item.",
                    nameof(postedPrices));
        _budgets = items;
        _prices = p;
    }

    public void ValidateCoverage(EconomicProductionDemandResult flows)
    {
        ArgumentNullException.ThrowIfNull(flows);
        var buyers = flows.DemandFlows.Select(x => x.EconomicPointId).ToHashSet();
        var keys = flows.DemandFlows
            .Select(x => (x.EconomicPointId, x.CommodityId)).ToHashSet();
        if (!_budgets.Keys.ToHashSet().SetEquals(buyers) ||
            !_prices.Keys.ToHashSet().SetEquals(keys))
            throw new ArgumentException(
                "Every current demand destination needs one frozen cash budget and a posted item price; unexpected accounts are forbidden.");
    }

    public ExperimentalBuyerCashEvaluation Evaluate(
        IReadOnlyList<ExperimentalBuyerCashDelivery> deliveries,
        IReadOnlyDictionary<ExperimentalSharedFreightCorridorKey, BigInteger> corridorLoads,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy)
    {
        ArgumentNullException.ThrowIfNull(deliveries);
        ArgumentNullException.ThrowIfNull(corridorLoads);
        ArgumentNullException.ThrowIfNull(congestionPolicy);

        var commodities = _budgets.Keys.ToDictionary(x => x, _ => ExperimentalCashMoney.Zero);
        var freight = _budgets.Keys.ToDictionary(x => x, _ => ExperimentalCashMoney.Zero);
        foreach (var delivery in deliveries)
        {
            if (delivery is null || delivery.RawQuantity == 0UL ||
                delivery.PhysicalLoadMicrograms.Sign <= 0 ||
                !_budgets.ContainsKey(delivery.Buyer) ||
                !_prices.TryGetValue((delivery.Buyer, delivery.Commodity),
                    out var posted) ||
                delivery.Service is null ||
                delivery.Service.RouteKey.DestinationEconomicPointId != delivery.Buyer)
                throw new ArgumentException(
                    "A budgeted delivery must have a covered buyer/item, positive volume and a valid destination service.",
                    nameof(deliveries));

            var purchase = ExperimentalCashMoney.Multiply(
                posted.PricePerWholeFlowUnit, delivery.RawQuantity, RawUnit);
            commodities[delivery.Buyer] =
                ExperimentalCashMoney.Add(commodities[delivery.Buyer], purchase);

            var shipping = ExperimentalSharedFreightBatchCost.UncongestedCost(
                delivery.PhysicalLoadMicrograms, delivery.Service.Pricing);
            foreach (var crossing in delivery.Service.Corridors)
            {
                if (!corridorLoads.TryGetValue(crossing, out var total) ||
                    total < delivery.PhysicalLoadMicrograms)
                    throw new InvalidOperationException(
                        "Shared congestion requires the aggregate physical corridor load.");
                // q_i/Q share of C(Q) = weight*Q³/(1e6*scale²),
                // preserving full cost across all buyers with zero rounding loss.
                shipping = ExperimentalCashMoney.Add(shipping,
                    new FoodTransportCostFraction(
                        congestionPolicy.SaturationWeight *
                            delivery.PhysicalLoadMicrograms * total * total,
                        MicrogramsPerGram *
                            congestionPolicy.SaturationScaleMicrograms *
                            congestionPolicy.SaturationScaleMicrograms));
            }

            freight[delivery.Buyer] =
                ExperimentalCashMoney.Add(freight[delivery.Buyer], shipping);
        }

        var affordable = true;
        var totalBuyerFreight = ExperimentalCashMoney.Zero;
        var rows = new List<ExperimentalBuyerCashLedger>();
        foreach (var budget in _budgets.Values.OrderBy(x => x.Buyer.Value))
        {
            var purchases = commodities[budget.Buyer];
            var shipping = freight[budget.Buyer];
            var debit = ExperimentalCashMoney.Add(purchases, shipping);
            var canPay = debit.CompareTo(budget.SpendableCash) <= 0;
            affordable &= canPay;
            rows.Add(new ExperimentalBuyerCashLedger(
                budget.Buyer, budget.GrossCash, budget.ProtectedResidual,
                budget.SpendableCash, purchases, shipping, debit,
                canPay ? ExperimentalCashMoney.Subtract(budget.SpendableCash, debit)
                       : ExperimentalCashMoney.Zero));
            totalBuyerFreight = ExperimentalCashMoney.Add(totalBuyerFreight, shipping);
        }
        return new ExperimentalBuyerCashEvaluation(affordable,
            Array.AsReadOnly(rows.ToArray()), totalBuyerFreight);
    }
}
