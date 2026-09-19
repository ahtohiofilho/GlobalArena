namespace GlobalArena.World;

public sealed class StrategicCell
{
    public StrategicCellId Id { get; }

    public StrategicCellKind Kind { get; }

    public IReadOnlyList<StrategicCellId> AdjacentCellIds { get; }

    public IReadOnlyList<StrategicEdgeId> IncidentEdgeIds { get; }

    public IReadOnlyList<StrategicVertexId> IncidentVertexIds { get; }

    internal StrategicCell(
        StrategicCellId id,
        StrategicCellKind kind,
        IEnumerable<StrategicCellId> adjacentCellIds,
        IEnumerable<StrategicEdgeId> incidentEdgeIds,
        IEnumerable<StrategicVertexId> incidentVertexIds)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Strategic cell ID must be valid.",
                nameof(id));
        }

        var expectedDegree = kind switch
        {
            StrategicCellKind.Pentagon => 5,
            StrategicCellKind.Hexagon => 6,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                "Unsupported strategic cell kind.")
        };

        ArgumentNullException.ThrowIfNull(adjacentCellIds);
        ArgumentNullException.ThrowIfNull(incidentEdgeIds);
        ArgumentNullException.ThrowIfNull(incidentVertexIds);

        var adjacent = adjacentCellIds.ToArray();
        var edges = incidentEdgeIds.ToArray();
        var vertices = incidentVertexIds.ToArray();

        if (adjacent.Any(candidate => !candidate.IsValid))
        {
            throw new ArgumentException(
                "Adjacent strategic cell IDs must be valid.",
                nameof(adjacentCellIds));
        }

        if (edges.Any(candidate => !candidate.IsValid))
        {
            throw new ArgumentException(
                "Incident strategic edge IDs must be valid.",
                nameof(incidentEdgeIds));
        }

        if (vertices.Any(candidate => !candidate.IsValid))
        {
            throw new ArgumentException(
                "Incident strategic vertex IDs must be valid.",
                nameof(incidentVertexIds));
        }

        if (adjacent.Contains(id))
        {
            throw new ArgumentException(
                "A strategic cell cannot be adjacent to itself.",
                nameof(adjacentCellIds));
        }

        if (adjacent.Distinct().Count() != adjacent.Length)
        {
            throw new ArgumentException(
                "Adjacent strategic cell IDs cannot contain duplicates.",
                nameof(adjacentCellIds));
        }

        if (edges.Distinct().Count() != edges.Length)
        {
            throw new ArgumentException(
                "Incident strategic edge IDs cannot contain duplicates.",
                nameof(incidentEdgeIds));
        }

        if (vertices.Distinct().Count() != vertices.Length)
        {
            throw new ArgumentException(
                "Incident strategic vertex IDs cannot contain duplicates.",
                nameof(incidentVertexIds));
        }

        if (adjacent.Length != expectedDegree
            || edges.Length != expectedDegree
            || vertices.Length != expectedDegree)
        {
            throw new ArgumentException(
                "Strategic cell incidence counts must match its polygon kind.");
        }

        Id = id;
        Kind = kind;
        AdjacentCellIds =
            Array.AsReadOnly(
                adjacent
                    .OrderBy(candidate => candidate.Value)
                    .ToArray());
        IncidentEdgeIds =
            Array.AsReadOnly(
                edges
                    .OrderBy(candidate => candidate.Value)
                    .ToArray());
        IncidentVertexIds =
            Array.AsReadOnly(
                vertices
                    .OrderBy(candidate => candidate.Value)
                    .ToArray());
    }
}
