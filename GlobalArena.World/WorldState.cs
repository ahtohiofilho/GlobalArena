namespace GlobalArena.World;

public sealed class WorldState
{
    public ulong Revision { get; }

    public static WorldState CreateInitial()
    {
        return new WorldState(
            revision: 0UL);
    }

    public WorldState AdvanceRevision()
    {
        return new WorldState(
            checked(Revision + 1UL));
    }

    private WorldState(
        ulong revision)
    {
        Revision = revision;
    }
}
