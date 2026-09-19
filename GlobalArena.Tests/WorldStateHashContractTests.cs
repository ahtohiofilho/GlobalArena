using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldStateHashContractTests
{
    [Fact]
    public void ZeroFormatVersionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorldStateHash(
                0U,
                ValidDigest));
    }

    [Fact]
    public void NullDigestIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new WorldStateHash(
                1U,
                null!));
    }

    [Fact]
    public void DigestWithWrongLengthIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new WorldStateHash(
                1U,
                "ABCD"));
    }

    [Fact]
    public void NonHexDigestIsRejected()
    {
        var invalid =
            new string(
                'A',
                WorldStateHash.DigestHexLength - 1)
            + "G";

        Assert.Throws<ArgumentException>(
            () => new WorldStateHash(
                1U,
                invalid));
    }

    [Fact]
    public void LowercaseDigestIsNormalizedToUppercase()
    {
        var hash =
            new WorldStateHash(
                1U,
                ValidDigest.ToLowerInvariant());

        Assert.Equal(
            ValidDigest,
            hash.HexDigest);
    }

    [Fact]
    public void EqualityUsesFormatVersionAndDigest()
    {
        var first =
            new WorldStateHash(
                1U,
                ValidDigest);

        var same =
            new WorldStateHash(
                1U,
                ValidDigest.ToLowerInvariant());

        var differentVersion =
            new WorldStateHash(
                2U,
                ValidDigest);

        var differentDigest =
            new WorldStateHash(
                1U,
                DifferentDigest);

        Assert.Equal(
            first,
            same);

        Assert.NotEqual(
            first,
            differentVersion);

        Assert.NotEqual(
            first,
            differentDigest);
    }

    [Fact]
    public void HasherContractReceivesWorldStateAndReturnsHash()
    {
        var worldState =
            WorldState.CreateInitial();

        var expected =
            new WorldStateHash(
                1U,
                ValidDigest);

        var hasher =
            new TestWorldStateHasher(
                expected);

        var actual =
            hasher.Compute(
                worldState);

        Assert.Same(
            worldState,
            hasher.WorldState);

        Assert.Equal(
            expected,
            actual);
    }

    private const string ValidDigest =
        "0123456789ABCDEF0123456789ABCDEF"
        + "0123456789ABCDEF0123456789ABCDEF";

    private const string DifferentDigest =
        "1123456789ABCDEF0123456789ABCDEF"
        + "0123456789ABCDEF0123456789ABCDEF";

    private sealed class TestWorldStateHasher
        : IWorldStateHasher
    {
        private readonly WorldStateHash _result;

        public TestWorldStateHasher(
            WorldStateHash result)
        {
            _result = result;
        }

        public WorldState? WorldState { get; private set; }

        public WorldStateHash Compute(
            WorldState worldState)
        {
            WorldState = worldState;

            return _result;
        }
    }
}
