using System;
using System.Linq;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2AgriculturalProductivityTests
{
    [Theory]
    [InlineData(0L, 50_000L)]
    [InlineData(650_000L, 1_000_000L)]
    [InlineData(1_000_000L, 650_000L)]
    [InlineData(625_000L, 975_000L)]
    public void ApprovedMoistureCurve(long moisture, long expected) =>
        Assert.Equal(expected, WorldGenerationV2AgriculturalProductivity.MoistureYield(moisture));

    [Fact]
    public void InvalidMoistureIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldGenerationV2AgriculturalProductivity.MoistureYield(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldGenerationV2AgriculturalProductivity.MoistureYield(1_000_001));
    }

    [Theory]
    [InlineData(1, 0, 31UL, 25)]
    [InlineData(2, 0, 42UL, 35)]
    [InlineData(3, 0, 53UL, 45)]
    public void DeterministicAndGraphBound(int n, int m, ulong seed, int landPercent)
    {
        var request = new WorldGenerationRequest(new WorldSeed(seed),
            WorldGenerationVersion.ProceduralBiomesV2, new GoldbergParameters(n, m));
        var fields = WorldGenerationV2EntryPoint.GeneratePhysical(request, landPercent);
        var physical = WorldGenerationV2SharedPhysicalAdapter.Create(request, fields);
        var first = WorldGenerationV2AgriculturalProductivity.Generate(physical);
        var second = WorldGenerationV2AgriculturalProductivity.Generate(physical);
        Assert.Same(physical.SurfaceGraph, first.SurfaceGraph);
        Assert.Equal(first.RawValues, second.RawValues);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.InRange(first.GetRawValue(i), 0, 1_000_000);
            if (physical.LandWater.GetKind(i) == StrategicLandWaterKind.Water)
                Assert.Equal(0, first.GetRawValue(i));
        }
    }
}