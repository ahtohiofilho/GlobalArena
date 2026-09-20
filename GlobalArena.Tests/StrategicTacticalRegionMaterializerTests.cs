using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicTacticalRegionMaterializerTests
{
    [Fact]
    public void NullStrategicTopologyIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicTacticalRegionMaterializer.Materialize(
                    null!));
    }

    [Fact]
    public void MaterializationCreatesOneRegionPerStrategicCell()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        Assert.Equal(
            topology.Cells.Count,
            regions.Count);
    }

    [Fact]
    public void RegionsPreserveCanonicalStrategicCellOrder()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

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
    public void EveryStrategicParentAppearsExactlyOnce()
    {
        var topology =
            CreateTopology(
                1,
                1);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        Assert.Equal(
            topology.Cells.Count,
            regions
                .Select(
                    region =>
                        region.StrategicCellId)
                .Distinct()
                .Count());

        Assert.Equal(
            topology.Cells
                .Select(
                    cell =>
                        cell.Id)
                .OrderBy(
                    id =>
                        id.Value)
                .ToArray(),
            regions
                .Select(
                    region =>
                        region.StrategicCellId)
                .OrderBy(
                    id =>
                        id.Value)
                .ToArray());
    }

    [Fact]
    public void EveryRegionUsesMinimalReferenceGraph()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        Assert.All(
            regions,
            region =>
            {
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
            });
    }

    [Fact]
    public void MaterializedAdjacencyNeverCrossesStrategicParent()
    {
        var topology =
            CreateTopology(
                2,
                1);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        Assert.All(
            regions,
            region =>
                Assert.All(
                    region.Cells,
                    cell =>
                        Assert.All(
                            cell.AdjacentCellIds,
                            adjacentId =>
                                Assert.Equal(
                                    region.StrategicCellId,
                                    adjacentId.ParentStrategicCellId))));
    }

    [Fact]
    public void ReturnedRegionCollectionIsReadOnly()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var regions =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        var mutableView =
            Assert.IsAssignableFrom<IList<TacticalRegion>>(
                regions);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView[0] =
                    mutableView[0]);
    }

    [Fact]
    public void RepeatedMaterializationProducesSameCanonicalSignature()
    {
        var topology =
            CreateTopology(
                2,
                1);

        var first =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        var second =
            StrategicTacticalRegionMaterializer.Materialize(
                topology);

        Assert.Equal(
            CreateSignature(
                first),
            CreateSignature(
                second));
    }

    [Theory]
    [InlineData(1, 0, 12)]
    [InlineData(1, 1, 32)]
    [InlineData(2, 1, 72)]
    public void RepresentativeGoldbergFamiliesMaterialize(
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

    private static StrategicTopology CreateTopology(
        int m,
        int n)
    {
        return GoldbergStrategicTopologyGenerator.Generate(
            new GoldbergParameters(
                m,
                n));
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

    private static string[] CreateSignature(
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
