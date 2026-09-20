using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class IcosahedronSeedVertexIdTests
{
    [Fact]
    public void DefaultSeedVertexIdIsInvalid()
    {
        var id =
            default(IcosahedronSeedVertexId);

        Assert.False(id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => id.Value);
    }

    [Fact]
    public void SeedVertexZeroIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IcosahedronSeedVertexId(
                0));
    }

    [Fact]
    public void SeedVertexThirteenIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IcosahedronSeedVertexId(
                13));
    }

    [Fact]
    public void ValidSeedVertexIdentityPreservesValueAndEquality()
    {
        var first =
            new IcosahedronSeedVertexId(
                7);

        var second =
            new IcosahedronSeedVertexId(
                7);

        Assert.True(first.IsValid);
        Assert.Equal(7, first.Value);
        Assert.Equal(first, second);
    }
}
