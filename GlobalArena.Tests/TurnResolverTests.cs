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
        var eventRevalidator = new TestEventRevalidator();
        var eventExecutor = new TestEventExecutor();

        var input = new TurnResolutionInput(
            state,
            Array.Empty<ISimulationCommand>(),
            new SimulationContext(
                new TurnNumber(1UL),
                new SimulationSeed(123UL)));

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Equal(0, eventRevalidator.CallCount);
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
            new TestEventRevalidator(),
            new TestEventExecutor());

        var result = resolver.Resolve(input);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void SingleCommandSingleEventIsRevalidatedBeforeExecutionAndProducesResultingState()
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

        var callOrder = new List<string>();

        var eventRevalidator = new TestEventRevalidator(
            canExecute: true,
            callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder);

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            eventRevalidator,
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

        Assert.Equal(1, eventRevalidator.CallCount);
        Assert.Same(state, eventRevalidator.WorldState);
        Assert.Same(
            simulationEvent,
            eventRevalidator.SimulationEvent);
        Assert.Equal(
            input.Context,
            eventRevalidator.Context);

        Assert.Equal(1, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "revalidate",
                "execute"
            },
            callOrder);
    }

    [Fact]
    public void RejectedSingleEventPreservesWorldStateAndIsNotExecuted()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(3UL);

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
                new SimulationSeed(789UL)));

        var callOrder = new List<string>();

        var eventRevalidator = new TestEventRevalidator(
            canExecute: false,
            callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder);

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);

        var simulationEvent = Assert.Single(result.Events);

        Assert.Equal(
            new EventId(command.Id, 1UL),
            simulationEvent.Id);

        Assert.Equal(1, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "revalidate"
            },
            callOrder);
    }

    [Fact]
    public void SingleCommandWithNoEventsPreservesWorldState()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(4UL);

        var input = new TurnResolutionInput(
            state,
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(101112UL)));

        var eventRevalidator = new TestEventRevalidator();
        var eventExecutor = new TestEventExecutor();

        var resolver = new TurnResolver(
            new TestCommandProcessor(eventCount: 0),
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Empty(result.Events);
        Assert.Equal(0, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);
    }

    [Fact]
    public void MultipleEventsAreRejected()
    {
        var turn = new TurnNumber(5UL);

        var input = new TurnResolutionInput(
            WorldState.CreateInitial(),
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(131415UL)));

        var eventRevalidator = new TestEventRevalidator();
        var eventExecutor = new TestEventExecutor();

        var resolver = new TurnResolver(
            new TestCommandProcessor(eventCount: 2),
            eventRevalidator,
            eventExecutor);

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));

        Assert.Equal(0, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);
    }

    [Fact]
    public void MultipleCommandsAreRejected()
    {
        var turn = new TurnNumber(6UL);

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
                new SimulationSeed(161718UL)));

        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            new TestEventRevalidator(),
            new TestEventExecutor());

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var resolver = new TurnResolver(
            new TestCommandProcessor(),
            new TestEventRevalidator(),
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
                new TestEventRevalidator(),
                new TestEventExecutor()));
    }

    [Fact]
    public void NullEventRevalidatorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                new TestCommandProcessor(),
                null!,
                new TestEventExecutor()));
    }

    [Fact]
    public void NullEventExecutorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                new TestCommandProcessor(),
                new TestEventRevalidator(),
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

    private sealed class TestEventRevalidator
        : ISimulationEventRevalidator
    {
        private readonly bool _canExecute;
        private readonly IList<string>? _callOrder;

        public TestEventRevalidator(
            bool canExecute = true,
            IList<string>? callOrder = null)
        {
            _canExecute = canExecute;
            _callOrder = callOrder;
        }

        public int CallCount { get; private set; }

        public WorldState? WorldState { get; private set; }

        public ISimulationEvent? SimulationEvent { get; private set; }

        public SimulationContext Context { get; private set; }

        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            CallCount++;
            WorldState = worldState;
            SimulationEvent = simulationEvent;
            Context = context;
            _callOrder?.Add("revalidate");

            return _canExecute;
        }
    }

    private sealed class TestEventExecutor
        : ISimulationEventExecutor
    {
        private readonly IList<string>? _callOrder;

        public TestEventExecutor(
            IList<string>? callOrder = null)
        {
            _callOrder = callOrder;
        }

        public int CallCount { get; private set; }

        public WorldState ResultingState { get; } =
            WorldState.CreateInitial();

        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            CallCount++;
            _callOrder?.Add("execute");

            return ResultingState;
        }
    }
}
