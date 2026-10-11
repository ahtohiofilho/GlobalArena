using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalViewportFitTests
{
    private static (TacticalCameraFrame Camera, MeshPresentationPlan Plan,
        StrategicSphericalGeometry Fine, GoldbergParameters Parameters) Create()
    {
        // Intentionally exercise the existing canonical world generator, not OpenGL.
        var coarseParameters = new GoldbergParameters(1, 0);
        var fineParameters = new GoldbergParameters(6, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(coarseParameters, fineParameters));
        var coarse = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(7UL),
                WorldGenerationVersion.Initial, coarseParameters));
        var fine = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(7UL),
                WorldGenerationVersion.Initial, fineParameters));
        var focus = map.CoarseTopology.Cells[0].Id;
        return (TacticalCameraFrame.Create(coarse.StrategicSphericalGeometry, focus),
            MeshPresentationPlan.TacticalNeighborhood(map, focus),
            fine.StrategicSphericalGeometry, fineParameters);
    }

    [Fact]
    public void FitEitherContainsEveryFinePolygonVertexOrRequiresHorizonClipping()
    {
        var (camera, plan, fine, parameters) = Create();
        try
        {
            var fit = TacticalViewportFit.Create(camera, plan, fine, parameters);
            Assert.Equal(plan.PhysicalTileIds.Count, fit.PhysicalTileCount);
            Assert.True(fit.HalfSpan > 0d);
            foreach (var tile in plan.PhysicalTileIds)
                foreach (var id in fine.GetCell(tile.FineStrategicCellId).BoundaryVertexIds)
                {
                    var point = TacticalSurfaceProjection.Project(camera, fine.GetVertex(id).Position);
                    Assert.True(point.Forward > 0d);
                    Assert.InRange(point.X, fit.MinX - 1e-12, fit.MaxX + 1e-12);
                    Assert.InRange(point.Y, fit.MinY - 1e-12, fit.MaxY + 1e-12);
                }
        }
        catch (NotSupportedException ex)
        {
            Assert.Contains("horizon", ex.Message);
        }
    }

    [Fact]
    public void RejectsNonTacticalPlanAndInvalidMargin()
    {
        var (camera, plan, fine, parameters) = Create();
        var map = PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(new GoldbergParameters(1, 0), parameters));
        var overview = MeshPresentationPlan.StrategicOverview(
            map.CoarseTopology);
        Assert.Throws<ArgumentException>(() => TacticalViewportFit.Create(camera, overview, fine, parameters));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalViewportFit.Create(camera, plan, fine, parameters, -0.1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalViewportFit.Create(camera, plan, fine, parameters, 1d));
    }

    [Fact]
    public void RejectsIncorrectFineMeshIdentity()
    {
        var (camera, plan, fine, _) = Create();
        Assert.Throws<ArgumentException>(() => TacticalViewportFit.Create(
            camera, plan, fine, new GoldbergParameters(2, 0)));
    }
}