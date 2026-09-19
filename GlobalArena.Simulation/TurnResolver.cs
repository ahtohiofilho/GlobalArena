using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class TurnResolver
{
    private readonly ISimulationCommandProcessor _commandProcessor;
    private readonly ISimulationEventExecutor _eventExecutor;

    public TurnResolver(
        ISimulationCommandProcessor commandProcessor,
        ISimulationEventExecutor eventExecutor)
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(eventExecutor);

        _commandProcessor = commandProcessor;
        _eventExecutor = eventExecutor;
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

        if (events.Count > 1)
        {
            throw new NotSupportedException(
                "Multiple event resolution is not implemented yet.");
        }

        if (events.Count == 0)
        {
            return new TurnResolutionResult(
                input.WorldState,
                events);
        }

        var resultingWorldState = _eventExecutor.Execute(
            input.WorldState,
            events[0],
            input.Context);

        return new TurnResolutionResult(
            resultingWorldState,
            events);
    }
}
