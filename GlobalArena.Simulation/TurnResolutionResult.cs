using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class TurnResolutionResult
{
    public WorldState ResultingWorldState { get; }

    public IReadOnlyList<ISimulationEvent> Events { get; }

    public TurnResolutionResult(
        WorldState resultingWorldState,
        IEnumerable<ISimulationEvent> events)
    {
        ArgumentNullException.ThrowIfNull(resultingWorldState);
        ArgumentNullException.ThrowIfNull(events);

        ResultingWorldState = resultingWorldState;
        Events = Array.AsReadOnly(events.ToArray());
    }
}
