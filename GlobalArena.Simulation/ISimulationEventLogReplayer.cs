using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public interface ISimulationEventLogReplayer
{
    WorldState Replay(
        SimulationReplayInput input);
}
