using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public interface ISimulationCommandValidator
{
    bool IsValid(
        WorldState worldState,
        ISimulationCommand command,
        SimulationContext context);
}
