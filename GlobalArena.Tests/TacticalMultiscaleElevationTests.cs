using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalMultiscaleElevationTests
{
    private static (StrategicSphericalGeometry Geometry, StrategicTopology Topology) MakeFine()
    {
        var parameters = new GoldbergParameters(12, 0);
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(187UL), WorldGenerationVersion.Initial, parameters));
        return (world.StrategicSphericalGeometry, world.StrategicTopology);
    }

    [Fact]
    public void MultiscaleSeedIsDeterministicAndBounded()
    {
        var (geometry, _) = MakeFine();
        var a = TacticalRadialElevationGenerator.GenerateVaried(geometry, 20261013UL);
        var b = TacticalRadialElevationGenerator.GenerateVaried(geometry, 20261013UL);
        Assert.True(a.SequenceEqual(b));
        Assert.Equal(1442, a.Count);
        Assert.All(a, value => Assert.InRange(value, -1_000_000L, 1_000_000L));
        Assert.True(a.Distinct().Count() > 100);
    }

    [Fact]
    public void SeedChangesStrengthAndCenterCount()
    {
        var counts = Enumerable.Range(1, 100).Select(i =>
            TacticalRadialElevationGenerator.GetVariedDeformationCount((ulong)i)).ToArray();
        var strengths = Enumerable.Range(1, 100).Select(i =>
            TacticalRadialElevationGenerator.GetVariedDetailStrength((ulong)i)).ToArray();
        Assert.All(counts, n => Assert.InRange(n, 54, 108));
        Assert.All(strengths, n => Assert.InRange(n, 0.25, 1.60));
        Assert.True(counts.Distinct().Count() > 10);
        Assert.True(strengths.Max() - strengths.Min() > 0.9);
    }

    [Fact]
    public void FineScaleStrengthChangesActualNeighborRoughness()
    {
        var (geometry, topology) = MakeFine();
        var flat = TacticalRadialElevationGenerator.GenerateMultiscale(geometry, 20261013UL, 0.0);
        var rough = TacticalRadialElevationGenerator.GenerateMultiscale(geometry, 20261013UL, 2.0);
        double MeanEdgeDelta(IReadOnlyList<long> values)
        {
            double sum = 0;
            long edges = 0;
            foreach (var cell in topology.Cells)
                foreach (var adjacent in cell.AdjacentCellIds)
                    if (cell.Id.Value < adjacent.Value)
                    {
                        sum += Math.Abs(values[(int)cell.Id.Value - 1] - values[(int)adjacent.Value - 1]);
                        edges++;
                    }
            return sum / edges;
        }
        Assert.True(MeanEdgeDelta(rough) > MeanEdgeDelta(flat) * 1.10,
            $"Expected measurable increased roughness; gentle={MeanEdgeDelta(flat)} rough={MeanEdgeDelta(rough)}");
    }

    [Fact]
    public void RejectsOutOfRangeDetail()
    {
        var (geometry, _) = MakeFine();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TacticalRadialElevationGenerator.GenerateMultiscale(geometry, 7UL, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TacticalRadialElevationGenerator.GenerateMultiscale(geometry, 7UL, 2.1));
    }
}
