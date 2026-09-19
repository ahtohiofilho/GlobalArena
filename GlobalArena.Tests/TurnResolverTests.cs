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
        var commandValidator = new TestCommandValidator();
        var commandProcessor = new TestCommandProcessor();
        var eventRevalidator = new TestEventRevalidator();
        var eventExecutor = new TestEventExecutor();

        var input = new TurnResolutionInput(
            state,
            Array.Empty<ISimulationCommand>(),
            new SimulationContext(
                new TurnNumber(1UL),
                new SimulationSeed(123UL)));

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Equal(0, commandValidator.CallCount);
        Assert.Equal(0, commandProcessor.CallCount);
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
            new TestCommandValidator(),
            new TestCommandProcessor(),
            new TestEventRevalidator(),
            new TestEventExecutor());

        var result = resolver.Resolve(input);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void SingleCommandSingleEventFollowsValidationRevalidationAndExecutionPipeline()
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

        var commandValidator = new TestCommandValidator(
            isValid: true,
            callOrder);

        var commandProcessor = new TestCommandProcessor(
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            canExecute: true,
            callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
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

        Assert.Equal(1, commandValidator.CallCount);
        Assert.Same(state, commandValidator.WorldState);
        Assert.Same(command, commandValidator.Command);
        Assert.Equal(input.Context, commandValidator.Context);

        Assert.Equal(1, commandProcessor.CallCount);

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
                "validate",
                "process",
                "revalidate",
                "execute"
            },
            callOrder);
    }

    [Fact]
    public void RejectedCommandPreservesWorldStateAndProducesNoEvents()
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

        var callOrder = new List<string>();

        var commandValidator = new TestCommandValidator(
            isValid: false,
            callOrder);

        var commandProcessor = new TestCommandProcessor(
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            callOrder: callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Empty(result.Events);
        Assert.Equal(1, commandValidator.CallCount);
        Assert.Equal(0, commandProcessor.CallCount);
        Assert.Equal(0, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "validate"
            },
            callOrder);
    }

    [Fact]
    public void RejectedSingleEventPreservesWorldStateAndIsNotExecuted()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(4UL);

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
                new SimulationSeed(101112UL)));

        var callOrder = new List<string>();

        var commandValidator = new TestCommandValidator(
            callOrder: callOrder);

        var commandProcessor = new TestCommandProcessor(
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            canExecute: false,
            callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);

        var simulationEvent = Assert.Single(result.Events);

        Assert.Equal(
            new EventId(command.Id, 1UL),
            simulationEvent.Id);

        Assert.Equal(1, commandValidator.CallCount);
        Assert.Equal(1, commandProcessor.CallCount);
        Assert.Equal(1, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "validate",
                "process",
                "revalidate"
            },
            callOrder);
    }

    [Fact]
    public void SingleCommandWithNoEventsPreservesWorldState()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(5UL);

        var input = new TurnResolutionInput(
            state,
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(131415UL)));

        var callOrder = new List<string>();

        var commandValidator = new TestCommandValidator(
            callOrder: callOrder);

        var commandProcessor = new TestCommandProcessor(
            eventCount: 0,
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            callOrder: callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Empty(result.Events);
        Assert.Equal(1, commandValidator.CallCount);
        Assert.Equal(1, commandProcessor.CallCount);
        Assert.Equal(0, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "validate",
                "process"
            },
            callOrder);
    }

    [Fact]
    public void MultipleEventsAreRejected()
    {
        var turn = new TurnNumber(6UL);

        var input = new TurnResolutionInput(
            WorldState.CreateInitial(),
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(161718UL)));

        var commandValidator = new TestCommandValidator();
        var commandProcessor = new TestCommandProcessor(
            eventCount: 2);
        var eventRevalidator = new TestEventRevalidator();
        var eventExecutor = new TestEventExecutor();

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventRevalidator,
            eventExecutor);

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));

        Assert.Equal(1, commandValidator.CallCount);
        Assert.Equal(1, commandProcessor.CallCount);
        Assert.Equal(0, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);
    }

    [Fact]
    public void MultipleCommandsAreRejected()
    {
        var turn = new TurnNumber(7UL);

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
                new SimulationSeed(192021UL)));

        var commandValidator = new TestCommandValidator();

        var resolver = new TurnResolver(
            commandValidator,
            new TestCommandProcessor(),
            new TestEventRevalidator(),
            new TestEventExecutor());

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));

        Assert.Equal(0, commandValidator.CallCount);
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var resolver = new TurnResolver(
            new TestCommandValidator(),
            new TestCommandProcessor(),
            new TestEventRevalidator(),
            new TestEventExecutor());

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(null!));
    }

    [Fact]
    public void NullCommandValidatorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                null!,
                new TestCommandProcessor(),
                new TestEventRevalidator(),
                new TestEventExecutor()));
    }

    [Fact]
    public void NullCommandProcessorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                new TestCommandValidator(),
                null!,
                new TestEventRevalidator(),
                new TestEventExecutor()));
    }

    [Fact]
    public void NullEventRevalidatorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                new TestCommandValidator(),
                new TestCommandProcessor(),
                null!,
                new TestEventExecutor()));
    }

    [Fact]
    public void NullEventExecutorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                new TestCommandValidator(),
                new TestCommandProcessor(),
                new TestEventRevalidator(),
                null!));
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestCommandValidator
        : ISimulationCommandValidator
    {
        private readonly bool _isValid;
        private readonly IList<string>? _callOrder;

        public TestCommandValidator(
            bool isValid = true,
            IList<string>? callOrder = null)
        {
            _isValid = isValid;
            _callOrder = callOrder;
        }

        public int CallCount { get; private set; }

        public WorldState? WorldState { get; private set; }

        public ISimulationCommand? Command { get; private set; }

        public SimulationContext Context { get; private set; }

        public bool IsValid(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            CallCount++;
            WorldState = worldState;
            Command = command;
            Context = context;
            _callOrder?.Add("validate");

            return _isValid;
        }
    }

    private sealed class TestCommandProcessor
        : ISimulationCommandProcessor
    {
        private readonly int _eventCount;
        private readonly IList<string>? _callOrder;

        public TestCommandProcessor(
            int eventCount = 1,
            IList<string>? callOrder = null)
        {
            _eventCount = eventCount;
            _callOrder = callOrder;
        }

        public int CallCount { get; private set; }

        public IReadOnlyList<ISimulationEvent> Process(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            CallCount++;
            _callOrder?.Add("process");

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
