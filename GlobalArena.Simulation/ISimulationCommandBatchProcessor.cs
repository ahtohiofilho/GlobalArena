using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public interface ISimulationCommandBatchProcessor
{
    IReadOnlyList<ISimulationEvent> BuildEvents(
        WorldState planningWorldState,
        IReadOnlyList<ISimulationCommand> commands,
        SimulationContext context);
}
