using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationCommandBatchProcessorContractTests
{
    [Fact]
    public void BatchProcessorCanReceivePlanningSnapshotCommandsAndContext()
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

        IReadOnlyList<ISimulationCommand> commands =
            new ISimulationCommand[]
            {
                firstCommand,
                secondCommand
            };

        var producedEvent = new TestEvent(
            new EventId(
                firstCommand.Id,
                1UL));

        var processor =
            new TestCommandBatchProcessor(
                new ISimulationEvent[]
                {
                    producedEvent
                });

        var events = processor.BuildEvents(
            state,
            commands,
            context);

        Assert.Same(
            state,
            processor.PlanningWorldState);

        Assert.Same(
            commands,
            processor.Commands);

        Assert.Equal(
            context,
            processor.Context);

        Assert.Same(
            producedEvent,
            Assert.Single(events));
    }

    [Fact]
    public void BatchProcessorCanReturnExplicitEventSequenceWithoutMutatingCommands()
    {
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

        var input = new ISimulationCommand[]
        {
            firstCommand,
            secondCommand
        };

        IReadOnlyList<ISimulationCommand> commands =
            input;

        var firstEvent = new TestEvent(
            new EventId(
                firstCommand.Id,
                1UL));

        var secondEvent = new TestEvent(
            new EventId(
                secondCommand.Id,
                1UL));

        var processor =
            new TestCommandBatchProcessor(
                new ISimulationEvent[]
                {
                    secondEvent,
                    firstEvent
                });

        var events = processor.BuildEvents(
            WorldState.CreateInitial(),
            commands,
            context);

        Assert.Equal(
            new ISimulationCommand[]
            {
                firstCommand,
                secondCommand
            },
            input);

        Assert.Equal(
            new ISimulationEvent[]
            {
                secondEvent,
                firstEvent
            },
            events);
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestCommandBatchProcessor
        : ISimulationCommandBatchProcessor
    {
        private readonly IReadOnlyList<ISimulationEvent> _events;

        public TestCommandBatchProcessor(
            IReadOnlyList<ISimulationEvent> events)
        {
            _events = events;
        }

        public WorldState? PlanningWorldState { get; private set; }

        public IReadOnlyList<ISimulationCommand>? Commands { get; private set; }

        public SimulationContext Context { get; private set; }

        public IReadOnlyList<ISimulationEvent> BuildEvents(
            WorldState planningWorldState,
            IReadOnlyList<ISimulationCommand> commands,
            SimulationContext context)
        {
            PlanningWorldState = planningWorldState;
            Commands = commands;
            Context = context;

            return _events;
        }
    }
}
