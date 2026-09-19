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

        var input = new TurnResolutionInput(
            state,
            Array.Empty<ISimulationCommand>(),
            new SimulationContext(
                new TurnNumber(1UL),
                new SimulationSeed(123UL)));

        var resolver = new TurnResolver(
            new TestCommandProcessor());

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);
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
            new TestCommandProcessor());

        var result = resolver.Resolve(input);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void SingleCommandIsProcessedAndProducesEvents()
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

        var resolver = new TurnResolver(
            new TestCommandProcessor());

        var result = resolver.Resolve(input);

        Assert.Same(state, result.ResultingWorldState);

        var simulationEvent = Assert.Single(result.Events);

        Assert.Equal(
            new EventId(command.Id, 1UL),
            simulationEvent.Id);
    }

    [Fact]
    public void MultipleCommandsAreRejected()
    {
        var turn = new TurnNumber(3UL);

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
                new SimulationSeed(789UL)));

        var resolver = new TurnResolver(
            new TestCommandProcessor());

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var resolver = new TurnResolver(
            new TestCommandProcessor());

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(null!));
    }

    [Fact]
    public void NullCommandProcessorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnResolver(null!));
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
