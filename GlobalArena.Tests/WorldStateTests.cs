using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldStateTests
{
    [Fact]
    public void CreateInitialReturnsWorldState()
    {
        var state = WorldState.CreateInitial();

        Assert.NotNull(state);
    }

    [Fact]
    public void SeparateInitialStatesAreSeparateInstances()
    {
        var first = WorldState.CreateInitial();
        var second = WorldState.CreateInitial();

        Assert.NotSame(first, second);
    }
}
