namespace GlobalArena.World;

public sealed class SharedBorderIncidence
{
    public StrategicEdge StrategicEdge { get; }

    public SharedBorderBand SharedBorderBand { get; }

    public IReadOnlyList<TacticalRegion> IncidentRegions { get; }

    internal SharedBorderIncidence(
        StrategicEdge strategicEdge,
        SharedBorderBand sharedBorderBand,
        TacticalRegion firstIncidentRegion,
        TacticalRegion secondIncidentRegion)
    {
        ArgumentNullException.ThrowIfNull(
            strategicEdge);

        ArgumentNullException.ThrowIfNull(
            sharedBorderBand);

        ArgumentNullException.ThrowIfNull(
            firstIncidentRegion);

        ArgumentNullException.ThrowIfNull(
            secondIncidentRegion);

        if (sharedBorderBand.StrategicEdgeId
            != strategicEdge.Id)
        {
            throw new ArgumentException(
                "Shared border band must belong to the strategic edge.",
                nameof(sharedBorderBand));
        }

        if (firstIncidentRegion.StrategicCellId
            != strategicEdge.IncidentCellIds[0]
            || secondIncidentRegion.StrategicCellId
            != strategicEdge.IncidentCellIds[1])
        {
            throw new ArgumentException(
                "Incident tactical regions must match the strategic edge incident cells in canonical order.");
        }

        StrategicEdge =
            strategicEdge;

        SharedBorderBand =
            sharedBorderBand;

        IncidentRegions =
            Array.AsReadOnly(
                new[]
                {
                    firstIncidentRegion,
                    secondIncidentRegion
                });
    }
}
