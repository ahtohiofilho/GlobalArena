using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class AuthoritativeTurnCommitGateTests
{
    private static CanonicalGameSession Session(ulong seed = 20261012UL) =>
        CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));

    private static TurnPolicyResolutionGateInput Input(
        WorldState planningState, ulong turn = 1UL,
        IEnumerable<ISimulationCommand>? commands = null) =>
        new(new TurnPolicyInput(new TurnNumber(turn), true, false),
            new TurnResolutionInput(planningState,
                commands ?? Array.Empty<ISimulationCommand>(),
                new SimulationContext(new TurnNumber(turn), new SimulationSeed(17UL))));

    private static TurnPolicyResolutionGateResult Closed(WorldState state) =>
        TurnPolicyResolutionGateResult.Resolved(new TurnResolutionResult(
            state, Array.Empty<ISimulationEvent>(),
            new SimulationEventLog(Array.Empty<SimulationEventLogEntry>())));

    private sealed class Gate(Func<TurnPolicyResolutionGateInput, TurnPolicyResolutionGateResult> execute)
        : ITurnPolicyResolutionGate
    {
        public int Calls { get; private set; }

        public TurnPolicyResolutionGateResult Resolve(TurnPolicyResolutionGateInput input)
        {
            Calls++;
            return execute(input);
        }
    }

    private sealed record Command(CommandId Id) : ISimulationCommand;

    [Fact]
    public void ClosedTurnPublishesFinalSnapshotAndAdvancesTurnSeparatelyFromRevision()
    {
        var session = Session();
        var pipeline = new Gate(input => Closed(input.ResolutionInput.WorldState.AdvanceRevision()));
        var publisher = new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 1U);
        var result = publisher.ResolveAndCommit(Input(session.Snapshot));

        Assert.True(result.WasResolved);
        Assert.Same(result.ResolutionResult!.ResultingWorldState, session.Snapshot);
        Assert.Equal(1UL, session.Snapshot.Revision);
        Assert.Equal(new TurnNumber(1UL), publisher.LastCommittedTurn);
        Assert.Equal(new TurnNumber(2UL), publisher.NextTurn);
        Assert.Equal(1U, publisher.RulesetVersion);
        Assert.Equal(1, pipeline.Calls);
    }

    [Fact]
    public void OpenTurnLeavesSessionAndTurnUnchanged()
    {
        var session = Session();
        var before = session.Snapshot;
        var publisher = new AuthoritativeTurnCommitGate(session,
            new Gate(_ => TurnPolicyResolutionGateResult.Open()), new TurnNumber(1UL), 1U);
        var result = publisher.ResolveAndCommit(Input(before));

        Assert.False(result.WasResolved);
        Assert.Same(before, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), publisher.NextTurn);
        Assert.Null(publisher.LastCommittedTurn);
    }

    [Fact]
    public void DuplicateTurnFailsBeforeCallingResolverAgain()
    {
        var session = Session();
        var pipeline = new Gate(input => Closed(input.ResolutionInput.WorldState.AdvanceRevision()));
        var publisher = new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 1U);
        var original = Input(session.Snapshot);
        publisher.ResolveAndCommit(original);

        Assert.Throws<InvalidOperationException>(() => publisher.ResolveAndCommit(original));
        Assert.Equal(1, pipeline.Calls);
        Assert.Equal(new TurnNumber(2UL), publisher.NextTurn);
    }

    [Fact]
    public void FutureTurnCannotSkipARequiredTurn()
    {
        var session = Session();
        var pipeline = new Gate(input => Closed(input.ResolutionInput.WorldState.AdvanceRevision()));
        var publisher = new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 1U);

        Assert.Throws<InvalidOperationException>(() => publisher.ResolveAndCommit(Input(session.Snapshot, 2UL)));
        Assert.Equal(0, pipeline.Calls);
        Assert.Equal(0UL, session.Snapshot.Revision);
    }

    [Fact]
    public void StalePlanningSnapshotFailsBeforeResolution()
    {
        var session = Session();
        var stale = session.Snapshot;
        session.AcceptSnapshot(stale.AdvanceRevision());
        var pipeline = new Gate(input => Closed(input.ResolutionInput.WorldState.AdvanceRevision()));
        var publisher = new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 1U);

        Assert.Throws<InvalidOperationException>(() => publisher.ResolveAndCommit(Input(stale)));
        Assert.Equal(0, pipeline.Calls);
        Assert.Equal(1UL, session.Snapshot.Revision);
    }

    [Fact]
    public void DifferentWorldResultFailsWithoutPublishingOrAdvancing()
    {
        var session = Session();
        var old = session.Snapshot;
        var foreign = Session(20261013UL).Snapshot.AdvanceRevision();
        var publisher = new AuthoritativeTurnCommitGate(session,
            new Gate(_ => Closed(foreign)), new TurnNumber(1UL), 1U);

        Assert.Throws<InvalidOperationException>(() => publisher.ResolveAndCommit(Input(old)));
        Assert.Same(old, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), publisher.NextTurn);
        Assert.Null(publisher.LastCommittedTurn);
    }

    [Fact]
    public void RevisionRegressionFailsWithoutAdvancingTurn()
    {
        var session = Session();
        var stale = session.Snapshot;
        var pipeline = new Gate(input => input.PolicyInput.Turn.Value == 1UL
            ? Closed(input.ResolutionInput.WorldState.AdvanceRevision())
            : Closed(stale));
        var publisher = new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 1U);
        publisher.ResolveAndCommit(Input(session.Snapshot));
        var current = session.Snapshot;

        Assert.Throws<InvalidOperationException>(() => publisher.ResolveAndCommit(Input(current, 2UL)));
        Assert.Same(current, session.Snapshot);
        Assert.Equal(new TurnNumber(2UL), publisher.NextTurn);
        Assert.Equal(new TurnNumber(1UL), publisher.LastCommittedTurn);
    }

    [Fact]
    public void CommandsFromAnotherTurnAreRejectedBeforeDomainResolution()
    {
        var session = Session();
        var pipeline = new Gate(input => Closed(input.ResolutionInput.WorldState.AdvanceRevision()));
        var publisher = new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 1U);
        var wrongCommand = new Command(new CommandId(new TurnNumber(2UL), 1UL));

        Assert.Throws<InvalidOperationException>(() => publisher.ResolveAndCommit(
            Input(session.Snapshot, 1UL, new ISimulationCommand[] { wrongCommand })));
        Assert.Equal(0, pipeline.Calls);
    }

    [Fact]
    public void ClosedNoCommandResultDoesNotInventPeriodicEconomicEffects()
    {
        var session = Session();
        var original = session.Snapshot;
        var publisher = new AuthoritativeTurnCommitGate(session,
            new Gate(input => Closed(input.ResolutionInput.WorldState)),
            new TurnNumber(1UL), 1U);

        publisher.ResolveAndCommit(Input(original));
        Assert.Same(original, session.Snapshot);
        Assert.Equal(0UL, session.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), publisher.NextTurn);
        // An economy phase policy remains a separate, unimplemented responsibility.
    }

    [Fact]
    public void NewSnapshotWithEqualRevisionCanBeCommitted()
    {
        var session = Session();
        var original = session.Snapshot;
        var replacement = original.WithWarfare(original.Warfare);
        Assert.NotSame(original, replacement);
        var publisher = new AuthoritativeTurnCommitGate(session,
            new Gate(_ => Closed(replacement)), new TurnNumber(1UL), 1U);

        publisher.ResolveAndCommit(Input(original));
        Assert.Same(replacement, session.Snapshot);
        Assert.Equal(0UL, session.Snapshot.Revision);
    }

    [Fact]
    public void DomainResolutionExceptionDoesNotPartiallyCommit()
    {
        var session = Session();
        var original = session.Snapshot;
        var publisher = new AuthoritativeTurnCommitGate(session,
            new Gate(_ => throw new InvalidOperationException("domain error")),
            new TurnNumber(1UL), 1U);

        Assert.Throws<InvalidOperationException>(() => publisher.ResolveAndCommit(Input(original)));
        Assert.Same(original, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), publisher.NextTurn);
        Assert.Null(publisher.LastCommittedTurn);
    }

    [Fact]
    public void RejectsInvalidRulesetVersionAndNullDependencies()
    {
        var session = Session();
        var pipeline = new Gate(_ => TurnPolicyResolutionGateResult.Open());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 0U));
        Assert.Throws<ArgumentNullException>(() =>
            new AuthoritativeTurnCommitGate(null!, pipeline, new TurnNumber(1UL), 1U));
        Assert.Throws<ArgumentNullException>(() =>
            new AuthoritativeTurnCommitGate(session, null!, new TurnNumber(1UL), 1U));
        var publisher = new AuthoritativeTurnCommitGate(session, pipeline, new TurnNumber(1UL), 1U);
        Assert.Throws<ArgumentNullException>(() => publisher.ResolveAndCommit(null!));
    }
}
