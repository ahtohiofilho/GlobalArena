using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Real all-ready tactical turns journal physical construction exactly once
/// per worker. The v14 transport ledger stores accumulated LABOR ONLY:
/// no road, title, cargo upgrade, stock, money or economic publication.
/// </summary>
public sealed class FoundationPhysicalConstructionCommittedTurnTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeRecord Builder,
        PhysicalWorkerRuntimeRecord Other,
        PhysicalTacticalEdgeId Crossing);

    private static Scenario Create(ulong laborPerWorker = 100_000UL)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var people = fixture.Snapshot.Workers.Workers
            .Where(w => w.Owner == Red).Take(2).ToArray();
        Assert.Equal(2, people.Length);
        var first = people[0];
        var idleIds = new HashSet<PhysicalWorkerId> { first.Id };
        var workers = new PhysicalWorkerRuntimeState(
            fixture.Snapshot.Workers.Workers.Select(w =>
                idleIds.Contains(w.Id)
                    ? w.WithTacticalLocationAndActivity(
                        w.TacticalTileId!.Value, w.CellId,
                        PhysicalWorkerActivity.Other)
                    : w),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var operating = new FoundationNaturalTransportSeedingPolicy(
            new FoundationNaturalTransportCostBand(1_000_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(20_000_000UL, 100_000UL),
            new FoundationNaturalTransportCostBand(100_000_000UL, 10_000UL),
            4_000_000UL);
        var construction = new FoundationNaturalConstructionSeedingPolicy(
            2_000_000UL, 6_000_000UL, 10_000_000UL, 8_000_000UL);
        var natural = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence,
            operating, construction);
        var initial = fixture.Snapshot.WithWorkers(workers)
            .DeployInitialPhysicalTransport(natural.Transport,
                fixture.Session.GeneratedWorld, fixture.Incidence);
        fixture.Session.AcceptSnapshot(initial);
        Assert.Equal(13U, new CanonicalWorldStateHasher()
            .Compute(initial).FormatVersion);
        var source = workers.Workers.Single(w => w.Id == first.Id);
        var target = fixture.Incidence.FineTopology.Edges
            .Where(edge => edge.IncidentCellIds.Contains(
                source.TacticalTileId!.Value.FineStrategicCellId))
            .Select(edge => new PhysicalTacticalEdgeId(
                fixture.Incidence.Refinement.FineParameters, edge.Id))
            .First();

        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence,
            new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            },
            new FoundationPhysicalConstructionLaborPolicy(laborPerWorker));
        return new Scenario(host, source,
            workers.Workers.Single(w => w.Id == people[1].Id), target);
    }

    private static PhysicalTransportEdgeRuntimeRecord Road(Scenario s) =>
        s.Host.Snapshot.Transport.Edges.Single(row =>
            row.Edge == s.Crossing);

    [Fact]
    public void AllReadyConstructionTurnCommitsOnlyRealLaborAndNoRoad()
    {
        var s = Create();
        var worldBefore = s.Host.Snapshot;
        var before = s.Host.StateHash;
        var oldEdge = Road(s);
        var demand = FoundationPhysicalWorkforceEconomicProjector
            .Project(worldBefore.Workers);
        s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing);
        Assert.Equal(1, s.Host.PendingOrderCount);
        Assert.Equal(before, s.Host.StateHash);
        Assert.Null(Road(s).CommittedConstructionLaborMicro);
        Assert.Null(s.Host.TryCloseTurn()); // Red not ready.
        Assert.Equal(before, s.Host.StateHash);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt!.ReplayMatched);
        Assert.Single(receipt.ConstructionEvents);
        Assert.Empty(receipt.Events);
        Assert.Equal(1, receipt.ExecutedEvents);
        Assert.Equal(14U, receipt.Hash.FormatVersion);
        Assert.Equal(14U, s.Host.StateHash.FormatVersion);
        Assert.NotEqual(before, receipt.Hash);
        Assert.Equal(worldBefore.Revision + 1UL, s.Host.Snapshot.Revision);
        Assert.Equal(receipt.Hash, s.Host.StateHash);
        Assert.Equal(100_000UL, Road(s).CommittedConstructionLaborMicro);
        Assert.Equal(oldEdge.NaturalConstructionLaborMicro,
            Road(s).NaturalConstructionLaborMicro);
        Assert.Equal(oldEdge.FreePhysicalCostMicroPerWholeCargoUnit,
            Road(s).FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(oldEdge.CargoMicroAtReferenceLoad,
            Road(s).CargoMicroAtReferenceLoad);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            Road(s).OwnershipKind);
        Assert.Null(Road(s).InfrastructureOwner);
        Assert.Equal(0UL, Road(s).ExternalTariffMicroPerWholeCargoUnit);
        Assert.All(s.Host.Snapshot.Transport.Edges
            .Where(row => row.Edge != s.Crossing), row =>
            Assert.Equal(0UL, row.CommittedConstructionLaborMicro));
        Assert.Equal(demand.TotalDemandMultiplierMicro,
            FoundationPhysicalWorkforceEconomicProjector
                .Project(s.Host.Snapshot.Workers)
                .TotalDemandMultiplierMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(0, s.Host.PendingOrderCount);
    }

    [Fact]
    public void TwoTurnsAccumulateSameWorkersLaborAndKeepReplayDeterministic()
    {
        var s = Create(125_000UL);
        for (int round = 1; round <= 3; round++)
        {
            var prior = s.Host.Snapshot;
            var priorHash = s.Host.StateHash;
            s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing);
            s.Host.SetHumanReady(Red);
            var receipt = s.Host.TryCloseTurn();
            Assert.NotNull(receipt);
            Assert.True(receipt!.ReplayMatched);
            Assert.Equal(1, receipt.ExecutedEvents);
            Assert.Equal(14U, receipt.Hash.FormatVersion);
            Assert.Equal((ulong)round * 125_000UL,
                Road(s).CommittedConstructionLaborMicro);
            Assert.Equal(prior.Revision + 1UL, s.Host.Snapshot.Revision);
            Assert.NotEqual(priorHash, s.Host.StateHash);
        }
        Assert.Equal(4UL, s.Host.NextTurn.Value);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            Road(s).OwnershipKind);
    }

    [Fact]
    public void MixedMovementAndConstructionUseOneAtomicReplayableTurn()
    {
        var s = Create();
        var before = s.Host.Snapshot;
        Assert.Equal(PhysicalWorkerActivity.Producing, s.Other.Activity);
        s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing);
        s.Host.QueueHumanActivity(Red, s.Other.Id,
            PhysicalWorkerActivity.Other);
        Assert.Equal(2, s.Host.PendingOrderCount);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Single(receipt!.Events);
        Assert.Single(receipt.ConstructionEvents);
        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(before.Revision + 2UL, s.Host.Snapshot.Revision);
        Assert.Equal(100_000UL, Road(s).CommittedConstructionLaborMicro);
        Assert.Equal(PhysicalWorkerActivity.Other,
            s.Host.Snapshot.Workers.Workers.Single(w =>
                w.Id == s.Other.Id).Activity);
    }

    [Fact]
    public void AWorkerCannotSimultaneouslyMoveAndBuildOrBuildWithoutOwnership()
    {
        var s = Create();
        var before = s.Host.StateHash;
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanConstruction(Blue, s.Builder.Id, s.Crossing));
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanConstruction(Red, s.Other.Id, s.Crossing));
        s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing);
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanActivity(Red, s.Builder.Id,
                PhysicalWorkerActivity.Traveling));
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing));
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanOccupation(Red, s.Builder.Id,
                PhysicalWorkerOccupation.Forestry));
        Assert.Equal(1, s.Host.PendingOrderCount);
        Assert.True(s.Host.CancelHumanOrder(Red, s.Builder.Id));
        Assert.Equal(0, s.Host.PendingOrderCount);
        Assert.Equal(before, s.Host.StateHash);
    }

    [Fact]
    public void ForgedAmountOwnerOrStaleEventCannotChangeTheWorld()
    {
        var s = Create();
        var before = s.Host.Snapshot;
        s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        var ev = Assert.Single(receipt!.ConstructionEvents);
        var executor = new PhysicalTransportConstructionEventExecutor(
            s.Host.Session.GeneratedWorld, s.Host.Incidence,
            new FoundationPhysicalConstructionLaborPolicy(100_000UL));
        var context = new SimulationContext(
            ev.Id.Turn, new SimulationSeed(712UL));
        var independent = executor.Execute(before, ev, context);
        Assert.Equal(receipt.Hash,
            new CanonicalWorldStateHasher().Compute(independent));
        var forgedAmount = new PhysicalTransportConstructionWorkedEvent(
            ev.Id, ev.Issuer, ev.WorkerId, ev.WorkerTile,
            ev.Edge, ev.CreditedLaborMicro + 1UL, ev.ExpectedWorldRevision);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(before, forgedAmount, context));
        var forgedOwner = new PhysicalTransportConstructionWorkedEvent(
            ev.Id, Blue, ev.WorkerId, ev.WorkerTile,
            ev.Edge, ev.CreditedLaborMicro, ev.ExpectedWorldRevision);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(before, forgedOwner, context));
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(independent, ev, context));
    }

    [Fact]
    public void LaborSufficiencyDoesNotTransferTitleOrInventMaterials()
    {
        var s = Create(100_000_000UL);
        var needed = Road(s).NaturalConstructionLaborMicro!.Value;
        s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Equal(needed, Road(s).CommittedConstructionLaborMicro);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            Road(s).OwnershipKind);
        Assert.Null(Road(s).InfrastructureOwner);
        Assert.Equal(0UL, Road(s).ExternalTariffMicroPerWholeCargoUnit);
        Assert.Throws<InvalidOperationException>(() =>
            s.Host.QueueHumanConstruction(Red, s.Builder.Id, s.Crossing));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }
}
