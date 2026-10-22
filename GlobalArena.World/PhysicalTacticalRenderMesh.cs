namespace GlobalArena.World;

/// <summary>Immutable, API-neutral triangle geometry for canonical physical tiles.</summary>
public sealed class PhysicalTacticalRenderMesh
{
    public IReadOnlyList<SphericalPoint3> Positions { get; }
    public IReadOnlyList<int> TriangleIndices { get; }
    public IReadOnlyList<PhysicalTacticalTileId> TriangleTileIds { get; }

    private PhysicalTacticalRenderMesh(SphericalPoint3[] positions, int[] indices, PhysicalTacticalTileId[] tileIds)
    {
        Positions = Array.AsReadOnly(positions);
        TriangleIndices = Array.AsReadOnly(indices);
        TriangleTileIds = Array.AsReadOnly(tileIds);
    }

    /// <summary>Uses the authoritative fine Goldberg spherical geometry; no independent tile identity is generated.</summary>
    public static PhysicalTacticalRenderMesh Create(
        PhysicalTacticalIncidenceMap map,
        PhysicalTacticalRenderSelection selection,
        StrategicSphericalGeometry fineGeometry)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(fineGeometry);
        if (fineGeometry.Cells.Count != map.FineTopology.Cells.Count ||
            fineGeometry.Vertices.Count != map.FineTopology.Vertices.Count)
            throw new ArgumentException("Fine spherical geometry counts must match the authoritative topology.", nameof(fineGeometry));

        // Reject a different spherical mesh even when its cell/vertex counts match.
        // The authoritative topology owns the canonical geometry.
        var canonical = StrategicSphericalGeometryGenerator.Generate(map.FineTopology);
        for (var i = 0; i < canonical.Vertices.Count; i++)
        {
            var actual = fineGeometry.Vertices[i];
            var expected = canonical.Vertices[i];
            if (actual.VertexId != expected.VertexId || actual.Position != expected.Position)
                throw new ArgumentException("Fine vertex geometry does not match authoritative topology.", nameof(fineGeometry));
        }
        for (var i = 0; i < canonical.Cells.Count; i++)
        {
            var actual = fineGeometry.Cells[i];
            var expected = canonical.Cells[i];
            if (actual.CellId != expected.CellId ||
                actual.Center != expected.Center ||
                !actual.BoundaryVertexIds.SequenceEqual(expected.BoundaryVertexIds))
                throw new ArgumentException("Fine cell geometry does not match authoritative topology.", nameof(fineGeometry));
        }

        var approved = map.TileIncidences.ToDictionary(i => i.PhysicalTacticalTileId);
        var positions = new List<SphericalPoint3>();
        var indices = new List<int>();
        var tileIds = new List<PhysicalTacticalTileId>();
        var seen = new HashSet<PhysicalTacticalTileId>();
        foreach (var item in selection.Items)
        {
            if (!seen.Add(item.TileId) || !approved.TryGetValue(item.TileId, out var incidence) ||
                item.TileId.FineGoldbergParameters != map.Refinement.FineParameters ||
                !incidence.IncidentCoarseCellIds.SequenceEqual(item.IncidentStrategicCells))
                throw new ArgumentException("Selection must contain unique authoritative physical identities and incidences.", nameof(selection));
            var cell = fineGeometry.GetCell(item.TileId.FineStrategicCellId);
            if (cell.CellId != item.TileId.FineStrategicCellId)
                throw new InvalidOperationException("Spherical cell identity mismatch.");
            var start = positions.Count;
            positions.Add(cell.Center);
            foreach (var vertex in cell.BoundaryVertexIds)
                positions.Add(fineGeometry.GetVertex(vertex).Position);
            for (var j = 0; j < cell.BoundaryVertexIds.Count; j++)
            {
                var next = (j + 1) % cell.BoundaryVertexIds.Count;
                var a = positions[start];
                var b = positions[start + 1 + j];
                var c = positions[start + 1 + next];
                var ux = b.X - a.X;
                var uy = b.Y - a.Y;
                var uz = b.Z - a.Z;
                var vx = c.X - a.X;
                var vy = c.Y - a.Y;
                var vz = c.Z - a.Z;
                var winding = a.X * (uy * vz - uz * vy)
                    + a.Y * (uz * vx - ux * vz)
                    + a.Z * (ux * vy - uy * vx);
                if (!double.IsFinite(winding) || winding <= 1e-16)
                    throw new InvalidOperationException("Tactical render triangle is degenerate or incorrectly oriented.");
                indices.Add(start);
                indices.Add(start + 1 + j);
                indices.Add(start + 1 + ((j + 1) % cell.BoundaryVertexIds.Count));
                tileIds.Add(item.TileId);
            }
        }
        return new PhysicalTacticalRenderMesh(positions.ToArray(), indices.ToArray(), tileIds.ToArray());
    }
}
