using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-D geographic proxies are deterministic, normalized and generated from
/// existing world fields. They do not invent deposits or facilities.
/// </summary>
public sealed class G3DGeographicPrimitiveProductionTests
{
    private static readonly CivilizationId Civilization = new(1UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);
    private static readonly EconomicGeographicPrimitiveWorkerEconomyResolver Engine = new();

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(2, 0)));

    private static StrategicCellId FindLand(
        WorldGenerationResult generated,
        Func<StrategicCellId, bool> predicate) =>
        generated.StrategicTopology.Cells
            .Select(x => x.Id)
            .First(id =>
                generated.StrategicPhysicalFields.LandWater.GetKind(id) ==
                    StrategicLandWaterKind.Land && predicate(id));

    private static WorldState State(
        WorldGenerationResult generated,
        StrategicCellId cell,
        ulong workers,
        IEnumerable<EconomicActivityWorkforceAllocation> assignments) =>
        WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Civilization }))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(new[]
                {
                    new EconomicPointRuntimeState(A, Civilization, cell,
                        workers, assignments)
                }),
                new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(A, workers)
                }));

    [Fact]
    public void SameSeedHasExactlySameThreePotentialsAndDoesNotChangeWorldSignature()
    {
        var first = Generate();
        var before = WorldGenerationCanonicalSignature.Compute(first);
        var one = StrategicPrimitiveResourcePotentialFieldSet.Derive(first);
        var repeated = StrategicPrimitiveResourcePotentialFieldSet.Derive(first);
        var second = StrategicPrimitiveResourcePotentialFieldSet.Derive(Generate());

        Assert.Equal(1U, StrategicPrimitiveResourcePotentialFieldSet.DerivationVersion);
        Assert.Equal(one.Agriculture.RawValues, repeated.Agriculture.RawValues);
        Assert.Equal(one.WoodGathering.RawValues, repeated.WoodGathering.RawValues);
        Assert.Equal(one.SurfaceStone.RawValues, repeated.SurfaceStone.RawValues);
        Assert.Equal(one.Agriculture.RawValues, second.Agriculture.RawValues);
        Assert.Equal(one.WoodGathering.RawValues, second.WoodGathering.RawValues);
        Assert.Equal(one.SurfaceStone.RawValues, second.SurfaceStone.RawValues);
        Assert.Equal(before, WorldGenerationCanonicalSignature.Compute(first));
    }

    [Fact]
    public void DerivedPotentialsAreNormalizedAndWaterAndPolarIceProvideNone()
    {
        var generated = Generate();
        var fields = StrategicPrimitiveResourcePotentialFieldSet.Derive(generated);

        foreach (var cell in generated.StrategicTopology.Cells)
        {
            var id = cell.Id;
            foreach (var raw in new[]
            {
                fields.Agriculture.GetRawValue(id),
                fields.WoodGathering.GetRawValue(id),
                fields.SurfaceStone.GetRawValue(id)
            })
                Assert.InRange(raw, 0L, StrategicScalarField.Denominator);

            if (generated.StrategicPhysicalFields.LandWater.GetKind(id) ==
                    StrategicLandWaterKind.Water ||
                generated.StrategicBiomes.GetKind(id) == StrategicBiomeKind.PolarIce)
            {
                Assert.Equal(0L, fields.Agriculture.GetRawValue(id));
                Assert.Equal(0L, fields.WoodGathering.GetRawValue(id));
                Assert.Equal(0L, fields.SurfaceStone.GetRawValue(id));
            }
        }
    }

    [Fact]
    public void TimberCanOnlyAppearWhereTheGeneratedBiomeIsForested()
    {
        var generated = Generate();
        var fields = StrategicPrimitiveResourcePotentialFieldSet.Derive(generated);
        foreach (var cell in generated.StrategicTopology.Cells)
        {
            var biome = generated.StrategicBiomes.GetKind(cell.Id);
            var timber = fields.WoodGathering.GetRawValue(cell.Id);
            if (biome == StrategicBiomeKind.Forest ||
                biome == StrategicBiomeKind.Rainforest)
                Assert.Equal(generated.StrategicClimateFields.Moisture.GetRawValue(
                    cell.Id), timber);
            else
                Assert.Equal(0L, timber);
        }
    }

    [Fact]
    public void GeographicOutputsEqualAllocatedWorkersTimesLocalRates()
    {
        var generated = Generate();
        var fields = StrategicPrimitiveResourcePotentialFieldSet.Derive(generated);
        var cell = FindLand(generated, id =>
            fields.Agriculture.GetRawValue(id) > 0L &&
            fields.WoodGathering.GetRawValue(id) > 0L &&
            fields.SurfaceStone.GetRawValue(id) > 0L);
        var world = State(generated, cell, 6UL, new[]
        {
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Agriculture, 2UL),
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Forestry, 2UL),
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Mining, 2UL)
        });
        var before = new CanonicalWorldStateHasher().Compute(world);

        var result = Engine.Resolve(generated, world, Mapping, new[]
        {
            new EconomicDemandProfile(A, Food, 500_000L)
        });

        Assert.Equal(3, result.SupplyFlows.Count);
        Assert.Equal(2UL * (ulong)fields.Agriculture.GetRawValue(cell),
            Assert.Single(result.SupplyFlows, x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(2UL * (ulong)fields.WoodGathering.GetRawValue(cell),
            Assert.Single(result.SupplyFlows, x => x.CommodityId == Wood).RawQuantity);
        Assert.Equal(2UL * (ulong)fields.SurfaceStone.GetRawValue(cell),
            Assert.Single(result.SupplyFlows, x => x.CommodityId == Stone).RawQuantity);

        // All six workers consume, irrespective of productive specialization.
        Assert.Equal(3_000_000UL, Assert.Single(result.DemandFlows).RawQuantity);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void NonForestedWorkersCannotProduceTimberButStillCauseDemand()
    {
        var generated = Generate();
        var fields = StrategicPrimitiveResourcePotentialFieldSet.Derive(generated);
        var cell = FindLand(generated, id =>
            fields.WoodGathering.GetRawValue(id) == 0L);
        var world = State(generated, cell, 4UL, new[]
        {
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Forestry, 4UL)
        });

        var result = Engine.Resolve(generated, world, Mapping,
            new[] { new EconomicDemandProfile(A, Food, 500_000L) });
        Assert.Empty(result.SupplyFlows);
        Assert.Equal(2_000_000UL, Assert.Single(result.DemandFlows).RawQuantity);
    }

    [Fact]
    public void WithoutProductionWorkersThereIsStillDemandButNeverSupply()
    {
        var generated = Generate();
        var cell = FindLand(generated, _ => true);
        var world = State(generated, cell, 7UL,
            Array.Empty<EconomicActivityWorkforceAllocation>());

        var result = Engine.Resolve(generated, world, Mapping,
            new[] { new EconomicDemandProfile(A, Food, 500_000L) });
        Assert.Empty(result.SupplyFlows);
        Assert.Equal(3_500_000UL, Assert.Single(result.DemandFlows).RawQuantity);
    }

    [Fact]
    public void CommodityMappingRequiresThreeDistinctValidIdentities()
    {
        Assert.Throws<ArgumentException>(() =>
            new EconomicPrimitiveCommodityMapping(Food, Food, Stone));
        Assert.Throws<ArgumentException>(() =>
            new EconomicPrimitiveCommodityMapping(Food, Wood, Food));
        Assert.Throws<ArgumentException>(() =>
            new EconomicPrimitiveCommodityMapping(default, Wood, Stone));
    }

    [Fact]
    public void DifferentGeneratedWorldCannotReinterpretAnExistingEconomicPoint()
    {
        var generated = Generate();
        var cell = FindLand(generated, _ => true);
        var world = State(generated, cell, 2UL, new[]
        {
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Mining, 2UL)
        });

        Assert.Throws<InvalidOperationException>(() =>
            Engine.Resolve(Generate(1UL), world, Mapping,
                Array.Empty<EconomicDemandProfile>()));
    }
}
