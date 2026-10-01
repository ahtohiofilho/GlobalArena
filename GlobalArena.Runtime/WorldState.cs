namespace GlobalArena.Runtime;

public sealed class WorldState
{
    public ulong Revision { get; }

    public CivilizationRuntimeState Civilizations { get; }

    public EconomyRuntimeState Economy { get; }

    public WarfareRuntimeState Warfare { get; }

    public static WorldState CreateInitial()
    {
        return new WorldState(
            revision: 0UL,
            CivilizationRuntimeState.Empty,
            EconomyRuntimeState.Empty,
            WarfareRuntimeState.Empty);
    }

    public WorldState WithCivilizations(
        CivilizationRuntimeState civilizations)
    {
        ArgumentNullException.ThrowIfNull(
            civilizations);

        return new WorldState(
            Revision,
            civilizations,
            Economy,
            Warfare);
    }

    public WorldState WithEconomy(
        EconomyRuntimeState economy)
    {
        ArgumentNullException.ThrowIfNull(
            economy);

        return new WorldState(
            Revision,
            Civilizations,
            economy,
            Warfare);
    }

    public WorldState WithWarfare(
        WarfareRuntimeState warfare)
    {
        ArgumentNullException.ThrowIfNull(
            warfare);

        return new WorldState(
            Revision,
            Civilizations,
            Economy,
            warfare);
    }

    public WorldState AdvanceRevision()
    {
        return new WorldState(
            checked(Revision + 1UL),
            Civilizations,
            Economy,
            Warfare);
    }

    private WorldState(
        ulong revision,
        CivilizationRuntimeState civilizations,
        EconomyRuntimeState economy,
        WarfareRuntimeState warfare)
    {
        ArgumentNullException.ThrowIfNull(
            civilizations);

        ArgumentNullException.ThrowIfNull(
            economy);

        ArgumentNullException.ThrowIfNull(
            warfare);

        Revision = revision;
        Civilizations = civilizations;
        Economy = economy;
        Warfare = warfare;
    }
}
