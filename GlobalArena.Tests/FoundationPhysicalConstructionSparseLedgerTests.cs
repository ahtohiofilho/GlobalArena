using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Scale and location invariants: the builder remains at a real fine NODE,
/// demands goods at that node, and only contributes labor to an incident
/// physical EDGE. Construction v14 snapshots use structurally shared
/// physical edges, not one full inventory copy per worker journal event.
/// </summary>
public sealed class FoundationPhysicalConstructionSparseLedgerTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalTacticalEdgeId Edge,
        PhysicalTacticalTileId FirstEndpoint,
        PhysicalTacticalTileId SecondEndpoint,
        PhysicalWorkerRuntimeRecord[] Builders);

    private static Scenario Create(int workersPerCivilization = 6,
        bool splitEndpoints = false)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: workersPerCivilization);
        var actual = fixture.Snapshot.Workers.Workers
            .Where(w => w.Owner == Red).ToArray();
        Assert.Equal(workersPerCivilization, actual.Length);
        var home = actual[0].TacticalTileId!.Value;
        var fine = fixture.Incidence.Refinement.FineParameters;
        var crossing = fixture.Incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(home.FineStrategicCellId));
        var edge = new PhysicalTacticalEdgeId(fine, crossing.Id);
        var farCell = crossing.IncidentCellIds[0] == home.FineStrategicCellId
            ? crossing.IncidentCellIds[1] : crossing.IncidentCellIds[0];
        var far = new PhysicalTacticalTileId(fine, farCell);
        var move = new PhysicalTacticalWorkerMoveEventExecutor(
            fixture.Session.GeneratedWorld, fixture.Incidence);

        var workforce = new PhysicalWorkerRuntimeState(
            fixture.Snapshot.Workers.Workers.Select(worker =>
            {
                if (worker.Owner != Red) return worker;
                var newTile = splitEndpoints && worker.Id == actual[1].Id
                    ? far : worker.TacticalTileId!.Value;
                var coarse = move.PresentationRegion(newTile, worker.CellId);
                return worker.WithTacticalLocationAndActivity(
                    newTile, coarse, PhysicalWorkerActivity.Other);
            }),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var operation = new FoundationNaturalTransportSeedingPolicy(
            new FoundationNaturalTransportCostBand(1_000_000UL, 1_000_000UL),
            new FoundationNaturalTransportCostBand(20_000_000UL, 100_000UL),
            new FoundationNaturalTransportCostBand(100_000_000UL, 10_000UL),
            4_000_000UL);
        var construction = new FoundationNaturalConstructionSeedingPolicy(
            10_000_000UL, 20_000_000UL, 30_000_000UL, 8_000_000UL);
        var proposed = FoundationPhysicalNaturalTransportSeeder.Create(
            fixture.Session.GeneratedWorld, fixture.Incidence,
            operation, construction);
        var v13 = fixture.Snapshot.WithWorkers(workforce)
            .DeployInitialPhysicalTransport(
                proposed.Transport, fixture.Session.GeneratedWorld,
                fixture.Incidence);
        fixture.Session.AcceptSnapshot(v13);
        var host = new FoundationTacticalHumanAiTurnHost(
            fixture.Session, fixture.Incidence,
            new[] { Red },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            {
                [Blue] = new FoundationTacticalAiHoldPolicy()
            },
            new FoundationPhysicalConstructionLaborPolicy(1_000UL));
        var builders = workforce.Workers
            .Where(w => w.Owner == Red).ToArray();
        return new Scenario(host, edge, home, far, builders);
    }

    [Fact]
    public void ImmutableEdgeRecordsAreSharedAndProgressCopiesOnlyTouchedPages()
    {
        var s = Create();
        var original = s.Host.Snapshot.Transport;
        Assert.True(original.Edges.Count > 256 * 2);
        Assert.False(original.HasCommittedConstructionLabor);
        Assert.Equal(0, original.AllocatedConstructionPageCount);
        var first = original.Edges[0].Edge;
        var last = original.Edges[^1].Edge;
        Assert.NotEqual(first, last);

        var withFirst = original.ApplyJournaledWork(first, 1_000UL);
        var withLast = withFirst.ApplyJournaledWork(last, 1_000UL);
        var withMoreFirst = withLast.ApplyJournaledWork(first, 1_000UL);

        Assert.True(original.SharesCanonicalPhysicalStorageWith(withFirst));
        Assert.True(original.SharesCanonicalPhysicalStorageWith(withLast));
        Assert.True(withLast.SharesCanonicalPhysicalStorageWith(withMoreFirst));
        Assert.Equal(1, withFirst.AllocatedConstructionPageCount);
        Assert.Equal(2, withLast.AllocatedConstructionPageCount);
        Assert.Equal(2, withMoreFirst.AllocatedConstructionPageCount);

        Assert.True(withMoreFirst.TryGetEdge(first, out var firstRecord));
        Assert.True(withMoreFirst.TryGetEdge(last, out var lastRecord));
        Assert.Equal(2_000UL, firstRecord!.CommittedConstructionLaborMicro);
        Assert.Equal(1_000UL, lastRecord!.CommittedConstructionLaborMicro);
        Assert.True(withLast.TryGetEdge(first, out var oldFirst));
        Assert.Equal(1_000UL, oldFirst!.CommittedConstructionLaborMicro);
        Assert.True(withFirst.TryGetEdge(last, out var untouched));
        Assert.Equal(0UL, untouched!.CommittedConstructionLaborMicro);
        Assert.All(original.Edges, row =>
            Assert.Null(row.CommittedConstructionLaborMicro));
        Assert.False(original.HasCommittedConstructionLabor);
        Assert.True(withFirst.HasCommittedConstructionLabor);
        Assert.Equal(original.Edges.Count, withMoreFirst.Edges.Count);
        Assert.All(withMoreFirst.Edges, row =>
        {
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                row.OwnershipKind);
            Assert.Null(row.InfrastructureOwner);
            Assert.Equal(0UL, row.ExternalTariffMicroPerWholeCargoUnit);
        });
    }

    [Fact]
    public void OppositeEndpointBuildersKeepDistinctNodeDemandWhileWorkingOneEdge()
    {
        var s = Create(splitEndpoints: true);
        var before = s.Host.Snapshot;
        var workforceBefore = FoundationPhysicalWorkforceEconomicProjector
            .Project(before.Workers);
        var redAtA = workforceBefore.DemandSites.Single(site =>
            site.Owner == Red && site.Tile == s.FirstEndpoint);
        var redAtB = workforceBefore.DemandSites.Single(site =>
            site.Owner == Red && site.Tile == s.SecondEndpoint);
        Assert.Equal(s.Builders.Length - 1, redAtA.WorkerCount);
        Assert.Equal(1, redAtB.WorkerCount);
        Assert.Equal(0, redAtA.ProducingWorkers + redAtB.ProducingWorkers);

        var orders = new[]
        {
            new FoundationPhysicalConstructionWorkAssignment(
                Red, s.Builders[0].Id, s.Edge),
            new FoundationPhysicalConstructionWorkAssignment(
                Red, s.Builders[1].Id, s.Edge)
        };
        Assert.Equal(2, s.Host.QueueHumanConstructionBatch(Red, orders));
        Assert.Equal(before.Revision, s.Host.Snapshot.Revision);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Equal(2, receipt!.ConstructionEvents.Count);
        Assert.Equal(14U, receipt.Hash.FormatVersion);
        Assert.All(receipt.ConstructionEvents, ev =>
            Assert.Equal(s.Edge, ev.Edge));
        Assert.Contains(receipt.ConstructionEvents,
            ev => ev.WorkerTile == s.FirstEndpoint);
        Assert.Contains(receipt.ConstructionEvents,
            ev => ev.WorkerTile == s.SecondEndpoint);

        var after = s.Host.Snapshot;
        Assert.Same(before.Workers, after.Workers);
        var afterDemand = FoundationPhysicalWorkforceEconomicProjector
            .Project(after.Workers);
        Assert.Equal(workforceBefore.DemandSites.ToArray(),
            afterDemand.DemandSites.ToArray());
        Assert.Equal(workforceBefore.TotalDemandMultiplierMicro,
            afterDemand.TotalDemandMultiplierMicro);
        Assert.Equal(workforceBefore.WorkerCount, afterDemand.WorkerCount);
        Assert.True(after.Transport.TryGetEdge(s.Edge, out var road));
        Assert.Equal(2_000UL, road!.CommittedConstructionLaborMicro);
        Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
            road.OwnershipKind);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void HundredTwentyRealBuildersPreserveHistoryDemandAndReplay()
    {
        var s = Create(workersPerCivilization: 120);
        var before = s.Host.Snapshot;
        var initialHash = s.Host.StateHash;
        Assert.True(before.Transport.Edges.Count > 2_000);
        var demandBefore = FoundationPhysicalWorkforceEconomicProjector
            .Project(before.Workers);
        var orders = s.Builders.Select(w =>
            new FoundationPhysicalConstructionWorkAssignment(
                Red, w.Id, s.Edge)).ToArray();
        Assert.Equal(120,
            s.Host.QueueHumanConstructionBatch(Red, orders));
        Assert.Equal(initialHash, s.Host.StateHash);
        s.Host.SetHumanReady(Red);
        var receipt = s.Host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Equal(120, receipt!.ConstructionEvents.Count);
        Assert.Equal(120, receipt.ExecutedEvents);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(14U, receipt.Hash.FormatVersion);
        Assert.Equal(120_000UL,
            s.Host.Snapshot.Transport.Edges.Single(r => r.Edge == s.Edge)
                .CommittedConstructionLaborMicro);
        Assert.Equal(before.Revision + 120UL,
            s.Host.Snapshot.Revision);
        Assert.Equal(receipt.Hash, s.Host.StateHash);
        Assert.True(before.Transport.SharesCanonicalPhysicalStorageWith(
            s.Host.Snapshot.Transport));
        Assert.Equal(1,
            s.Host.Snapshot.Transport.AllocatedConstructionPageCount);
        Assert.Same(before.Workers, s.Host.Snapshot.Workers);
        Assert.False(before.Transport.HasCommittedConstructionLabor);
        Assert.Equal(initialHash, new CanonicalWorldStateHasher()
            .Compute(before));
        var demandAfter = FoundationPhysicalWorkforceEconomicProjector
            .Project(s.Host.Snapshot.Workers);
        Assert.Equal(demandBefore.DemandSites.ToArray(),
            demandAfter.DemandSites.ToArray());
        Assert.Equal(demandBefore.TotalDemandMultiplierMicro,
            demandAfter.TotalDemandMultiplierMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void CanonicalWorkerLookupUsesExactIndividualIds()
    {
        var s = Create(workersPerCivilization: 10);
        var census = s.Host.Snapshot.Workers;
        Assert.Equal(census.Workers.Count,
            census.Workers.Select(w => w.Id).Distinct().Count());
        foreach (var worker in census.Workers)
            Assert.Same(worker, census.FindWorker(worker.Id));
        Assert.Null(census.FindWorker(new PhysicalWorkerId(99_999UL)));
        Assert.Null(census.FindWorker(default));
    }
}
