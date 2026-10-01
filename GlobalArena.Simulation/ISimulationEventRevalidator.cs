using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public interface ISimulationEventRevalidator
{
    bool CanExecute(
        WorldState worldState,
        ISimulationEvent simulationEvent,
        SimulationContext context);
}
