using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public interface ISimulationEventOrderer
{
    IReadOnlyList<ISimulationEvent> Order(
        IReadOnlyList<ISimulationEvent> events,
        SimulationContext context);
}
