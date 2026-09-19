using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class SimulationEventLogEntry
{
    public ulong ResolutionSequence { get; }

    public ISimulationEvent Event { get; }

    public EventId EventId => Event.Id;

    public bool WasEligible { get; }

    public bool WasExecuted { get; }

    public SimulationEventLogEntry(
        ulong resolutionSequence,
        ISimulationEvent simulationEvent,
        bool wasEligible,
        bool wasExecuted)
    {
        if (resolutionSequence == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolutionSequence),
                "Resolution sequence must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(simulationEvent);

        if (wasExecuted && !wasEligible)
        {
            throw new ArgumentException(
                "An executed event must have been eligible.",
                nameof(wasExecuted));
        }

        ResolutionSequence = resolutionSequence;
        Event = simulationEvent;
        WasEligible = wasEligible;
        WasExecuted = wasExecuted;
    }
}
