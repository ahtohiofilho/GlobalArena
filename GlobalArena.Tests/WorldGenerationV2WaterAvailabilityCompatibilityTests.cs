using System;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2WaterAvailabilityCompatibilityTests
{
    [Theory]
    [InlineData(1, 0, 31UL, 25)]
    [InlineData(2, 0, 42UL, 35)]
    [InlineData(3, 0, 53UL, 45)]
    public void ExistingWaterAvailabilityRulesWorkOnV2WithoutRegeneration(
        int n, int m, ulong seed, int landPercent)
    {
        var request = new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.ProceduralBiomesV2,
            new GoldbergParameters(n, m));
        var composition = WorldGenerationV2EntryPoint.GeneratePhysical(request, landPercent);
        var adapter = WorldGenerationV2SharedPhysicalAdapter.Create(request, composition);
        var availability = StrategicWaterAvailabilityFieldGenerator.Generate(
            adapter.Moisture, adapter.Elevation, adapter.LandWater);
        Assert.Same(adapter.SurfaceGraph, availability.SurfaceGraph);
        for (var i = 0; i < adapter.SurfaceGraph.NodeCount; i++)
        {
            var expected = adapter.LandWater.GetKind(i) == StrategicLandWaterKind.Water
                ? StrategicScalarField.Denominator : adapter.Moisture.GetRawValue(i);
            Assert.Equal(expected, availability.GetRawValue(i));
        }
        var hydrology = StrategicHydrologyFieldGenerator.Generate(
            adapter.Elevation, adapter.LandWater, availability);
        Assert.NotNull(hydrology);
    }
}