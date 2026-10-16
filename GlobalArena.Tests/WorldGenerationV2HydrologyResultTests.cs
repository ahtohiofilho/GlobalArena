using System;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2HydrologyResultTests
{
    [Theory]
    [InlineData(1, 0, 701UL, 25)]
    [InlineData(2, 0, 702UL, 35)]
    [InlineData(3, 0, 703UL, 45)]
    public void UsesOneGraphAndExistingHydrologyDeterministically(
        int n, int m, ulong seed, int landPercent)
    {
        var request = new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.ProceduralBiomesV2,
            new GoldbergParameters(n, m));
        var physical = WorldGenerationV2PhysicalResult.Generate(request, landPercent);
        var first = WorldGenerationV2HydrologyResult.Create(physical);
        var second = WorldGenerationV2HydrologyResult.Create(physical);
        Assert.Same(first.Physical.SurfaceGraph, first.Physical.Elevation.SurfaceGraph);
        Assert.Same(first.Physical.SurfaceGraph, first.Physical.Moisture.SurfaceGraph);
        Assert.Same(first.Physical.SurfaceGraph, first.Physical.Temperature.SurfaceGraph);
        Assert.Same(first.Physical.SurfaceGraph, first.WaterAvailability.SurfaceGraph);
        Assert.Same(first.Physical.SurfaceGraph, first.Hydrology.SurfaceGraph);
        Assert.Same(first.Physical.Elevation, first.Hydrology.Elevation);
        Assert.Same(first.Physical.LandWater, first.Hydrology.LandWater);
        Assert.Same(first.WaterAvailability, first.Hydrology.WaterAvailability);
        for (var i = 0; i < first.Physical.SurfaceGraph.NodeCount; i++)
        {
            var water = first.Physical.LandWater.GetKind(i) == StrategicLandWaterKind.Water;
            var expected = water ? StrategicScalarField.Denominator
                : first.Physical.Moisture.GetRawValue(i);
            Assert.Equal(expected, first.WaterAvailability.GetRawValue(i));
            Assert.Equal(first.WaterAvailability.GetRawValue(i), second.WaterAvailability.GetRawValue(i));
            Assert.Equal(first.Hydrology.GetNodeKind(i), second.Hydrology.GetNodeKind(i));
            Assert.Equal(first.Hydrology.GetDownstreamNodeIndex(i), second.Hydrology.GetDownstreamNodeIndex(i));
            Assert.Equal(first.Hydrology.FlowAccumulation.GetRawValue(i),
                second.Hydrology.FlowAccumulation.GetRawValue(i));
            if (water)
                Assert.Equal(StrategicHydrologyNodeKind.WaterOutlet, first.Hydrology.GetNodeKind(i));
        }
    }

    [Fact]
    public void RejectsNullResult()
    {
        Assert.Throws<ArgumentNullException>(() => WorldGenerationV2HydrologyResult.Create(null!));
    }
}