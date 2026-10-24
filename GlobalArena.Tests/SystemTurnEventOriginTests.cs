using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SystemTurnEventOriginTests
{
    private sealed record ProbeEvent(EventId Id) : ISimulationEvent;
    private sealed record ProbeCommand(CommandId Id) : ISimulationCommand;

    private sealed class ProbePhase(
        string id, Func<TurnResolutionInput, WorldState, TurnResolutionResult> handler)
        : ITurnResolutionPhase
    {
        public string PhaseId => id;
        public TurnResolutionResult Resolve(TurnResolutionInput input, WorldState staged) =>
            handler(input, staged);
    }

    private static EventId SystemEvent(ulong turn = 1UL, string phase = "periodic",
        ulong ordinal = 1UL, ulong sequence = 1UL) =>
        EventId.ForSystemPhase(new TurnNumber(turn), phase, ordinal, sequence);

    private static TurnResolutionInput Input(WorldState state, ulong turn = 1UL,
        IEnumerable<ISimulationCommand>? commands = null) =>
        new(state, commands ?? Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(44UL)));

    private static TurnResolutionResult Emitted(WorldState next, params ProbeEvent[] events) =>
        new(next, events,
            new SimulationEventLog(events.Select((ev, index) =>
                new SimulationEventLogEntry((ulong)index + 1UL, ev, true, true))));

    private static CanonicalGameSession Session() =>
        CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));

    [Fact]
    public void LegacyCommandEventRetainsItsCommandIdentity()
    {
        var cmd = new CommandId(new TurnNumber(3UL), 7UL);
        var value = new EventId(cmd, 2UL);
        Assert.Equal(EventOriginKind.Command, value.OriginKind);
        Assert.Equal(cmd, value.OriginCommandId);
        Assert.Equal(new TurnNumber(3UL), value.Turn);
        Assert.Null(value.SystemPhaseId);
        Assert.Equal(0UL, value.SystemOrdinal);
        Assert.Equal(2UL, value.Sequence);
    }

    [Fact]
    public void DefaultEventIdRetainsLegacyInvalidCommandIdInspection()
    {
        var empty = default(EventId);
        Assert.Equal(default(CommandId), empty.OriginCommandId);
        Assert.Equal(0UL, empty.OriginCommandId.Turn.Value);
        Assert.Equal(0UL, empty.Sequence);
    }

    [Fact]
    public void SystemEventExposesRealTurnAndPhaseButNoFabricatedCommand()
    {
        var value = SystemEvent(8UL, "periodic.agriculture", 5UL, 2UL);
        Assert.Equal(EventOriginKind.System, value.OriginKind);
        Assert.Equal(new TurnNumber(8UL), value.Turn);
        Assert.Equal("periodic.agriculture", value.SystemPhaseId);
        Assert.Equal(5UL, value.SystemOrdinal);
        Assert.Equal(2UL, value.Sequence);
        Assert.Throws<InvalidOperationException>(() => { _ = value.OriginCommandId; });
    }

    [Fact]
    public void SystemEventRejectsInvalidTurn()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventId.ForSystemPhase(default, "periodic", 1UL));
    }

    [Fact]
    public void SystemEventRejectsMissingPhaseIdentity()
    {
        Assert.Throws<ArgumentException>(() =>
            EventId.ForSystemPhase(new TurnNumber(1UL), " ", 1UL));
        Assert.Throws<ArgumentException>(() =>
            EventId.ForSystemPhase(new TurnNumber(1UL), null!, 1UL));
    }

    [Fact]
    public void SystemEventRejectsZeroOrdinal()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventId.ForSystemPhase(new TurnNumber(1UL), "periodic", 0UL));
    }

    [Fact]
    public void EventIdsRejectZeroSequenceForBothOrigins()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventId(new CommandId(new TurnNumber(1UL), 1UL), 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventId.ForSystemPhase(new TurnNumber(1UL), "periodic", 1UL, 0UL));
    }

    [Fact]
    public void SystemEventIdentityIsStableAndScopedByTurnPhaseOrdinalAndSequence()
    {
        var original = SystemEvent(2UL, "periodic", 3UL, 4UL);
        Assert.Equal(original, SystemEvent(2UL, "periodic", 3UL, 4UL));
        Assert.NotEqual(original, SystemEvent(3UL, "periodic", 3UL, 4UL));
        Assert.NotEqual(original, SystemEvent(2UL, "trade", 3UL, 4UL));
        Assert.NotEqual(original, SystemEvent(2UL, "periodic", 4UL, 4UL));
        Assert.NotEqual(original, SystemEvent(2UL, "periodic", 3UL, 5UL));
    }

    [Fact]
    public void PeriodicSystemEventCanAdvanceClosedTurnWithNoPlayerCommands()
    {
        var session = Session();
        var original = session.Snapshot;
        var phase = new ProbePhase("periodic", (input, staged) =>
            Emitted(staged.AdvanceRevision(), new ProbeEvent(SystemEvent())));
        var engine = new PhasedTurnResolutionEngine(new[] { phase });
        var authority = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        var resolved = authority.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false), Input(original)));
        Assert.True(resolved.WasResolved);
        Assert.Equal(1UL, session.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), authority.NextTurn);
        Assert.Single(resolved.ResolutionResult!.EventLog.Entries);
        Assert.Equal(EventOriginKind.System, resolved.ResolutionResult.Events[0].Id.OriginKind);
    }

    [Fact]
    public void MixedCommandAndSystemEventsKeepDistinctProvenanceAndGlobalOrder()
    {
        var initial = WorldState.CreateInitial();
        var commandId = new CommandId(new TurnNumber(1UL), 9UL);
        var periodic = new ProbePhase("periodic", (_, staged) =>
            Emitted(staged.AdvanceRevision(), new ProbeEvent(SystemEvent())));
        var movement = new ProbePhase("movement", (_, staged) =>
            Emitted(staged.AdvanceRevision(),
                new ProbeEvent(new EventId(commandId, 1UL))));
        var result = new PhasedTurnResolutionEngine(
            new ITurnResolutionPhase[] { periodic, movement }).Resolve(
                Input(initial, 1UL, new ISimulationCommand[] { new ProbeCommand(commandId) }));
        Assert.Equal(2UL, result.ResultingWorldState.Revision);
        Assert.Equal(new ulong[] { 1UL, 2UL },
            result.EventLog.Entries.Select(e => e.ResolutionSequence));
        Assert.Equal(EventOriginKind.System, result.Events[0].Id.OriginKind);
        Assert.Equal(EventOriginKind.Command, result.Events[1].Id.OriginKind);
        Assert.Equal(commandId, result.Events[1].Id.OriginCommandId);
    }

    [Fact]
    public void PhasedEngineRejectsSystemEventFromAnotherTurn()
    {
        var phase = new ProbePhase("periodic", (_, staged) =>
            Emitted(staged.AdvanceRevision(), new ProbeEvent(SystemEvent(2UL))));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(
                Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void PhasedEngineRejectsSystemEventMisattributedToAnotherPhase()
    {
        var phase = new ProbePhase("periodic", (_, staged) =>
            Emitted(staged.AdvanceRevision(), new ProbeEvent(SystemEvent(1UL, "trade"))));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(
                Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void PhasedEngineRejectsDuplicatedSystemEventIdentity()
    {
        var evt = new ProbeEvent(SystemEvent());
        var phase = new ProbePhase("periodic", (_, staged) =>
            Emitted(staged.AdvanceRevision(), evt, evt));
        Assert.Throws<InvalidOperationException>(() =>
            new PhasedTurnResolutionEngine(new[] { phase }).Resolve(
                Input(WorldState.CreateInitial())));
    }

    [Fact]
    public void RejectedSystemEventNeverCommitsPartialSnapshot()
    {
        var session = Session();
        var original = session.Snapshot;
        var first = new ProbePhase("periodic", (_, staged) =>
            Emitted(staged.AdvanceRevision(), new ProbeEvent(SystemEvent())));
        var second = new ProbePhase("trade", (_, staged) =>
            Emitted(staged.AdvanceRevision(), new ProbeEvent(SystemEvent(2UL, "trade"))));
        var engine = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { first, second });
        var authority = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        Assert.Throws<InvalidOperationException>(() => authority.ResolveAndCommit(
            new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(new TurnNumber(1UL), true, false), Input(original))));
        Assert.Same(original, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), authority.NextTurn);
        Assert.Null(authority.LastCommittedTurn);
    }
}
