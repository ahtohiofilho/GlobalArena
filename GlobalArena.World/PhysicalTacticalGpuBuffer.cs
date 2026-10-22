namespace GlobalArena.World;

/// <summary>Immutable, graphics-API-neutral, float32 GPU upload snapshot.</summary>
public sealed class PhysicalTacticalGpuBuffer
{
    public IReadOnlyList<float> VertexXYZ { get; }
    public IReadOnlyList<uint> TriangleIndices { get; }
    public IReadOnlyList<PhysicalTacticalTileId> PickingTiles { get; }
    public IReadOnlyList<uint> TrianglePickingIndices { get; }

    private PhysicalTacticalGpuBuffer(float[] vertices, uint[] triangles,
        PhysicalTacticalTileId[] pickingTiles, uint[] trianglePickingIndices)
    {
        VertexXYZ = Array.AsReadOnly(vertices);
        TriangleIndices = Array.AsReadOnly(triangles);
        PickingTiles = Array.AsReadOnly(pickingTiles);
        TrianglePickingIndices = Array.AsReadOnly(trianglePickingIndices);
    }

    public PhysicalTacticalTileId GetTileForTriangle(int triangleIndex)
    {
        if (triangleIndex < 0 || triangleIndex >= TrianglePickingIndices.Count)
            throw new ArgumentOutOfRangeException(nameof(triangleIndex));
        return PickingTiles[checked((int)TrianglePickingIndices[triangleIndex])];
    }

    public static PhysicalTacticalGpuBuffer Create(PhysicalTacticalRenderMesh source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.TriangleIndices.Count % 3 != 0 ||
            source.TriangleTileIds.Count != source.TriangleIndices.Count / 3)
            throw new ArgumentException("Triangle metadata must match index count.", nameof(source));

        var positions = new float[checked(source.Positions.Count * 3)];
        for (var i = 0; i < source.Positions.Count; i++)
        {
            var point = source.Positions[i];
            if (!point.IsValid) throw new ArgumentException("Invalid spherical position.", nameof(source));
            positions[3 * i] = checked((float)point.X);
            positions[3 * i + 1] = checked((float)point.Y);
            positions[3 * i + 2] = checked((float)point.Z);
            if (!float.IsFinite(positions[3 * i]) ||
                !float.IsFinite(positions[3 * i + 1]) ||
                !float.IsFinite(positions[3 * i + 2]))
                throw new ArgumentException("Position cannot be encoded as finite float32.", nameof(source));
        }
        var indices = new uint[source.TriangleIndices.Count];
        for (var i = 0; i < indices.Length; i++)
        {
            var sourceIndex = source.TriangleIndices[i];
            if (sourceIndex < 0 || sourceIndex >= source.Positions.Count)
                throw new ArgumentException("Triangle index outside position buffer.", nameof(source));
            indices[i] = checked((uint)sourceIndex);
        }
        var unique = new List<PhysicalTacticalTileId>();
        var lookup = new Dictionary<PhysicalTacticalTileId, uint>();
        var picks = new uint[source.TriangleTileIds.Count];
        for (var i = 0; i < picks.Length; i++)
        {
            var tile = source.TriangleTileIds[i];
            if (!tile.IsValid) throw new ArgumentException("Invalid physical tile identity.", nameof(source));
            if (!lookup.TryGetValue(tile, out var slot))
            {
                slot = checked((uint)unique.Count);
                lookup.Add(tile, slot);
                unique.Add(tile);
            }
            picks[i] = slot;
        }
        return new PhysicalTacticalGpuBuffer(positions, indices, unique.ToArray(), picks);
    }
}
