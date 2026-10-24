using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TurnResolutionEngineContractTests
{
    private sealed class Engine(Func<TurnResolutionInput, TurnResolutionResult> resolve)
        : ITurnResolutionEngine
    {
        public int Calls { get; private set; }
        public TurnResolutionInput? Seen { get; private set; }

        public TurnResolutionResult Resolve(TurnResolutionInput input)
        {
            Calls++;
            Seen = input;
            return resolve(input);
        }
    }

    private static TurnResolutionInput Input(WorldState state, ulong turn = 1UL) =>
        new(state, Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(123UL)));

    private static TurnPolicyResolutionGateInput PolicyInput(
        WorldState state, bool ready, bool deadline = false, ulong turn = 1UL) =>
        new(new TurnPolicyInput(new TurnNumber(turn), ready, deadline), Input(state, turn));

    private static TurnResolutionResult Result(WorldState state) =>
        new(state, Array.Empty<ISimulationEvent>(),
            new SimulationEventLog(Array.Empty<SimulationEventLogEntry>()));

    private static CanonicalGameSession Session() =>
        CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));

    [Fact]
    public void ExistingResolverImplementsEngineContract()
    {
        Assert.True(typeof(ITurnResolutionEngine).IsAssignableFrom(typeof(TurnResolver)));
    }

    [Fact]
    public void ClosedNoCommandTurnInvokesPluggableEngine()
    {
        var initial = WorldState.CreateInitial();
        var engine = new Engine(input => Result(input.WorldState.AdvanceRevision()));
        var gate = new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine);
        var resolved = gate.Resolve(PolicyInput(initial, ready: true));
        Assert.True(resolved.WasResolved);
        Assert.Equal(1UL, resolved.ResolutionResult!.ResultingWorldState.Revision);
        Assert.Equal(1, engine.Calls);
        Assert.Empty(engine.Seen!.Commands);
        Assert.Same(initial, engine.Seen.WorldState);
        // Fixture proves delegation without asserting that production economy exists.
    }

    [Fact]
    public void OpenTurnDoesNotCallPluggableEngine()
    {
        var engine = new Engine(input => Result(input.WorldState.AdvanceRevision()));
        var gate = new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine);
        var result = gate.Resolve(PolicyInput(WorldState.CreateInitial(), ready: false));
        Assert.False(result.WasResolved);
        Assert.Null(result.ResolutionResult);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public void DeadlineClosureDelegatesEvenWithNoCommands()
    {
        var engine = new Engine(input => Result(input.WorldState));
        var gate = new TurnPolicyResolutionGate(new ExternalDeadlineTurnPolicy(), engine);
        var result = gate.Resolve(PolicyInput(WorldState.CreateInitial(), ready: false, deadline: true));
        Assert.True(result.WasResolved);
        Assert.Equal(1, engine.Calls);
    }

    [Fact]
    public void OriginalContextAndPlanningStateAreForwardedUnmodified()
    {
        var original = WorldState.CreateInitial();
        var input = PolicyInput(original, ready: true, turn: 7UL);
        var engine = new Engine(request => Result(request.WorldState));
        var gate = new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine);
        gate.Resolve(input);
        Assert.Same(input.ResolutionInput, engine.Seen);
        Assert.Same(original, engine.Seen!.WorldState);
        Assert.Equal(new TurnNumber(7UL), engine.Seen.Context.Turn);
        Assert.Equal(7UL, input.PolicyInput.Turn.Value);
    }

    [Fact]
    public void EngineExceptionCannotProduceClosedResult()
    {
        var engine = new Engine(_ => throw new InvalidOperationException("phase failure"));
        var gate = new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine);
        Assert.Throws<InvalidOperationException>(() => gate.Resolve(
            PolicyInput(WorldState.CreateInitial(), ready: true)));
        Assert.Equal(1, engine.Calls);
    }

    [Fact]
    public void AuthoritativeGatePublishesClosedFixtureWithoutCommands()
    {
        var session = Session();
        var original = session.Snapshot;
        var engine = new Engine(input => Result(input.WorldState.AdvanceRevision()));
        var policyGate = new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine);
        var authority = new AuthoritativeTurnCommitGate(session, policyGate, new TurnNumber(1UL), 1U);
        var resolved = authority.ResolveAndCommit(PolicyInput(original, ready: true));
        Assert.True(resolved.WasResolved);
        Assert.Equal(1UL, session.Snapshot.Revision);
        Assert.Same(resolved.ResolutionResult!.ResultingWorldState, session.Snapshot);
        Assert.Equal(new TurnNumber(2UL), authority.NextTurn);
        Assert.Equal(new TurnNumber(1UL), authority.LastCommittedTurn);
    }

    [Fact]
    public void OpenTurnCannotCommitFixture()
    {
        var session = Session();
        var original = session.Snapshot;
        var engine = new Engine(input => Result(input.WorldState.AdvanceRevision()));
        var authority = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        var resolved = authority.ResolveAndCommit(PolicyInput(original, ready: false));
        Assert.False(resolved.WasResolved);
        Assert.Same(original, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), authority.NextTurn);
        Assert.Equal(0, engine.Calls);
    }

    [Fact]
    public void NoOpEngineClosesEmptyTurnWithoutFabricatingEconomy()
    {
        var session = Session();
        var original = session.Snapshot;
        var engine = new Engine(input => Result(input.WorldState));
        var authority = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        authority.ResolveAndCommit(PolicyInput(original, ready: true));
        Assert.Same(original, session.Snapshot);
        Assert.Equal(0UL, session.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), authority.NextTurn);
    }

    [Fact]
    public void ForeignWorldFixtureFailurePreservesSessionAndTurn()
    {
        var session = Session();
        var original = session.Snapshot;
        var foreign = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261013UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0))).Snapshot;
        var engine = new Engine(_ => Result(foreign));
        var authority = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        Assert.Throws<InvalidOperationException>(() => authority.ResolveAndCommit(
            PolicyInput(original, ready: true)));
        Assert.Same(original, session.Snapshot);
        Assert.Equal(new TurnNumber(1UL), authority.NextTurn);
        Assert.Null(authority.LastCommittedTurn);
    }
}
