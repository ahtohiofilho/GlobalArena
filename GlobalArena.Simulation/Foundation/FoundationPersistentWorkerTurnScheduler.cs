using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationPersistentWorkerSlice(
    PhysicalWorkerId Worker, PhysicalTacticalTileId Node, uint StartMicro,
    uint DurationMicro, PhysicalWorkerActivity Activity,
    PhysicalWorkerOccupation Occupation, ulong? ProjectId);

public sealed record FoundationPersistentWorkerTurnEvent(
    EventId Id, WorldStateHash Before, WorldStateHash After,
    IReadOnlyList<FoundationPersistentWorkerSlice> Slices) : ISimulationEvent;

/// <summary>Reference synchronized event-time scheduler. All builders present
/// during the same interval contribute simultaneously. Opening transport
/// quotes remain fixed for new crossings until the next turn.</summary>
public sealed class FoundationPersistentWorkerTurnScheduler : ISimulationEventExecutor
{
    public const string StablePhaseId = "foundation.persistent-worker-time-v20";
    private const uint Whole = 1_000_000;
    private readonly FoundationConstructionApproachRouter _router;
    private static readonly CanonicalWorldStateHasher Hasher = new();

    public FoundationPersistentWorkerTurnScheduler(FoundationConstructionApproachRouter router)
        => _router = router ?? throw new ArgumentNullException(nameof(router));

    private sealed record Mode(PhysicalWorkerId Worker, PhysicalTacticalTileId Node,
        PhysicalWorkerActivity Activity, PhysicalWorkerOccupation Occupation,
        ulong? Project, FoundationPendingEdgeTravel? Travel);

    private static bool Produces(PhysicalWorkerOccupation occupation) =>
        occupation is PhysicalWorkerOccupation.Agriculture or PhysicalWorkerOccupation.Forestry
            or PhysicalWorkerOccupation.Mining;

    private static WorldState Assign(WorldState world, PhysicalWorkerRuntimeRecord worker,
        PhysicalTacticalTileId tile, StrategicCellId region, PhysicalWorkerActivity activity,
        PhysicalWorkerOccupation occupation, FoundationEdgeTravelRuntimeState? journeys = null)
    {
        var roster = new PhysicalWorkerRuntimeState(world.Workers.Workers.Select(w =>
            w.Id == worker.Id ? w.WithTacticalAssignment(tile, region, activity, occupation) : w),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        return journeys is null ? world.WithWorkers(roster) : world.WithPhysicalEdgeTravel(roster, journeys);
    }

    private (WorldState Next, FoundationPersistentWorkerSlice[] Slices) Calculate(WorldState opening)
    {
        if (!opening.WorkerOrders.IsInitialized || !opening.EdgeTravels.IsInitialized ||
            !opening.Constructions.IsInitialized)
            throw new InvalidOperationException("Persistent scheduling requires deployed ledgers.");
        var state = opening;
        var slices = new List<FoundationPersistentWorkerSlice>();
        uint now = 0;
        while (now < Whole)
        {
            var modes = new List<Mode>();
            foreach (var id in state.Workers.Workers.Select(w => w.Id).ToArray())
            {
                while (true)
                {
                    var worker = state.Workers.Workers.Single(w => w.Id == id);
                    var node = worker.TacticalTileId!.Value;
                    var occupation = worker.Occupation!.Value;
                    var queue = state.WorkerOrders.Queues.Single(q => q.Worker == id);
                    var pending = state.EdgeTravels.Pending.SingleOrDefault(p => p.Worker == id);
                    // An existing physical crossing always finishes before a new order.
                    if (pending is not null)
                    {
                        modes.Add(new(id, node, PhysicalWorkerActivity.Traveling, occupation, null, pending));
                        break;
                    }
                    var head = queue.Orders.FirstOrDefault();
                    if (head is null)
                    {
                        modes.Add(new(id, node, worker.Activity, occupation, null, null));
                        break;
                    }
                    if (!queue.ActiveOrderId.HasValue)
                        state = state.StartWorkerOrder(worker.Owner, id);
                    if (head.Kind == FoundationWorkerOrderKind.Occupation)
                    {
                        var nextOccupation = head.Occupation!.Value;
                        var activity = Produces(nextOccupation) ? PhysicalWorkerActivity.Producing : PhysicalWorkerActivity.Other;
                        state = Assign(state, worker, node, worker.CellId, activity, nextOccupation);
                        // Continuous occupation remains the active head until explicit interruption.
                        modes.Add(new(id, node, activity, nextOccupation, null, null));
                        break;
                    }
                    if (head.Kind == FoundationWorkerOrderKind.Build &&
                        state.Constructions.Projects.Single(p => p.Id == head.ProjectId).IsComplete)
                    {
                        state = state.CompleteWorkerOrder(worker.Owner, id, head.Id);
                        continue;
                    }
                    var plan = head.Kind == FoundationWorkerOrderKind.Build
                        ? _router.Plan(state, id) : _router.PlanMove(state, id, head.Destination!.Value);
                    if (plan.Status == FoundationConstructionApproachStatus.AtSite)
                    {
                        if (head.Kind == FoundationWorkerOrderKind.Move)
                        {
                            state = state.CompleteWorkerOrder(worker.Owner, id, head.Id);
                            continue;
                        }
                        state = Assign(state, worker, node, worker.CellId,
                            PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned);
                        modes.Add(new(id, node, PhysicalWorkerActivity.Other,
                            PhysicalWorkerOccupation.Unassigned, head.ProjectId, null));
                        break;
                    }
                    if (plan.Status == FoundationConstructionApproachStatus.RouteAvailable)
                    {
                        // R166 quote from opening infrastructure; activation is deferred
                        // until after the entire timeline, so state transport is unchanged.
                        var (next, _) = FoundationPersistedEdgeTravelTransitions.Start(
                            state, id, plan.Path[1], _router.Movement, _router.Policy);
                        state = next;
                        var travel = state.EdgeTravels.Pending.Single(p => p.Worker == id);
                        modes.Add(new(id, node, PhysicalWorkerActivity.Traveling, occupation, null, travel));
                        break;
                    }
                    state = Assign(state, worker, node, worker.CellId, PhysicalWorkerActivity.Other,
                        PhysicalWorkerOccupation.Unassigned);
                    modes.Add(new(id, node, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null, null));
                    break;
                }
            }

            ulong step = Whole - now;
            foreach (var m in modes.Where(m => m.Travel is not null))
                step = Math.Min(step, m.Travel!.RemainingMicro);
            var groups = modes.Where(m => m.Project.HasValue).GroupBy(m => m.Project!.Value).ToArray();
            foreach (var team in groups)
            {
                var project = state.Constructions.Projects.Single(p => p.Id == team.Key);
                ulong count = (ulong)team.Count();
                ulong remaining = project.RemainingWorkMicro;
                step = Math.Min(step, remaining / count + (remaining % count == 0 ? 0UL : 1UL));
            }
            if (step == 0) throw new InvalidOperationException("Temporal scheduler made no progress.");
            uint duration = checked((uint)step);
            var shares = new Dictionary<PhysicalWorkerId, uint>();
            foreach (var team in groups)
            {
                var project = state.Constructions.Projects.Single(p => p.Id == team.Key);
                var ordered = team.OrderBy(m => m.Worker.Value).ToArray();
                ulong count = (ulong)ordered.Length;
                ulong used = Math.Min(project.RemainingWorkMicro, checked(step * count));
                for (int i = 0; i < ordered.Length; i++)
                    shares[ordered[i].Worker] = checked((uint)(used / count + ((ulong)i < used % count ? 1UL : 0UL)));
                state = state.WithConstructions(new FoundationConstructionRuntimeState(
                    state.Constructions.Projects.Select(p => p.Id == project.Id
                        ? p with { CompletedWorkMicro = p.CompletedWorkMicro + used } : p)));
            }
            foreach (var m in modes)
            {
                uint work = m.Project.HasValue ? shares[m.Worker] : duration;
                if (work > 0) slices.Add(new(m.Worker, m.Node, now, work, m.Activity, m.Occupation, m.Project));
                if (work < duration) slices.Add(new(m.Worker, m.Node, now + work, duration - work,
                    PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null));
                if (m.Travel is not { } t) continue;
                var worker = state.Workers.Workers.Single(w => w.Id == m.Worker);
                var remaining = t.RemainingMicro - step;
                if (_router.Movement.CheckEdge(t.Source, t.Destination) != t.Edge)
                    throw new InvalidOperationException("Invalid canonical crossing.");
                var ledger = state.EdgeTravels.Replace(m.Worker, remaining == 0 ? null : t with { RemainingMicro = remaining });
                var destination = remaining == 0 ? t.Destination : t.Source;
                state = Assign(state, worker, destination,
                    _router.Movement.PresentationRegion(destination, worker.CellId),
                    remaining == 0 ? PhysicalWorkerActivity.Other : PhysicalWorkerActivity.Traveling,
                    m.Occupation, ledger);
            }
            now += duration;
        }
        // Release completed finite orders even on the exact turn boundary.
        foreach (var q in state.WorkerOrders.Queues.ToArray())
        {
            var head = q.Orders.FirstOrDefault();
            if (head is null || q.ActiveOrderId != head.Id) continue;
            var worker = state.Workers.Workers.Single(w => w.Id == q.Worker);
            if ((head.Kind == FoundationWorkerOrderKind.Build &&
                state.Constructions.Projects.Single(p => p.Id == head.ProjectId).IsComplete &&
                !state.EdgeTravels.Pending.Any(p => p.Worker == q.Worker)) ||
                (head.Kind == FoundationWorkerOrderKind.Move && worker.TacticalTileId == head.Destination &&
                !state.EdgeTravels.Pending.Any(p => p.Worker == q.Worker)))
                state = state.CompleteWorkerOrder(q.Owner, q.Worker, head.Id);
        }
        foreach (var p in state.Constructions.Projects.Where(p => p.IsComplete && !p.IsActivated).ToArray())
            state = state.ActivateCompletedConstruction(p.Id);
        return (state.AdvanceRevision(), slices.ToArray());
    }

    public (WorldState Next, FoundationPersistentWorkerTurnEvent Event) Record(WorldState opening, SimulationContext context)
    {
        var (next, slices) = Calculate(opening);
        return (next, new(EventId.ForSystemPhase(context.Turn, StablePhaseId, 1UL),
            Hasher.Compute(opening), Hasher.Compute(next), Array.AsReadOnly(slices)));
    }

    public WorldState Execute(WorldState state, ISimulationEvent simulationEvent, SimulationContext context)
    {
        if (simulationEvent is not FoundationPersistentWorkerTurnEvent ev ||
            ev.Id != EventId.ForSystemPhase(context.Turn, StablePhaseId, 1UL) || Hasher.Compute(state) != ev.Before)
            throw new InvalidOperationException("Stale persistent worker timeline.");
        var (next, actual) = Record(state, context);
        if (actual.After != ev.After || !actual.Slices.SequenceEqual(ev.Slices))
            throw new InvalidOperationException("Persistent worker timeline replay diverged.");
        return next;
    }
}
