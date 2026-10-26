using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Isolated deterministic multi-commodity, multi-route reference allocator.
/// The transport unit cost comes exclusively from pre-turn immutable quotes.
/// Commodity marginal values change as each destination fills; producer
/// reservation costs are separate explicit inputs.
///
/// This is a greedy, quantized reference, NOT a global welfare equilibrium
/// or a production replacement for legacy or opt-in live-congestion markets.
/// No WorldState, route topology, inventories, or caches are mutated.
/// </summary>
public sealed class EconomicTurnLockedFreightMarketResolver
{
    public IReadOnlyList<EconomicTurnLockedFreightTrade> Resolve(
        ulong turnOrdinal,
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<EconomicTurnFreightQuote> freightQuotes,
        IEnumerable<EconomicTurnCommodityValueOffer> commodityValues,
        IEnumerable<EconomicTurnSourceReservation> producerReservations,
        EconomicCongestionMarketSearchPolicy searchPolicy,
        EconomicTurnFreightMarketDiagnostics? diagnostics = null,
        EconomicTurnFreightCandidateSelectionMode selectionMode =
            EconomicTurnFreightCandidateSelectionMode.Exhaustive)
    {
        diagnostics?.Reset();
        if (selectionMode != EconomicTurnFreightCandidateSelectionMode.Exhaustive &&
            selectionMode != EconomicTurnFreightCandidateSelectionMode.DependencyIndex)
            throw new ArgumentOutOfRangeException(nameof(selectionMode));
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(freightQuotes);
        ArgumentNullException.ThrowIfNull(commodityValues);
        ArgumentNullException.ThrowIfNull(producerReservations);
        ArgumentNullException.ThrowIfNull(searchPolicy);

        var foods = catalog.Commodities.ToDictionary(x => x.CommodityId);
        var supplies = flows.SupplyFlows
            .OrderBy(x => x.CommodityId.Value)
            .ThenBy(x => x.EconomicPointId.Value)
            .ThenBy(x => (byte)x.Activity)
            .Select(x => new SupplyBalance(x)).ToArray();
        var demands = flows.DemandFlows
            .OrderBy(x => x.CommodityId.Value)
            .ThenBy(x => x.EconomicPointId.Value)
            .Select(x => new DemandBalance(x)).ToArray();

        foreach (var supply in supplies)
            if (!foods.ContainsKey(supply.Flow.CommodityId))
                throw new InvalidOperationException(
                    "Every source requires an explicit commodity mass.");
        foreach (var demand in demands)
            if (!foods.ContainsKey(demand.Flow.CommodityId))
                throw new InvalidOperationException(
                    "Every destination requires an explicit commodity mass.");

        var supplyKeys = supplies
            .Select(x => (x.Flow.EconomicPointId, x.Flow.CommodityId, x.Flow.Activity))
            .ToHashSet();
        var demandKeys = demands
            .Select(x => (x.Flow.EconomicPointId, x.Flow.CommodityId))
            .ToHashSet();
        var sourcePoints = supplies.Select(x => x.Flow.EconomicPointId).ToHashSet();
        var destinationPoints = demands.Select(x => x.Flow.EconomicPointId).ToHashSet();

        var reservations = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind),
            EconomicTurnSourceReservation>();
        foreach (var row in Snapshot(producerReservations, nameof(producerReservations)))
        {
            var key = (row.EconomicPointId, row.CommodityId, row.Activity);
            if (!supplyKeys.Contains(key) || !reservations.TryAdd(key, row))
                throw new ArgumentException(
                    "Unknown or duplicate source reservation.", nameof(producerReservations));
        }
        if (reservations.Count != supplies.Length)
            throw new ArgumentException(
                "Every producer supply requires a reservation price.", nameof(producerReservations));

        var valuations = new Dictionary<
            (EconomicPointId, CommodityId), EconomicTurnCommodityValueOffer>();
        foreach (var row in Snapshot(commodityValues, nameof(commodityValues)))
        {
            var key = (row.EconomicPointId, row.CommodityId);
            if (!demandKeys.Contains(key) || !valuations.TryAdd(key, row))
                throw new ArgumentException(
                    "Unknown or duplicate demand valuation.", nameof(commodityValues));
        }
        if (valuations.Count != demands.Length)
            throw new ArgumentException(
                "Every demand requires a market value curve.", nameof(commodityValues));

        var quotes = new Dictionary<StrategicEconomicRouteKey, EconomicTurnFreightQuote>();
        foreach (var quote in Snapshot(freightQuotes, nameof(freightQuotes)))
        {
            if (quote.TurnOrdinal != turnOrdinal)
                throw new ArgumentException(
                    "Freight quote belongs to a different economic turn.", nameof(freightQuotes));
            if (!sourcePoints.Contains(quote.RouteKey.SourceEconomicPointId) ||
                !destinationPoints.Contains(quote.RouteKey.DestinationEconomicPointId) ||
                !quotes.TryAdd(quote.RouteKey, quote))
                throw new ArgumentException(
                    "Unknown or duplicate freight relationship.", nameof(freightQuotes));
        }

        var lanes = new List<Lane>();
        foreach (var supply in supplies)
            foreach (var demand in demands)
            {
                if (supply.Flow.CommodityId != demand.Flow.CommodityId)
                    continue;
                var key = new StrategicEconomicRouteKey(
                    supply.Flow.EconomicPointId, demand.Flow.EconomicPointId);
                if (!quotes.TryGetValue(key, out var quote))
                    continue; // Only explicitly quoted transport relationships exist.
                var sourceKey = (
                    supply.Flow.EconomicPointId, supply.Flow.CommodityId, supply.Flow.Activity);
                var demandKey = (demand.Flow.EconomicPointId, demand.Flow.CommodityId);
                lanes.Add(new Lane(
                    supply, demand, foods[supply.Flow.CommodityId],
                    reservations[sourceKey].RawUnitReservationCost,
                    valuations[demandKey].Curve, quote));
            }

        diagnostics?.SetInputs(supplies.Length, demands.Length, quotes.Count, lanes.Count);

        // The index is created afresh from this turn's canonical lanes.
        // Frozen freight quotes never become dirty as parcels accumulate.
        var indexedCandidates =
            selectionMode == EconomicTurnFreightCandidateSelectionMode.DependencyIndex
                ? new DependencyCandidateIndex(lanes, searchPolicy, diagnostics)
                : null;

        var chosenTrades = new List<EconomicTurnLockedFreightTrade>();
        while (true)
        {
            diagnostics?.Round();
            Lane? chosen = null;
            SignedMargin best = default;
            ulong chosenQuantity = 0UL;
            FoodTransportCostFraction? chosenMarginalValue = null;

            if (indexedCandidates is not null)
            {
                var winner = indexedCandidates.Best;
                if (winner is not null)
                {
                    chosen = lanes[winner.LaneIndex];
                    chosenQuantity = winner.ProfitableQuantity;
                    chosenMarginalValue = winner.MarginalValue;
                }
            }
            else
            {
                // Preserve this exhaustive loop as the semantic oracle.
                foreach (var lane in lanes)
                {
                    diagnostics?.ExamineLane();
                    var remaining = Math.Min(lane.Supply.Remaining, lane.Demand.Remaining);
                    if (remaining == 0UL)
                        continue;

                    var curve = lane.Curve;
                    diagnostics?.EvaluateMarginal();
                    var marginal = curve.EvaluateMarginalRawUnitValue(
                        lane.Demand.Allocated);
                    ArgumentNullException.ThrowIfNull(marginal);
                    var net = SubtractSigned(marginal, lane.UnitExpense);
                    if (net.Numerator.Sign <= 0)
                        continue;

                    var maximum = Math.Min(remaining, searchPolicy.RawDecisionQuantum);
                    var profitable = LargestProfitableQuantity(
                        lane, maximum, diagnostics);
                    if (profitable == 0UL)
                        continue;

                    if (chosen is null || CompareSigned(net, best) > 0)
                    {
                        chosen = lane;
                        best = net;
                        chosenQuantity = profitable;
                        chosenMarginalValue = marginal;
                    }
                }
            }
            if (chosen is null)
                break;
            if (chosenTrades.Count >= searchPolicy.MaxDecisions)
                throw new InvalidOperationException(
                    "Reference allocation exhausted numerical decision budget.");

            var from = chosen.Demand.Allocated;
            var to = checked(from + chosenQuantity);
            diagnostics?.EvaluateIntegral();
            var before = chosen.Curve.EvaluateIntegratedRawValue(from);
            diagnostics?.EvaluateIntegral();
            var after = chosen.Curve.EvaluateIntegratedRawValue(to);
            if (before is null || after is null || after.CompareTo(before) < 0)
                throw new InvalidOperationException(
                    "Market value integral must be nonnegative and monotone.");
            var value = SubtractNonnegative(after, before);
            var freight = chosen.Quote.ChargeForRawQuantity(
                chosenQuantity, chosen.Food.GramsPerFlowUnit);
            var reserve = new FoodTransportCostFraction(
                (BigInteger)chosenQuantity * chosen.RawUnitReservationCost,
                EconomicTransferPolicy.FixedPointDenominator);
            var netReturn = SubtractNonnegative(
                SubtractNonnegative(value, reserve), freight);
            if (netReturn.Numerator.Sign <= 0)
                throw new InvalidOperationException(
                    "Curve/integral inconsistent with profitable marginal quotation.");

            chosenTrades.Add(new EconomicTurnLockedFreightTrade(
                (ulong)chosenTrades.Count + 1UL, turnOrdinal,
                chosen.Supply.Flow, chosen.Demand.Flow,
                chosenQuantity, chosenMarginalValue!, chosen.Quote,
                value, freight, reserve, netReturn));
            chosen.Supply.Remaining -= chosenQuantity;
            chosen.Demand.Remaining -= chosenQuantity;
            chosen.Demand.Allocated = to;
            diagnostics?.AcceptTrade();
            // Only this producer's balance and destination/commodity
            // valuation changed. Other lane candidates remain exact.
            indexedCandidates?.Refresh(chosen.Supply, chosen.Demand);
        }
        return Array.AsReadOnly(chosenTrades.ToArray());
    }

    /// <summary>
    /// A turn-local index of profitable candidates. Exact marginal ordering
    /// and canonical tie breaking match the exhaustive foreach loop:
    /// greatest margin wins, and earliest lane wins exact ties.
    /// Candidate validity depends only on its own supply balance, shared
    /// destination-commodity allocation and immutable pre-turn freight quote.
    /// No mutable global cache or inferred unquoted route is introduced.
    /// </summary>
    private sealed class DependencyCandidateIndex
    {
        private sealed record Candidate(
            int LaneIndex,
            SignedMargin Margin,
            ulong ProfitableQuantity,
            FoodTransportCostFraction MarginalValue);

        private readonly IReadOnlyList<Lane> _lanes;
        private readonly EconomicCongestionMarketSearchPolicy _policy;
        private readonly EconomicTurnFreightMarketDiagnostics? _diagnostics;
        private readonly SortedSet<Candidate> _ordered;
        private readonly Candidate?[] _current;
        private readonly Dictionary<SupplyBalance, List<int>> _bySupply = new();
        private readonly Dictionary<DemandBalance, List<int>> _byDemand = new();

        public DependencyCandidateIndex(
            IReadOnlyList<Lane> lanes,
            EconomicCongestionMarketSearchPolicy policy,
            EconomicTurnFreightMarketDiagnostics? diagnostics)
        {
            _lanes = lanes;
            _policy = policy;
            _diagnostics = diagnostics;
            _current = new Candidate[lanes.Count];
            _ordered = new SortedSet<Candidate>(
                Comparer<Candidate>.Create((left, right) =>
                {
                    var margin = CompareSigned(left.Margin, right.Margin);
                    // The Max candidate must be the earliest canonical lane
                    // when two lanes have exactly equal rational margins.
                    return margin != 0
                        ? margin
                        : right.LaneIndex.CompareTo(left.LaneIndex);
                }));

            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                if (!_bySupply.TryGetValue(lane.Supply, out var fromSupply))
                    _bySupply[lane.Supply] = fromSupply = new List<int>();
                fromSupply.Add(i);
                if (!_byDemand.TryGetValue(lane.Demand, out var toDemand))
                    _byDemand[lane.Demand] = toDemand = new List<int>();
                toDemand.Add(i);
                Recompute(i);
            }
        }

        public (int LaneIndex, ulong ProfitableQuantity,
            FoodTransportCostFraction MarginalValue)? Best
        {
            get
            {
                var best = _ordered.Count == 0 ? null : _ordered.Max;
                return best is null ? null : (
                    best.LaneIndex, best.ProfitableQuantity, best.MarginalValue);
            }
        }

        public void Refresh(SupplyBalance supply, DemandBalance demand)
        {
            var dirty = new SortedSet<int>();
            if (_bySupply.TryGetValue(supply, out var sourceLanes))
                foreach (var index in sourceLanes)
                    dirty.Add(index);
            if (_byDemand.TryGetValue(demand, out var destinationLanes))
                foreach (var index in destinationLanes)
                    dirty.Add(index);

            // Canonical ascending indices: results and diagnostics remain
            // deterministic, even for a lane sharing both dependencies.
            foreach (var index in dirty)
                Recompute(index);
        }

        private void Recompute(int index)
        {
            var prior = _current[index];
            if (prior is not null)
            {
                if (!_ordered.Remove(prior))
                    throw new InvalidOperationException(
                        "Indexed market candidate was not found.");
                _current[index] = null;
            }

            _diagnostics?.ExamineLane();
            var lane = _lanes[index];
            var remaining = Math.Min(lane.Supply.Remaining, lane.Demand.Remaining);
            if (remaining == 0UL)
                return;

            _diagnostics?.EvaluateMarginal();
            var marginal = lane.Curve.EvaluateMarginalRawUnitValue(
                lane.Demand.Allocated);
            ArgumentNullException.ThrowIfNull(marginal);
            var net = SubtractSigned(marginal, lane.UnitExpense);
            if (net.Numerator.Sign <= 0)
                return;

            var maximum = Math.Min(remaining, _policy.RawDecisionQuantum);
            var profitable = LargestProfitableQuantity(
                lane, maximum, _diagnostics);
            if (profitable == 0UL)
                return;

            var candidate = new Candidate(index, net, profitable, marginal);
            if (!_ordered.Add(candidate))
                throw new InvalidOperationException(
                    "Duplicate indexed market candidate.");
            _current[index] = candidate;
        }
    }

    private static ulong LargestProfitableQuantity(
        Lane lane, ulong maximum, EconomicTurnFreightMarketDiagnostics? diagnostics)
    {
        diagnostics?.SearchQuantity();
        var start = lane.Demand.Allocated;
        diagnostics?.EvaluateMarginal();
        var atStart = lane.Curve.EvaluateMarginalRawUnitValue(start);
        diagnostics?.EvaluateMarginal();
        var atEnd = lane.Curve.EvaluateMarginalRawUnitValue(checked(start + maximum));
        if (atStart is null || atEnd is null ||
            atEnd.CompareTo(atStart) > 0)
            throw new InvalidOperationException(
                "Reference commodity marginal value must be nonincreasing.");

        if (SubtractSigned(atEnd, lane.UnitExpense).Numerator.Sign > 0)
            return maximum;

        ulong lower = 0UL;
        ulong upper = maximum;
        while (lower < upper)
        {
            diagnostics?.SearchStep();
            var midpoint = lower + (upper - lower) / 2UL + (upper - lower) % 2UL;
            diagnostics?.EvaluateMarginal();
            var value = lane.Curve.EvaluateMarginalRawUnitValue(
                checked(start + midpoint));
            if (value is null)
                throw new InvalidOperationException("Null commodity marginal value.");
            if (SubtractSigned(value, lane.UnitExpense).Numerator.Sign > 0)
                lower = midpoint;
            else
                upper = midpoint - 1UL;
        }
        return lower;
    }

    private static T[] Snapshot<T>(IEnumerable<T> values, string arg) where T : class
    {
        var rows = values.ToArray();
        if (rows.Any(x => x is null))
            throw new ArgumentException("Reference input cannot contain null.", arg);
        return rows;
    }

    private static SignedMargin SubtractSigned(
        FoodTransportCostFraction first, FoodTransportCostFraction second) =>
        new(first.Numerator * second.Denominator -
                second.Numerator * first.Denominator,
            first.Denominator * second.Denominator);

    private static int CompareSigned(SignedMargin first, SignedMargin second) =>
        (first.Numerator * second.Denominator).CompareTo(
            second.Numerator * first.Denominator);

    private static FoodTransportCostFraction SubtractNonnegative(
        FoodTransportCostFraction first, FoodTransportCostFraction second)
    {
        var n = first.Numerator * second.Denominator -
            second.Numerator * first.Denominator;
        if (n.Sign < 0)
            throw new InvalidOperationException(
                "Integrated return cannot be negative.");
        return new FoodTransportCostFraction(
            n, first.Denominator * second.Denominator);
    }

    private readonly record struct SignedMargin(BigInteger Numerator, BigInteger Denominator);

    private sealed class SupplyBalance(EconomicSupplyFlow flow)
    {
        public EconomicSupplyFlow Flow { get; } = flow;
        public ulong Remaining { get; set; } = flow.RawQuantity;
    }

    private sealed class DemandBalance(EconomicDemandFlow flow)
    {
        public EconomicDemandFlow Flow { get; } = flow;
        public ulong Remaining { get; set; } = flow.RawQuantity;
        public ulong Allocated { get; set; }
    }

    private sealed class Lane
    {
        public SupplyBalance Supply { get; }
        public DemandBalance Demand { get; }
        public FoodCommodityDefinition Food { get; }
        public ulong RawUnitReservationCost { get; }
        public IEconomicTurnCommodityValueCurve Curve { get; }
        public EconomicTurnFreightQuote Quote { get; }
        public FoodTransportCostFraction UnitExpense { get; }

        public Lane(
            SupplyBalance supply, DemandBalance demand,
            FoodCommodityDefinition food, ulong rawUnitReservationCost,
            IEconomicTurnCommodityValueCurve curve,
            EconomicTurnFreightQuote quote)
        {
            Supply = supply;
            Demand = demand;
            Food = food;
            RawUnitReservationCost = rawUnitReservationCost;
            Curve = curve;
            Quote = quote;
            UnitExpense = new FoodTransportCostFraction(
                (BigInteger)rawUnitReservationCost *
                    quote.RawUnitCostPerGram.Denominator +
                    quote.RawUnitCostPerGram.Numerator * food.GramsPerFlowUnit,
                quote.RawUnitCostPerGram.Denominator);
        }
    }
}
