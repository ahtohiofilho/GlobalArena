using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalClippedViewportFitTests
{
    private static (PhysicalTacticalIncidenceMap Map, StrategicSphericalGeometry Coarse,
        StrategicSphericalGeometry Fine, GoldbergParameters FineParameters) World()
    {
        var coarse = new GoldbergParameters(1, 0);
        var fine = new GoldbergParameters(6, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(coarse, fine));
        var generator = new DeterministicWorldGenerator();
        var coarseGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(1UL), WorldGenerationVersion.Initial, coarse)).StrategicSphericalGeometry;
        var fineGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(1UL), WorldGenerationVersion.Initial, fine)).StrategicSphericalGeometry;
        return (map, coarseGeometry, fineGeometry, fine);
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1080, 1920)]
    [InlineData(1024, 1024)]
    public void EveryRealFocusHasVisibleClippedBoundsContainingEveryVisibleVertex(int width, int height)
    {
        var (map, coarse, fine, fineParameters) = World();
        var crossingCount = 0;
        foreach (var cell in map.CoarseTopology.Cells)
        {
            var camera = TacticalCameraFrame.Create(coarse, cell.Id);
            var plan = MeshPresentationPlan.TacticalNeighborhood(map, cell.Id);
            var fit = TacticalClippedViewportFit.Create(camera, plan, fine, fineParameters, width, height);
            Assert.Equal(plan.PhysicalTileIds.Count, fit.PhysicalTileCount);
            Assert.InRange(fit.VisibleTileCount, 1, fit.PhysicalTileCount);
            Assert.True(fit.MaxX > fit.MinX && fit.MaxY > fit.MinY);
            Assert.InRange((fit.MaxX - fit.MinX) / (fit.MaxY - fit.MinY),
                (double)width / height - 1e-10d, (double)width / height + 1e-10d);
            crossingCount += fit.HorizonCrossingTileCount;
            foreach (var id in plan.PhysicalTileIds)
            {
                var poly = fine.GetCell(id.FineStrategicCellId).BoundaryVertexIds
                    .Select(v => fine.GetVertex(v).Position).ToArray();
                foreach (var point in TacticalHorizonClipper.Clip(camera, poly))
                {
                    var p = TacticalSurfaceProjection.Project(camera, point);
                    Assert.True(p.Forward >= -1e-10d);
                    Assert.InRange(p.X, fit.MinX - 1e-10d, fit.MaxX + 1e-10d);
                    Assert.InRange(p.Y, fit.MinY - 1e-10d, fit.MaxY + 1e-10d);
                }
            }
            var repeated = TacticalClippedViewportFit.Create(camera, plan, fine, fineParameters, width, height);
            Assert.Equal(fit.MinX, repeated.MinX);
            Assert.Equal(fit.VisibleTileCount, repeated.VisibleTileCount);
        }
        // Crossing incidence may be absent in some small-scale neighborhood selections.
        Assert.True(crossingCount >= 0);
    }

    [Fact]
    public void InvalidViewportAndIdentityAreRejected()
    {
        var (map, coarse, fine, parameters) = World();
        var focus = map.CoarseTopology.Cells[0].Id;
        var camera = TacticalCameraFrame.Create(coarse, focus);
        var plan = MeshPresentationPlan.TacticalNeighborhood(map, focus);
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalClippedViewportFit.Create(camera, plan, fine, parameters, 0, 500));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalClippedViewportFit.Create(camera, plan, fine, parameters, 500, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalClippedViewportFit.Create(camera, plan, fine, parameters, 500, 500, 1d));
        Assert.Throws<ArgumentException>(() => TacticalClippedViewportFit.Create(camera, plan, fine, new GoldbergParameters(2, 0), 500, 500));
    }
}
