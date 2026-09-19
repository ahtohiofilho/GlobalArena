using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class DeterministicTurnResolutionTests
{
    [Fact]
    public void IdenticalIndependentRunsProduceSameObservableStateAndEventOrder()
    {
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(0UL));

        var firstInputState =
            WorldState.CreateInitial();

        var secondInputState =
            WorldState.CreateInitial();

        var firstCommand =
            new RevisionCommand(
                new CommandId(
                    turn,
                    1UL),
                EventCount: 3);

        var secondCommand =
            new RevisionCommand(
                new CommandId(
                    turn,
                    1UL),
                EventCount: 3);

        var firstResult = CreateResolver().Resolve(
            new TurnResolutionInput(
                firstInputState,
                new ISimulationCommand[]
                {
                    firstCommand
                },
                context));

        var secondResult = CreateResolver().Resolve(
            new TurnResolutionInput(
                secondInputState,
                new ISimulationCommand[]
                {
                    secondCommand
                },
                context));

        Assert.NotSame(
            firstInputState,
            firstResult.ResultingWorldState);

        Assert.NotSame(
            secondInputState,
            secondResult.ResultingWorldState);

        Assert.Equal(
            0UL,
            firstInputState.Revision);

        Assert.Equal(
            0UL,
            secondInputState.Revision);

        Assert.Equal(
            3UL,
            firstResult.ResultingWorldState.Revision);

        Assert.Equal(
            firstResult.ResultingWorldState.Revision,
            secondResult.ResultingWorldState.Revision);

        Assert.Equal(
            new ulong[]
            {
                3UL,
                1UL,
                2UL
            },
            firstResult.Events.Select(
                simulationEvent =>
                    simulationEvent.Id.Sequence));

        Assert.Equal(
            firstResult.Events.Select(
                simulationEvent =>
                    simulationEvent.Id),
            secondResult.Events.Select(
                simulationEvent =>
                    simulationEvent.Id));
    }

    [Fact]
    public void EquivalentMultiCommandBatchesIgnoreInputArrivalOrder()
    {
        var turn = new TurnNumber(2UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(987UL));

        var firstResult = CreateResolver().Resolve(
            new TurnResolutionInput(
                WorldState.CreateInitial(),
                new ISimulationCommand[]
                {
                    new RevisionCommand(
                        new CommandId(
                            turn,
                            1UL),
                        EventCount: 1),
                    new RevisionCommand(
                        new CommandId(
                            turn,
                            2UL),
                        EventCount: 1)
                },
                context));

        var secondResult = CreateResolver().Resolve(
            new TurnResolutionInput(
                WorldState.CreateInitial(),
                new ISimulationCommand[]
                {
                    new RevisionCommand(
                        new CommandId(
                            turn,
                            2UL),
                        EventCount: 1),
                    new RevisionCommand(
                        new CommandId(
                            turn,
                            1UL),
                        EventCount: 1)
                },
                context));

        Assert.Equal(
            2UL,
            firstResult.ResultingWorldState.Revision);

        Assert.Equal(
            firstResult.ResultingWorldState.Revision,
            secondResult.ResultingWorldState.Revision);

        Assert.Equal(
            firstResult.Events.Select(
                simulationEvent =>
                    simulationEvent.Id),
            secondResult.Events.Select(
                simulationEvent =>
                    simulationEvent.Id));

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL
            },
            firstResult.Events
                .Select(
                    simulationEvent =>
                        simulationEvent
                            .Id
                            .OriginCommandId
                            .Sequence)
                .OrderBy(sequence => sequence));
    }

    private static TurnResolver CreateResolver()
    {
        return new TurnResolver(
            new RevisionCommandValidator(),
            new RevisionCommandProcessor(),
            new SeededSimulationEventOrderer(),
            new RevisionEventRevalidator(),
            new RevisionEventExecutor());
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
                Assert.IsType<RevisionCommand>(command);

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

    private sealed class RevisionEventRevalidator
        : ISimulationEventRevalidator
    {
        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            return simulationEvent
                is RevisionEvent;
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
