using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldGenerationV2FoundationTests
{
    [Fact]
    public void InitialVersionRemainsOne()
    {
        Assert.Equal(1, WorldGenerationVersion.Initial.Value);
    }

    [Fact]
    public void ProceduralBiomesV2HasDistinctStableVersion()
    {
        Assert.Equal(2, WorldGenerationVersion.ProceduralBiomesV2.Value);
        Assert.NotEqual(WorldGenerationVersion.Initial, WorldGenerationVersion.ProceduralBiomesV2);
        Assert.Equal(new WorldGenerationVersion(2), WorldGenerationVersion.ProceduralBiomesV2);
    }
}