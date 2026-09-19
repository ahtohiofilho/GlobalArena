using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationCommandBatchProcessorTests
{
    [Fact]
    public void CommandsAreValidatedAndProcessedAgainstSamePlanningSnapshot()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(123UL));

        var firstCommand = new TestCommand(
            new CommandId(
                turn,
                1UL));

        var secondCommand = new TestCommand(
            new CommandId(
                turn,
                2UL));

        var validator =
            new TrackingCommandValidator();

        var processor =
            new TrackingCommandProcessor();

        var batchProcessor =
            new SimulationCommandBatchProcessor(
                validator,
                processor);

        var events = batchProcessor.BuildEvents(
            state,
            new ISimulationCommand[]
            {
                secondCommand,
                firstCommand
            },
            context);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL
            },
            validator.Commands.Select(
                command =>
                    command.Id.Sequence));

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL
            },
            processor.Commands.Select(
                command =>
                    command.Id.Sequence));

        Assert.All(
            validator.WorldStates,
            worldState =>
                Assert.Same(
                    state,
                    worldState));

        Assert.All(
            processor.WorldStates,
            worldState =>
                Assert.Same(
                    state,
                    worldState));

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL
            },
            events.Select(
                simulationEvent =>
                    simulationEvent
                        .Id
                        .OriginCommandId
                        .Sequence));
    }

    [Fact]
    public void InputOrderDoesNotChangeAggregatedEventSequence()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(2UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(456UL));

        var firstCommand = new TestCommand(
            new CommandId(
                turn,
                1UL));

        var secondCommand = new TestCommand(
            new CommandId(
                turn,
                2UL));

        var firstBatch =
            new SimulationCommandBatchProcessor(
                new TrackingCommandValidator(),
                new TrackingCommandProcessor());

        var secondBatch =
            new SimulationCommandBatchProcessor(
                new TrackingCommandValidator(),
                new TrackingCommandProcessor());

        var firstEvents = firstBatch.BuildEvents(
            state,
            new ISimulationCommand[]
            {
                firstCommand,
                secondCommand
            },
            context);

        var secondEvents = secondBatch.BuildEvents(
            state,
            new ISimulationCommand[]
            {
                secondCommand,
                firstCommand
            },
            context);

        Assert.Equal(
            firstEvents.Select(
                simulationEvent =>
                    simulationEvent.Id),
            secondEvents.Select(
                simulationEvent =>
                    simulationEvent.Id));
    }

    [Fact]
    public void InvalidCommandsAreSkippedWhileValidCommandsContinue()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(3UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(789UL));

        var firstCommand = new TestCommand(
            new CommandId(
                turn,
                1UL));

        var secondCommand = new TestCommand(
            new CommandId(
                turn,
                2UL));

        var validator =
            new TrackingCommandValidator(
                validSequence: 2UL);

        var processor =
            new TrackingCommandProcessor();

        var batchProcessor =
            new SimulationCommandBatchProcessor(
                validator,
                processor);

        var events = batchProcessor.BuildEvents(
            state,
            new ISimulationCommand[]
            {
                firstCommand,
                secondCommand
            },
            context);

        Assert.Equal(
            2,
            validator.CallCount);

        Assert.Equal(
            1,
            processor.CallCount);

        var simulationEvent =
            Assert.Single(events);

        Assert.Equal(
            2UL,
            simulationEvent
                .Id
                .OriginCommandId
                .Sequence);
    }

    [Fact]
    public void DuplicateCommandIdsAreRejectedBeforeValidation()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(4UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(101112UL));

        var commandId =
            new CommandId(
                turn,
                1UL);

        var validator =
            new TrackingCommandValidator();

        var processor =
            new TrackingCommandProcessor();

        var batchProcessor =
            new SimulationCommandBatchProcessor(
                validator,
                processor);

        Assert.Throws<InvalidOperationException>(
            () => batchProcessor.BuildEvents(
                state,
                new ISimulationCommand[]
                {
                    new TestCommand(commandId),
                    new TestCommand(commandId)
                },
                context));

        Assert.Equal(
            0,
            validator.CallCount);

        Assert.Equal(
            0,
            processor.CallCount);
    }

    [Fact]
    public void NullCommandValidatorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SimulationCommandBatchProcessor(
                null!,
                new TrackingCommandProcessor()));
    }

    [Fact]
    public void NullCommandProcessorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SimulationCommandBatchProcessor(
                new TrackingCommandValidator(),
                null!));
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TrackingCommandValidator
        : ISimulationCommandValidator
    {
        private readonly ulong? _validSequence;

        public TrackingCommandValidator(
            ulong? validSequence = null)
        {
            _validSequence = validSequence;
        }

        public int CallCount { get; private set; }

        public List<WorldState> WorldStates { get; } =
            new();

        public List<ISimulationCommand> Commands { get; } =
            new();

        public bool IsValid(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            CallCount++;
            WorldStates.Add(worldState);
            Commands.Add(command);

            return !_validSequence.HasValue
                || command.Id.Sequence
                    == _validSequence.Value;
        }
    }

    private sealed class TrackingCommandProcessor
        : ISimulationCommandProcessor
    {
        public int CallCount { get; private set; }

        public List<WorldState> WorldStates { get; } =
            new();

        public List<ISimulationCommand> Commands { get; } =
            new();

        public IReadOnlyList<ISimulationEvent> Process(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            CallCount++;
            WorldStates.Add(worldState);
            Commands.Add(command);

            return new ISimulationEvent[]
            {
                new TestEvent(
                    new EventId(
                        command.Id,
                        1UL))
            };
        }
    }
}
