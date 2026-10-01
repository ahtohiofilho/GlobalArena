using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class SimulationReplayInput
{
    public WorldState InitialWorldState { get; }

    public SimulationEventLog EventLog { get; }

    public SimulationContext Context { get; }

    public SimulationReplayInput(
        WorldState initialWorldState,
        SimulationEventLog eventLog,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(initialWorldState);
        ArgumentNullException.ThrowIfNull(eventLog);

        InitialWorldState = initialWorldState;
        EventLog = eventLog;
        Context = context;
    }
}
