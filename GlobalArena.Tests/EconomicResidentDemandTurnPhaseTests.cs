using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class EconomicResidentDemandTurnPhaseTests
{
    private static WorldState State(
        ulong firstResidents = 10UL,
        ulong firstWorkers = 3UL,
        ulong secondResidents = 12UL)
    {
        return WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(
                new[] { new CivilizationId(1UL) }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(2UL),
                        0UL),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        firstWorkers,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 2UL)
                        })
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(2UL), secondResidents),
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), firstResidents)
                }));
    }

    private static EconomicResidentDemandProfile Demand(
        ulong point = 1UL, uint commodity = 1U,
        long rate = 500_000L) =>
        new(new EconomicPointId(point), new CommodityId(commodity), rate);

    private static EconomicProductionPotential Production() =>
        new(new EconomicPointId(1UL), new CommodityId(1U),
            EconomicActivityKind.Agriculture, 1_000_000L);

    private static EconomicResidentProductionDemandTurnPhase Phase() =>
        new(new[] { Production() }, new[] { Demand() });

    private static TurnResolutionInput Input(WorldState state, ulong turn = 1UL) =>
        new(state, Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(73UL)));

    [Fact]
    public void ResidentDemandIncludesNonworkersWhileProductionUsesAllocatedWorkforce()
    {
        var state = State();
        var result = Phase().Resolve(Input(state), state);
        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            result.Events.Single());

        Assert.Equal(2_000_000UL, evt.Flows.SupplyFlows.Single().RawQuantity);
        Assert.Equal(5_000_000UL, evt.Flows.DemandFlows.Single().RawQuantity);
        Assert.Equal(EconomicProductionDemandTurnPhase.StablePhaseId,
            evt.Id.SystemPhaseId);
        Assert.Equal(EventOriginKind.System, evt.Id.OriginKind);
        Assert.Same(state, result.ResultingWorldState);
    }

    [Fact]
    public void ResidentDemandIsAnAlternativeToWorkforceDemandRatherThanAddedToIt()
    {
        var state = State();
        var historical = new EconomicProductionDemandTurnPhase(
            new[] { Production() },
            new[] { new EconomicDemandProfile(
                new EconomicPointId(1UL), new CommodityId(1U), 500_000L) });
        var current = Phase();

        var workerFlow = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            historical.Resolve(Input(state), state).Events.Single())
            .Flows.DemandFlows.Single();
        var residentFlow = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            current.Resolve(Input(state), state).Events.Single())
            .Flows.DemandFlows.Single();

        Assert.Equal(1_500_000UL, workerFlow.RawQuantity);
        Assert.Equal(5_000_000UL, residentFlow.RawQuantity);
        Assert.Equal(workerFlow.CommodityId, residentFlow.CommodityId);
    }

    [Fact]
    public void BothDemandBasesCannotBeInstalledIntoOnePhaseEngine()
    {
        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                new EconomicProductionDemandTurnPhase(
                    new[] { Production() },
                    Array.Empty<EconomicDemandProfile>()),
                Phase()
            }));
    }

    [Fact]
    public void NonworkingResidentsStillGenerateDemand()
    {
        var result = new EconomicResidentDemandResolver().Resolve(
            State(), new[] { Demand(point: 2UL, rate: 250_000L) });

        Assert.Equal(3_000_000UL, result.Single().RawQuantity);
    }

    [Fact]
    public void UninitializedPopulationFailsClosedWithoutInferringZeroResidents()
    {
        var state = WorldState.CreateInitial();
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicResidentDemandResolver().Resolve(
                state, Array.Empty<EconomicResidentDemandProfile>()));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicResidentProductionDemandTurnPhase(
                Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicResidentDemandProfile>())
                .Resolve(Input(state), state));
    }

    [Fact]
    public void ResidentDemandUsesStagedCensusFromEarlierPhases()
    {
        var planning = State(firstResidents: 10UL);
        var staged = planning.WithPopulation(new PopulationRuntimeState(new[]
        {
            new PopulationPointRuntimeState(new EconomicPointId(1UL), 20UL),
            new PopulationPointRuntimeState(new EconomicPointId(2UL), 12UL)
        }));

        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            Phase().Resolve(Input(planning), staged).Events.Single());

        Assert.Equal(10_000_000UL, evt.Flows.DemandFlows.Single().RawQuantity);
        Assert.Equal(10UL, planning.Population.PointPopulations[0].Residents);
    }

    [Fact]
    public void DemandIsCanonicalAcrossProfileOrderingAndRepeatableAcrossTurns()
    {
        var state = State();
        var profiles = new[]
        {
            Demand(point: 2UL, commodity: 2U, rate: 250_000L),
            Demand(point: 1UL, commodity: 3U, rate: 750_000L),
            Demand(point: 1UL, commodity: 1U, rate: 500_000L)
        };
        var resolver = new EconomicResidentDemandResolver();

        var first = resolver.Resolve(state, profiles);
        var second = resolver.Resolve(state, profiles.Reverse());

        Assert.Equal(
            new[] { (1UL, 1U, 5_000_000UL),
                    (1UL, 3U, 7_500_000UL),
                    (2UL, 2U, 3_000_000UL) },
            first.Select(x => (x.EconomicPointId.Value, x.CommodityId.Value, x.RawQuantity)));
        Assert.Equal(
            first.Select(x => (x.EconomicPointId, x.CommodityId, x.RawQuantity)),
            second.Select(x => (x.EconomicPointId, x.CommodityId, x.RawQuantity)));

        var phase = new EconomicResidentProductionDemandTurnPhase(
            new[] { Production() }, profiles);
        var firstEvent = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            phase.Resolve(Input(state, 1UL), state).Events.Single());
        var secondEvent = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            phase.Resolve(Input(state, 2UL), state).Events.Single());

        Assert.NotEqual(firstEvent.Id, secondEvent.Id);
        Assert.Equal(
            firstEvent.Flows.DemandFlows.Select(f =>
                (f.EconomicPointId, f.CommodityId, f.RawQuantity)),
            secondEvent.Flows.DemandFlows.Select(f =>
                (f.EconomicPointId, f.CommodityId, f.RawQuantity)));
    }

    [Fact]
    public void DuplicateProfilesAndUnknownEconomicPointsAreRejected()
    {
        var state = State();
        var resolver = new EconomicResidentDemandResolver();

        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(state, new[] { Demand(), Demand() }));
        Assert.Throws<InvalidOperationException>(() =>
            resolver.Resolve(state, new[] { Demand(point: 999UL) }));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(1_000_001L)]
    public void InvalidPerResidentRateFailsFast(long rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Demand(rate: rate));
    }

    [Fact]
    public void ZeroResidentsGenerateNoDemandFlow()
    {
        var state = State(secondResidents: 0UL);
        var result = new EconomicResidentDemandResolver().Resolve(
            state, new[] { Demand(point: 2UL) });

        Assert.Empty(result);
    }

    [Fact]
    public void ResidentDemandUsesCheckedUInt128IntermediateAndRejectsOverflow()
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
                        0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), ulong.MaxValue)
                }));

        Assert.Throws<OverflowException>(() =>
            new EconomicResidentDemandResolver().Resolve(
                state, new[] { Demand(rate: 1_000_000L) }));
    }

    [Fact]
    public void PhaseSnapshotsExternalInputCollections()
    {
        var production = new List<EconomicProductionPotential> { Production() };
        var profiles = new List<EconomicResidentDemandProfile> { Demand() };
        var phase = new EconomicResidentProductionDemandTurnPhase(production, profiles);
        production.Clear();
        profiles.Clear();

        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            phase.Resolve(Input(State()), State()).Events.Single());

        Assert.Single(evt.Flows.SupplyFlows);
        Assert.Single(evt.Flows.DemandFlows);
    }

    [Fact]
    public void AuthoritativeCommandlessTurnWithResidentsIsNonmutating()
    {
        var generatedWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(
            generatedWorld, 1);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
            civilizations);
        var start = civilizations.Records[0].StartCellId!.Value;
        var point = new EconomicPointRuntimeState(
            new EconomicPointId(1UL),
            civilizations.Records[0].Id,
            start,
            3UL,
            new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 2UL)
            });

        var session = new CanonicalGameSession(generatedWorld);
        session.AcceptSnapshot(session.Snapshot
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[] { point }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(new EconomicPointId(1UL), 10UL)
                })));

        var initial = session.Snapshot;
        var initialHash = new CanonicalWorldStateHasher().Compute(initial);
        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
                {
                    Phase()
                })),
            new TurnNumber(1UL), 1U);

        var result = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false),
            Input(session.Snapshot)));

        Assert.True(result.WasResolved);
        Assert.Same(initial, session.Snapshot);
        Assert.Equal(initialHash, new CanonicalWorldStateHasher().Compute(session.Snapshot));
        var evt = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            result.ResolutionResult!.Events.Single());
        Assert.Equal(5_000_000UL, evt.Flows.DemandFlows.Single().RawQuantity);
        Assert.Equal(2_000_000UL, evt.Flows.SupplyFlows.Single().RawQuantity);
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
    }

    [Fact]
    public void NullInputsAndNullProfilesFailFast()
    {
        var resolver = new EconomicResidentDemandResolver();
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(null!, Array.Empty<EconomicResidentDemandProfile>()));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(State(), null!));
        Assert.Throws<ArgumentException>(() =>
            resolver.Resolve(State(), new EconomicResidentDemandProfile[] { null! }));

        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentProductionDemandTurnPhase(
                null!, Array.Empty<EconomicResidentDemandProfile>()));
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentProductionDemandTurnPhase(
                Array.Empty<EconomicProductionPotential>(), null!));
    }
}
