using System.Diagnostics;
using GlobalArena.Foundation.Agriculture;

// Opt-in microbenchmark. Times are non-gating; integer checksums are reproducible.
// No dependency on old worldgen, simulation, or market.
const int repeat = 3;
int[] nodeCounts = { 10_000, 100_000 };
Console.WriteLine("Global Arena - Foundation Agriculture Lab Benchmark");
Console.WriteLine("nodes,crops,repeat,elapsed_ms,weighted_checksum");

foreach (int nodes in nodeCounts)
{
    for (int iteration = 1; iteration <= repeat; iteration++)
    {
        long checksum = 0;
        Span<int> values = stackalloc int[CropCatalog.Count];
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < nodes; i++)
        {
            var node = new AgriculturalNode(
                TemperatureRaw: (int)((i * 37_001L) % 2_000_001L) - 1_000_000,
                MoistureRaw: (int)((i * 97_001L) % 1_000_001L),
                SlopeRaw: (int)((i * 131_071L) % 1_000_001L),
                IsLand: i % 13 != 0);
            AgriculturalSuitability.EvaluateAll(node, values);
            for (int j = 0; j < values.Length; j++)
                checksum = checked(checksum + values[j] * (j + 1L));
        }
        watch.Stop();
        Console.WriteLine($"{nodes},{CropCatalog.Count},{iteration},{watch.Elapsed.TotalMilliseconds:F3},{checksum}");
    }
}
