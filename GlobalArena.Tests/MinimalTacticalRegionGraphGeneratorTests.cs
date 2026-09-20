using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class MinimalTacticalRegionGraphGeneratorTests
{
    [Fact]
    public void InvalidParentIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                MinimalTacticalRegionGraphGenerator.Generate(
                    default));
    }

    [Fact]
    public void GeneratedRegionUsesRequestedParent()
    {
        var parent =
            new StrategicCellId(
                17UL);

        var region =
            MinimalTacticalRegionGraphGenerator.Generate(
                parent);

        Assert.Equal(
            parent,
            region.StrategicCellId);
    }

    [Fact]
    public void GeneratedRegionHasExactlyThreeCells()
    {
        var region =
            CreateRegion();

        Assert.Equal(
            3,
            region.Cells.Count);
    }

    [Fact]
    public void GeneratedRegionUsesCanonicalLocalOrdinals()
    {
        var region =
            CreateRegion();

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
    }

    [Fact]
    public void EveryGeneratedCellPreservesParentIdentity()
    {
        var parent =
            new StrategicCellId(
                17UL);

        var region =
            MinimalTacticalRegionGraphGenerator.Generate(
                parent);

        Assert.All(
            region.Cells,
            cell =>
                Assert.Equal(
                    parent,
                    cell.Id.ParentStrategicCellId));
    }

    [Fact]
    public void GeneratedRegionHasExpectedReferenceAdjacency()
    {
        var region =
            CreateRegion();

        Assert.Equal(
            new ulong[]
            {
                2UL
            },
            GetAdjacentOrdinals(
                region.Cells[0]));

        Assert.Equal(
            new ulong[]
            {
                1UL,
                3UL
            },
            GetAdjacentOrdinals(
                region.Cells[1]));

        Assert.Equal(
            new ulong[]
            {
                2UL
            },
            GetAdjacentOrdinals(
                region.Cells[2]));
    }

    [Fact]
    public void GeneratedRegionHasNoSelfLoopsOrDuplicateAdjacency()
    {
        var region =
            CreateRegion();

        Assert.All(
            region.Cells,
            cell =>
            {
                Assert.DoesNotContain(
                    cell.Id,
                    cell.AdjacentCellIds);

                Assert.Equal(
                    cell.AdjacentCellIds.Count,
                    cell.AdjacentCellIds
                        .Distinct()
                        .Count());
            });
    }

    [Fact]
    public void GeneratedAdjacencyIsReciprocal()
    {
        var region =
            CreateRegion();

        foreach (var cell in region.Cells)
        {
            foreach (var adjacentId in cell.AdjacentCellIds)
            {
                var adjacent =
                    region.Cells.Single(
                        candidate =>
                            candidate.Id == adjacentId);

                Assert.Contains(
                    cell.Id,
                    adjacent.AdjacentCellIds);
            }
        }
    }

    [Fact]
    public void GeneratedRegionIsConnected()
    {
        var region =
            CreateRegion();

        var visited =
            new HashSet<TacticalCellId>();

        var queue =
            new Queue<TacticalCellId>();

        queue.Enqueue(
            region.Cells[0].Id);

        while (queue.Count > 0)
        {
            var currentId =
                queue.Dequeue();

            if (!visited.Add(
                currentId))
            {
                continue;
            }

            var current =
                region.Cells.Single(
                    cell =>
                        cell.Id == currentId);

            foreach (var adjacentId
                in current.AdjacentCellIds)
            {
                if (!visited.Contains(
                    adjacentId))
                {
                    queue.Enqueue(
                        adjacentId);
                }
            }
        }

        Assert.Equal(
            region.Cells.Count,
            visited.Count);
    }

    [Fact]
    public void RepeatedGenerationProducesSameCanonicalSignature()
    {
        var parent =
            new StrategicCellId(
                17UL);

        var first =
            MinimalTacticalRegionGraphGenerator.Generate(
                parent);

        var second =
            MinimalTacticalRegionGraphGenerator.Generate(
                parent);

        Assert.Equal(
            CreateSignature(first),
            CreateSignature(second));
    }

    private static TacticalRegion CreateRegion()
    {
        return MinimalTacticalRegionGraphGenerator.Generate(
            new StrategicCellId(
                17UL));
    }

    private static ulong[] GetAdjacentOrdinals(
        TacticalCell cell)
    {
        return cell.AdjacentCellIds
            .Select(
                id =>
                    id.LocalOrdinal)
            .ToArray();
    }

    private static string CreateSignature(
        TacticalRegion region)
    {
        return string.Join(
            "|",
            region.Cells.Select(
                cell =>
                    cell.Id.ParentStrategicCellId.Value
                    + ":"
                    + cell.Id.LocalOrdinal
                    + "->["
                    + string.Join(
                        ",",
                        cell.AdjacentCellIds.Select(
                            adjacent =>
                                adjacent.LocalOrdinal))
                    + "]"));
    }
}
