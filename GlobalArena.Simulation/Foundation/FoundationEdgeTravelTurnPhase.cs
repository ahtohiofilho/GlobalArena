using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Explicitly opt-in journal command. Continuations are generated from the
/// trusted pending ledger, never issued as a fresh movement by a player.
/// </summary>
public sealed record FoundationEdgeTravelTurnCommand : ISimulationCommand
{
    public CommandId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public PhysicalTacticalTileId Destination { get; }
    public bool IsStart { get; }

    public FoundationEdgeTravelTurnCommand(
        CommandId id, CivilizationId issuer, PhysicalWorkerId worker,
        PhysicalTacticalTileId destination, bool isStart)
    {
        if (id.Turn.Value == 0UL || id.Sequence == 0UL ||
            !issuer.IsValid || !worker.IsValid || !destination.IsValid)
            throw new ArgumentException("Invalid physical edge travel command.");
        Id = id;
        Issuer = issuer;
        WorkerId = worker;
        Destination = destination;
        IsStart = isStart;
    }
}

public sealed record FoundationEdgeTravelTurnEvent : ISimulationEvent
{
    public EventId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalWorkerId WorkerId { get; }
    public PhysicalTacticalTileId Destination { get; }
    public FoundationEdgeTravelTransitionReceipt Receipt { get; }

    public FoundationEdgeTravelTurnEvent(
        EventId id, CivilizationId issuer, PhysicalWorkerId worker,
        PhysicalTacticalTileId destination,
        FoundationEdgeTravelTransitionReceipt receipt)
    {
        if (id.OriginKind != EventOriginKind.Command ||
            id.Turn.Value == 0UL || id.Sequence == 0UL ||
            !issuer.IsValid || !worker.IsValid || !destination.IsValid ||
            receipt is null || receipt.Worker != worker)
            throw new ArgumentException("Invalid persisted travel journal event.");
        Id = id;
        Issuer = issuer;
        WorkerId = worker;
        Destination = destination;
        Receipt = receipt;
    }
}

/// <summary>
/// The same physical travel executor runs during resolution and replay.
/// A tampered event, wrong owner or stale world hash is rejected.
/// </summary>
public sealed class FoundationEdgeTravelEventExecutor : ISimulationEventExecutor
{
    private readonly PhysicalTacticalWorkerMoveEventExecutor _movement;
    private readonly FoundationGroundEdgeTravelTimePolicy _policy;

    public FoundationEdgeTravelEventExecutor(
        PhysicalTacticalWorkerMoveEventExecutor movement,
        FoundationGroundEdgeTravelTimePolicy policy)
    {
        _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _policy.RequireValid();
    }

    public WorldState Execute(WorldState state, ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (simulationEvent is not FoundationEdgeTravelTurnEvent ev ||
            ev.Id.Turn != context.Turn)
            throw new InvalidOperationException("Wrong physical travel journal event/turn.");
        var worker = state.Workers.Workers.SingleOrDefault(w => w.Id == ev.WorkerId)
            ?? throw new InvalidOperationException("Missing physical worker.");
        if (worker.Owner != ev.Issuer)
            throw new InvalidOperationException("Foreign worker journal event.");
        if (!ev.Receipt.Started &&
            !state.EdgeTravels.Pending.Any(p =>
                p.Worker == ev.WorkerId && p.Owner == ev.Issuer &&
                p.Destination == ev.Destination))
            throw new InvalidOperationException("Forged or stale continuation.");
        return ev.Receipt.Started
            ? FoundationPersistedEdgeTravelTransitions.ReplayStartAndAdvance(
                state, ev.Receipt, ev.Destination, _movement, _policy)
            : FoundationPersistedEdgeTravelTransitions.ReplayAdvance(
                state, ev.Receipt, _movement);
    }
}

public sealed class FoundationEdgeTravelTurnPhase : ITurnResolutionPhase
{
    public const string StablePhaseId = "foundation.persisted-physical-edge-travel-v17";
    public string PhaseId => StablePhaseId;
    private readonly FoundationEdgeTravelEventExecutor _executor;
    private readonly PhysicalTacticalWorkerMoveEventExecutor _movement;
    private readonly FoundationGroundEdgeTravelTimePolicy _policy;

    public FoundationEdgeTravelTurnPhase(
        PhysicalTacticalWorkerMoveEventExecutor movement,
        FoundationGroundEdgeTravelTimePolicy policy)
    {
        _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _executor = new FoundationEdgeTravelEventExecutor(_movement, _policy);
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);
        var commands = planningInput.Commands
            .OfType<FoundationEdgeTravelTurnCommand>()
            .OrderBy(c => c.Id.Sequence).ToArray();
        if (commands.Length == 0)
            return new TurnResolutionResult(stagedState,
                Array.Empty<ISimulationEvent>(),
                new SimulationEventLog(Array.Empty<SimulationEventLogEntry>()));
        if (!stagedState.EdgeTravels.IsInitialized ||
            commands.Any(c => c.Id.Turn != planningInput.Context.Turn) ||
            commands.Select(c => c.Id).Distinct().Count() != commands.Length ||
            commands.Select(c => c.WorkerId).Distinct().Count() != commands.Length)
            throw new InvalidOperationException(
                "Travel turn requires opt-in ledger and unique worker commands.");
        var state = stagedState;
        var events = new List<ISimulationEvent>(commands.Length);
        var entries = new List<SimulationEventLogEntry>(commands.Length);
        foreach (var cmd in commands)
        {
            var person = state.Workers.Workers.SingleOrDefault(w => w.Id == cmd.WorkerId);
            if (person is null || person.Owner != cmd.Issuer)
                throw new InvalidOperationException("Foreign travel command.");
            var (next, receipt) = cmd.IsStart
                ? FoundationPersistedEdgeTravelTransitions.StartAndAdvance(
                    state, cmd.WorkerId, cmd.Destination, _movement, _policy)
                : FoundationPersistedEdgeTravelTransitions.Advance(
                    state, cmd.WorkerId, _movement);
            var ev = new FoundationEdgeTravelTurnEvent(
                new EventId(cmd.Id, 1UL), cmd.Issuer,
                cmd.WorkerId, cmd.Destination, receipt);
            // Verify the resolved event by the independent replay path.
            var reproduced = _executor.Execute(state, ev, planningInput.Context);
            var hash = new CanonicalWorldStateHasher();
            if (hash.Compute(reproduced) != hash.Compute(next))
                throw new InvalidOperationException("R167 journal cannot reproduce the transition.");
            state = next;
            events.Add(ev);
            entries.Add(new SimulationEventLogEntry(
                checked((ulong)entries.Count + 1UL),
                ev, wasEligible: true, wasExecuted: true));
        }
        return new TurnResolutionResult(state, events, new SimulationEventLog(entries));
    }
}

/// <summary>Opt-in replay multiplexer; legacy movement/economy remain unchanged.</summary>
public sealed class FoundationEdgeTravelCompositeReplayExecutor : ISimulationEventExecutor
{
    private readonly ISimulationEventExecutor _legacy;
    private readonly FoundationEdgeTravelEventExecutor _travel;
    public FoundationEdgeTravelCompositeReplayExecutor(
        ISimulationEventExecutor legacy,
        FoundationEdgeTravelEventExecutor travel)
    {
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        _travel = travel ?? throw new ArgumentNullException(nameof(travel));
    }
    public WorldState Execute(WorldState state, ISimulationEvent ev,
        SimulationContext context) =>
        ev is FoundationEdgeTravelTurnEvent
            ? _travel.Execute(state, ev, context)
            : _legacy.Execute(state, ev, context);
}
