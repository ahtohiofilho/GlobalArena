using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using GlobalArena.Simulation;

namespace GlobalArena.Benchmarks;

/// <summary>
/// Reproducible SYNTHETIC grid workload for VS41 source-tree reuse.
/// Counts, elapsed times and allocations are observations, not performance
/// acceptance thresholds. No economic freight prices, geographies or assets
/// are fabricated inside the canonical game.
/// </summary>
public static class GaVs41FreightRoutingBenchmarkHarness
{
    private sealed record Scenario(int Side, int Destinations);

    private static readonly Scenario[] Cases =
    {
        new(20, 8),
        new(32, 32),
        new(40, 72)
    };

    public static int Run()
    {
#if DEBUG
        Console.Error.WriteLine("VS41 routing benchmark requires Release.");
        return 3;
#else
        try
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            Console.WriteLine("BENCHMARK=GA-VS41-SYNTHETIC-GRID-FREIGHT-ROUTING");
            Console.WriteLine($"ENV_OS={RuntimeInformation.OSDescription}");
            Console.WriteLine($"ENV_RUNTIME={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"ENV_PROCESSORS={Environment.ProcessorCount}");
            Console.WriteLine("SAMPLES=3;WARMUPS=1;PERFORMANCE_THRESHOLDS=none");
            foreach (var c in Cases)
                RunCase(c);
            Console.WriteLine("RESULT=PASS_GA_VS41_FREIGHT_ROUTING_BENCHMARK");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Console.WriteLine("RESULT=FAIL_GA_VS41_FREIGHT_ROUTING_BENCHMARK");
            return 1;
        }
#endif
    }

    private static void RunCase(Scenario scenario)
    {
        var arcs = BuildGrid(scenario.Side);
        var source = 1UL;
        var destinations = Enumerable.Range(0, scenario.Destinations)
            .Select(i => checked((ulong)(
                2 + ((i * 503 + 37) % (scenario.Side * scenario.Side - 1)))))
            .Distinct().ToArray();
        if (destinations.Length != scenario.Destinations)
            throw new InvalidOperationException("Benchmark destination IDs must be unique.");

        var shared = new double[3];
        var isolated = new double[3];
        var sharedAllocation = new long[3];
        var isolatedAllocation = new long[3];
        var expectedSignature = "";
        long sharedArcs = 0, isolatedArcs = 0;

        for (var iteration = -1; iteration < 3; iteration++)
        {
            var beforeSharedBytes = GC.GetAllocatedBytesForCurrentThread();
            var startShared = Stopwatch.GetTimestamp();
            var sharedDiag = new ExperimentalFreightRouteSearchDiagnostics();
            var sharedTree = ExperimentalFreightWeightedPathfinder.FromSource(
                source, arcs, sharedDiag);
            var sharedSignature = Signature(sharedTree, destinations);
            var elapsedShared = Stopwatch.GetElapsedTime(startShared).TotalMilliseconds;
            var bytesShared = GC.GetAllocatedBytesForCurrentThread() - beforeSharedBytes;

            var isolatedDiag = new ExperimentalFreightRouteSearchDiagnostics();
            var beforeIsolatedBytes = GC.GetAllocatedBytesForCurrentThread();
            var startIsolated = Stopwatch.GetTimestamp();
            var pieces = new Dictionary<ulong, ExperimentalFreightWeightedPath>();
            foreach (var target in destinations)
            {
                var tree = ExperimentalFreightWeightedPathfinder.FromSource(
                    source, arcs, isolatedDiag);
                pieces.Add(target, tree[target]);
            }
            var isolatedSignature = Signature(pieces, destinations);
            var elapsedIsolated = Stopwatch.GetElapsedTime(startIsolated).TotalMilliseconds;
            var bytesIsolated = GC.GetAllocatedBytesForCurrentThread() - beforeIsolatedBytes;

            if (sharedSignature != isolatedSignature ||
                sharedDiag.SourceTreeBuilds != 1 ||
                isolatedDiag.SourceTreeBuilds != destinations.Length ||
                sharedDiag.ExaminedArcs * destinations.Length != isolatedDiag.ExaminedArcs)
                throw new InvalidOperationException(
                    "VS41 path reuse changed results or deterministic operation counts.");

            if (iteration < 0) continue; // JIT and allocation warmup
            if (iteration == 0) expectedSignature = sharedSignature;
            if (sharedSignature != expectedSignature)
                throw new InvalidOperationException(
                    "Repeated VS41 synthetic route results must be deterministic.");

            shared[iteration] = elapsedShared;
            isolated[iteration] = elapsedIsolated;
            sharedAllocation[iteration] = bytesShared;
            isolatedAllocation[iteration] = bytesIsolated;
            sharedArcs = sharedDiag.ExaminedArcs;
            isolatedArcs = isolatedDiag.ExaminedArcs;
        }

        Console.WriteLine(
            $"CASE=grid-{scenario.Side};nodes={scenario.Side * scenario.Side};" +
            $"destinations={destinations.Length};" +
            $"shared_source_trees=1;isolated_source_trees={destinations.Length};" +
            $"shared_examined_arcs={sharedArcs};isolated_examined_arcs={isolatedArcs};" +
            $"shared_ms={Median(shared):F3};isolated_ms={Median(isolated):F3};" +
            $"shared_allocated_bytes={Median(sharedAllocation)};" +
            $"isolated_allocated_bytes={Median(isolatedAllocation)}");
    }

    private static ExperimentalFreightWeightedArc[] BuildGrid(int side)
    {
        var arcs = new List<ExperimentalFreightWeightedArc>();
        ulong asset = 1UL;
        for (var y = 0; y < side; y++)
        for (var x = 0; x < side; x++)
        {
            var node = checked((ulong)(y * side + x + 1));
            if (x + 1 < side)
            {
                var next = node + 1;
                var east = new BigInteger(15 + (x * 13 + y * 7) % 51);
                arcs.Add(new(node, next, asset, east));
                arcs.Add(new(next, node, asset, east + ((x + y) % 3)));
                asset++;
            }
            if (y + 1 < side)
            {
                var next = checked(node + (ulong)side);
                var south = new BigInteger(14 + (x * 17 + y * 11) % 63);
                arcs.Add(new(node, next, asset, south));
                arcs.Add(new(next, node, asset, south + ((x + y) % 5)));
                asset++;
            }
        }
        return arcs.ToArray();
    }

    private static string Signature(
        IReadOnlyDictionary<ulong, ExperimentalFreightWeightedPath> tree,
        IEnumerable<ulong> destinations) =>
        string.Join(";", destinations.Select(d =>
            d + ":" + tree[d].GeographicWeight + ":" +
            string.Join(",", tree[d].AssetIds)));

    private static double Median(double[] values) =>
        values.OrderBy(x => x).ElementAt(values.Length / 2);

    private static long Median(long[] values) =>
        values.OrderBy(x => x).ElementAt(values.Length / 2);
}
