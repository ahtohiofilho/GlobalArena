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
        EconomicCongestionMarketSearchPolicy? policy = null,
        EconomicTurnFreightCandidateSelectionMode mode =
            EconomicTurnFreightCandidateSelectionMode.Exhaustive,
        EconomicTurnFreightMarketDiagnostics? diagnostics = null) =>
        new EconomicTurnLockedFreightMarketResolver().Resolve(
            turn, flows ?? Flows(), Foods(), quotes ?? Quotes(turn),
            values ?? Values(), reservations ?? Reservations(),
            policy ?? new EconomicCongestionMarketSearchPolicy(One, 30),
            diagnostics, mode);

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

        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            Assert.Equal(1UL, item.TurnOrdinal);
            Assert.Equal(One, item.RawQuantity);
            Assert.Equal((ulong)i + 1UL,
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
        Assert.Equal(3, results.Select(x => x.FreightQuote.RouteKey).Distinct().Count());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<EconomicTurnLockedFreightTrade>)results).Clear());
    }

    private static string ExactTradeTranscript(
        IReadOnlyList<EconomicTurnLockedFreightTrade> trades) =>
        string.Join(";", trades.Select(x => string.Join(":",
            x.DecisionOrdinal, x.TurnOrdinal,
            x.SourceEconomicPointId.Value, (byte)x.SourceActivity,
            x.DestinationEconomicPointId.Value, x.CommodityId.Value,
            x.RawQuantity,
            x.FreightQuote.RouteKey.SourceEconomicPointId.Value,
            x.FreightQuote.RouteKey.DestinationEconomicPointId.Value,
            x.FreightQuote.RulesetVersion,
            x.FreightQuote.ExpectedLoadMicrograms,
            x.RawMarginalCommodityUnitValueAtDecision.Numerator,
            x.RawMarginalCommodityUnitValueAtDecision.Denominator,
            x.RawCommodityValue.Numerator, x.RawCommodityValue.Denominator,
            x.RawFreightCharge.Numerator, x.RawFreightCharge.Denominator,
            x.RawProducerReservationCost.Numerator,
            x.RawProducerReservationCost.Denominator,
            x.RawNetReturn.Numerator, x.RawNetReturn.Denominator)));

    [Fact]
    public void IndexedModeMatchesExhaustiveTranscriptAndSkipsUnrelatedReevaluation()
    {
        var referenceDiagnostics = new EconomicTurnFreightMarketDiagnostics();
        var reference = Resolve(diagnostics: referenceDiagnostics);
        var indexedDiagnostics = new EconomicTurnFreightMarketDiagnostics();
        var indexed = Resolve(
            mode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex,
            diagnostics: indexedDiagnostics);

        Assert.Equal(ExactTradeTranscript(reference), ExactTradeTranscript(indexed));
        Assert.Equal(reference.Count, indexedDiagnostics.AcceptedTrades);
        Assert.Equal(referenceDiagnostics.DecisionRounds,
            indexedDiagnostics.DecisionRounds);
        Assert.Equal(referenceDiagnostics.IntegratedValueEvaluations,
            indexedDiagnostics.IntegratedValueEvaluations);
        Assert.True(indexedDiagnostics.LaneExaminations <
            referenceDiagnostics.LaneExaminations);
        Assert.True(indexedDiagnostics.MarginalValueEvaluations <
            referenceDiagnostics.MarginalValueEvaluations);

        var reverseIndexed = Resolve(
            quotes: Quotes(1).Reverse(),
            values: Values().Reverse(),
            reservations: Reservations().Reverse(),
            mode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex);
        Assert.Equal(ExactTradeTranscript(reference),
            ExactTradeTranscript(reverseIndexed));
    }

    [Fact]
    public void IndexedModePreservesEarliestCanonicalLaneAtEqualMargins()
    {
        var flows = new EconomicProductionDemandResult(
            new[]
            {
                new EconomicSupplyFlow(P(1), C(7),
                    EconomicActivityKind.Agriculture, 2 * One),
                new EconomicSupplyFlow(P(2), C(7),
                    EconomicActivityKind.Agriculture, 2 * One)
            },
            new[] { new EconomicDemandFlow(P(3), C(7), 3 * One) });
        var quotes = new[] { Quote(1, 1, 3, 900), Quote(1, 2, 3, 900) };
        var values = new[] { new EconomicTurnCommodityValueOffer(
            P(3), C(7), new LinearCommodityMarketValue(600_000, 50_000)) };
        var reservations = new[]
        {
            new EconomicTurnSourceReservation(P(1), C(7),
                EconomicActivityKind.Agriculture, 10_000),
            new EconomicTurnSourceReservation(P(2), C(7),
                EconomicActivityKind.Agriculture, 10_000)
        };
        IReadOnlyList<EconomicTurnLockedFreightTrade> Run(
            EconomicTurnFreightCandidateSelectionMode mode) =>
            Resolve(flows: flows, quotes: quotes, values: values,
                reservations: reservations, mode: mode);

        var oracle = Run(EconomicTurnFreightCandidateSelectionMode.Exhaustive);
        var indexed = Run(EconomicTurnFreightCandidateSelectionMode.DependencyIndex);
        Assert.Equal(ExactTradeTranscript(oracle), ExactTradeTranscript(indexed));
        Assert.Equal(new[] { P(1), P(1), P(2) },
            indexed.Select(x => x.SourceEconomicPointId));
    }

    [Fact]
    public void IndexedModePreservesProfitBoundaryAndFailsClosedAtBudget()
    {
        // The same reference must stop inside a quantum after the dynamic
        // commodity value crosses a positive unit freight+reservation cost.
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(P(1), C(7),
                EconomicActivityKind.Agriculture, 3 * One) },
            new[] { new EconomicDemandFlow(P(3), C(7), 3 * One) });
        var values = new[] { new EconomicTurnCommodityValueOffer(
            P(3), C(7), new LinearCommodityMarketValue(
                400_000, 120_000)) };
        var quotes = new[] { Quote(1, 1, 3, 900) };
        var reservations = new[] { new EconomicTurnSourceReservation(
            P(1), C(7), EconomicActivityKind.Agriculture, 20_000) };
        var policy = new EconomicCongestionMarketSearchPolicy(2 * One, 20);
        var exhaustiveCounters = new EconomicTurnFreightMarketDiagnostics();
        var indexedCounters = new EconomicTurnFreightMarketDiagnostics();
        var reference = Resolve(
            flows: flows, quotes: quotes, values: values,
            reservations: reservations, policy: policy,
            diagnostics: exhaustiveCounters);
        var indexed = Resolve(
            flows: flows, quotes: quotes, values: values,
            reservations: reservations, policy: policy,
            mode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex,
            diagnostics: indexedCounters);
        Assert.Equal(ExactTradeTranscript(reference), ExactTradeTranscript(indexed));
        Assert.True(exhaustiveCounters.BinarySearchIterations > 0);
        Assert.True(indexedCounters.BinarySearchIterations > 0);

        Assert.Throws<InvalidOperationException>(() =>
            Resolve(mode: EconomicTurnFreightCandidateSelectionMode.DependencyIndex,
                policy: new EconomicCongestionMarketSearchPolicy(One, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Resolve(mode: (EconomicTurnFreightCandidateSelectionMode)255));
    }

    [Fact]
    public void SeededSparseAndDenseMarketsMatchReferenceExactly()
    {
        for (var seed = 1; seed <= 24; seed++)
        {
            var random = new Random(seed);
            var sources = 2 + seed % 4;
            var destinations = 2 + seed % 3;
            var commodities = 1 + seed % 3;
            var supply = new List<EconomicSupplyFlow>();
            var demand = new List<EconomicDemandFlow>();
            var foods = new List<FoodCommodityDefinition>();
            var values = new List<EconomicTurnCommodityValueOffer>();
            var reservations = new List<EconomicTurnSourceReservation>();
            var quotes = new List<EconomicTurnFreightQuote>();
            for (var c = 0; c < commodities; c++)
            {
                var food = C(checked((uint)(7 + c)));
                foods.Add(new FoodCommodityDefinition(food, $"test-{c}",
                    checked((ulong)(1_000 + c * 500))));
                for (var i = 0; i < sources; i++)
                {
                    var activity = i % 2 == 0
                        ? EconomicActivityKind.Agriculture
                        : EconomicActivityKind.Mining;
                    supply.Add(new EconomicSupplyFlow(P(1UL + (ulong)i),
                        food, activity, (ulong)random.Next(1, 5) * One));
                    reservations.Add(new EconomicTurnSourceReservation(
                        P(1UL + (ulong)i), food, activity,
                        (ulong)random.Next(2_000, 80_000)));
                }
                for (var i = 0; i < destinations; i++)
                {
                    var point = P(101UL + (ulong)i);
                    demand.Add(new EconomicDemandFlow(point, food,
                        (ulong)random.Next(1, 5) * One));
                    values.Add(new EconomicTurnCommodityValueOffer(
                        point, food, new LinearCommodityMarketValue(
                            (ulong)random.Next(550_000, 950_000),
                            (ulong)random.Next(40_000, 100_000))));
                }
            }

            for (var src = 0; src < sources; src++)
                for (var dest = 0; dest < destinations; dest++)
                    if (random.Next(0, 5) != 0) // explicit unquoted route
                        quotes.Add(Quote(1, 1UL + (ulong)src,
                            101UL + (ulong)dest,
                            (ulong)random.Next(350, 4_000)));

            var flows = new EconomicProductionDemandResult(supply, demand);
            var catalog = new FoodCommodityCatalog(foods);
            var policy = new EconomicCongestionMarketSearchPolicy(
                seed % 3 == 0 ? One / 3UL : seed % 3 == 1 ? One : 2 * One,
                500);
            var resolver = new EconomicTurnLockedFreightMarketResolver();
            IReadOnlyList<EconomicTurnLockedFreightTrade> Run(
                EconomicTurnFreightCandidateSelectionMode mode) =>
                resolver.Resolve(1UL, flows, catalog, quotes,
                    values, reservations, policy, null, mode);

            Assert.Equal(ExactTradeTranscript(
                Run(EconomicTurnFreightCandidateSelectionMode.Exhaustive)),
                ExactTradeTranscript(
                    Run(EconomicTurnFreightCandidateSelectionMode.DependencyIndex)));
        }
    }

    [Fact]
    public void OptionalDiagnosticsPreserveAllEconomicResultsAndCountOperations()
    {
        var reference = Resolve();
        var diagnostics = new EconomicTurnFreightMarketDiagnostics();
        var measured = new EconomicTurnLockedFreightMarketResolver().Resolve(
            1UL, Flows(), Foods(), Quotes(1), Values(), Reservations(),
            new EconomicCongestionMarketSearchPolicy(One, 30), diagnostics);

        Assert.Equal(reference.Count, measured.Count);
        for (var index = 0; index < reference.Count; index++)
        {
            var expected = reference[index];
            var actual = measured[index];
            Assert.Equal(expected.DecisionOrdinal, actual.DecisionOrdinal);
            Assert.Equal(expected.SourceEconomicPointId, actual.SourceEconomicPointId);
            Assert.Equal(expected.DestinationEconomicPointId, actual.DestinationEconomicPointId);
            Assert.Equal(expected.CommodityId, actual.CommodityId);
            Assert.Equal(expected.RawQuantity, actual.RawQuantity);
            Assert.Equal(0, expected.RawMarginalCommodityUnitValueAtDecision.CompareTo(
                actual.RawMarginalCommodityUnitValueAtDecision));
            Assert.Equal(0, expected.RawCommodityValue.CompareTo(actual.RawCommodityValue));
            Assert.Equal(0, expected.RawFreightCharge.CompareTo(actual.RawFreightCharge));
            Assert.Equal(0, expected.RawProducerReservationCost.CompareTo(
                actual.RawProducerReservationCost));
            Assert.Equal(0, expected.RawNetReturn.CompareTo(actual.RawNetReturn));
        }

        Assert.Equal(3, diagnostics.SupplyCount);
        Assert.Equal(3, diagnostics.DemandCount);
        Assert.Equal(4, diagnostics.QuoteCount);
        Assert.Equal(5, diagnostics.CandidateLaneCount);
        Assert.Equal(measured.Count, diagnostics.AcceptedTrades);
        Assert.True(diagnostics.DecisionRounds >= measured.Count);
        Assert.Equal(diagnostics.DecisionRounds * diagnostics.CandidateLaneCount,
            diagnostics.LaneExaminations);
        Assert.Equal(measured.Count * 2L, diagnostics.IntegratedValueEvaluations);
        Assert.True(diagnostics.MarginalValueEvaluations >=
            2L * diagnostics.QuantitySearchCalls);
    }

    [Fact]
    public void ReusingDiagnosticsStartsFreshAndDoesNotAffectCanonicalWorldHash()
    {
        var state = WorldState.CreateInitial();
        var hash = new CanonicalWorldStateHasher().Compute(state);
        var diagnostics = new EconomicTurnFreightMarketDiagnostics();
        var resolver = new EconomicTurnLockedFreightMarketResolver();

        var first = resolver.Resolve(
            1UL, Flows(), Foods(), Quotes(1), Values(), Reservations(),
            new EconomicCongestionMarketSearchPolicy(One, 30), diagnostics);
        var firstRounds = diagnostics.DecisionRounds;
        var firstSearches = diagnostics.QuantitySearchCalls;

        var second = resolver.Resolve(
            1UL, Flows(), Foods(), Quotes(1), Values(), Reservations(),
            new EconomicCongestionMarketSearchPolicy(One, 30), diagnostics);
        Assert.Equal(first.Count, second.Count);
        Assert.Equal(firstRounds, diagnostics.DecisionRounds);
        Assert.Equal(firstSearches, diagnostics.QuantitySearchCalls);
        Assert.Equal(second.Count, diagnostics.AcceptedTrades);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(state));

        Assert.Throws<ArgumentOutOfRangeException>(() => resolver.Resolve(
            0UL, Flows(), Foods(), Quotes(1), Values(), Reservations(),
            new EconomicCongestionMarketSearchPolicy(One, 30), diagnostics));
        Assert.Equal(0, diagnostics.AcceptedTrades);
        Assert.Equal(0, diagnostics.DecisionRounds);
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
