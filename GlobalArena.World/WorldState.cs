namespace GlobalArena.World;

public sealed class WorldState
{
    public static WorldState CreateInitial()
    {
        return new WorldState();
    }

    private WorldState()
    {
    }
}
