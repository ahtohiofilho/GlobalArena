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
                Array.Empty<ISimulationEvent>(),
                new SimulationEventLog(
                    Array.Empty<SimulationEventLogEntry>()));
        }

        var events = _commandBatchProcessor.BuildEvents(
            input.WorldState,
            input.Commands,
            input.Context);

        var orderedEvents = _eventOrderer.Order(
            events,
            input.Context);

        var currentWorldState = input.WorldState;

        var eventLogEntries =
            new List<SimulationEventLogEntry>(
                orderedEvents.Count);

        for (var index = 0;
             index < orderedEvents.Count;
             index++)
        {
            var simulationEvent =
                orderedEvents[index];

            var canExecute = _eventRevalidator.CanExecute(
                currentWorldState,
                simulationEvent,
                input.Context);

            if (!canExecute)
            {
                eventLogEntries.Add(
                    new SimulationEventLogEntry(
                        (ulong)index + 1UL,
                        simulationEvent,
                        wasEligible: false,
                        wasExecuted: false));

                continue;
            }

            currentWorldState = _eventExecutor.Execute(
                currentWorldState,
                simulationEvent,
                input.Context);

            eventLogEntries.Add(
                new SimulationEventLogEntry(
                    (ulong)index + 1UL,
                    simulationEvent,
                    wasEligible: true,
                    wasExecuted: true));
        }

        return new TurnResolutionResult(
            currentWorldState,
            orderedEvents,
            new SimulationEventLog(
                eventLogEntries));
    }
}
