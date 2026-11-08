using System.Diagnostics;
using GlobalArena.Foundation.Circulation;

internal static class EdgeInvestmentBenchmark
{
    public static void Run()
    {
        Console.WriteLine("GA F-084C - hypothetical capital/edge improvement laboratory");
        Console.WriteLine("edges,iteration,elapsed_ms,weighted_checksum");

        var naturalCalibration = NaturalEdgeReferenceProfiles.CandidateA;
        foreach (int count in new[] { 10_000, 100_000 })
        {
            var natural = new NaturalEdgeEvaluation[count];
            var incorporated = new long[count];
            for (int i = 0; i < count; i++)
            {
                var mediumA = i % 7 == 0 ? NodeMedium.Water : NodeMedium.Land;
                var mediumB = i % 11 == 0 ? NodeMedium.Water : NodeMedium.Land;
                var edge = new NaturalLogicalEdge(
                    mediumA,
                    mediumB,
                    (int)(i * 37_001L % 2_000_001L) - 1_000_000,
                    (int)(i * 97_003L % 2_000_001L) - 1_000_000,
                    mediumA == NodeMedium.Water && mediumB == NodeMedium.Water
                        ? 0 : (int)(i * 9_101L % 1_000_001L));
                natural[i] = NaturalEdgeEvaluator.Evaluate(edge, naturalCalibration);
                incorporated[i] = i * 13_781L;
            }

            for (int iteration = 1; iteration <= 3; iteration++)
            {
                long checksum = 0;
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    var evaluated = EdgeInvestmentLaboratory.Evaluate(
                        natural[i], incorporated[i], referenceCapitalRaw: 1_000_000);
                    checksum = checked(checksum + evaluated.FreeFlowImpedanceRaw * 3
                        + evaluated.CapacityRateRaw * 7 + evaluated.ImprovementFactorRaw);
                }
                watch.Stop();
                Console.WriteLine($"{count},{iteration},{watch.Elapsed.TotalMilliseconds:F3},{checksum}");
            }
        }
    }
}
