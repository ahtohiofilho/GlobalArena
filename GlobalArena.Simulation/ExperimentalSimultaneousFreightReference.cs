using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Exact SIGNED net welfare. It is not a payable, nonnegative freight charge.
/// This fraction represents integrated buyer value MINUS producer reservation
/// MINUS the aggregate VS42 geographic/operational/shared-corridor freight.
/// </summary>
public readonly record struct ExperimentalSignedEconomicValue
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public ExperimentalSignedEconomicValue(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator));
        var gcd = BigInteger.GreatestCommonDivisor(
            BigInteger.Abs(numerator), denominator);
        Numerator = numerator / gcd;
        Denominator = denominator / gcd;
    }

    public int CompareTo(ExperimentalSignedEconomicValue other) =>
        (Numerator * other.Denominator)
            .CompareTo(other.Numerator * Denominator);
}

/// <summary>
/// An eligible commodity lane and its already offered whole-service options.
/// Real services must have passed the VS42 FromBuiltOffer attestation at this
/// external boundary; synthetic options are only for tests/benchmarks.
/// </summary>
public sealed class ExperimentalSimultaneousFreightLane
{
    public EconomicSupplyFlow Supply { get; }
    public EconomicDemandFlow Demand { get; }
    public IReadOnlyList<ExperimentalSharedFreightOption> Services { get; }

    public ExperimentalSimultaneousFreightLane(
        EconomicSupplyFlow supply, EconomicDemandFlow demand,
        IEnumerable<ExperimentalSharedFreightOption> services)
    {
        Supply = supply ?? throw new ArgumentNullException(nameof(supply));
        Demand = demand ?? throw new ArgumentNullException(nameof(demand));
        ArgumentNullException.ThrowIfNull(services);
        var options = services.ToArray();
        if (supply.CommodityId != demand.CommodityId ||
            options.Length == 0 || options.Any(x => x is null) ||
            options.Select(x => x.ServiceId).Distinct().Count() != options.Length ||
            options.Any(x =>
                x.RouteKey.SourceEconomicPointId != supply.EconomicPointId ||
                x.RouteKey.DestinationEconomicPointId != demand.EconomicPointId))
            throw new ArgumentException(
                "A finite commodity lane requires matching supplied/demanded goods and real route keys.",
                nameof(services));
        Services = Array.AsReadOnly(options.OrderBy(x => x.ServiceId).ToArray());
    }
}

public sealed record ExperimentalSimultaneousFreightDecision(
    EconomicPointId Source,
    EconomicPointId Destination,
    CommodityId Commodity,
    ulong ServiceId,
    ulong RawQuantity);

public sealed record ExperimentalSimultaneousFreightAllocation(
    ExperimentalSignedEconomicValue NetEconomicValue,
    FoodTransportCostFraction IntegratedBuyerValue,
    FoodTransportCostFraction ProducerReservationCost,
    FoodTransportCostFraction SharedFreightCost,
    IReadOnlyList<ExperimentalSimultaneousFreightDecision> Trades,
    IReadOnlyDictionary<ExperimentalSharedFreightCorridorKey, BigInteger> CorridorLoads);

public sealed record ExperimentalSimultaneousFreightComparison(
    ExperimentalSimultaneousFreightAllocation Independent,
    ExperimentalSimultaneousFreightAllocation Coordinated,
    ExperimentalSimultaneousFreightAllocation? Exact,
    BigInteger PossibleConfigurations,
    int CandidateEvaluations,
    int LocalPasses);

/// <summary>
/// Independent = each lane's best standalone volume/service ignoring OTHER
/// lanes. Coordinated = strictly improving local search across entire
/// feasible snapshot, including ability to shut a lane down.
/// Exact = bounded exhaustive reference. Not a live simultaneous market:
/// volumes are quantized, prices are not settled, buyer money budgets are
/// not modeled, and local search does not certify a global optimum.
/// </summary>
public sealed class ExperimentalSimultaneousFreightReference
{
    private sealed record Row(
        ExperimentalSimultaneousFreightLane Lane,
        ulong MaximumQuanta,
        ulong GramsPerFlowUnit,
        ulong ReservationCost);

    private sealed record State(int[] Choices,
        ExperimentalSimultaneousFreightAllocation Allocation);

    private readonly record struct Variant(ulong RawQuantity, int ServiceIndex);

    private static readonly BigInteger Unit =
        EconomicTransferPolicy.FixedPointDenominator;

    public ExperimentalSimultaneousFreightComparison Compare(
        EconomicProductionDemandResult flows,
        FoodCommodityCatalog catalog,
        IEnumerable<EconomicTurnCommodityValueOffer> valueOffers,
        IEnumerable<EconomicTurnSourceReservation> reservations,
        IEnumerable<ExperimentalSimultaneousFreightLane> candidateLanes,
        ExperimentalSharedFreightCongestionPolicy corridorPolicy,
        ulong rawQuantityQuantum,
        int maxLocalPasses = 4,
        int maxExactConfigurations = 0)
    {
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(valueOffers);
        ArgumentNullException.ThrowIfNull(reservations);
        ArgumentNullException.ThrowIfNull(candidateLanes);
        ArgumentNullException.ThrowIfNull(corridorPolicy);
        if (rawQuantityQuantum == 0UL || maxLocalPasses < 0 ||
            maxExactConfigurations < 0)
            throw new ArgumentOutOfRangeException(nameof(rawQuantityQuantum));

        var valuations = valueOffers.ToArray();
        var reserveRows = reservations.ToArray();
        var lanes = candidateLanes.ToArray();
        if (lanes.Length == 0 || lanes.Any(x => x is null))
            throw new ArgumentException("At least one valid offered lane is required.",
                nameof(candidateLanes));

        var demandCurves = new Dictionary<(EconomicPointId, CommodityId),
            IEconomicTurnCommodityValueCurve>();
        foreach (var v in valuations)
        {
            if (v is null || !demandCurves.TryAdd(
                (v.EconomicPointId, v.CommodityId), v.Curve))
                throw new ArgumentException("Duplicate or invalid demand valuation.",
                    nameof(valueOffers));
        }
        var reservationCosts = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), ulong>();
        foreach (var r in reserveRows)
        {
            if (r is null || !reservationCosts.TryAdd(
                (r.EconomicPointId, r.CommodityId, r.Activity),
                r.RawUnitReservationCost))
                throw new ArgumentException("Duplicate or invalid source reservation.",
                    nameof(reservations));
        }

        var supply = flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity), x => x.RawQuantity);
        var demand = flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        if (demandCurves.Count != demand.Count ||
            reservationCosts.Count != supply.Count ||
            demand.Keys.Any(k => !demandCurves.ContainsKey(k)) ||
            supply.Keys.Any(k => !reservationCosts.ContainsKey(k)))
            throw new ArgumentException(
                "Every declared supply and demand requires a matching reservation or valuation.");

        var ordered = lanes.OrderBy(x => x.Supply.CommodityId.Value)
            .ThenBy(x => x.Supply.EconomicPointId.Value)
            .ThenBy(x => (byte)x.Supply.Activity)
            .ThenBy(x => x.Demand.EconomicPointId.Value).ToArray();
        if (ordered.Select(x => (
                x.Supply.EconomicPointId, x.Supply.CommodityId, x.Supply.Activity,
                x.Demand.EconomicPointId)).Distinct().Count() != ordered.Length)
            throw new ArgumentException("A source/destination/commodity lane must be unique.",
                nameof(candidateLanes));

        var rows = new Row[ordered.Length];
        var choices = new Variant[ordered.Length][];
        var product = BigInteger.One;
        for (var i = 0; i < ordered.Length; i++)
        {
            var lane = ordered[i];
            var sourceKey = (lane.Supply.EconomicPointId,
                lane.Supply.CommodityId, lane.Supply.Activity);
            var demandKey = (lane.Demand.EconomicPointId, lane.Demand.CommodityId);
            if (!supply.TryGetValue(sourceKey, out var available) ||
                !demand.TryGetValue(demandKey, out var required) ||
                !catalog.TryGet(lane.Supply.CommodityId, out var food) ||
                food is null)
                throw new ArgumentException(
                    "All candidate lanes must refer to existing flows and explicit commodity mass.",
                    nameof(candidateLanes));

            var maximum = Math.Min(available, required) / rawQuantityQuantum;
            if (maximum > 24UL)
                throw new ArgumentOutOfRangeException(nameof(rawQuantityQuantum),
                    "The VS43 finite reference allows at most 24 quanta per candidate lane.");
            rows[i] = new Row(lane, maximum, food.GramsPerFlowUnit,
                reservationCosts[sourceKey]);
            var variants = new List<Variant> { new(0UL, -1) };
            for (ulong q = 1UL; q <= maximum; q++)
                for (var service = 0; service < lane.Services.Count; service++)
                    variants.Add(new Variant(
                        checked(q * rawQuantityQuantum), service));
            choices[i] = variants.ToArray();
            product *= variants.Count;
        }

        var evaluations = 0;
        ExperimentalSimultaneousFreightAllocation? Evaluate(int[] selected)
        {
            evaluations++;
            var usedSupply = new Dictionary<
                (EconomicPointId, CommodityId, EconomicActivityKind), BigInteger>();
            var usedDemand = new Dictionary<(EconomicPointId, CommodityId), BigInteger>();
            var freightLots = new List<ExperimentalSharedFreightLot>();
            var trades = new List<ExperimentalSimultaneousFreightDecision>();
            var costReserve = new FoodTransportCostFraction(0, 1);
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                var variant = choices[i][selected[i]];
                if (variant.RawQuantity == 0UL) continue;
                var origin = row.Lane.Supply;
                var destination = row.Lane.Demand;
                var sourceKey = (origin.EconomicPointId, origin.CommodityId,
                    origin.Activity);
                var demandKey = (destination.EconomicPointId, destination.CommodityId);
                usedSupply.TryGetValue(sourceKey, out var sourceTotal);
                usedDemand.TryGetValue(demandKey, out var demandTotal);
                usedSupply[sourceKey] = sourceTotal + variant.RawQuantity;
                usedDemand[demandKey] = demandTotal + variant.RawQuantity;
                if (usedSupply[sourceKey] > supply[sourceKey] ||
                    usedDemand[demandKey] > demand[demandKey])
                    return null;

                var option = row.Lane.Services[variant.ServiceIndex];
                var q = (BigInteger)variant.RawQuantity * row.GramsPerFlowUnit;
                freightLots.Add(new ExperimentalSharedFreightLot(
                    checked((ulong)i + 1UL), q, new[] { option }));
                trades.Add(new ExperimentalSimultaneousFreightDecision(
                    origin.EconomicPointId, destination.EconomicPointId,
                    origin.CommodityId, option.ServiceId, variant.RawQuantity));
                costReserve = ExperimentalSharedFreightBatchCost.Add(
                    costReserve, new FoodTransportCostFraction(
                        (BigInteger)variant.RawQuantity * row.ReservationCost, Unit));
            }

            var integratedValue = new FoodTransportCostFraction(0, 1);
            foreach (var (key, q) in usedDemand)
            {
                var delivered = checked((ulong)q);
                var marginalCurve = demandCurves[key];
                var value = marginalCurve.EvaluateIntegratedRawValue(delivered);
                ArgumentNullException.ThrowIfNull(value);
                integratedValue = ExperimentalSharedFreightBatchCost.Add(
                    integratedValue, value);
            }

            var freight = freightLots.Count > 0
                ? ExperimentalSharedFreightBatchCost.Evaluate(
                    freightLots, new int[freightLots.Count], corridorPolicy)
                : new ExperimentalSharedFreightBatchEvaluation(
                    new FoodTransportCostFraction(0, 1),
                    new System.Collections.ObjectModel.ReadOnlyDictionary<
                        ExperimentalSharedFreightCorridorKey, BigInteger>(
                        new Dictionary<ExperimentalSharedFreightCorridorKey, BigInteger>()));
            var totalExpense = ExperimentalSharedFreightBatchCost.Add(
                costReserve, freight.TotalCost);
            var net = new ExperimentalSignedEconomicValue(
                integratedValue.Numerator * totalExpense.Denominator -
                totalExpense.Numerator * integratedValue.Denominator,
                integratedValue.Denominator * totalExpense.Denominator);
            return new ExperimentalSimultaneousFreightAllocation(
                net, integratedValue, costReserve, freight.TotalCost,
                Array.AsReadOnly(trades.ToArray()), freight.CorridorLoads);
        }

        var baseline = new int[rows.Length];
        // Independent per-lane maximization may oversubscribe shared supplies
        // and demands. Repair in canonical order by selecting only feasible
        // variants, ignoring congestion with other lanes.
        for (var i = 0; i < rows.Length; i++)
        {
            var bestIndex = 0;
            ExperimentalSignedEconomicValue? bestStandalone = null;
            for (var candidate = 0; candidate < choices[i].Length; candidate++)
            {
                var trial = (int[])baseline.Clone();
                trial[i] = candidate;
                // For a standalone comparison, earlier lanes are disabled.
                var alone = new int[rows.Length];
                alone[i] = candidate;
                var hypothetical = Evaluate(alone);
                if (hypothetical is null) continue;
                if (bestStandalone is not null &&
                    hypothetical.NetEconomicValue.CompareTo(bestStandalone.Value) <= 0)
                    continue;
                if (Evaluate(trial) is null) continue;
                bestStandalone = hypothetical.NetEconomicValue;
                bestIndex = candidate;
            }
            baseline[i] = bestIndex;
        }

        var independent = Evaluate(baseline)!;
        var selected = (int[])baseline.Clone();
        var coordinated = independent;
        var passes = 0;
        for (var pass = 0; pass < maxLocalPasses; pass++)
        {
            var improved = false;
            for (var i = 0; i < rows.Length; i++)
            {
                var winningChoice = selected[i];
                var winningValue = coordinated.NetEconomicValue;
                ExperimentalSimultaneousFreightAllocation? winning = null;
                for (var candidate = 0; candidate < choices[i].Length; candidate++)
                {
                    if (candidate == selected[i]) continue;
                    selected[i] = candidate;
                    var alternative = Evaluate(selected);
                    if (alternative is null ||
                        alternative.NetEconomicValue.CompareTo(winningValue) <= 0)
                        continue;
                    winningChoice = candidate;
                    winningValue = alternative.NetEconomicValue;
                    winning = alternative;
                }
                selected[i] = winningChoice;
                if (winning is not null)
                {
                    coordinated = winning;
                    improved = true;
                }
            }
            passes++;
            if (!improved) break;
        }

        ExperimentalSimultaneousFreightAllocation? exact = null;
        if (maxExactConfigurations > 0 && product <= maxExactConfigurations)
        {
            var indices = new int[rows.Length];
            void Enumerate(int idx)
            {
                if (idx == rows.Length)
                {
                    var evaluated = Evaluate(indices);
                    if (evaluated is not null &&
                        (exact is null ||
                         evaluated.NetEconomicValue.CompareTo(exact.NetEconomicValue) > 0))
                        exact = evaluated;
                    return;
                }
                for (var j = 0; j < choices[idx].Length; j++)
                {
                    indices[idx] = j;
                    Enumerate(idx + 1);
                }
            }
            Enumerate(0);
            if (exact is null ||
                coordinated.NetEconomicValue.CompareTo(exact.NetEconomicValue) > 0)
                throw new InvalidOperationException(
                    "An exhaustively enumerated reference must bound the heuristic.");
        }

        return new ExperimentalSimultaneousFreightComparison(
            independent, coordinated, exact, product, evaluations, passes);
    }
}
