using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// An accepted collective-turn order from one real physical worker to ONE
/// natural fine edge. The client cannot choose the amount of work or a price.
/// </summary>
public sealed record PhysicalTransportConstructionWorkCommand : ISimulationCommand
{
    public CommandId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public PhysicalTacticalEdgeId Edge { get; }

    public PhysicalTransportConstructionWorkCommand(
        CommandId id, CivilizationId issuer, PhysicalWorkerId workerId,
        PhysicalTacticalEdgeId edge)
    {
        if (id.Turn.Value == 0UL || id.Sequence == 0UL ||
            !issuer.IsValid || !workerId.IsValid || !edge.IsValid)
            throw new ArgumentException("Invalid physical construction command.");
        Id = id;
        Issuer = issuer;
        WorkerId = workerId;
        Edge = edge;
    }
}

/// <summary>
/// Replayable proof of an accepted physical effort. Both the worker and the
/// crossing are actual world identities; work cannot be supplied by an
/// external numeric order. No ownership or road is granted on labor sufficiency.
/// </summary>
public sealed record PhysicalTransportConstructionWorkedEvent : ISimulationEvent
{
    public EventId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public PhysicalTacticalTileId WorkerTile { get; }
    public PhysicalTacticalEdgeId Edge { get; }
    public ulong CreditedLaborMicro { get; }
    public ulong ExpectedWorldRevision { get; }

    public PhysicalTransportConstructionWorkedEvent(
        EventId id, CivilizationId issuer, PhysicalWorkerId workerId,
        PhysicalTacticalTileId workerTile, PhysicalTacticalEdgeId edge,
        ulong creditedLaborMicro, ulong expectedWorldRevision)
    {
        if (id.OriginKind != EventOriginKind.Command ||
            id.Turn.Value == 0UL || id.Sequence == 0UL ||
            id.OriginCommandId.Sequence == 0UL ||
            !issuer.IsValid || !workerId.IsValid ||
            !workerTile.IsValid || !edge.IsValid ||
            workerTile.FineGoldbergParameters != edge.FineGoldbergParameters ||
            creditedLaborMicro == 0UL)
            throw new ArgumentException(
                "A construction receipt needs a real command, positive " +
                "work and qualified physical identities.");
        Id = id;
        Issuer = issuer;
        WorkerId = workerId;
        WorkerTile = workerTile;
        Edge = edge;
        CreditedLaborMicro = creditedLaborMicro;
        ExpectedWorldRevision = expectedWorldRevision;
    }
}

/// <summary>
/// Used BOTH by the private resolution phase AND by independent journal
/// replay. No implicit worker activity, construction material, wealth or
/// infrastructure transfer is created. Every credited event advances the
/// immutable authoritative world revision exactly once.
/// </summary>
public sealed class PhysicalTransportConstructionEventExecutor
    : ISimulationEventExecutor
{
    private readonly RuntimeWorldBinding _binding;
    private readonly IReadOnlyDictionary<PhysicalTacticalEdgeId,
        (PhysicalTacticalTileId First, PhysicalTacticalTileId Second)> _edges;
    private readonly FoundationPhysicalConstructionLaborPolicy _policy;

    public PhysicalTransportConstructionEventExecutor(
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationPhysicalConstructionLaborPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        if (incidence.Refinement.CoarseParameters !=
            generatedWorld.Request.StrategicParameters)
            throw new InvalidOperationException(
                "Physical construction requires the bound fine graph.");
        _binding = RuntimeWorldBinding.FromGeneratedWorld(generatedWorld);
        var fine = incidence.Refinement.FineParameters;
        _edges = incidence.FineTopology.Edges.ToDictionary(
            edge => new PhysicalTacticalEdgeId(fine, edge.Id),
            edge => (
                new PhysicalTacticalTileId(fine, edge.IncidentCellIds[0]),
                new PhysicalTacticalTileId(fine, edge.IncidentCellIds[1])));
    }

    public WorldState Execute(
        WorldState state, ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (simulationEvent is not PhysicalTransportConstructionWorkedEvent ev)
            throw new ArgumentException(
                "Physical construction executor only handles work events.");
        if (ev.Id.Turn != context.Turn ||
            state.WorldBinding != _binding ||
            !state.Workers.HasAuthoritativeOccupations ||
            !state.Transport.HasNaturalConstructionLabor ||
            state.Revision != ev.ExpectedWorldRevision)
            throw new InvalidOperationException(
                "Stale, foreign or unbound construction journal event.");

        var worker = state.Workers.FindWorker(ev.WorkerId)
            ?? throw new InvalidOperationException(
                "Construction journal references a nonexistent worker.");
        if (worker.Owner != ev.Issuer ||
            worker.Activity != PhysicalWorkerActivity.Other ||
            worker.TacticalTileId != ev.WorkerTile ||
            !_edges.TryGetValue(ev.Edge, out var endpoints) ||
            (ev.WorkerTile != endpoints.First &&
             ev.WorkerTile != endpoints.Second))
            throw new InvalidOperationException(
                "Construction journal must identify an eligible real " +
                "owner-controlled worker at a real edge endpoint.");

        if (!state.Transport.TryGetEdge(ev.Edge, out var crossing) ||
            crossing is null)
            throw new InvalidOperationException(
                "Construction crossing absent from authoritative transport.");
        if (crossing.OwnershipKind !=
            PhysicalTransportOwnershipKind.NoInfrastructure ||
            crossing.NaturalConstructionLaborMicro is not { } required)
            throw new InvalidOperationException(
                "Cannot credit construction to built or unknown infrastructure.");

        ulong previous = crossing.CommittedConstructionLaborMicro ?? 0UL;
        if (previous >= required)
            throw new InvalidOperationException(
                "Construction project already has sufficient physical labor.");
        ulong exact = Math.Min(
            _policy.LaborMicroPerWorkerPerTurn, required - previous);
        if (ev.CreditedLaborMicro != exact)
            throw new InvalidOperationException(
                "Forged or duplicated construction labor in journal event.");

        return state.RecordJournaledConstructionWork(
            ev.Edge, ev.CreditedLaborMicro).AdvanceRevision();
    }
}

/// <summary>
/// The separately ordered construction phase runs only after the tactical
/// worker movement/occupation phase. It accepts commands only from the
/// all-ready collective batch, rejects movement + construction for the same
/// worker, and guarantees a deterministic atomic candidate without accepting
/// fabricated work at the event boundary.
/// </summary>
public sealed class PhysicalTransportConstructionTurnPhase
    : ITurnResolutionPhase
{
    public const string StablePhaseId =
        "foundation.real-worker-construction-v14";

    private readonly WorldGenerationResult _world;
    private readonly PhysicalTacticalIncidenceMap _incidence;
    private readonly FoundationPhysicalConstructionLaborPolicy _policy;
    private readonly PhysicalTransportConstructionEventExecutor _executor;
    public string PhaseId => StablePhaseId;

    public PhysicalTransportConstructionTurnPhase(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence,
        FoundationPhysicalConstructionLaborPolicy policy)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _incidence = incidence ?? throw new ArgumentNullException(nameof(incidence));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _executor = new PhysicalTransportConstructionEventExecutor(
            world, incidence, policy);
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput input, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(stagedState);
        var commands = input.Commands
            .OfType<PhysicalTransportConstructionWorkCommand>()
            .OrderBy(c => c.Edge.FineStrategicEdgeId.Value)
            .ThenBy(c => c.WorkerId.Value)
            .ToArray();
        if (commands.Length == 0)
            return new TurnResolutionResult(stagedState,
                Array.Empty<ISimulationEvent>(),
                new SimulationEventLog(Array.Empty<SimulationEventLogEntry>()));

        var moving = input.Commands
            .OfType<PhysicalTacticalWorkerMoveCommand>()
            .Select(c => c.WorkerId).ToHashSet();
        if (commands.Any(c => c.Id.Turn != input.Context.Turn ||
                moving.Contains(c.WorkerId)) ||
            commands.Select(c => c.Id).Distinct().Count() != commands.Length ||
            commands.Select(c => c.WorkerId).Distinct().Count() != commands.Length)
            throw new InvalidOperationException(
                "One worker gets at most one canonical order per collective turn.");

        var assignments = commands.Select(c =>
            new FoundationPhysicalConstructionWorkAssignment(
                c.Issuer, c.WorkerId, c.Edge)).ToArray();
        var candidate = FoundationPhysicalConstructionWorkPlanner.PlanCandidate(
            stagedState, _world, _incidence, _policy, assignments);
        if (candidate.Workers.Count != commands.Length ||
            candidate.Workers.Any(w => w.ContributedLaborMicro == 0UL))
            throw new InvalidOperationException(
                "A committed work order must have positive remaining " +
                "physical labor; extra workers must not create no-op events.");

        var byWorker = commands.ToDictionary(c => c.WorkerId);
        var staged = stagedState;
        var events = new List<ISimulationEvent>(commands.Length);
        var log = new List<SimulationEventLogEntry>(commands.Length);
        foreach (var contribution in candidate.Workers)
        {
            var command = byWorker[contribution.WorkerId];
            var ev = new PhysicalTransportConstructionWorkedEvent(
                new EventId(command.Id, 1UL), command.Issuer,
                command.WorkerId, contribution.WorkerTile,
                command.Edge, contribution.ContributedLaborMicro,
                staged.Revision);
            staged = _executor.Execute(staged, ev, input.Context);
            events.Add(ev);
            log.Add(new SimulationEventLogEntry(
                checked((ulong)log.Count + 1UL), ev,
                wasEligible: true, wasExecuted: true));
        }
        return new TurnResolutionResult(
            staged, events, new SimulationEventLog(log));
    }
}

/// <summary>
/// Replays two domain event kinds without bypassing either domain validator.
/// It is registered only when construction is explicitly enabled in the host.
/// </summary>
public sealed class FoundationTacticalConstructionReplayExecutor
    : ISimulationEventExecutor
{
    private readonly PhysicalTacticalWorkerMoveEventExecutor _movement;
    private readonly PhysicalTransportConstructionEventExecutor _construction;

    public FoundationTacticalConstructionReplayExecutor(
        PhysicalTacticalWorkerMoveEventExecutor movement,
        PhysicalTransportConstructionEventExecutor construction)
    {
        _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        _construction = construction ??
            throw new ArgumentNullException(nameof(construction));
    }

    public WorldState Execute(WorldState state,
        ISimulationEvent simulationEvent, SimulationContext context) =>
        simulationEvent switch
        {
            PhysicalTacticalWorkerMovedEvent => _movement.Execute(
                state, simulationEvent, context),
            PhysicalTransportConstructionWorkedEvent => _construction.Execute(
                state, simulationEvent, context),
            _ => throw new InvalidOperationException(
                "Unexpected event type in the mixed tactical construction journal.")
        };
}
