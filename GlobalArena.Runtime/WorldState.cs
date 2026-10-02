using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed class WorldState
{
    public ulong Revision { get; }

    public RuntimeWorldBinding? WorldBinding { get; }

    public bool IsWorldBound =>
        WorldBinding is not null;

    public CivilizationRuntimeState Civilizations { get; }

    public StrategicTerritoryRuntimeState Territory { get; }

    public BaselineDiplomacyRuntimeState Diplomacy { get; }

    public EconomyRuntimeState Economy { get; }

    public WarfareRuntimeState Warfare { get; }

    public static WorldState CreateInitial()
    {
        return new WorldState(
            revision: 0UL,
            worldBinding: null,
            CivilizationRuntimeState.Empty,
            StrategicTerritoryRuntimeState.Empty,
            BaselineDiplomacyRuntimeState.Empty,
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
            StrategicTerritoryRuntimeState.Empty,
            BaselineDiplomacyRuntimeState.Empty,
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
            Territory,
            Diplomacy,
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
            Territory,
            Diplomacy,
            Economy,
            Warfare);
    }

    public WorldState WithTerritory(
        StrategicTerritoryRuntimeState territory)
    {
        ArgumentNullException.ThrowIfNull(
            territory);

        return new WorldState(
            Revision,
            WorldBinding,
            Civilizations,
            territory,
            Diplomacy,
            Economy,
            Warfare);
    }

    public WorldState WithDiplomacy(
        BaselineDiplomacyRuntimeState diplomacy)
    {
        ArgumentNullException.ThrowIfNull(
            diplomacy);

        return new WorldState(
            Revision,
            WorldBinding,
            Civilizations,
            Territory,
            diplomacy,
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
            Territory,
            Diplomacy,
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
            Territory,
            Diplomacy,
            Economy,
            warfare);
    }

    public WorldState AdvanceRevision()
    {
        return new WorldState(
            checked(Revision + 1UL),
            WorldBinding,
            Civilizations,
            Territory,
            Diplomacy,
            Economy,
            Warfare);
    }

    private WorldState(
        ulong revision,
        RuntimeWorldBinding? worldBinding,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        BaselineDiplomacyRuntimeState diplomacy,
        EconomyRuntimeState economy,
        WarfareRuntimeState warfare)
    {
        ArgumentNullException.ThrowIfNull(
            civilizations);

        ArgumentNullException.ThrowIfNull(
            territory);

        ArgumentNullException.ThrowIfNull(
            diplomacy);

        ArgumentNullException.ThrowIfNull(
            economy);

        ArgumentNullException.ThrowIfNull(
            warfare);

        ValidateCrossDomainInvariants(
            worldBinding,
            civilizations,
            territory,
            diplomacy,
            economy,
            warfare);

        Revision = revision;
        WorldBinding = worldBinding;
        Civilizations = civilizations;
        Territory = territory;
        Diplomacy = diplomacy;
        Economy = economy;
        Warfare = warfare;
    }

    private static void ValidateCrossDomainInvariants(
        RuntimeWorldBinding? worldBinding,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        BaselineDiplomacyRuntimeState diplomacy,
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

        foreach (var control in
            territory.Controls)
        {
            if (!civilizationIds.Contains(
                control.Controller))
            {
                throw new InvalidOperationException(
                    $"Territorial controller {control.Controller.Value} is not present in the runtime civilization roster.");
            }

            if (worldBinding is not null
                && !worldBinding.Contains(
                    control.StrategicCellId))
            {
                throw new InvalidOperationException(
                    $"Territorial control references strategic cell {control.StrategicCellId.Value}, which does not belong to the bound generated world.");
            }
        }

        foreach (var relation in
            diplomacy.Relations)
        {
            if (!civilizationIds.Contains(
                relation.First))
            {
                throw new InvalidOperationException(
                    $"Diplomacy participant {relation.First.Value} is not present in the runtime civilization roster.");
            }

            if (!civilizationIds.Contains(
                relation.Second))
            {
                throw new InvalidOperationException(
                    $"Diplomacy participant {relation.Second.Value} is not present in the runtime civilization roster.");
            }
        }

        foreach (var point in
            economy.EconomicPoints)
        {
            if (!civilizationIds.Contains(
                point.Owner))
            {
                throw new InvalidOperationException(
                    $"Economic point owner {point.Owner.Value} is not present in the runtime civilization roster.");
            }

            if (worldBinding is not null
                && !worldBinding.Contains(
                    point.StrategicCellId))
            {
                throw new InvalidOperationException(
                    $"Economic point {point.Id.Value} references strategic cell {point.StrategicCellId.Value}, which does not belong to the bound generated world.");
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
