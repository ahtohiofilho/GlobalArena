using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalOperationalWorkProjectorTests
{
    private static (
        WorldState World,
        PhysicalTacticalIncidenceMap Incidence,
        FoundationFineFirstPhysicalGeography Geography,
        PhysicalWorkerId Worker) Scenario()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var source = fixture.Session.GeneratedWorld;
        var incidence = fixture.Incidence;
        var geography = FoundationFineFirstPhysicalGeography.Generate(
            source, incidence);
        var transport = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            source, incidence, geography,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var world = fixture.Snapshot.DeployInitialPhysicalTransport(
            transport.Transport, source, incidence);
        var person = world.Workers.Workers.First(w =>
            w.Activity == PhysicalWorkerActivity.Producing);
        // Construct an explicit test-only committed Other census to exercise
        // operations without allowing a producing worker to count twice.
        var workers = world.Workers.Workers.Select(w =>
            w.Id == person.Id
                ? w.WithTacticalLocationAndActivity(
                    w.TacticalTileId!.Value, w.CellId,
                    PhysicalWorkerActivity.Other)
                : w).ToArray();
        world = world.WithWorkers(new PhysicalWorkerRuntimeState(
            workers, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        return (world, incidence, geography, person.Id);
    }

    [Fact]
    public void RealWorkerAllocatesOnlyToRealIncidentCrossingsWithoutChangingWorld()
    {
        var s = Scenario();
        var worker = s.World.Workers.Workers.Single(w => w.Id == s.Worker);
        var tile = worker.TacticalTileId!.Value;
        var actual = FoundationPhysicalOperationalWorkProjector.Project(
            s.World, s.Incidence, s.Geography,
            new[] { new FoundationOperationalWorkerAssignment(
                s.Worker, FoundationOperationalWorkKind.Land) });
        Assert.Single(actual);
        var node = actual[0];
        Assert.Equal(tile, node.Tile);
        Assert.Equal(worker.Owner, node.Owner);
        Assert.Equal(1_000_000UL, node.Allocation.LandEffortMicro);
        Assert.Equal(0UL, node.Allocation.PortEffortMicro);
        var adjacent = s.Incidence.FineTopology.Edges.Where(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId))
            .Select(e => new PhysicalTacticalEdgeId(
                s.Incidence.Refinement.FineParameters, e.Id)).ToHashSet();
        Assert.All(node.Allocation.Shares, share =>
        {
            Assert.Contains(share.Edge, adjacent);
            // The physical node can have both land and port crossings.
            // A land-only worker pool must never supply effort to ports.
            if (share.Kind == FoundationOperationalWorkKind.Port)
                Assert.Equal(0UL, share.EffortMicro);
            else
                Assert.Equal(FoundationOperationalWorkKind.Land, share.Kind);
        });
        Assert.Equal(1_000_000UL,
            node.Allocation.Shares.Aggregate(
                node.Allocation.UnallocatedLandEffortMicro,
                (total, share) => checked(total + share.EffortMicro)));
        var repeated = FoundationPhysicalOperationalWorkProjector.Project(
            s.World, s.Incidence, s.Geography,
            new[] { new FoundationOperationalWorkerAssignment(
                s.Worker, FoundationOperationalWorkKind.Land) });
        Assert.Equal(node.Allocation.Shares, repeated[0].Allocation.Shares);
    }

    [Fact]
    public void NoAssignmentsMeansNoFictionalOperationalWorkers()
    {
        var s = Scenario();
        Assert.Empty(FoundationPhysicalOperationalWorkProjector.Project(
            s.World, s.Incidence, s.Geography,
            Array.Empty<FoundationOperationalWorkerAssignment>()));
    }

    [Fact]
    public void RejectsDuplicateWorkerAndUnknownWorker()
    {
        var s = Scenario();
        var one = new FoundationOperationalWorkerAssignment(
            s.Worker, FoundationOperationalWorkKind.Land);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalOperationalWorkProjector.Project(
                s.World, s.Incidence, s.Geography, new[] { one, one }));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalOperationalWorkProjector.Project(
                s.World, s.Incidence, s.Geography,
                new[] { new FoundationOperationalWorkerAssignment(
                    new PhysicalWorkerId(999_999UL),
                    FoundationOperationalWorkKind.Port) }));
    }

    [Fact]
    public void CannotDoubleBookAnExistingProducingWorker()
    {
        var s = Scenario();
        var producing = s.World.Workers.Workers.First(w =>
            w.Activity == PhysicalWorkerActivity.Producing);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalOperationalWorkProjector.Project(
                s.World, s.Incidence, s.Geography,
                new[] { new FoundationOperationalWorkerAssignment(
                    producing.Id, FoundationOperationalWorkKind.Land) }));
    }
    [Fact]
    public void JournaledOccupationIsTheOnlySourceOfCommittedOperationalWork()
    {
        var s = Scenario();
        var selected = s.World.Workers.Workers.Single(w => w.Id == s.Worker);
        var committed = s.World.Workers.Workers.Select(w =>
            w.Id == selected.Id
                ? w.WithTacticalOccupation(PhysicalWorkerOccupation.LandTransport)
                : w).ToArray();
        var world = s.World.WithWorkers(new PhysicalWorkerRuntimeState(
            committed, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        var allocations = FoundationPhysicalOperationalWorkProjector.ProjectCommitted(
            world, s.Incidence, s.Geography);
        Assert.Single(allocations);
        var node = allocations[0];
        Assert.Equal(selected.TacticalTileId, node.Tile);
        Assert.Equal(1_000_000UL, node.Allocation.LandEffortMicro);
        Assert.Equal(0UL, node.Allocation.PortEffortMicro);
        Assert.Equal(1_000_000UL,
            node.Allocation.Shares.Aggregate(
                node.Allocation.UnallocatedLandEffortMicro,
                (sum, work) => checked(sum + work.EffortMicro)));
        Assert.All(node.Allocation.Shares.Where(s =>
            s.Kind == FoundationOperationalWorkKind.Port),
            s => Assert.Equal(0UL, s.EffortMicro));
    }

    [Fact]
    public void CommittedRealLogisticsWorkerProducesServiceOnlyOnPhysicalIncidentEdges()
    {
        var s = Scenario();
        var selected = s.World.Workers.Workers.Single(w => w.Id == s.Worker);
        var committed = s.World.Workers.Workers.Select(w =>
            w.Id == selected.Id
                ? w.WithTacticalOccupation(PhysicalWorkerOccupation.LandTransport)
                : w).ToArray();
        var world = s.World.WithWorkers(new PhysicalWorkerRuntimeState(
            committed, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        var allocation = FoundationPhysicalOperationalWorkProjector
            .ProjectCommitted(world, s.Incidence, s.Geography);
        var rates = allocation.SelectMany(node => node.Allocation.Shares)
            .Where(share => share.EffortMicro > 0UL)
            .Select(share => new FoundationOperationalEdgeProductivity(
                share.Edge, share.Kind, 1_000_000UL))
            .ToArray();
        var result = FoundationOperationalServiceProjector.ProjectCommitted(
            world, s.Incidence, s.Geography, rates);
        Assert.Equal(1_000_000UL, result.TotalAllocatedEffortMicro +
            result.TotalIdleEffortMicro);
        Assert.Equal(result.TotalAllocatedEffortMicro, result.TotalServiceMicro);
        Assert.All(result.Edges, item =>
        {
            Assert.Equal(FoundationOperationalWorkKind.Land, item.Kind);
            Assert.True(item.EffortMicro > 0UL);
            Assert.Contains(item.Edge,
                s.Incidence.FineTopology.Edges
                    .Where(edge => edge.IncidentCellIds.Contains(
                        selected.TacticalTileId!.Value.FineStrategicCellId))
                    .Select(edge => new PhysicalTacticalEdgeId(
                        s.Incidence.Refinement.FineParameters, edge.Id)));
        });
    }

}
