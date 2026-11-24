using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationConstructionWorkCommand(CommandId Id, CivilizationId Issuer,
    PhysicalWorkerId WorkerId, ulong ProjectId) : ISimulationCommand;

public sealed record FoundationConstructionWorkBatchEvent(EventId Id, ulong ExpectedRevision,
    IReadOnlyList<FoundationConstructionWorkCommand> Commands,
    IReadOnlyList<FoundationConstructionWorkerReceipt> Receipts) : ISimulationEvent;

public sealed class FoundationConstructionCollectiveTurnPhase : ITurnResolutionPhase
{
    public const string StablePhaseId = "foundation.construction-collective-v19";
    public string PhaseId => StablePhaseId;
    private readonly FoundationConstructionFundingExecutor _funding;
    private readonly PhysicalTacticalIncidenceMap _incidence;
    private readonly FoundationFineFirstPhysicalGeography _geography;

    public FoundationConstructionCollectiveTurnPhase(FoundationConstructionFundingExecutor funding,
        PhysicalTacticalIncidenceMap incidence, FoundationFineFirstPhysicalGeography geography)
    {
        _funding = funding ?? throw new ArgumentNullException(nameof(funding));
        _incidence = incidence ?? throw new ArgumentNullException(nameof(incidence));
        _geography = geography ?? throw new ArgumentNullException(nameof(geography));
    }

    private FoundationConstructionWorkResult Calculate(WorldState state,
        IReadOnlyList<FoundationConstructionWorkCommand> commands, SimulationContext context)
    {
        var people = state.Workers.Workers.ToDictionary(w => w.Id);
        if (commands.Any(c => c.Id.Turn != context.Turn || c.Id.Sequence == 0 ||
            !people.TryGetValue(c.WorkerId, out var worker) || worker.Owner != c.Issuer) ||
            commands.Select(c => c.Id).Distinct().Count() != commands.Count)
            throw new InvalidOperationException("Invalid construction command authority.");
        return FoundationConstructionWorkerWork.Apply(state, _incidence, _geography,
            commands.Select(c => new FoundationConstructionWorkerAssignment(c.WorkerId, c.ProjectId)).ToArray());
    }

    private static WorldState Commission(WorldState state)
    {
        foreach (var id in state.Constructions.Projects.Where(p => p.IsComplete && !p.IsActivated)
            .Select(p => p.Id).ToArray())
            state = state.ActivateCompletedConstruction(id);
        return state;
    }

    public TurnResolutionResult Resolve(TurnResolutionInput input, WorldState stagedState)
    {
        if (!stagedState.Constructions.IsInitialized)
            throw new InvalidOperationException("Construction ledger must be enabled.");
        var commands = input.Commands.OfType<FoundationConstructionWorkCommand>()
            .OrderBy(c => c.Id.Sequence).ToArray();
        var moving = input.Commands.OfType<PhysicalTacticalWorkerMoveCommand>().Select(c => c.WorkerId)
            .Concat(input.Commands.OfType<FoundationEdgeTravelTurnCommand>().Select(c => c.WorkerId)).ToHashSet();
        if (commands.Any(c => moving.Contains(c.WorkerId)))
            throw new InvalidOperationException("Construction cannot share a worker turn with movement or reassignment.");
        var funded = new FoundationConstructionFundingTurnPhase(_funding).Resolve(input, stagedState);
        var state = funded.ResultingWorldState;
        var events = funded.Events.ToList();
        if (commands.Length > 0)
        {
            var work = Calculate(state, commands, input.Context);
            var ev = new FoundationConstructionWorkBatchEvent(
                EventId.ForSystemPhase(input.Context.Turn, StablePhaseId, 1UL), state.Revision,
                Array.AsReadOnly(commands), work.Workers);
            state = Commission(work.Snapshot).AdvanceRevision();
            events.Add(ev);
        }
        var log = new SimulationEventLog(events.Select((e, i) =>
            new SimulationEventLogEntry(checked((ulong)i + 1UL), e, true, true)).ToArray());
        return new TurnResolutionResult(state, events, log);
    }

    // Fresh executor per complete turn journal. No per-worker replay calls
    // can allocate the same worker twice within the batch.
    public ISimulationEventExecutor CreateReplayExecutor(ISimulationEventExecutor fallback) =>
        new Replay(this, fallback);

    private sealed class Replay : ISimulationEventExecutor
    {
        private readonly FoundationConstructionCollectiveTurnPhase _phase;
        private readonly ISimulationEventExecutor _fallback;
        private readonly HashSet<PhysicalWorkerId> _otherWorkers = new();
        private bool _workSeen;
        public Replay(FoundationConstructionCollectiveTurnPhase phase, ISimulationEventExecutor fallback)
        { _phase = phase; _fallback = fallback; }

        public WorldState Execute(WorldState state, ISimulationEvent simulationEvent, SimulationContext context)
        {
            if (_workSeen)
                throw new InvalidOperationException("Construction batch must be last in this journal.");
            if (simulationEvent is FoundationConstructionFundedEvent)
                return _phase._funding.Execute(state, simulationEvent, context);
            if (simulationEvent is FoundationConstructionWorkBatchEvent ev)
            {
                if (ev.Id != EventId.ForSystemPhase(context.Turn, StablePhaseId, 1UL) ||
                    ev.ExpectedRevision != state.Revision || ev.Commands.Count == 0 ||
                    ev.Commands.Any(c => _otherWorkers.Contains(c.WorkerId)))
                    throw new InvalidOperationException("Invalid or competing construction batch.");
                var calculated = _phase.Calculate(state, ev.Commands, context);
                if (!calculated.Workers.SequenceEqual(ev.Receipts))
                    throw new InvalidOperationException("Construction work receipt mismatch.");
                _workSeen = true;
                return Commission(calculated.Snapshot).AdvanceRevision();
            }
            if (simulationEvent is PhysicalTacticalWorkerMovedEvent move) _otherWorkers.Add(move.WorkerId);
            if (simulationEvent is FoundationEdgeTravelTurnEvent travel) _otherWorkers.Add(travel.WorkerId);
            return _fallback.Execute(state, simulationEvent, context);
        }
    }
}
