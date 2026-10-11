using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GoldbergCentroidCanonicalIntegrationTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(6, 0)]
    [InlineData(7, 4)]
    public void CanonicalGeometryUsesIncidentCellCentroids(int m, int n)
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(42UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(m, n)));
        var topology = world.StrategicTopology;
        var geometry = world.StrategicSphericalGeometry;
        var candidate = GoldbergCentroidGeometryCandidate.Build(topology, geometry);
        Assert.Equal(geometry.Vertices.Count, candidate.Count);
        for (var i = 0; i < candidate.Count; i++)
        {
            var actual = geometry.Vertices[i].Position;
            Assert.InRange(Math.Abs(actual.X - candidate[i].X), 0, 1e-12);
            Assert.InRange(Math.Abs(actual.Y - candidate[i].Y), 0, 1e-12);
            Assert.InRange(Math.Abs(actual.Z - candidate[i].Z), 0, 1e-12);
        }
    }
}
