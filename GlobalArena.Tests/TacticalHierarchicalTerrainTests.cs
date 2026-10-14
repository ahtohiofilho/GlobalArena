using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public class TacticalHierarchicalTerrainTests
{
    private static StrategicSphericalGeometry MakeGeometry() =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261014UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(12, 0)))
            .StrategicSphericalGeometry;

    [Theory]
    [InlineData(1UL)]
    [InlineData(20261014UL)]
    [InlineData(987654321UL)]
    public void HierarchicalCountsUseThreeDenseBands(ulong seed)
    {
        var counts = TacticalRadialElevationGenerator.GetHierarchicalImpulseCounts(seed);
        Assert.InRange(counts.Continental, 5, 20);
        Assert.Equal(counts.Continental * 10, counts.Regional);
        Assert.Equal(counts.Continental * 100, counts.Local);
    }


    [Fact]
    public void CountsVaryAcrossSeedsAndKeepExactRatio()
    {
        var macros = Enumerable.Range(0, 128)
            .Select(i => TacticalRadialElevationGenerator.GetHierarchicalImpulseCounts((ulong)i).Continental)
            .Distinct().ToArray();
        Assert.True(macros.Length >= 4);
        Assert.Contains(macros, x => x <= 8);
        Assert.Contains(macros, x => x >= 17);
    }

    [Fact]
    public void HierarchicalTerrainIsDeterministicAndBounded()
    {
        var geometry = MakeGeometry();
        var first = TacticalRadialElevationGenerator.GenerateVaried(geometry, 20261014UL);
        var second = TacticalRadialElevationGenerator.GenerateVaried(geometry, 20261014UL);
        Assert.True(first.SequenceEqual(second));
        Assert.Equal(1442, first.Count);
        Assert.All(first, value => Assert.InRange(value, -1000000L, 1000000L));
        Assert.True(first.Distinct().Count() > 100);
    }

    [Fact]
    public void HigherDetailStrengthChangesTheField()
    {
        var geometry = MakeGeometry();
        var gentle = TacticalRadialElevationGenerator.GenerateMultiscale(geometry, 20261014UL, 0.15);
        var detailed = TacticalRadialElevationGenerator.GenerateMultiscale(geometry, 20261014UL, 1.75);
        Assert.False(gentle.SequenceEqual(detailed));
    }

    [Fact]
    public void RejectsInvalidDetailStrength()
    {
        var geometry = MakeGeometry();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TacticalRadialElevationGenerator.GenerateMultiscale(geometry, 1UL, double.NaN));
    }
}
