using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationCityResidenceRuntimeTests
{
    private static (WorldState World,
        FoundationCityResidenceRuntimeState Ledger,
        CivilizationId[] Owners) Scenario()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var world = host.Session.Snapshot;
        var groups = world.Workers.Workers
            .GroupBy(w => (w.Owner, Tile: w.TacticalTileId!.Value))
            .OrderBy(x => x.Key.Owner.Value)
            .ThenBy(x => x.Key.Tile.FineStrategicCellId.Value)
            .ToArray();
        var cityIds = groups.Select((g, i) => new
        {
            g.Key,
            City = new FoundationCityRuntimeRecord(
                new FoundationCityId(checked((ulong)i + 1UL)),
                g.Key.Owner, g.Key.Tile)
        }).ToArray();
        var residences = world.Workers.Workers.Select(w =>
            new FoundationWorkerResidenceRuntimeRecord(w.Id,
                cityIds.Single(c => c.Key.Owner == w.Owner &&
                    c.Key.Tile == w.TacticalTileId!.Value).City.Id)).ToArray();
        var ledger = new FoundationCityResidenceRuntimeState(
            cityIds.Select(x => x.City), residences);
        return (world, ledger, world.Civilizations.Civilizations.ToArray());
    }

    [Fact]
    public void CanonicalResidenceIsIndependentOfWorkerTravelAndPreservedByClone()
    {
        var s = Scenario();
        var originalHash = new CanonicalWorldStateHasher().Compute(s.World);
        Assert.False(s.World.CityResidences.IsInitialized);

        var promoted = s.World.DeployInitialCityResidences(s.Ledger);
        var cityHash = new CanonicalWorldStateHasher().Compute(promoted);
        Assert.Equal(CanonicalWorldStateHasher.CanonicalCityResidenceFormatVersion,
            cityHash.FormatVersion);
        Assert.NotEqual(originalHash, cityHash);
        Assert.Equal(s.World.Workers.Workers.Count,
            promoted.CityResidences.Residences.Count);
        Assert.Equal(3UL, promoted.CityResidences.PopulationByResidence()[0].Population);
        Assert.Equal(3UL, promoted.CityResidences.PopulationByResidence()[1].Population);

        var traveler = promoted.Workers.Workers.Single(w =>
            w.Activity == PhysicalWorkerActivity.Traveling);
        var away = new PhysicalTacticalTileId(
            traveler.TacticalTileId!.Value.FineGoldbergParameters,
            new StrategicCellId(checked(
                traveler.TacticalTileId.Value.FineStrategicCellId.Value + 1UL)));
        var movedWorkers = new PhysicalWorkerRuntimeState(
            promoted.Workers.Workers.Select(w =>
                w.Id == traveler.Id
                    ? w.WithTacticalLocationAndActivity(
                        away, w.CellId, PhysicalWorkerActivity.Traveling)
                    : w),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var traveled = promoted.WithWorkers(movedWorkers);

        Assert.NotEqual(cityHash, new CanonicalWorldStateHasher().Compute(traveled));
        Assert.Same(promoted.CityResidences, traveled.CityResidences);
        Assert.Same(promoted.CityResidences,
            traveled.AdvanceRevision().CityResidences);
        Assert.Equal(3UL, traveled.CityResidences.PopulationByResidence()
            .Single(x => x.City.Owner == traveler.Owner).Population);

        var projected = FoundationDemographicGrowthPlanner.Plan(
            traveled, s.Owners, turn: 17);
        Assert.Equal(s.Owners.Length, projected.Births.Count);
        Assert.Equal(3UL, projected.Cities.Single(x =>
            x.Owner == traveler.Owner).Before);
        Assert.All(projected.Births, birth =>
            Assert.Contains(s.Ledger.Cities,
                city => city.Id.Value == birth.CityId &&
                    city.Owner == birth.Owner));

        var provisional = traveled.Workers.Workers
            .GroupBy(w => (w.Owner, Tile: w.TacticalTileId!.Value))
            .Select((g, i) => new FoundationDemographicCityPopulation(
                checked((ulong)i + 1UL), g.Key.Owner, g.Key.Tile,
                checked((ulong)g.Count()))).ToArray();
        Assert.Equal(3, provisional.Length); // Travel physically split a city.
        Assert.Equal(2, traveled.CityResidences.Cities.Count);
    }

    [Fact]
    public void CanonicalLedgerDoesNotDependOnCallerIterationOrder()
    {
        var s = Scenario();
        var a = s.World.DeployInitialCityResidences(s.Ledger);
        var shuffled = new FoundationCityResidenceRuntimeState(
            s.Ledger.Cities.Reverse(), s.Ledger.Residences.Reverse());
        var b = s.World.DeployInitialCityResidences(shuffled);
        Assert.Equal(new CanonicalWorldStateHasher().Compute(a),
            new CanonicalWorldStateHasher().Compute(b));
        Assert.Equal(a.CityResidences.PopulationByResidence(),
            b.CityResidences.PopulationByResidence());

        Assert.Throws<InvalidOperationException>(() =>
            a.DeployInitialCityResidences(s.Ledger));
    }

    [Fact]
    public void RejectsMissingDuplicateOrForeignWorkerResidence()
    {
        var s = Scenario();
        Assert.Throws<InvalidOperationException>(() =>
            s.World.DeployInitialCityResidences(
                new FoundationCityResidenceRuntimeState(
                    s.Ledger.Cities, s.Ledger.Residences.Skip(1))));
        Assert.Throws<ArgumentException>(() =>
            new FoundationCityResidenceRuntimeState(
                s.Ledger.Cities, s.Ledger.Residences
                    .Append(s.Ledger.Residences[0])));
        Assert.Throws<ArgumentException>(() =>
            new FoundationCityResidenceRuntimeState(
                s.Ledger.Cities.Append(s.Ledger.Cities[0]),
                s.Ledger.Residences));
        var person = s.World.Workers.Workers.First();
        var wrongCity = s.Ledger.Cities.Single(c => c.Owner != person.Owner);
        var corrupted = new FoundationCityResidenceRuntimeState(
            s.Ledger.Cities, s.Ledger.Residences.Select(r =>
                r.Worker == person.Id
                    ? r with { HomeCity = wrongCity.Id }
                    : r));
        Assert.Throws<InvalidOperationException>(() =>
            s.World.DeployInitialCityResidences(corrupted));
    }
}
