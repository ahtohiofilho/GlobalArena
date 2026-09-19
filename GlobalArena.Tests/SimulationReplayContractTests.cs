using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationReplayContractTests
{
    [Fact]
    public void ReplayInputCarriesInitialWorldStateEventLogAndContext()
    {
        var state = WorldState.CreateInitial();

        var context = new SimulationContext(
            new TurnNumber(1UL),
            new SimulationSeed(123UL));

        var eventLog = new SimulationEventLog(
            Array.Empty<SimulationEventLogEntry>());

        var input = new SimulationReplayInput(
            state,
            eventLog,
            context);

        Assert.Same(
            state,
            input.InitialWorldState);

        Assert.Same(
            eventLog,
            input.EventLog);

        Assert.Equal(
            context,
            input.Context);
    }

    [Fact]
    public void ReplayInputRejectsNullInitialWorldState()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SimulationReplayInput(
                null!,
                new SimulationEventLog(
                    Array.Empty<SimulationEventLogEntry>()),
                new SimulationContext(
                    new TurnNumber(2UL),
                    new SimulationSeed(456UL))));
    }

    [Fact]
    public void ReplayInputRejectsNullEventLog()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SimulationReplayInput(
                WorldState.CreateInitial(),
                null!,
                new SimulationContext(
                    new TurnNumber(3UL),
                    new SimulationSeed(789UL))));
    }

    [Fact]
    public void ReplayerContractReceivesReplayInputAndReturnsWorldState()
    {
        var initialState =
            WorldState.CreateInitial();

        var resultingState =
            initialState.AdvanceRevision();

        var eventLog = new SimulationEventLog(
            new[]
            {
                new SimulationEventLogEntry(
                    resolutionSequence: 1UL,
                    CreateEvent(),
                    wasEligible: true,
                    wasExecuted: true)
            });

        var input = new SimulationReplayInput(
            initialState,
            eventLog,
            new SimulationContext(
                new TurnNumber(4UL),
                new SimulationSeed(101112UL)));

        var replayer =
            new TestEventLogReplayer(
                resultingState);

        var result =
            replayer.Replay(input);

        Assert.Same(
            input,
            replayer.Input);

        Assert.Same(
            resultingState,
            result);
    }

    private static TestEvent CreateEvent()
    {
        var turn = new TurnNumber(4UL);

        return new TestEvent(
            new EventId(
                new CommandId(
                    turn,
                    1UL),
                1UL));
    }

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestEventLogReplayer
        : ISimulationEventLogReplayer
    {
        private readonly WorldState _resultingState;

        public TestEventLogReplayer(
            WorldState resultingState)
        {
            _resultingState = resultingState;
        }

        public SimulationReplayInput? Input { get; private set; }

        public WorldState Replay(
            SimulationReplayInput input)
        {
            Input = input;

            return _resultingState;
        }
    }
}
