using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationDemographicGrowthPlannerTests
{
    private static (WorldState World,
        FoundationDemographicCityPopulation[] Cities,
        CivilizationId[] Civilizations) Preview(int workersPerCivilization)
    {
        var fixture = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: workersPerCivilization);
        var world = fixture.Session.Snapshot;
        var cityGroups = world.Workers.Workers
            .GroupBy(w => (w.Owner, Tile: w.TacticalTileId!.Value))
            .OrderBy(g => g.Key.Owner.Value)
            .ThenBy(g => g.Key.Tile.FineStrategicCellId.Value)
            .ToArray();
        var cities = cityGroups.Select((g, i) =>
            new FoundationDemographicCityPopulation(
                checked((ulong)i + 1UL),
                g.Key.Owner, g.Key.Tile, checked((ulong)g.Count()))).ToArray();
        return (world, cities, world.Civilizations.Civilizations.ToArray());
    }

    [Fact]
    public void OneInitialWorkerPerCivilizationYieldsExactlyOneWorldwideBirthSlotPerSurvivor()
    {
        var (world, cities, survivors) = Preview(1);
        var original = new CanonicalWorldStateHasher().Compute(world);
        Assert.Equal(2, world.Workers.Workers.Count);
        Assert.Equal(2, cities.Length);
        var a = FoundationDemographicGrowthPlanner.Plan(
            world, cities, survivors, turn: 1);
        var b = FoundationDemographicGrowthPlanner.Plan(
            world, cities.Reverse().ToArray(),
            survivors.Reverse().ToArray(), turn: 1);

        Assert.False(a.IsCommitted);
        Assert.Equal(2, a.SurvivingCivilizations);
        Assert.Equal(2, a.Births.Count);
        Assert.Equal(4UL, a.Cities.Aggregate(0UL,
            (sum, city) => checked(sum + city.After)));
        Assert.Equal(2UL, a.Cities.Aggregate(0UL,
            (sum, city) => checked(sum + city.Added)));
        Assert.Equal(a.Births, b.Births);
        Assert.Equal(a.Cities, b.Cities);
        Assert.Equal(original, new CanonicalWorldStateHasher().Compute(world));
        Assert.Equal(2, world.Workers.Workers.Count);
        Assert.All(a.Births, birth =>
            Assert.Contains(cities, city => city.CityId == birth.CityId &&
                city.Owner == birth.Owner && city.Tile == birth.Tile));
    }

    [Fact]
    public void GrowthIsWorldwideRatherThanGuaranteedOneBirthForEachCivilization()
    {
        var (world, cities, survivors) = Preview(3);
        // Multiple deterministic turns are sampled independently from the
        // same base world. This tests the contract; births are not committed.
        bool bothBirthsSameCivilization = Enumerable.Range(1, 128)
            .Select(turn => FoundationDemographicGrowthPlanner.Plan(
                world, cities, survivors, checked((ulong)turn)))
            .Any(plan => plan.Births.Select(b => b.Owner).Distinct().Count() == 1);
        Assert.True(bothBirthsSameCivilization,
            "Worldwide birth slots must be able to accumulate in one civilization.");
        Assert.All(Enumerable.Range(1, 8), turn =>
        {
            var plan = FoundationDemographicGrowthPlanner.Plan(
                world, cities, survivors, checked((ulong)turn));
            Assert.Equal(survivors.Length, plan.Births.Count);
            Assert.Equal(6UL + (ulong)survivors.Length,
                plan.Cities.Aggregate(0UL,
                    (sum, city) => checked(sum + city.After)));
        });

        // Extinction changes the worldwide birth budget immediately;
        // surviving city populations are supplied for surviving owners.
        var onlyFirst = survivors.Take(1).ToArray();
        var eligibleCities = cities
            .Where(c => c.Owner == onlyFirst[0]).ToArray();
        var reduced = FoundationDemographicGrowthPlanner.Plan(
            world, eligibleCities, onlyFirst, turn: 2);
        Assert.Single(reduced.Births);
        Assert.Equal(1, reduced.SurvivingCivilizations);
        Assert.Equal(4UL, reduced.Cities.Single().After);
        Assert.Equal(onlyFirst[0], reduced.Births.Single().Owner);
    }

    [Fact]
    public void TwoCitiesCanBelongToOneCivilizationWithoutReservedCivilizationBirths()
    {
        var (original, _, survivors) = Preview(3);
        var migrant = original.Workers.Workers.First(w =>
            w.Owner == survivors[0] &&
            w.Activity == PhysicalWorkerActivity.Producing);
        var newTile = new PhysicalTacticalTileId(
            migrant.TacticalTileId!.Value.FineGoldbergParameters,
            new StrategicCellId(checked(
                migrant.TacticalTileId.Value.FineStrategicCellId.Value + 1UL)));
        var relocated = original.Workers.Workers.Select(w =>
            w.Id == migrant.Id
                ? w.WithTacticalLocationAndActivity(
                    newTile, w.CellId, w.Activity)
                : w).ToArray();
        var world = original.WithWorkers(new PhysicalWorkerRuntimeState(
            relocated, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true));
        var cities = world.Workers.Workers
            .GroupBy(w => (w.Owner, Tile: w.TacticalTileId!.Value))
            .OrderBy(g => g.Key.Owner.Value)
            .ThenBy(g => g.Key.Tile.FineStrategicCellId.Value)
            .Select((group, index) => new FoundationDemographicCityPopulation(
                checked((ulong)index + 1UL), group.Key.Owner,
                group.Key.Tile, checked((ulong)group.Count())))
            .ToArray();
        Assert.Equal(3, cities.Length);
        Assert.Equal(2, cities.Count(c => c.Owner == survivors[0]));
        var plan = FoundationDemographicGrowthPlanner.Plan(
            world, cities, survivors, turn: 47);
        Assert.Equal(2, plan.Births.Count);
        Assert.Equal(3, plan.Cities.Count);
        Assert.Equal(2UL, plan.Cities.Aggregate(0UL,
            (sum, c) => checked(sum + c.Added)));
        Assert.All(plan.Births, birth =>
            Assert.Contains(cities, c => c.CityId == birth.CityId &&
                c.Owner == birth.Owner));
    }

    [Fact]
    public void RejectsTamperedCityPopulationAndDuplicatePhysicalCityIdentities()
    {
        var (world, cities, survivors) = Preview(1);
        Assert.Throws<ArgumentException>(() =>
            FoundationDemographicGrowthPlanner.Plan(
                world, new[]
                {
                    cities[0] with { Population = 2UL }, cities[1]
                }, survivors, turn: 1));
        Assert.Throws<ArgumentException>(() =>
            FoundationDemographicGrowthPlanner.Plan(
                world, new[] { cities[0], cities[0] },
                survivors, turn: 1));
        Assert.Throws<ArgumentException>(() =>
            FoundationDemographicGrowthPlanner.Plan(
                world, cities, survivors, turn: 0));
        Assert.Throws<ArgumentException>(() =>
            FoundationDemographicGrowthPlanner.Plan(
                world, cities, new[] { survivors[0], survivors[0] }, turn: 1));
    }
}
