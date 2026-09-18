using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationCommandProcessorContractTests
{
    [Fact]
    public void ProcessorCanReceiveWorldCommandAndContextAndProduceEvents()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(1UL);

        var context = new SimulationContext(
            turn,
            new SimulationSeed(123UL));

        var command = new TestCommand(
            new CommandId(turn, 1UL));

        ISimulationCommandProcessor processor =
            new TestCommandProcessor();

        var events = processor.Process(
            state,
            command,
            context);

        var simulationEvent = Assert.Single(events);

        Assert.Equal(
            new EventId(command.Id, 1UL),
            simulationEvent.Id);
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestCommandProcessor
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
                    new EventId(command.Id, 1UL))
            };
        }
    }
}
