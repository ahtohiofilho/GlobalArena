using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TurnResolverEventLogTests
{
    [Fact]
    public void EmptyTurnProducesEmptyEventLog()
    {
        var result = CreateResolver(
            new AlwaysValidCommandValidator(),
            new ThreeEventCommandProcessor(),
            new PreserveEventOrderer(),
            new SequenceEventRevalidator(),
            new RevisionEventExecutor())
            .Resolve(
                new TurnResolutionInput(
                    WorldState.CreateInitial(),
                    Array.Empty<ISimulationCommand>(),
                    new SimulationContext(
                        new TurnNumber(1UL),
                        new SimulationSeed(1UL))));

        Assert.Empty(
            result.EventLog.Entries);
    }

    [Fact]
    public void AcceptedAndRejectedEventsAreCapturedInResolutionOrder()
    {
        var turn = new TurnNumber(2UL);

        var result = CreateResolver(
            new AlwaysValidCommandValidator(),
            new ThreeEventCommandProcessor(),
            new PreserveEventOrderer(),
            new SequenceEventRevalidator(
                true,
                false,
                true),
            new RevisionEventExecutor())
            .Resolve(
                new TurnResolutionInput(
                    WorldState.CreateInitial(),
                    new ISimulationCommand[]
                    {
                        new TestCommand(
                            new CommandId(
                                turn,
                                1UL))
                    },
                    new SimulationContext(
                        turn,
                        new SimulationSeed(2UL))));

        Assert.Equal(
            2UL,
            result.ResultingWorldState.Revision);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL
            },
            result.EventLog.Entries.Select(
                entry =>
                    entry.ResolutionSequence));

        Assert.Equal(
            result.Events.Select(
                simulationEvent =>
                    simulationEvent.Id),
            result.EventLog.Entries.Select(
                entry =>
                    entry.EventId));

        Assert.Equal(
            new[]
            {
                true,
                false,
                true
            },
            result.EventLog.Entries.Select(
                entry =>
                    entry.WasEligible));

        Assert.Equal(
            new[]
            {
                true,
                false,
                true
            },
            result.EventLog.Entries.Select(
                entry =>
                    entry.WasExecuted));
    }

    [Fact]
    public void IdenticalResolutionsProduceEquivalentEventLogs()
    {
        var turn = new TurnNumber(3UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(0UL));

        var firstResult = CreateResolver(
            new AlwaysValidCommandValidator(),
            new ThreeEventCommandProcessor(),
            new SeededSimulationEventOrderer(),
            new SequenceEventRevalidator(
                true,
                true,
                true),
            new RevisionEventExecutor())
            .Resolve(
                new TurnResolutionInput(
                    WorldState.CreateInitial(),
                    new ISimulationCommand[]
                    {
                        new TestCommand(
                            new CommandId(
                                turn,
                                1UL))
                    },
                    context));

        var secondResult = CreateResolver(
            new AlwaysValidCommandValidator(),
            new ThreeEventCommandProcessor(),
            new SeededSimulationEventOrderer(),
            new SequenceEventRevalidator(
                true,
                true,
                true),
            new RevisionEventExecutor())
            .Resolve(
                new TurnResolutionInput(
                    WorldState.CreateInitial(),
                    new ISimulationCommand[]
                    {
                        new TestCommand(
                            new CommandId(
                                turn,
                                1UL))
                    },
                    context));

        Assert.Equal(
            firstResult.EventLog.Entries.Select(
                entry =>
                    entry.ResolutionSequence),
            secondResult.EventLog.Entries.Select(
                entry =>
                    entry.ResolutionSequence));

        Assert.Equal(
            firstResult.EventLog.Entries.Select(
                entry =>
                    entry.EventId),
            secondResult.EventLog.Entries.Select(
                entry =>
                    entry.EventId));

        Assert.Equal(
            firstResult.EventLog.Entries.Select(
                entry =>
                    entry.WasEligible),
            secondResult.EventLog.Entries.Select(
                entry =>
                    entry.WasEligible));

        Assert.Equal(
            firstResult.EventLog.Entries.Select(
                entry =>
                    entry.WasExecuted),
            secondResult.EventLog.Entries.Select(
                entry =>
                    entry.WasExecuted));
    }

    private static TurnResolver CreateResolver(
        ISimulationCommandValidator validator,
        ISimulationCommandProcessor processor,
        ISimulationEventOrderer orderer,
        ISimulationEventRevalidator revalidator,
        ISimulationEventExecutor executor)
    {
        return new TurnResolver(
            validator,
            processor,
            orderer,
            revalidator,
            executor);
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class AlwaysValidCommandValidator
        : ISimulationCommandValidator
    {
        public bool IsValid(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            return true;
        }
    }

    private sealed class ThreeEventCommandProcessor
        : ISimulationCommandProcessor
    {
        public IReadOnlyList<ISimulationEvent> Process(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            return new ISimulationEvent[]
            {
                new TestEvent(
                    new EventId(
                        command.Id,
                        1UL)),
                new TestEvent(
                    new EventId(
                        command.Id,
                        2UL)),
                new TestEvent(
                    new EventId(
                        command.Id,
                        3UL))
            };
        }
    }

    private sealed class PreserveEventOrderer
        : ISimulationEventOrderer
    {
        public IReadOnlyList<ISimulationEvent> Order(
            IReadOnlyList<ISimulationEvent> events,
            SimulationContext context)
        {
            return Array.AsReadOnly(
                events.ToArray());
        }
    }

    private sealed class SequenceEventRevalidator
        : ISimulationEventRevalidator
    {
        private readonly IReadOnlyList<bool> _outcomes;
        private int _index;

        public SequenceEventRevalidator(
            params bool[] outcomes)
        {
            _outcomes = outcomes;
        }

        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            if (_index >= _outcomes.Count)
            {
                return true;
            }

            return _outcomes[_index++];
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
            return worldState.AdvanceRevision();
        }
    }
}
