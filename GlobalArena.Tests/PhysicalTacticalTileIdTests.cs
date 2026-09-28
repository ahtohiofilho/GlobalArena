using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalTileIdTests
{
    [Fact]
    public void ValidValuesCreatePhysicalTacticalTileId()
    {
        var parameters =
            new GoldbergParameters(
                3,
                0);

        var cellId =
            new StrategicCellId(
                42UL);

        var id =
            new PhysicalTacticalTileId(
                parameters,
                cellId);

        Assert.True(id.IsValid);
        Assert.Equal(parameters, id.FineGoldbergParameters);
        Assert.Equal(cellId, id.FineStrategicCellId);
    }

    [Fact]
    public void InvalidFineParametersAreRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileId(
                    default,
                    new StrategicCellId(1UL)));
    }

    [Fact]
    public void InvalidFineCellIdIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileId(
                    new GoldbergParameters(3, 0),
                    default));
    }

    [Fact]
    public void FineCellIdOutsideSelectedTopologyIsRejected()
    {
        var parameters =
            new GoldbergParameters(
                1,
                0);

        Assert.Equal(
            12UL,
            parameters.StrategicCellCount);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new PhysicalTacticalTileId(
                    parameters,
                    new StrategicCellId(13UL)));
    }

    [Fact]
    public void DefaultPhysicalTacticalTileIdCannotBeConsumed()
    {
        var id =
            default(PhysicalTacticalTileId);

        Assert.False(id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => id.FineGoldbergParameters);

        Assert.Throws<InvalidOperationException>(
            () => id.FineStrategicCellId);
    }

    [Fact]
    public void EquivalentValuesProduceEqualPhysicalIds()
    {
        var first =
            new PhysicalTacticalTileId(
                new GoldbergParameters(3, 0),
                new StrategicCellId(7UL));

        var second =
            new PhysicalTacticalTileId(
                new GoldbergParameters(3, 0),
                new StrategicCellId(7UL));

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentFineParametersProduceDifferentPhysicalIds()
    {
        var first =
            new PhysicalTacticalTileId(
                new GoldbergParameters(3, 0),
                new StrategicCellId(7UL));

        var second =
            new PhysicalTacticalTileId(
                new GoldbergParameters(4, 0),
                new StrategicCellId(7UL));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentFineCellIdsProduceDifferentPhysicalIds()
    {
        var parameters =
            new GoldbergParameters(
                3,
                0);

        var first =
            new PhysicalTacticalTileId(
                parameters,
                new StrategicCellId(7UL));

        var second =
            new PhysicalTacticalTileId(
                parameters,
                new StrategicCellId(8UL));

        Assert.NotEqual(first, second);
    }
}