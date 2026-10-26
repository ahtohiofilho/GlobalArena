using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Reproducible synthetic reference workload for the *already defined*
/// turn-locked freight economics. It does not calibrate gameplay constants,
/// claim performance targets, or change the solver's objective.
/// Runtime and allocation measurements are observational, never acceptance
/// thresholds. Compare only repeated samples on comparable environments.
/// </summary>
public static class TurnLockedFreightMarketBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const int Warmups = 1;
    private const int Samples = 5;

    private sealed record Scenario(
        string Id, int Producers, int Destinations, int Commodities);

    private static readonly Scenario[] Cases =
    {
        new("reference-profit-boundary", 1, 1, 1),
        new("reference-small", 2, 2, 2),
        new("reference-medium", 4, 4, 3),
        new("reference-wide", 8, 8, 4)
    };

    private sealed class SyntheticFreightCostFunction : IEconomicTurnFreightCostFunction
    {
        public ulong RulesetVersion => 1UL;
        public int Evaluations { get; private set; }

        public FoodTransportCostFraction EvaluateRawUnitCostPerGram(
            BigInteger expectedLoadMicrograms)
        {
            Evaluations++;
            var denominator = expectedLoadMicrograms + 1_000_000_000;
            // Test-only smooth utilization function, not a gameplay tariff.
            return new FoodTransportCostFraction(
                20 * denominator + (BigInteger)120_000 * 1_000_000, denominator);
        }
    }

    private sealed class SyntheticCommodityCurve(
        int commodityIndex, int destinationIndex,
        ulong? initialOverride = null, ulong? slopeOverride = null)
        : IEconomicTurnCommodityValueCurve
    {
        private readonly ulong _initial = initialOverride ?? checked((ulong)(
            900_000 + commodityIndex * 2_000 + destinationIndex * 500));
        private readonly ulong _slope = slopeOverride ?? checked((ulong)(
            2_000 + destinationIndex * 50));

        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong alreadyAllocatedRawQuantity)
        {
            var numerator = (BigInteger)_initial * Unit -
                (BigInteger)_slope * alreadyAllocatedRawQuantity;
            if (numerator.Sign < 0)
                throw new InvalidOperationException(
                    "Synthetic benchmark commodity domain exhausted.");
            return new FoodTransportCostFraction(numerator, Unit);
        }

        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong allocatedRawQuantity)
        {
            var x = (BigInteger)allocatedRawQuantity;
            var numerator = 2 * (BigInteger)_initial * x * Unit -
                (BigInteger)_slope * x * x;
            if (numerator.Sign < 0)
                throw new InvalidOperationException(
                    "Synthetic integrated value outside tested domain.");
            return new FoodTransportCostFraction(
                numerator, 2 * (BigInteger)Unit * Unit);
        }
    }

    private sealed record Inputs(
        EconomicProductionDemandResult Flows,
        FoodCommodityCatalog Catalog,
        EconomicTurnFreightQuote[] Quotes,
        EconomicTurnCommodityValueOffer[] Values,
        EconomicTurnSourceReservation[] Reservations,
        EconomicCongestionMarketSearchPolicy Policy,
        int QuoteFunctionEvaluations);

    private sealed record Measurement(
        double ElapsedMilliseconds, long AllocatedBytes,
        int Decisions, string Signature,
        EconomicTurnFreightMarketDiagnostics? Diagnostics);

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("Economic allocation benchmark requires Release.");
        Console.WriteLine("RESULT=FAIL_TURN_FREIGHT_MARKET_CONFIGURATION");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"ENV_ARCH={RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"ENV_PROCESSORS={Environment.ProcessorCount}");
            Console.WriteLine($"WARMUPS={Warmups}");
            Console.WriteLine($"MEASURED_SAMPLES={Samples}");
            Console.WriteLine("BENCHMARK_SCOPE=synthetic-unoptimized-reference");
            Console.WriteLine("PERFORMANCE_THRESHOLDS=none");
            foreach (var scenario in Cases)
                MeasureScenario(scenario);
            Console.WriteLine("RESULT=PASS_TURN_FREIGHT_MARKET_REFERENCE_BENCHMARK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("RESULT=FAIL_TURN_FREIGHT_MARKET_REFERENCE_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void MeasureScenario(Scenario scenario)
    {
        var input = BuildInputs(scenario);
        var resolver = new EconomicTurnLockedFreightMarketResolver();
        var warm = resolver.Resolve(
            1UL, input.Flows, input.Catalog, input.Quotes,
            input.Values, input.Reservations, input.Policy);
        var expected = Signature(warm);
        var baselineMeasurements = new Measurement[Samples];
        var countedMeasurements = new Measurement[Samples];

        for (var i = 0; i < Warmups; i++)
        {
            AssertEquivalent(expected, resolver.Resolve(
                1UL, input.Flows, input.Catalog, input.Quotes,
                input.Values, input.Reservations, input.Policy));
            var warmDiagnostics = new EconomicTurnFreightMarketDiagnostics();
            AssertEquivalent(expected, resolver.Resolve(
                1UL, input.Flows, input.Catalog, input.Quotes,
                input.Values, input.Reservations, input.Policy, warmDiagnostics));
        }

        for (var i = 0; i < Samples; i++)
        {
            // Measurements intentionally separate baseline and counted mode;
            // the underlying input and all immutable quotes are identical.
            baselineMeasurements[i] = Measure(resolver, input, null);
            var counters = new EconomicTurnFreightMarketDiagnostics();
            countedMeasurements[i] = Measure(resolver, input, counters);
            if (baselineMeasurements[i].Signature != expected ||
                countedMeasurements[i].Signature != expected)
                throw new InvalidOperationException(
                    $"{scenario.Id}: diagnostics changed an economic transcript.");
        }

        var counted = countedMeasurements[0].Diagnostics!;
        if (counted.QuoteCount != input.Quotes.Length ||
            counted.AcceptedTrades != warm.Count ||
            counted.CandidateLaneCount <= 0 ||
            counted.MarginalValueEvaluations <= 0 ||
            counted.IntegratedValueEvaluations != 2L * warm.Count ||
            counted.DecisionRounds < warm.Count ||
            counted.LaneExaminations !=
                counted.DecisionRounds * counted.CandidateLaneCount ||
            (scenario.Id == "reference-profit-boundary" &&
             counted.BinarySearchIterations == 0))
            throw new InvalidOperationException(
                $"{scenario.Id}: inconsistent reference diagnostic counters.");

        foreach (var sample in countedMeasurements)
        {
            var d = sample.Diagnostics!;
            if (d.CandidateLaneCount != counted.CandidateLaneCount ||
                d.LaneExaminations != counted.LaneExaminations ||
                d.MarginalValueEvaluations != counted.MarginalValueEvaluations ||
                d.BinarySearchIterations != counted.BinarySearchIterations)
                throw new InvalidOperationException(
                    $"{scenario.Id}: operation counts changed between repeats.");
        }

        Console.WriteLine($"CASE={scenario.Id};producers={scenario.Producers};" +
            $"destinations={scenario.Destinations};commodities={scenario.Commodities};" +
            $"quotes={input.Quotes.Length};quote_function_evaluations={input.QuoteFunctionEvaluations};" +
            $"lanes={counted.CandidateLaneCount};trades={warm.Count};" +
            $"decision_rounds={counted.DecisionRounds};lane_examinations={counted.LaneExaminations};" +
            $"marginal_evaluations={counted.MarginalValueEvaluations};" +
            $"integrated_evaluations={counted.IntegratedValueEvaluations};" +
            $"quantity_search_calls={counted.QuantitySearchCalls};" +
            $"binary_search_iterations={counted.BinarySearchIterations};" +
            $"signature={expected}");

        WriteMeasurements(scenario.Id, "baseline", baselineMeasurements);
        WriteMeasurements(scenario.Id, "diagnostics", countedMeasurements);
    }

    private static Measurement Measure(
        EconomicTurnLockedFreightMarketResolver resolver,
        Inputs input,
        EconomicTurnFreightMarketDiagnostics? diagnostics)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var start = Stopwatch.GetTimestamp();
        var trades = resolver.Resolve(
            1UL, input.Flows, input.Catalog, input.Quotes, input.Values,
            input.Reservations, input.Policy, diagnostics);
        var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return new Measurement(
            elapsed, allocated, trades.Count, Signature(trades), diagnostics);
    }

    private static void WriteMeasurements(
        string id, string mode, IReadOnlyList<Measurement> measurements)
    {
        var elapsed = measurements.Select(x => x.ElapsedMilliseconds).Order().ToArray();
        var allocations = measurements.Select(x => x.AllocatedBytes).Order().ToArray();
        Console.WriteLine(
            $"METRICS={id};mode={mode};" +
            $"median_elapsed_ms={elapsed[elapsed.Length / 2]:F3};" +
            $"max_elapsed_ms={elapsed[^1]:F3};" +
            $"median_allocated_bytes={allocations[allocations.Length / 2]};" +
            $"max_allocated_bytes={allocations[^1]};" +
            $"samples={measurements.Count};status=OBSERVATIONAL");
    }

    private static void AssertEquivalent(
        string signature, IReadOnlyList<EconomicTurnLockedFreightTrade> trades)
    {
        if (Signature(trades) != signature)
            throw new InvalidOperationException("Reference economic transcript changed.");
    }

    private static string Signature(
        IReadOnlyList<EconomicTurnLockedFreightTrade> trades)
    {
        var sb = new StringBuilder();
        foreach (var x in trades)
        {
            sb.Append(x.DecisionOrdinal).Append(':')
                .Append(x.SourceEconomicPointId.Value).Append(':')
                .Append((byte)x.SourceActivity).Append(':')
                .Append(x.DestinationEconomicPointId.Value).Append(':')
                .Append(x.CommodityId.Value).Append(':')
                .Append(x.RawQuantity).Append(':')
                .Append(x.RawMarginalCommodityUnitValueAtDecision.Numerator).Append('/')
                .Append(x.RawMarginalCommodityUnitValueAtDecision.Denominator).Append(':')
                .Append(x.RawCommodityValue.Numerator).Append('/')
                .Append(x.RawCommodityValue.Denominator).Append(':')
                .Append(x.RawFreightCharge.Numerator).Append('/')
                .Append(x.RawFreightCharge.Denominator).Append(':')
                .Append(x.RawProducerReservationCost.Numerator).Append('/')
                .Append(x.RawProducerReservationCost.Denominator).Append(':')
                .Append(x.RawNetReturn.Numerator).Append('/')
                .Append(x.RawNetReturn.Denominator).Append(';');
        }
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static Inputs BuildInputs(Scenario scenario)
    {
        var foods = new List<FoodCommodityDefinition>();
        var supplies = new List<EconomicSupplyFlow>();
        var demands = new List<EconomicDemandFlow>();
        var valuations = new List<EconomicTurnCommodityValueOffer>();
        var reservations = new List<EconomicTurnSourceReservation>();
        var quotes = new List<EconomicTurnFreightQuote>();
        var transportFunction = new SyntheticFreightCostFunction();

        for (var c = 0; c < scenario.Commodities; c++)
        {
            var commodity = new CommodityId(checked((uint)(7 + c)));
            foods.Add(new FoodCommodityDefinition(
                commodity, $"synthetic-{c}", checked((ulong)(1_000 + c * 500))));

            for (var s = 0; s < scenario.Producers; s++)
            {
                var source = new EconomicPointId(checked((ulong)(1 + s)));
                var activity = s % 2 == 0
                    ? EconomicActivityKind.Agriculture
                    : EconomicActivityKind.Mining;
                supplies.Add(new EconomicSupplyFlow(
                    source, commodity, activity, 3UL * Unit));
                reservations.Add(new EconomicTurnSourceReservation(
                    source, commodity, activity,
                    checked((ulong)(20_000 + (s % 7) * 1_000))));
            }

            for (var d = 0; d < scenario.Destinations; d++)
            {
                var destination = new EconomicPointId(checked((ulong)(1_001 + d)));
                demands.Add(new EconomicDemandFlow(
                    destination, commodity, 4UL * Unit));
                var boundary = scenario.Id == "reference-profit-boundary";
                valuations.Add(new EconomicTurnCommodityValueOffer(
                    destination, commodity, new SyntheticCommodityCurve(
                        c, d,
                        boundary ? 800_000UL : (ulong?)null,
                        boundary ? 240_000UL : (ulong?)null)));
            }
        }

        for (var s = 0; s < scenario.Producers; s++)
            for (var d = 0; d < scenario.Destinations; d++)
            {
                var route = new StrategicEconomicRouteKey(
                    new EconomicPointId(checked((ulong)(1 + s))),
                    new EconomicPointId(checked((ulong)(1_001 + d))));
                // Synthetic expectation, determined from indices before the
                // turn; unrelated to quantities later allocated this turn.
                var forecast = (BigInteger)(900 + s * 200 + d * 100) *
                    1_000_000;
                quotes.Add(EconomicTurnFreightQuote.FromForecast(
                    1UL, route, forecast, transportFunction));
            }

        return new Inputs(
            new EconomicProductionDemandResult(supplies, demands),
            new FoodCommodityCatalog(foods), quotes.ToArray(),
            valuations.ToArray(), reservations.ToArray(),
            new EconomicCongestionMarketSearchPolicy(
                Unit, checked(scenario.Producers *
                    scenario.Commodities * 3 * 2 + 100)),
            transportFunction.Evaluations);
    }
}
