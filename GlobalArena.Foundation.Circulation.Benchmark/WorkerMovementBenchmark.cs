using System.Diagnostics;
using GlobalArena.Foundation.Circulation;

internal static class WorkerMovementBenchmark
{
    public static void Run()
    {
        Console.WriteLine("GA F-084B - worker time movement laboratory");
        Console.WriteLine("edges,iteration,elapsed_ms,weighted_checksum");

        var calibration = WorkerTravelReferenceProfiles.CandidateB;
        foreach (int count in new[] { 10_000, 100_000 })
        {
            var edges = new NaturalLogicalEdge[count];
            var efficiencies = new long[count];

            for (int i = 0; i < count; i++)
            {
                var a = i % 7 == 0 ? NodeMedium.Water : NodeMedium.Land;
                var b = i % 11 == 0 ? NodeMedium.Water : NodeMedium.Land;
                int roughness = a == NodeMedium.Water && b == NodeMedium.Water
                    ? 0 : (int)(i * 9_101L % 1_000_001L);
                edges[i] = new NaturalLogicalEdge(
                    a, b,
                    (int)(i * 37_001L % 2_000_001L) - 1_000_000,
                    (int)(i * 97_003L % 2_000_001L) - 1_000_000,
                    roughness);
                efficiencies[i] = 300_000 + i * 131_071L % 2_700_001L;
            }

            for (int iteration = 1; iteration <= 3; iteration++)
            {
                long checksum = 0;
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < count; i++)
                {
                    long duration = WorkerEdgeTravel.RequiredTimeRaw(
                        edges[i], efficiencies[i], calibration);
                    var transit = WorkerEdgeTransit.Begin(i, i + 1L, duration);
                    var budget = WorkerActionTimeBudget.Create(1_000_000);
                    var advance = WorkerTravelProgress.Advance(transit, budget, 1_000_000);
                    checksum = checked(checksum
                        + duration * 7
                        + advance.TimeUsedRaw * 3
                        + advance.Transit.ElapsedTimeRaw
                        + (advance.Arrived ? 1 : 0));
                }
                watch.Stop();
                Console.WriteLine($"{count},{iteration},{watch.Elapsed.TotalMilliseconds:F3},{checksum}");
            }
        }
    }
}
