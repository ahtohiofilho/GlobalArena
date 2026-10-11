using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalViewportFitSuccessCoverageTests
{
    [Fact]
    public void AtLeastOneClassIFocusProducesRealCompleteBoundedFit()
    {
        var coarseParameters = new GoldbergParameters(2, 0);
        var fineParameters = new GoldbergParameters(12, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(coarseParameters, fineParameters));
        var generator = new DeterministicWorldGenerator();
        var coarse = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(7UL), WorldGenerationVersion.Initial, coarseParameters));
        var fine = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(7UL), WorldGenerationVersion.Initial, fineParameters));
        var validFittings = 0;
        foreach (var cell in map.CoarseTopology.Cells)
        {
            var camera = TacticalCameraFrame.Create(coarse.StrategicSphericalGeometry, cell.Id);
            var plan = MeshPresentationPlan.TacticalNeighborhood(map, cell.Id);
            TacticalViewportFit fit;
            try
            {
                fit = TacticalViewportFit.Create(camera, plan,
                    fine.StrategicSphericalGeometry, fineParameters, 0.10d);
            }
            catch (NotSupportedException)
            {
                continue;
            }
            validFittings++;
            Assert.Equal(plan.PhysicalTileIds.Count, fit.PhysicalTileCount);
            Assert.True(fit.HalfSpan > 0d);
            foreach (var tile in plan.PhysicalTileIds)
            {
                foreach (var id in fine.StrategicSphericalGeometry
                    .GetCell(tile.FineStrategicCellId).BoundaryVertexIds)
                {
                    var projected = TacticalSurfaceProjection.Project(
                        camera, fine.StrategicSphericalGeometry.GetVertex(id).Position);
                    Assert.True(projected.Forward > 1e-10d);
                    Assert.InRange(projected.X, fit.MinX - 1e-12d, fit.MaxX + 1e-12d);
                    Assert.InRange(projected.Y, fit.MinY - 1e-12d, fit.MaxY + 1e-12d);
                }
            }
            var expanded = TacticalViewportFit.Create(camera, plan,
                fine.StrategicSphericalGeometry, fineParameters, 0.20d);
            Assert.True(expanded.HalfSpan > fit.HalfSpan);
        }
        Assert.True(validFittings > 0,
            "All focus regions were rejected by the horizon guard; no successful fit was exercised.");
    }
}