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

    public PopulationRuntimeState Population { get; }

    public WarfareRuntimeState Warfare { get; }

    public PhysicalTacticalEconomyRuntimeState? PhysicalEconomy { get; }

    /// <summary>Optional dormant VS101 treasury reservation; absent in all old saves.</summary>
    public CanonicalTreasuryReservationRuntimeState? TreasuryReservation { get; }

    public static WorldState CreateInitial()
    {
        return new WorldState(
            revision: 0UL,
            worldBinding: null,
            CivilizationRuntimeState.Empty,
            StrategicTerritoryRuntimeState.Empty,
            BaselineDiplomacyRuntimeState.Empty,
            EconomyRuntimeState.Empty,
            PopulationRuntimeState.Uninitialized,
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
            PopulationRuntimeState.Uninitialized,
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
            Population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
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
            Population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
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
            Population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
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
            Population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
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
            Population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
    }

    /// <summary>
    /// Replaces the complete census. Once initialized, every economic point
    /// must have exactly one resident headcount, including zero-resident points.
    /// </summary>
    public WorldState WithPopulation(PopulationRuntimeState population)
    {
        ArgumentNullException.ThrowIfNull(population);
        RejectPopulationDowngrade(population);

        return new WorldState(
            Revision,
            WorldBinding,
            Civilizations,
            Territory,
            Diplomacy,
            Economy,
            population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
    }

    /// <summary>
    /// Atomic replacement required when economic point identities or workforce
    /// change and an initialized census must be updated in the same snapshot.
    /// </summary>
    public WorldState WithEconomyAndPopulation(
        EconomyRuntimeState economy,
        PopulationRuntimeState population)
    {
        ArgumentNullException.ThrowIfNull(economy);
        ArgumentNullException.ThrowIfNull(population);
        RejectPopulationDowngrade(population);

        return new WorldState(
            Revision,
            WorldBinding,
            Civilizations,
            Territory,
            Diplomacy,
            economy,
            population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
    }

    /// <summary>
    /// Publishes a validated physical economic snapshot without creating
    /// strategic people, cities, economic points or implicit transport.
    /// Caller advances the revision when committing a world transition.
    /// </summary>
    public WorldState WithPhysicalEconomy(
        PhysicalTacticalEconomyRuntimeState physicalEconomy)
    {
        ArgumentNullException.ThrowIfNull(physicalEconomy);
        return new WorldState(
            Revision,
            WorldBinding,
            Civilizations,
            Territory,
            Diplomacy,
            Economy,
            Population,
            Warfare,
            physicalEconomy,
            TreasuryReservation);
    }

    /// <summary>
    /// Opt-in dormant treasury position in the canonical physical snapshot.
    /// No balances or receipt entries are permitted. Legacy world snapshots
    /// never gain the reservation implicitly.
    /// </summary>
    public WorldState WithTreasuryReservation(
        CanonicalTreasuryReservationRuntimeState reservation)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        if (WorldBinding is null || PhysicalEconomy is null)
            throw new InvalidOperationException(
                "Treasury reservation requires a bound physical world.");
        reservation.RequireWorld(WorldBinding);
        if (TreasuryReservation is not null)
        {
            if (TreasuryReservation == reservation) return this;
            throw new InvalidOperationException(
                "Canonical treasury reservation cannot be silently replaced.");
        }
        return new WorldState(Revision, WorldBinding, Civilizations,
            Territory, Diplomacy, Economy, Population, Warfare,
            PhysicalEconomy, reservation);
    }

    private void RejectPopulationDowngrade(PopulationRuntimeState next)
    {
        if (Population.IsInitialized && !next.IsInitialized)
            throw new InvalidOperationException(
                "An initialized population census cannot silently become uninitialized.");
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
            Population,
            warfare,
            PhysicalEconomy,
            TreasuryReservation);
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
            Population,
            Warfare,
            PhysicalEconomy,
            TreasuryReservation);
    }

    private WorldState(
        ulong revision,
        RuntimeWorldBinding? worldBinding,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        BaselineDiplomacyRuntimeState diplomacy,
        EconomyRuntimeState economy,
        PopulationRuntimeState population,
        WarfareRuntimeState warfare,
        PhysicalTacticalEconomyRuntimeState? physicalEconomy = null,
        CanonicalTreasuryReservationRuntimeState? treasuryReservation = null)
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
            population);

        ArgumentNullException.ThrowIfNull(
            warfare);

        ValidateCrossDomainInvariants(
            worldBinding,
            civilizations,
            territory,
            diplomacy,
            economy,
            population,
            warfare,
            physicalEconomy,
            treasuryReservation);

        Revision = revision;
        WorldBinding = worldBinding;
        Civilizations = civilizations;
        Territory = territory;
        Diplomacy = diplomacy;
        Economy = economy;
        Population = population;
        Warfare = warfare;
        PhysicalEconomy = physicalEconomy;
        TreasuryReservation = treasuryReservation;
    }

    private static void ValidateCrossDomainInvariants(
        RuntimeWorldBinding? worldBinding,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        BaselineDiplomacyRuntimeState diplomacy,
        EconomyRuntimeState economy,
        PopulationRuntimeState population,
        WarfareRuntimeState warfare,
        PhysicalTacticalEconomyRuntimeState? physicalEconomy = null,
        CanonicalTreasuryReservationRuntimeState? treasuryReservation = null)
    {
        if (treasuryReservation is not null)
        {
            if (worldBinding is null || physicalEconomy is null)
                throw new InvalidOperationException(
                    "An active treasury-reservation domain requires bound physical world state.");
            treasuryReservation.RequireWorld(worldBinding);
        }
        if (physicalEconomy is not null)
        {
            if (worldBinding is null ||
                physicalEconomy.CoarseWorldBinding != worldBinding)
                throw new InvalidOperationException(
                    "Physical economy belongs to a different or unbound generated world.");
            if (economy.EconomicPoints.Count != 0 ||
                (population.IsInitialized && population.PointPopulations.Count != 0))
                throw new InvalidOperationException(
                    "Physical workforce cannot coexist with legacy strategic residents/economic points.");
        }

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

        if (population.IsInitialized)
        {
            if (population.PointPopulations.Count != economy.EconomicPoints.Count)
                throw new InvalidOperationException(
                    "Initialized population census must contain exactly one entry per economic point.");

            for (var index = 0; index < economy.EconomicPoints.Count; index++)
            {
                var economicPoint = economy.EconomicPoints[index];
                var census = population.PointPopulations[index];

                if (census.EconomicPointId != economicPoint.Id)
                    throw new InvalidOperationException(
                        "Initialized population census must reference precisely the existing economic points.");

                if (census.Residents < economicPoint.Workforce)
                    throw new InvalidOperationException(
                        $"Economic point {economicPoint.Id.Value} cannot have more workers than residents.");
            }
        }
        else if (population.PointPopulations.Count != 0)
        {
            throw new InvalidOperationException(
                "An uninitialized population census cannot contain resident records.");
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
