using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Worker-only basic settlement move. The v8 WorldState still has a resident
/// census, so the transition mirrors each transferred worker as exactly one
/// census entry for backwards compatibility; independent resident headcounts
/// are not a separate Neolithic gameplay resource.
/// </summary>
public sealed class EconomicPrimitiveWorkerMoveOrder
{
    public EconomicPointId SourcePointId { get; }
    public EconomicPointId NewPointId { get; }
    public StrategicCellId DestinationStrategicCellId { get; }
    public ulong Workers { get; }
    public IReadOnlyList<EconomicActivityWorkforceAllocation> MovingAllocations { get; }

    internal EconomicAdjacentSiteRelocationOrder CompatibleLegacyOrder { get; }

    public EconomicPrimitiveWorkerMoveOrder(
        EconomicPointId sourcePointId, EconomicPointId newPointId,
        StrategicCellId destinationStrategicCellId, ulong workers,
        IEnumerable<EconomicActivityWorkforceAllocation> movingAllocations)
    {
        // The existing relocation contract validates identities, positive
        // counts, duplicate activity kinds and allocated workforce bounds.
        CompatibleLegacyOrder = new EconomicAdjacentSiteRelocationOrder(
            sourcePointId, newPointId, destinationStrategicCellId,
            workers, workers, movingAllocations);
        SourcePointId = sourcePointId;
        NewPointId = newPointId;
        DestinationStrategicCellId = destinationStrategicCellId;
        Workers = workers;
        MovingAllocations = CompatibleLegacyOrder.MovingAllocations;
    }
}
