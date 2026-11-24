using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationConstructionWorkerWorkTests
{
    private static (WorldState World, PhysicalTacticalIncidenceMap Incidence,
        FoundationFineFirstPhysicalGeography Geography, PhysicalWorkerId Worker) Scenario()
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var source = fixture.Session.GeneratedWorld;
        var incidence = fixture.Incidence;
        var geography = FoundationFineFirstPhysicalGeography.Generate(source, incidence);
        var transport = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            source, incidence, geography,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL), 10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var world = fixture.Snapshot.DeployInitialPhysicalTransport(
            transport.Transport, source, incidence);
        world = world.DeployInitialTreasury(CivilizationTreasuryRuntimeState.CreateOpening(
            world.Civilizations.Civilizations.Select(c => (c, 10_000_000UL))));
        var person = world.Workers.Workers.First(w =>
            w.TacticalTileId.HasValue && incidence.FineTopology.Edges.Any(e =>
                e.IncidentCellIds.Contains(w.TacticalTileId.Value.FineStrategicCellId) &&
                geography.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                    (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground));
        var tile = person.TacticalTileId!.Value;
        var edge = incidence.FineTopology.Edges.First(e =>
            e.IncidentCellIds.Contains(tile.FineStrategicCellId) &&
            geography.CrossingAtIndices((int)e.IncidentCellIds[0].Value - 1,
                (int)e.IncidentCellIds[1].Value - 1) == FoundationFineTransportCrossingKind.Ground);
        world = world.WithWorkers(new PhysicalWorkerRuntimeState(
            world.Workers.Workers.Select(w => w.Id == person.Id
                ? w.WithTacticalAssignment(tile, w.CellId, PhysicalWorkerActivity.Other,
                    PhysicalWorkerOccupation.Unassigned) : w),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        world = world.WithConstructions(new FoundationConstructionRuntimeState(
            Array.Empty<FoundationConstructionProject>()));
        world = world.FundConstruction(person.Owner,
            new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, edge.Id),
            1_000_000UL, 1_600_000UL);
        return (world, incidence, geography, person.Id);
    }

    [Fact]
    public void RealWorkerProgressSurvivesTurnsAndReturnsResidualWithoutCapitalCredit()
    {
        var s = Scenario();
        var assignment = new[] { new FoundationConstructionWorkerAssignment(s.Worker, 1UL) };
        var first = FoundationConstructionWorkerWork.Apply(s.World, s.Incidence, s.Geography, assignment);
        Assert.Equal(1_000_000UL, Assert.Single(first.Workers).UsedWorkMicro);
        Assert.Equal(600_000UL, first.Snapshot.Constructions.Projects.Single().RemainingWorkMicro);
        var second = FoundationConstructionWorkerWork.Apply(first.Snapshot.AdvanceRevision(),
            s.Incidence, s.Geography, assignment);
        var receipt = Assert.Single(second.Workers);
        Assert.Equal(600_000UL, receipt.UsedWorkMicro);
        Assert.Equal(400_000UL, receipt.ResidualWorkMicro);
        Assert.True(second.Snapshot.Constructions.Projects.Single().IsComplete);
        Assert.Same(s.World.Transport, second.Snapshot.Transport);
        Assert.Same(s.World.Treasury, second.Snapshot.Treasury);
        Assert.Same(s.World.Workers, second.Snapshot.Workers);
        Assert.Throws<InvalidOperationException>(() => FoundationConstructionWorkerWork.Apply(
            second.Snapshot, s.Incidence, s.Geography, assignment));
    }

    [Fact]
    public void RejectsDoubleBookingAndOperationalWorker()
    {
        var s = Scenario();
        var a = new FoundationConstructionWorkerAssignment(s.Worker, 1UL);
        Assert.Throws<InvalidOperationException>(() => FoundationConstructionWorkerWork.Apply(
            s.World, s.Incidence, s.Geography, new[] { a, a }));
        var logistics = s.World.WithWorkers(new PhysicalWorkerRuntimeState(
            s.World.Workers.Workers.Select(w => w.Id == s.Worker
                ? w.WithTacticalOccupation(PhysicalWorkerOccupation.LandTransport) : w),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true));
        Assert.Throws<InvalidOperationException>(() => FoundationConstructionWorkerWork.Apply(
            logistics, s.Incidence, s.Geography, new[] { a }));
        Assert.Same(s.World, FoundationConstructionWorkerWork.Apply(
            s.World, s.Incidence, s.Geography, Array.Empty<FoundationConstructionWorkerAssignment>()).Snapshot);
    }
}
