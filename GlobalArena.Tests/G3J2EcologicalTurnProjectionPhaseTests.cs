using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Opt-in G3-J2 projection is compatible with the existing phase engine
/// and kernel publication gate, but NEVER commits ecological stocks there.
/// </summary>
public sealed class G3J2EcologicalTurnProjectionPhaseTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId Site = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);

    private static WorldGenerationRequest Request() =>
        new(new WorldSeed(20261028UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(2, 0));

    private static WorldGenerationResult Generate() =>
        new DeterministicWorldGenerator().Generate(Request());

    private static WorldState World(WorldGenerationResult generated)
    {
        var potential = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            potential.WoodyVegetation.GetRawValue(id) > 0L &&
            potential.ExposedStone.GetRawValue(id) > 0L);

        return WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(Site, Owner, cell, 4UL,
                        new[]
                        {
                            new EconomicActivityWorkforceAllocation(
                                EconomicActivityKind.Forestry, 4UL)
                        })
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(Site, 4UL)
                }))
            .WithRestoredRevision(5UL);
    }

    private static EconomicEcologicalCheckpoint Checkpoint(
        WorldGenerationResult generated, WorldState world)
    {
        var policy = new EconomicNaturalCapitalPolicy(
            80UL * D, 80UL * D, 0L, 0L, 0L);
        return new EconomicEcologicalCheckpoint(
            generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, policy),
            EconomicMaterialReserveBook.Empty, policy,
            new EconomicPrimitiveCommodityMapping(Food, Wood, Stone));
    }

    private static EconomicDemandProfile[] Demand() =>
        new[] { new EconomicDemandProfile(Site, Food, 500_000L) };

    private static EconomicMaterialOriginHoldOrder[] Holds() =>
        new[] { new EconomicMaterialOriginHoldOrder(
            Site, Wood, EconomicActivityKind.Forestry, D / 100UL) };

    private static TurnResolutionInput Input(WorldState world, ulong turn) =>
        new(world, Array.Empty<ISimulationCommand>(),
            new SimulationContext(new TurnNumber(turn), new SimulationSeed(7UL)));

    [Fact]
    public void ExistingPhaseEngineEmitsTypedAuditWithRealizedProductionAndHeldOffer()
    {
        var generated = Generate();
        var world = World(generated);
        var previous = Checkpoint(generated, world);
        var worldHash = previous.WorldHash;
        var phase = new EconomicEcologicalTurnProjectionPhase(
            generated, previous, previous.Sha256Hex, Demand(), Holds());

        var resolved = new PhasedTurnResolutionEngine(
            new ITurnResolutionPhase[] { phase }).Resolve(Input(world, 1UL));

        Assert.Same(world, resolved.ResultingWorldState);
        Assert.Equal(worldHash, new CanonicalWorldStateHasher().Compute(world));
        var evt = Assert.IsType<EconomicEcologicalTurnProjectedEvent>(
            Assert.Single(resolved.Events));
        Assert.Equal(EventOriginKind.System, evt.Id.OriginKind);
        Assert.Equal(EconomicProductionDemandTurnPhase.StablePhaseId,
            evt.Id.SystemPhaseId);
        Assert.Equal(1UL, evt.Id.SystemOrdinal);
        Assert.Equal(new TurnNumber(1UL), evt.Id.Turn);
        Assert.Equal(previous.Sha256Hex, evt.PreviousCheckpointSha256Hex);
        Assert.NotEqual(previous.Sha256Hex, evt.ProposedCheckpointSha256Hex);
        Assert.Equal(1UL, evt.Projection.ProposedNext.NaturalCapital.LastAppliedTurn);
        Assert.Equal(D / 100UL,
            evt.Projection.ProposedNext.MaterialReserves.RawAvailable(Site, Wood));
        Assert.Equal(2UL * D,
            Assert.Single(evt.Projection.RealizedProductionAndDemand.DemandFlows)
                .RawQuantity);
        var harvested = Assert.Single(
            evt.Projection.RealizedProductionAndDemand.SupplyFlows);
        var offered = Assert.Single(evt.Projection.MarketOfferAndDemand.SupplyFlows);
        Assert.Equal(harvested.RawQuantity - D / 100UL, offered.RawQuantity);
        Assert.Equal(0UL, previous.NaturalCapital.LastAppliedTurn);
        Assert.Empty(previous.MaterialReserves.Balances);

        var log = Assert.Single(resolved.EventLog.Entries);
        Assert.True(log.WasEligible);
        Assert.True(log.WasExecuted);
        Assert.Equal(evt.Id, log.EventId);
    }

    [Fact]
    public void ProjectionIsDeterministicAndCallerOrderListsAreSnapshotted()
    {
        var generated = Generate();
        var world = World(generated);
        var previous = Checkpoint(generated, world);
        var profiles = Demand().ToList();
        var holds = Holds().ToList();
        var phase = new EconomicEcologicalTurnProjectionPhase(
            generated, previous, previous.Sha256Hex, profiles, holds);
        profiles.Clear();
        holds.Clear();

        var first = Assert.IsType<EconomicEcologicalTurnProjectedEvent>(
            Assert.Single(phase.Resolve(Input(world, 1UL), world).Events));
        var second = Assert.IsType<EconomicEcologicalTurnProjectedEvent>(
            Assert.Single(phase.Resolve(Input(world, 1UL), world).Events));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.ProposedCheckpointSha256Hex,
            second.ProposedCheckpointSha256Hex);
        Assert.Equal(D / 100UL,
            second.Projection.ProposedNext.MaterialReserves.RawAvailable(Site, Wood));
        Assert.Empty(previous.MaterialReserves.Balances);
    }

    [Fact]
    public void NoSecondWorkerOrResidentDemandPhaseCanBeInstalled()
    {
        var generated = Generate();
        var previous = Checkpoint(generated, World(generated));
        var ecological = new EconomicEcologicalTurnProjectionPhase(
            generated, previous, previous.Sha256Hex, Demand(), Holds());
        var other = new EconomicProductionDemandTurnPhase(
            Array.Empty<EconomicProductionPotential>(), Demand());
        Assert.Equal(other.PhaseId, ecological.PhaseId);
        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                ecological, other
            }));
        Assert.Throws<ArgumentException>(() =>
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
            {
                ecological, ecological
            }));
    }

    [Fact]
    public void ChangedStagedWorldOrDifferentPlanningSnapshotFailClosed()
    {
        var generated = Generate();
        var world = World(generated);
        var previous = Checkpoint(generated, world);
        var phase = new EconomicEcologicalTurnProjectionPhase(
            generated, previous, previous.Sha256Hex, Demand(), Holds());
        Assert.Throws<InvalidOperationException>(() =>
            phase.Resolve(Input(world, 1UL), world.AdvanceRevision()));
        Assert.Throws<InvalidOperationException>(() =>
            phase.Resolve(Input(world.AdvanceRevision(), 1UL), world));
        Assert.Equal(0UL, previous.NaturalCapital.LastAppliedTurn);
        Assert.Empty(previous.MaterialReserves.Balances);
    }

    [Fact]
    public void StaleAndSkippedTurnCannotEmitAnAuditEvent()
    {
        var generated = Generate();
        var world = World(generated);
        var previous = Checkpoint(generated, world);
        var phase = new EconomicEcologicalTurnProjectionPhase(
            generated, previous, previous.Sha256Hex, Demand(), Holds());

        Assert.Throws<InvalidOperationException>(() =>
            phase.Resolve(Input(world, 2UL), world));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicEcologicalTurnProjectionPhase(
                generated, previous, new string('F', 64), Demand(), Holds()));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicEcologicalTurnProjectionPhase(
                new DeterministicWorldGenerator().Generate(
                    new WorldGenerationRequest(new WorldSeed(9UL),
                        WorldGenerationVersion.Initial, new GoldbergParameters(2, 0))),
                previous, previous.Sha256Hex, Demand(), Holds()));
    }

    [Fact]
    public void InvalidOriginHoldDoesNotChangeStagedWorldOrEcologicalBooks()
    {
        var generated = Generate();
        var world = World(generated);
        var previous = Checkpoint(generated, world);
        var phase = new EconomicEcologicalTurnProjectionPhase(
            generated, previous, previous.Sha256Hex,
            Demand(), new[]
            {
                new EconomicMaterialOriginHoldOrder(
                    Site, Wood, EconomicActivityKind.Forestry, ulong.MaxValue)
            });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            phase.Resolve(Input(world, 1UL), world));
        Assert.Equal(previous.WorldHash, new CanonicalWorldStateHasher().Compute(world));
        Assert.Equal(0UL, previous.NaturalCapital.LastAppliedTurn);
        Assert.Empty(previous.MaterialReserves.Balances);
    }

    [Fact]
    public void ClosedAuthoritativeGatePublishesOnlyUnchangedWorldNotProjectedBooks()
    {
        var generated = Generate();
        var session = CanonicalGameSession.Start(Request());
        var economyWorld = World(generated);
        session.AcceptSnapshot(economyWorld);
        var accepted = session.Snapshot;
        var previous = Checkpoint(generated, accepted);
        var phase = new EconomicEcologicalTurnProjectionPhase(
            generated, previous, previous.Sha256Hex, Demand(), Holds());
        var gate = new AuthoritativeTurnCommitGate(
            session, new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(),
                new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { phase })),
            new TurnNumber(1UL), 1U);
        var hash = new CanonicalWorldStateHasher().Compute(accepted);

        var result = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false),
            Input(accepted, 1UL)));

        Assert.True(result.WasResolved);
        Assert.IsType<EconomicEcologicalTurnProjectedEvent>(
            Assert.Single(result.ResolutionResult!.Events));
        Assert.Same(accepted, session.Snapshot);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(session.Snapshot));
        Assert.Equal(0UL, previous.NaturalCapital.LastAppliedTurn);
        Assert.Empty(previous.MaterialReserves.Balances);
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
        // A new phase with the actually accepted ecological books would be
        // required before another economic turn can be projected. The kernel
        // gate does not claim to have durably accepted these books.
    }
}
