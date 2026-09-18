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

        var resolver = new TurnResolver();

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

        var resolver = new TurnResolver();

        var result = resolver.Resolve(input);

        Assert.Empty(result.Events);
    }

    [Fact]
    public void CommandsAreRejectedUntilCommandResolutionExists()
    {
        var turn = new TurnNumber(2UL);

        var input = new TurnResolutionInput(
            WorldState.CreateInitial(),
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(turn, 1UL))
            },
            new SimulationContext(
                turn,
                new SimulationSeed(456UL)));

        var resolver = new TurnResolver();

        Assert.Throws<NotSupportedException>(
            () => resolver.Resolve(input));
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var resolver = new TurnResolver();

        Assert.Throws<ArgumentNullException>(
            () => resolver.Resolve(null!));
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;
}
