using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class SharedBorderContractTests
{
    [Fact]
    public void PositiveValuesCreateSharedBorderElementId()
    {
        var edgeId =
            new StrategicEdgeId(
                7UL);

        var id =
            new SharedBorderElementId(
                edgeId,
                3UL);

        Assert.True(
            id.IsValid);

        Assert.Equal(
            edgeId,
            id.StrategicEdgeId);

        Assert.Equal(
            3UL,
            id.LocalOrdinal);
    }

    [Fact]
    public void ZeroLocalOrdinalIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new SharedBorderElementId(
                    new StrategicEdgeId(1UL),
                    0UL));
    }

    [Fact]
    public void InvalidStrategicEdgeIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderElementId(
                    default,
                    1UL));
    }

    [Fact]
    public void DefaultSharedBorderElementIdCannotBeConsumedAsValidIdentity()
    {
        var id =
            default(SharedBorderElementId);

        Assert.False(
            id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => id.StrategicEdgeId);

        Assert.Throws<InvalidOperationException>(
            () => id.LocalOrdinal);
    }

    [Fact]
    public void EquivalentSharedBorderElementValuesProduceEqualIds()
    {
        var first =
            new SharedBorderElementId(
                new StrategicEdgeId(4UL),
                9UL);

        var second =
            new SharedBorderElementId(
                new StrategicEdgeId(4UL),
                9UL);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void DifferentStrategicEdgesProduceDifferentIds()
    {
        var first =
            new SharedBorderElementId(
                new StrategicEdgeId(4UL),
                9UL);

        var second =
            new SharedBorderElementId(
                new StrategicEdgeId(5UL),
                9UL);

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void DifferentLocalOrdinalsProduceDifferentIds()
    {
        var edgeId =
            new StrategicEdgeId(
                4UL);

        var first =
            new SharedBorderElementId(
                edgeId,
                9UL);

        var second =
            new SharedBorderElementId(
                edgeId,
                10UL);

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void SharedBorderElementRejectsInvalidId()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderElement(
                    default));
    }

    [Fact]
    public void SharedBorderBandRejectsInvalidStrategicEdgeId()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderBand(
                    default,
                    new[]
                    {
                        CreateElement(
                            1UL,
                            1UL)
                    }));
    }

    [Fact]
    public void SharedBorderBandRejectsNullElements()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new SharedBorderBand(
                    EdgeId,
                    null!));
    }

    [Fact]
    public void SharedBorderBandRejectsEmptyElements()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderBand(
                    EdgeId,
                    Array.Empty<SharedBorderElement>()));
    }

    [Fact]
    public void SharedBorderBandRejectsNullElement()
    {
        var elements =
            new SharedBorderElement[]
            {
                CreateElement(
                    1UL,
                    1UL),
                null!
            };

        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderBand(
                    EdgeId,
                    elements));
    }

    [Fact]
    public void SharedBorderBandRejectsElementFromDifferentEdge()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderBand(
                    EdgeId,
                    new[]
                    {
                        CreateElement(
                            1UL,
                            1UL),
                        CreateElement(
                            2UL,
                            2UL)
                    }));
    }

    [Fact]
    public void SharedBorderBandRejectsDuplicateElementIds()
    {
        var duplicate =
            CreateElement(
                1UL,
                1UL);

        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderBand(
                    EdgeId,
                    new[]
                    {
                        duplicate,
                        new SharedBorderElement(
                            duplicate.Id)
                    }));
    }

    [Fact]
    public void SharedBorderBandRejectsOrdinalSequenceThatDoesNotStartAtOne()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderBand(
                    EdgeId,
                    new[]
                    {
                        CreateElement(
                            1UL,
                            2UL),
                        CreateElement(
                            1UL,
                            3UL)
                    }));
    }

    [Fact]
    public void SharedBorderBandRejectsOrdinalGap()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new SharedBorderBand(
                    EdgeId,
                    new[]
                    {
                        CreateElement(
                            1UL,
                            1UL),
                        CreateElement(
                            1UL,
                            3UL)
                    }));
    }

    [Fact]
    public void SharedBorderBandOrdersElementsCanonically()
    {
        var band =
            new SharedBorderBand(
                EdgeId,
                new[]
                {
                    CreateElement(
                        1UL,
                        3UL),
                    CreateElement(
                        1UL,
                        1UL),
                    CreateElement(
                        1UL,
                        2UL)
                });

        Assert.Equal(
            EdgeId,
            band.StrategicEdgeId);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL
            },
            band.Elements
                .Select(
                    element =>
                        element.Id.LocalOrdinal)
                .ToArray());
    }

    [Fact]
    public void SharedBorderBandCapturesReadOnlySnapshot()
    {
        var source =
            new List<SharedBorderElement>
            {
                CreateElement(
                    1UL,
                    1UL),
                CreateElement(
                    1UL,
                    2UL)
            };

        var band =
            new SharedBorderBand(
                EdgeId,
                source);

        source.Clear();

        Assert.Equal(
            2,
            band.Elements.Count);

        var mutableView =
            Assert.IsAssignableFrom<IList<SharedBorderElement>>(
                band.Elements);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.RemoveAt(
                    0));
    }

    private static StrategicEdgeId EdgeId =>
        new(
            1UL);

    private static SharedBorderElement CreateElement(
        ulong edgeValue,
        ulong localOrdinal)
    {
        return new SharedBorderElement(
            new SharedBorderElementId(
                new StrategicEdgeId(
                    edgeValue),
                localOrdinal));
    }
}
