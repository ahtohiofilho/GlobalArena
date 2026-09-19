using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class TurnResolver
{
    private readonly ISimulationCommandProcessor _commandProcessor;
    private readonly ISimulationEventRevalidator _eventRevalidator;
    private readonly ISimulationEventExecutor _eventExecutor;

    public TurnResolver(
        ISimulationCommandProcessor commandProcessor,
        ISimulationEventRevalidator eventRevalidator,
        ISimulationEventExecutor eventExecutor)
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(eventRevalidator);
        ArgumentNullException.ThrowIfNull(eventExecutor);

        _commandProcessor = commandProcessor;
        _eventRevalidator = eventRevalidator;
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

        var simulationEvent = events[0];

        var canExecute = _eventRevalidator.CanExecute(
            input.WorldState,
            simulationEvent,
            input.Context);

        if (!canExecute)
        {
            return new TurnResolutionResult(
                input.WorldState,
                events);
        }

        var resultingWorldState = _eventExecutor.Execute(
            input.WorldState,
            simulationEvent,
            input.Context);

        return new TurnResolutionResult(
            resultingWorldState,
            events);
    }
}
