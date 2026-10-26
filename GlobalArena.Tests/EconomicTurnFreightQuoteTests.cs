using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicTurnFreightQuoteTests
{
    private static StrategicEconomicRouteKey Route() => new(
        new EconomicPointId(1UL), new EconomicPointId(2UL));

    /// <summary>
    /// Synthetic continuous function for *reference tests only*:
    /// raw cost/gram = 20 + 60,000 / (100 + expected grams).
    /// It has economies of expected utilization and no load threshold.
    /// This is NOT a ruleset default, modal price or final calibration.
    /// </summary>
    private sealed class SyntheticContinuousFreightCostFunction
        : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var microgramsPerGram = new BigInteger(1_000_000);
            var denominator = expectedLoadMicrograms +
                100 * microgramsPerGram;
            var numerator = 20 * denominator +
                60_000 * microgramsPerGram;
            return new FoodTransportCostFraction(numerator, denominator);
        }
    }

    // Separate, illustrative continuous *commodity market value* curve:
    // raw value per whole commodity unit falls as the good is allocated.
    // It is NOT a transport function and not a game balance policy.
    private static FoodTransportCostFraction CommodityMarginalValue(
        ulong alreadyAllocatedRawUnits)
    {
        var scale = new BigInteger(
            EconomicTransferPolicy.FixedPointDenominator);
        var numerator = 300_000 * scale -
            40_000 * (BigInteger)alreadyAllocatedRawUnits;
        return new FoodTransportCostFraction(numerator, scale);
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a,
        FoodTransportCostFraction b) => new(
            a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    [Fact]
    public void TwoTurnsLockFreightCostWhileCommodityValueChangesWithinTurn()
    {
        var function = new SyntheticContinuousFreightCostFunction();
        var initial = WorldState.CreateInitial();
        var beforeHash = new CanonicalWorldStateHasher().Compute(initial);
        var initialForecast = new BigInteger(900_000_000); // 900 grams

        var firstTurn = EconomicTurnFreightQuote.FromForecast(
            1UL, Route(), initialForecast, function);
        Assert.Equal(1, function.Evaluations);
        Assert.Equal(1UL, firstTurn.TurnOrdinal);
        Assert.Equal(1UL, firstTurn.RulesetVersion);
        Assert.Equal(initialForecast, firstTurn.ExpectedLoadMicrograms);
        Assert.Equal(new BigInteger(80),
            firstTurn.RawUnitCostPerGram.Numerator);
        Assert.Equal(BigInteger.One,
            firstTurn.RawUnitCostPerGram.Denominator);

        var commodityValues = new List<FoodTransportCostFraction>();
        var freightCharges = new List<FoodTransportCostFraction>();
        const ulong quantum = 1_000_000UL; // one whole commodity-flow unit
        const ulong gramsPerFlowUnit = 1_000UL;

        for (ulong parcel = 0UL; parcel < 3UL; parcel++)
        {
            commodityValues.Add(CommodityMarginalValue(parcel * quantum));
            freightCharges.Add(firstTurn.ChargeForRawQuantity(
                quantum, gramsPerFlowUnit));
        }

        // The commodity's marginal value changes at each trade allocation.
        Assert.True(commodityValues[0].CompareTo(commodityValues[1]) > 0);
        Assert.True(commodityValues[1].CompareTo(commodityValues[2]) > 0);

        // All three freight parcels use the same *pre-turn* evaluated cost.
        Assert.All(freightCharges, charge =>
        {
            Assert.Equal(new BigInteger(80_000), charge.Numerator);
            Assert.Equal(BigInteger.One, charge.Denominator);
        });
        Assert.Equal(1, function.Evaluations);
        Assert.All(commodityValues, value =>
            Assert.True(value.CompareTo(freightCharges[0]) > 0));

        // Realized first-turn freight: three commodity units x 1000 g.
        // It informs the *next* turn's forecast, never reprices turn one.
        var nextForecast = new BigInteger(3_000_000_000);
        var secondTurn = EconomicTurnFreightQuote.FromForecast(
            2UL, Route(), nextForecast, function);
        Assert.Equal(2, function.Evaluations);
        Assert.True(secondTurn.RawUnitCostPerGram.CompareTo(
            firstTurn.RawUnitCostPerGram) < 0);
        Assert.Equal(new BigInteger(80_000),
            firstTurn.ChargeForRawQuantity(
                quantum, gramsPerFlowUnit).Numerator);
        Assert.Equal(beforeHash, new CanonicalWorldStateHasher().Compute(initial));
    }

    [Fact]
    public void SplittingOrReorderingParcelsDoesNotChangeLockedFreightCharge()
    {
        var quote = EconomicTurnFreightQuote.FromForecast(
            1UL, Route(), new BigInteger(900_000_000),
            new SyntheticContinuousFreightCostFunction());

        var whole = quote.ChargeForRawQuantity(3_000_000UL, 1_000UL);
        var pieces = new[] { 250_000UL, 1_000_000UL, 1_750_000UL };
        var combined = pieces
            .Select(q => quote.ChargeForRawQuantity(q, 1_000UL))
            .Aggregate(new FoodTransportCostFraction(0, 1), Add);
        var reverseCombined = pieces.Reverse()
            .Select(q => quote.ChargeForRawQuantity(q, 1_000UL))
            .Aggregate(new FoodTransportCostFraction(0, 1), Add);

        Assert.Equal(0, whole.CompareTo(combined));
        Assert.Equal(0, whole.CompareTo(reverseCombined));
        Assert.Equal(new BigInteger(240_000), whole.Numerator);
    }

    [Fact]
    public void IdenticalForecastAndVersionRepeatExactUnitCost()
    {
        var function = new SyntheticContinuousFreightCostFunction();
        var a = EconomicTurnFreightQuote.FromForecast(
            4UL, Route(), new BigInteger(1_234_567_890), function);
        var b = EconomicTurnFreightQuote.FromForecast(
            4UL, Route(), new BigInteger(1_234_567_890), function);
        Assert.Equal(a.TurnOrdinal, b.TurnOrdinal);
        Assert.Equal(a.RouteKey, b.RouteKey);
        Assert.Equal(a.RulesetVersion, b.RulesetVersion);
        Assert.Equal(a.ExpectedLoadMicrograms, b.ExpectedLoadMicrograms);
        Assert.Equal(a.RawUnitCostPerGram.Numerator, b.RawUnitCostPerGram.Numerator);
        Assert.Equal(a.RawUnitCostPerGram.Denominator, b.RawUnitCostPerGram.Denominator);
    }

    [Fact]
    public void ZeroForecastAndVeryLargeTradeHaveNoSyntheticCapacityCeiling()
    {
        var quote = EconomicTurnFreightQuote.FromForecast(
            1UL, Route(), BigInteger.Zero,
            new SyntheticContinuousFreightCostFunction());

        var hugeCharge = quote.ChargeForRawQuantity(
            ulong.MaxValue, ulong.MaxValue);
        Assert.True(hugeCharge.Numerator > BigInteger.Zero);
        Assert.Equal(BigInteger.Zero,
            quote.ChargeForRawQuantity(0UL, 1_000UL).Numerator);
    }

    [Fact]
    public void InvalidTurnForecastPolicyAndPhysicalMassFailClosed()
    {
        var function = new SyntheticContinuousFreightCostFunction();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EconomicTurnFreightQuote.FromForecast(
                0UL, Route(), BigInteger.Zero, function));
        Assert.Throws<ArgumentException>(() =>
            EconomicTurnFreightQuote.FromForecast(
                1UL, default, BigInteger.Zero, function));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EconomicTurnFreightQuote.FromForecast(
                1UL, Route(), -BigInteger.One, function));
        Assert.Throws<ArgumentNullException>(() =>
            EconomicTurnFreightQuote.FromForecast(
                1UL, Route(), BigInteger.Zero, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EconomicTurnFreightQuote.FromForecast(
                1UL, Route(), BigInteger.Zero,
                new InvalidVersionCostFunction()));

        var quote = EconomicTurnFreightQuote.FromForecast(
            1UL, Route(), BigInteger.Zero, function);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            quote.ChargeForRawQuantity(1_000_000UL, 0UL));
    }

    private sealed class InvalidVersionCostFunction
        : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 0UL;
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms) => new(1, 1);
    }
}
