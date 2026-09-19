using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SimulationCommandValidatorContractTests
{
    [Fact]
    public void ValidatorCanReceiveWorldCommandAndContextAndAcceptCommand()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(1UL);

        var command = new TestCommand(
            new CommandId(turn, 1UL));

        var context = new SimulationContext(
            turn,
            new SimulationSeed(123UL));

        var validator = new TestCommandValidator(
            isValid: true);

        var isValid = validator.IsValid(
            state,
            command,
            context);

        Assert.True(isValid);
        Assert.Same(state, validator.WorldState);
        Assert.Same(command, validator.Command);
        Assert.Equal(context, validator.Context);
    }

    [Fact]
    public void ValidatorCanRejectCommand()
    {
        var state = WorldState.CreateInitial();
        var turn = new TurnNumber(2UL);

        var command = new TestCommand(
            new CommandId(turn, 1UL));

        var context = new SimulationContext(
            turn,
            new SimulationSeed(456UL));

        var validator = new TestCommandValidator(
            isValid: false);

        var isValid = validator.IsValid(
            state,
            command,
            context);

        Assert.False(isValid);
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed class TestCommandValidator
        : ISimulationCommandValidator
    {
        private readonly bool _isValid;

        public TestCommandValidator(
            bool isValid)
        {
            _isValid = isValid;
        }

        public WorldState? WorldState { get; private set; }

        public ISimulationCommand? Command { get; private set; }

        public SimulationContext Context { get; private set; }

        public bool IsValid(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            WorldState = worldState;
            Command = command;
            Context = context;

            return _isValid;
        }
    }
}
