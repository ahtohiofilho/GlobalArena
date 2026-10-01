using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationEventRevalidatorContractTests
{
    [Fact]
    public void RevalidatorCanReceiveWorldEventAndContextAndAllowExecution()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(123UL));

        var simulationEvent = new TestEvent(
            new EventId(
                new CommandId(turn, 1UL),
                1UL));

        var revalidator = new TestEventRevalidator(
            canExecute: true);

        var canExecute = revalidator.CanExecute(
            state,
            simulationEvent,
            context);

        Assert.True(canExecute);
        Assert.Same(state, revalidator.WorldState);
        Assert.Same(simulationEvent, revalidator.SimulationEvent);
        Assert.Equal(context, revalidator.Context);
    }

    [Fact]
    public void RevalidatorCanRejectExecution()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(2UL);
        var context = new SimulationContext(
            turn,
            new SimulationSeed(456UL));

        var simulationEvent = new TestEvent(
            new EventId(
                new CommandId(turn, 1UL),
                1UL));

        var revalidator = new TestEventRevalidator(
            canExecute: false);

        var canExecute = revalidator.CanExecute(
            state,
            simulationEvent,
            context);

        Assert.False(canExecute);
    }

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TestEventRevalidator
        : ISimulationEventRevalidator
    {
        private readonly bool _canExecute;

        public TestEventRevalidator(
            bool canExecute)
        {
            _canExecute = canExecute;
        }

        public WorldState? WorldState { get; private set; }

        public ISimulationEvent? SimulationEvent { get; private set; }

        public SimulationContext Context { get; private set; }

        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            WorldState = worldState;
            SimulationEvent = simulationEvent;
            Context = context;

            return _canExecute;
        }
    }
}
