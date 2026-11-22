using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationFractionalEdgeTravelProjectorTests
{
    private static (WorldState State, PhysicalWorkerRuntimeRecord Worker,
        PhysicalTacticalTileId Adjacent,
        PhysicalTacticalWorkerMoveEventExecutor Validator) Fixture()
    {
        var demo = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)),
            workersPerCivilization: 1);
        var worker = demo.Session.Snapshot.Workers.Workers.First(w =>
            w.Activity == PhysicalWorkerActivity.Producing);
        var world = demo.Session.GeneratedWorld;
        var map = demo.Incidence;
        var fine = FoundationFineFirstPhysicalGeography.Generate(world, map);
        var signature = FoundationFineFirstWorldgenCanonicalSignature
            .Compute(world, map, fine);
        var validator = new PhysicalTacticalWorkerMoveEventExecutor(
            world, map, fine, signature);
        var location = worker.TacticalTileId!.Value;
        var neighbor = map.FineTopology.Edges
            .Where(edge => edge.IncidentCellIds.Contains(
                location.FineStrategicCellId))
            .Select(edge => edge.IncidentCellIds
                .Single(id => id != location.FineStrategicCellId))
            .Where(tile => fine.CanTraverseOnFoot(
                checked((int)tile.Value - 1)))
            .Select(tile => new PhysicalTacticalTileId(
                map.Refinement.FineParameters, tile))
            .First(tile =>
            {
                try
                {
                    return validator.CheckEdge(location, tile).HasValue;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            });
        return (demo.Session.Snapshot, worker, neighbor, validator);
    }

    [Fact]
    public void OnePointFourTurnsYieldsFullTravelThenFortySixtyArrival()
    {
        var (world, worker, destination, validator) = Fixture();
        var initialHash = new CanonicalWorldStateHasher().Compute(world);
        var progress = FoundationFractionalEdgeTravelProjector.Start(
            worker, destination, 1_400_000UL, validator);

        var first = FoundationFractionalEdgeTravelProjector.Advance(
            progress, worker, validator);
        Assert.False(first.IsCommitted);
        Assert.False(first.Arrived);
        Assert.Equal(worker.TacticalTileId, first.PhysicalTileAfterTurn);
        Assert.Equal(400_000UL, first.ContinuingTravel!.RemainingTravelMicro);
        Assert.Single(first.TimeSlices);
        Assert.Equal(1_000_000U, first.TimeSlices[0].PresenceMicro);
        Assert.Equal(0U, first.TimeSlices[0].ProductiveEffortMicro);
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            first.TimeSlices[0].DemandActivity);

        var second = FoundationFractionalEdgeTravelProjector.Advance(
            first.ContinuingTravel!, worker, validator);
        Assert.True(second.Arrived);
        Assert.Null(second.ContinuingTravel);
        Assert.Equal(destination, second.PhysicalTileAfterTurn);
        Assert.Equal(2, second.TimeSlices.Count);
        Assert.Equal(400_000U, second.TimeSlices[0].PresenceMicro);
        Assert.Equal(0U, second.TimeSlices[0].ProductiveEffortMicro);
        Assert.Equal(600_000U, second.TimeSlices[1].PresenceMicro);
        Assert.Equal(600_000U, second.TimeSlices[1].ProductiveEffortMicro);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            second.TimeSlices[1].DemandActivity);

        var replayFirst = FoundationFractionalEdgeTravelProjector.Advance(
            progress, worker, validator);
        var replaySecond = FoundationFractionalEdgeTravelProjector.Advance(
            replayFirst.ContinuingTravel!, worker, validator);
        Assert.Equal(first.TimeSlices, replayFirst.TimeSlices);
        Assert.Equal(second.TimeSlices, replaySecond.TimeSlices);
        Assert.Equal(initialHash, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void PhysicalTravelOutputFeedsRealTemporalDemandAndBirthWeights()
    {
        var (world, worker, destination, validator) = Fixture();
        var travel = FoundationFractionalEdgeTravelProjector.Start(
            worker, destination, 1_400_000UL, validator);
        var first = FoundationFractionalEdgeTravelProjector.Advance(
            travel, worker, validator);
        var second = FoundationFractionalEdgeTravelProjector.Advance(
            first.ContinuingTravel!, worker, validator);
        var other = world.Workers.Workers.Single(w => w.Id != worker.Id);
        var completeSchedule = second.TimeSlices.Concat(new[]
        {
            new FoundationWorkerTemporalNodeSlice(
                other.Id, other.TacticalTileId!.Value,
                1_000_000U, 0U, PhysicalWorkerActivity.Traveling)
        }).ToArray();
        var commodities = new[] { new CommodityId(1U), new CommodityId(2U) };
        var demandPolicy = new FoundationActivityCommodityDemandPolicy(
            commodities,
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => commodities.Select(commodity =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, commodity, 1_000_000UL))));
        var report = FoundationPhysicalPresenceDemographicProjector.Project(
            world, completeSchedule, demandPolicy);
        Assert.Equal(400_000UL,
            report.Buckets.Single(x => x.Owner == worker.Owner &&
                x.Node == worker.TacticalTileId!.Value).PresenceMicro);
        Assert.Equal(600_000UL,
            report.Buckets.Single(x => x.Owner == worker.Owner &&
                x.Node == destination).PresenceMicro);
        Assert.Equal(600_000UL,
            report.Buckets.Single(x => x.Owner == worker.Owner &&
                x.Node == destination).ProductiveEffortMicro);
        Assert.Equal(4_000_000UL, report.Demand.Aggregate(0UL,
            (sum, row) => checked(sum + row.DemandMicro)));

        var births = FoundationPhysicalPresenceDemographicProjector.PlanBirths(
            world, report, world.Civilizations.Civilizations, turn: 2);
        Assert.Equal(world.Civilizations.Civilizations.Count,
            births.Births.Count);
        Assert.All(births.Births, birth => Assert.Contains(report.Buckets,
            bucket => bucket.Owner == birth.Owner &&
                bucket.Node == birth.NatalNode));
    }

    [Fact]
    public void CrossingRejectsForgedStateAndOnlyOneTurnIsConsumed()
    {
        var (_, worker, destination, validator) = Fixture();
        var initial = FoundationFractionalEdgeTravelProjector.Start(
            worker, destination, 300_000UL, validator);
        var arrived = FoundationFractionalEdgeTravelProjector.Advance(
            initial, worker, validator);
        Assert.True(arrived.Arrived);
        Assert.Equal(300_000U, arrived.TimeSlices[0].PresenceMicro);
        Assert.Equal(700_000U, arrived.TimeSlices[1].ProductiveEffortMicro);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFractionalEdgeTravelProjector.Advance(
                initial with { RemainingTravelMicro = 0UL },
                worker, validator));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationFractionalEdgeTravelProjector.Advance(
                initial with
                {
                    Crossing = new PhysicalTacticalEdgeId(
                        initial.Crossing.FineGoldbergParameters,
                        new StrategicEdgeId(
                            initial.Crossing.FineStrategicEdgeId.Value == 1UL
                                ? 2UL : 1UL))
                }, worker, validator));
        Assert.Throws<ArgumentException>(() =>
            FoundationFractionalEdgeTravelProjector.Start(
                worker, worker.TacticalTileId!.Value, 900_000UL, validator));
    }
}
