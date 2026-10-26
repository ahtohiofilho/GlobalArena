using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable, function-derived TRANSPORT unit-cost snapshot for one economic
/// turn and one directional relationship. Allocated volumes during this turn
/// do not update this quote; commodity market prices are a separate concern.
///
/// Quoted monetary units are raw economic cost per physical gram.
/// Raw trade quantities are fixed-point whole commodity-flow units; their
/// explicit grams-per-flow-unit scale is required when calculating a charge.
///
/// No infrastructure class, global transport tariff, physical capacity limit
/// or mutable WorldState entry is introduced by this reference contract.
/// </summary>
public sealed class EconomicTurnFreightQuote
{
    public ulong TurnOrdinal { get; }
    public StrategicEconomicRouteKey RouteKey { get; }
    public ulong RulesetVersion { get; }
    public BigInteger ExpectedLoadMicrograms { get; }
    public FoodTransportCostFraction RawUnitCostPerGram { get; }

    private EconomicTurnFreightQuote(
        ulong turnOrdinal,
        StrategicEconomicRouteKey routeKey,
        ulong rulesetVersion,
        BigInteger expectedLoadMicrograms,
        FoodTransportCostFraction rawUnitCostPerGram)
    {
        TurnOrdinal = turnOrdinal;
        RouteKey = routeKey;
        RulesetVersion = rulesetVersion;
        ExpectedLoadMicrograms = expectedLoadMicrograms;
        RawUnitCostPerGram = rawUnitCostPerGram;
    }

    /// <summary>
    /// Evaluates a caller-defined continuous pricing function exactly once
    /// from an ex-ante forecast. The caller owns the forecasting policy and
    /// supplies the corresponding ruleset version. No current-turn allocation
    /// or post-hoc load is read by the quote.
    /// </summary>
    public static EconomicTurnFreightQuote FromForecast(
        ulong turnOrdinal,
        StrategicEconomicRouteKey routeKey,
        BigInteger expectedLoadMicrograms,
        IEconomicTurnFreightCostFunction costFunction)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        if (!routeKey.SourceEconomicPointId.IsValid ||
            !routeKey.DestinationEconomicPointId.IsValid)
            throw new ArgumentException(
                "The transport relationship requires valid economic points.",
                nameof(routeKey));
        if (expectedLoadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedLoadMicrograms));
        ArgumentNullException.ThrowIfNull(costFunction);
        if (costFunction.RulesetVersion == 0UL)
            throw new ArgumentOutOfRangeException(nameof(costFunction),
                "The freight cost function needs a positive ruleset version.");

        var unitCost = costFunction.EvaluateRawUnitCostPerGram(
            expectedLoadMicrograms);
        if (unitCost is null)
            throw new InvalidOperationException(
                "A freight cost function must return an exact unit cost.");

        return new EconomicTurnFreightQuote(
            turnOrdinal, routeKey, costFunction.RulesetVersion,
            expectedLoadMicrograms, unitCost);
    }

    /// <summary>
    /// The exact *financial freight charge* at the turn-locked quote.
    /// This is not the integral of a varying intra-turn resource-cost curve,
    /// and such an integral must not be charged a second time.
    /// </summary>
    public FoodTransportCostFraction ChargeForRawQuantity(
        ulong rawCommodityQuantity,
        ulong gramsPerWholeFlowUnit)
    {
        if (gramsPerWholeFlowUnit == 0UL)
            throw new ArgumentOutOfRangeException(nameof(gramsPerWholeFlowUnit));

        // Trade quantity uses the same 1,000,000 fixed-point denominator
        // as the existing M4.4 and food-market monetary/unit contracts.
        var numerator = RawUnitCostPerGram.Numerator *
            rawCommodityQuantity * gramsPerWholeFlowUnit;
        var denominator = RawUnitCostPerGram.Denominator *
            EconomicTransferPolicy.FixedPointDenominator;
        return new FoodTransportCostFraction(numerator, denominator);
    }
}
