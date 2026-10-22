using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalGpuBufferTests
{
    private static PhysicalTacticalRenderMesh Mesh()
    {
        var coarse = new GoldbergParameters(1, 0);
        var fine = new GoldbergParameters(6, 0);
        var map = PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(coarse, fine));
        var world = new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(42UL), WorldGenerationVersion.Initial, fine));
        var selection = PhysicalTacticalRenderSelection.Create(map, map.CoarseTopology.Cells[0].Id, 0);
        return PhysicalTacticalRenderMesh.Create(map, selection, world.StrategicSphericalGeometry);
    }

    [Fact]
    public void OutputMatchesSourceAndRoundTripsPicking()
    {
        var source = Mesh();
        var buffer = PhysicalTacticalGpuBuffer.Create(source);
        Assert.Equal(source.Positions.Count * 3, buffer.VertexXYZ.Count);
        Assert.Equal(source.TriangleIndices.Count, buffer.TriangleIndices.Count);
        Assert.Equal(source.TriangleTileIds.Count, buffer.TrianglePickingIndices.Count);
        for (var i = 0; i < source.TriangleIndices.Count; i++)
            Assert.Equal((uint)source.TriangleIndices[i], buffer.TriangleIndices[i]);
        for (var i = 0; i < source.TriangleTileIds.Count; i++)
            Assert.Equal(source.TriangleTileIds[i], buffer.GetTileForTriangle(i));
        Assert.All(buffer.VertexXYZ, value => Assert.True(float.IsFinite(value)));
    }

    [Fact]
    public void BufferIsDeterministicAndReadOnly()
    {
        var source = Mesh();
        var a = PhysicalTacticalGpuBuffer.Create(source);
        var b = PhysicalTacticalGpuBuffer.Create(source);
        Assert.Equal(a.VertexXYZ, b.VertexXYZ);
        Assert.Equal(a.TriangleIndices, b.TriangleIndices);
        Assert.Equal(a.TrianglePickingIndices, b.TrianglePickingIndices);
        Assert.Throws<NotSupportedException>(() => ((IList<float>)a.VertexXYZ).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<uint>)a.TriangleIndices).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PhysicalTacticalTileId>)a.PickingTiles).Clear());
    }

    [Fact]
    public void InvalidInputsAndPickingBoundsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => PhysicalTacticalGpuBuffer.Create(null!));
        var buffer = PhysicalTacticalGpuBuffer.Create(Mesh());
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetTileForTriangle(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetTileForTriangle(buffer.TrianglePickingIndices.Count));
    }
}
