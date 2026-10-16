using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2PhysicalResultTests
{
    [Fact]
    public void NullRequestIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WorldGenerationV2PhysicalResult.Generate(null!));
    }

    [Fact]
    public void V1RequestIsRejectedWithoutCanonicalFallback()
    {
        var request = new WorldGenerationRequest(new WorldSeed(42UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(1, 0));
        Assert.Throws<NotSupportedException>(() =>
            WorldGenerationV2PhysicalResult.Generate(request));
    }

    [Fact]
    public void VersionedPhysicalResultPreservesRequestAndFields()
    {
        var request = new WorldGenerationRequest(new WorldSeed(42UL),
            WorldGenerationVersion.ProceduralBiomesV2, new GoldbergParameters(1, 0));
        var result = WorldGenerationV2PhysicalResult.Generate(request, 35);
        var independent = WorldGenerationV2EntryPoint.GeneratePhysical(request, 35);
        Assert.Same(request, result.Request);
        Assert.Equal(35, result.LandTargetPercent);
        Assert.Equal(independent.Elevation, result.PhysicalFields.Elevation);
        Assert.Equal(independent.Moisture, result.PhysicalFields.Moisture);
        Assert.Equal(independent.Temperature, result.PhysicalFields.Temperature);
        Assert.Equal(independent.SeaLevel, result.PhysicalFields.SeaLevel);
        Assert.Equal(independent.Biomes, result.PhysicalFields.Biomes);
    }
}