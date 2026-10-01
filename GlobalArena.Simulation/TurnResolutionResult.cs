using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class TurnResolutionResult
{
    public WorldState ResultingWorldState { get; }

    public IReadOnlyList<ISimulationEvent> Events { get; }

    public SimulationEventLog EventLog { get; }

    public TurnResolutionResult(
        WorldState resultingWorldState,
        IEnumerable<ISimulationEvent> events,
        SimulationEventLog eventLog)
    {
        ArgumentNullException.ThrowIfNull(resultingWorldState);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(eventLog);

        ResultingWorldState = resultingWorldState;
        Events = Array.AsReadOnly(events.ToArray());
        EventLog = eventLog;
    }
}
