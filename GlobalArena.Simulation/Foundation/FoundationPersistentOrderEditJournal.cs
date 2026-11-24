using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public enum FoundationPersistentOrderEditKind { Insert = 1, Remove = 2, Reorder = 3 }

public sealed record FoundationPersistentOrderEdit(
    CivilizationId Issuer, PhysicalWorkerId Worker,
    FoundationPersistentOrderEditKind Kind, ulong OrderId = 0,
    int Index = 0, FoundationWorkerOrderKind? OrderKind = null,
    PhysicalTacticalTileId? Destination = null, ulong? ProjectId = null,
    PhysicalWorkerOccupation? Occupation = null);

public sealed record FoundationPersistentOrderEditEvent(
    EventId Id, FoundationPersistentOrderEdit Edit,
    WorldStateHash Before, WorldStateHash After) : ISimulationEvent;

/// <summary>Recompute every edit from its canonical inputs. Never trust a supplied resulting queue.</summary>
public sealed class FoundationPersistentOrderEditJournal : ISimulationEventExecutor
{
    private static readonly CanonicalWorldStateHasher Hasher = new();

    private static WorldState Apply(WorldState world, FoundationPersistentOrderEdit edit)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(edit);
        if (!world.WorkerOrders.IsInitialized || !edit.Issuer.IsValid || !edit.Worker.IsValid)
            throw new InvalidOperationException("Persistent order edit requires canonical queue deployment.");
        var noPayload = edit.OrderKind is null && edit.Destination is null &&
            edit.ProjectId is null && edit.Occupation is null;
        return (edit.Kind switch
        {
            FoundationPersistentOrderEditKind.Insert when edit.OrderId == 0 && edit.OrderKind.HasValue =>
                world.InsertWorkerOrder(edit.Issuer, edit.Worker, edit.Index, edit.OrderKind.Value,
                    edit.Destination, edit.ProjectId, edit.Occupation),
            FoundationPersistentOrderEditKind.Remove when edit.OrderId != 0 && edit.Index == 0 && noPayload =>
                world.RemoveWorkerOrder(edit.Issuer, edit.Worker, edit.OrderId),
            FoundationPersistentOrderEditKind.Reorder when edit.OrderId != 0 && noPayload =>
                world.ReorderWorkerOrder(edit.Issuer, edit.Worker, edit.OrderId, edit.Index),
            _ => throw new InvalidOperationException("Malformed persistent order edit.")
        }).AdvanceRevision();
    }

    public static (WorldState Next, FoundationPersistentOrderEditEvent Event) Record(
        WorldState world, EventId id, FoundationPersistentOrderEdit edit, SimulationContext context)
    {
        if (id.OriginKind != EventOriginKind.Command || id.Turn != context.Turn || id.Sequence == 0)
            throw new InvalidOperationException("Invalid persistent order event identity.");
        var next = Apply(world, edit);
        return (next, new(id, edit, Hasher.Compute(world), Hasher.Compute(next)));
    }

    public WorldState Execute(WorldState state, ISimulationEvent simulationEvent, SimulationContext context)
    {
        if (simulationEvent is not FoundationPersistentOrderEditEvent ev ||
            ev.Id.OriginKind != EventOriginKind.Command || ev.Id.Turn != context.Turn ||
            ev.Id.Sequence == 0 || Hasher.Compute(state) != ev.Before)
            throw new InvalidOperationException("Stale or invalid persistent order event.");
        var next = Apply(state, ev.Edit);
        if (Hasher.Compute(next) != ev.After)
            throw new InvalidOperationException("Persistent order replay diverged.");
        return next;
    }
}
