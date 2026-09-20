using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TacticalRegionStageValidationTests
{
    [Fact]
    public void TacticalCellCapturesReadOnlyAdjacencySnapshot()
    {
        var parent =
            new StrategicCellId(
                1UL);

        var firstId =
            new TacticalCellId(
                parent,
                1UL);

        var secondId =
            new TacticalCellId(
                parent,
                2UL);

        var thirdId =
            new TacticalCellId(
                parent,
                3UL);

        var source =
            new List<TacticalCellId>
            {
                secondId
            };

        var cell =
            new TacticalCell(
                firstId,
                source);

        source.Clear();
        source.Add(
            thirdId);

        Assert.Equal(
            new[]
            {
                secondId
            },
            cell.AdjacentCellIds);

        var mutableView =
            Assert.IsAssignableFrom<IList<TacticalCellId>>(
                cell.AdjacentCellIds);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.Add(
                    thirdId));
    }

    [Fact]
    public void TacticalRegionCapturesReadOnlyCellSnapshot()
    {
        var parent =
            new StrategicCellId(
                2UL);

        var firstId =
            new TacticalCellId(
                parent,
                1UL);

        var secondId =
            new TacticalCellId(
                parent,
                2UL);

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

        var source =
            new List<TacticalCell>
            {
                second,
                first
            };

        var region =
            new TacticalRegion(
                parent,
                source);

        source.Clear();

        Assert.Equal(
            new[]
            {
                firstId,
                secondId
            },
            region.Cells
                .Select(
                    cell =>
                        cell.Id)
                .ToArray());

        var mutableView =
            Assert.IsAssignableFrom<IList<TacticalCell>>(
                region.Cells);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.RemoveAt(
                    0));
    }

    [Theory]
    [InlineData(0, 2, 42)]
    [InlineData(2, 2, 122)]
    [InlineData(1, 2, 72)]
    [InlineData(3, 1, 132)]
    [InlineData(3, 2, 192)]
    public void AdditionalGoldbergVariantsMaterialize(
        int m,
        int n,
        int expectedRegionCount)
    {
        var topology =
            CreateTopology(
                m,
                n);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        Assert.Equal(
            expectedRegionCount,
            regions.Count);

        Assert.Equal(
            topology.Cells
                .Select(
                    cell =>
                        cell.Id)
                .ToArray(),
            regions
                .Select(
                    region =>
                        region.StrategicCellId)
                .ToArray());
    }

    [Fact]
    public void LargerG32MaterializationPreservesGlobalIdentityAndLocalAdjacency()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        var tacticalCells =
            regions
                .SelectMany(
                    region =>
                        region.Cells)
                .ToArray();

        Assert.Equal(
            192,
            regions.Count);

        Assert.Equal(
            576,
            tacticalCells.Length);

        Assert.Equal(
            576,
            tacticalCells
                .Select(
                    cell =>
                        cell.Id)
                .Distinct()
                .Count());

        Assert.All(
            regions,
            region =>
                Assert.All(
                    region.Cells,
                    cell =>
                    {
                        Assert.Equal(
                            region.StrategicCellId,
                            cell.Id.ParentStrategicCellId);

                        Assert.All(
                            cell.AdjacentCellIds,
                            adjacentId =>
                                Assert.Equal(
                                    region.StrategicCellId,
                                    adjacentId.ParentStrategicCellId));
                    }));
    }

    [Fact]
    public void LargerG32RepeatedMaterializationHasIdenticalStageSignature()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var first =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        var second =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        Assert.Equal(
            CreateStageSignature(
                first),
            CreateStageSignature(
                second));
    }

    private static StrategicTopology CreateTopology(
        int m,
        int n)
    {
        return GoldbergStrategicTopologyGenerator.Generate(
            new GoldbergParameters(
                m,
                n));
    }

    private static string[] CreateStageSignature(
        IReadOnlyList<TacticalRegion> regions)
    {
        return regions
            .SelectMany(
                region =>
                    region.Cells.Select(
                        cell =>
                            region.StrategicCellId.Value
                            + ":"
                            + cell.Id.LocalOrdinal
                            + "->["
                            + string.Join(
                                ",",
                                cell.AdjacentCellIds.Select(
                                    adjacent =>
                                        adjacent.LocalOrdinal))
                            + "]"))
            .ToArray();
    }
}
