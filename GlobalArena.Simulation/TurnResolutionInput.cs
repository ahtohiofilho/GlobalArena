using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class TurnResolutionInput
{
    public WorldState WorldState { get; }

    public IReadOnlyList<ISimulationCommand> Commands { get; }

    public SimulationContext Context { get; }

    public TurnResolutionInput(
        WorldState worldState,
        IEnumerable<ISimulationCommand> commands,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(worldState);
        ArgumentNullException.ThrowIfNull(commands);

        WorldState = worldState;
        Commands = Array.AsReadOnly(commands.ToArray());
        Context = context;
    }
}
