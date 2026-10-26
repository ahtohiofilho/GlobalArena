using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoodDemandAccessCoverageTests
{
    private static FoodCommodityCatalog Catalog() =>
        new(new[]
        {
            new FoodCommodityDefinition(new CommodityId(8U), "roots", 2_000UL),
            new FoodCommodityDefinition(new CommodityId(7U), "grain", 1_000UL)
        });

    private static FoodMarketDemandCoverageEntry FoodTrade(
        ulong point = 2UL, uint commodity = 7U,
        ulong demand = 5_000_000UL, ulong allocated = 1_000_000UL) =>
        new(new EconomicPointId(point), new CommodityId(commodity),
            demand, allocated);

    private static FoodPointAccessObservation Access(
        ulong point = 2UL, uint commodity = 7U,
        ulong raw = 2_000_000UL, string source = "survey-a") =>
        new(new EconomicPointId(point), new CommodityId(commodity), raw, source);

    private static FoodAccessObservationSnapshot Snapshot(
        params FoodPointAccessObservation[] observations) =>
        new(new TurnNumber(1UL), observations);

    private static FoodDemandAccessCoverageResult Resolve(
        FoodMarketDemandCoverageResult trade,
        FoodAccessObservationSnapshot? observations = null,
        FoodCommodityCatalog? catalog = null,
        ulong turn = 1UL) =>
        new FoodDemandAccessCoverageResolver().Resolve(
            trade, catalog ?? Catalog(),
            observations ?? Snapshot(),
            new TurnNumber(turn));

    private static WorldState State() =>
        WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(
                new[] { new CivilizationId(1UL), new CivilizationId(2UL) }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(
                        new EconomicPointId(1UL),
                        new CivilizationId(1UL),
                        new StrategicCellId(1UL),
                        2UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Agriculture, 2UL)
                        }),
                    new EconomicPointRuntimeState(
                        new EconomicPointId(2UL),
                        new CivilizationId(2UL),
                        new StrategicCellId(2UL), 0UL)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(new EconomicPointId(2UL), 10UL)
                }));

    private static EconomicResidentFoodAccessTurnPhase Phase(
        WorldState state,
        FoodAccessObservationSnapshot? access = null,
        IEnumerable<EconomicTransferSignal>? transferSignals = null) =>
        new(
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(7U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            FoodResidentDemandProfileMaterializer.Materialize(
                state, Catalog(),
                new[]
                {
                    new FoodResidentConsumptionRequirement(
                        new EconomicPointId(2UL), new CommodityId(7U), 500UL)
                }),
            transferSignals ?? new[]
            {
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL), new EconomicPointId(2UL)),
                    1, 100_000UL, 1_000_000UL, 0UL)
            },
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1),
            Catalog(),
            access ?? Snapshot(Access()));

    private static TurnResolutionInput Input(
        WorldState world, ulong turn = 1UL) =>
        new(world, Array.Empty<ISimulationCommand>(),
            new SimulationContext(
                new TurnNumber(turn), new SimulationSeed(127UL)));

    [Fact]
    public void MissingObservationMeansUnknownNotZeroAccess()
    {
        var trade = new FoodMarketDemandCoverageResult(new[] { FoodTrade() });
        var result = Resolve(trade);
        var entry = Assert.Single(result.Entries);

        Assert.False(entry.HasAccessObservation);
        Assert.Null(entry.RawAccessibleQuantity);
        Assert.Null(entry.RawDemandMatchedByAccess);
        Assert.Null(entry.RawDemandNotMatchedByAccess);
        Assert.Null(entry.RawAccessibleBeyondDemand);
        Assert.Null(entry.EvidenceSourceKey);
        Assert.Equal(5_000_000UL, result.TotalRawFoodDemand);
        Assert.Equal(1_000_000UL, result.TotalRawTradeAllocated);
        Assert.Equal(0UL, result.RawDemandWithKnownAccess);
        Assert.Equal(5_000_000UL, result.RawDemandWithUnknownAccess);
        Assert.Equal(0UL, result.RawDemandNotMatchedByKnownAccess);
    }

    [Fact]
    public void ExplicitZeroAccessIsDistinctFromUnknownAccess()
    {
        var report = Resolve(
            new FoodMarketDemandCoverageResult(new[] { FoodTrade() }),
            Snapshot(Access(raw: 0UL)));

        var row = Assert.Single(report.Entries);
        Assert.True(row.HasAccessObservation);
        Assert.Equal(0UL, row.RawAccessibleQuantity);
        Assert.Equal(0UL, row.RawDemandMatchedByAccess);
        Assert.Equal(5_000_000UL, row.RawDemandNotMatchedByAccess);
        Assert.Equal("survey-a", row.EvidenceSourceKey);
        Assert.Equal(5_000_000UL, report.RawDemandWithKnownAccess);
        Assert.Equal(0UL, report.RawDemandWithUnknownAccess);
        Assert.Equal(5_000_000UL, report.RawDemandNotMatchedByKnownAccess);
    }

    [Fact]
    public void MarketTradeAllocationDoesNotAutomaticallyBecomeAccessibleFood()
    {
        var coverage = new FoodMarketDemandCoverageResult(new[] { FoodTrade() });
        var unknown = Resolve(coverage);
        var reported = Resolve(coverage, Snapshot(Access(raw: 3_000_000UL)));

        Assert.Null(unknown.Entries.Single().RawAccessibleQuantity);
        Assert.Equal(1_000_000UL, reported.Entries.Single().RawTradeAllocatedQuantity);
        Assert.Equal(3_000_000UL, reported.Entries.Single().RawAccessibleQuantity);
        Assert.Equal(3_000_000UL, reported.RawDemandMatchedByKnownAccess);
        Assert.Equal(2_000_000UL, reported.RawDemandNotMatchedByKnownAccess);
    }

    [Fact]
    public void SurplusObservedAccessDoesNotProduceNegativeDemandGap()
    {
        var result = Resolve(
            new FoodMarketDemandCoverageResult(new[] { FoodTrade() }),
            Snapshot(Access(raw: 7_000_000UL)));
        var entry = Assert.Single(result.Entries);

        Assert.Equal(7_000_000UL, entry.RawAccessibleQuantity);
        Assert.Equal(5_000_000UL, entry.RawDemandMatchedByAccess);
        Assert.Equal(0UL, entry.RawDemandNotMatchedByAccess);
        Assert.Equal(2_000_000UL, entry.RawAccessibleBeyondDemand);
        Assert.Equal(5_000_000UL, result.RawDemandMatchedByKnownAccess);
    }

    [Fact]
    public void PartiallyObservedMultiFoodDemandPreservesUnknownRows()
    {
        var trade = new FoodMarketDemandCoverageResult(new[]
        {
            FoodTrade(point: 3UL, commodity: 8U,
                demand: 2_000_000UL, allocated: 0UL),
            FoodTrade(point: 2UL, commodity: 7U,
                demand: 5_000_000UL, allocated: 1_000_000UL)
        });
        var snapshot = Snapshot(Access(raw: 2_000_000UL));
        var result = Resolve(trade, snapshot);

        Assert.Equal(new[] { (2UL, 7U), (3UL, 8U) },
            result.Entries.Select(row =>
                (row.EconomicPointId.Value, row.CommodityId.Value)));
        Assert.True(result.Entries[0].HasAccessObservation);
        Assert.False(result.Entries[1].HasAccessObservation);
        Assert.Equal(7_000_000UL, result.TotalRawFoodDemand);
        Assert.Equal(1_000_000UL, result.TotalRawTradeAllocated);
        Assert.Equal(5_000_000UL, result.RawDemandWithKnownAccess);
        Assert.Equal(2_000_000UL, result.RawDemandWithUnknownAccess);
        Assert.Equal(2_000_000UL, result.RawDemandMatchedByKnownAccess);
        Assert.Equal(3_000_000UL, result.RawDemandNotMatchedByKnownAccess);
    }

    [Fact]
    public void AccessObservationMustMatchDeclaredDemandAndRegisteredFood()
    {
        var trade = new FoodMarketDemandCoverageResult(new[] { FoodTrade() });

        Assert.Throws<InvalidOperationException>(() =>
            Resolve(trade, Snapshot(Access(point: 42UL))));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(trade, Snapshot(Access(commodity: 100U))));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(trade, Snapshot(), new FoodCommodityCatalog(
                Array.Empty<FoodCommodityDefinition>())));
    }

    [Fact]
    public void AccessObservationIsTurnScopedAndStaleInputFailsClosed()
    {
        var trade = new FoodMarketDemandCoverageResult(new[] { FoodTrade() });
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(trade, Snapshot(Access()), turn: 2UL));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(trade, Snapshot(), turn: 2UL));
    }

    [Fact]
    public void ObservationSnapshotIsCanonicalAndRejectsOverlappingSources()
    {
        var source = new List<FoodPointAccessObservation>
        {
            Access(point: 3UL, commodity: 8U, raw: 0UL),
            Access(point: 2UL, commodity: 7U, raw: 4UL)
        };
        var snapshot = new FoodAccessObservationSnapshot(
            new TurnNumber(1UL), source);
        source.Clear();

        Assert.Equal(new[] { (2UL, 7U), (3UL, 8U) },
            snapshot.Observations.Select(row =>
                (row.EconomicPointId.Value, row.CommodityId.Value)));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<FoodPointAccessObservation>)snapshot.Observations).Clear());
        Assert.Throws<ArgumentException>(() =>
            Snapshot(Access(), Access(source: "alternate")));
        Assert.Throws<ArgumentException>(() =>
            new FoodAccessObservationSnapshot(default,
                Array.Empty<FoodPointAccessObservation>()));
        Assert.Throws<ArgumentException>(() =>
            Snapshot(null!));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodAccessObservationSnapshot(new TurnNumber(1UL), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Research")]
    [InlineData("survey id")]
    [InlineData("survéy")]
    public void InvalidEvidenceSourceKeysAreRejected(string source)
    {
        Assert.Throws<ArgumentException>(() =>
            Access(source: source));
    }

    [Fact]
    public void ZeroAndDefaultIdentitiesAreRejectedInSourceRecords()
    {
        Assert.Throws<ArgumentException>(() =>
            new FoodPointAccessObservation(
                default, new CommodityId(7U), 0UL, "source-a"));
        Assert.Throws<ArgumentException>(() =>
            new FoodPointAccessObservation(
                new EconomicPointId(2UL), default, 0UL, "source-a"));
        Assert.Throws<ArgumentNullException>(() =>
            Access(source: null!));
    }

    [Fact]
    public void AccessComparisonRejectsMismatchingRowsAndNull()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FoodDemandAccessCoverageEntry(null!, null));
        Assert.Throws<ArgumentException>(() =>
            new FoodDemandAccessCoverageEntry(
                FoodTrade(), Access(point: 3UL)));
    }

    [Fact]
    public void ResultTotalsFailOnUnsignedOverflowAndDuplicateKeys()
    {
        var large = new FoodDemandAccessCoverageEntry(
            FoodTrade(point: 2UL,
                demand: ulong.MaxValue, allocated: 0UL), null);
        var small = new FoodDemandAccessCoverageEntry(
            FoodTrade(point: 3UL, demand: 1UL, allocated: 0UL), null);

        Assert.Throws<OverflowException>(() =>
            new FoodDemandAccessCoverageResult(new[] { large, small }));
        Assert.Throws<ArgumentException>(() =>
            new FoodDemandAccessCoverageResult(new[] { small, small }));
        Assert.Throws<ArgumentException>(() =>
            new FoodDemandAccessCoverageResult(
                new FoodDemandAccessCoverageEntry[] { null! }));
    }

    [Fact]
    public void ResolverAndResultRequireNonNullInputs()
    {
        var resolver = new FoodDemandAccessCoverageResolver();
        var trade = new FoodMarketDemandCoverageResult(new[] { FoodTrade() });
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(null!, Catalog(), Snapshot(), new TurnNumber(1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(trade, null!, Snapshot(), new TurnNumber(1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            resolver.Resolve(trade, Catalog(), null!, new TurnNumber(1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            new FoodDemandAccessCoverageResult(null!));
    }

    [Fact]
    public void IntegratedFoodAccessPhaseEmitsFourOrderedIndependentEvents()
    {
        var state = State();
        var initialHash = new CanonicalWorldStateHasher().Compute(state);
        var phase = Phase(state, Snapshot(Access(raw: 2_000_000UL)));

        var resolved = phase.Resolve(Input(state), state);

        Assert.Equal(4, resolved.Events.Count);
        Assert.Equal(4, resolved.EventLog.Entries.Count);
        Assert.All(resolved.EventLog.Entries,
            row => Assert.True(row.WasExecuted));

        var flow = Assert.IsType<EconomicProductionDemandResolvedEvent>(
            resolved.Events[0]);
        var market = Assert.IsType<EconomicMarketAllocationResolvedEvent>(
            resolved.Events[1]);
        var trade = Assert.IsType<EconomicFoodMarketCoverageResolvedEvent>(
            resolved.Events[2]);
        var access = Assert.IsType<EconomicFoodAccessCoverageResolvedEvent>(
            resolved.Events[3]);

        Assert.Equal(2_000_000UL,
            flow.Flows.SupplyFlows.Single().RawQuantity);
        Assert.Equal(5_000_000UL,
            flow.Flows.DemandFlows.Single().RawQuantity);
        Assert.Equal(1_000_000UL,
            market.Allocation.TotalRawAllocatedQuantity);
        Assert.Equal(4_000_000UL,
            trade.Coverage.TotalRawUnallocatedQuantity);
        Assert.Equal(2_000_000UL,
            access.Coverage.RawDemandMatchedByKnownAccess);
        Assert.Equal(3_000_000UL,
            access.Coverage.RawDemandNotMatchedByKnownAccess);
        Assert.Equal(1_000_000UL,
            access.Coverage.TotalRawTradeAllocated);
        Assert.Equal("survey-a",
            access.Coverage.Entries.Single().EvidenceSourceKey);

        Assert.All(resolved.Events,
            evt => Assert.Equal(phase.PhaseId, evt.Id.SystemPhaseId));
        Assert.Equal(4, resolved.Events.Select(e => e.Id).Distinct().Count());
        Assert.Same(state, resolved.ResultingWorldState);
        Assert.Equal(initialHash,
            new CanonicalWorldStateHasher().Compute(state));
    }

    [Fact]
    public void MissingObservedAccessStillEmitsUnknownNotFakeZeroFromTrades()
    {
        var state = State();
        var resolved = Phase(state, Snapshot()).Resolve(Input(state), state);
        var access = Assert.IsType<EconomicFoodAccessCoverageResolvedEvent>(
            resolved.Events[3]).Coverage;

        Assert.Single(access.Entries);
        Assert.Null(access.Entries[0].RawAccessibleQuantity);
        Assert.Equal(5_000_000UL, access.RawDemandWithUnknownAccess);
        Assert.Equal(0UL, access.RawDemandNotMatchedByKnownAccess);
        Assert.Equal(1_000_000UL, access.TotalRawTradeAllocated);
    }

    [Fact]
    public void IntegratedPhaseRejectsStaleAccessSnapshotBeforeEvents()
    {
        var state = State();
        var phase = Phase(state, Snapshot(Access()));
        Assert.Throws<InvalidOperationException>(() =>
            phase.Resolve(Input(state, turn: 2UL), state));
    }

    [Fact]
    public void AccessPhaseCannotCoexistWithFoodMarketAlternative()
    {
        var state = State();
        var accessPhase = Phase(state);
        var other = new EconomicResidentFoodMarketTurnPhase(
            Array.Empty<EconomicProductionPotential>(),
            Array.Empty<EconomicResidentDemandProfile>(),
            Array.Empty<EconomicTransferSignal>(),
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1),
            Catalog());

        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                accessPhase, other
            }));
    }

    [Fact]
    public void GeneratedWorldAuthoritativeCommandlessTurnPreservesHashAndCensus()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL), WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));
        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 2);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(
            civilizations);

        var producer = new EconomicPointRuntimeState(
            new EconomicPointId(1UL),
            civilizations.Records[0].Id,
            civilizations.Records[0].StartCellId!.Value,
            2UL,
            new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 2UL)
            });
        var consumers = new EconomicPointRuntimeState(
            new EconomicPointId(2UL),
            civilizations.Records[1].Id,
            civilizations.Records[1].StartCellId!.Value,
            0UL);

        var state = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[] { producer, consumers }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(
                        new EconomicPointId(1UL), 2UL),
                    new PopulationPointRuntimeState(
                        new EconomicPointId(2UL), 10UL)
                }));
        var route = new StrategicEconomicRouteResolver(
            world.StrategicSurfaceGraph).ResolveDetailed(
                state.Economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(2UL)),
                StrategicEdgeAccessSnapshot.AllOpen);
        Assert.True(route.IsReachable);
        var signal = new EconomicTransferSignalResolver().Resolve(
            state.Economy, route.Route!,
            new EconomicTransferPolicy(1UL, 1_000_000UL, 0UL, 0UL));

        var phase = Phase(state, Snapshot(Access()), new[] { signal });
        var session = new CanonicalGameSession(world);
        session.AcceptSnapshot(state);
        var hash = new CanonicalWorldStateHasher().Compute(state);

        var gate = new AuthoritativeTurnCommitGate(
            session,
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
                {
                    phase
                })),
            new TurnNumber(1UL), 1U);
        var resolved = gate.ResolveAndCommit(
            new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(new TurnNumber(1UL), true, false),
                Input(session.Snapshot)));

        Assert.True(resolved.WasResolved);
        Assert.Equal(4, resolved.ResolutionResult!.Events.Count);
        var report = Assert.IsType<EconomicFoodAccessCoverageResolvedEvent>(
            resolved.ResolutionResult.Events[3]).Coverage;
        Assert.Equal(2_000_000UL, report.RawDemandMatchedByKnownAccess);
        Assert.Equal(3_000_000UL, report.RawDemandNotMatchedByKnownAccess);
        Assert.Same(state, session.Snapshot);
        Assert.Equal(hash,
            new CanonicalWorldStateHasher().Compute(session.Snapshot));
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
    }

    [Fact]
    public void EmptyCensusFailsPriorToAnyAccessObservationOrEvent()
    {
        var state = WorldState.CreateInitial();
        var phase = new EconomicResidentFoodAccessTurnPhase(
            Array.Empty<EconomicProductionPotential>(),
            Array.Empty<EconomicResidentDemandProfile>(),
            Array.Empty<EconomicTransferSignal>(),
            new EconomicMarketAllocationPolicy(
                900_000UL, 900_000UL, 1, 1),
            Catalog(),
            Snapshot());

        Assert.Throws<InvalidOperationException>(() =>
            phase.Resolve(Input(state), state));
    }

    [Fact]
    public void NullPhaseInputsFailFast()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentFoodAccessTurnPhase(
                Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicResidentDemandProfile>(),
                Array.Empty<EconomicTransferSignal>(),
                new EconomicMarketAllocationPolicy(
                    900_000UL, 900_000UL, 1, 1),
                null!, Snapshot()));
        Assert.Throws<ArgumentNullException>(() =>
            new EconomicResidentFoodAccessTurnPhase(
                Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicResidentDemandProfile>(),
                Array.Empty<EconomicTransferSignal>(),
                new EconomicMarketAllocationPolicy(
                    900_000UL, 900_000UL, 1, 1),
                Catalog(), null!));

        var state = State();
        var phase = Phase(state);
        Assert.Throws<ArgumentNullException>(() =>
            phase.Resolve(null!, state));
        Assert.Throws<ArgumentNullException>(() =>
            phase.Resolve(Input(state), null!));
    }
}
