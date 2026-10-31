using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Benchmarks;

/// <summary>
/// A SYNTHETIC finite portfolio of economic lots, offered corridors and
/// expected physical loads. It measures hypothetical coupling and search
/// costs, NOT the current game market/terrain or game-wide equilibrium.
/// Three fixed samples with no timing acceptance thresholds.
/// </summary>
public static class GaVs42SharedCorridorBenchmarkHarness
{
    private static readonly GoldbergParameters Fine = new(18, 0);
    private const int Warmups = 1;
    private const int Samples = 3;

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("VS42 shared freight benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS42-SYNTHETIC-SHARED-CORRIDOR-BATCH");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"ENV_CPUS={Environment.ProcessorCount}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var count in new[] { 8, 28, 64 })
                Measure(count);
            Console.WriteLine("RESULT=PASS_GA_VS42_SHARED_FREIGHT_BENCHMARK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("RESULT=FAIL_GA_VS42_SHARED_FREIGHT_BENCHMARK");
            return 1;
        }
#endif
    }

    private static ExperimentalSharedFreightLot[] BuildLots(int count)
    {
        var calibration = new ExperimentalFreightTechnologyPricing(
            101UL, 7UL, 42UL,
            ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 1_000_000, 1_000_000, 15, 1_000_000);
        var hub = new ExperimentalSharedFreightCorridorKey(
            new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(1UL)),
            101UL, 7UL);
        var lots = new List<ExperimentalSharedFreightLot>();
        for (var i = 0; i < count; i++)
        {
            var route = new StrategicEconomicRouteKey(
                new EconomicPointId((ulong)i + 1UL),
                new EconomicPointId((ulong)i + 1_001UL));
            var spur = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(Fine,
                    new StrategicEdgeId((ulong)(2 + i % 12))), 101UL, 7UL);
            var bypass = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(Fine,
                    new StrategicEdgeId((ulong)(20 + i % 12))), 101UL, 7UL);
            var options = new[]
            {
                ExperimentalSharedFreightOption.Synthetic(
                    1UL, route, calibration, 1_000_000,
                    new[] { hub, spur }),
                ExperimentalSharedFreightOption.Synthetic(
                    2UL, route, calibration, 2_100_000,
                    new[] { bypass, spur })
            };
            lots.Add(new ExperimentalSharedFreightLot(
                (ulong)i + 1UL, 600_000 + (i % 4) * 100_000, options));
        }
        return lots.ToArray();
    }

    private static void Measure(int count)
    {
        var lots = BuildLots(count);
        var policy = new ExperimentalSharedFreightCongestionPolicy(
            15, 1_000_000);
        var lab = new ExperimentalSharedFreightAllocationLaboratory();
        var samples = new double[Samples];
        var allocated = new long[Samples];
        string? signature = null;
        for (var iteration = -Warmups; iteration < Samples; iteration++)
        {
            var beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            var timer = Stopwatch.GetTimestamp();
            var result = lab.Compare(lots, policy,
                maxImprovementPasses: 4,
                maxExactConfigurations: 256);
            var ms = Stopwatch.GetElapsedTime(timer).TotalMilliseconds;
            var used = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            if (result.Coordinated.TotalCost.CompareTo(
                    result.Independent.TotalCost) > 0)
                throw new InvalidOperationException(
                    "Coupled local improvement increased the objective.");
            if (count == 8 && result.Exact is null)
                throw new InvalidOperationException(
                    "Small benchmark must enumerate the full portfolio.");
            if (result.Exact is not null &&
                result.Coordinated.TotalCost.CompareTo(
                    result.Exact.TotalCost) < 0)
                throw new InvalidOperationException(
                    "A local heuristic cannot beat the enumerated reference.");

            var current = string.Join(",", result.Coordinated.ChosenServiceIds) +
                ":" + result.Coordinated.TotalCost.Numerator + "/" +
                result.Coordinated.TotalCost.Denominator +
                ":" + result.EvaluatedConfigurations;
            if (iteration >= 0)
            {
                if (signature is null) signature = current;
                else if (signature != current)
                    throw new InvalidOperationException(
                        "Deterministic batch outcomes must repeat.");
                samples[iteration] = ms;
                allocated[iteration] = used;
            }
            if (iteration == Samples - 1)
            {
                Console.WriteLine(
                    $"CASE=lots-{count};candidates_per_lot=2;" +
                    $"possible_configurations={result.PossibleConfigurations};" +
                    $"evaluated_configurations={result.EvaluatedConfigurations};" +
                    $"local_passes={result.LocalImprovementPasses};" +
                    $"independent_cost={result.Independent.TotalCost.Numerator}/" +
                        $"{result.Independent.TotalCost.Denominator};" +
                    $"coordinated_cost={result.Coordinated.TotalCost.Numerator}/" +
                        $"{result.Coordinated.TotalCost.Denominator};" +
                    $"exact_available={(result.Exact is not null ? 1 : 0)};" +
                    $"median_ms={samples.OrderBy(x => x).ElementAt(1):F3};" +
                    $"median_allocated_bytes={allocated.OrderBy(x => x).ElementAt(1)}");
            }
        }
    }
}
