using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TurnResolverTests
{
    [Fact]
    public void EmptyTurnPreservesWorldState()
    {
        var state = WorldState.CreateInitial();
        var eventExecutor = new TestEventExecutor();

        var input = new TurnResolutionInput(
            state,
            Array.Empty<ISimulationCommand>(),
            new SimulationContext(
                new TurnNumber(1UL),
                new SimulationSeed(123UL)));

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Equal(0, eventExecutor.CallCount);
    }

    [Fact]
    public void EmptyTurnProducesNoEvents()
    {
        var input = new TurnResolutionInput(
            WorldState.CreateInitial(),
            Array.Empty<ISimulationCommand>(),
            new SimulationContext(
                new TurnNumber(1UL),
                new SimulationSeed(123UL)));

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            new TestEventExecutor());

        var result = resolver.Resolve(input);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void SingleCommandSingleEventIsExecutedAndProducesResultingState()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(2UL);

        var command = new TestCommand(
            new CommandId(turn, 1UL));

        var input = new TurnResolutionInput(
            state,
            new ISimulationCommand[]
            {
                command
            },
            new SimulationContext(
                turn,
                new SimulationSeed(456UL)));

        var eventExecutor = new TestEventExecutor();

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(
            eventExecutor.ResultingState,
            result.ResultingWorldState);

        Assert.NotSame(
            state,
            result.ResultingWorldState);

        var simulationEvent = Assert.Single(result.Events);

        Assert.Equal(
            new EventId(command.Id, 1UL),
            simulationEvent.Id);

        Assert.Equal(1, eventExecutor.CallCount);
    }

    [Fact]
    public void SingleCommandWithNoEventsPreservesWorldState()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(3UL);

        var input = new TurnResolutionInput(
            state,
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(789UL)));

        var eventExecutor = new TestEventExecutor();

        var resolver = new TurnResolver(
            new TestCommandProcessor(eventCount: 0),
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Empty(result.Events);
        Assert.Equal(0, eventExecutor.CallCount);
    }

    [Fact]
    public void MultipleEventsAreRejected()
    {
        var turn = new TurnNumber(4UL);

        var input = new TurnResolutionInput(
            WorldState.CreateInitial(),
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(101112UL)));

        var eventExecutor = new TestEventExecutor();

        var resolver = new TurnResolver(
            new TestCommandProcessor(eventCount: 2),
            eventExecutor);

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));

        Assert.Equal(0, eventExecutor.CallCount);
    }

    [Fact]
    public void MultipleCommandsAreRejected()
    {
        var turn = new TurnNumber(5UL);

        var input = new TurnResolutionInput(
            WorldState.CreateInitial(),
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL)),
                new TestCommand(
                    new CommandId(turn, 2UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(131415UL)));

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            new TestEventExecutor());

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            new TestEventExecutor());

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(null!));
    }

    [Fact]
    public void NullCommandProcessorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                null!,
                new TestEventExecutor()));
    }

    [Fact]
    public void NullEventExecutorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                new TestCommandProcessor(),
                null!));
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestCommandProcessor
        : ISimulationCommandProcessor
    {
        private readonly int _eventCount;

        public TestCommandProcessor(
            int eventCount = 1)
        {
            _eventCount = eventCount;
        }

        public IReadOnlyList<ISimulationEvent> Process(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            var events =
                new ISimulationEvent[_eventCount];

            for (var index = 0; index < _eventCount; index++)
            {
                events[index] = new TestEvent(
                    new EventId(
                        command.Id,
                        (ulong)index + 1UL));
            }

            return events;
        }
    }

    private sealed class TestEventExecutor
        : ISimulationEventExecutor
    {
        public int CallCount { get; private set; }

        public WorldState ResultingState { get; } =
            WorldState.CreateInitial();

        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            CallCount++;

            return ResultingState;
        }
    }
}
