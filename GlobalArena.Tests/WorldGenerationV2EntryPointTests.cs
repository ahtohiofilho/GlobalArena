using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2EntryPointTests
{
    [Fact]
    public void RejectsNullRequest()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WorldGenerationV2EntryPoint.GeneratePhysical(null!));
    }

    [Fact]
    public void RejectsV1RequestWithoutChangingCanonicalGenerator()
    {
        var request = new WorldGenerationRequest(new WorldSeed(42UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(1, 0));
        Assert.Throws<NotSupportedException>(() =>
            WorldGenerationV2EntryPoint.GeneratePhysical(request));
    }

    [Fact]
    public void V2RequestProducesSameFieldsForSameSeed()
    {
        var request = new WorldGenerationRequest(new WorldSeed(42UL),
            WorldGenerationVersion.ProceduralBiomesV2, new GoldbergParameters(1, 0));
        var first = WorldGenerationV2EntryPoint.GeneratePhysical(request);
        var second = WorldGenerationV2EntryPoint.GeneratePhysical(request);
        Assert.Equal(first.Elevation, second.Elevation);
        Assert.Equal(first.Moisture, second.Moisture);
        Assert.Equal(first.Temperature, second.Temperature);
        Assert.Equal(first.SeaLevel, second.SeaLevel);
        Assert.Equal(first.Biomes, second.Biomes);
    }
}