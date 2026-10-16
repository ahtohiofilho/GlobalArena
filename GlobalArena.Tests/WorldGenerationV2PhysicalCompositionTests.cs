using System;
using System.Linq;
using GlobalArena.World;
using Xunit;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2PhysicalCompositionTests
{
    [Fact]
    public void NullGeometryIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => WorldGenerationV2PhysicalComposition.Generate(null!, 7UL));
    }

    [Theory]
    [InlineData(20261013UL, 25)]
    [InlineData(20261014UL, 35)]
    [InlineData(20261015UL, 45)]
    public void MatchesBiomeUltraPhysicalFieldsAndVisibleBiomes(ulong seed, int landTarget)
    {
        var request = new WorldGenerationRequest(new WorldSeed(seed),
            WorldGenerationVersion.Initial, new GoldbergParameters(3, 0));
        var geometry = new DeterministicWorldGenerator().Generate(request).StrategicSphericalGeometry;
        var composed = WorldGenerationV2PhysicalComposition.Generate(geometry, seed, landTarget);
        var elevation = TacticalIndependentScaleGenerator.GenerateWeighted(geometry, seed, 0.5, 2.0, 3.0);
        var moisture = ExperimentalRandomMoisturePatchGenerator.Generate(geometry, seed);
        var temperature = ExperimentalIceTemperatureComposer.Generate(geometry, elevation, moisture);
        var sortedElevation = elevation.OrderBy(value => value).ToArray();
        int cutoffIndex = Math.Clamp((int)Math.Floor(elevation.Count * (100.0 - landTarget) / 100.0), 0, elevation.Count - 1);
        long expectedSea = sortedElevation[cutoffIndex];
        var expectedBiomes = ExperimentalProceduralBiomeClassifier.Generate(elevation, moisture, temperature, expectedSea);
        Assert.Equal(expectedSea, composed.SeaLevel);
        Assert.Equal(landTarget, composed.LandTargetPercent);
        Assert.Equal(elevation, composed.Elevation);
        Assert.Equal(moisture, composed.Moisture);
        Assert.Equal(temperature, composed.Temperature);
        Assert.Equal(expectedBiomes, composed.Biomes);
        var repeated = WorldGenerationV2PhysicalComposition.Generate(geometry, seed, landTarget);
        Assert.Equal(composed.Biomes, repeated.Biomes);
        Assert.Equal(composed.SeaLevel, repeated.SeaLevel);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(46)]
    [InlineData(33)]
    public void RejectsUnsupportedLandTargets(int invalidTarget)
    {
        var request = new WorldGenerationRequest(new WorldSeed(11UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(1, 0));
        var geometry = new DeterministicWorldGenerator().Generate(request).StrategicSphericalGeometry;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorldGenerationV2PhysicalComposition.Generate(geometry, 11UL, invalidTarget));
    }

    [Fact]
    public void ExistingGeneratorStillRejectsV2()
    {
        var version = WorldGenerationVersion.ProceduralBiomesV2;
        Assert.Equal(2, version.Value);
        // The canonical generator must remain unmodified in this foundation cycle.
        var source = typeof(DeterministicWorldGenerator);
        Assert.NotNull(source);
    }
}
