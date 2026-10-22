using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalRenderMeshTests
{
    private static (PhysicalTacticalIncidenceMap Map, StrategicSphericalGeometry Geo) Setup()
    {
        var coarse = new GoldbergParameters(1, 0);
        var fine = new GoldbergParameters(6, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(coarse, fine));
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(42UL), WorldGenerationVersion.Initial, fine));
        return (map, world.StrategicSphericalGeometry);
    }

    [Fact]
    public void FullGlobeProducesCanonicalTrianglesAndMetadata()
    {
        var (map, geo) = Setup();
        var selection = PhysicalTacticalRenderSelection.Create(map, map.CoarseTopology.Cells[0].Id, map.CoarseTopology.Cells.Count);
        var mesh = PhysicalTacticalRenderMesh.Create(map, selection, geo);
        Assert.Equal(geo.Cells.Sum(c => c.BoundaryVertexIds.Count), mesh.TriangleTileIds.Count);
        Assert.Equal(mesh.TriangleTileIds.Count * 3, mesh.TriangleIndices.Count);
        Assert.Equal(geo.Cells.Sum(c => c.BoundaryVertexIds.Count + 1), mesh.Positions.Count);
        Assert.Equal(map.TileIncidences.Select(i => i.PhysicalTacticalTileId).ToArray(),
            mesh.TriangleTileIds.Distinct().ToArray());
        Assert.All(mesh.TriangleIndices, i => Assert.InRange(i, 0, mesh.Positions.Count - 1));
    }

    [Fact]
    public void LocalViewportTriangulatesOnlySelectedPhysicalTiles()
    {
        var (map, geo) = Setup();
        var selection = PhysicalTacticalRenderSelection.Create(map, map.CoarseTopology.Cells[0].Id, 0);
        var mesh = PhysicalTacticalRenderMesh.Create(map, selection, geo);
        Assert.Equal(selection.Items.Select(i => i.TileId).ToArray(), mesh.TriangleTileIds.Distinct().ToArray());
    }

    [Fact]
    public void GeometryIsDeterministicAndCollectionsAreReadOnly()
    {
        var (map, geo) = Setup();
        var selection = PhysicalTacticalRenderSelection.Create(map, map.CoarseTopology.Cells[0].Id, 1);
        var first = PhysicalTacticalRenderMesh.Create(map, selection, geo);
        var second = PhysicalTacticalRenderMesh.Create(map, selection, geo);
        Assert.Equal(first.Positions, second.Positions);
        Assert.Equal(first.TriangleIndices, second.TriangleIndices);
        Assert.Equal(first.TriangleTileIds, second.TriangleTileIds);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)first.TriangleIndices).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PhysicalTacticalTileId>)first.TriangleTileIds).Clear());
    }

    [Fact]
    public void EveryTriangleIsOutwardAndCarriesCorrectCanonicalTileId()
    {
        var (map, geo) = Setup();
        var selection = PhysicalTacticalRenderSelection.Create(
            map, map.CoarseTopology.Cells[0].Id, map.CoarseTopology.Cells.Count);
        var mesh = PhysicalTacticalRenderMesh.Create(map, selection, geo);
        var counts = mesh.TriangleTileIds.GroupBy(id => id).ToDictionary(g => g.Key, g => g.Count());
        foreach (var incidence in map.TileIncidences)
        {
            var geometry = geo.GetCell(incidence.PhysicalTacticalTileId.FineStrategicCellId);
            Assert.Equal(geometry.BoundaryVertexIds.Count, counts[incidence.PhysicalTacticalTileId]);
        }
        for (var t = 0; t < mesh.TriangleTileIds.Count; t++)
        {
            var a = mesh.Positions[mesh.TriangleIndices[3 * t]];
            var b = mesh.Positions[mesh.TriangleIndices[3 * t + 1]];
            var c = mesh.Positions[mesh.TriangleIndices[3 * t + 2]];
            var ux = b.X - a.X; var uy = b.Y - a.Y; var uz = b.Z - a.Z;
            var vx = c.X - a.X; var vy = c.Y - a.Y; var vz = c.Z - a.Z;
            var winding = a.X * (uy * vz - uz * vy)
                + a.Y * (uz * vx - ux * vz)
                + a.Z * (ux * vy - uy * vx);
            Assert.True(winding > 1e-16, $"Bad winding for physical tile {mesh.TriangleTileIds[t]}");
        }
    }

    [Fact]
    public void InvalidArgumentsAndMismatchedGeometryAreRejected()
    {
        var (map, geo) = Setup();
        var selection = PhysicalTacticalRenderSelection.Create(map, map.CoarseTopology.Cells[0].Id, 0);
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalRenderMesh.Create(null!, selection, geo));
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalRenderMesh.Create(map, null!, geo));
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalRenderMesh.Create(map, selection, null!));
        var other = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(42UL), WorldGenerationVersion.Initial, new GoldbergParameters(2, 0))).StrategicSphericalGeometry;
        Assert.Throws<ArgumentException>(() => PhysicalTacticalRenderMesh.Create(map, selection, other));
    }
}
