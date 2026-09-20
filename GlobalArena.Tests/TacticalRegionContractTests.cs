using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalRegionContractTests
{
    [Fact]
    public void PositiveValuesCreateTacticalCellId()
    {
        var parent =
            new StrategicCellId(
                7UL);

        var id =
            new TacticalCellId(
                parent,
                3UL);

        Assert.True(id.IsValid);
        Assert.Equal(parent, id.ParentStrategicCellId);
        Assert.Equal(3UL, id.LocalOrdinal);
    }

    [Fact]
    public void ZeroLocalOrdinalIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new TacticalCellId(
                    new StrategicCellId(1UL),
                    0UL));
    }

    [Fact]
    public void InvalidParentIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new TacticalCellId(
                    default,
                    1UL));
    }

    [Fact]
    public void DefaultTacticalCellIdCannotBeConsumedAsValidIdentity()
    {
        var id =
            default(TacticalCellId);

        Assert.False(id.IsValid);

        Assert.Throws<InvalidOperationException>(
            () => id.ParentStrategicCellId);

        Assert.Throws<InvalidOperationException>(
            () => id.LocalOrdinal);
    }

    [Fact]
    public void EquivalentTacticalCellValuesProduceEqualIds()
    {
        var first =
            new TacticalCellId(
                new StrategicCellId(4UL),
                9UL);

        var second =
            new TacticalCellId(
                new StrategicCellId(4UL),
                9UL);

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentStrategicParentsProduceDifferentIds()
    {
        var first =
            new TacticalCellId(
                new StrategicCellId(4UL),
                9UL);

        var second =
            new TacticalCellId(
                new StrategicCellId(5UL),
                9UL);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentLocalOrdinalsProduceDifferentIds()
    {
        var parent =
            new StrategicCellId(
                4UL);

        var first =
            new TacticalCellId(
                parent,
                9UL);

        var second =
            new TacticalCellId(
                parent,
                10UL);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TacticalCellRejectsSelfAdjacency()
    {
        var id =
            CreateId(
                1UL);

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalCell(
                    id,
                    new[]
                    {
                        id
                    }));
    }

    [Fact]
    public void TacticalCellRejectsDuplicateAdjacency()
    {
        var id =
            CreateId(
                1UL);

        var adjacent =
            CreateId(
                2UL);

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalCell(
                    id,
                    new[]
                    {
                        adjacent,
                        adjacent
                    }));
    }

    [Fact]
    public void TacticalCellRejectsCrossRegionAdjacency()
    {
        var id =
            new TacticalCellId(
                new StrategicCellId(1UL),
                1UL);

        var foreign =
            new TacticalCellId(
                new StrategicCellId(2UL),
                1UL);

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalCell(
                    id,
                    new[]
                    {
                        foreign
                    }));
    }

    [Fact]
    public void TacticalCellOrdersAdjacencyCanonically()
    {
        var cell =
            new TacticalCell(
                CreateId(3UL),
                new[]
                {
                    CreateId(4UL),
                    CreateId(1UL),
                    CreateId(2UL)
                });

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                4UL
            },
            cell.AdjacentCellIds
                .Select(
                    id =>
                        id.LocalOrdinal)
                .ToArray());
    }

    [Fact]
    public void TacticalRegionRejectsEmptyCells()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new TacticalRegion(
                    Parent,
                    Array.Empty<TacticalCell>()));
    }

    [Fact]
    public void TacticalRegionRejectsMixedParents()
    {
        var local =
            new TacticalCell(
                CreateId(1UL),
                Array.Empty<TacticalCellId>());

        var foreign =
            new TacticalCell(
                new TacticalCellId(
                    new StrategicCellId(2UL),
                    1UL),
                Array.Empty<TacticalCellId>());

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalRegion(
                    Parent,
                    new[]
                    {
                        local,
                        foreign
                    }));
    }

    [Fact]
    public void TacticalRegionRejectsDuplicateCellIds()
    {
        var first =
            new TacticalCell(
                CreateId(1UL),
                Array.Empty<TacticalCellId>());

        var duplicate =
            new TacticalCell(
                CreateId(1UL),
                Array.Empty<TacticalCellId>());

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalRegion(
                    Parent,
                    new[]
                    {
                        first,
                        duplicate
                    }));
    }

    [Fact]
    public void TacticalRegionRejectsDanglingAdjacency()
    {
        var firstId =
            CreateId(1UL);

        var missingId =
            CreateId(2UL);

        var first =
            new TacticalCell(
                firstId,
                new[]
                {
                    missingId
                });

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalRegion(
                    Parent,
                    new[]
                    {
                        first
                    }));
    }

    [Fact]
    public void TacticalRegionRejectsNonReciprocalAdjacency()
    {
        var firstId =
            CreateId(1UL);

        var secondId =
            CreateId(2UL);

        var first =
            new TacticalCell(
                firstId,
                new[]
                {
                    secondId
                });

        var second =
            new TacticalCell(
                secondId,
                Array.Empty<TacticalCellId>());

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalRegion(
                    Parent,
                    new[]
                    {
                        first,
                        second
                    }));
    }

    [Fact]
    public void TacticalRegionRejectsDisconnectedGraph()
    {
        var firstId =
            CreateId(1UL);

        var secondId =
            CreateId(2UL);

        var thirdId =
            CreateId(3UL);

        var first =
            new TacticalCell(
                firstId,
                new[]
                {
                    secondId
                });

        var second =
            new TacticalCell(
                secondId,
                new[]
                {
                    firstId
                });

        var third =
            new TacticalCell(
                thirdId,
                Array.Empty<TacticalCellId>());

        Assert.Throws<ArgumentException>(
            () =>
                new TacticalRegion(
                    Parent,
                    new[]
                    {
                        first,
                        second,
                        third
                    }));
    }

    [Fact]
    public void TacticalRegionAcceptsConnectedCanonicalGraph()
    {
        var firstId =
            CreateId(1UL);

        var secondId =
            CreateId(2UL);

        var thirdId =
            CreateId(3UL);

        var first =
            new TacticalCell(
                firstId,
                new[]
                {
                    secondId
                });

        var second =
            new TacticalCell(
                secondId,
                new[]
                {
                    thirdId,
                    firstId
                });

        var third =
            new TacticalCell(
                thirdId,
                new[]
                {
                    secondId
                });

        var region =
            new TacticalRegion(
                Parent,
                new[]
                {
                    third,
                    first,
                    second
                });

        Assert.Equal(
            Parent,
            region.StrategicCellId);

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL
            },
            region.Cells
                .Select(
                    cell =>
                        cell.Id.LocalOrdinal)
                .ToArray());

        Assert.Equal(
            new ulong[]
            {
                1UL,
                3UL
            },
            region.Cells[1]
                .AdjacentCellIds
                .Select(
                    id =>
                        id.LocalOrdinal)
                .ToArray());
    }

    private static StrategicCellId Parent =>
        new(
            1UL);

    private static TacticalCellId CreateId(
        ulong localOrdinal)
    {
        return new TacticalCellId(
            Parent,
            localOrdinal);
    }
}
