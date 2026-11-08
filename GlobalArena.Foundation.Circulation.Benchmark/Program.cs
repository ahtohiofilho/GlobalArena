using System.Diagnostics;
using GlobalArena.Foundation.Circulation;

Console.WriteLine("GA F-084A - natural unit-edge impedance laboratory");
Console.WriteLine("edges,iteration,elapsed_ms,weighted_checksum");

var calibration = NaturalEdgeReferenceProfiles.CandidateA;
foreach (int count in new[] { 10_000, 100_000 })
{
    var edges = new NaturalLogicalEdge[count];
    for (int i = 0; i < count; i++)
    {
        var a = (i % 7 == 0) ? NodeMedium.Water : NodeMedium.Land;
        var b = (i % 11 == 0) ? NodeMedium.Water : NodeMedium.Land;
        int roughness = (a == NodeMedium.Water && b == NodeMedium.Water)
            ? 0 : (int)(i * 9_101L % 1_000_001);
        edges[i] = new NaturalLogicalEdge(
            a, b,
            (int)(i * 37_001L % 2_000_001) - 1_000_000,
            (int)(i * 97_003L % 2_000_001) - 1_000_000,
            roughness);
    }

    for (int repetition = 1; repetition <= 3; repetition++)
    {
        long checksum = 0;
        var watch = Stopwatch.StartNew();
        foreach (var edge in edges)
        {
            var result = NaturalEdgeEvaluator.Evaluate(edge, calibration);
            checksum = checked(checksum + result.ImpedanceRaw * 7
                + result.NaturalCapacityRateRaw * 3 + (int)result.Medium);
        }
        watch.Stop();
        Console.WriteLine($"{count},{repetition},{watch.Elapsed.TotalMilliseconds:F3},{checksum}");
    }
}

WorkerMovementBenchmark.Run();

EdgeInvestmentBenchmark.Run();

EdgeCongestionBenchmark.Run();

EdgeTariffReferenceBenchmark.Run();

DestinationRevenueBenchmark.Run();
