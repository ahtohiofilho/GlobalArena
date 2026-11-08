using System.Diagnostics;
using GlobalArena.Foundation.Agriculture;

internal static class GeographyProjectionBenchmark
{
    public static void Run()
    {
        Console.WriteLine("geographic_nodes,logical_edges,crops,iteration,elapsed_ms,weighted_checksum");

        foreach (int count in new[] { 10_000, 100_000 })
        {
            // Six neighbors (three in either direction) per node, all logical unit
            // edges. This deterministic ring is only a performance fixture.
            var nodes = new AgriculturalGeographicNode[count];
            var offsets = new int[count + 1];
            var adjacency = new int[checked(count * 6)];
            for (int i = 0; i < count; i++)
            {
                nodes[i] = new AgriculturalGeographicNode(
                    StableNodeId: 1_000_000 + i,
                    ElevationRaw: (int)((i * 13_003L) % 2_000_001L) - 1_000_000,
                    TemperatureRaw: (int)((i * 37_001L) % 2_000_001L) - 1_000_000,
                    MoistureRaw: (int)((i * 97_001L) % 1_000_001L),
                    IsLand: i % 13 != 0);
                offsets[i] = i * 6;
                int[] adjacent = {
                    (i + count - 1) % count, (i + count - 2) % count,
                    (i + count - 3) % count, (i + 1) % count,
                    (i + 2) % count, (i + 3) % count
                };
                Array.Sort(adjacent);
                adjacent.CopyTo(adjacency, offsets[i]);
            }
            offsets[count] = adjacency.Length;
            var map = new AgriculturalLogicalGeography(nodes, offsets, adjacency);

            for (int iteration = 1; iteration <= 3; iteration++)
            {
                var timer = Stopwatch.StartNew();
                var projection = AgriculturalGeographyProjection.Build(map);
                timer.Stop();
                long checksum = 0;
                for (int i = 0; i < projection.NodeCount; i++)
                    for (int j = 0; j < CropCatalog.Count; j++)
                        checksum = checked(checksum +
                            projection.GetSuitability(i, CropCatalog.All[j].Id) * (j + 1L));
                Console.WriteLine($"{count},{adjacency.Length / 2},{CropCatalog.Count},{iteration},{timer.Elapsed.TotalMilliseconds:F3},{checksum}");
            }
        }
    }
}
