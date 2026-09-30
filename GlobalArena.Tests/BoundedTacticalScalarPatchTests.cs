using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class BoundedTacticalScalarPatchTests
{
    [Fact]
    public void PatchExposesTargetStrategicCell()
    {
        var patch =
            CreatePatch();

        Assert.Equal(
            new StrategicCellId(
                1UL),
            patch.TargetStrategicCellId);
    }

    [Fact]
    public void PatchExposesRefinement()
    {
        var patch =
            CreatePatch();

        Assert.Equal(
            new GoldbergParameters(
                1,
                0),
            patch.Refinement.CoarseParameters);

        Assert.Equal(
            new GoldbergParameters(
                6,
                0),
            patch.Refinement.FineParameters);
    }

    [Fact]
    public void CountMatchesSampleCollection()
    {
        var patch =
            CreatePatch();

        Assert.Equal(
            patch.Samples.Count,
            patch.Count);
    }

    [Fact]
    public void SamplesCollectionIsReadOnly()
    {
        var patch =
            CreatePatch();

        var mutableView =
            Assert.IsAssignableFrom<
                IList<BoundedTacticalScalarSample>>(
                patch.Samples);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.Add(
                    patch.Samples[0]));
    }

    [Fact]
    public void GetSampleByIndexReturnsCanonicalSample()
    {
        var patch =
            CreatePatch();

        for (var index = 0;
             index < patch.Count;
             index++)
        {
            Assert.Same(
                patch.Samples[index],
                patch.GetSample(
                    index));
        }
    }

    [Fact]
    public void InvalidSampleIndexIsRejected()
    {
        var patch =
            CreatePatch();

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                patch.GetSample(
                    -1));

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                patch.GetSample(
                    patch.Count));
    }

    [Fact]
    public void GetSampleByPhysicalIdentityReturnsSameSample()
    {
        var patch =
            CreatePatch();

        foreach (var sample
            in patch.Samples)
        {
            Assert.Same(
                sample,
                patch.GetSample(
                    sample.PhysicalTacticalTileId));
        }
    }

    [Fact]
    public void ForeignPhysicalIdentityIsRejected()
    {
        var patch =
            CreatePatch();

        var fullMap =
            PhysicalTacticalIncidenceMapper.Materialize(
                patch.Refinement);

        var foreign =
            fullMap
                .TileIncidences
                .Select(
                    incidence =>
                        incidence.PhysicalTacticalTileId)
                .First(
                    tileId =>
                        !patch.Samples.Any(
                            sample =>
                                sample.PhysicalTacticalTileId
                                == tileId));

        Assert.Throws<KeyNotFoundException>(
            () =>
                patch.GetSample(
                    foreign));
    }

    private static BoundedTacticalScalarPatch CreatePatch()
    {
        var parameters =
            new GoldbergParameters(
                1,
                0);

        var graph =
            new StrategicSurfaceGraph(
                GoldbergStrategicTopologyGenerator.Generate(
                    parameters));

        var field =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    123L,
                    graph.NodeCount));

        return BoundedTacticalScalarPatchMaterializer.Materialize(
            field,
            new StrategicCellId(
                1UL),
            new GoldbergScaledRefinement(
                parameters,
                new GoldbergParameters(
                    6,
                    0)));
    }
}
