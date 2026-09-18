using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TurnResolutionContractTests
{
    [Fact]
    public void InputCarriesWorldStateCommandsAndContext()
    {
        var state = WorldState.CreateInitial();
        var context = new SimulationContext(
            new TurnNumber(3UL),
            new SimulationSeed(123UL));

        var first = new TestCommand(
            new CommandId(context.Turn, 1UL));

        var second = new TestCommand(
            new CommandId(context.Turn, 2UL));

        var input = new TurnResolutionInput(
            state,
            new ISimulationCommand[] { first, second },
            context);

        Assert.Same(state, input.WorldState);
        Assert.Equal(context, input.Context);
        Assert.Equal(2, input.Commands.Count);
        Assert.Same(first, input.Commands[0]);
        Assert.Same(second, input.Commands[1]);
    }

    [Fact]
    public void InputSnapshotsCommandCollection()
    {
        var state = WorldState.CreateInitial();
        var context = new SimulationContext(
            new TurnNumber(4UL),
            new SimulationSeed(456UL));

        var commands = new List<ISimulationCommand>
        {
            new TestCommand(
                new CommandId(context.Turn, 1UL))
        };

        var input = new TurnResolutionInput(
            state,
            commands,
            context);

        commands.Add(
            new TestCommand(
                new CommandId(context.Turn, 2UL)));

        Assert.Single(input.Commands);
    }

    [Fact]
    public void ResultCarriesWorldStateAndEvents()
    {
        var state = WorldState.CreateInitial();
        var commandId = new CommandId(
            new TurnNumber(5UL),
            1UL);

        var first = new TestEvent(
            new EventId(commandId, 1UL));

        var second = new TestEvent(
            new EventId(commandId, 2UL));

        var result = new TurnResolutionResult(
            state,
            new ISimulationEvent[] { first, second });

        Assert.Same(state, result.ResultingWorldState);
        Assert.Equal(2, result.Events.Count);
        Assert.Same(first, result.Events[0]);
        Assert.Same(second, result.Events[1]);
    }

    [Fact]
    public void ResultSnapshotsEventCollection()
    {
        var state = WorldState.CreateInitial();
        var commandId = new CommandId(
            new TurnNumber(6UL),
            1UL);

        var events = new List<ISimulationEvent>
        {
            new TestEvent(
                new EventId(commandId, 1UL))
        };

        var result = new TurnResolutionResult(
            state,
            events);

        events.Add(
            new TestEvent(
                new EventId(commandId, 2UL)));

        Assert.Single(result.Events);
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;
}
