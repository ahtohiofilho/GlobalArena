using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class CanonicalWorldStateHasherTests
{
    [Fact]
    public void NullWorldStateIsRejected()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        Assert.Throws<ArgumentNullException>(
            () => hasher.Compute(
                null!));
    }

    [Fact]
    public void InitialStateHasKnownCanonicalHash()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var hash =
            hasher.Compute(
                WorldState.CreateInitial());

        Assert.Equal(
            1U,
            hash.FormatVersion);

        Assert.Equal(
            InitialStateDigest,
            hash.HexDigest);
    }

    [Fact]
    public void EquivalentStatesProduceSameHash()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var first =
            hasher.Compute(
                WorldState.CreateInitial());

        var second =
            hasher.Compute(
                WorldState.CreateInitial());

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void AdvancedStateHasKnownCanonicalHash()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var initial =
            WorldState.CreateInitial();

        var advanced =
            initial.AdvanceRevision();

        var initialHash =
            hasher.Compute(
                initial);

        var advancedHash =
            hasher.Compute(
                advanced);

        Assert.Equal(
            RevisionOneDigest,
            advancedHash.HexDigest);

        Assert.NotEqual(
            initialHash,
            advancedHash);
    }

    [Fact]
    public void RepeatedHashingOfSameStateIsStable()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var worldState =
            WorldState.CreateInitial()
                .AdvanceRevision()
                .AdvanceRevision();

        var first =
            hasher.Compute(
                worldState);

        var second =
            hasher.Compute(
                worldState);

        Assert.Equal(
            first,
            second);
    }

    private const string InitialStateDigest =
        "E52764CDAC5F546D1BD7AF34E0B03141"
        + "E27EAAC1E25C40580350E2C9A72FDC9C";

    private const string RevisionOneDigest =
        "5F99DEE3022BD8F617BA44730B089FE0"
        + "8405E711F8578145938FF732C56E1C11";
}
