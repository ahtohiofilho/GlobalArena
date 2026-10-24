using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhasedTurnResolutionEngineTests
{
    private sealed record TestEvent(EventId Id) : ISimulationEvent;

    private sealed class Phase(string id,
        Func<TurnResolutionInput, WorldState, TurnResolutionResult> run)
        : ITurnResolutionPhase
    {
        public string PhaseId { get; set; } = id;
        public int Calls { get; private set; }
        public WorldState? SeenPlanning { get; private set; }
        public WorldState? SeenStaged { get; private set; }
        public TurnResolutionResult Resolve(TurnResolutionInput input, WorldState staged)
        {
            Calls++;
            SeenPlanning = input.WorldState;
            SeenStaged = staged;
            return run(input, staged);
        }
    }

    private static TurnResolutionInput Input(WorldState state, ulong turn = 1UL) =>
        new(state, Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(712UL)));

    private static CanonicalGameSession Session() =>
        CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));

    private static TurnResolutionResult NoOp(WorldState state) =>
        new(state, Array.Empty<ISimulationEvent>(),
            new SimulationEventLog(Array.Empty<SimulationEventLogEntry>()));

    private static TestEvent Event(ulong turn, ulong command, ulong eventSequence = 1UL) =>
        new(new EventId(new CommandId(new TurnNumber(turn), command), eventSequence));

    private static TurnResolutionResult Result(
        WorldState next, TestEvent simulationEvent, bool executed = true) =>
        new(next, new ISimulationEvent[] { simulationEvent },
            new SimulationEventLog(new[] {
                new SimulationEventLogEntry(1UL, simulationEvent,
                    wasEligible: executed, wasExecuted: executed) }));

    [Fact]
    public void ConstructorRejectsNullAndEmptyPhaseCollection()
    {
        Assert.Throws<ArgumentNullException>(() => new PhasedTurnResolutionEngine(null!));
        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(Array.Empty<ITurnResolutionPhase>()));
    }

    [Fact]
    public void ConstructorRejectsNullBlankAndDuplicatePhaseIds()
    {
        var valid = new Phase("economy", (_, state) => NoOp(state));
        Assert.Throws<ArgumentException>(() => new PhasedTurnResolutionEngine(
            new ITurnResolutionPhase[] { null! }));
        Assert.Throws<ArgumentException>(() => new PhasedTurnResolutionEngine(
            new ITurnResolutionPhase[] { new Phase("  ", (_, state) => NoOp(state)) }));
        Assert.Throws<ArgumentException>(() => new PhasedTurnResolutionEngine(
            new ITurnResolutionPhase[] { valid, new Phase("economy", (_, state) => NoOp(state)) }));
    }

    [Fact]
    public void PhaseOrderIsExplicitAndRegisteredAsAReadOnlySnapshot()
    {
        var first = new Phase("periodic", (_, state) => NoOp(state));
        var second = new Phase("movement", (_, state) => NoOp(state));
        var stages = new List<ITurnResolutionPhase> { first, second };
        var engine = new PhasedTurnResolutionEngine(stages);
        stages.Reverse();
        Assert.Equal(new[] { "periodic", "movement" }, engine.PhaseIds);
        engine.Resolve(Input(WorldState.CreateInitial()));
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public void EmptyCommandTurnStillExecutesEveryPhase()
    {
        var first = new Phase("periodic", (_, state) => NoOp(state));
        var second = new Phase("reconcile", (_, state) => NoOp(state));
        var initial = WorldState.CreateInitial();
        var result = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second })
            .Resolve(Input(initial));
        Assert.Same(initial, result.ResultingWorldState);
        Assert.Empty(result.Events);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public void EveryPhaseSeesOriginalPlanningAndPreviousStagedSnapshot()
    {
        var initial = WorldState.CreateInitial();
        var firstEvent = Event(1UL, 1UL);
        var first = new Phase("periodic", (_, staged) =>
            Result(staged.AdvanceRevision(), firstEvent));
        var second = new Phase("reconcile", (_, staged) => NoOp(staged));
        var result = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second })
            .Resolve(Input(initial));
        Assert.Same(initial, first.SeenPlanning);
        Assert.Same(initial, first.SeenStaged);
        Assert.Same(initial, second.SeenPlanning);
        Assert.Same(result.ResultingWorldState, second.SeenStaged);
        Assert.Equal(1UL, result.ResultingWorldState.Revision);
    }

    [Fact]
    public void PhaseEventLogsAreRebasedToContiguousGlobalOrder()
    {
        var firstEvent = Event(1UL, 1UL);
        var secondEvent = Event(1UL, 2UL);
        var first = new Phase("periodic", (_, staged) =>
            Result(staged.AdvanceRevision(), firstEvent));
        var second = new Phase("movement", (_, staged) =>
            Result(staged.AdvanceRevision(), secondEvent));
        var result = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second })
            .Resolve(Input(WorldState.CreateInitial()));
        Assert.Equal(new ulong[] { 1UL, 2UL },
            result.EventLog.Entries.Select(entry => entry.ResolutionSequence));
        Assert.Equal(new[] { firstEvent.Id, secondEvent.Id },
            result.Events.Select(entry => entry.Id));
        Assert.All(result.EventLog.Entries, entry => Assert.True(entry.WasExecuted));
        Assert.Equal(2UL, result.ResultingWorldState.Revision);
    }

    [Fact]
    public void RejectsChangingPhaseIdentityAfterConstruction()
    {
        var phase = new Phase("initial", (_, state) => NoOp(state));
        var engine = new PhasedTurnResolutionEngine(new[] { phase });
        phase.PhaseId = "modified";
        Assert.Throws<InvalidOperationException>(() => engine.Resolve(Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void RejectsDifferentWorldBindingBeforeNextPhaseRuns()
    {
        var session = Session();
        var foreign = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261013UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0))).Snapshot;
        var first = new Phase("bad", (_, _) => Result(foreign, Event(1UL, 1UL)));
        var second = new Phase("never", (_, state) => NoOp(state));
        var engine = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second });
        Assert.Throws<InvalidOperationException>(() => engine.Resolve(Input(session.Snapshot)));
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public void RejectsRevisionRegression()
    {
        var initial = WorldState.CreateInitial().AdvanceRevision();
        var phase = new Phase("bad", (_, _) => Result(WorldState.CreateInitial(), Event(1UL, 1UL)));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(Input(initial)));
    }

    [Fact]
    public void RejectsUnloggedSnapshotReplacement()
    {
        var phase = new Phase("bad", (_, state) => NoOp(state.AdvanceRevision()));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void RejectsEventAndLogMismatch()
    {
        var simulationEvent = Event(1UL, 1UL);
        var phase = new Phase("bad", (_, state) => new TurnResolutionResult(
            state, new ISimulationEvent[] { simulationEvent },
            new SimulationEventLog(Array.Empty<SimulationEventLogEntry>())));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void RejectsLogWithDifferentEventIdentity()
    {
        var expected = Event(1UL, 1UL);
        var wrong = Event(1UL, 2UL);
        var phase = new Phase("bad", (_, state) => new TurnResolutionResult(
            state, new ISimulationEvent[] { expected },
            new SimulationEventLog(new[] {
                new SimulationEventLogEntry(1UL, wrong, true, true) })));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void RejectsEventsFromAnotherTurn()
    {
        var foreignEvent = Event(2UL, 1UL);
        var phase = new Phase("bad", (_, state) => Result(state, foreignEvent, false));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(Input(WorldState.CreateInitial(), 1UL)));
    }

    [Fact]
    public void RejectsRepeatedEventIdAcrossPhases()
    {
        var simulationEvent = Event(1UL, 1UL);
        var first = new Phase("one", (_, state) => Result(state, simulationEvent, false));
        var second = new Phase("two", (_, state) => Result(state, simulationEvent, false));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second })
                .Resolve(Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void OpenTurnDoesNotExecuteAnyPhase()
    {
        var session = Session();
        var phase = new Phase("periodic", (_, state) => NoOp(state));
        var engine = new PhasedTurnResolutionEngine(new[] { phase });
        var gate = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        var state = session.Snapshot;
        var result = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), false, false), Input(state)));
        Assert.False(result.WasResolved);
        Assert.Equal(0, phase.Calls);
        Assert.Same(state, session.Snapshot);
    }

    [Fact]
    public void ClosedTurnPublishesOnlyFinalPhaseResult()
    {
        var session = Session();
        var initial = session.Snapshot;
        var first = new Phase("periodic", (_, state) =>
            Result(state.AdvanceRevision(), Event(1UL, 1UL)));
        var second = new Phase("reconcile", (_, state) =>
            Result(state.AdvanceRevision(), Event(1UL, 2UL)));
        var engine = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second });
        var gate = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        var result = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false), Input(initial)));
        Assert.True(result.WasResolved);
        Assert.Same(initial, first.SeenPlanning);
        Assert.Same(initial, second.SeenPlanning);
        Assert.NotSame(initial, session.Snapshot);
        Assert.Equal(2UL, session.Snapshot.Revision);
        Assert.Same(result.ResolutionResult!.ResultingWorldState, session.Snapshot);
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
    }

    [Fact]
    public void FailedLaterPhaseDoesNotPublishPartialStateOrAdvanceTurn()
    {
        var session = Session();
        var initial = session.Snapshot;
        var first = new Phase("periodic", (_, state) =>
            Result(state.AdvanceRevision(), Event(1UL, 1UL)));
        var second = new Phase("broken", (_, _) =>
            throw new InvalidOperationException("domain resolution failed"));
        var engine = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second });
        var gate = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        Assert.Throws<InvalidOperationException>(() => gate.ResolveAndCommit(
            new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(new TurnNumber(1UL), true, false), Input(initial))));
        Assert.Same(initial, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), gate.NextTurn);
        Assert.Null(gate.LastCommittedTurn);
    }
}
