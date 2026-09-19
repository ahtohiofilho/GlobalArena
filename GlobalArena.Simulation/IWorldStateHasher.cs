using GlobalArena.World;

namespace GlobalArena.Simulation;

public interface IWorldStateHasher
{
    WorldStateHash Compute(
        WorldState worldState);
}
