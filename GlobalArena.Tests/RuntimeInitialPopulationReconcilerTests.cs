using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class RuntimeInitialPopulationReconcilerTests
{
    private static (WorldGenerationResult World,
        CivilizationRuntimeState Civilizations,
        StrategicTerritoryRuntimeState Territory) Fixture()
    {
        var world = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(
                new WorldSeed(0UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(2, 0)));

        var civilizations = RuntimeCivilizationMaterializer.Materialize(world, 3);
        var territory = RuntimeStrategicTerritoryMaterializer.MaterializeInitial(civilizations);
        return (world, civilizations, territory);
    }

    private static EconomicPointRuntimeState Point(
        ulong id,
        CivilizationRuntimeRecord civilization,
        ulong workforce,
        ulong allocatedAgriculture = 0UL)
    {
        var allocation = allocatedAgriculture == 0UL
            ? Array.Empty<EconomicActivityWorkforceAllocation>()
            : new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, allocatedAgriculture)
            };

        return new EconomicPointRuntimeState(
            new EconomicPointId(id),
            civilization.Id,
            civilization.StartCellId!.Value,
            workforce,
            allocation);
    }

    private static InitialCivilizationWorkforceBudget[] Workforce(
        CivilizationRuntimeState civilizations,
        ulong first = 0UL, ulong second = 0UL, ulong third = 0UL) =>
        new[]
        {
            new InitialCivilizationWorkforceBudget(civilizations.Records[0].Id, first),
            new InitialCivilizationWorkforceBudget(civilizations.Records[1].Id, second),
            new InitialCivilizationWorkforceBudget(civilizations.Records[2].Id, third)
        };

    private static InitialCivilizationPopulationBudget[] Population(
        CivilizationRuntimeState civilizations,
        ulong first = 0UL, ulong second = 0UL, ulong third = 0UL) =>
        new[]
        {
            new InitialCivilizationPopulationBudget(civilizations.Records[0].Id, first),
            new InitialCivilizationPopulationBudget(civilizations.Records[1].Id, second),
            new InitialCivilizationPopulationBudget(civilizations.Records[2].Id, third)
        };

    private static InitialEconomicPointPopulation Census(ulong point, ulong residents) =>
        new(new EconomicPointId(point), residents);

    [Fact]
    public void RealWorldCensusSeparatesResidentsWorkforceAndActivityAllocations()
    {
        var (world, civilizations, territory) = Fixture();

        var result = RuntimeInitialPopulationReconciler.Reconcile(
            world, civilizations, territory,
            new[]
            {
                Point(3UL, civilizations.Records[1], 3UL),
                Point(2UL, civilizations.Records[0], 2UL, 1UL),
                Point(1UL, civilizations.Records[0], 8UL, 5UL)
            },
            Workforce(civilizations, first: 10UL, second: 3UL),
            Population(civilizations, first: 35UL, second: 7UL),
            new[]
            {
                Census(2UL, 15UL),
                Census(3UL, 7UL),
                Census(1UL, 20UL)
            });

        Assert.Equal(new[] { 1UL, 2UL, 3UL },
            result.Economy.EconomicPoints.Select(point => point.Id.Value));
        Assert.Equal(new[] { 1UL, 2UL, 3UL },
            result.PointPopulations.Select(row => row.EconomicPointId.Value));
        Assert.Equal(new[] { 20UL, 15UL, 7UL },
            result.PointPopulations.Select(row => row.Residents));
        Assert.Equal(8UL, result.Economy.EconomicPoints[0].Workforce);
        Assert.Equal(5UL, result.Economy.EconomicPoints[0].AllocatedWorkforce);
        Assert.Equal(3UL, result.Economy.EconomicPoints[0].UnallocatedWorkforce);

        var bound = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomy(result.Economy);
        Assert.Equal(3, bound.Economy.EconomicPoints.Count);
        Assert.Equal(new[] { 1UL, 2UL, 3UL },
            result.CivilizationPopulations.Select(row => row.CivilizationId.Value));
    }

    [Fact]
    public void CensusCannotHaveFewerResidentsThanWorkersAtAPoint()
    {
        var (world, civilizations, territory) = Fixture();

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                new[] { Point(1UL, civilizations.Records[0], 5UL) },
                Workforce(civilizations, first: 5UL),
                Population(civilizations, first: 4UL),
                new[] { Census(1UL, 4UL) }));
    }

    [Fact]
    public void CensusRejectsMissingPointInsteadOfSilentlyAssumingZeroPopulation()
    {
        var (world, civilizations, territory) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                new[] { Point(1UL, civilizations.Records[0], 0UL) },
                Workforce(civilizations),
                Population(civilizations),
                Array.Empty<InitialEconomicPointPopulation>()));
    }

    [Fact]
    public void CensusRejectsDuplicatedPointRecords()
    {
        var (world, civilizations, territory) = Fixture();
        Assert.Throws<ArgumentException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                new[] { Point(1UL, civilizations.Records[0], 0UL) },
                Workforce(civilizations),
                Population(civilizations),
                new[] { Census(1UL, 0UL), Census(1UL, 0UL) }));
    }

    [Fact]
    public void CensusRejectsUnknownEconomicPoint()
    {
        var (world, civilizations, territory) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                Workforce(civilizations),
                Population(civilizations),
                new[] { Census(44UL, 0UL) }));
    }

    [Theory]
    [InlineData(11UL)]
    [InlineData(9UL)]
    public void CensusRejectsCreationOrLossOfCivilizationResidents(ulong expectedResidents)
    {
        var (world, civilizations, territory) = Fixture();

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                new[] { Point(1UL, civilizations.Records[0], 5UL) },
                Workforce(civilizations, first: 5UL),
                Population(civilizations, first: expectedResidents),
                new[] { Census(1UL, 10UL) }));
    }

    [Fact]
    public void CensusRejectsAbsentCivilizationPopulationBudget()
    {
        var (world, civilizations, territory) = Fixture();

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                Workforce(civilizations),
                new[] { new InitialCivilizationPopulationBudget(
                    civilizations.Records[0].Id, 0UL) },
                Array.Empty<InitialEconomicPointPopulation>()));
    }

    [Fact]
    public void CensusRejectsDuplicatedCivilizationPopulationBudget()
    {
        var (world, civilizations, territory) = Fixture();
        var duplicate = new InitialCivilizationPopulationBudget(
            civilizations.Records[0].Id, 0UL);

        Assert.Throws<ArgumentException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                Workforce(civilizations),
                Population(civilizations).Append(duplicate),
                Array.Empty<InitialEconomicPointPopulation>()));
    }

    [Fact]
    public void CensusRejectsUnknownCivilizationPopulationBudget()
    {
        var (world, civilizations, territory) = Fixture();

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                Workforce(civilizations),
                Population(civilizations).Append(
                    new InitialCivilizationPopulationBudget(
                        new CivilizationId(99UL), 0UL)),
                Array.Empty<InitialEconomicPointPopulation>()));
    }

    [Fact]
    public void CensusCannotBypassPreviousWorkforceBudgetValidation()
    {
        var (world, civilizations, territory) = Fixture();

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                new[] { Point(1UL, civilizations.Records[0], 5UL) },
                Workforce(civilizations, first: 4UL),
                Population(civilizations, first: 10UL),
                new[] { Census(1UL, 10UL) }));
    }

    [Fact]
    public void CensusCannotBypassPreviousTerritorialOwnershipValidation()
    {
        var (world, civilizations, territory) = Fixture();
        var foreign = new EconomicPointRuntimeState(
            new EconomicPointId(1UL),
            civilizations.Records[0].Id,
            civilizations.Records[1].StartCellId!.Value,
            1UL);

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                new[] { foreign },
                Workforce(civilizations, first: 1UL),
                Population(civilizations, first: 2UL),
                new[] { Census(1UL, 2UL) }));
    }

    [Fact]
    public void SupportsZeroPopulationAndZeroWorkforce()
    {
        var (world, civilizations, territory) = Fixture();
        var result = RuntimeInitialPopulationReconciler.Reconcile(
            world, civilizations, territory,
            Array.Empty<EconomicPointRuntimeState>(),
            Workforce(civilizations),
            Population(civilizations),
            Array.Empty<InitialEconomicPointPopulation>());

        Assert.Empty(result.PointPopulations);
        Assert.Empty(result.Economy.EconomicPoints);
        Assert.Equal(3, result.CivilizationPopulations.Count);
    }

    [Fact]
    public void LocalResidentsWithoutWorkersAreNotConvertedIntoWorkforce()
    {
        var (world, civilizations, territory) = Fixture();
        var result = RuntimeInitialPopulationReconciler.Reconcile(
            world, civilizations, territory,
            new[] { Point(1UL, civilizations.Records[0], 4UL, 2UL) },
            Workforce(civilizations, first: 4UL),
            Population(civilizations, first: 100UL),
            new[] { Census(1UL, 100UL) });

        Assert.Equal(100UL, result.PointPopulations.Single().Residents);
        Assert.Equal(4UL, result.Economy.EconomicPoints.Single().Workforce);
        Assert.Equal(2UL, result.Economy.EconomicPoints.Single().AllocatedWorkforce);

        // The existing economic resolver still uses its accepted M4.3 demand
        // basis (workforce), not a newly invented population participation rate.
        var flow = new EconomicProductionDemandResolver().Resolve(
            result.Economy,
            new[]
            {
                new EconomicProductionPotential(
                    new EconomicPointId(1UL), new CommodityId(1U),
                    EconomicActivityKind.Agriculture, 1_000_000L)
            },
            new[]
            {
                new EconomicDemandProfile(
                    new EconomicPointId(1UL), new CommodityId(1U),
                    500_000L)
            });

        Assert.Equal(2_000_000UL, flow.SupplyFlows.Single().RawQuantity);
        Assert.Equal(2_000_000UL, flow.DemandFlows.Single().RawQuantity);
    }

    [Fact]
    public void CensusInputOrderingIsCanonicalAndDoesNotChangeWorldStateHash()
    {
        var (world, civilizations, territory) = Fixture();
        var points = new[]
        {
            Point(2UL, civilizations.Records[1], 3UL),
            Point(1UL, civilizations.Records[0], 4UL)
        };
        var workers = Workforce(civilizations, first: 4UL, second: 3UL);
        var population = Population(civilizations, first: 40UL, second: 20UL);
        var census = new[] { Census(2UL, 20UL), Census(1UL, 40UL) };

        InitialPopulationReconciliationResult Build(
            IEnumerable<EconomicPointRuntimeState> p,
            IEnumerable<InitialCivilizationWorkforceBudget> w,
            IEnumerable<InitialCivilizationPopulationBudget> b,
            IEnumerable<InitialEconomicPointPopulation> c) =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory, p, w, b, c);

        var one = Build(points, workers, population, census);
        var two = Build(points.Reverse(), workers.Reverse(),
            population.Reverse(), census.Reverse());

        Assert.Equal(
            one.PointPopulations.Select(row => (row.EconomicPointId, row.Residents)),
            two.PointPopulations.Select(row => (row.EconomicPointId, row.Residents)));
        Assert.Equal(
            one.CivilizationPopulations.Select(row => (row.CivilizationId, row.Residents)),
            two.CivilizationPopulations.Select(row => (row.CivilizationId, row.Residents)));

        WorldState Bound(InitialPopulationReconciliationResult result) =>
            WorldState.CreateBound(world)
                .WithCivilizations(civilizations)
                .WithTerritory(territory)
                .WithEconomy(result.Economy);

        Assert.Equal(
            new CanonicalWorldStateHasher().Compute(Bound(one)),
            new CanonicalWorldStateHasher().Compute(Bound(two)));

        // Reconciliation alone is not a WorldState mutation: attaching the
        // validated census is a separate explicit materialization step.
        var largerCensus = Build(points, workers,
            Population(civilizations, first: 50UL, second: 20UL),
            new[] { Census(1UL, 50UL), Census(2UL, 20UL) });
        Assert.Equal(
            new CanonicalWorldStateHasher().Compute(Bound(one)),
            new CanonicalWorldStateHasher().Compute(Bound(largerCensus)));
    }

    [Fact]
    public void PopulationOverflowFailsRatherThanWrapping()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];

        Assert.Throws<OverflowException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                new[] { Point(1UL, first, 0UL), Point(2UL, first, 0UL) },
                Workforce(civilizations),
                Population(civilizations, first: ulong.MaxValue),
                new[] { Census(1UL, ulong.MaxValue), Census(2UL, 1UL) }));
    }

    [Fact]
    public void InvalidIdentitiesAndNullCensusInputsFailFast()
    {
        Assert.Throws<ArgumentException>(() =>
            new InitialCivilizationPopulationBudget(default, 0UL));
        Assert.Throws<ArgumentException>(() =>
            new InitialEconomicPointPopulation(default, 0UL));

        var (world, civilizations, territory) = Fixture();
        Assert.Throws<ArgumentNullException>(() =>
            RuntimeInitialPopulationReconciler.Reconcile(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                Workforce(civilizations),
                Population(civilizations), null!));
    }
}
