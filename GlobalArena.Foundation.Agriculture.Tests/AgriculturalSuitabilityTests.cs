using GlobalArena.Foundation.Agriculture;

namespace GlobalArena.Foundation.Agriculture.Tests;

public sealed class AgriculturalSuitabilityTests
{
    [Fact]
    public void CatalogHasExactlySixDistinctStableIdentities()
    {
        Assert.Equal(6, CropCatalog.Count);
        Assert.Equal(new[] { "milho", "sorgo", "trigo", "arroz", "soja", "cevada" },
            CropCatalog.All.Select(x => x.StableKey).ToArray());
        Assert.Equal(CropCatalog.Count, CropCatalog.All.Select(x => x.Id).Distinct().Count());
        Assert.Equal(CropCatalog.Count, CropCatalog.All.Select(x => x.StableKey).Distinct().Count());
        foreach (var item in CropCatalog.All)
            Assert.Equal(item, CropCatalog.Get(item.Id));
        Assert.Throws<ArgumentOutOfRangeException>(() => CropCatalog.Get((CropId)99));
    }

    [Theory]
    [InlineData(-1_000_000, 0, 0)]
    [InlineData(1_000_000, 1_000_000, 1_000_000)]
    [InlineData(0, 500_000, 250_000)]
    public void AllCropsRemainCultivableOnAnyOrdinaryLandNode(int temperature, int moisture, int slope)
    {
        var node = new AgriculturalNode(temperature, moisture, slope, true);
        Span<int> result = stackalloc int[CropCatalog.Count];
        AgriculturalSuitability.EvaluateAll(node, result);
        Assert.All(result.ToArray(), score => Assert.InRange(score, AgriculturalSuitability.LandFloor, 1_000_000));
        for (var index = 0; index < CropCatalog.Count; index++)
            Assert.Equal(result[index], AgriculturalSuitability.Evaluate(CropCatalog.All[index].Id, node));
    }

    [Fact]
    public void WaterDoesNotYieldLandCrops()
    {
        Span<int> values = stackalloc int[CropCatalog.Count];
        AgriculturalSuitability.EvaluateAll(new AgriculturalNode(0, 500_000, 0, false), values);
        Assert.All(values.ToArray(), score => Assert.Equal(0, score));
    }

    [Theory]
    [InlineData(680_000, 150_000, CropId.Sorghum)]
    [InlineData(650_000, 880_000, CropId.Rice)]
    [InlineData(350_000, 530_000, CropId.Maize)]
    [InlineData(-200_000, 470_000, CropId.Wheat)]
    [InlineData(90_000, 740_000, CropId.Soybean)]
    [InlineData(-650_000, 250_000, CropId.Barley)]
    public void EveryCropHasARegionWhereItIsTheBest(int temperature, int moisture, CropId expectedWinner)
    {
        var node = new AgriculturalNode(temperature, moisture, 0, true);
        var scores = CropCatalog.All.Select(x => AgriculturalSuitability.Evaluate(x.Id, node)).ToArray();
        var bestIndex = Array.IndexOf(scores, scores.Max());
        Assert.Equal(expectedWinner, CropCatalog.All[bestIndex].Id);
        Assert.Equal(1, scores.Count(score => score == scores.Max())); // no ties for winner
    }

    [Fact]
    public void NeighboringPhysicalValuesDoNotCreateBiomeCliffs()
    {
        var baseline = new AgriculturalNode(100_000, 500_000, 123_456, true);
        foreach (var crop in CropCatalog.All)
        {
            var a = AgriculturalSuitability.Evaluate(crop.Id, baseline);
            var b = AgriculturalSuitability.Evaluate(crop.Id, baseline with { TemperatureRaw = baseline.TemperatureRaw + 1 });
            var c = AgriculturalSuitability.Evaluate(crop.Id, baseline with { MoistureRaw = baseline.MoistureRaw + 1 });
            var d = AgriculturalSuitability.Evaluate(crop.Id, baseline with { SlopeRaw = baseline.SlopeRaw + 1 });
            Assert.InRange(Math.Abs(a - b), 0, 2);
            Assert.InRange(Math.Abs(a - c), 0, 2);
            Assert.InRange(Math.Abs(a - d), 0, 2);
        }
    }

    [Fact]
    public void SlopeRespondsToLocalReliefWithoutAnAbsoluteAltitudePenalty()
    {
        var slope1 = AgriculturalLogicalSlope.Estimate(10_000, new[] { 20_000, -10_000, 10_000 });
        var slope2 = AgriculturalLogicalSlope.Estimate(510_000, new[] { 520_000, 490_000, 510_000 });
        var slopeReordered = AgriculturalLogicalSlope.Estimate(10_000, new[] { 10_000, 20_000, -10_000 });
        Assert.Equal(10_000, slope1);
        Assert.Equal(slope1, slope2);
        Assert.Equal(slope1, slopeReordered);

        var flat = AgriculturalSuitability.Evaluate(CropId.Rice,
            new AgriculturalNode(650_000, 880_000, 0, true));
        var steep = AgriculturalSuitability.Evaluate(CropId.Rice,
            new AgriculturalNode(650_000, 880_000, 900_000, true));
        Assert.True(flat > steep);
    }

    [Fact]
    public void NoEvaluationDependsOnQueryOrderOrGlobalState()
    {
        var node = new AgriculturalNode(-125_000, 635_000, 210_000, true);
        Span<int> before = stackalloc int[CropCatalog.Count];
        Span<int> after = stackalloc int[CropCatalog.Count];
        AgriculturalSuitability.EvaluateAll(node, before);
        for (var i = CropCatalog.Count - 1; i >= 0; i--)
            _ = AgriculturalSuitability.Evaluate(CropCatalog.All[i].Id, node);
        AgriculturalSuitability.EvaluateAll(node, after);
        Assert.Equal(before.ToArray(), after.ToArray());
    }

    [Fact]
    public void RejectsInvalidInputsAndDestinationSizes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgriculturalSuitability.Evaluate(CropId.Maize, new AgriculturalNode(-1_000_001, 0, 0, true)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgriculturalSuitability.Evaluate(CropId.Maize, new AgriculturalNode(0, -1, 0, true)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgriculturalSuitability.Evaluate(CropId.Maize, new AgriculturalNode(0, 0, 1_000_001, true)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgriculturalSuitability.Evaluate((CropId)255, new AgriculturalNode(0, 0, 0, true)));
        Assert.Throws<ArgumentException>(() =>
            AgriculturalSuitability.EvaluateAll(new AgriculturalNode(0, 0, 0, true), new int[5]));
        Assert.Throws<ArgumentException>(() => AgriculturalLogicalSlope.Estimate(0, Array.Empty<int>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AgriculturalLogicalSlope.Estimate(0, new[] { 1_000_001 }));
    }

    [Fact]
    public void FixedPointChecksumIsStableForCrossPlatformValidation()
    {
        // Independent of locale, time, CPU floating-point, threading, and hashing.
        long sum = 0;
        Span<int> scores = stackalloc int[CropCatalog.Count];
        for (var i = 0; i < 1_024; i++)
        {
            var temp = (int)((i * 37_001L) % 2_000_001L) - 1_000_000;
            var moisture = (int)((i * 97_001L) % 1_000_001L);
            var slope = (int)((i * 131_071L) % 1_000_001L);
            AgriculturalSuitability.EvaluateAll(new AgriculturalNode(temp, moisture, slope, i % 13 != 0), scores);
            for (var j = 0; j < scores.Length; j++)
                sum = checked(sum + scores[j] * (j + 1L));
        }
        // Golden result frozen after independent integer reference computation.
        Assert.Equal(12_018_979_627L, sum);
    }
}
