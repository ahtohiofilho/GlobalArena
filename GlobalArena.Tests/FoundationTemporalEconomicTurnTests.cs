using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationTemporalEconomicTurnTests
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



    private static FoundationActivityCommodityDemandPolicy Demand()
    {
        var items = new[] { new CommodityId(1U), new CommodityId(2U) };
        return new FoundationActivityCommodityDemandPolicy(
            items, Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => items.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
    }

    private static (
        WorldState Before, WorldState After,
        FoundationEdgeTravelTurnEvent Event,
        PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Destination)
        FirstTurn()
    {
        var (world, worker, destination, movement) = Fixture();
        var before = FoundationPersistedEdgeTravelTransitions.DeployEmpty(world);
        var phase = new FoundationEdgeTravelTurnPhase(movement,
            new FoundationGroundEdgeTravelTimePolicy(1_400_000UL));
        var turn = new TurnNumber(1UL);
        var context = new SimulationContext(turn, new SimulationSeed(712UL));
        var command = new FoundationEdgeTravelTurnCommand(
            new CommandId(turn, 1UL), worker.Owner, worker.Id,
            destination, isStart: true);
        var resolved = phase.Resolve(
            new TurnResolutionInput(before, new ISimulationCommand[] { command },
                context), before);
        return (before, resolved.ResultingWorldState,
            Assert.IsType<FoundationEdgeTravelTurnEvent>(
                Assert.Single(resolved.Events)), worker, destination);
    }

    [Fact]
    public void FirstTravelTurnHasNoMovingWorkerProductionButFullDemand()
    {
        var (before, after, travel, worker, _) = FirstTurn();
        var result = FoundationTemporalEconomicTurnProjector.Project(
            before, after, new[] { travel }, Demand());
        Assert.Equal((ulong)before.Workers.Workers.Count * 1_000_000UL,
            result.TotalPresenceMicro);
        Assert.Equal(0U,
            result.Slices.Single(s => s.Worker == worker.Id).ProductiveEffortMicro);
        Assert.Equal(1_000_000U,
            result.Slices.Single(s => s.Worker == worker.Id).PresenceMicro);
        Assert.DoesNotContain(result.Production, p => p.Worker == worker.Id);
        Assert.Equal(2_000_000UL,
            result.Presence.Demand.Where(d => d.Owner == worker.Owner)
                .Aggregate(0UL, (sum, d) => checked(sum + d.DemandMicro)));
        Assert.Single(after.EdgeTravels.Pending);
        Assert.False(result.IsCommitted);
    }

    [Fact]
    public void ArrivalTurnAllocatesFortyPercentOriginSixtyPercentDestination()
    {
        var (firstStart, firstEnd, _, worker, destination) = FirstTurn();
        var (_, _, movement) = RebuildMovement(firstStart, worker, destination);
        var (secondEnd, receipt) =
            FoundationPersistedEdgeTravelTransitions.Advance(
                firstEnd, worker.Id, movement);
        var travel = new FoundationEdgeTravelTurnEvent(
            new EventId(new CommandId(new TurnNumber(2UL), 1UL), 1UL),
            worker.Owner, worker.Id, destination, receipt);
        var report = FoundationTemporalEconomicTurnProjector.Project(
            firstEnd, secondEnd, new[] { travel }, Demand());
        Assert.Equal(400_000U, report.Slices[0].PresenceMicro);
        Assert.Equal(600_000U, report.Slices[1].PresenceMicro);
        Assert.Equal(600_000U, report.Slices[1].ProductiveEffortMicro);
        Assert.Equal(400_000UL,
            report.Presence.Buckets.Single(b =>
                b.Owner == worker.Owner &&
                b.Node == worker.TacticalTileId!.Value).PresenceMicro);
        Assert.Equal(600_000UL,
            report.Presence.Buckets.Single(b =>
                b.Owner == worker.Owner && b.Node == destination).PresenceMicro);
        var output = Assert.Single(report.Production, p => p.Worker == worker.Id);
        Assert.Equal(destination, output.Node);
        Assert.Equal(600_000U, output.EffortMicro);
        Assert.Equal((ulong)((UInt128)worker.OutputMicroPerTurn * 600_000U /
            1_000_000U), output.ProducedMicro);
        Assert.Equal(2_000_000UL,
            report.Presence.Demand.Where(d => d.Owner == worker.Owner)
                .Aggregate(0UL, (sum, d) => checked(sum + d.DemandMicro)));
        Assert.Empty(secondEnd.EdgeTravels.Pending);
    }

    private static (WorldState, PhysicalWorkerRuntimeRecord,
        PhysicalTacticalWorkerMoveEventExecutor) RebuildMovement(
        WorldState world, PhysicalWorkerRuntimeRecord worker,
        PhysicalTacticalTileId destination)
    {
        // Resolve using the same signed topology provided by Fixture().
        // Deterministic scenario construction yields the same physical edge.
        var (otherWorld, otherWorker, otherDestination, movement) = Fixture();
        Assert.Equal(worker.Id, otherWorker.Id);
        Assert.Equal(destination, otherDestination);
        Assert.Equal(world.WorldBinding, otherWorld.WorldBinding);
        return (otherWorld, otherWorker, movement);
    }

    [Fact]
    public void RejectsMissingOrDuplicateWorkerTimeAndUnjournaledMoves()
    {
        var (before, after, ev, worker, _) = FirstTurn();
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicTurnProjector.Project(
                before, after, Array.Empty<FoundationEdgeTravelTurnEvent>(),
                Demand()));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTemporalEconomicTurnProjector.Project(
                before, after, new[] { ev, ev }, Demand()));

    }
}
