using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class EconomicTurnLockedFreightMarketTests
{
    private const ulong One = EconomicTransferPolicy.FixedPointDenominator;

    private static EconomicPointId P(ulong id) => new(id);
    private static CommodityId C(uint id) => new(id);
    private static StrategicEconomicRouteKey Route(ulong source, ulong dest) =>
        new(P(source), P(dest));

    /// <summary>
    /// Synthetic continuous decrease in marginal commodity MARKET valuation.
    /// These test parameters are not gameplay price/balance coefficients.
    /// Marginal: a - b*(rawQuantity / D); integrated: a*x - b*x*x/2,
    /// where x is in whole-flow-unit equivalents.
    /// </summary>
    private sealed class LinearCommodityMarketValue(
        ulong initialRawUnitValue, ulong slopeRawUnitValue)
        : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong alreadyAllocatedRawQuantity)
        {
            var numerator = (BigInteger)initialRawUnitValue * One -
                (BigInteger)slopeRawUnitValue * alreadyAllocatedRawQuantity;
            if (numerator < 0)
                throw new InvalidOperationException("Synthetic demand beyond nonnegative range.");
            return new FoodTransportCostFraction(numerator, One);
        }

        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong allocatedRawQuantity)
        {
            var q = (BigInteger)allocatedRawQuantity;
            var numerator = 2 * (BigInteger)initialRawUnitValue * q * One -
                (BigInteger)slopeRawUnitValue * q * q;
            if (numerator < 0)
                throw new InvalidOperationException("Synthetic demand beyond nonnegative range.");
            return new FoodTransportCostFraction(numerator, 2 * (BigInteger)One * One);
        }
    }

    // Synthetic smooth utilization-based freight function used only in tests;
    // no transport technology, discount law, or numeric tariff is prescribed.
    private sealed class SmoothFreightCost : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            var d = expectedLoadMicrograms + 100_000_000;
            return new FoodTransportCostFraction(
                20 * d + 60_000_000_000, d);
        }
    }

    private static EconomicTurnFreightQuote Quote(
        ulong turn, ulong source, ulong dest, ulong expectedWholeGrams)
        => EconomicTurnFreightQuote.FromForecast(
            turn, Route(source, dest),
            (BigInteger)expectedWholeGrams * 1_000_000,
            new SmoothFreightCost());

    private static FoodCommodityCatalog Foods() => new(new[]
    {
        new FoodCommodityDefinition(C(7), "grain", 1_000UL),
        new FoodCommodityDefinition(C(8), "roots", 2_000UL)
    });

    private static EconomicProductionDemandResult Flows() => new(
        new[]
        {
            new EconomicSupplyFlow(P(1), C(7), EconomicActivityKind.Agriculture, 3*One),
            new EconomicSupplyFlow(P(2), C(7), EconomicActivityKind.Mining, 2*One),
            new EconomicSupplyFlow(P(2), C(8), EconomicActivityKind.Agriculture, 3*One)
        },
        new[]
        {
            new EconomicDemandFlow(P(3), C(7), 3*One),
            new EconomicDemandFlow(P(4), C(7), 2*One),
            new EconomicDemandFlow(P(3), C(8), 3*One)
        });

    private static EconomicTurnCommodityValueOffer[] Values() => new[]
    {
        new EconomicTurnCommodityValueOffer(P(3), C(7),
            new LinearCommodityMarketValue(800_000UL, 100_000UL)),
        new EconomicTurnCommodityValueOffer(P(4), C(7),
            new LinearCommodityMarketValue(760_000UL, 50_000UL)),
        new EconomicTurnCommodityValueOffer(P(3), C(8),
            new LinearCommodityMarketValue(840_000UL, 80_000UL))
    };

    private static EconomicTurnSourceReservation[] Reservations() => new[]
    {
        new EconomicTurnSourceReservation(P(1), C(7),
            EconomicActivityKind.Agriculture, 50_000UL),
        new EconomicTurnSourceReservation(P(2), C(7),
            EconomicActivityKind.Mining, 100_000UL),
        new EconomicTurnSourceReservation(P(2), C(8),
            EconomicActivityKind.Agriculture, 20_000UL)
    };

    private static EconomicTurnFreightQuote[] Quotes(ulong turn) => new[]
    {
        Quote(turn, 1UL, 3UL, 900UL),  // synthetic 80 raw / gram
        Quote(turn, 1UL, 4UL, 900UL),
        Quote(turn, 2UL, 3UL, 2_900UL), // synthetic 40 raw / gram
        Quote(turn, 2UL, 4UL, 2_900UL)
    };

    private static IReadOnlyList<EconomicTurnLockedFreightTrade> Resolve(
        ulong turn = 1UL,
        EconomicProductionDemandResult? flows = null,
        IEnumerable<EconomicTurnFreightQuote>? quotes = null,
        IEnumerable<EconomicTurnCommodityValueOffer>? values = null,
        IEnumerable<EconomicTurnSourceReservation>? reservations = null,
        EconomicCongestionMarketSearchPolicy? policy = null) =>
        new EconomicTurnLockedFreightMarketResolver().Resolve(
            turn, flows ?? Flows(), Foods(), quotes ?? Quotes(turn),
            values ?? Values(), reservations ?? Reservations(),
            policy ?? new EconomicCongestionMarketSearchPolicy(One, 30));

    [Fact]
    public void MultipleCommoditiesAndDestinationsCompeteOnChangingMarketValueNotFreight()
    {
        var results = Resolve();
        Assert.Equal(8, results.Count);
        Assert.Equal(C(8), results[0].CommodityId);
        Assert.Contains(results, x => x.CommodityId == C(7));
        Assert.Contains(results, x => x.SourceEconomicPointId == P(1));
        Assert.Contains(results, x => x.SourceEconomicPointId == P(2));
        Assert.Contains(results, x => x.DestinationEconomicPointId == P(3));
        Assert.Contains(results, x => x.DestinationEconomicPointId == P(4));

        foreach (var item in results)
        {
            Assert.Equal(1UL, item.TurnOrdinal);
            Assert.Equal(One, item.RawQuantity);
            Assert.Equal((ulong)results.IndexOf(item) + 1UL,
                item.DecisionOrdinal);
            Assert.True(item.RawNetReturn.Numerator > 0);
            Assert.Equal(1UL, item.FreightQuote.TurnOrdinal);
            var expected = item.FreightQuote.ChargeForRawQuantity(
                item.RawQuantity, item.CommodityId == C(7) ? 1_000UL : 2_000UL);
            Assert.Equal(0, item.RawFreightCharge.CompareTo(expected));
        }

        // At this destination, different suppliers share *one* commodity
        // demand curve; allocations are not reset when the supplier changes.
        var grainAtThird = results.Where(x =>
            x.CommodityId == C(7) && x.DestinationEconomicPointId == P(3))
            .ToArray();
        Assert.Equal(3, grainAtThird.Length);
        Assert.True(grainAtThird[0].RawMarginalCommodityUnitValueAtDecision
            .CompareTo(grainAtThird[1].RawMarginalCommodityUnitValueAtDecision) > 0);
        Assert.True(grainAtThird[1].RawMarginalCommodityUnitValueAtDecision
            .CompareTo(grainAtThird[2].RawMarginalCommodityUnitValueAtDecision) > 0);

        Assert.Equal(5 * One,
            results.Where(x => x.CommodityId == C(7))
                .Aggregate(0UL, (total, x) => checked(total + x.RawQuantity)));
        Assert.Equal(3 * One,
            results.Where(x => x.CommodityId == C(8))
                .Aggregate(0UL, (total, x) => checked(total + x.RawQuantity)));
        Assert.Equal(2, results.Select(x => x.CommodityId).Distinct().Count());
        Assert.Equal(4, results.Select(x => x.FreightQuote.RouteKey).Distinct().Count());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EconomicTurnLockedFreightTrade>)results).Clear());
    }

    [Fact]
    public void ReversingAllSuppliedInputsDoesNotChangeCanonicalDecisionsOrSettlement()
    {
        var original = Resolve();
        var reversed = Resolve(quotes: Quotes(1).Reverse(),
            values: Values().Reverse(), reservations: Reservations().Reverse());

        string Fingerprint(EconomicTurnLockedFreightTrade x) =>
            $"{x.DecisionOrdinal}:{x.CommodityId.Value}:" +
            $"{x.SourceEconomicPointId.Value}:{x.DestinationEconomicPointId.Value}:" +
            $"{x.RawQuantity}:{x.RawCommodityValue.Numerator}/{x.RawCommodityValue.Denominator}:" +
            $"{x.RawFreightCharge.Numerator}/{x.RawFreightCharge.Denominator}:" +
            $"{x.RawNetReturn.Numerator}/{x.RawNetReturn.Denominator}";

        Assert.Equal(original.Select(Fingerprint), reversed.Select(Fingerprint));
    }

    [Fact]
    public void NewTurnCanRequoteButPreviousTurnsTradeAuditCannotReprice()
    {
        var old = Resolve();
        var oldFreight = old.Select(x =>
            (x.RawFreightCharge.Numerator, x.RawFreightCharge.Denominator)).ToArray();

        // Requote from updated ex-ante expectation in the following turn.
        var updatedQuotes = new[]
        {
            Quote(2, 1, 3, 4_900), Quote(2, 1, 4, 4_900),
            Quote(2, 2, 3, 5_900), Quote(2, 2, 4, 5_900)
        };
        var next = Resolve(2UL, quotes: updatedQuotes);

        Assert.Equal(8, next.Count);
        Assert.True(next[0].FreightQuote.RawUnitCostPerGram.CompareTo(
            old[0].FreightQuote.RawUnitCostPerGram) < 0);
        Assert.Equal(oldFreight, old.Select(x =>
            (x.RawFreightCharge.Numerator, x.RawFreightCharge.Denominator)).ToArray());
        Assert.All(next, x => Assert.Equal(2UL, x.TurnOrdinal));
        Assert.All(old, x => Assert.Equal(1UL, x.TurnOrdinal));
    }

    [Fact]
    public void RejectsWrongTurnUnknownAndDuplicateQuotesAndMissingMarkets()
    {
        Assert.Throws<ArgumentException>(() =>
            Resolve(turn: 2UL, quotes: Quotes(1)));
        Assert.Throws<ArgumentException>(() =>
            Resolve(quotes: Quotes(1).Concat(Quotes(1).Take(1))));
        Assert.Throws<ArgumentException>(() =>
            Resolve(quotes: Quotes(1).Append(Quote(1, 99, 3, 900))));
        Assert.Throws<ArgumentException>(() =>
            Resolve(values: Values().Take(2)));
        Assert.Throws<ArgumentException>(() =>
            Resolve(values: Values().Concat(Values().Take(1))));
        Assert.Throws<ArgumentException>(() =>
            Resolve(reservations: Reservations().Take(2)));
        Assert.Throws<ArgumentException>(() =>
            Resolve(reservations: Reservations().Concat(Reservations().Take(1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => Resolve(turn: 0UL));
    }

    [Fact]
    public void FailsClosedAtDecisionBudgetWithoutReturningPartialMarket()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(policy: new EconomicCongestionMarketSearchPolicy(One, 1)));
    }

    [Fact]
    public void RejectsIncreasingOrInconsistentCommodityValueCurve()
    {
        var increasing = Values();
        increasing[0] = new EconomicTurnCommodityValueOffer(P(3), C(7),
            new IncreasingCommodityValue());
        Assert.Throws<InvalidOperationException>(() => Resolve(values: increasing));

        var inconsistent = Values();
        inconsistent[0] = new EconomicTurnCommodityValueOffer(P(3), C(7),
            new InconsistentIntegral());
        Assert.Throws<InvalidOperationException>(() => Resolve(values: inconsistent));
    }

    private sealed class IncreasingCommodityValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(800_000 + (BigInteger)q / One, 1);
        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new((BigInteger)q * 800_000, One);
    }

    private sealed class InconsistentIntegral : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong q) =>
            new(800_000, 1);
        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong q) =>
            new(BigInteger.Zero, BigInteger.One);
    }
}
