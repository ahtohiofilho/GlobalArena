using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-E v2: seeded but repeatable land endowments and opt-in worker economy.
/// No built facilities, persistent deposits or replacement of G3-D v1.
/// </summary>
public sealed class G3ENaturalEndowmentWorkerEconomyTests
{
    private const long D = StrategicScalarField.Denominator;
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static StrategicCellId FirstLand(
        WorldGenerationResult generated,
        Func<StrategicCellId, bool>? predicate = null) =>
        generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            generated.StrategicPhysicalFields.LandWater.GetKind(id) ==
                StrategicLandWaterKind.Land &&
            (predicate is null || predicate(id)));

    private static WorldState State(
        WorldGenerationResult generated, StrategicCellId cell,
        ulong workers,
        params EconomicActivityWorkforceAllocation[] assignments) =>
        WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, Owner, cell, workers, assignments)
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, workers)
            }));

    private static EconomicNaturalEndowmentWorkerEconomyResolver Resolver() =>
        new(StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion);

    [Fact]
    public void SameSeedAndVersionGiveIdenticalSpatialFieldsWithoutMutatingSignature()
    {
        var first = Generate();
        var signature = WorldGenerationCanonicalSignature.Compute(first);
        var one = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(first);
        var twice = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(first);
        var fresh = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(Generate());

        Assert.Equal(2U, StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion);
        Assert.Equal(one.SoilSuitability.RawValues, twice.SoilSuitability.RawValues);
        Assert.Equal(one.WoodyVegetation.RawValues, fresh.WoodyVegetation.RawValues);
        Assert.Equal(one.ExposedStone.RawValues, fresh.ExposedStone.RawValues);
        Assert.Equal(one.LithologyProxy.RawValues, fresh.LithologyProxy.RawValues);
        Assert.Equal(signature, WorldGenerationCanonicalSignature.Compute(first));
    }

    [Fact]
    public void DifferentSeedsChangeTheIndependentRockPattern()
    {
        var first = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(Generate(0UL));
        var other = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(Generate(1UL));
        Assert.Contains(
            first.LithologyProxy.RawValues.Zip(other.LithologyProxy.RawValues,
                (x, y) => x != y),
            changed => changed);
    }

    [Fact]
    public void OceanAndPolarIceHaveNoPrimitiveEndowmentsAndValuesAreNormalized()
    {
        var generated = Generate();
        var e = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        foreach (var cell in generated.StrategicTopology.Cells)
        {
            var id = cell.Id;
            var quantities = new[]
            {
                e.SoilSuitability.GetRawValue(id),
                e.WoodyVegetation.GetRawValue(id),
                e.ExposedStone.GetRawValue(id),
                e.LithologyProxy.GetRawValue(id)
            };
            foreach (var raw in quantities)
                Assert.InRange(raw, 0L, D);

            if (generated.StrategicPhysicalFields.LandWater.GetKind(id) ==
                    StrategicLandWaterKind.Water ||
                generated.StrategicBiomes.GetKind(id) == StrategicBiomeKind.PolarIce)
                Assert.All(quantities, raw => Assert.Equal(0L, raw));
        }
    }

    [Fact]
    public void RockAndVegetationAreSpatialMeansOverExistingLandNeighbors()
    {
        var generated = Generate();
        var e = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var graph = generated.StrategicSurfaceGraph;
        var land = FirstLand(generated, id =>
            generated.StrategicBiomes.GetKind(id) != StrategicBiomeKind.PolarIce);
        var i = graph.GetNodeIndex(land);

        // Smoothed stone cannot exceed the normalized scale. Soil and
        // exposed stone share the same smoothed lithology input, but neither
        // is a free construction material stock.
        var rock = e.LithologyProxy.GetRawValue(i);
        var rawRelief = generated.StrategicPhysicalFields.Relief.GetRawValue(i);
        var relief = rawRelief / 2L;

        var expectedSoil = (long)(((Int128)(D - relief) * (D - rock)) / D);
        var expectedStone = (long)(((Int128)rock * (D + relief)) / (2L * D));
        Assert.Equal(expectedSoil, e.SoilSuitability.GetRawValue(i));
        Assert.Equal(expectedStone, e.ExposedStone.GetRawValue(i));

        // The one-ring averaging rule keeps woody vegetation between the
        // local ecological response and its land neighbors' responses.
        var values = graph.GetNeighborIndexes(i).Append(i)
            .Where(index =>
                generated.StrategicPhysicalFields.LandWater.GetKind(index) ==
                    StrategicLandWaterKind.Land &&
                generated.StrategicBiomes.GetKind(index) != StrategicBiomeKind.PolarIce)
            .Select(index =>
            {
                var m = generated.StrategicClimateFields.Moisture.GetRawValue(index);
                var t = generated.StrategicClimateFields.Temperature.GetRawValue(index);
                return (long)(((Int128)m * (D - Math.Abs(t))) / D);
            }).ToArray();

        Assert.InRange(e.WoodyVegetation.GetRawValue(i), values.Min(), values.Max());
    }

    [Fact]
    public void AllThreePrimitiveActivitiesUseIndependentNaturalRatesAndAllWorkersConsume()
    {
        var generated = Generate();
        var endowments = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = FirstLand(generated, id =>
            endowments.SoilSuitability.GetRawValue(id) > 0L &&
            endowments.WoodyVegetation.GetRawValue(id) > 0L &&
            endowments.ExposedStone.GetRawValue(id) > 0L);
        var world = State(generated, cell, 9UL,
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Agriculture, 2UL),
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Forestry, 3UL),
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Mining, 2UL),
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.TradeLogistics, 2UL));
        var before = new CanonicalWorldStateHasher().Compute(world);
        var result = Resolver().Resolve(generated!, world, Mapping,
            new[] { new EconomicDemandProfile(A, Food, 500_000L) });

        var water = generated.StrategicClimateFields.WaterAvailability.GetRawValue(cell);
        var temp = generated.StrategicClimateFields.Temperature.GetRawValue(cell);
        var farmRate = (long)(((Int128)endowments.SoilSuitability.GetRawValue(cell)
            * water * (D - Math.Abs(temp))) / ((Int128)D * D));

        Assert.Equal(3, result.SupplyFlows.Count);
        Assert.Equal(2UL * (ulong)farmRate,
            Assert.Single(result.SupplyFlows, x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(3UL * (ulong)endowments.WoodyVegetation.GetRawValue(cell),
            Assert.Single(result.SupplyFlows, x => x.CommodityId == Wood).RawQuantity);
        Assert.Equal(2UL * (ulong)endowments.ExposedStone.GetRawValue(cell),
            Assert.Single(result.SupplyFlows, x => x.CommodityId == Stone).RawQuantity);
        Assert.Equal(4_500_000UL, Assert.Single(result.DemandFlows).RawQuantity);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void WorkersWithNoAllocationsConsumeButGenerateNoGoods()
    {
        var generated = Generate();
        var cell = FirstLand(generated);
        var world = State(generated, cell, 7UL);
        var result = Resolver().Resolve(generated, world, Mapping,
            new[] { new EconomicDemandProfile(A, Food, 500_000L) });
        Assert.Empty(result.SupplyFlows);
        Assert.Equal(3_500_000UL, Assert.Single(result.DemandFlows).RawQuantity);
    }

    [Fact]
    public void PolarLandHasNoPrimitiveOutputButWorkersStillCauseDemand()
    {
        // Exact zero rainfall is not generally sampled by the generator.
        // Use an actually generated polar-land cell, whose endowment is
        // deliberately zero, instead of assuming a zero-value random draw.
        WorldGenerationResult? generated = null;
        StrategicCellId cell = default;
        for (ulong seed = 0UL; seed < 8UL && !cell.IsValid; seed++)
        {
            var candidate = Generate(seed);
            var polar = candidate.StrategicTopology.Cells.FirstOrDefault(x =>
                candidate.StrategicPhysicalFields.LandWater.GetKind(x.Id) ==
                    StrategicLandWaterKind.Land &&
                candidate.StrategicBiomes.GetKind(x.Id) == StrategicBiomeKind.PolarIce);
            if (polar is null)
                continue;
            generated = candidate;
            cell = polar.Id;
        }
        Assert.NotNull(generated);
        var world = State(generated!, cell, 4UL,
            new EconomicActivityWorkforceAllocation(EconomicActivityKind.Agriculture, 4UL));
        var result = Resolver().Resolve(generated, world, Mapping,
            new[] { new EconomicDemandProfile(A, Food, 500_000L) });
        Assert.Empty(result.SupplyFlows);
        Assert.Equal(2_000_000UL, Assert.Single(result.DemandFlows).RawQuantity);
    }

    [Fact]
    public void VersionMismatchAndDifferentWorldFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicNaturalEndowmentWorkerEconomyResolver(1U));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicNaturalEndowmentWorkerEconomyResolver(3U));

        var generated = Generate();
        var cell = FirstLand(generated);
        var world = State(generated, cell, 2UL);
        Assert.Throws<InvalidOperationException>(() =>
            Resolver().Resolve(Generate(2UL), world, Mapping,
                Array.Empty<EconomicDemandProfile>()));
    }
}
