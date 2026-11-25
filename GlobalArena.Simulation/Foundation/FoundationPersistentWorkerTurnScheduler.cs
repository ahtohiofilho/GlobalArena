using GlobalArena.Kernel;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationPersistentWorkerSlice(
    PhysicalWorkerId Worker, PhysicalTacticalTileId Node, uint StartMicro,
    uint DurationMicro, PhysicalWorkerActivity Activity,
    PhysicalWorkerOccupation Occupation, ulong? ProjectId, byte? Crop = null);

public sealed record FoundationPersistentWorkerTurnEvent(
    EventId Id, WorldStateHash Before, WorldStateHash After,
    IReadOnlyList<FoundationPersistentWorkerSlice> Slices) : ISimulationEvent;

/// <summary>Reference synchronized event-time scheduler. All builders present
/// during the same interval contribute simultaneously. Opening transport
/// quotes remain fixed for new crossings until the next turn.</summary>
public sealed class FoundationPersistentWorkerTurnScheduler : ISimulationEventExecutor
{
    public const string StablePhaseId = FoundationTemporalEconomicJournalGate.StablePhaseId;
    private const uint Whole = 1_000_000;
    private readonly FoundationConstructionApproachRouter _router;
    private static readonly CanonicalWorldStateHasher Hasher = new();
    private readonly FoundationAgriculturalProductionPolicy? _agriculture;
    private readonly FoundationAuthoritativeEconomicSettlementPolicy? _automaticEconomy;
    private readonly FoundationOperationalFreightShadowPolicy? _automaticOperations;

    public FoundationPersistentWorkerTurnScheduler(FoundationConstructionApproachRouter router,
        FoundationAgriculturalProductionPolicy? agriculture = null,
        FoundationAuthoritativeEconomicSettlementPolicy? experimentalAutomaticEconomy = null,
        FoundationOperationalFreightShadowPolicy? automaticOperations = null)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _agriculture = agriculture;
        _automaticEconomy = experimentalAutomaticEconomy;
        _automaticOperations = automaticOperations;
        if (_automaticEconomy is not null && agriculture is null)
            throw new InvalidOperationException("Experimental automatic base requires crop policy.");
        if (automaticOperations is not null && _automaticEconomy is null)
            throw new InvalidOperationException("Automatic operations require the experimental allocator.");
        automaticOperations?.RequireValid();
        if (agriculture is not null && !ReferenceEquals(agriculture.Geography, router.Geography))
            throw new InvalidOperationException("Base crop scheduling requires the same signed geography.");
    }

    private sealed record Mode(PhysicalWorkerId Worker, PhysicalTacticalTileId Node,
        PhysicalWorkerActivity Activity, PhysicalWorkerOccupation Occupation,
        ulong? Project, FoundationPendingEdgeTravel? Travel, byte? Crop = null);

    private static bool Produces(PhysicalWorkerOccupation occupation) =>
        occupation is PhysicalWorkerOccupation.Agriculture or PhysicalWorkerOccupation.Forestry
            or PhysicalWorkerOccupation.Mining;

    private static WorldState Assign(WorldState world, PhysicalWorkerRuntimeRecord worker,
        PhysicalTacticalTileId tile, StrategicCellId region, PhysicalWorkerActivity activity,
        PhysicalWorkerOccupation occupation, FoundationEdgeTravelRuntimeState? journeys = null)
    {
        if (journeys is null && worker.TacticalTileId == tile && worker.CellId == region &&
            worker.Activity == activity && worker.Occupation == occupation) return world;
        var roster = new PhysicalWorkerRuntimeState(world.Workers.Workers.Select(w =>
            w.Id == worker.Id ? w.WithTacticalAssignment(tile, region, activity, occupation) : w),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        return journeys is null ? world.WithWorkers(roster) : world.WithPhysicalEdgeTravel(roster, journeys);
    }

    // Automatic availability is marked as an idle placeholder only under
    // explicit experimental policy, then resolved on the complete timeline.
    private Mode BaseMode(PhysicalWorkerRuntimeRecord worker, FoundationWorkerBaseActivity preference)
    {
        var node = worker.TacticalTileId!.Value;
        if (preference.Mode == FoundationWorkerBaseMode.Automatic)
        {
            if (_automaticEconomy is null)
                throw new InvalidOperationException("Automatic base allocation is not activated.");
            return new(worker.Id, node, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null, null);
        }
        var occupation = preference.Occupation!.Value;
        if (preference.Crop.HasValue && _agriculture is null)
            throw new InvalidOperationException("Manual crop base requires agricultural policy.");
        int index = checked((int)node.FineStrategicCellId.Value - 1);
        // Provisional conservative eligibility: retain preference but do not
        // invent work/output on incompatible physical surfaces.
        bool eligible = _router.Geography.SurfaceAtIndex(index) == FoundationFineSurfaceKind.Land;
        if (eligible && preference.Crop is { } selected)
            eligible = _agriculture!.Resolve(node, (CropId)selected).WholeTurnOutput > 0;
        if (!eligible)
            return new(worker.Id, node, PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null, null);
        return new(worker.Id, node, Produces(occupation) ? PhysicalWorkerActivity.Producing :
            PhysicalWorkerActivity.Other, occupation, null, null, preference.Crop);
    }

    private (WorldState Next, FoundationPersistentWorkerSlice[] Slices) Calculate(WorldState opening, SimulationContext context)
    {
        if (!opening.WorkerOrders.IsInitialized || !opening.EdgeTravels.IsInitialized ||
            !opening.Constructions.IsInitialized)
            throw new InvalidOperationException("Persistent scheduling requires deployed ledgers.");
        // Reject unresolved policies before any timeline work, even if a task
        // would consume the entire turn. No implicit automatic allocation.
        foreach (var q in opening.WorkerOrders.Queues)
            if (q.BaseActivity is { } b && ((b.Mode == FoundationWorkerBaseMode.Automatic && _automaticEconomy is null) ||
                (b.Crop.HasValue && _agriculture is null)))
                throw new InvalidOperationException("Base activity execution policy is unavailable.");
        var state = opening;
        var slices = new List<FoundationPersistentWorkerSlice>();
        var automaticStarts = new Dictionary<PhysicalWorkerId, uint>();
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
                        if (queue.BaseActivity is { } preference)
                        {
                            var mode = BaseMode(worker, preference);
                            if (preference.Mode == FoundationWorkerBaseMode.Automatic)
                                automaticStarts.TryAdd(id, now);
                            state = Assign(state, worker, node, worker.CellId, mode.Activity, mode.Occupation);
                            modes.Add(mode);
                        }
                        else modes.Add(new(id, node, worker.Activity, occupation, null, null));
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
                        modes.Add(new(id, node, activity, nextOccupation, null, null, head.Crop));
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
                if (work > 0) slices.Add(new(m.Worker, m.Node, now, work, m.Activity, m.Occupation, m.Project, m.Crop));
                if (work < duration)
                {
                    // Team completion can leave a sub-micro remainder for a
                    // subset of builders. Preserve existing idle rounding
                    // for this interval; no overlapping or invented effort.
                    slices.Add(new(m.Worker, m.Node, now + work, duration - work,
                        PhysicalWorkerActivity.Other, PhysicalWorkerOccupation.Unassigned, null));
                }
                if (m.Travel is not { } t) continue;
                var worker = state.Workers.Workers.Single(w => w.Id == m.Worker);
                var remaining = t.RemainingMicro - step;
                if (_router.Movement.CheckEdge(t.Source, t.Destination) != t.Edge)
                    throw new InvalidOperationException("Invalid canonical crossing.");
                var ledger = state.EdgeTravels.Replace(m.Worker, remaining == 0 ? null : t with { RemainingMicro = remaining });
                var destination = remaining == 0 ? t.Destination : t.Source;
                state = Assign(state, worker, destination,
                    _router.Movement.PresentationRegion(destination, worker.CellId),
                    remaining == 0
                        ? (Produces(m.Occupation) ? PhysicalWorkerActivity.Producing : PhysicalWorkerActivity.Other)
                        : PhysicalWorkerActivity.Traveling,
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
        // At an exact turn boundary, report readiness for base work only
        // when no finite task or frozen crossing remains. No extra slice.
        foreach (var q in state.WorkerOrders.Queues.Where(q => q.BaseActivity is not null &&
            q.Orders.Count == 0 && !state.EdgeTravels.Pending.Any(p => p.Worker == q.Worker)).ToArray())
        {
            var worker = state.Workers.Workers.Single(w => w.Id == q.Worker);
            var mode = BaseMode(worker, q.BaseActivity!);
            state = Assign(state, worker, mode.Node, worker.CellId, mode.Activity, mode.Occupation);
        }
        if (automaticStarts.Count > 0)
        {
            // Other workers' event boundaries can split a continuous idle
            // suffix. Allocate that suffix once, not once per unrelated event.
            foreach (var item in automaticStarts.OrderBy(x => x.Key.Value))
            {
                var suffix = slices.Where(s => s.Worker == item.Key && s.StartMicro >= item.Value)
                    .OrderBy(s => s.StartMicro).ToArray();
                if (suffix.Length == 0 || suffix[0].StartMicro != item.Value ||
                    suffix.Any(s => s.Node != suffix[0].Node || s.Activity != PhysicalWorkerActivity.Other ||
                        s.Occupation != PhysicalWorkerOccupation.Unassigned || s.ProjectId.HasValue || s.Crop.HasValue) ||
                    suffix.Aggregate(0UL, (sum, s) => sum + s.DurationMicro) != Whole - item.Value)
                    throw new InvalidOperationException("Automatic base must occupy only the stationary available suffix.");
                slices.RemoveAll(s => s.Worker == item.Key && s.StartMicro >= item.Value);
                slices.Add(suffix[0] with { DurationMicro = Whole - item.Value });
            }
            var intervals = automaticStarts.OrderBy(x => x.Key.Value)
                .Select(x => new FoundationBaseMarginalInterval(x.Key, x.Value)).ToArray();
            var plan = FoundationFrozenMarginalRevenueEstimator.Plan(opening, _router.Incidence,
                _router.Geography, _automaticEconomy!, slices, intervals, _agriculture!, _automaticOperations,
                context.Turn.Value - 1UL);
            slices = plan.Calendar.OrderBy(s => s.StartMicro).ThenBy(s => s.Worker.Value).ToList();
            foreach (var item in intervals)
            {
                var selected = slices.Single(s => s.Worker == item.Worker && s.StartMicro == item.StartMicro);
                var worker = state.Workers.Workers.Single(w => w.Id == item.Worker);
                if (worker.TacticalTileId != selected.Node ||
                    state.EdgeTravels.Pending.Any(t => t.Worker == item.Worker) ||
                    state.WorkerOrders.Queues.Single(q => q.Worker == item.Worker).Orders.Count != 0)
                    throw new InvalidOperationException("Automatic base cannot supersede a physical task.");
                state = Assign(state, worker, selected.Node, worker.CellId, selected.Activity, selected.Occupation);
            }
        }
        foreach (var p in state.Constructions.Projects.Where(p => p.IsComplete && !p.IsActivated).ToArray())
            state = state.ActivateCompletedConstruction(p.Id);
        return (state.AdvanceRevision(), slices.ToArray());
    }

    public (WorldState Next, FoundationPersistentWorkerTurnEvent Event) Record(WorldState opening, SimulationContext context)
    {
        var (next, slices) = Calculate(opening, context);
        return (next, new(EventId.ForSystemPhase(context.Turn, StablePhaseId, 2UL),
            Hasher.Compute(opening), Hasher.Compute(next), Array.AsReadOnly(slices)));
    }

    public WorldState Execute(WorldState state, ISimulationEvent simulationEvent, SimulationContext context)
    {
        if (simulationEvent is not FoundationPersistentWorkerTurnEvent ev ||
            ev.Id != EventId.ForSystemPhase(context.Turn, StablePhaseId, 2UL) || Hasher.Compute(state) != ev.Before)
            throw new InvalidOperationException("Stale persistent worker timeline.");
        var (next, actual) = Record(state, context);
        if (actual.After != ev.After || !actual.Slices.SequenceEqual(ev.Slices))
            throw new InvalidOperationException("Persistent worker timeline replay diverged.");
        return next;
    }
}
