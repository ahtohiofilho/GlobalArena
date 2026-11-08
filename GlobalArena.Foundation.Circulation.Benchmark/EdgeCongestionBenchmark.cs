using System.Diagnostics;
using GlobalArena.Foundation.Circulation;

internal static class EdgeCongestionBenchmark
{
    public static void Run()
    {
        Console.WriteLine("GA F-084D - global convex congestion laboratory");
        Console.WriteLine("edges,exponent,iteration,elapsed_ms,weighted_checksum");

        var naturalCalibration = NaturalEdgeReferenceProfiles.CandidateA;
        foreach (int count in new[] { 10_000, 100_000 })
        {
            // Precompute world snapshots outside the timed section.
            var invested = new FreeFlowInvestedEdge[count];
            var loads = new long[count];
            for (int i = 0; i < count; i++)
            {
                var a = i % 7 == 0 ? NodeMedium.Water : NodeMedium.Land;
                var b = i % 11 == 0 ? NodeMedium.Water : NodeMedium.Land;
                var edge = new NaturalLogicalEdge(a, b,
                    (int)(i * 37_001L % 2_000_001L) - 1_000_000,
                    (int)(i * 97_003L % 2_000_001L) - 1_000_000,
                    a == NodeMedium.Water && b == NodeMedium.Water
                        ? 0 : (int)(i * 9_101L % 1_000_001L));
                var natural = NaturalEdgeEvaluator.Evaluate(edge, naturalCalibration);
                invested[i] = EdgeInvestmentLaboratory.Evaluate(natural,
                    incorporatedCapitalRaw: i * 13_781L % 7_000_001L,
                    referenceCapitalRaw: 1_000_000);
                loads[i] = i * 53_271L % 3_500_001L;
            }

            foreach (int globalExponent in new[] { 3, 4, 5 })
            {
                for (int iteration = 1; iteration <= 3; iteration++)
                {
                    long checksum = 0;
                    var timer = Stopwatch.StartNew();
                    for (int i = 0; i < count; i++)
                    {
                        var value = EdgeCongestionLaboratory.Evaluate(
                            invested[i], loads[i], globalExponent);
                        checksum = checked(checksum
                            + value.EffectiveImpedanceRaw * 7
                            + value.CongestionMultiplierRaw * 3
                            + value.OccupancyRaw);
                    }
                    timer.Stop();
                    Console.WriteLine($"{count},{globalExponent},{iteration}," +
                        $"{timer.Elapsed.TotalMilliseconds:F3},{checksum}");
                }
            }
        }
    }
}
