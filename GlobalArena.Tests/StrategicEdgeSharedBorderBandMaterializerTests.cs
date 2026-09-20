using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicEdgeSharedBorderBandMaterializerTests
{
    [Fact]
    public void NullTopologyIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicEdgeSharedBorderBandMaterializer.Materialize(
                    null!));
    }

    [Fact]
    public void MaterializesOneBandPerStrategicEdge()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        Assert.Equal(
            topology.Edges.Count,
            bands.Count);
    }

    [Fact]
    public void PreservesStrategicEdgeCanonicalOrder()
    {
        var topology =
            CreateTopology(
                2,
                0);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        Assert.Equal(
            topology.Edges
                .Select(
                    edge =>
                        edge.Id)
                .ToArray(),
            bands
                .Select(
                    band =>
                        band.StrategicEdgeId)
                .ToArray());
    }

    [Fact]
    public void EveryStrategicEdgeAppearsExactlyOnce()
    {
        var topology =
            CreateTopology(
                2,
                2);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        Assert.Equal(
            topology.Edges.Count,
            bands
                .Select(
                    band =>
                        band.StrategicEdgeId)
                .Distinct()
                .Count());

        Assert.All(
            topology.Edges,
            edge =>
                Assert.Single(
                    bands,
                    band =>
                        band.StrategicEdgeId
                        == edge.Id));
    }

    [Fact]
    public void EveryBandContainsExactlyOneReferenceElement()
    {
        var topology =
            CreateTopology(
                2,
                1);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        Assert.All(
            bands,
            band =>
                Assert.Single(
                    band.Elements));
    }

    [Fact]
    public void ReferenceElementUsesBandEdgeAndOrdinalOne()
    {
        var topology =
            CreateTopology(
                2,
                1);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        Assert.All(
            bands,
            band =>
            {
                var element =
                    Assert.Single(
                        band.Elements);

                Assert.Equal(
                    band.StrategicEdgeId,
                    element.Id.StrategicEdgeId);

                Assert.Equal(
                    1UL,
                    element.Id.LocalOrdinal);
            });
    }

    [Fact]
    public void ReferenceElementIdsAreGloballyUnique()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        var elementIds =
            bands
                .SelectMany(
                    band =>
                        band.Elements)
                .Select(
                    element =>
                        element.Id)
                .ToArray();

        Assert.Equal(
            topology.Edges.Count,
            elementIds.Length);

        Assert.Equal(
            elementIds.Length,
            elementIds
                .Distinct()
                .Count());
    }

    [Fact]
    public void ReturnedBandCollectionIsReadOnly()
    {
        var topology =
            CreateTopology(
                1,
                0);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        var mutableView =
            Assert.IsAssignableFrom<IList<SharedBorderBand>>(
                bands);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.RemoveAt(
                    0));
    }

    [Fact]
    public void RepeatedMaterializationProducesSameCanonicalSignature()
    {
        var topology =
            CreateTopology(
                3,
                2);

        var first =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        var second =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        Assert.Equal(
            CreateSignature(
                first),
            CreateSignature(
                second));
    }

    [Theory]
    [InlineData(2, 0, 120)]
    [InlineData(2, 2, 360)]
    [InlineData(3, 2, 570)]
    public void RepresentativeGoldbergTopologiesMaterializeExpectedBandCounts(
        int m,
        int n,
        int expectedBandCount)
    {
        var topology =
            CreateTopology(
                m,
                n);

        var bands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                topology);

        Assert.Equal(
            expectedBandCount,
            topology.Edges.Count);

        Assert.Equal(
            expectedBandCount,
            bands.Count);
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

    private static string[] CreateSignature(
        IReadOnlyList<SharedBorderBand> bands)
    {
        return bands
            .Select(
                (band, index) =>
                    index
                    + ":"
                    + band.StrategicEdgeId.Value
                    + ":"
                    + string.Join(
                        ",",
                        band.Elements.Select(
                            element =>
                                element.Id.StrategicEdgeId.Value
                                + "."
                                + element.Id.LocalOrdinal)))
            .ToArray();
    }
}
