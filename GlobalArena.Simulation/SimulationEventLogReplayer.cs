using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class SimulationEventLogReplayer
    : ISimulationEventLogReplayer
{
    private readonly ISimulationEventExecutor _eventExecutor;

    public SimulationEventLogReplayer(
        ISimulationEventExecutor eventExecutor)
    {
        ArgumentNullException.ThrowIfNull(eventExecutor);

        _eventExecutor = eventExecutor;
    }

    public WorldState Replay(
        SimulationReplayInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var currentWorldState =
            input.InitialWorldState;

        foreach (var entry in input.EventLog.Entries)
        {
            if (!entry.WasExecuted)
            {
                continue;
            }

            currentWorldState =
                _eventExecutor.Execute(
                    currentWorldState,
                    entry.Event,
                    input.Context);
        }

        return currentWorldState;
    }
}
