using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Synthetic many-pole, multi-flow VS43 workload. All outcomes are read-only.
/// Absolute timings and bytes are observations, not product SLAs.
/// </summary>
public static class GaVs43SimultaneousFreightBenchmarkHarness
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Fine = new(18, 0);

    private sealed class ConstantValue(ulong rawValue) :
        IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(
            ulong allocated) => new(rawValue, 1);
        public FoodTransportCostFraction EvaluateIntegratedRawValue(
            ulong allocated) => new((BigInteger)rawValue * allocated, Unit);
    }

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("VS43 benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS43-SYNTHETIC-SIMULTANEOUS-FREIGHT");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"ENV_CPUS={Environment.ProcessorCount}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var lanes in new[] { 4, 8, 12 })
                Measure(lanes);
            Console.WriteLine("RESULT=PASS_GA_VS43_SIMULTANEOUS_FREIGHT_BENCHMARK");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Console.WriteLine("RESULT=FAIL_GA_VS43_SIMULTANEOUS_FREIGHT_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void Measure(int count)
    {
        var foodId = new CommodityId(7);
        var commodity = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(foodId, "food", 1)
        });
        var calibration = new ExperimentalFreightTechnologyPricing(
            101, 7, 43, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var shared = new ExperimentalSharedFreightCorridorKey(
            new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(1)), 101, 7);
        var supplies = new List<EconomicSupplyFlow>();
        var demands = new List<EconomicDemandFlow>();
        var values = new List<EconomicTurnCommodityValueOffer>();
        var reservations = new List<EconomicTurnSourceReservation>();
        var candidates = new List<ExperimentalSimultaneousFreightLane>();
        for (var i = 0; i < count; i++)
        {
            var from = new EconomicPointId((ulong)i + 1);
            var to = new EconomicPointId((ulong)(1_001 + i % 4));
            var supply = new EconomicSupplyFlow(
                from, foodId, EconomicActivityKind.Agriculture, Unit);
            supplies.Add(supply);
            reservations.Add(new EconomicTurnSourceReservation(
                from, foodId, supply.Activity, 2));
            if (demands.All(x => x.EconomicPointId != to))
            {
                demands.Add(new EconomicDemandFlow(to, foodId, 4 * Unit));
                values.Add(new EconomicTurnCommodityValueOffer(
                    to, foodId, new ConstantValue(35)));
            }

            var alternate = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(Fine,
                    new StrategicEdgeId((ulong)(2 + i % 7))), 101, 7);
            var route = new StrategicEconomicRouteKey(from, to);
            var services = new[] {
                ExperimentalSharedFreightOption.Synthetic(
                    1, route, calibration, 1_000_000, new[] { shared }),
                ExperimentalSharedFreightOption.Synthetic(
                    2, route, calibration, 2_500_000, new[] { alternate })
            };
            candidates.Add(new ExperimentalSimultaneousFreightLane(
                supply, demands.First(x => x.EconomicPointId == to), services));
        }

        var flows = new EconomicProductionDemandResult(supplies, demands);
        var policy = new ExperimentalSharedFreightCongestionPolicy(10, 1_000_000);
        var reference = new ExperimentalSimultaneousFreightReference();
        var times = new double[3];
        var bytes = new long[3];
        string? signature = null;

        for (var run = -1; run < 3; run++)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var timeBefore = Stopwatch.GetTimestamp();
            var result = reference.Compare(
                flows, commodity, values, reservations, candidates, policy,
                Unit, maxLocalPasses: 4, maxExactConfigurations: 128);
            var elapsed = Stopwatch.GetElapsedTime(timeBefore).TotalMilliseconds;
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            if (result.Coordinated.NetEconomicValue.CompareTo(
                    result.Independent.NetEconomicValue) < 0)
                throw new InvalidOperationException(
                    "Coordinated reference must never reduce its objective.");
            if ((count == 4) != (result.Exact is not null))
                throw new InvalidOperationException(
                    "Bounded exact reference should run only for four lanes.");
            if (result.Exact is not null &&
                result.Coordinated.NetEconomicValue.CompareTo(
                    result.Exact.NetEconomicValue) > 0)
                throw new InvalidOperationException(
                    "Local improvement cannot beat exhaustive enumeration.");
            var current = result.Coordinated.NetEconomicValue.Numerator + "/" +
                result.Coordinated.NetEconomicValue.Denominator + ":" +
                string.Join(",", result.Coordinated.Trades.Select(x =>
                    x.Source.Value + "->" + x.Destination.Value + "@" + x.ServiceId +
                    "=" + x.RawQuantity)) + ":" + result.CandidateEvaluations;
            if (run >= 0)
            {
                if (signature is null) signature = current;
                else if (signature != current)
                    throw new InvalidOperationException(
                        "The coupled economic forecast must be deterministic.");
                times[run] = elapsed;
                bytes[run] = allocated;
            }
            if (run == 2)
                Console.WriteLine(
                    $"CASE=lanes-{count};possible={result.PossibleConfigurations};" +
                    $"evaluations={result.CandidateEvaluations};" +
                    $"independent_net={result.Independent.NetEconomicValue.Numerator}/" +
                    $"{result.Independent.NetEconomicValue.Denominator};" +
                    $"coordinated_net={result.Coordinated.NetEconomicValue.Numerator}/" +
                    $"{result.Coordinated.NetEconomicValue.Denominator};" +
                    $"accepted_trades={result.Coordinated.Trades.Count};" +
                    $"exact_available={(result.Exact is not null ? 1 : 0)};" +
                    $"median_ms={times.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={bytes.OrderBy(x => x).ElementAt(1)}");
        }
    }
}
