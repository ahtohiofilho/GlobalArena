using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalRadialElevationGeneratorTests
{
    [Fact]
    public void SameSeedProducesSameFineFieldAndBounds()
    {
        var topology = GoldbergStrategicTopologyGenerator.Generate(new GoldbergParameters(6, 0));
        var geometry = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(new WorldSeed(45891UL), WorldGenerationVersion.Initial, new GoldbergParameters(6, 0))).StrategicSphericalGeometry;
        var first = TacticalRadialElevationGenerator.Generate(geometry, 45891UL);
        var second = TacticalRadialElevationGenerator.Generate(geometry, 45891UL);
        Assert.Equal(geometry.Cells.Count, first.Count);
        Assert.True(first.SequenceEqual(second));
        Assert.All(first, v => Assert.InRange(v, -1_000_000L, 1_000_000L));
        Assert.True(first.Distinct().Count() > 12);
    }

    [Fact]
    public void DistinctSeedsChangeField()
    {
        var geometry = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(new WorldSeed(123UL), WorldGenerationVersion.Initial, new GoldbergParameters(3, 0))).StrategicSphericalGeometry;
        var a = TacticalRadialElevationGenerator.Generate(geometry, 123UL);
        var b = TacticalRadialElevationGenerator.Generate(geometry, 456UL);
        Assert.False(a.SequenceEqual(b));
    }

    [Fact]
    public void SeedControlsComplexityWithinRequestedRange()
    {
        var counts = Enumerable.Range(1, 64)
            .Select(i => TacticalRadialElevationGenerator.GetVariedDeformationCount((ulong)i))
            .ToArray();
        Assert.All(counts, count => Assert.InRange(count, 54, 108));
        Assert.True(counts.Distinct().Count() > 5);
        Assert.Equal(TacticalRadialElevationGenerator.GetVariedDeformationCount(77UL),
            TacticalRadialElevationGenerator.GetVariedDeformationCount(77UL));
    }

    [Fact]
    public void RejectsInvalidConfiguration()
    {
        var geometry = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(new WorldSeed(1UL), WorldGenerationVersion.Initial, new GoldbergParameters(1, 0))).StrategicSphericalGeometry;
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalRadialElevationGenerator.Generate(geometry, 1UL, 0));
    }
}
