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

    public PhysicalWorkerRuntimeState Workers { get; }

    /// <summary>
    /// The complete hashed fine transport inventory, distinct from economic
    /// previews or caller-supplied routing inputs. Legacy worlds remain
    /// explicitly uninitialized until an opt-in topology-checked deployment.
    /// </summary>
    public PhysicalTransportInfrastructureRuntimeState Transport { get; }

    /// <summary>
    /// Opt-in authoritative money balances, distinct from market proposals.
    /// Every investment must debit a real pre-existing civilization account.
    /// </summary>
    public CivilizationTreasuryRuntimeState Treasury { get; }

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
            WarfareRuntimeState.Empty,
            PhysicalWorkerRuntimeState.Uninitialized);
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
            WarfareRuntimeState.Empty,
            PhysicalWorkerRuntimeState.Uninitialized);
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
            Workers, Transport, Treasury);
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
            Workers, Transport, Treasury);
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
            Workers, Transport, Treasury);
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
            Workers, Transport, Treasury);
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
            Workers, Transport, Treasury);
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
            Workers, Transport, Treasury);
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
            Workers, Transport, Treasury);
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
            Workers, Transport, Treasury);
    }

    /// <summary>
    /// Explicitly replaces the canonical individual physical-worker census.
    /// The legacy aggregate population/economic point fields stay untouched.
    /// Initialization is a world-materialization operation; movement after
    /// setup must use the journaled authoritative movement turn phase.
    /// </summary>
    public WorldState WithWorkers(PhysicalWorkerRuntimeState workers)
    {
        ArgumentNullException.ThrowIfNull(workers);
        if (WorldBinding is null && workers.IsInitialized)
            throw new InvalidOperationException(
                "Authoritative worker placement requires a bound generated world.");
        if (Workers.IsInitialized && !workers.IsInitialized)
            throw new InvalidOperationException(
                "An initialized worker census cannot silently become absent.");
        if (Workers.HasAuthoritativeTacticalLocations !=
            workers.HasAuthoritativeTacticalLocations)
            throw new InvalidOperationException(
                "Changing worker location schema requires explicit tactical deployment.");
        if (Workers.HasAuthoritativeOccupations !=
            workers.HasAuthoritativeOccupations)
            throw new InvalidOperationException(
                "Changing worker occupation schema requires explicit full-census classification.");
        return new WorldState(
            Revision, WorldBinding, Civilizations, Territory, Diplomacy,
            Economy, Population, Warfare, workers, Transport, Treasury);
    }

    /// <summary>
    /// Explicit, world-bound v9 -> v10 deployment. Every submitted
    /// worker already has an individually chosen *real* fine cell whose
    /// incidence includes its previous coarse region. This API does not
    /// guess worker locations or silently migrate save files.
    /// </summary>
    public WorldState DeployTacticalWorkers(
        PhysicalWorkerRuntimeState tacticalWorkers,
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence)
    {
        ArgumentNullException.ThrowIfNull(tacticalWorkers);
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        if (WorldBinding is null || !Workers.IsInitialized ||
            Workers.HasAuthoritativeTacticalLocations ||
            !tacticalWorkers.IsInitialized ||
            !tacticalWorkers.HasAuthoritativeTacticalLocations)
            throw new InvalidOperationException(
                "Tactical deployment requires a bound v9 census and explicit complete v10 data.");
        if (WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld) ||
            incidence.Refinement.CoarseParameters !=
                generatedWorld.Request.StrategicParameters ||
            incidence.CoarseTopology.Cells.Count !=
                checked((int)WorldBinding.StrategicCellCount))
            throw new InvalidOperationException(
                "Tactical topology does not match this exact bound generated world.");
        if (Workers.Workers.Count != tacticalWorkers.Workers.Count)
            throw new InvalidOperationException("Tactical deployment cannot add or remove workers.");

        for (var i = 0; i < Workers.Workers.Count; i++)
        {
            var original = Workers.Workers[i];
            var updated = tacticalWorkers.Workers[i];
            if (original.Id != updated.Id || original.Owner != updated.Owner ||
                original.CellId != updated.CellId ||
                original.Activity != updated.Activity ||
                original.Commodity != updated.Commodity ||
                original.OutputMicroPerTurn != updated.OutputMicroPerTurn ||
                original.DemandMultiplierMicro != updated.DemandMultiplierMicro)
                throw new InvalidOperationException(
                    "Tactical deployment may only add fine locations, not rewrite worker facts.");
            var tile = updated.TacticalTileId!.Value;
            if (tile.FineGoldbergParameters != incidence.Refinement.FineParameters)
                throw new InvalidOperationException(
                    "Tactical worker position belongs to a different refinement.");
            var entry = incidence.TileIncidences[
                checked((int)tile.FineStrategicCellId.Value - 1)];
            if (!entry.IncidentCoarseCellIds.Contains(original.CellId))
                throw new InvalidOperationException(
                    "Worker's fine position is not incident to its prior coarse region.");
        }
        return new WorldState(
            Revision, WorldBinding, Civilizations, Territory, Diplomacy,
            Economy, Population, Warfare, tacticalWorkers, Transport, Treasury);
    }

    /// <summary>
    /// Explicit v10->v11 all-worker productive classification. Does not
    /// invent workforce, output, production, demand, commodity or positions.
    /// Classification may only occur at a schema migration boundary,
    /// never implicitly from a commodity or a rendering component.
    /// </summary>
    public WorldState ClassifyTacticalWorkerOccupations(
        IReadOnlyDictionary<PhysicalWorkerId, PhysicalWorkerOccupation> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        if (WorldBinding is null || !Workers.IsInitialized ||
            !Workers.HasAuthoritativeTacticalLocations ||
            Workers.HasAuthoritativeOccupations)
            throw new InvalidOperationException(
                "Occupation deployment requires an already-bound v10 fine worker census.");
        if (Workers.Workers.Count != assignments.Count ||
            Workers.Workers.Any(worker => !assignments.ContainsKey(worker.Id)) ||
            assignments.Keys.Any(id => !id.IsValid))
            throw new InvalidOperationException(
                "Every canonical physical worker needs exactly one occupation assignment.");

        var recategorized = new PhysicalWorkerRuntimeState(
            Workers.Workers.Select(worker =>
            {
                var occupation = assignments[worker.Id];
                if (!Enum.IsDefined(occupation) ||
                    (worker.Activity == PhysicalWorkerActivity.Producing &&
                        occupation == PhysicalWorkerOccupation.Unassigned))
                    throw new InvalidOperationException(
                        "Invalid or missing productive occupation for active worker.");
                return new PhysicalWorkerRuntimeRecord(
                    worker.Id, worker.Owner, worker.CellId,
                    worker.Commodity, worker.OutputMicroPerTurn,
                    worker.Activity, worker.DemandMultiplierMicro,
                    worker.TacticalTileId, occupation);
            }), tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        return new WorldState(
            Revision, WorldBinding, Civilizations, Territory, Diplomacy,
            Economy, Population, Warfare, recategorized, Transport, Treasury);
    }

    /// <summary>
    /// One-time explicit v11 -> v12 physical-transport deployment. Requires
    /// complete, canonical fine-topology evidence and a bound world; never
    /// derives natural rates, constructed ownership or tariff defaults.
    /// Future upgrades/captures MUST use a separate journaled turn phase.
    /// </summary>
    public WorldState DeployInitialPhysicalTransport(
        PhysicalTransportInfrastructureRuntimeState transport,
        WorldGenerationResult generatedWorld,
        PhysicalTacticalIncidenceMap incidence)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        if (WorldBinding is null || !Workers.HasAuthoritativeOccupations ||
            Transport.IsInitialized || !transport.IsInitialized)
            throw new InvalidOperationException(
                "Physical transport deployment requires an unmodified " +
                "bound v11 worker world and an explicit initialized inventory.");
        if (WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld) ||
            incidence.Refinement.CoarseParameters !=
                generatedWorld.Request.StrategicParameters ||
            transport.Edges.Count != incidence.FineTopology.Edges.Count)
            throw new InvalidOperationException(
                "Complete transport must match all and only the bound " +
                "fine Goldberg dual-graph crossings.");
        var fine = incidence.Refinement.FineParameters;
        var canonical = incidence.FineTopology.Edges
            .Select(edge => new PhysicalTacticalEdgeId(fine, edge.Id))
            .ToHashSet();
        var known = Civilizations.Civilizations.ToHashSet();
        if (transport.Edges.Any(row =>
                row.Edge.FineGoldbergParameters != fine ||
                !canonical.Remove(row.Edge) ||
                (row.InfrastructureOwner.HasValue &&
                    !known.Contains(row.InfrastructureOwner.Value))) ||
            canonical.Count != 0)
            throw new InvalidOperationException(
                "Transport contains missing/foreign crossings or owners " +
                "outside the authoritative civilization roster.");
        return new WorldState(
            Revision, WorldBinding, Civilizations, Territory, Diplomacy,
            Economy, Population, Warfare, Workers, transport, Treasury);
    }

    /// <summary>
    /// One-time complete treasury deployment: every real civilization must
    /// have an explicitly financed opening account (zero is legitimate).
    /// This is NOT settlement of a hypothetical sale; an economic journal
    /// must later supply income before finance can increase organically.
    /// </summary>
    public WorldState DeployInitialTreasury(
        CivilizationTreasuryRuntimeState opening)
    {
        ArgumentNullException.ThrowIfNull(opening);
        if (WorldBinding is null || Treasury.IsInitialized ||
            !opening.IsInitialized || !Transport.HasMonetaryConstructionCosts ||
            opening.Accounts.Count != Civilizations.Civilizations.Count ||
            !opening.Accounts.Select(a => a.Civilization)
                .SequenceEqual(Civilizations.Civilizations))
            throw new InvalidOperationException(
                "Opening treasury requires one explicit account for each " +
                "civilization in an authoritative monetary world.");
        return new WorldState(
            Revision, WorldBinding, Civilizations, Territory, Diplomacy,
            Economy, Population, Warfare, Workers, Transport, opening);
    }

    /// <summary>
    /// Called by the validated monetary investment event executor. Both
    /// the cash debit and edge capital investment are one immutable change;
    /// no commodity, labor or buyer inventory is touched.
    /// </summary>
    public WorldState RecordMonetaryInfrastructureInvestment(
        CivilizationId investor, PhysicalTacticalEdgeId edge,
        ulong moneyMicro)
    {
        if (!Treasury.IsInitialized || !Transport.HasMonetaryConstructionCosts)
            throw new InvalidOperationException(
                "Investments need authoritative treasury and quoted costs.");
        var newTransport = Transport.RecordMoneyInvestment(edge, moneyMicro);
        var newTreasury = Treasury.DebitConstruction(investor, moneyMicro);
        return new WorldState(
            Revision, WorldBinding, Civilizations, Territory, Diplomacy,
            Economy, Population, Warfare, Workers, newTransport, newTreasury);
    }

    /// <summary>
    /// This is an INTERNAL mutation capability, invoked only after a
    /// system-event executor independently recalculates and audits all
    /// real production, deliveries, market prices, taxes, tolls, and
    /// freight. Buyers have no wallets; goods never become stock.
    /// </summary>
    internal WorldState RecordAuditedEconomicCycle(
        ulong ordinal, string cargoUnitCode,
        IReadOnlyDictionary<CivilizationId, System.Numerics.BigInteger>
            verifiedCivilizationCreditsScaled,
        IEnumerable<CivilizationSettledFreightQuote> nextFreightQuotes)
    {
        if (WorldBinding is null || !Treasury.IsInitialized ||
            !Transport.HasMonetaryConstructionCosts ||
            !Workers.HasAuthoritativeOccupations)
            throw new InvalidOperationException(
                "Economic settlement needs a real bound market and treasury.");
        var settled = Treasury.CloseVerifiedEconomicCycle(
            ordinal, cargoUnitCode, verifiedCivilizationCreditsScaled,
            nextFreightQuotes);
        return new WorldState(
            Revision, WorldBinding, Civilizations, Territory, Diplomacy,
            Economy, Population, Warfare, Workers, Transport, settled);
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
            Workers, Transport, Treasury);
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
        PhysicalWorkerRuntimeState? workers = null,
        PhysicalTransportInfrastructureRuntimeState? transport = null,
        CivilizationTreasuryRuntimeState? treasury = null)
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

        var workerState = workers ?? PhysicalWorkerRuntimeState.Uninitialized;
        var transportState = transport ??
            PhysicalTransportInfrastructureRuntimeState.Uninitialized;
        var treasuryState = treasury ??
            CivilizationTreasuryRuntimeState.Uninitialized;

        ValidateCrossDomainInvariants(
            worldBinding,
            civilizations,
            territory,
            diplomacy,
            economy,
            population,
            warfare,
            workerState,
            transportState,
            treasuryState);

        Revision = revision;
        WorldBinding = worldBinding;
        Civilizations = civilizations;
        Territory = territory;
        Diplomacy = diplomacy;
        Economy = economy;
        Population = population;
        Warfare = warfare;
        Workers = workerState;
        Transport = transportState;
        Treasury = treasuryState;
    }

    private static void ValidateCrossDomainInvariants(
        RuntimeWorldBinding? worldBinding,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        BaselineDiplomacyRuntimeState diplomacy,
        EconomyRuntimeState economy,
        PopulationRuntimeState population,
        WarfareRuntimeState warfare,
        PhysicalWorkerRuntimeState workers,
        PhysicalTransportInfrastructureRuntimeState transport,
        CivilizationTreasuryRuntimeState treasury)
    {
        var civilizationIds =
            civilizations
                .Civilizations
                .ToHashSet();

        if (treasury.IsInitialized)
        {
            if (worldBinding is null ||
                !transport.HasMonetaryConstructionCosts ||
                treasury.Accounts.Count != civilizationIds.Count ||
                treasury.Accounts.Any(account =>
                    !civilizationIds.Contains(account.Civilization) ||
                    account.CashScaled.Sign < 0 ||
                    account.TotalInfrastructureInvestmentScaled.Sign < 0 ||
                    account.TotalSettledEconomicIncomeScaled.Sign < 0))
                throw new InvalidOperationException(
                    "Authoritative treasury must match all real civilization " +
                    "identities, monetary transport and nonnegative money.");
        }
        else if (treasury.Accounts.Count != 0)
            throw new InvalidOperationException(
                "An uninitialized treasury cannot contain accounts.");

        if (treasury.HasClosedEconomicCycle)
        {
            var quotes = treasury.LastSettledFreightQuotes;
            var known = transport.Edges.Select(e => e.Edge).ToHashSet();
            if (!transport.IsInitialized ||
                string.IsNullOrWhiteSpace(treasury.LastSettledCargoUnitCode) ||
                quotes.Count != known.Count ||
                quotes.Any(q => q is null || !q.Edge.IsValid ||
                    q.FreePhysicalCostMicroPerWholeCargoUnit == 0UL ||
                    q.ReferenceCargoMicro == 0UL || !known.Remove(q.Edge)) ||
                known.Count != 0)
                throw new InvalidOperationException(
                    "A closed economic cycle requires complete canonical " +
                    "freight memory tied to real physical crossings.");
        }
        else if (treasury.LastSettledFreightQuotes.Count != 0 ||
                 treasury.LastSettledCargoUnitCode is not null)
            throw new InvalidOperationException(
                "Unsettled worlds cannot carry invented freight history.");

        if (transport.HasCommittedInvestments &&
            (!treasury.IsInitialized || !transport.HasMonetaryConstructionCosts ||
             transport.Edges.Any(row =>
                 row.NaturalConstructionCostMoneyMicro is not > 0UL ||
                 (row.CargoMicroAtReferenceLoad == ulong.MaxValue &&
                  row.CommittedInvestmentMoneyMicro.GetValueOrDefault() != 0UL))))
            throw new InvalidOperationException(
                "Committed improvements require a positive original monetary " +
                "reference, authoritative treasury and zero investment in open sea.");

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

        if (workers.IsInitialized)
        {
            if (worldBinding is null)
                throw new InvalidOperationException(
                    "Authoritative physical workers need a world binding.");
            foreach (var worker in workers.Workers)
            {
                if (!civilizationIds.Contains(worker.Owner))
                    throw new InvalidOperationException(
                        $"Physical worker {worker.Id.Value} references an unknown civilization.");
                if (!worldBinding.Contains(worker.CellId))
                    throw new InvalidOperationException(
                        $"Physical worker {worker.Id.Value} is located outside the Goldberg world.");
                if (workers.HasAuthoritativeTacticalLocations)
                {
                    var fine = worker.TacticalTileId!.Value;
                    if (!fine.IsValid ||
                        fine.FineGoldbergParameters.StrategicCellCount !=
                        checked((worldBinding.StrategicCellCount - 2UL) * 36UL + 2UL))
                        throw new InvalidOperationException(
                            "v10 worker position must use a scale-six physical Goldberg topology.");
                }
            }
        }
        else if (workers.Workers.Count != 0)
        {
            throw new InvalidOperationException(
                "An uninitialized physical worker census cannot contain records.");
        }

        if (transport.IsInitialized)
        {
            if (worldBinding is null || !workers.HasAuthoritativeOccupations)
                throw new InvalidOperationException(
                    "Hashed transport requires a bound v11 worker world.");
            if (transport.Edges.Any(row =>
                row.InfrastructureOwner.HasValue &&
                !civilizationIds.Contains(row.InfrastructureOwner.Value)))
                throw new InvalidOperationException(
                    "Hashed physical transport references a foreign civilization.");
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
