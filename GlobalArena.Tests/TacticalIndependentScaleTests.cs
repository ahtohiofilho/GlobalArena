using System;
using System.Linq;
using GlobalArena.World;
using Xunit;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

public sealed class TacticalIndependentScaleTests
{
    private readonly ITestOutputHelper _output;
    public TacticalIndependentScaleTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void CountsAndIntensityVaryIndependentlyAcrossSeeds()
    {
        var sample = Enumerable.Range(0, 256)
            .Select(i => TacticalIndependentScaleGenerator.GetCounts((ulong)i)).ToArray();
        Assert.All(sample, c => { Assert.InRange(c.Macro, 0, 10); Assert.InRange(c.Medium, 20, 200); Assert.InRange(c.Micro, 400, 4000); });
        Assert.Contains(sample, c => c.Macro == 0);
        Assert.Contains(sample, c => c.Macro == 10);
        Assert.True(sample.Select(c => c.Medium).Distinct().Count() > 75);
        Assert.True(sample.Select(c => c.Micro).Distinct().Count() > 200);
        Assert.True(sample.Where(c => c.Macro == 0).Select(c => c.Medium).Distinct().Count() > 2);
        var scales = TacticalIndependentScaleGenerator.GetIntensity(20261014UL);
        Assert.InRange(scales.Macro, 0.7, 1.3);
        Assert.InRange(scales.Medium, 0.7, 1.3);
        Assert.InRange(scales.Micro, 0.7, 1.3);
    }

    [Fact]
    public void DeterministicAndBounded()
    {
        var geometry = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(20261014UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(6, 0))).StrategicSphericalGeometry;
        var a = TacticalIndependentScaleGenerator.Generate(geometry, 20261019UL);
        var b = TacticalIndependentScaleGenerator.Generate(geometry, 20261019UL);
        var c = TacticalIndependentScaleGenerator.Generate(geometry, 20261022UL);
        Assert.Equal(362, a.Count);
        Assert.True(a.SequenceEqual(b));
        Assert.False(a.SequenceEqual(c));
        Assert.All(a.Concat(c), v => Assert.InRange(v, -1_000_000L, 1_000_000L));
    }

    [Fact]
    public void ComparativeBenchmarkReportsDominanceAndFragmentation()
    {
        var geometry = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(20261014UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(6, 0))).StrategicSphericalGeometry;
        var topology = GoldbergStrategicTopologyGenerator.Generate(new GoldbergParameters(6, 0));
        var samples = new [] { 20261013UL,20261014UL,20261015UL,20261016UL,
            20261017UL,20261018UL,20261019UL,20261020UL,20261021UL,
            20261022UL,20261023UL,20261024UL };
        foreach(var seed in samples)
        {
            var old = TacticalContinentDiversityGenerator.Generate(geometry, seed);
            var candidate = TacticalIndependentScaleGenerator.Generate(geometry, seed);
            var a = Measure(old, topology);
            var b = Measure(candidate, topology);
            var counts = TacticalIndependentScaleGenerator.GetCounts(seed);
            _output.WriteLine($"SEED={seed} MODEL=PREVIOUS LARGEST_LAND_SHARE={a.Share:F4} COMPONENTS={a.Components}");
            _output.WriteLine($"SEED={seed} MODEL=INDEPENDENT MACRO={counts.Macro} MEDIUM={counts.Medium} MICRO={counts.Micro} LARGEST_LAND_SHARE={b.Share:F4} COMPONENTS={b.Components}");
            Assert.InRange(a.Share, 0.0, 1.0);
            Assert.InRange(b.Share, 0.0, 1.0);
        }
    }

    private static (double Share, int Components) Measure(System.Collections.Generic.IReadOnlyList<long> heights, StrategicTopology topology)
    {
        // Common target quantile; ordinal cell count, NOT geographic area weighted.
        var threshold = heights.OrderBy(x => x).ElementAt((int)Math.Floor(heights.Count * 0.65));
        var land = heights.Select(x => x > threshold).ToArray();
        var visited = new bool[heights.Count];
        int total = land.Count(x => x), largest = 0, components = 0;
        for(int i = 0; i < land.Length; i++)
        {
            if(!land[i] || visited[i]) continue;
            components++;
            int size = 0;
            var queue = new System.Collections.Generic.Queue<int>();
            queue.Enqueue(i); visited[i] = true;
            while(queue.Count > 0)
            {
                int node = queue.Dequeue(); size++;
                foreach(var adjacent in topology.Cells[node].AdjacentCellIds)
                {
                    int j = checked((int)adjacent.Value - 1);
                    if(land[j] && !visited[j]) { visited[j] = true; queue.Enqueue(j); }
                }
            }
            largest = Math.Max(largest,size);
        }
        return (total == 0 ? 0 : (double)largest / total, components);
    }
}
