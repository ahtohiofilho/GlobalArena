using System;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2AgriculturalStrategicResultTests
{
    [Fact]
    public void NullPhysicalResultIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WorldGenerationV2AgriculturalStrategicResult.Create(null!));
    }

    [Theory]
    [InlineData(1, 0, 17UL, 25)]
    [InlineData(2, 0, 29UL, 35)]
    [InlineData(3, 0, 41UL, 45)]
    public void AgricultureAndHydrologyShareGraphAndRemainDeterministic(
        int n, int m, ulong seed, int landPercent)
    {
        var request = new WorldGenerationRequest(new WorldSeed(seed),
            WorldGenerationVersion.ProceduralBiomesV2, new GoldbergParameters(n, m));
        var input = WorldGenerationV2PhysicalResult.Generate(request, landPercent);
        var first = WorldGenerationV2AgriculturalStrategicResult.Create(input);
        var second = WorldGenerationV2AgriculturalStrategicResult.Create(input);
        Assert.Same(first.Physical.SurfaceGraph, first.AgriculturalProductivity.SurfaceGraph);
        Assert.Same(first.Physical.SurfaceGraph, first.HydrologyResult.WaterAvailability.SurfaceGraph);
        Assert.Equal(first.AgriculturalProductivity.RawValues, second.AgriculturalProductivity.RawValues);
        Assert.Equal(first.HydrologyResult.WaterAvailability.RawValues,
            second.HydrologyResult.WaterAvailability.RawValues);
        for (var i = 0; i < first.AgriculturalProductivity.Count; i++)
            if (first.Physical.LandWater.GetKind(i) == StrategicLandWaterKind.Water)
                Assert.Equal(0, first.AgriculturalProductivity.GetRawValue(i));
    }
}