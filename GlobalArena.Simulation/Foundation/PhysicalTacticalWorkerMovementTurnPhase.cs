using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>v10 command. Every destination is a qualified physical tile in
/// the same fine Goldberg topology; coarse strategic cell IDs are not orders.</summary>
public sealed record PhysicalTacticalWorkerMoveCommand : ISimulationCommand
{
    public CommandId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public PhysicalTacticalTileId Destination { get; }
    public PhysicalWorkerActivity? NextActivity { get; }

    public PhysicalTacticalWorkerMoveCommand(
        CommandId id, CivilizationId issuer, PhysicalWorkerId workerId,
        PhysicalTacticalTileId destination, PhysicalWorkerActivity? nextActivity = null)
    {
        if (id.Turn.Value == 0UL || id.Sequence == 0UL ||
            !issuer.IsValid || !workerId.IsValid || !destination.IsValid ||
            (nextActivity.HasValue && !Enum.IsDefined(nextActivity.Value)))
            throw new ArgumentException("Invalid v10 physical tactical worker order.");
        Id = id;
        Issuer = issuer;
        WorkerId = workerId;
        Destination = destination;
        NextActivity = nextActivity;
    }
}

/// <summary>Immutable replay evidence of the actual fine crossing. The
/// coarse region is a presentation projection, NOT a movement endpoint.</summary>
public sealed record PhysicalTacticalWorkerMovedEvent : ISimulationEvent
{
    public EventId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public PhysicalTacticalTileId Source { get; }
    public PhysicalTacticalTileId Destination { get; }
    public PhysicalTacticalEdgeId? TraversedEdge { get; }
    public StrategicCellId ResultingPresentationRegion { get; }
    public PhysicalWorkerActivity NextActivity { get; }

    public PhysicalTacticalWorkerMovedEvent(
        EventId id, CivilizationId issuer, PhysicalWorkerId workerId,
        PhysicalTacticalTileId source, PhysicalTacticalTileId destination,
        PhysicalTacticalEdgeId? traversedEdge,
        StrategicCellId resultingPresentationRegion,
        PhysicalWorkerActivity nextActivity)
    {
        if (id.OriginKind != EventOriginKind.Command ||
            id.Turn.Value == 0UL || id.Sequence == 0UL ||
            id.OriginCommandId.Sequence == 0UL || !issuer.IsValid ||
            !workerId.IsValid || !source.IsValid || !destination.IsValid ||
            source.FineGoldbergParameters != destination.FineGoldbergParameters ||
            !resultingPresentationRegion.IsValid || !Enum.IsDefined(nextActivity) ||
            (source == destination && traversedEdge.HasValue) ||
            (source != destination &&
                (!traversedEdge.HasValue || !traversedEdge.Value.IsValid ||
                 traversedEdge.Value.FineGoldbergParameters !=
                    source.FineGoldbergParameters)))
            throw new ArgumentException("Invalid v10 physical tactical worker journal event.");
        Id = id;
        Issuer = issuer;
        WorkerId = workerId;
        Source = source;
        Destination = destination;
        TraversedEdge = traversedEdge;
        ResultingPresentationRegion = resultingPresentationRegion;
        NextActivity = nextActivity;
    }
}

/// <summary>The same executor is used in private resolution and journal replay.
/// No tile center is inferred and no coarse movement fallback is possible.</summary>
public sealed class PhysicalTacticalWorkerMoveEventExecutor : ISimulationEventExecutor
{
    private readonly RuntimeWorldBinding _binding;
    private readonly PhysicalTacticalIncidenceMap _map;
    private readonly Dictionary<StrategicCellId,
        Dictionary<StrategicCellId, PhysicalTacticalEdgeId>> _edges = new();

    public PhysicalTacticalWorkerMoveEventExecutor(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap map)
    {
        ArgumentNullException.ThrowIfNull(world);
        _map = map ?? throw new ArgumentNullException(nameof(map));
        if (world.Request.StrategicParameters != map.Refinement.CoarseParameters)
            throw new InvalidOperationException(
                "Fine graph must refine the actual bound canonical generated world.");
        _binding = RuntimeWorldBinding.FromGeneratedWorld(world);
        foreach (var edge in map.FineTopology.Edges)
        {
            var a = edge.IncidentCellIds[0];
            var b = edge.IncidentCellIds[1];
            var qualified = new PhysicalTacticalEdgeId(
                map.Refinement.FineParameters, edge.Id);
            if (!_edges.TryGetValue(a, out var aEdges))
                _edges[a] = aEdges = new();
            if (!_edges.TryGetValue(b, out var bEdges))
                _edges[b] = bEdges = new();
            if (!aEdges.TryAdd(b, qualified) || !bEdges.TryAdd(a, qualified))
                throw new InvalidOperationException("Duplicate fine graph edge.");
        }
    }

    public PhysicalTacticalEdgeId? CheckEdge(
        PhysicalTacticalTileId source, PhysicalTacticalTileId target)
    {
        if (!source.IsValid || !target.IsValid ||
            source.FineGoldbergParameters != _map.Refinement.FineParameters ||
            target.FineGoldbergParameters != _map.Refinement.FineParameters)
            throw new ArgumentException("Tactical endpoint is not in the bound fine graph.");
        if (source == target) return null;
        if (_edges.TryGetValue(source.FineStrategicCellId, out var neighbors) &&
            neighbors.TryGetValue(target.FineStrategicCellId, out var edge))
            return edge;
        throw new InvalidOperationException(
            "Worker cannot traverse more than one physical fine edge per move.");
    }

    public StrategicCellId PresentationRegion(
        PhysicalTacticalTileId destination, StrategicCellId previous)
    {
        if (!destination.IsValid ||
            destination.FineGoldbergParameters != _map.Refinement.FineParameters)
            throw new ArgumentException("Unknown tactical destination.");
        var incident = _map.TileIncidences[
            checked((int)destination.FineStrategicCellId.Value - 1)]
            .IncidentCoarseCellIds;
        // Prefer the previous region while crossing a shared boundary;
        // otherwise pick the lowest canonical incident region.
        return incident.Contains(previous) ? previous : incident[0];
    }

    public WorldState Execute(
        WorldState state, ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (simulationEvent is not PhysicalTacticalWorkerMovedEvent ev)
            throw new ArgumentException("v10 executor only processes tactical events.");
        if (state.WorldBinding != _binding ||
            !state.Workers.HasAuthoritativeTacticalLocations)
            throw new InvalidOperationException(
                "v10 tactical movement requires the correct bound fine-worker census.");
        if (ev.Id.Turn != context.Turn)
            throw new InvalidOperationException("Mismatched tactical journal turn.");

        var edge = CheckEdge(ev.Source, ev.Destination);
        if (edge != ev.TraversedEdge)
            throw new InvalidOperationException("Forged tactical traversed edge.");
        var worker = state.Workers.Workers.SingleOrDefault(w => w.Id == ev.WorkerId)
            ?? throw new InvalidOperationException("Unknown fine worker identity.");
        if (worker.Owner != ev.Issuer || worker.TacticalTileId != ev.Source)
            throw new InvalidOperationException(
                "Stale or foreign tactical worker origin.");
        if (edge is null && ev.NextActivity == worker.Activity)
            throw new InvalidOperationException("No-op tactical event rejected.");
        if (ev.ResultingPresentationRegion !=
            PresentationRegion(ev.Destination, worker.CellId))
            throw new InvalidOperationException(
                "Forged coarse presentation region on fine worker event.");

        var newWorkers = state.Workers.Workers.Select(w => w.Id == ev.WorkerId
            ? w.WithTacticalLocationAndActivity(
                ev.Destination, ev.ResultingPresentationRegion, ev.NextActivity)
            : w).ToArray();
        return state.WithWorkers(new PhysicalWorkerRuntimeState(
            newWorkers, tacticalLocationsAuthoritative: true)).AdvanceRevision();
    }
}

public sealed class PhysicalTacticalWorkerMovementTurnPhase : ITurnResolutionPhase
{
    public const string StablePhaseId = "foundation.tactical-worker-movement-v10";
    private readonly PhysicalTacticalWorkerMoveEventExecutor _executor;
    public string PhaseId => StablePhaseId;

    public PhysicalTacticalWorkerMovementTurnPhase(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap map)
    {
        _executor = new(world, map);
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);
        var commands = planningInput.Commands
            .OfType<PhysicalTacticalWorkerMoveCommand>()
            .OrderBy(c => c.Id.Sequence).ToArray();
        if (commands.Length == 0)
            return new TurnResolutionResult(stagedState,
                Array.Empty<ISimulationEvent>(),
                new SimulationEventLog(Array.Empty<SimulationEventLogEntry>()));
        if (!stagedState.Workers.HasAuthoritativeTacticalLocations)
            throw new InvalidOperationException("v10 commands cannot operate on v9 workers.");
        if (commands.Any(c => c.Id.Turn != planningInput.Context.Turn) ||
            commands.Select(c => c.Id).Distinct().Count() != commands.Length ||
            commands.Select(c => c.WorkerId).Distinct().Count() != commands.Length)
            throw new InvalidOperationException(
                "Tactical orders need correct turn, unique command and one order per worker.");
        var staged = stagedState;
        var events = new List<ISimulationEvent>(commands.Length);
        var entries = new List<SimulationEventLogEntry>(commands.Length);
        foreach (var command in commands)
        {
            var worker = staged.Workers.Workers.SingleOrDefault(
                w => w.Id == command.WorkerId)
                ?? throw new InvalidOperationException("Unknown tactical worker.");
            if (worker.Owner != command.Issuer || !worker.TacticalTileId.HasValue)
                throw new InvalidOperationException("Foreign or non-tactical worker.");
            var from = worker.TacticalTileId.Value;
            var edge = _executor.CheckEdge(from, command.Destination);
            var activity = command.NextActivity ??
                (edge.HasValue ? PhysicalWorkerActivity.Traveling : worker.Activity);
            if (edge is null && activity == worker.Activity)
                throw new InvalidOperationException("No-op tactical order.");
            var presentation = _executor.PresentationRegion(
                command.Destination, worker.CellId);
            var ev = new PhysicalTacticalWorkerMovedEvent(
                new EventId(command.Id, 1UL), command.Issuer, command.WorkerId,
                from, command.Destination, edge, presentation, activity);
            staged = _executor.Execute(staged, ev, planningInput.Context);
            events.Add(ev);
            entries.Add(new SimulationEventLogEntry(
                checked((ulong)entries.Count + 1UL),
                ev, wasEligible: true, wasExecuted: true));
        }
        return new TurnResolutionResult(staged, events, new SimulationEventLog(entries));
    }
}
