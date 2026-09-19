using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GoldbergParametersTests
{
    [Fact]
    public void NegativeMIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GoldbergParameters(
                -1,
                0));
    }

    [Fact]
    public void NegativeNIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GoldbergParameters(
                0,
                -1));
    }

    [Fact]
    public void ZeroPairIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () => new GoldbergParameters(
                0,
                0));
    }

    [Fact]
    public void DefaultParametersAreInvalid()
    {
        var parameters =
            default(GoldbergParameters);

        Assert.False(parameters.IsValid);
    }

    [Fact]
    public void G10HasDodecahedralReferenceCounts()
    {
        var parameters =
            new GoldbergParameters(
                1,
                0);

        Assert.True(parameters.IsValid);
        Assert.Equal(1UL, parameters.TriangulationNumber);
        Assert.Equal(12UL, parameters.StrategicCellCount);
        Assert.Equal(30UL, parameters.StrategicEdgeCount);
        Assert.Equal(20UL, parameters.StrategicVertexCount);
        Assert.Equal(12UL, parameters.PentagonCount);
        Assert.Equal(0UL, parameters.HexagonCount);
    }

    [Fact]
    public void G11HasExpectedReferenceCounts()
    {
        var parameters =
            new GoldbergParameters(
                1,
                1);

        Assert.Equal(3UL, parameters.TriangulationNumber);
        Assert.Equal(32UL, parameters.StrategicCellCount);
        Assert.Equal(90UL, parameters.StrategicEdgeCount);
        Assert.Equal(60UL, parameters.StrategicVertexCount);
        Assert.Equal(12UL, parameters.PentagonCount);
        Assert.Equal(20UL, parameters.HexagonCount);
    }

    [Fact]
    public void G21HasExpectedReferenceCounts()
    {
        var parameters =
            new GoldbergParameters(
                2,
                1);

        Assert.Equal(7UL, parameters.TriangulationNumber);
        Assert.Equal(72UL, parameters.StrategicCellCount);
        Assert.Equal(210UL, parameters.StrategicEdgeCount);
        Assert.Equal(140UL, parameters.StrategicVertexCount);
        Assert.Equal(12UL, parameters.PentagonCount);
        Assert.Equal(60UL, parameters.HexagonCount);
    }

    [Fact]
    public void SwappedParametersPreserveCountsButRemainDistinct()
    {
        var first =
            new GoldbergParameters(
                2,
                1);

        var second =
            new GoldbergParameters(
                1,
                2);

        Assert.NotEqual(first, second);
        Assert.Equal(
            first.TriangulationNumber,
            second.TriangulationNumber);
        Assert.Equal(
            first.StrategicCellCount,
            second.StrategicCellCount);
        Assert.Equal(
            first.StrategicEdgeCount,
            second.StrategicEdgeCount);
        Assert.Equal(
            first.StrategicVertexCount,
            second.StrategicVertexCount);
    }

    [Fact]
    public void EquivalentParametersCompareByValue()
    {
        var first =
            new GoldbergParameters(
                3,
                2);

        var second =
            new GoldbergParameters(
                3,
                2);

        Assert.Equal(first, second);
    }

    [Fact]
    public void CountsThatOverflowUInt64AreRejected()
    {
        Assert.Throws<OverflowException>(
            () => new GoldbergParameters(
                int.MaxValue,
                int.MaxValue));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 1)]
    public void ReferenceCountsSatisfyEulerIdentity(
        int m,
        int n)
    {
        var parameters =
            new GoldbergParameters(
                m,
                n);

        var euler =
            (long)parameters.StrategicVertexCount
            - (long)parameters.StrategicEdgeCount
            + (long)parameters.StrategicCellCount;

        Assert.Equal(2L, euler);
    }
}
