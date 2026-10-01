using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed class WorldState
{
    public ulong Revision { get; }

    public RuntimeWorldBinding? WorldBinding { get; }

    public bool IsWorldBound =>
        WorldBinding is not null;

    public CivilizationRuntimeState Civilizations { get; }

    public EconomyRuntimeState Economy { get; }

    public WarfareRuntimeState Warfare { get; }

    public static WorldState CreateInitial()
    {
        return new WorldState(
            revision: 0UL,
            worldBinding: null,
            CivilizationRuntimeState.Empty,
            EconomyRuntimeState.Empty,
            WarfareRuntimeState.Empty);
    }

    public static WorldState CreateBound(
        WorldGenerationResult generatedWorld)
    {
        return new WorldState(
            revision: 0UL,
            RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld),
            CivilizationRuntimeState.Empty,
            EconomyRuntimeState.Empty,
            WarfareRuntimeState.Empty);
    }

    public WorldState BindToWorld(
        WorldGenerationResult generatedWorld)
    {
        var binding =
            RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld);

        if (WorldBinding is not null)
        {
            if (WorldBinding == binding)
            {
                return this;
            }

            throw new InvalidOperationException(
                "WorldState is already bound to a different generated world.");
        }

        return new WorldState(
            Revision,
            binding,
            Civilizations,
            Economy,
            Warfare);
    }

    public WorldState WithCivilizations(
        CivilizationRuntimeState civilizations)
    {
        ArgumentNullException.ThrowIfNull(
            civilizations);

        return new WorldState(
            Revision,
            WorldBinding,
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
            WorldBinding,
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
            WorldBinding,
            Civilizations,
            Economy,
            warfare);
    }

    public WorldState AdvanceRevision()
    {
        return new WorldState(
            checked(Revision + 1UL),
            WorldBinding,
            Civilizations,
            Economy,
            Warfare);
    }

    private WorldState(
        ulong revision,
        RuntimeWorldBinding? worldBinding,
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

        ValidateCrossDomainInvariants(
            worldBinding,
            civilizations,
            economy,
            warfare);

        Revision = revision;
        WorldBinding = worldBinding;
        Civilizations = civilizations;
        Economy = economy;
        Warfare = warfare;
    }

    private static void ValidateCrossDomainInvariants(
        RuntimeWorldBinding? worldBinding,
        CivilizationRuntimeState civilizations,
        EconomyRuntimeState economy,
        WarfareRuntimeState warfare)
    {
        var civilizationIds =
            civilizations
                .Civilizations
                .ToHashSet();

        if (worldBinding is not null)
        {
            foreach (var civilization in
                civilizations.Records)
            {
                if (civilization.StartCellId
                    is StrategicCellId startCellId
                    && !worldBinding.Contains(
                        startCellId))
                {
                    throw new InvalidOperationException(
                        $"Civilization {civilization.Id.Value} references start cell {startCellId.Value}, which does not belong to the bound generated world.");
                }
            }
        }

        foreach (var stock in
            economy.StrategicStocks)
        {
            if (!civilizationIds.Contains(
                stock.Owner))
            {
                throw new InvalidOperationException(
                    $"Strategic stock owner {stock.Owner.Value} is not present in the runtime civilization roster.");
            }
        }

        foreach (var unit in
            warfare.Units)
        {
            if (!civilizationIds.Contains(
                unit.Owner))
            {
                throw new InvalidOperationException(
                    $"Military unit owner {unit.Owner.Value} is not present in the runtime civilization roster.");
            }

            if (worldBinding is not null
                && !worldBinding.Contains(
                    unit.StrategicCellId))
            {
                throw new InvalidOperationException(
                    $"Military unit {unit.Id.Value} references strategic cell {unit.StrategicCellId.Value}, which does not belong to the bound generated world.");
            }
        }
    }
}
