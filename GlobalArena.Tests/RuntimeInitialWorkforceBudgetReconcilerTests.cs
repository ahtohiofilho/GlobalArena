using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class RuntimeInitialWorkforceBudgetReconcilerTests
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
        IEnumerable<EconomicActivityWorkforceAllocation>? allocations = null) =>
        new(
            new EconomicPointId(id),
            civilization.Id,
            civilization.StartCellId!.Value,
            workforce,
            allocations ?? Array.Empty<EconomicActivityWorkforceAllocation>());

    private static InitialCivilizationWorkforceBudget Budget(
        CivilizationRuntimeRecord civilization,
        ulong workforce) =>
        new(civilization.Id, workforce);

    private static InitialCivilizationWorkforceBudget[] Budgets(
        CivilizationRuntimeState civilizations, ulong first, ulong second = 0UL,
        ulong third = 0UL) =>
        new[]
        {
            Budget(civilizations.Records[0], first),
            Budget(civilizations.Records[1], second),
            Budget(civilizations.Records[2], third)
        };

    [Fact]
    public void AllocatesEachExplicitBudgetAcrossMultipleRealWorldEconomicPoints()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var second = civilizations.Records[1];

        var economy = RuntimeInitialWorkforceBudgetReconciler.Materialize(
            world, civilizations, territory,
            new[]
            {
                Point(3UL, second, 5UL),
                Point(2UL, first, 7UL),
                Point(1UL, first, 5UL)
            },
            Budgets(civilizations, first: 12UL, second: 5UL));

        Assert.Equal(new[] { 1UL, 2UL, 3UL },
            economy.EconomicPoints.Select(point => point.Id.Value));
        Assert.Equal(17UL,
            economy.EconomicPoints.Aggregate(0UL,
                (sum, point) => checked(sum + point.Workforce)));

        var bound = WorldState.CreateBound(world)
            .WithCivilizations(civilizations)
            .WithTerritory(territory)
            .WithEconomy(economy);
        Assert.True(bound.IsWorldBound);
        Assert.Equal(3, bound.Economy.EconomicPoints.Count);
    }

    [Fact]
    public void ZeroBudgetsAndNoEconomicPointsAreValid()
    {
        var (world, civilizations, territory) = Fixture();
        var economy = RuntimeInitialWorkforceBudgetReconciler.Materialize(
            world, civilizations, territory,
            Array.Empty<EconomicPointRuntimeState>(),
            Budgets(civilizations, 0UL));

        Assert.Empty(economy.EconomicPoints);
    }

    [Fact]
    public void RejectsMissingCivilizationBudgetRatherThanInventingWorkers()
    {
        var (world, civilizations, territory) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialWorkforceBudgetReconciler.Materialize(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                new[] { Budget(civilizations.Records[0], 0UL) }));
    }

    [Fact]
    public void RejectsDuplicateBudgets()
    {
        var (world, civilizations, territory) = Fixture();
        var first = Budget(civilizations.Records[0], 0UL);
        Assert.Throws<ArgumentException>(() =>
            RuntimeInitialWorkforceBudgetReconciler.Materialize(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                new[] { first, first }));
    }

    [Fact]
    public void RejectsUnknownCivilizationBudget()
    {
        var (world, civilizations, territory) = Fixture();
        var budgets = Budgets(civilizations, 0UL)
            .Append(new InitialCivilizationWorkforceBudget(
                new CivilizationId(999UL), 0UL));

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialWorkforceBudgetReconciler.Materialize(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(), budgets));
    }

    [Theory]
    [InlineData(4UL)]
    [InlineData(6UL)]
    public void RejectsBothCreationAndLossOfInitialWorkers(ulong expectedWorkers)
    {
        var (world, civilizations, territory) = Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialWorkforceBudgetReconciler.Materialize(
                world, civilizations, territory,
                new[] { Point(1UL, civilizations.Records[0], 5UL) },
                Budgets(civilizations, expectedWorkers)));
    }

    [Fact]
    public void RejectsOverflowWhenSummingLargeWorkerCounts()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        Assert.Throws<OverflowException>(() =>
            RuntimeInitialWorkforceBudgetReconciler.Materialize(
                world, civilizations, territory,
                new[]
                {
                    Point(1UL, first, ulong.MaxValue),
                    Point(2UL, first, 1UL)
                },
                Budgets(civilizations, ulong.MaxValue)));
    }

    [Fact]
    public void CannotBypassExistingOwnershipAndGeographyValidation()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var second = civilizations.Records[1];

        var foreign = new EconomicPointRuntimeState(
            new EconomicPointId(1UL),
            first.Id,
            second.StartCellId!.Value,
            5UL);

        Assert.Throws<InvalidOperationException>(() =>
            RuntimeInitialWorkforceBudgetReconciler.Materialize(
                world, civilizations, territory,
                new[] { foreign }, Budgets(civilizations, 5UL)));
    }

    [Fact]
    public void UnallocatedActivityWorkersStillCountInLocalWorkforce()
    {
        var (world, civilizations, territory) = Fixture();
        var first = civilizations.Records[0];
        var economy = RuntimeInitialWorkforceBudgetReconciler.Materialize(
            world, civilizations, territory,
            new[]
            {
                Point(1UL, first, 5UL,
                    new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture, 3UL)
                    })
            },
            Budgets(civilizations, 5UL));

        Assert.Equal(5UL, economy.EconomicPoints[0].Workforce);
        Assert.Equal(3UL, economy.EconomicPoints[0].AllocatedWorkforce);
        Assert.Equal(2UL, economy.EconomicPoints[0].UnallocatedWorkforce);

        var result = new EconomicProductionDemandResolver().Resolve(
            economy,
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

        Assert.Equal(3_000_000UL, result.SupplyFlows.Single().RawQuantity);
        Assert.Equal(2_500_000UL, result.DemandFlows.Single().RawQuantity);
    }

    [Fact]
    public void ReorderingInputPointsAndBudgetsDoesNotChangeBoundStateHash()
    {
        var (world, civilizations, territory) = Fixture();
        var points = new[]
        {
            Point(2UL, civilizations.Records[1], 3UL),
            Point(1UL, civilizations.Records[0], 8UL)
        };
        var budgets = Budgets(civilizations, first: 8UL, second: 3UL);

        WorldState Build(
            IEnumerable<EconomicPointRuntimeState> suppliedPoints,
            IEnumerable<InitialCivilizationWorkforceBudget> suppliedBudgets) =>
            WorldState.CreateBound(world)
                .WithCivilizations(civilizations)
                .WithTerritory(territory)
                .WithEconomy(RuntimeInitialWorkforceBudgetReconciler.Materialize(
                    world, civilizations, territory,
                    suppliedPoints, suppliedBudgets));

        var first = Build(points, budgets);
        var second = Build(points.Reverse(), budgets.Reverse());
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(first), hasher.Compute(second));
    }

    [Fact]
    public void NullBudgetsAndInvalidCivilizationIdsFailFast()
    {
        Assert.Throws<ArgumentException>(() =>
            new InitialCivilizationWorkforceBudget(default, 5UL));

        var (world, civilizations, territory) = Fixture();
        Assert.Throws<ArgumentNullException>(() =>
            RuntimeInitialWorkforceBudgetReconciler.Materialize(
                world, civilizations, territory,
                Array.Empty<EconomicPointRuntimeState>(),
                null!));
    }
}
