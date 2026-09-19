using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class TurnResolver
{
    private readonly ISimulationCommandProcessor _commandProcessor;

    public TurnResolver(
        ISimulationCommandProcessor commandProcessor)
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);

        _commandProcessor = commandProcessor;
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Commands.Count > 1)
        {
            throw new NotSupportedException(
                "Multiple command resolution is not implemented yet.");
        }

        if (input.Commands.Count == 0)
        {
            return new TurnResolutionResult(
                input.WorldState,
                Array.Empty<ISimulationEvent>());
        }

        var events = _commandProcessor.Process(
            input.WorldState,
            input.Commands[0],
            input.Context);

        return new TurnResolutionResult(
            input.WorldState,
            events);
    }
}
