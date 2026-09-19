using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public interface ISimulationEventExecutor
{
    WorldState Execute(
        WorldState worldState,
        ISimulationEvent simulationEvent,
        SimulationContext context);
}
