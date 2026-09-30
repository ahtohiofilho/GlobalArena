namespace GlobalArena.World;

public sealed class BoundedTacticalScalarSample
{
    public PhysicalTacticalTileId PhysicalTacticalTileId { get; }

    public IReadOnlyList<StrategicCellId> IncidentStrategicCellIds { get; }

    public long RawValue { get; }

    public int AggregationWeight { get; }

    internal BoundedTacticalScalarSample(
        PhysicalTacticalTileId physicalTacticalTileId,
        IEnumerable<StrategicCellId> incidentStrategicCellIds,
        long rawValue)
    {
        if (!physicalTacticalTileId.IsValid)
        {
            throw new ArgumentException(
                "Physical tactical tile ID must be valid.",
                nameof(physicalTacticalTileId));
        }

        ArgumentNullException.ThrowIfNull(
            incidentStrategicCellIds);

        var incidentIds =
            incidentStrategicCellIds
                .OrderBy(
                    id =>
                        id.Value)
                .ToArray();

        if (incidentIds.Length is < 1 or > 3)
        {
            throw new ArgumentException(
                "A bounded tactical sample must have one, two, or three incident strategic cells.",
                nameof(incidentStrategicCellIds));
        }

        if (incidentIds.Any(
            id =>
                !id.IsValid)
            || incidentIds.Distinct().Count()
                != incidentIds.Length)
        {
            throw new ArgumentException(
                "Incident strategic cell IDs must be valid and unique.",
                nameof(incidentStrategicCellIds));
        }

        PhysicalTacticalTileId =
            physicalTacticalTileId;

        IncidentStrategicCellIds =
            Array.AsReadOnly(
                incidentIds);

        RawValue =
            rawValue;

        AggregationWeight =
            6 / incidentIds.Length;
    }
}
