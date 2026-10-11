using System.Xml.Linq;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalSvgRendererTests
{
    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(1080, 1920)]
    [InlineData(1024, 1024)]
    public void RenderProducesParseableImageWithCanonicalTileIds(int width, int height)
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
            var svg = TacticalSvgRenderer.Render(draw, width, height);
            var doc = XDocument.Parse(svg);
            XNamespace ns = "http://www.w3.org/2000/svg";
            Assert.Equal("svg", doc.Root!.Name.LocalName);
            var elements = doc.Descendants(ns + "polygon").ToArray();
            Assert.Equal(draw.Polygons.Count, elements.Length);
            Assert.Equal(draw.Polygons.Select(p => p.TileId.FineStrategicCellId.Value.ToString()),
                elements.Select(e => (string)e.Attribute("data-tile-id")!));
            Assert.Equal(svg, TacticalSvgRenderer.Render(draw, width, height));
        }
    }

    [Fact]
    public void RejectsInvalidDimensions()
    {
        // Input validation happens before accessing the polygon sequence.
        Assert.Throws<ArgumentNullException>(() => TacticalSvgRenderer.Render(null!, 100, 100));
    }
}
