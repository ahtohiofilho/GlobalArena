namespace GlobalArena.World;

/// <summary>
/// Research-only alternative placement for Goldberg dual vertices.
/// Preserves authoritative topology and vertex identifiers; does not modify canonical geometry.
/// </summary>
public static class GoldbergCentroidGeometryCandidate
{
    public static IReadOnlyList<SphericalPoint3> Build(
        StrategicTopology topology,
        StrategicSphericalGeometry canonicalGeometry)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(canonicalGeometry);
        if (canonicalGeometry.Cells.Count != topology.Cells.Count ||
            canonicalGeometry.Vertices.Count != topology.Vertices.Count)
            throw new ArgumentException("Geometry cardinality differs from topology.", nameof(canonicalGeometry));

        var positions = new SphericalPoint3[topology.Vertices.Count];
        for (var i = 0; i < topology.Vertices.Count; i++)
        {
            var vertex = topology.Vertices[i];
            if (canonicalGeometry.Vertices[i].VertexId != vertex.Id ||
                vertex.IncidentCellIds.Count != 3)
                throw new ArgumentException("Vertex identity/incidence mismatch.", nameof(canonicalGeometry));
            var a = canonicalGeometry.GetCell(vertex.IncidentCellIds[0]);
            var b = canonicalGeometry.GetCell(vertex.IncidentCellIds[1]);
            var c = canonicalGeometry.GetCell(vertex.IncidentCellIds[2]);
            if (a.CellId != vertex.IncidentCellIds[0] ||
                b.CellId != vertex.IncidentCellIds[1] ||
                c.CellId != vertex.IncidentCellIds[2])
                throw new ArgumentException("Cell identity mismatch.", nameof(canonicalGeometry));
            positions[i] = new SphericalPoint3(
                a.Center.X + b.Center.X + c.Center.X,
                a.Center.Y + b.Center.Y + c.Center.Y,
                a.Center.Z + b.Center.Z + c.Center.Z);
        }
        return Array.AsReadOnly(positions);
    }
}
