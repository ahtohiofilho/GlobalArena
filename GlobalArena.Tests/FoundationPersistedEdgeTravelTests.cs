using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPersistedEdgeTravelTests
{
    private static (WorldState World, PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Destination,
        PhysicalTacticalWorkerMoveEventExecutor Validator) Fixture()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 1);
        var worker = demo.Session.Snapshot.Workers.Workers.First(w =>
            w.Activity == PhysicalWorkerActivity.Producing);
        var fine = FoundationFineFirstPhysicalGeography.Generate(
            demo.Session.GeneratedWorld, demo.Incidence);
        var signature = FoundationFineFirstWorldgenCanonicalSignature.Compute(
            demo.Session.GeneratedWorld, demo.Incidence, fine);
        var validator = new PhysicalTacticalWorkerMoveEventExecutor(
            demo.Session.GeneratedWorld, demo.Incidence, fine, signature);
        var location = worker.TacticalTileId!.Value;
        var adjacent = demo.Incidence.FineTopology.Edges
            .Where(edge => edge.IncidentCellIds.Contains(
                location.FineStrategicCellId))
            .Select(edge => edge.IncidentCellIds.Single(
                id => id != location.FineStrategicCellId))
            .Where(id => fine.CanTraverseOnFoot(checked((int)id.Value - 1)))
            .Select(id => new PhysicalTacticalTileId(
                demo.Incidence.Refinement.FineParameters, id))
            .First(tile =>
            {
                try { return validator.CheckEdge(location, tile).HasValue; }
                catch (InvalidOperationException) { return false; }
            });
        var seeded = FoundationPhysicalNaturalTransportSeeder.PreviewFineFirst(
            demo.Session.GeneratedWorld, demo.Incidence, fine,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var bound = demo.Session.Snapshot.DeployInitialPhysicalTransport(
            seeded.Transport, demo.Session.GeneratedWorld, demo.Incidence);
        return (bound, worker, adjacent, validator);
    }


    [Fact]
    public void TravelOfOnePointFourTurnsPersistsBetweenWorldRevisionsAndReplays()
    {
        var (baseWorld, worker, to, validator) = Fixture();
        var original = new CanonicalWorldStateHasher().Compute(baseWorld);
        var startWorld = FoundationPersistedEdgeTravelTransitions.DeployEmpty(baseWorld);
        Assert.Equal(17U, new CanonicalWorldStateHasher().Compute(startWorld).FormatVersion);
        var policy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);

        var (started, startEvent) = FoundationPersistedEdgeTravelTransitions.Start(
            startWorld, worker.Id, to, validator, policy);
        Assert.Single(started.EdgeTravels.Pending);
        Assert.Equal(worker.TacticalTileId,
            started.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPersistedEdgeTravelTransitions.Start(
                started, worker.Id, to, validator, policy));
        var replayStart = FoundationPersistedEdgeTravelTransitions.ReplayStart(
            startWorld, startEvent, to, validator, policy);
        Assert.Equal(new CanonicalWorldStateHasher().Compute(started),
            new CanonicalWorldStateHasher().Compute(replayStart));

        var (first, firstEvent) = FoundationPersistedEdgeTravelTransitions.Advance(
            started, worker.Id, validator);
        Assert.Equal(400_000UL,
            first.EdgeTravels.Pending.Single().RemainingMicro);
        Assert.Equal(1_400_000UL,
            first.EdgeTravels.Pending.Single().OriginalDurationMicro);
        Assert.Equal(worker.TacticalTileId,
            first.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
        Assert.Equal(1_000_000U, firstEvent.TurnResult!.TimeSlices.Single().PresenceMicro);

        var replayFirst = FoundationPersistedEdgeTravelTransitions.ReplayAdvance(
            replayStart, firstEvent, validator);
        Assert.Equal(new CanonicalWorldStateHasher().Compute(first),
            new CanonicalWorldStateHasher().Compute(replayFirst));

        var (second, secondEvent) = FoundationPersistedEdgeTravelTransitions.Advance(
            first, worker.Id, validator);
        Assert.Empty(second.EdgeTravels.Pending);
        Assert.Equal(to,
            second.Workers.Workers.Single(w => w.Id == worker.Id).TacticalTileId);
        Assert.Equal(400_000U, secondEvent.TurnResult!.TimeSlices[0].PresenceMicro);
        Assert.Equal(600_000U, secondEvent.TurnResult.TimeSlices[1].ProductiveEffortMicro);
        var replaySecond = FoundationPersistedEdgeTravelTransitions.ReplayAdvance(
            replayFirst, secondEvent, validator);
        Assert.Equal(new CanonicalWorldStateHasher().Compute(second),
            new CanonicalWorldStateHasher().Compute(replaySecond));
        Assert.Equal(original, new CanonicalWorldStateHasher().Compute(baseWorld));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPersistedEdgeTravelTransitions.Advance(
                second, worker.Id, validator));
    }

    [Fact]
    public void HashRejectsReorderedOrForgedPendingJourneysAndStaleReplay()
    {
        var (world, worker, to, validator) = Fixture();
        var prepared = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var policy = new FoundationGroundEdgeTravelTimePolicy(1_400_000UL);
        var (started, receipt) = FoundationPersistedEdgeTravelTransitions.Start(
            prepared, worker.Id, to, validator, policy);
        var p = started.EdgeTravels.Pending.Single();
        Assert.Throws<InvalidOperationException>(() =>
            new FoundationEdgeTravelRuntimeState(new[]
            {
                p, p
            }));
        Assert.Throws<InvalidOperationException>(() =>
            new FoundationEdgeTravelRuntimeState(new[]
            {
                p with { RemainingMicro = 0UL }
            }));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPersistedEdgeTravelTransitions.ReplayStart(
                started, receipt, to, validator, policy));
    }
}
