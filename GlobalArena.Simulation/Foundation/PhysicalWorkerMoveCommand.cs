using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// New-foundation worker order: a real one-edge move OR an in-place activity
/// change. A moving worker defaults to Traveling. Ownership belongs to the
/// authoritative worker census, not to a fabricated economic producer.
/// </summary>
public sealed record PhysicalWorkerMoveCommand : ISimulationCommand
{
    public CommandId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public StrategicCellId Destination { get; }
    public PhysicalWorkerActivity? NextActivity { get; }

    public PhysicalWorkerMoveCommand(
        CommandId id, CivilizationId issuer, PhysicalWorkerId workerId,
        StrategicCellId destination, PhysicalWorkerActivity? nextActivity = null)
    {
        if (id.Turn.Value == 0UL || id.Sequence == 0UL ||
            !issuer.IsValid || !workerId.IsValid || !destination.IsValid ||
            (nextActivity.HasValue && !Enum.IsDefined(nextActivity.Value)))
            throw new ArgumentException("Invalid physical worker move order.");
        Id = id;
        Issuer = issuer;
        WorkerId = workerId;
        Destination = destination;
        NextActivity = nextActivity;
    }
}

/// <summary>
/// Audit event includes the BEFORE cell and the actual traversed Goldberg edge.
/// In-place activity changes have no traversed edge.
/// </summary>
public sealed record PhysicalWorkerMovedEvent : ISimulationEvent
{
    public EventId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public StrategicCellId Source { get; }
    public StrategicCellId Destination { get; }
    public StrategicEdgeId? TraversedEdge { get; }
    public PhysicalWorkerActivity NextActivity { get; }

    public PhysicalWorkerMovedEvent(
        EventId id, CivilizationId issuer, PhysicalWorkerId workerId,
        StrategicCellId source, StrategicCellId destination,
        StrategicEdgeId? traversedEdge, PhysicalWorkerActivity nextActivity)
    {
        if (id.OriginKind != EventOriginKind.Command ||
            id.Turn.Value == 0UL || id.Sequence == 0UL ||
            id.OriginCommandId.Sequence == 0UL ||
            !issuer.IsValid || !workerId.IsValid ||
            !source.IsValid || !destination.IsValid ||
            !Enum.IsDefined(nextActivity) ||
            (source == destination && traversedEdge.HasValue) ||
            (source != destination &&
                (!traversedEdge.HasValue || !traversedEdge.Value.IsValid)))
            throw new ArgumentException("Invalid physical worker movement event.");
        Id = id;
        Issuer = issuer;
        WorkerId = workerId;
        Source = source;
        Destination = destination;
        TraversedEdge = traversedEdge;
        NextActivity = nextActivity;
    }
}
