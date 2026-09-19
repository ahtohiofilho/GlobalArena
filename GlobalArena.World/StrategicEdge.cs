namespace GlobalArena.World;

public sealed class StrategicEdge
{
    public StrategicEdgeId Id { get; }

    public IReadOnlyList<StrategicCellId> IncidentCellIds { get; }

    public IReadOnlyList<StrategicVertexId> IncidentVertexIds { get; }

    internal StrategicEdge(
        StrategicEdgeId id,
        StrategicCellId firstCellId,
        StrategicCellId secondCellId,
        StrategicVertexId firstVertexId,
        StrategicVertexId secondVertexId)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Strategic edge ID must be valid.",
                nameof(id));
        }

        if (!firstCellId.IsValid || !secondCellId.IsValid)
        {
            throw new ArgumentException(
                "Strategic edge incident cell IDs must be valid.");
        }

        if (!firstVertexId.IsValid || !secondVertexId.IsValid)
        {
            throw new ArgumentException(
                "Strategic edge incident vertex IDs must be valid.");
        }

        if (firstCellId == secondCellId)
        {
            throw new ArgumentException(
                "A strategic edge must separate two distinct cells.");
        }

        if (firstVertexId == secondVertexId)
        {
            throw new ArgumentException(
                "A strategic edge must connect two distinct vertices.");
        }

        Id = id;
        IncidentCellIds =
            Array.AsReadOnly(
                new[] { firstCellId, secondCellId }
                    .OrderBy(candidate => candidate.Value)
                    .ToArray());
        IncidentVertexIds =
            Array.AsReadOnly(
                new[] { firstVertexId, secondVertexId }
                    .OrderBy(candidate => candidate.Value)
                    .ToArray());
    }
}
