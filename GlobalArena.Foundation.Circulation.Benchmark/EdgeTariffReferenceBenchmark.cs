using System.Diagnostics;
using GlobalArena.Foundation.Circulation;

internal static class EdgeTariffReferenceBenchmark
{
    public static void Run()
    {
        Console.WriteLine("GA F-084E - local fixed-load tariff reference laboratory");
        Console.WriteLine("edges,exponent,iteration,elapsed_ms,weighted_checksum");

        var calibration = NaturalEdgeReferenceProfiles.CandidateA;

        foreach (int count in new[] { 10_000, 100_000 })
        {
            var natural = new NaturalEdgeEvaluation[count];
            var invested = new FreeFlowInvestedEdge[count];

            // Build geometric and incorporated-capital snapshots outside
            // the timed section; include no market/treasury iteration.
            for (int i = 0; i < count; i++)
            {
                var a = i % 7 == 0 ? NodeMedium.Water : NodeMedium.Land;
                var b = i % 11 == 0 ? NodeMedium.Water : NodeMedium.Land;
                var edge = new NaturalLogicalEdge(a, b,
                    (int)(i * 37_001L % 2_000_001L) - 1_000_000,
                    (int)(i * 97_003L % 2_000_001L) - 1_000_000,
                    a == NodeMedium.Water && b == NodeMedium.Water ? 0
                        : (int)(i * 9_101L % 1_000_001L));

                natural[i] = NaturalEdgeEvaluator.Evaluate(edge, calibration);
                invested[i] = EdgeInvestmentLaboratory.Evaluate(natural[i],
                    incorporatedCapitalRaw: i * 13_781L % 7_000_001L,
                    referenceCapitalRaw: 1_000_000);
            }

            foreach (int exponent in new[] { 3, 4, 5 })
            {
                for (int repetition = 1; repetition <= 3; repetition++)
                {
                    long checksum = 0;
                    var ownership = new long[2];
                    var time = Stopwatch.StartNew();
                    for (int i = 0; i < count; i++)
                    {
                        var tariff = EdgeTariffReferenceLaboratory.Evaluate(
                            natural[i], invested[i], exponent);
                        long sellerCapital = i % 3 == 0 ? invested[i].TotalInvestedCapitalRaw : 0;
                        ownership[0] = sellerCapital;
                        ownership[1] = invested[i].TotalInvestedCapitalRaw - sellerCapital;
                        var external = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
                            tariff, ownership, 0);
                        checksum = checked(checksum
                            + tariff.WeightRaw * 7
                            + tariff.PhysicalGainRaw * 3
                            + external);
                    }
                    time.Stop();

                    Console.WriteLine($"{count},{exponent},{repetition}," +
                        $"{time.Elapsed.TotalMilliseconds:F3},{checksum}");
                }
            }
        }
    }
}
