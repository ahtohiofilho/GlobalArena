using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public interface ISimulationCommandProcessor
{
    IReadOnlyList<ISimulationEvent> Process(
        WorldState worldState,
        ISimulationCommand command,
        SimulationContext context);
}
