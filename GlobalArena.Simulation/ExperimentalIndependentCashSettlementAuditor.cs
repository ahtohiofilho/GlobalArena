using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public enum ExperimentalCashAuditAccountRole : byte
{
    Household = 1,
    Producer = 2,
    TransportOperator = 3
}

/// <summary>
/// A counterparty in a hypothetical, balanced economic-turn journal.
/// Producer proceeds never become household funds within this snapshot.
/// Operator identity is scoped to the explicit source, destination and
/// service ID; identical IDs on unrelated routes do not merge wallets.
/// </summary>
public readonly record struct ExperimentalCashAuditAccountKey(
    ExperimentalCashAuditAccountRole Role,
    EconomicPointId Point,
    EconomicPointId Destination,
    ulong ServiceId)
{
    public static ExperimentalCashAuditAccountKey Household(EconomicPointId buyer) =>
        new(ExperimentalCashAuditAccountRole.Household, buyer, default, 0);
    public static ExperimentalCashAuditAccountKey Producer(EconomicPointId source) =>
        new(ExperimentalCashAuditAccountRole.Producer, source, default, 0);
    public static ExperimentalCashAuditAccountKey Operator(
        EconomicPointId source, EconomicPointId destination, ulong serviceId) =>
        new(ExperimentalCashAuditAccountRole.TransportOperator, source,
            destination, serviceId);
}

public enum ExperimentalCashAuditTransferKind : byte
{
    CommodityPurchase = 1,
    Freight = 2
}

public sealed record ExperimentalCashAuditTransfer(
    ExperimentalCashAuditAccountKey Debit,
    ExperimentalCashAuditAccountKey Credit,
    ExperimentalCashAuditTransferKind Kind,
    CommodityId Commodity,
    ulong RawQuantity,
    FoodTransportCostFraction Amount);

public sealed record ExperimentalCashAuditBuyer(
    EconomicPointId Buyer,
    FoodTransportCostFraction OpeningGrossCash,
    FoodTransportCostFraction ProtectedH,
    FoodTransportCostFraction CommodityDebits,
    FoodTransportCostFraction FreightDebits,
    FoodTransportCostFraction ClosingGrossCash);

public sealed record ExperimentalCashAuditResult(
    IReadOnlyList<ExperimentalCashAuditTransfer> Transfers,
    IReadOnlyList<ExperimentalCashAuditBuyer> Buyers,
    IReadOnlyDictionary<ExperimentalCashAuditAccountKey, FoodTransportCostFraction> Opening,
    IReadOnlyDictionary<ExperimentalCashAuditAccountKey, FoodTransportCostFraction> Closing,
    FoodTransportCostFraction TotalOpening,
    FoodTransportCostFraction TotalClosing,
    FoodTransportCostFraction TotalCommodityPurchases,
    FoodTransportCostFraction TotalFreight);

/// <summary>
/// Independent, read-only AUDIT of the proposed VS43/44 trades. Reconstructs
/// demand/supply conservation, exact physical corridor loads, commodity
/// invoices, buyer-side shared congestion and double-entry cash transfer
/// counterparts from the submitted original inputs. It does NOT reuse the
/// VS44 buyer affordability evaluator or trust proposed cash/freight totals.
///
/// All buyers are pre-funded from the SAME opening snapshot. Producer and
/// carrier receipts are credited only at the end of this hypothetical
/// posting; they cannot finance purchases in the current round. No money is
/// created, borrowed or persisted. The journal is a PROPOSAL, not SETTLE.
/// </summary>
public sealed class ExperimentalIndependentCashSettlementAuditor
{
    private static readonly BigInteger Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly BigInteger MicrogramsPerGram = 1_000_000;

    private static FoodTransportCostFraction Zero => new(0, 1);

    private static FoodTransportCostFraction Sum(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static FoodTransportCostFraction Minus(
        FoodTransportCostFraction a, FoodTransportCostFraction b)
    {
        var numerator = a.Numerator * b.Denominator -
            b.Numerator * a.Denominator;
        if (numerator.Sign < 0)
            throw new InvalidOperationException("An audited account would go negative.");
        return new(numerator, a.Denominator * b.Denominator);
    }

    private static bool Equal(FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        a.CompareTo(b) == 0;

    private static FoodTransportCostFraction CommodityCharge(
        FoodTransportCostFraction price, ulong rawQuantity) =>
        new(price.Numerator * rawQuantity, price.Denominator * Unit);

    public ExperimentalCashAuditResult Audit(
        ExperimentalSimultaneousFreightAllocation allocation,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<ExperimentalSimultaneousFreightLane> declaredLanes,
        IEnumerable<ExperimentalBuyerCashBudget> frozenBudgets,
        IEnumerable<ExperimentalBuyerCommodityPrice> frozenPrices,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(declaredLanes);
        ArgumentNullException.ThrowIfNull(frozenBudgets);
        ArgumentNullException.ThrowIfNull(frozenPrices);
        ArgumentNullException.ThrowIfNull(congestionPolicy);

        var supply = flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity), x => x.RawQuantity);
        var demand = flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        var buyerIds = demand.Keys.Select(x => x.Item1).ToHashSet();
        var budgets = new Dictionary<EconomicPointId, ExperimentalBuyerCashBudget>();
        foreach (var budget in frozenBudgets)
            if (budget is null || !budgets.TryAdd(budget.Buyer, budget))
                throw new ArgumentException(
                    "All buyer cash budgets must be distinct and explicit.",
                    nameof(frozenBudgets));
        if (!budgets.Keys.ToHashSet().SetEquals(buyerIds))
            throw new ArgumentException(
                "Frozen budgets must cover precisely all existing buyers.",
                nameof(frozenBudgets));

        var prices = new Dictionary<(EconomicPointId, CommodityId),
            FoodTransportCostFraction>();
        foreach (var price in frozenPrices)
            if (price is null || !prices.TryAdd(
                (price.Buyer, price.Commodity), price.PricePerWholeFlowUnit))
                throw new ArgumentException(
                    "Posted purchase prices must be unique and non-null.",
                    nameof(frozenPrices));
        if (!prices.Keys.ToHashSet().SetEquals(demand.Keys))
            throw new ArgumentException(
                "Posted prices must cover precisely all current demand items.",
                nameof(frozenPrices));

        var lanes = new Dictionary<(EconomicPointId, EconomicPointId, CommodityId,
            ulong), (EconomicActivityKind Activity, ExperimentalSharedFreightOption Option)>();
        foreach (var lane in declaredLanes)
        {
            if (lane is null ||
                !supply.ContainsKey((lane.Supply.EconomicPointId,
                    lane.Supply.CommodityId, lane.Supply.Activity)) ||
                !demand.ContainsKey((lane.Demand.EconomicPointId,
                    lane.Demand.CommodityId)))
                throw new ArgumentException(
                    "Each offered lane must reference existing physical flow.",
                    nameof(declaredLanes));
            foreach (var option in lane.Services)
            {
                var key = (lane.Supply.EconomicPointId,
                    lane.Demand.EconomicPointId, lane.Supply.CommodityId,
                    option.ServiceId);
                if (!lanes.TryAdd(key, (lane.Supply.Activity, option)))
                    throw new ArgumentException(
                        "A trade must identify one unambiguous offered source/activity/service.",
                        nameof(declaredLanes));
            }
        }

        var opening = new Dictionary<ExperimentalCashAuditAccountKey, FoodTransportCostFraction>();
        foreach (var buyer in budgets.Values)
            opening.Add(ExperimentalCashAuditAccountKey.Household(buyer.Buyer),
                buyer.GrossCash);
        var buyerCommodity = budgets.Keys.ToDictionary(x => x, _ => Zero);
        var buyerFreight = budgets.Keys.ToDictionary(x => x, _ => Zero);
        var usedSupply = new Dictionary<(EconomicPointId, CommodityId,
            EconomicActivityKind), BigInteger>();
        var usedDemand = new Dictionary<(EconomicPointId, CommodityId), BigInteger>();
        var loads = new Dictionary<ExperimentalSharedFreightCorridorKey, BigInteger>();
        var records = new List<(ExperimentalSimultaneousFreightDecision Trade,
            ExperimentalSharedFreightOption Option, BigInteger Load)>();
        var seen = new HashSet<(EconomicPointId, EconomicPointId, CommodityId)>();
        foreach (var trade in allocation.Trades)
        {
            if (trade is null || trade.RawQuantity == 0UL ||
                !seen.Add((trade.Source, trade.Destination, trade.Commodity)) ||
                !lanes.TryGetValue((trade.Source, trade.Destination, trade.Commodity,
                    trade.ServiceId), out var candidate) ||
                !catalog.TryGet(trade.Commodity, out var item) || item is null)
                throw new InvalidOperationException(
                    "A cash audit requires unique, physically priced, explicitly offered trades.");

            var sourceKey = (trade.Source, trade.Commodity, candidate.Activity);
            var destinationKey = (trade.Destination, trade.Commodity);
            usedSupply.TryGetValue(sourceKey, out var consumed);
            usedDemand.TryGetValue(destinationKey, out var delivered);
            usedSupply[sourceKey] = consumed + trade.RawQuantity;
            usedDemand[destinationKey] = delivered + trade.RawQuantity;
            if (usedSupply[sourceKey] > supply[sourceKey] ||
                usedDemand[destinationKey] > demand[destinationKey])
                throw new InvalidOperationException(
                    "A proposed batch exceeds its original physical supply or demand.");

            var physicalLoad = (BigInteger)trade.RawQuantity * item.GramsPerFlowUnit;
            foreach (var corridor in candidate.Option.Corridors)
            {
                loads.TryGetValue(corridor, out var used);
                loads[corridor] = used + physicalLoad;
            }
            records.Add((trade, candidate.Option, physicalLoad));
        }
        if (loads.Count != allocation.CorridorLoads.Count ||
            loads.Any(x => !allocation.CorridorLoads.TryGetValue(x.Key, out var q) ||
                q != x.Value))
            throw new InvalidOperationException(
                "Submitted shared corridor loads disagree with independent physical totals.");

        var closing = opening.ToDictionary(x => x.Key, x => x.Value);
        var transfers = new List<ExperimentalCashAuditTransfer>();
        var totalGoods = Zero;
        var totalFreight = Zero;
        foreach (var (trade, option, physicalLoad) in records
            .OrderBy(x => x.Trade.Source.Value)
            .ThenBy(x => x.Trade.Destination.Value)
            .ThenBy(x => x.Trade.Commodity.Value)
            .ThenBy(x => x.Trade.ServiceId))
        {
            var payer = ExperimentalCashAuditAccountKey.Household(trade.Destination);
            var seller = ExperimentalCashAuditAccountKey.Producer(trade.Source);
            var carrier = ExperimentalCashAuditAccountKey.Operator(
                trade.Source, trade.Destination, trade.ServiceId);

            var goods = CommodityCharge(prices[(trade.Destination, trade.Commodity)],
                trade.RawQuantity);
            // Independently derive both variable operating costs and the
            // contribution q_i * Q_e^2 of shared corridor congestion.
            var terms = option.Pricing;
            var distance = new FoodTransportCostFraction(
                physicalLoad * terms.GeographicBaseNumerator,
                MicrogramsPerGram *
                    ExperimentalAggregatedLandFreightCostFunction.GeographicDenominator);
            var overhead = new FoodTransportCostFraction(
                physicalLoad * terms.OverheadNumerator,
                MicrogramsPerGram *
                    (physicalLoad + terms.OverheadSmoothingMicrograms));
            var freight = Sum(distance, overhead);
            foreach (var crossing in option.Corridors)
            {
                var q = loads[crossing];
                freight = Sum(freight, new FoodTransportCostFraction(
                    congestionPolicy.SaturationWeight * physicalLoad * q * q,
                    MicrogramsPerGram *
                        congestionPolicy.SaturationScaleMicrograms *
                        congestionPolicy.SaturationScaleMicrograms));
            }

            void Credit(ExperimentalCashAuditAccountKey recipient,
                ExperimentalCashAuditTransferKind kind,
                FoodTransportCostFraction value)
            {
                // Crucial: debit from opening prefunded buyers only, never
                // from producer or transport receipts in this same batch.
                var debit = Sum(buyerCommodity[trade.Destination],
                    buyerFreight[trade.Destination]);
                var remaining = Minus(budgets[trade.Destination].SpendableCash,
                    Sum(debit, value));
                _ = remaining;
                closing[payer] = Minus(closing[payer], value);
                closing.TryGetValue(recipient, out var previous);
                closing[recipient] = Sum(previous ?? Zero, value);
                transfers.Add(new ExperimentalCashAuditTransfer(
                    payer, recipient, kind, trade.Commodity,
                    trade.RawQuantity, value));
            }

            Credit(seller, ExperimentalCashAuditTransferKind.CommodityPurchase,
                goods);
            buyerCommodity[trade.Destination] = Sum(
                buyerCommodity[trade.Destination], goods);
            Credit(carrier, ExperimentalCashAuditTransferKind.Freight, freight);
            buyerFreight[trade.Destination] = Sum(
                buyerFreight[trade.Destination], freight);
            totalGoods = Sum(totalGoods, goods);
            totalFreight = Sum(totalFreight, freight);
        }

        // These assertions are intentionally independent of VS44's cost
        // evaluator and refuse falsified aggregate freight or cash reports.
        if (!Equal(allocation.SharedFreightCost, totalFreight))
            throw new InvalidOperationException(
                "The proposed shared freight charge differs from the independently reconstructed invoices.");
        if (allocation.CashLedger.Count != budgets.Count ||
            allocation.CashLedger.Select(x => x.Buyer).Distinct().Count() !=
                budgets.Count)
            throw new InvalidOperationException(
                "The proposed buyer cash ledger is incomplete or duplicated.");

        var audited = new List<ExperimentalCashAuditBuyer>();
        foreach (var buyer in budgets.Values.OrderBy(x => x.Buyer.Value))
        {
            var proposed = allocation.CashLedger.SingleOrDefault(
                x => x.Buyer == buyer.Buyer) ??
                throw new InvalidOperationException("Missing buyer cash report.");
            var itemDebit = buyerCommodity[buyer.Buyer];
            var freightDebit = buyerFreight[buyer.Buyer];
            var commitment = Sum(itemDebit, freightDebit);
            var finalGross = closing[ExperimentalCashAuditAccountKey.Household(
                buyer.Buyer)];
            var finalSpendable = Minus(finalGross, buyer.ProtectedResidual);
            if (!Equal(proposed.GrossCash, buyer.GrossCash) ||
                !Equal(proposed.ProtectedResidual, buyer.ProtectedResidual) ||
                !Equal(proposed.SpendableCash, buyer.SpendableCash) ||
                !Equal(proposed.CommodityCharges, itemDebit) ||
                !Equal(proposed.FreightCharges, freightDebit) ||
                !Equal(proposed.TotalCommitment, commitment) ||
                !Equal(proposed.RemainingSpendableCash, finalSpendable))
                throw new InvalidOperationException(
                    "The proposed buyer ledger differs from independently audited cash transfers.");
            audited.Add(new ExperimentalCashAuditBuyer(
                buyer.Buyer, buyer.GrossCash, buyer.ProtectedResidual,
                itemDebit, freightDebit, finalGross));
        }

        var initial = opening.Values.Aggregate(Zero, Sum);
        var final = closing.Values.Aggregate(Zero, Sum);
        if (!Equal(initial, final))
            throw new InvalidOperationException(
                "The hypothetical posting did not conserve its opening monetary supply.");
        return new ExperimentalCashAuditResult(
            Array.AsReadOnly(transfers.ToArray()),
            Array.AsReadOnly(audited.ToArray()),
            new ReadOnlyDictionary<ExperimentalCashAuditAccountKey,
                FoodTransportCostFraction>(opening),
            new ReadOnlyDictionary<ExperimentalCashAuditAccountKey,
                FoodTransportCostFraction>(closing),
            initial, final, totalGoods, totalFreight);
    }
}
