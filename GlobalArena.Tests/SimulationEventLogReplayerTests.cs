using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationEventLogReplayerTests
{
    [Fact]
    public void NullExecutorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SimulationEventLogReplayer(
                null!));
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var replayer =
            new SimulationEventLogReplayer(
                new TrackingEventExecutor());

        Assert.Throws<ArgumentNullException>(
            () => replayer.Replay(
                null!));
    }

    [Fact]
    public void EmptyLogPreservesInitialWorldState()
    {
        var initialState =
            WorldState.CreateInitial();

        var executor =
            new TrackingEventExecutor();

        var replayer =
            new SimulationEventLogReplayer(
                executor);

        var result =
            replayer.Replay(
                new SimulationReplayInput(
                    initialState,
                    new SimulationEventLog(
                        Array.Empty<SimulationEventLogEntry>()),
                    new SimulationContext(
                        new TurnNumber(1UL),
                        new SimulationSeed(123UL))));

        Assert.Same(
            initialState,
            result);

        Assert.Equal(
            0,
            executor.CallCount);
    }

    [Fact]
    public void ReplayExecutesOnlyOriginallyExecutedEntriesInRecordedOrder()
    {
        var initialState =
            WorldState.CreateInitial();

        var turn =
            new TurnNumber(2UL);

        var context =
            new SimulationContext(
                turn,
                new SimulationSeed(456UL));

        var first =
            CreateEvent(
                turn,
                eventSequence: 3UL);

        var rejected =
            CreateEvent(
                turn,
                eventSequence: 1UL);

        var second =
            CreateEvent(
                turn,
                eventSequence: 2UL);

        var eventLog =
            new SimulationEventLog(
                new[]
                {
                    new SimulationEventLogEntry(
                        resolutionSequence: 1UL,
                        first,
                        wasEligible: true,
                        wasExecuted: true),
                    new SimulationEventLogEntry(
                        resolutionSequence: 2UL,
                        rejected,
                        wasEligible: false,
                        wasExecuted: false),
                    new SimulationEventLogEntry(
                        resolutionSequence: 3UL,
                        second,
                        wasEligible: true,
                        wasExecuted: true)
                });

        var executor =
            new TrackingEventExecutor();

        var replayer =
            new SimulationEventLogReplayer(
                executor);

        var result =
            replayer.Replay(
                new SimulationReplayInput(
                    initialState,
                    eventLog,
                    context));

        Assert.Equal(
            2UL,
            result.Revision);

        Assert.Equal(
            2,
            executor.CallCount);

        Assert.Equal(
            new ulong[]
            {
                3UL,
                2UL
            },
            executor.Events.Select(
                simulationEvent =>
                    simulationEvent.Id.Sequence));

        Assert.Equal(
            new ulong[]
            {
                0UL,
                1UL
            },
            executor.WorldStates.Select(
                worldState =>
                    worldState.Revision));

        Assert.All(
            executor.Contexts,
            actual =>
                Assert.Equal(
                    context,
                    actual));

        Assert.Equal(
            0UL,
            initialState.Revision);
    }

    [Fact]
    public void ReplayMatchesOriginalResolutionObservableState()
    {
        var turn =
            new TurnNumber(3UL);

        var context =
            new SimulationContext(
                turn,
                new SimulationSeed(0UL));

        var originalInitialState =
            WorldState.CreateInitial();

        var originalResult =
            new TurnResolver(
                new RevisionCommandValidator(),
                new RevisionCommandProcessor(),
                new SeededSimulationEventOrderer(),
                new RejectSecondResolutionEvent(),
                new RevisionEventExecutor())
            .Resolve(
                new TurnResolutionInput(
                    originalInitialState,
                    new ISimulationCommand[]
                    {
                        new RevisionCommand(
                            new CommandId(
                                turn,
                                1UL),
                            EventCount: 3)
                    },
                    context));

        var replayInitialState =
            WorldState.CreateInitial();

        var replayResult =
            new SimulationEventLogReplayer(
                new RevisionEventExecutor())
            .Replay(
                new SimulationReplayInput(
                    replayInitialState,
                    originalResult.EventLog,
                    context));

        Assert.Equal(
            new bool[]
            {
                true,
                false,
                true
            },
            originalResult.EventLog.Entries.Select(
                entry =>
                    entry.WasExecuted));

        Assert.Equal(
            originalResult.ResultingWorldState.Revision,
            replayResult.Revision);

        Assert.Equal(
            2UL,
            replayResult.Revision);

        Assert.Equal(
            0UL,
            originalInitialState.Revision);

        Assert.Equal(
            0UL,
            replayInitialState.Revision);
    }

    private static TestEvent CreateEvent(
        TurnNumber turn,
        ulong eventSequence)
    {
        return new TestEvent(
            new EventId(
                new CommandId(
                    turn,
                    1UL),
                eventSequence));
    }

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TrackingEventExecutor
        : ISimulationEventExecutor
    {
        public int CallCount { get; private set; }

        public List<WorldState> WorldStates { get; } =
            new();

        public List<ISimulationEvent> Events { get; } =
            new();

        public List<SimulationContext> Contexts { get; } =
            new();

        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            CallCount++;
            WorldStates.Add(worldState);
            Events.Add(simulationEvent);
            Contexts.Add(context);

            return worldState.AdvanceRevision();
        }
    }

    private sealed record RevisionCommand(
        CommandId Id,
        int EventCount) : ISimulationCommand;

    private sealed record RevisionEvent(
        EventId Id) : ISimulationEvent;

    private sealed class RevisionCommandValidator
        : ISimulationCommandValidator
    {
        public bool IsValid(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            return command is RevisionCommand revisionCommand
                && revisionCommand.EventCount >= 0;
        }
    }

    private sealed class RevisionCommandProcessor
        : ISimulationCommandProcessor
    {
        public IReadOnlyList<ISimulationEvent> Process(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            var revisionCommand =
                Assert.IsType<RevisionCommand>(
                    command);

            var events =
                new ISimulationEvent[
                    revisionCommand.EventCount];

            for (var index = 0;
                 index < events.Length;
                 index++)
            {
                events[index] =
                    new RevisionEvent(
                        new EventId(
                            revisionCommand.Id,
                            (ulong)index + 1UL));
            }

            return events;
        }
    }

    private sealed class RejectSecondResolutionEvent
        : ISimulationEventRevalidator
    {
        private int _callCount;

        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            _callCount++;

            return _callCount != 2;
        }
    }

    private sealed class RevisionEventExecutor
        : ISimulationEventExecutor
    {
        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            Assert.IsType<RevisionEvent>(
                simulationEvent);

            return worldState.AdvanceRevision();
        }
    }
}
