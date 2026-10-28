using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-C: worker-only consumer demand, and three primitive productive
/// activities without facility construction or stock expenditure.
/// Geographic productivity rates remain explicit scenario inputs.
/// </summary>
public sealed class G3CPrimitiveWorkerEconomyTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);

    private static (WorldGenerationResult Generated, WorldState State) OnePoint()
    {
        var generated = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(0UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));
        var land = generated.StrategicTopology.Cells.First(x =>
            generated.StrategicPhysicalFields.LandWater.GetKind(x.Id) ==
                StrategicLandWaterKind.Land);

        var state = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { new CivilizationId(1UL) }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, new CivilizationId(1UL),
                    land.Id, 6UL, new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture, 2UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Forestry, 2UL),
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining, 2UL)
                    })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 6UL)
            }));
        return (generated, state);
    }

    [Fact]
    public void FoodWoodAndStoneAreProducedByAllocatedWorkersWithNoBuiltFacility()
    {
        var (generated, world) = OnePoint();
        var before = new CanonicalWorldStateHasher().Compute(world);

        var resolved = new EconomicPrimitiveWorkerEconomyResolver().Resolve(
            generated, world,
            new[]
            {
                new EconomicProductionPotential(
                    A, Food, EconomicActivityKind.Agriculture, 1_000_000L),
                new EconomicProductionPotential(
                    A, Wood, EconomicActivityKind.Forestry, 1_000_000L),
                new EconomicProductionPotential(
                    A, Stone, EconomicActivityKind.Mining, 1_000_000L)
            },
            new[]
            {
                new EconomicDemandProfile(A, Food, 500_000L),
                new EconomicDemandProfile(A, Wood, 250_000L)
            });

        Assert.Equal(3, resolved.SupplyFlows.Count);
        Assert.Equal(2UL * U, Assert.Single(resolved.SupplyFlows,
            x => x.CommodityId == Food && x.Activity == EconomicActivityKind.Agriculture).RawQuantity);
        Assert.Equal(2UL * U, Assert.Single(resolved.SupplyFlows,
            x => x.CommodityId == Wood && x.Activity == EconomicActivityKind.Forestry).RawQuantity);
        Assert.Equal(2UL * U, Assert.Single(resolved.SupplyFlows,
            x => x.CommodityId == Stone && x.Activity == EconomicActivityKind.Mining).RawQuantity);

        // Consumption is caused by ALL six workers, not only the two farmers.
        Assert.Equal(3UL * U, Assert.Single(resolved.DemandFlows,
            x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(1_500_000UL, Assert.Single(resolved.DemandFlows,
            x => x.CommodityId == Wood).RawQuantity);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void WorkersWithNoProductiveAllocationStillGenerateDemand()
    {
        var (generated, original) = OnePoint();
        var point = original.Economy.EconomicPoints.Single();
        var workforceOnly = original.WithEconomy(new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(A, point.Owner, point.StrategicCellId, 6UL)
        }));
        var flows = new EconomicPrimitiveWorkerEconomyResolver().Resolve(
            generated, workforceOnly,
            Array.Empty<EconomicProductionPotential>(),
            new[] { new EconomicDemandProfile(A, Food, 500_000L) });

        Assert.Empty(flows.SupplyFlows);
        Assert.Equal(3UL * U, Assert.Single(flows.DemandFlows).RawQuantity);
    }

    [Fact]
    public void NoDeclaredNaturalPotentialMeansNoImplicitProduction()
    {
        var (generated, world) = OnePoint();
        var flows = new EconomicPrimitiveWorkerEconomyResolver().Resolve(
            generated, world, Array.Empty<EconomicProductionPotential>(),
            Array.Empty<EconomicDemandProfile>());
        Assert.Empty(flows.SupplyFlows);
        Assert.Empty(flows.DemandFlows);
    }

    [Fact]
    public void ForestProductionCanReachAnotherWorkerMarketWithoutTerminal()
    {
        var (generated, original) = OnePoint();
        var land = original.Economy.EconomicPoints.Single().StrategicCellId;
        var world = original.WithEconomyAndPopulation(
            new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, new CivilizationId(1UL), land, 3UL,
                    new[] { new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture, 3UL) }),
                new EconomicPointRuntimeState(B, new CivilizationId(1UL), land, 2UL,
                    new[] { new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Forestry, 2UL) })
            }),
            new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 3UL),
                new PopulationPointRuntimeState(B, 2UL)
            }));

        var flows = new EconomicPrimitiveWorkerEconomyResolver().Resolve(
            generated, world,
            new[]
            {
                new EconomicProductionPotential(A, Food, EconomicActivityKind.Agriculture,
                    1_000_000L),
                new EconomicProductionPotential(B, Wood, EconomicActivityKind.Forestry,
                    1_000_000L)
            },
            new[]
            {
                new EconomicDemandProfile(A, Wood, 1_000_000L),
                new EconomicDemandProfile(B, Food, 1_000_000L)
            });

        var market = new EconomicMarketAllocationResolver().Resolve(flows,
            new[]
            {
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(A, B), 0, 0UL, 10UL * U, 0UL),
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(B, A), 0, 0UL, 10UL * U, 0UL)
            },
            new EconomicMarketAllocationPolicy(900_000UL, 900_000UL, 1, 1));

        Assert.Equal(4UL * U, market.TotalRawAllocatedQuantity);
        Assert.Contains(market.Allocations, x =>
            x.SourceEconomicPointId == B && x.DestinationEconomicPointId == A &&
            x.CommodityId == Wood && x.SourceActivity == EconomicActivityKind.Forestry &&
            x.RawQuantity == 2UL * U);
    }

    [Fact]
    public void PrimitiveProductionRejectsWrongWorldOrUnallocatedResource()
    {
        var (generated, world) = OnePoint();
        var otherWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(1UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

        Assert.Throws<InvalidOperationException>(() =>
            new EconomicPrimitiveWorkerEconomyResolver().Resolve(
                otherWorld, world, Array.Empty<EconomicProductionPotential>(),
                Array.Empty<EconomicDemandProfile>()));

        var point = world.Economy.EconomicPoints.Single();
        var noForest = world.WithEconomy(new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(A, point.Owner, point.StrategicCellId,
                6UL, new[] { new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 6UL) })
        }));
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicPrimitiveWorkerEconomyResolver().Resolve(
                generated, noForest,
                new[] { new EconomicProductionPotential(
                    A, Wood, EconomicActivityKind.Forestry, 1_000_000L) },
                Array.Empty<EconomicDemandProfile>()));
    }
}
