using System;
using System.Linq;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public class TacticalContinentDiversityTests
{
    private static StrategicSphericalGeometry Geometry() =>
        new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(20261014UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(12, 0))).StrategicSphericalGeometry;

    [Fact]
    public void MacroRangeIncludesZeroAndTwentyAndDoesNotSuppressMicro()
    {
        var counts = Enumerable.Range(0, 2000)
            .Select(i => TacticalContinentDiversityGenerator.GetCounts((ulong)i)).ToArray();
        Assert.Contains(counts, x => x.Macro == 0);
        Assert.Contains(counts, x => x.Macro == 20);
        Assert.All(counts, x => { Assert.InRange(x.Macro, 0, 20);
            Assert.Equal(Math.Max(50, x.Macro * 10), x.Medium);
            Assert.Equal(Math.Max(500, x.Macro * 100), x.Micro); });
    }

    [Fact]
    public void BothModesAreBoundedDeterministicAndDifferent()
    {
        var geometry = Geometry();
        var a = TacticalContinentDiversityGenerator.Generate(geometry, 20261014UL);
        var b = TacticalContinentDiversityGenerator.Generate(geometry, 20261014UL);
        var c = TacticalContinentDiversityGenerator.Generate(geometry, 20261014UL, true);
        var d = TacticalContinentDiversityGenerator.Generate(geometry, 20261014UL, true);
        Assert.Equal(1442, a.Count);
        Assert.True(a.SequenceEqual(b));
        Assert.True(c.SequenceEqual(d));
        Assert.False(a.SequenceEqual(c));
        Assert.All(a.Concat(c), x => Assert.InRange(x, -1_000_000L, 1_000_000L));
    }
}
