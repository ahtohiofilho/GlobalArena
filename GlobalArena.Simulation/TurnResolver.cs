using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class TurnResolver
{
    private readonly ISimulationCommandBatchProcessor _commandBatchProcessor;
    private readonly ISimulationEventOrderer _eventOrderer;
    private readonly ISimulationEventRevalidator _eventRevalidator;
    private readonly ISimulationEventExecutor _eventExecutor;

    public TurnResolver(
        ISimulationCommandValidator commandValidator,
        ISimulationCommandProcessor commandProcessor,
        ISimulationEventOrderer eventOrderer,
        ISimulationEventRevalidator eventRevalidator,
        ISimulationEventExecutor eventExecutor)
    {
        ArgumentNullException.ThrowIfNull(commandValidator);
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(eventOrderer);
        ArgumentNullException.ThrowIfNull(eventRevalidator);
        ArgumentNullException.ThrowIfNull(eventExecutor);

        _commandBatchProcessor =
            new SimulationCommandBatchProcessor(
                commandValidator,
                commandProcessor);

        _eventOrderer = eventOrderer;
        _eventRevalidator = eventRevalidator;
        _eventExecutor = eventExecutor;
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Commands.Count == 0)
        {
            return new TurnResolutionResult(
                input.WorldState,
                Array.Empty<ISimulationEvent>());
        }

        var events = _commandBatchProcessor.BuildEvents(
            input.WorldState,
            input.Commands,
            input.Context);

        var orderedEvents = _eventOrderer.Order(
            events,
            input.Context);

        var currentWorldState = input.WorldState;

        foreach (var simulationEvent in orderedEvents)
        {
            var canExecute = _eventRevalidator.CanExecute(
                currentWorldState,
                simulationEvent,
                input.Context);

            if (!canExecute)
            {
                continue;
            }

            currentWorldState = _eventExecutor.Execute(
                currentWorldState,
                simulationEvent,
                input.Context);
        }

        return new TurnResolutionResult(
            currentWorldState,
            orderedEvents);
    }
}
