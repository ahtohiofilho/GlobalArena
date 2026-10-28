using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-F: finite physical wood/stone in each strategic cell, ecological
/// recovery for forest/soil, and unchanged worker-driven consumer demand.
/// All resource scales and recovery/wear rates are explicit fixture inputs.
/// </summary>
public sealed class G3FNaturalCapitalTurnTests
{
    private const ulong D = (ulong)StrategicScalarField.Denominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping = new(Food, Wood, Stone);

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static StrategicCellId SuitableCell(WorldGenerationResult world)
    {
        var e = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(world);
        return world.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            e.SoilSuitability.GetRawValue(id) > 0L &&
            e.WoodyVegetation.GetRawValue(id) > 0L &&
            e.ExposedStone.GetRawValue(id) > 0L &&
            world.StrategicClimateFields.WaterAvailability.GetRawValue(id) > 0L);
    }

    private static WorldState State(
        WorldGenerationResult generated, StrategicCellId cell,
        ulong workers = 20UL, bool twoPoints = false, bool idle = false)
    {
        var rows = new List<EconomicPointRuntimeState>();
        var people = new List<PopulationPointRuntimeState>();
        var one = twoPoints ? 10UL : workers;
        var allocations = idle
            ? Array.Empty<EconomicActivityWorkforceAllocation>()
            : twoPoints
                ? new[] { new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Forestry, one) }
                : new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture, 4UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Forestry, 8UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Mining, 8UL)
                };

        rows.Add(new EconomicPointRuntimeState(A, Owner, cell, one, allocations));
        people.Add(new PopulationPointRuntimeState(A, one));
        if (twoPoints)
        {
            rows.Add(new EconomicPointRuntimeState(B, Owner, cell, 10UL, new[]
            {
                new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Forestry, 10UL)
            }));
            people.Add(new PopulationPointRuntimeState(B, 10UL));
        }

        return WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(rows),
                new PopulationRuntimeState(people));
    }

    private static EconomicNaturalCapitalPolicy Policy(
        ulong woodCapacity = D, ulong stoneCapacity = D,
        long woodRecovery = 0L, long soilRecovery = 0L,
        long soilWear = 0L) => new(woodCapacity, stoneCapacity,
            woodRecovery, soilRecovery, soilWear);

    private static EconomicNaturalCapitalTurnResult Run(
        WorldGenerationResult generated, WorldState world,
        EconomicNaturalCapitalBook previous,
        EconomicNaturalCapitalPolicy policy, ulong turn) =>
        new EconomicNaturalCapitalTurnResolver().Resolve(
            generated, world, previous, policy, Mapping,
            new[] { new EconomicDemandProfile(A, Food, 500_000L) },
            turn);

    [Fact]
    public void WoodAndSurfaceStoneCannotBeHarvestedTwiceOrRegenerateWithoutRecovery()
    {
        var generated = Generate();
        var cell = SuitableCell(generated);
        var world = State(generated, cell);
        var initialHash = new CanonicalWorldStateHasher().Compute(world);
        var policy = Policy();
        var book = EconomicNaturalCapitalInitializer.Initialize(generated, policy);
        var initialWood = book.At(cell).RawStandingWood;
        var initialStone = book.At(cell).RawSurfaceStone;
        Assert.NotEqual(0UL, initialWood);
        Assert.NotEqual(0UL, initialStone);

        var first = Run(generated, world, book, policy, 1UL);
        Assert.Equal(initialWood, Assert.Single(first.RealizedFlows.SupplyFlows,
            x => x.CommodityId == Wood).RawQuantity);
        Assert.Equal(initialStone, Assert.Single(first.RealizedFlows.SupplyFlows,
            x => x.CommodityId == Stone).RawQuantity);
        Assert.Equal(0UL, first.NextBook.At(cell).RawStandingWood);
        Assert.Equal(0UL, first.NextBook.At(cell).RawSurfaceStone);

        var second = Run(generated, world, first.NextBook, policy, 2UL);
        Assert.DoesNotContain(second.RealizedFlows.SupplyFlows,
            x => x.CommodityId == Wood || x.CommodityId == Stone);
        Assert.Equal(0UL, second.NextBook.At(cell).RawSurfaceStone);
        Assert.Equal(10UL * D, Assert.Single(second.RealizedFlows.DemandFlows).RawQuantity);
        Assert.Equal(initialHash, new CanonicalWorldStateHasher().Compute(world));
        Assert.Equal(initialWood, book.At(cell).RawStandingWood);
    }

    [Fact]
    public void ForestAndSoilRecoverDuringFallowButStoneNeverDoes()
    {
        var generated = Generate();
        var cell = SuitableCell(generated);
        var active = State(generated, cell);
        var idle = State(generated, cell, idle: true);
        var policy = Policy(woodCapacity: 10UL * D, stoneCapacity: 10UL * D,
            woodRecovery: 500_000L, soilRecovery: 500_000L,
            soilWear: 200_000L);
        var initial = EconomicNaturalCapitalInitializer.Initialize(generated, policy);
        var first = Run(generated, active, initial, policy, 1UL);
        var afterHarvest = first.NextBook.At(cell);

        Assert.True(afterHarvest.RawStandingWood < initial.At(cell).RawStandingWood);
        Assert.True(afterHarvest.RawSurfaceStone < initial.At(cell).RawSurfaceStone);
        Assert.True(afterHarvest.RawSoilVitality < initial.At(cell).RawSoilVitality);

        var fallow = Run(generated, idle, first.NextBook, policy, 2UL);
        Assert.Empty(fallow.RealizedFlows.SupplyFlows);
        Assert.True(fallow.NextBook.At(cell).RawStandingWood > afterHarvest.RawStandingWood);
        Assert.True(fallow.NextBook.At(cell).RawSoilVitality > afterHarvest.RawSoilVitality);
        Assert.Equal(afterHarvest.RawSurfaceStone,
            fallow.NextBook.At(cell).RawSurfaceStone);
        Assert.Equal(10UL * D, Assert.Single(fallow.RealizedFlows.DemandFlows).RawQuantity);
    }

    [Fact]
    public void TwoEconomicPointsCannotDoubleCountOneForestsStandingWood()
    {
        var generated = Generate();
        var cell = SuitableCell(generated);
        var world = State(generated, cell, twoPoints: true);
        var policy = Policy();
        var book = EconomicNaturalCapitalInitializer.Initialize(generated, policy);
        var result = Run(generated, world, book, policy, 1UL);

        var shares = result.RealizedFlows.SupplyFlows
            .Where(x => x.CommodityId == Wood).ToArray();
        Assert.Equal(2, shares.Length);
        Assert.Equal(book.At(cell).RawStandingWood,
            shares.Aggregate(0UL, (sum, x) => sum + x.RawQuantity));
        Assert.InRange(shares[0].RawQuantity,
            shares[1].RawQuantity > 0UL ? shares[1].RawQuantity - 1UL : 0UL,
            shares[1].RawQuantity + 1UL);
        Assert.Equal(0UL, result.NextBook.At(cell).RawStandingWood);
    }

    [Fact]
    public void SoilDamageReducesFollowingHarvestWithoutChangingConsumerDemand()
    {
        var generated = Generate();
        var cell = SuitableCell(generated);
        var active = State(generated, cell);
        var policy = Policy(woodCapacity: 10UL * D, stoneCapacity: 10UL * D,
            soilWear: 900_000L);
        var start = EconomicNaturalCapitalInitializer.Initialize(generated, policy);
        var first = Run(generated, active, start, policy, 1UL);
        var second = Run(generated, active, first.NextBook, policy, 2UL);

        var initialFood = Assert.Single(first.RealizedFlows.SupplyFlows,
            x => x.CommodityId == Food).RawQuantity;
        var laterFood = second.RealizedFlows.SupplyFlows
            .Where(x => x.CommodityId == Food).Sum(x => (decimal)x.RawQuantity);
        Assert.True(laterFood < initialFood);
        Assert.Equal(Assert.Single(first.RealizedFlows.DemandFlows).RawQuantity,
            Assert.Single(second.RealizedFlows.DemandFlows).RawQuantity);
    }

    [Fact]
    public void ReplaysAndUntrustedStocksFailClosed()
    {
        var generated = Generate();
        var cell = SuitableCell(generated);
        var world = State(generated, cell);
        var policy = Policy();
        var book = EconomicNaturalCapitalInitializer.Initialize(generated, policy);
        var result = Run(generated, world, book, policy, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            Run(generated, world, result.NextBook, policy, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            Run(generated, world, result.NextBook, policy, 3UL));

        var entries = book.Cells.Select(x => x.CellId == cell
            ? new EconomicNaturalCapitalCellState(
                cell, x.RawWoodCarryingCapacity, x.RawStandingWood,
                x.RawSurfaceStone + 1UL, x.RawSoilVitality)
            : x);
        var fabricated = new EconomicNaturalCapitalBook(
            0UL, book.DerivationVersion, entries);
        Assert.Throws<InvalidOperationException>(() =>
            Run(generated, world, fabricated, policy, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            Run(Generate(1UL), world, book, policy, 1UL));
    }

    [Fact]
    public void InputsAreImmutableAndCanonicalRegardlessOfCellOrder()
    {
        var generated = Generate();
        var cell = SuitableCell(generated);
        var world = State(generated, cell);
        var policy = Policy();
        var book = EconomicNaturalCapitalInitializer.Initialize(generated, policy);
        var shuffled = new EconomicNaturalCapitalBook(0UL, book.DerivationVersion,
            book.Cells.Reverse());
        var one = Run(generated, world, book, policy, 1UL);
        var other = Run(generated, world, shuffled, policy, 1UL);
        Assert.Equal(one.RealizedFlows.SupplyFlows.Select(x => x.RawQuantity),
            other.RealizedFlows.SupplyFlows.Select(x => x.RawQuantity));
        Assert.Equal(one.NextBook.Cells.Select(x => x.RawStandingWood),
            other.NextBook.Cells.Select(x => x.RawStandingWood));
        Assert.Equal(one.NextBook.Cells.Select(x => x.RawSurfaceStone),
            other.NextBook.Cells.Select(x => x.RawSurfaceStone));
        Assert.Equal(one.NextBook.Cells.Select(x => x.RawSoilVitality),
            other.NextBook.Cells.Select(x => x.RawSoilVitality));
        Assert.Equal(0UL, book.LastAppliedTurn);
        Assert.Equal(1UL, one.NextBook.LastAppliedTurn);
    }

    [Fact]
    public void RatesAndStockBoundsAreStrictAndDoNotCreateResources()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Policy(woodCapacity: 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Policy(soilRecovery: -1L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Policy(woodRecovery: 1_000_001L));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicNaturalCapitalCellState(
                new StrategicCellId(1UL), D, D + 1UL, 0UL, 500_000L));
    }
}
