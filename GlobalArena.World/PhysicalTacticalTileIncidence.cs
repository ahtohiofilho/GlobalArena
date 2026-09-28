namespace GlobalArena.World;

public sealed class PhysicalTacticalTileIncidence
{
    public PhysicalTacticalTileId PhysicalTacticalTileId { get; }

    public IReadOnlyList<StrategicCellId> IncidentCoarseCellIds { get; }

    public PhysicalTacticalTileIncidence(
        PhysicalTacticalTileId physicalTacticalTileId,
        StrategicTopology coarseTopology,
        IEnumerable<StrategicCellId> incidentCoarseCellIds)
    {
        if (!physicalTacticalTileId.IsValid)
        {
            throw new ArgumentException(
                "Physical tactical tile ID must be valid.",
                nameof(physicalTacticalTileId));
        }

        ArgumentNullException.ThrowIfNull(coarseTopology);
        ArgumentNullException.ThrowIfNull(incidentCoarseCellIds);

        _ =
            new GoldbergScaledRefinement(
                coarseTopology.Parameters,
                physicalTacticalTileId.FineGoldbergParameters);

        var incidentCells =
            incidentCoarseCellIds.ToArray();

        if (incidentCells.Length is < 1 or > 3)
        {
            throw new ArgumentException(
                "Physical tactical tile incidence must contain one, two, or three coarse strategic cells.",
                nameof(incidentCoarseCellIds));
        }

        if (incidentCells.Any(candidate => !candidate.IsValid))
        {
            throw new ArgumentException(
                "Incident coarse strategic cell IDs must be valid.",
                nameof(incidentCoarseCellIds));
        }

        if (incidentCells.Distinct().Count() != incidentCells.Length)
        {
            throw new ArgumentException(
                "Incident coarse strategic cell IDs cannot contain duplicates.",
                nameof(incidentCoarseCellIds));
        }

        var ordered =
            incidentCells
                .OrderBy(candidate => candidate.Value)
                .ToArray();

        if (ordered.Any(candidate => candidate.Value > (ulong)coarseTopology.Cells.Count))
        {
            throw new ArgumentException(
                "Incident coarse strategic cell IDs must exist in the authoritative coarse topology.",
                nameof(incidentCoarseCellIds));
        }

        if (ordered.Length == 2
            && !coarseTopology.Edges.Any(
                edge =>
                    edge.IncidentCellIds.SequenceEqual(
                        ordered)))
        {
            throw new ArgumentException(
                "Two-cell physical incidence must correspond to one authoritative coarse strategic edge.",
                nameof(incidentCoarseCellIds));
        }

        if (ordered.Length == 3
            && !coarseTopology.Vertices.Any(
                vertex =>
                    vertex.IncidentCellIds.SequenceEqual(
                        ordered)))
        {
            throw new ArgumentException(
                "Three-cell physical incidence must correspond to one authoritative coarse strategic vertex.",
                nameof(incidentCoarseCellIds));
        }

        PhysicalTacticalTileId =
            physicalTacticalTileId;

        IncidentCoarseCellIds =
            Array.AsReadOnly(
                ordered);
    }
}