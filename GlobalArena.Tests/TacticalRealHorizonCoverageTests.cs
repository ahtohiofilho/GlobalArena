using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalRealHorizonCoverageTests
{
    [Fact]
    public void CanonicalFineGoldbergPolygonsReallyCrossTheCameraHorizon()
    {
        var coarseParameters = new GoldbergParameters(1, 0);
        var fineParameters = new GoldbergParameters(6, 0);
        var generator = new DeterministicWorldGenerator();
        var coarseGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(1UL), WorldGenerationVersion.Initial, coarseParameters))
            .StrategicSphericalGeometry;
        var fineGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(1UL), WorldGenerationVersion.Initial, fineParameters))
            .StrategicSphericalGeometry;
        var camera = TacticalCameraFrame.Create(coarseGeometry, new StrategicCellId(1));
        var crossingCount = 0;
        foreach (var cell in fineGeometry.Cells)
        {
            var polygon = cell.BoundaryVertexIds
                .Select(id => fineGeometry.GetVertex(id).Position).ToArray();
            var forward = polygon
                .Select(point => TacticalSurfaceProjection.Project(camera, point).Forward).ToArray();
            if (!forward.Any(value => value < -1e-10d) ||
                !forward.Any(value => value > 1e-10d)) continue;

            crossingCount++;
            var clipped = TacticalHorizonClipper.Clip(camera, polygon);
            Assert.True(clipped.Count >= 3, "A real horizon-crossing polygon was discarded.");
            Assert.All(clipped, point =>
            {
                var projected = TacticalSurfaceProjection.Project(camera, point);
                Assert.True(projected.Forward >= -1e-10d);
                Assert.True(double.IsFinite(projected.X) && double.IsFinite(projected.Y));
            });
            Assert.Equal(clipped.ToArray(), TacticalHorizonClipper.Clip(camera, polygon).ToArray());
        }
        Assert.True(crossingCount > 0,
            "No real fine Goldberg polygon crossed the horizon; real-crossing coverage was not demonstrated.");
    }
}
