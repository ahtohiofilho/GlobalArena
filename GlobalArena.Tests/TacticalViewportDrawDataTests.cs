using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalViewportDrawDataTests
{
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1080, 1920)]
    [InlineData(1024, 1024)]
    public void EveryCoarseFocusBuildsRendererNeutralCanonicalPolygons(int width, int height)
    {
        var coarse = new GoldbergParameters(1, 0);
        var fine = new GoldbergParameters(6, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(coarse, fine));
        var generator = new DeterministicWorldGenerator();
        var coarseGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(1UL), WorldGenerationVersion.Initial, coarse)).StrategicSphericalGeometry;
        var fineGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(1UL), WorldGenerationVersion.Initial, fine)).StrategicSphericalGeometry;
        foreach (var cell in map.CoarseTopology.Cells)
        {
            var camera = TacticalCameraFrame.Create(coarseGeometry, cell.Id);
            var plan = MeshPresentationPlan.TacticalNeighborhood(map, cell.Id);
            var fit = TacticalClippedViewportFit.Create(camera, plan, fineGeometry, fine, width, height);
            var draw = TacticalViewportDrawData.Create(camera, plan, fineGeometry, fine, fit);
            Assert.Equal(plan.PhysicalTileIds.Count, draw.SelectedPhysicalTileCount);
            Assert.Equal(fit.VisibleTileCount, draw.Polygons.Count);
            Assert.Equal(draw.Polygons.Count, draw.Polygons.Select(p => p.TileId).Distinct().Count());
            Assert.All(draw.Polygons, polygon =>
            {
                Assert.Contains(polygon.TileId, plan.PhysicalTileIds);
                Assert.True(polygon.Vertices.Count >= 3);
                Assert.All(polygon.Vertices, point =>
                {
                    Assert.InRange(point.X, -1d - 1e-9d, 1d + 1e-9d);
                    Assert.InRange(point.Y, -1d - 1e-9d, 1d + 1e-9d);
                });
            });
            var repeat = TacticalViewportDrawData.Create(camera, plan, fineGeometry, fine, fit);
            Assert.Equal(draw.Polygons.Select(p => p.TileId), repeat.Polygons.Select(p => p.TileId));
            Assert.Equal(draw.Polygons.SelectMany(p => p.Vertices), repeat.Polygons.SelectMany(p => p.Vertices));
            Assert.Throws<NotSupportedException>(() => ((IList<TacticalViewportDrawData.Polygon>)draw.Polygons).Clear());
        }
    }
}
