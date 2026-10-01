using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationEventExecutorContractTests
{
    [Fact]
    public void ExecutorCanReceiveWorldEventAndContextAndProduceWorldState()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(1UL);

        var context = new SimulationContext(
            turn,
            new SimulationSeed(123UL));

        var commandId = new CommandId(
            turn,
            1UL);

        var simulationEvent = new TestEvent(
            new EventId(commandId, 1UL));

        ISimulationEventExecutor executor =
            new TestEventExecutor();

        var resultingState = executor.Execute(
            state,
            simulationEvent,
            context);

        Assert.NotNull(resultingState);
        Assert.NotSame(state, resultingState);
    }

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestEventExecutor
        : ISimulationEventExecutor
    {
        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            return WorldState.CreateInitial();
        }
    }
}
