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
        var eventOrderer = new TestEventOrderer();
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
            eventOrderer,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Equal(0, commandValidator.CallCount);
        Assert.Equal(0, commandProcessor.CallCount);
        Assert.Equal(0, eventOrderer.CallCount);
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
            new TestEventOrderer(),
            new TestEventRevalidator(),
            new TestEventExecutor());

        var result = resolver.Resolve(input);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void SingleCommandSingleEventFollowsCompleteOrderedPipeline()
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

        var eventOrderer = new TestEventOrderer(
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            callOrder: callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder: callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventOrderer,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(
            Assert.Single(eventExecutor.ResultingStates),
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

        Assert.Equal(1, eventOrderer.CallCount);
        Assert.Equal(input.Context, eventOrderer.Context);
        Assert.Single(eventOrderer.Events!);

        Assert.Equal(1, eventRevalidator.CallCount);
        Assert.Same(
            state,
            Assert.Single(eventRevalidator.WorldStates));
        Assert.Same(
            simulationEvent,
            Assert.Single(eventRevalidator.Events));

        Assert.Equal(1, eventExecutor.CallCount);
        Assert.Same(
            state,
            Assert.Single(eventExecutor.InputStates));

        Assert.Equal(
            new[]
            {
                "validate",
                "process",
                "order",
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

        var eventOrderer = new TestEventOrderer(
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            callOrder: callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder: callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventOrderer,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Empty(result.Events);
        Assert.Equal(1, commandValidator.CallCount);
        Assert.Equal(0, commandProcessor.CallCount);
        Assert.Equal(1, eventOrderer.CallCount);
        Assert.Equal(0, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "validate",
                "order"
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

        var eventOrderer = new TestEventOrderer(
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            outcomes: new[]
            {
                false
            },
            callOrder: callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder: callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventOrderer,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);

        var simulationEvent = Assert.Single(result.Events);

        Assert.Equal(
            new EventId(command.Id, 1UL),
            simulationEvent.Id);

        Assert.Equal(1, eventOrderer.CallCount);
        Assert.Equal(1, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "validate",
                "process",
                "order",
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

        var eventOrderer = new TestEventOrderer(
            callOrder: callOrder);

        var eventRevalidator = new TestEventRevalidator(
            callOrder: callOrder);

        var eventExecutor = new TestEventExecutor(
            callOrder: callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventOrderer,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
        Assert.Empty(result.Events);
        Assert.Equal(1, commandValidator.CallCount);
        Assert.Equal(1, commandProcessor.CallCount);
        Assert.Equal(1, eventOrderer.CallCount);
        Assert.Equal(0, eventRevalidator.CallCount);
        Assert.Equal(0, eventExecutor.CallCount);

        Assert.Equal(
            new[]
            {
                "validate",
                "process",
                "order"
            },
            callOrder);
    }

    [Fact]
    public void MultipleEventsAreDeterministicallyOrderedAndExecutedSequentially()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(6UL);

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
                new SimulationSeed(0UL)));

        var firstResult = WorldState.CreateInitial();
        var secondResult = WorldState.CreateInitial();
        var thirdResult = WorldState.CreateInitial();

        var eventRevalidator = new TestEventRevalidator();

        var eventExecutor = new TestEventExecutor(
            resultingStates: new[]
            {
                firstResult,
                secondResult,
                thirdResult
            });

        var resolver = new TurnResolver(
            new TestCommandValidator(),
            new TestCommandProcessor(eventCount: 3),
            new SeededSimulationEventOrderer(),
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Equal(
            new ulong[]
            {
                3UL,
                1UL,
                2UL
            },
            result.Events.Select(
                simulationEvent =>
                    simulationEvent.Id.Sequence));

        Assert.Equal(3, eventRevalidator.CallCount);
        Assert.Equal(3, eventExecutor.CallCount);

        Assert.Same(
            state,
            eventRevalidator.WorldStates[0]);

        Assert.Same(
            firstResult,
            eventRevalidator.WorldStates[1]);

        Assert.Same(
            secondResult,
            eventRevalidator.WorldStates[2]);

        Assert.Same(
            state,
            eventExecutor.InputStates[0]);

        Assert.Same(
            firstResult,
            eventExecutor.InputStates[1]);

        Assert.Same(
            secondResult,
            eventExecutor.InputStates[2]);

        Assert.Equal(
            new ulong[]
            {
                3UL,
                1UL,
                2UL
            },
            eventExecutor.Events.Select(
                simulationEvent =>
                    simulationEvent.Id.Sequence));

        Assert.Same(
            thirdResult,
            result.ResultingWorldState);
    }

    [Fact]
    public void RejectedEventInSequencePreservesCurrentStateAndResolutionContinues()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(7UL);

        var input = new TurnResolutionInput(
            state,
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(161718UL)));

        var firstResult = WorldState.CreateInitial();
        var secondResult = WorldState.CreateInitial();

        var callOrder = new List<string>();

        var eventRevalidator = new TestEventRevalidator(
            outcomes: new[]
            {
                true,
                false,
                true
            },
            callOrder: callOrder);

        var eventExecutor = new TestEventExecutor(
            resultingStates: new[]
            {
                firstResult,
                secondResult
            },
            callOrder: callOrder);

        var resolver = new TurnResolver(
            new TestCommandValidator(
                callOrder: callOrder),
            new TestCommandProcessor(
                eventCount: 3,
                callOrder: callOrder),
            new TestEventOrderer(
                callOrder: callOrder),
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL
            },
            result.Events.Select(
                simulationEvent =>
                    simulationEvent.Id.Sequence));

        Assert.Equal(3, eventRevalidator.CallCount);
        Assert.Equal(2, eventExecutor.CallCount);

        Assert.Same(
            state,
            eventRevalidator.WorldStates[0]);

        Assert.Same(
            firstResult,
            eventRevalidator.WorldStates[1]);

        Assert.Same(
            firstResult,
            eventRevalidator.WorldStates[2]);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                3UL
            },
            eventExecutor.Events.Select(
                simulationEvent =>
                    simulationEvent.Id.Sequence));

        Assert.Same(
            secondResult,
            result.ResultingWorldState);

        Assert.Equal(
            new[]
            {
                "validate",
                "process",
                "order",
                "revalidate",
                "execute",
                "revalidate",
                "revalidate",
                "execute"
            },
            callOrder);
    }

    [Fact]
    public void MultipleCommandsAreBuiltBeforeOrderingAndExecutedSequentially()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(8UL);

        var input = new TurnResolutionInput(
            state,
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 2UL)),
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(192021UL)));

        var callOrder = new List<string>();

        var commandValidator =
            new TestCommandValidator(
                callOrder: callOrder);

        var commandProcessor =
            new TestCommandProcessor(
                callOrder: callOrder);

        var eventOrderer =
            new TestEventOrderer(
                callOrder: callOrder);

        var eventRevalidator =
            new TestEventRevalidator(
                callOrder: callOrder);

        var eventExecutor =
            new TestEventExecutor(
                callOrder: callOrder);

        var resolver = new TurnResolver(
            commandValidator,
            commandProcessor,
            eventOrderer,
            eventRevalidator,
            eventExecutor);

        var result = resolver.Resolve(input);

        Assert.Equal(
            2,
            commandValidator.CallCount);

        Assert.Equal(
            2,
            commandProcessor.CallCount);

        Assert.Equal(
            1,
            eventOrderer.CallCount);

        Assert.Equal(
            2,
            eventRevalidator.CallCount);

        Assert.Equal(
            2,
            eventExecutor.CallCount);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL
            },
            result.Events.Select(
                simulationEvent =>
                    simulationEvent
                        .Id
                        .OriginCommandId
                        .Sequence));

        Assert.Equal(
            new[]
            {
                "validate",
                "process",
                "validate",
                "process",
                "order",
                "revalidate",
                "execute",
                "revalidate",
                "execute"
            },
            callOrder);
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var resolver = new TurnResolver(
            new TestCommandValidator(),
            new TestCommandProcessor(),
            new TestEventOrderer(),
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
                new TestEventOrderer(),
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
                new TestEventOrderer(),
                new TestEventRevalidator(),
                new TestEventExecutor()));
    }

    [Fact]
    public void NullEventOrdererIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(
                new TestCommandValidator(),
                new TestCommandProcessor(),
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
                new TestEventOrderer(),
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
                new TestEventOrderer(),
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

    private sealed class TestEventOrderer
        : ISimulationEventOrderer
    {
        private readonly IReadOnlyList<int>? _order;
        private readonly IList<string>? _callOrder;

        public TestEventOrderer(
            IReadOnlyList<int>? order = null,
            IList<string>? callOrder = null)
        {
            _order = order;
            _callOrder = callOrder;
        }

        public int CallCount { get; private set; }

        public IReadOnlyList<ISimulationEvent>? Events { get; private set; }

        public SimulationContext Context { get; private set; }

        public IReadOnlyList<ISimulationEvent> Order(
            IReadOnlyList<ISimulationEvent> events,
            SimulationContext context)
        {
            CallCount++;
            Events = events;
            Context = context;
            _callOrder?.Add("order");

            if (_order is null)
            {
                return Array.AsReadOnly(
                    events.ToArray());
            }

            return Array.AsReadOnly(
                _order
                    .Select(index => events[index])
                    .ToArray());
        }
    }

    private sealed class TestEventRevalidator
        : ISimulationEventRevalidator
    {
        private readonly IReadOnlyList<bool>? _outcomes;
        private readonly IList<string>? _callOrder;

        public TestEventRevalidator(
            IReadOnlyList<bool>? outcomes = null,
            IList<string>? callOrder = null)
        {
            _outcomes = outcomes;
            _callOrder = callOrder;
        }

        public int CallCount { get; private set; }

        public List<WorldState> WorldStates { get; } = new();

        public List<ISimulationEvent> Events { get; } = new();

        public List<SimulationContext> Contexts { get; } = new();

        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            CallCount++;
            WorldStates.Add(worldState);
            Events.Add(simulationEvent);
            Contexts.Add(context);
            _callOrder?.Add("revalidate");

            if (_outcomes is null)
            {
                return true;
            }

            return _outcomes[CallCount - 1];
        }
    }

    private sealed class TestEventExecutor
        : ISimulationEventExecutor
    {
        private readonly IReadOnlyList<WorldState>? _resultingStates;
        private readonly IList<string>? _callOrder;

        public TestEventExecutor(
            IReadOnlyList<WorldState>? resultingStates = null,
            IList<string>? callOrder = null)
        {
            _resultingStates = resultingStates;
            _callOrder = callOrder;
        }

        public int CallCount { get; private set; }

        public List<WorldState> InputStates { get; } = new();

        public List<ISimulationEvent> Events { get; } = new();

        public List<SimulationContext> Contexts { get; } = new();

        public List<WorldState> ResultingStates { get; } = new();

        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            CallCount++;
            InputStates.Add(worldState);
            Events.Add(simulationEvent);
            Contexts.Add(context);
            _callOrder?.Add("execute");

            var resultingState =
                _resultingStates is null
                    ? WorldState.CreateInitial()
                    : _resultingStates[CallCount - 1];

            ResultingStates.Add(resultingState);

            return resultingState;
        }
    }
}
