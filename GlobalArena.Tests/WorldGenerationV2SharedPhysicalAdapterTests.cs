using System;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2SharedPhysicalAdapterTests
{
    [Theory]
    [InlineData(1, 0, 31UL, 25)]
    [InlineData(2, 0, 42UL, 35)]
    [InlineData(3, 0, 53UL, 45)]
    public void AllPhysicalFieldsShareOneGraphAndPreserveRawValues(
        int n, int m, ulong seed, int landPercent)
    {
        var request = new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.ProceduralBiomesV2,
            new GoldbergParameters(n, m));
        var source = WorldGenerationV2EntryPoint.GeneratePhysical(request, landPercent);
        var result = WorldGenerationV2SharedPhysicalAdapter.Create(request, source);

        Assert.Same(result.SurfaceGraph, result.Elevation.SurfaceGraph);
        Assert.Same(result.SurfaceGraph, result.Moisture.SurfaceGraph);
        Assert.Same(result.SurfaceGraph, result.Temperature.SurfaceGraph);
        Assert.Same(result.Elevation, result.LandWater.Elevation);
        Assert.Equal(source.SeaLevel, result.SeaLevel.RawValue);
        Assert.Equal(source.Geometry.Cells.Count, result.SurfaceGraph.NodeCount);
        for (var i = 0; i < result.SurfaceGraph.NodeCount; i++)
        {
            Assert.Equal(source.Geometry.Cells[i].CellId, result.SurfaceGraph.GetCellId(i));
            Assert.Equal(source.Elevation[i], result.Elevation.GetRawValue(i));
            Assert.Equal(source.Moisture[i], result.Moisture.GetRawValue(i));
            Assert.Equal(source.Temperature[i], result.Temperature.GetRawValue(i));
            var expectedLand = source.Elevation[i] > source.SeaLevel;
            Assert.Equal(expectedLand ? StrategicLandWaterKind.Land : StrategicLandWaterKind.Water,
                result.LandWater.GetKind(i));
        }
    }

    [Fact]
    public void RejectsInitialRequestWithoutModifyingLegacyGenerator()
    {
        var v2Request = new WorldGenerationRequest(new WorldSeed(7UL),
            WorldGenerationVersion.ProceduralBiomesV2, new GoldbergParameters(1, 0));
        var source = WorldGenerationV2EntryPoint.GeneratePhysical(v2Request);
        var v1Request = new WorldGenerationRequest(new WorldSeed(7UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(1, 0));
        Assert.Throws<NotSupportedException>(() =>
            WorldGenerationV2SharedPhysicalAdapter.Create(v1Request, source));
    }
}