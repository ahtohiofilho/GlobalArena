using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalViewportAspectFitTests
{
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1080, 1920)]
    [InlineData(1024, 1024)]
    public void AtLeastOneActualRegionFitsWithoutCropping(int width, int height)
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
        var completed = 0;
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
            completed++;
            var aspectFit = TacticalViewportAspectFit.Create(fit, width, height);
            Assert.Equal(plan.PhysicalTileIds.Count, aspectFit.PhysicalTileCount);
            Assert.Equal((double)width / height, aspectFit.AspectRatio, 12);
            Assert.True(aspectFit.MinX <= fit.MinX + 1e-12d);
            Assert.True(aspectFit.MaxX >= fit.MaxX - 1e-12d);
            Assert.True(aspectFit.MinY <= fit.MinY + 1e-12d);
            Assert.True(aspectFit.MaxY >= fit.MaxY - 1e-12d);
            Assert.Equal((double)width / height,
                (aspectFit.MaxX - aspectFit.MinX) / (aspectFit.MaxY - aspectFit.MinY), 10);
            foreach (var tile in plan.PhysicalTileIds)
                foreach (var vertex in fine.StrategicSphericalGeometry
                    .GetCell(tile.FineStrategicCellId).BoundaryVertexIds)
                {
                    var projected = TacticalSurfaceProjection.Project(camera,
                        fine.StrategicSphericalGeometry.GetVertex(vertex).Position);
                    Assert.True(projected.Forward > 1e-10d);
                    Assert.InRange(projected.X, aspectFit.MinX - 1e-12d, aspectFit.MaxX + 1e-12d);
                    Assert.InRange(projected.Y, aspectFit.MinY - 1e-12d, aspectFit.MaxY + 1e-12d);
                }
        }
        Assert.True(completed > 0, "No actual tactical region was successfully framed.");
    }

    [Fact]
    public void RejectsInvalidDimensionsAndNullFit()
    {
        Assert.Throws<ArgumentNullException>(() => TacticalViewportAspectFit.Create(null!, 1920, 1080));
        var p = new GoldbergParameters(2, 0);
        var fineParameters = new GoldbergParameters(12, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(p, fineParameters));
        var generator = new DeterministicWorldGenerator();
        var geometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(7UL), WorldGenerationVersion.Initial, p)).StrategicSphericalGeometry;
        var fine = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(7UL), WorldGenerationVersion.Initial, fineParameters)).StrategicSphericalGeometry;
        TacticalViewportFit? found = null;
        foreach (var cell in map.CoarseTopology.Cells)
        {
            var camera = TacticalCameraFrame.Create(geometry, cell.Id);
            var plan = MeshPresentationPlan.TacticalNeighborhood(map, cell.Id);
            try { found = TacticalViewportFit.Create(camera, plan, fine, fineParameters); break; }
            catch (NotSupportedException) { }
        }
        Assert.NotNull(found);
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalViewportAspectFit.Create(found!, 0, 1080));
        Assert.Throws<ArgumentOutOfRangeException>(() => TacticalViewportAspectFit.Create(found!, 1920, -1));
    }
}