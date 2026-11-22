using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalPresenceDemographicTests
{
    private static (WorldState State,
        FoundationActivityCommodityDemandPolicy Demand,
        CivilizationId[] Owners) Fixture()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)),
            workersPerCivilization: 1);
        var state = host.Session.Snapshot;
        var goods = new[] { new CommodityId(1U), new CommodityId(2U) };
        var policy = new FoundationActivityCommodityDemandPolicy(
            goods,
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => goods.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
        return (state, policy, state.Civilizations.Civilizations.ToArray());
    }

    [Fact]
    public void FortyPercentTravelAndSixtyPercentWorkPreserveTimeAndSplitDemand()
    {
        var (world, policy, owners) = Fixture();
        var traveler = world.Workers.Workers.Single(w =>
            w.Activity == PhysicalWorkerActivity.Traveling);
        var original = traveler.TacticalTileId!.Value;
        var target = new PhysicalTacticalTileId(
            original.FineGoldbergParameters,
            new StrategicCellId(original.FineStrategicCellId.Value + 1UL));
        var arrival = FoundationPhysicalPresenceDemographicProjector
            .ArrivalWithinTurn(traveler.Id, original, target, 400_000U);
        Assert.Equal(2, arrival.Count);
        Assert.Equal(400_000U, arrival[0].PresenceMicro);
        Assert.Equal(0U, arrival[0].ProductiveEffortMicro);
        Assert.Equal(600_000U, arrival[1].PresenceMicro);
        Assert.Equal(600_000U, arrival[1].ProductiveEffortMicro);

        var staying = world.Workers.Workers.Single(w => w.Id != traveler.Id);
        var schedule = arrival.Concat(new[]
        {
            new FoundationWorkerTemporalNodeSlice(staying.Id,
                staying.TacticalTileId!.Value, 1_000_000U, 1_000_000U,
                PhysicalWorkerActivity.Producing)
        }).ToArray();
        var hash = new CanonicalWorldStateHasher().Compute(world);
        var a = FoundationPhysicalPresenceDemographicProjector.Project(
            world, schedule, policy);
        var b = FoundationPhysicalPresenceDemographicProjector.Project(
            world, schedule.Reverse().ToArray(), policy);
        Assert.Equal(2_000_000UL, a.WorkerTurnMicro);
        Assert.Equal(a.ScheduleDigest, b.ScheduleDigest);
        Assert.Equal(a.Buckets, b.Buckets);
        Assert.Equal(a.Demand, b.Demand);
        Assert.Equal(400_000UL, a.Buckets.Single(x =>
            x.Owner == traveler.Owner && x.Node == original).PresenceMicro);
        Assert.Equal(600_000UL, a.Buckets.Single(x =>
            x.Owner == traveler.Owner && x.Node == target).PresenceMicro);
        Assert.Equal(600_000UL, a.Buckets.Single(x =>
            x.Owner == traveler.Owner && x.Node == target).ProductiveEffortMicro);
        Assert.Equal(400_000UL, a.Demand.Single(x =>
            x.Owner == traveler.Owner && x.Node == original &&
            x.Commodity == new CommodityId(1U)).DemandMicro);
        Assert.Equal(600_000UL, a.Demand.Single(x =>
            x.Owner == traveler.Owner && x.Node == target &&
            x.Commodity == new CommodityId(1U)).DemandMicro);
        Assert.Equal(4_000_000UL, a.Demand.Aggregate(0UL,
            (sum, x) => checked(sum + x.DemandMicro)));

        var births = FoundationPhysicalPresenceDemographicProjector.PlanBirths(
            world, a, owners, turn: 17);
        var replay = FoundationPhysicalPresenceDemographicProjector.PlanBirths(
            world, b, owners.Reverse().ToArray(), turn: 17);
        Assert.Equal(births.Births, replay.Births);
        Assert.Equal(2, births.Births.Count);
        Assert.All(births.Births, x =>
            Assert.Contains(a.Buckets, row =>
                row.Owner == x.Owner && row.Node == x.NatalNode));
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void BirthCanOccurAtNonCityTransitNodeWithoutChangingTheBirthBudget()
    {
        var (world, policy, owners) = Fixture();
        var traveler = world.Workers.Workers.Single(w =>
            w.Activity == PhysicalWorkerActivity.Traveling);
        var from = traveler.TacticalTileId!.Value;
        var to = new PhysicalTacticalTileId(
            from.FineGoldbergParameters,
            new StrategicCellId(from.FineStrategicCellId.Value + 1UL));
        var other = world.Workers.Workers.Single(w => w.Id != traveler.Id);
        var schedule = FoundationPhysicalPresenceDemographicProjector
            .ArrivalWithinTurn(traveler.Id, from, to, 400_000U)
            .Append(new FoundationWorkerTemporalNodeSlice(
                other.Id, other.TacticalTileId!.Value,
                1_000_000U, 1_000_000U, PhysicalWorkerActivity.Producing))
            .ToArray();
        var presence = FoundationPhysicalPresenceDemographicProjector.Project(
            world, schedule, policy);

        bool atTransit = Enumerable.Range(1, 150)
            .Select(turn => FoundationPhysicalPresenceDemographicProjector
                .PlanBirths(world, presence, owners, (ulong)turn))
            .Any(plan => plan.Births.Any(x =>
                x.Owner == traveler.Owner && x.NatalNode == to &&
                x.NatalCity is null));
        Assert.True(atTransit,
            "A passing worker must make a noncity node eligible for birth.");
        Assert.All(Enumerable.Range(1, 12), turn =>
            Assert.Equal(owners.Length,
                FoundationPhysicalPresenceDemographicProjector.PlanBirths(
                    world, presence, owners, (ulong)turn).Births.Count));
    }

    [Fact]
    public void RejectsIncompleteTimeInventedPeopleOrInvalidEffort()
    {
        var (world, policy, owners) = Fixture();
        var person = world.Workers.Workers.First();
        var tile = person.TacticalTileId!.Value;
        var full = world.Workers.Workers.Select(w =>
            new FoundationWorkerTemporalNodeSlice(
                w.Id, w.TacticalTileId!.Value, 1_000_000U, 0U,
                PhysicalWorkerActivity.Traveling)).ToArray();

        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalPresenceDemographicProjector.Project(
                world, full.Skip(1).ToArray(), policy));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalPresenceDemographicProjector.Project(
                world, full.Append(full[0]).ToArray(), policy));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalPresenceDemographicProjector.Project(
                world, full.Select(x => x.Worker == person.Id
                    ? x with { ProductiveEffortMicro = 1U }
                    : x).ToArray(), policy));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalPresenceDemographicProjector.Project(
                world, full.Select(x => x.Worker == person.Id
                    ? x with { Worker = new PhysicalWorkerId(999UL) }
                    : x).ToArray(), policy));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalPresenceDemographicProjector.ArrivalWithinTurn(
                person.Id, tile, tile, 400_000U));
        var valid = FoundationPhysicalPresenceDemographicProjector.Project(
            world, full, policy);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalPresenceDemographicProjector.PlanBirths(
                world, valid, new[] { owners[0], owners[0] }, 1UL));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalPresenceDemographicProjector.PlanBirths(
                world, valid, owners, 0UL));
    }
}
