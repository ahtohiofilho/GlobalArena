using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed record MilitaryMoveEvent
    : ISimulationEvent
{
    public EventId Id { get; }

    public CivilizationId IssuingCivilizationId { get; }

    public MilitaryUnitId MilitaryUnitId { get; }

    public StrategicCellId ExpectedSourceStrategicCellId { get; }

    public StrategicCellId DestinationStrategicCellId { get; }

    public StrategicEdgeId TraversedStrategicEdgeId { get; }

    public MilitaryMoveEvent(
        EventId id,
        CivilizationId issuingCivilizationId,
        MilitaryUnitId militaryUnitId,
        StrategicCellId expectedSourceStrategicCellId,
        StrategicCellId destinationStrategicCellId,
        StrategicEdgeId traversedStrategicEdgeId)
    {
        if (id.OriginCommandId.Turn.Value == 0UL
            || id.OriginCommandId.Sequence == 0UL
            || id.Sequence == 0UL)
        {
            throw new ArgumentException(
                "Military move event ID must contain a valid originating command and event sequence.",
                nameof(id));
        }

        if (!issuingCivilizationId.IsValid)
        {
            throw new ArgumentException(
                "Military move event issuer must be a valid civilization identity.",
                nameof(issuingCivilizationId));
        }

        if (!militaryUnitId.IsValid)
        {
            throw new ArgumentException(
                "Military move event unit must be a valid military unit identity.",
                nameof(militaryUnitId));
        }

        if (!expectedSourceStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Military move event expected source must be a valid strategic cell identity.",
                nameof(expectedSourceStrategicCellId));
        }

        if (!destinationStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Military move event destination must be a valid strategic cell identity.",
                nameof(destinationStrategicCellId));
        }

        if (expectedSourceStrategicCellId
            == destinationStrategicCellId)
        {
            throw new ArgumentException(
                "Military move event source and destination must be distinct.",
                nameof(destinationStrategicCellId));
        }

        if (!traversedStrategicEdgeId.IsValid)
        {
            throw new ArgumentException(
                "Military move event traversed edge must be a valid strategic edge identity.",
                nameof(traversedStrategicEdgeId));
        }

        Id = id;
        IssuingCivilizationId = issuingCivilizationId;
        MilitaryUnitId = militaryUnitId;
        ExpectedSourceStrategicCellId =
            expectedSourceStrategicCellId;
        DestinationStrategicCellId =
            destinationStrategicCellId;
        TraversedStrategicEdgeId =
            traversedStrategicEdgeId;
    }
}