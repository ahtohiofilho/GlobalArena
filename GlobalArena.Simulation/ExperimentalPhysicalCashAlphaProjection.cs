using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit, immutable one-to-one projection from the existing V14 household
/// microcredit ledger to VS44 buyer money. A tactical tile is not silently
/// the same thing as an EconomicPointId. A microcredit is NOT automatically
/// a VS40 freight price: the caller must supply the exchange scale.
/// </summary>
public sealed record ExperimentalCashBuyerTileAnchor(
    EconomicPointId Buyer, PhysicalTacticalTileId Tile);

public sealed record ExperimentalCashBudgetBridgeResult(
    ExperimentalBuyerCashSnapshot Snapshot,
    IReadOnlyList<ExperimentalBuyerCashBudget> Budgets,
    ulong MicrocreditsPerEconomicUnit,
    BigInteger VerifiedHouseholdMicrocredits,
    BigInteger VerifiedProducerMicrocredits);

public sealed class ExperimentalPhysicalCashBudgetBridge
{
    public ExperimentalCashBudgetBridgeResult Project(
        CanonicalPhysicalTacticalSession session,
        EconomicProductionDemandResult flows,
        IEnumerable<ExperimentalCashBuyerTileAnchor> buyerAnchors,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        IReadOnlyDictionary<EconomicPointId, FoodTransportCostFraction> protectedH,
        ulong microcreditsPerEconomicUnit)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(buyerAnchors);
        ArgumentNullException.ThrowIfNull(postedPrices);
        ArgumentNullException.ThrowIfNull(protectedH);
        if (microcreditsPerEconomicUnit == 0UL)
            throw new ArgumentOutOfRangeException(nameof(microcreditsPerEconomicUnit),
                "Money scaling must be supplied and cannot be inferred.");

        var ledger = session.FoodCash ??
            throw new InvalidOperationException(
                "A physical cash projection needs an existing LocalCashSettlement ledger.");
        if (session.MarketMode != PhysicalFoodMarketMode.LocalCashSettlement)
            throw new InvalidOperationException(
                "Cash projection cannot be enabled in a legacy direct-consumption world.");

        // Reaudit the authoritative monetary subledger with BigInteger,
        // without transferring money to any economic model.
        var household = BigInteger.Zero;
        var producers = BigInteger.Zero;
        var accounts = new Dictionary<PhysicalTacticalTileId, PhysicalFoodCashAccount>();
        foreach (var row in ledger.Accounts)
        {
            if (row is null || !accounts.TryAdd(row.Tile, row))
                throw new InvalidOperationException("Physical cash accounts must be unique.");
            household += row.HouseholdMicrocredits;
            producers += row.ProducerMicrocredits;
        }
        if (household + producers != ledger.InitialMoneySupplyMicrocredits)
            throw new InvalidOperationException(
                "The projection refuses a physically unbalanced cash ledger.");

        var demandBuyers = flows.DemandFlows
            .Select(x => x.EconomicPointId).ToHashSet();
        var anchors = buyerAnchors.ToArray();
        if (anchors.Any(a => a is null || !a.Buyer.IsValid ||
                !a.Tile.IsValid ||
                a.Tile.FineGoldbergParameters !=
                    session.FineWorld.StrategicTopology.Parameters) ||
            anchors.Select(x => x.Buyer).Distinct().Count() != anchors.Length ||
            anchors.Select(x => x.Tile).Distinct().Count() != anchors.Length ||
            !anchors.Select(x => x.Buyer).ToHashSet().SetEquals(demandBuyers))
            throw new ArgumentException(
                "Buyers require a complete, unique economic-point to physical-tile mapping; shared household accounts cannot be projected twice.",
                nameof(buyerAnchors));
        if (!protectedH.Keys.ToHashSet().SetEquals(demandBuyers))
            throw new ArgumentException(
                "A protected residual H must be explicitly provided for every buyer.",
                nameof(protectedH));

        var budgets = new List<ExperimentalBuyerCashBudget>();
        foreach (var anchor in anchors.OrderBy(a => a.Buyer.Value))
        {
            if (!accounts.TryGetValue(anchor.Tile, out var account))
                throw new ArgumentException(
                    "The physical buyer tile has no existing cash account.",
                    nameof(buyerAnchors));
            budgets.Add(new ExperimentalBuyerCashBudget(
                anchor.Buyer,
                new FoodTransportCostFraction(
                    account.HouseholdMicrocredits, microcreditsPerEconomicUnit),
                protectedH[anchor.Buyer] ??
                    throw new ArgumentException("Protected H may not be null.",
                        nameof(protectedH))));
        }

        var snapshot = new ExperimentalBuyerCashSnapshot(budgets, postedPrices);
        snapshot.ValidateCoverage(flows);
        return new ExperimentalCashBudgetBridgeResult(
            snapshot, Array.AsReadOnly(budgets.ToArray()),
            microcreditsPerEconomicUnit, household, producers);
    }
}

/// <summary>
/// One supplied, ex-ante estimate of the delivered price per whole flow unit.
/// The freight estimate must be explicit; VS42 corridor saturation may change
/// when different shipments are jointly accepted. This is NOT a settlement
/// quote and does not authorize additional infrastructure.
/// </summary>
public sealed record ExperimentalDeliveredDemandEstimate(
    EconomicPointId Buyer, CommodityId Commodity,
    FoodTransportCostFraction CommodityUnitPrice,
    FoodTransportCostFraction EstimatedFreightPerWholeUnit);

public sealed record ExperimentalBuyerAlphaPlan(
    EconomicPointId Buyer,
    FoodTransportCostFraction Alpha,
    FoodTransportCostFraction SpendableBudget,
    FoodTransportCostFraction BaselineEstimatedSpending,
    FoodTransportCostFraction ScaledEstimatedSpending,
    IReadOnlyList<EconomicDemandFlow> ProjectedDemand);

public sealed record ExperimentalAlphaDemandPlan(
    IReadOnlyList<ExperimentalBuyerAlphaPlan> Buyers,
    IReadOnlyList<EconomicDemandFlow> ProjectedDemand);

/// <summary>
/// Pure proportional demand forecast:
/// alpha_b = min(1, spendable_b / sum_i quantity_i * deliveredQuote_i).
/// Raw quantities are floored after exact rational multiplication. Zero
/// projected quantities are omitted (EconomicDemandFlow requires > 0).
/// H is not bought, banked or transferred. A later clearing still MUST
/// recheck actual freight and real buyer affordability with VS44.
/// </summary>
public sealed class ExperimentalCashDemandAlphaPlanner
{
    private static readonly BigInteger RawUnit =
        EconomicTransferPolicy.FixedPointDenominator;

    public ExperimentalAlphaDemandPlan Project(
        EconomicProductionDemandResult flows,
        IEnumerable<ExperimentalBuyerCashBudget> budgets,
        IEnumerable<ExperimentalDeliveredDemandEstimate> estimates)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(estimates);

        var buyers = new Dictionary<EconomicPointId, ExperimentalBuyerCashBudget>();
        foreach (var b in budgets)
            if (b is null || !buyers.TryAdd(b.Buyer, b))
                throw new ArgumentException("Cash budgets must be unique and explicit.",
                    nameof(budgets));
        var demandBuyers = flows.DemandFlows
            .Select(x => x.EconomicPointId).ToHashSet();
        if (!buyers.Keys.ToHashSet().SetEquals(demandBuyers))
            throw new ArgumentException(
                "Alpha planning requires exactly the buyers represented in the current demand.",
                nameof(budgets));

        var prices = new Dictionary<(EconomicPointId, CommodityId),
            ExperimentalDeliveredDemandEstimate>();
        foreach (var quote in estimates)
            if (quote is null || !quote.Buyer.IsValid || !quote.Commodity.IsValid ||
                quote.CommodityUnitPrice is null ||
                quote.EstimatedFreightPerWholeUnit is null ||
                !prices.TryAdd((quote.Buyer, quote.Commodity), quote))
                throw new ArgumentException(
                    "Each demand item needs exactly one explicit nonnegative commodity+freight estimate.",
                    nameof(estimates));

        var demandKeys = flows.DemandFlows
            .Select(x => (x.EconomicPointId, x.CommodityId)).ToHashSet();
        if (!prices.Keys.ToHashSet().SetEquals(demandKeys))
            throw new ArgumentException(
                "Alpha quotes must cover precisely the requested items.",
                nameof(estimates));

        var rows = new List<ExperimentalBuyerAlphaPlan>();
        var final = new List<EconomicDemandFlow>();
        foreach (var buyer in buyers.Values.OrderBy(x => x.Buyer.Value))
        {
            var requested = flows.DemandFlows
                .Where(x => x.EconomicPointId == buyer.Buyer)
                .OrderBy(x => x.CommodityId.Value).ToArray();
            var total = ExperimentalCashMoney.Zero;
            foreach (var flow in requested)
            {
                var price = prices[(flow.EconomicPointId, flow.CommodityId)];
                total = ExperimentalCashMoney.Add(total,
                    ExperimentalCashMoney.Multiply(
                        ExperimentalCashMoney.Add(price.CommodityUnitPrice,
                            price.EstimatedFreightPerWholeUnit),
                        flow.RawQuantity, RawUnit));
            }

            var spendable = buyer.SpendableCash;
            FoodTransportCostFraction alpha;
            if (total.Numerator.IsZero || spendable.CompareTo(total) >= 0)
                alpha = new FoodTransportCostFraction(1, 1);
            else
                alpha = new FoodTransportCostFraction(
                    spendable.Numerator * total.Denominator,
                    spendable.Denominator * total.Numerator);

            var projected = new List<EconomicDemandFlow>();
            var charged = ExperimentalCashMoney.Zero;
            foreach (var request in requested)
            {
                var raw = (BigInteger)request.RawQuantity * alpha.Numerator /
                    alpha.Denominator;
                if (raw.IsZero) continue;
                var flow = new EconomicDemandFlow(
                    request.EconomicPointId, request.CommodityId, checked((ulong)raw));
                projected.Add(flow);
                var price = prices[(request.EconomicPointId, request.CommodityId)];
                charged = ExperimentalCashMoney.Add(charged,
                    ExperimentalCashMoney.Multiply(
                        ExperimentalCashMoney.Add(price.CommodityUnitPrice,
                            price.EstimatedFreightPerWholeUnit), raw, RawUnit));
            }
            if (charged.CompareTo(spendable) > 0)
                throw new InvalidOperationException(
                    "Proportional demand must not exceed cash after conservative rounding.");
            rows.Add(new ExperimentalBuyerAlphaPlan(
                buyer.Buyer, alpha, spendable, total, charged,
                Array.AsReadOnly(projected.ToArray())));
            final.AddRange(projected);
        }
        return new ExperimentalAlphaDemandPlan(
            Array.AsReadOnly(rows.ToArray()),
            Array.AsReadOnly(final.ToArray()));
    }
}
