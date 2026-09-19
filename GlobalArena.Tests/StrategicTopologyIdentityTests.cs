using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicTopologyIdentityTests
{
    [Fact]
    public void PositiveValueCreatesStrategicCellId()
    {
        var id = new StrategicCellId(7UL);

        Assert.True(id.IsValid);
        Assert.Equal(7UL, id.Value);
    }

    [Fact]
    public void ZeroStrategicCellIdIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StrategicCellId(0UL));
    }

    [Fact]
    public void DefaultStrategicCellIdCannotBeConsumedAsValidIdentity()
    {
        var id = default(StrategicCellId);

        Assert.False(id.IsValid);
        Assert.Throws<InvalidOperationException>(
            () => id.Value);
    }

    [Fact]
    public void EqualStrategicCellValuesProduceEqualIds()
    {
        var first = new StrategicCellId(12UL);
        var second = new StrategicCellId(12UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentStrategicCellValuesProduceDifferentIds()
    {
        var first = new StrategicCellId(12UL);
        var second = new StrategicCellId(13UL);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void PositiveValueCreatesStrategicEdgeId()
    {
        var id = new StrategicEdgeId(7UL);

        Assert.True(id.IsValid);
        Assert.Equal(7UL, id.Value);
    }

    [Fact]
    public void ZeroStrategicEdgeIdIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StrategicEdgeId(0UL));
    }

    [Fact]
    public void DefaultStrategicEdgeIdCannotBeConsumedAsValidIdentity()
    {
        var id = default(StrategicEdgeId);

        Assert.False(id.IsValid);
        Assert.Throws<InvalidOperationException>(
            () => id.Value);
    }

    [Fact]
    public void EqualStrategicEdgeValuesProduceEqualIds()
    {
        var first = new StrategicEdgeId(12UL);
        var second = new StrategicEdgeId(12UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentStrategicEdgeValuesProduceDifferentIds()
    {
        var first = new StrategicEdgeId(12UL);
        var second = new StrategicEdgeId(13UL);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void PositiveValueCreatesStrategicVertexId()
    {
        var id = new StrategicVertexId(7UL);

        Assert.True(id.IsValid);
        Assert.Equal(7UL, id.Value);
    }

    [Fact]
    public void ZeroStrategicVertexIdIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StrategicVertexId(0UL));
    }

    [Fact]
    public void DefaultStrategicVertexIdCannotBeConsumedAsValidIdentity()
    {
        var id = default(StrategicVertexId);

        Assert.False(id.IsValid);
        Assert.Throws<InvalidOperationException>(
            () => id.Value);
    }

    [Fact]
    public void EqualStrategicVertexValuesProduceEqualIds()
    {
        var first = new StrategicVertexId(12UL);
        var second = new StrategicVertexId(12UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentStrategicVertexValuesProduceDifferentIds()
    {
        var first = new StrategicVertexId(12UL);
        var second = new StrategicVertexId(13UL);

        Assert.NotEqual(first, second);
    }
}
