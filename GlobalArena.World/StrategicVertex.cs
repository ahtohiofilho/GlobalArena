namespace GlobalArena.World;

public sealed class StrategicVertex
{
    public StrategicVertexId Id { get; }

    public IReadOnlyList<StrategicCellId> IncidentCellIds { get; }

    public IReadOnlyList<StrategicEdgeId> IncidentEdgeIds { get; }

    internal StrategicVertex(
        StrategicVertexId id,
        IEnumerable<StrategicCellId> incidentCellIds,
        IEnumerable<StrategicEdgeId> incidentEdgeIds)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Strategic vertex ID must be valid.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(incidentCellIds);
        ArgumentNullException.ThrowIfNull(incidentEdgeIds);

        var cells = incidentCellIds.ToArray();
        var edges = incidentEdgeIds.ToArray();

        if (cells.Length != 3 || edges.Length != 3)
        {
            throw new ArgumentException(
                "A Goldberg strategic vertex must have exactly three incident cells and three incident edges.");
        }

        if (cells.Any(candidate => !candidate.IsValid))
        {
            throw new ArgumentException(
                "Incident strategic cell IDs must be valid.",
                nameof(incidentCellIds));
        }

        if (edges.Any(candidate => !candidate.IsValid))
        {
            throw new ArgumentException(
                "Incident strategic edge IDs must be valid.",
                nameof(incidentEdgeIds));
        }

        if (cells.Distinct().Count() != cells.Length)
        {
            throw new ArgumentException(
                "Incident strategic cell IDs cannot contain duplicates.",
                nameof(incidentCellIds));
        }

        if (edges.Distinct().Count() != edges.Length)
        {
            throw new ArgumentException(
                "Incident strategic edge IDs cannot contain duplicates.",
                nameof(incidentEdgeIds));
        }

        Id = id;
        IncidentCellIds =
            Array.AsReadOnly(
                cells
                    .OrderBy(candidate => candidate.Value)
                    .ToArray());
        IncidentEdgeIds =
            Array.AsReadOnly(
                edges
                    .OrderBy(candidate => candidate.Value)
                    .ToArray());
    }
}
