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

    [Fact]
    public void InitialRevisionIsZero()
    {
        var state = WorldState.CreateInitial();

        Assert.Equal(
            0UL,
            state.Revision);
    }

    [Fact]
    public void AdvanceRevisionReturnsNewStateWithIncrementedRevision()
    {
        var state = WorldState.CreateInitial();

        var advanced = state.AdvanceRevision();

        Assert.NotSame(
            state,
            advanced);

        Assert.Equal(
            0UL,
            state.Revision);

        Assert.Equal(
            1UL,
            advanced.Revision);
    }
}
