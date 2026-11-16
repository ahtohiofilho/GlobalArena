using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Event executor used by BOTH turn resolution and deterministic journal replay.
/// Validates authoritative worker ownership, expected source and the exact
/// canonical Goldberg edge before producing a replacement WorldState.
/// </summary>
public sealed class PhysicalWorkerMoveEventExecutor : ISimulationEventExecutor
{
    private readonly RuntimeWorldBinding _binding;
    private readonly IReadOnlyDictionary<StrategicCellId,
        IReadOnlyDictionary<StrategicCellId, StrategicEdgeId>> _adjacency;

    public PhysicalWorkerMoveEventExecutor(WorldGenerationResult world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _binding = RuntimeWorldBinding.FromGeneratedWorld(world);
        var mutable = new Dictionary<StrategicCellId,
            Dictionary<StrategicCellId, StrategicEdgeId>>();
        foreach (var edge in world.StrategicTopology.Edges)
        {
            var a = edge.IncidentCellIds[0];
            var b = edge.IncidentCellIds[1];
            if (!mutable.TryGetValue(a, out var aNeighbors))
                mutable[a] = aNeighbors = new();
            if (!mutable.TryGetValue(b, out var bNeighbors))
                mutable[b] = bNeighbors = new();
            if (!aNeighbors.TryAdd(b, edge.Id) ||
                !bNeighbors.TryAdd(a, edge.Id))
                throw new InvalidOperationException("Duplicated canonical Goldberg adjacency.");
        }
        _adjacency = mutable.ToDictionary(
            item => item.Key,
            item => (IReadOnlyDictionary<StrategicCellId, StrategicEdgeId>)item.Value);
    }

    public StrategicEdgeId? CheckEdge(
        StrategicCellId source, StrategicCellId destination)
    {
        if (!_binding.Contains(source) || !_binding.Contains(destination))
            throw new ArgumentException("Worker move references unknown Goldberg tile.");
        if (source == destination)
            return null;
        if (_adjacency.TryGetValue(source, out var next) &&
            next.TryGetValue(destination, out var edge))
            return edge;
        throw new InvalidOperationException(
            "Physical worker move must cross one real Goldberg edge.");
    }

    public WorldState Execute(
        WorldState worldState, ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(worldState);
        if (simulationEvent is not PhysicalWorkerMovedEvent move)
            throw new ArgumentException(
                "Physical worker event executor accepts worker movement events only.");
        if (worldState.WorldBinding != _binding)
            throw new InvalidOperationException(
                "Physical worker journal cannot modify a different generated world.");
        if (!worldState.Workers.IsInitialized)
            throw new InvalidOperationException(
                "Cannot move physical workers before the authoritative census is initialized.");
        if (worldState.Workers.HasAuthoritativeTacticalLocations)
            throw new InvalidOperationException(
                "v9 strategic worker events cannot operate on v10 tactical positions.");
        if (move.Id.Turn != context.Turn)
            throw new InvalidOperationException("Worker move event has incorrect turn identity.");

        var edge = CheckEdge(move.Source, move.Destination);
        if (edge != move.TraversedEdge)
            throw new InvalidOperationException(
                "Worker move event traversed edge does not match canonical Goldberg graph.");

        var workers = worldState.Workers.Workers;
        var worker = workers.SingleOrDefault(item => item.Id == move.WorkerId)
            ?? throw new InvalidOperationException("Unknown physical worker ID in journal.");
        if (worker.Owner != move.Issuer || worker.CellId != move.Source)
            throw new InvalidOperationException(
                "Worker move owner or source no longer matches authoritative worker.");
        if (move.Destination == move.Source &&
            move.NextActivity == worker.Activity)
            throw new InvalidOperationException("No-op physical worker event rejected.");

        var updated = workers.Select(item => item.Id == worker.Id
            ? item.WithLocationAndActivity(move.Destination, move.NextActivity)
            : item).ToArray();

        return worldState.WithWorkers(new PhysicalWorkerRuntimeState(updated))
            .AdvanceRevision();
    }
}

/// <summary>
/// Optional worker movement stage in the *existing* phased turn engine.
/// Every requested movement has provenance in the command journal. A batch is
/// resolved on a private immutable snapshot; an invalid late order prevents
/// any partial authoritative publication. Other domain commands remain owned
/// by their respective phases.
/// </summary>
public sealed class PhysicalWorkerMovementTurnPhase : ITurnResolutionPhase
{
    public const string StablePhaseId = "foundation.worker-movement";
    private readonly RuntimeWorldBinding _binding;
    private readonly PhysicalWorkerMoveEventExecutor _executor;

    public string PhaseId => StablePhaseId;

    public PhysicalWorkerMovementTurnPhase(WorldGenerationResult world)
    {
        ArgumentNullException.ThrowIfNull(world);
        _binding = RuntimeWorldBinding.FromGeneratedWorld(world);
        _executor = new PhysicalWorkerMoveEventExecutor(world);
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);
        var commands = planningInput.Commands
            .OfType<PhysicalWorkerMoveCommand>()
            .OrderBy(x => x.Id.Sequence).ToArray();

        if (commands.Length == 0)
            return new TurnResolutionResult(stagedState,
                Array.Empty<ISimulationEvent>(),
                new SimulationEventLog(Array.Empty<SimulationEventLogEntry>()));
        if (!stagedState.Workers.IsInitialized ||
            stagedState.Workers.HasAuthoritativeTacticalLocations ||
            stagedState.WorldBinding != _binding)
            throw new InvalidOperationException(
                "Worker phase requires an initialized census bound to the same world.");
        if (commands.Any(c => c.Id.Turn != planningInput.Context.Turn))
            throw new InvalidOperationException(
                "Worker orders must belong to the resolving authoritative turn.");
        if (commands.Select(c => c.Id).Distinct().Count() != commands.Length)
            throw new InvalidOperationException("Duplicate physical worker command ID.");
        if (commands.Select(c => c.WorkerId).Distinct().Count() != commands.Length)
            throw new InvalidOperationException(
                "A worker can receive only one movement/activity order per turn.");

        var staged = stagedState;
        var events = new List<ISimulationEvent>(commands.Length);
        var entries = new List<SimulationEventLogEntry>(commands.Length);
        foreach (var command in commands)
        {
            var worker = staged.Workers.Workers.SingleOrDefault(
                item => item.Id == command.WorkerId)
                ?? throw new InvalidOperationException(
                    "Cannot order a nonexistent physical worker.");
            if (worker.Owner != command.Issuer)
                throw new InvalidOperationException(
                    "A civilization may move only its own physical worker.");
            var edge = _executor.CheckEdge(worker.CellId, command.Destination);
            var nextActivity = command.NextActivity ??
                (edge.HasValue ? PhysicalWorkerActivity.Traveling : worker.Activity);
            if (edge is null && nextActivity == worker.Activity)
                throw new InvalidOperationException(
                    "Worker command must move or change activity.");

            var ev = new PhysicalWorkerMovedEvent(
                new EventId(command.Id, 1UL), command.Issuer, worker.Id,
                worker.CellId, command.Destination, edge, nextActivity);
            staged = _executor.Execute(staged, ev, planningInput.Context);
            events.Add(ev);
            entries.Add(new SimulationEventLogEntry(
                checked((ulong)entries.Count + 1UL),
                ev, wasEligible: true, wasExecuted: true));
        }
        return new TurnResolutionResult(staged, events,
            new SimulationEventLog(entries));
    }
}
