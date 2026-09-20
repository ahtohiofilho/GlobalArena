using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GoldbergScaledRefinementTests
{
    [Fact]
    public void InvalidCoarseParametersAreRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new GoldbergScaledRefinement(
                default,
                new GoldbergParameters(
                    2,
                    0)));
    }

    [Fact]
    public void InvalidFineParametersAreRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    0),
                default));
    }

    [Fact]
    public void EqualResolutionIsRejected()
    {
        var parameters =
            new GoldbergParameters(
                1,
                1);

        Assert.Throws<NotSupportedException>(
            () => new GoldbergScaledRefinement(
                parameters,
                parameters));
    }

    [Fact]
    public void ReversedRefinementDirectionIsRejected()
    {
        Assert.Throws<NotSupportedException>(
            () => new GoldbergScaledRefinement(
                new GoldbergParameters(
                    2,
                    0),
                new GoldbergParameters(
                    1,
                    0)));
    }

    [Fact]
    public void NonCollinearPairIsRejected()
    {
        Assert.Throws<NotSupportedException>(
            () => new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    1),
                new GoldbergParameters(
                    2,
                    3)));
    }

    [Fact]
    public void ClassIAxisSwapIsRejected()
    {
        Assert.Throws<NotSupportedException>(
            () => new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    0),
                new GoldbergParameters(
                    0,
                    2)));
    }

    [Fact]
    public void ClassIIIChiralitySwapIsRejected()
    {
        Assert.Throws<NotSupportedException>(
            () => new GoldbergScaledRefinement(
                new GoldbergParameters(
                    2,
                    1),
                new GoldbergParameters(
                    2,
                    4)));
    }

    [Theory]
    [InlineData(1, 0, 2, 0, 2)]
    [InlineData(0, 2, 0, 6, 3)]
    [InlineData(1, 1, 2, 2, 2)]
    [InlineData(2, 1, 4, 2, 2)]
    [InlineData(1, 2, 3, 6, 3)]
    public void SupportedScaledPairsExposeExpectedContract(
        int coarseM,
        int coarseN,
        int fineM,
        int fineN,
        int expectedScale)
    {
        var coarse =
            new GoldbergParameters(
                coarseM,
                coarseN);

        var fine =
            new GoldbergParameters(
                fineM,
                fineN);

        var refinement =
            new GoldbergScaledRefinement(
                coarse,
                fine);

        Assert.Equal(
            coarse,
            refinement.CoarseParameters);

        Assert.Equal(
            fine,
            refinement.FineParameters);

        Assert.Equal(
            expectedScale,
            refinement.Scale);

        var scaleSquared =
            checked(
                (ulong)expectedScale
                * (ulong)expectedScale);

        var expectedFineTriangulationNumber =
            checked(
                coarse.TriangulationNumber
                * scaleSquared);

        Assert.Equal(
            expectedFineTriangulationNumber,
            fine.TriangulationNumber);
    }
}
