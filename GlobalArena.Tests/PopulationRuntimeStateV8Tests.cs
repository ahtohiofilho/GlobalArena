using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PopulationRuntimeStateV8Tests
{
    private static (WorldGenerationResult World,
        CivilizationRuntimeState Civilizations,
        StrategicTerritoryRuntimeState Territory) Fixture()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 3);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(civilizations);
        return (world, civilizations, territory);
    }

    private static EconomicPointRuntimeState Point(
        ulong id, CivilizationRuntimeRecord civ, ulong workers) =>
        new(new EconomicPointId(id), civ.Id, civ.StartCellId!.Value, workers);

    private static InitialPopulationReconciliationResult InitialCensus(
        WorldGenerationResult world,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        ulong firstResidents = 20UL,
        ulong firstWorkers = 5UL)
    {
        var first = civilizations.Records[0];

        return RuntimeInitialPopulationReconciler.Reconcile(
            world,
            civilizations,
            territory,
            new[] { Point(1UL, first, firstWorkers) },
            civilizations.Records.Select(civ =>
                new InitialCivilizationWorkforceBudget(
                    civ.Id, civ.Id == first.Id ? firstWorkers : 0UL)),
            civilizations.Records.Select(civ =>
                new InitialCivilizationPopulationBudget(
                    civ.Id, civ.Id == first.Id ? firstResidents : 0UL)),
            new[]
            {
                new InitialEconomicPointPopulation(
                    new EconomicPointId(1UL), firstResidents)
            });
    }

    private static WorldState BoundState(
        WorldGenerationResult world,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        InitialPopulationReconciliationResult census) =>
        WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                census.Economy,
                RuntimePopulationMaterializer.Materialize(census));

    [Fact]
    public void GeneratedWorldInitialCensusBecomesAuthoritativeResidentWorldState()
    {
        var (world, civilizations, territory) = Fixture();
        var census = InitialCensus(world, civilizations, territory);
        var state = BoundState(world, civilizations, territory, census);

        Assert.True(state.IsWorldBound);
        Assert.True(state.Population.IsInitialized);
        Assert.Single(state.Population.PointPopulations);
        Assert.Equal(20UL, state.Population.PointPopulations[0].Residents);
        Assert.Equal(5UL, state.Economy.EconomicPoints[0].Workforce);
        Assert.Equal(8U, new CanonicalWorldStateHasher().Compute(state).FormatVersion);
    }

    [Fact]
    public void PopulationChangesCanonicalHashWithoutChangingWorkforceOrGeometry()
    {
        var (world, civilizations, territory) = Fixture();
        var original = BoundState(world, civilizations, territory,
            InitialCensus(world, civilizations, territory, firstResidents: 20UL));
        var changed = original.WithPopulation(new PopulationRuntimeState(
            new[]
            {
                new PopulationPointRuntimeState(new EconomicPointId(1UL), 25UL)
            }));

        Assert.Same(original.Economy, changed.Economy);
        Assert.Equal(original.WorldBinding, changed.WorldBinding);
        Assert.NotEqual(
            new CanonicalWorldStateHasher().Compute(original),
            new CanonicalWorldStateHasher().Compute(changed));
        Assert.Equal(20UL, original.Population.PointPopulations.Single().Residents);
        Assert.Equal(25UL, changed.Population.PointPopulations.Single().Residents);
    }

    [Fact]
    public void PopulationSurvivesRevisionAndUnrelatedSubsystemReplacement()
    {
        var (world, civilizations, territory) = Fixture();
        var original = BoundState(world, civilizations, territory,
            InitialCensus(world, civilizations, territory));

        var changed = original.AdvanceRevision()
            .WithDiplomacy(BaselineDiplomacyRuntimeState.Empty);

        Assert.Same(original.Population, changed.Population);
        Assert.Equal(1UL, changed.Revision);
        Assert.Equal(20UL, changed.Population.PointPopulations.Single().Residents);
    }

    [Fact]
    public void CensusRejectsMissingAndUnknownEconomicPoints()
    {
        var (world, civilizations, territory) = Fixture();
        var state = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomy(new EconomyRuntimeState(
                new[] { Point(1UL, civilizations.Records[0], 5UL) }));

        Assert.Throws<InvalidOperationException>(() =>
            state.WithPopulation(new PopulationRuntimeState(
                Array.Empty<PopulationPointRuntimeState>())));

        Assert.Throws<InvalidOperationException>(() =>
            state.WithPopulation(new PopulationRuntimeState(
                new[] { new PopulationPointRuntimeState(
                    new EconomicPointId(999UL), 20UL) })));
    }

    [Fact]
    public void CensusRejectsPopulationBelowWorkforce()
    {
        var (world, civilizations, territory) = Fixture();
        var state = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomy(new EconomyRuntimeState(
                new[] { Point(1UL, civilizations.Records[0], 5UL) }));

        Assert.Throws<InvalidOperationException>(() =>
            state.WithPopulation(new PopulationRuntimeState(
                new[] { new PopulationPointRuntimeState(
                    new EconomicPointId(1UL), 4UL) })));
    }

    [Fact]
    public void DuplicateAndInvalidCensusIdentitiesAreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new PopulationPointRuntimeState(default, 0UL));

        var record = new PopulationPointRuntimeState(
            new EconomicPointId(1UL), 5UL);

        Assert.Throws<ArgumentException>(() =>
            new PopulationRuntimeState(new[] { record, record }));

        Assert.Throws<ArgumentException>(() =>
            new PopulationRuntimeState(new PopulationPointRuntimeState[] { null! }));
    }

    [Fact]
    public void PopulationInputCollectionsAreSnapshotReadOnly()
    {
        var records = new List<PopulationPointRuntimeState>
        {
            new(new EconomicPointId(2UL), 7UL),
            new(new EconomicPointId(1UL), 12UL)
        };
        var state = new PopulationRuntimeState(records);
        records.Clear();

        Assert.Equal(new[] { 1UL, 2UL },
            state.PointPopulations.Select(p => p.EconomicPointId.Value));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<PopulationPointRuntimeState>)state.PointPopulations)
                .Add(new PopulationPointRuntimeState(new EconomicPointId(3UL), 4UL)));
    }

    [Fact]
    public void InitializedEmptyPopulationIsDistinctFromNoPopulationData()
    {
        var uninitialized = WorldState.CreateInitial();
        var initialized = uninitialized.WithPopulation(
            new PopulationRuntimeState(Array.Empty<PopulationPointRuntimeState>()));
        var hasher = new CanonicalWorldStateHasher();

        Assert.False(uninitialized.Population.IsInitialized);
        Assert.True(initialized.Population.IsInitialized);
        Assert.NotEqual(hasher.Compute(uninitialized), hasher.Compute(initialized));
    }

    [Fact]
    public void WorkforceOrPointChangesRequireAtomicCensusReplacement()
    {
        var (world, civilizations, territory) = Fixture();
        var original = BoundState(world, civilizations, territory,
            InitialCensus(world, civilizations, territory));

        var changedEconomy = new EconomyRuntimeState(
            new[]
            {
                Point(1UL, civilizations.Records[0], 5UL),
                Point(2UL, civilizations.Records[1], 3UL)
            });

        Assert.Throws<InvalidOperationException>(() =>
            original.WithEconomy(changedEconomy));

        var changed = original.WithEconomyAndPopulation(
            changedEconomy,
            new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(new EconomicPointId(2UL), 4UL),
                new PopulationPointRuntimeState(new EconomicPointId(1UL), 20UL)
            }));

        Assert.Equal(2, changed.Economy.EconomicPoints.Count);
        Assert.Equal(2, changed.Population.PointPopulations.Count);
        Assert.Equal(4UL, changed.Population.PointPopulations[1].Residents);
    }

    [Fact]
    public void PopulationAndEconomicOrderingAreCanonical()
    {
        var (world, civilizations, territory) = Fixture();

        WorldState Build(bool reverse)
        {
            var points = new[]
            {
                Point(2UL, civilizations.Records[1], 3UL),
                Point(1UL, civilizations.Records[0], 4UL)
            };
            var people = new[]
            {
                new PopulationPointRuntimeState(new EconomicPointId(2UL), 10UL),
                new PopulationPointRuntimeState(new EconomicPointId(1UL), 20UL)
            };

            if (reverse)
            {
                Array.Reverse(points);
                Array.Reverse(people);
            }

            return WorldState.CreateBound(world)
                .WithCivilizations(civilizations)
                .WithTerritory(territory)
                .WithEconomyAndPopulation(
                    new EconomyRuntimeState(points),
                    new PopulationRuntimeState(people));
        }

        Assert.Equal(
            new CanonicalWorldStateHasher().Compute(Build(false)),
            new CanonicalWorldStateHasher().Compute(Build(true)));
    }

    [Fact]
    public void CommandlessEconomicTurnPreservesResidentCensus()
    {
        var (world, civilizations, territory) = Fixture();
        var session = new CanonicalGameSession(world);
        var original = BoundState(world, civilizations, territory,
            InitialCensus(world, civilizations, territory));
        session.AcceptSnapshot(original);

        var phase = new EconomicProductionDemandTurnPhase(
            Array.Empty<EconomicProductionPotential>(),
            new[]
            {
                new EconomicDemandProfile(
                    new EconomicPointId(1UL), new CommodityId(1U), 250_000L)
            });

        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { phase })),
            new TurnNumber(1UL), 1U);

        var hash = new CanonicalWorldStateHasher().Compute(original);
        var result = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false),
            new TurnResolutionInput(
                session.Snapshot,
                Array.Empty<ISimulationCommand>(),
                new SimulationContext(new TurnNumber(1UL), new SimulationSeed(77UL)))));

        Assert.True(result.WasResolved);
        Assert.Equal(1_250_000UL,
            Assert.IsType<EconomicProductionDemandResolvedEvent>(
                result.ResolutionResult!.Events.Single())
                .Flows.DemandFlows.Single().RawQuantity);
        Assert.Same(original, session.Snapshot);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(session.Snapshot));
        Assert.Equal(20UL, session.Snapshot.Population.PointPopulations.Single().Residents);
    }

    [Fact]
    public void AlreadyInitializedPopulationCannotBeSilentlyUninitialized()
    {
        var (world, civilizations, territory) = Fixture();
        var initial = BoundState(world, civilizations, territory,
            InitialCensus(world, civilizations, territory));

        Assert.Throws<InvalidOperationException>(() =>
            initial.WithPopulation(PopulationRuntimeState.Uninitialized));

        Assert.Throws<InvalidOperationException>(() =>
            initial.WithEconomyAndPopulation(
                EconomyRuntimeState.Empty,
                PopulationRuntimeState.Uninitialized));

        Assert.Equal(20UL, initial.Population.PointPopulations.Single().Residents);
    }

    [Fact]
    public void KnownNonemptyPopulationHasStableV8CanonicalHash()
    {
        var state = WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(
                new[] { new CivilizationId(1UL) }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        5UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 3UL)
                        })
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), 20UL)
                }));

        Assert.Equal(
            "E5F95A0DE100FF02ECC559943D903430" +
            "56606326B3F8E18016B3196891120858",
            new CanonicalWorldStateHasher().Compute(state).HexDigest);
    }

    [Fact]
    public void WorldStateRejectsNullPopulationOrEconomyReplacement()
    {
        var state = WorldState.CreateInitial();

        Assert.Throws<ArgumentNullException>(() => state.WithPopulation(null!));
        Assert.Throws<ArgumentNullException>(() =>
            state.WithEconomyAndPopulation(null!, PopulationRuntimeState.Uninitialized));
        Assert.Throws<ArgumentNullException>(() =>
            state.WithEconomyAndPopulation(EconomyRuntimeState.Empty, null!));
    }
}
