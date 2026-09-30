using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicPhysicalFieldSetTests
{
    [Fact]
    public void NullRequestIsRejected()
    {
        var graph =
            CreateGraph(
                new GoldbergParameters(
                    1,
                    0));

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicPhysicalFieldSet.Generate(
                    null!,
                    graph));
    }

    [Fact]
    public void NullSurfaceGraphIsRejected()
    {
        var request =
            CreateRequest(
                7UL,
                new GoldbergParameters(
                    1,
                    0));

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicPhysicalFieldSet.Generate(
                    request,
                    null!));
    }

    [Fact]
    public void MismatchedSurfaceGraphIsRejected()
    {
        var request =
            CreateRequest(
                7UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                new GoldbergParameters(
                    2,
                    0));

        Assert.Throws<ArgumentException>(
            () =>
                StrategicPhysicalFieldSet.Generate(
                    request,
                    graph));
    }

    [Fact]
    public void GeneratedSetSharesOneCanonicalSurfaceGraph()
    {
        var request =
            CreateRequest(
                7UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var fields =
            StrategicPhysicalFieldSet.Generate(
                request,
                graph);

        Assert.Same(
            graph,
            fields.Elevation.SurfaceGraph);

        Assert.Same(
            graph,
            fields.Relief.SurfaceGraph);

        Assert.Same(
            fields.Elevation,
            fields.LandWater.Elevation);
    }

    [Fact]
    public void WorldGenerationResultIncludesStrategicPhysicalFields()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    7UL,
                    new GoldbergParameters(
                        2,
                        1)));

        Assert.Same(
            result.StrategicSurfaceGraph,
            result.StrategicPhysicalFields.Elevation.SurfaceGraph);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicPhysicalFields.Elevation.Count);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicPhysicalFields.LandWater.Count);
    }

    [Fact]
    public void DifferentSeedsChangeElevationWithoutChangingTopology()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var parameters =
            new GoldbergParameters(
                2,
                1);

        var first =
            generator.Generate(
                CreateRequest(
                    1UL,
                    parameters));

        var second =
            generator.Generate(
                CreateRequest(
                    2UL,
                    parameters));

        Assert.NotEqual(
            first.StrategicPhysicalFields.Elevation.RawValues,
            second.StrategicPhysicalFields.Elevation.RawValues);

        Assert.Equal(
            first.StrategicTopology.Cells.Count,
            second.StrategicTopology.Cells.Count);

        Assert.Equal(
            first.StrategicSurfaceGraph.CellIds,
            second.StrategicSurfaceGraph.CellIds);
    }

    private static WorldGenerationRequest CreateRequest(
        ulong seed,
        GoldbergParameters parameters)
    {
        return new WorldGenerationRequest(
            new WorldSeed(
                seed),
            WorldGenerationVersion.Initial,
            parameters);
    }

    private static StrategicSurfaceGraph CreateGraph(
        GoldbergParameters parameters)
    {
        return new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(
                parameters));
    }
}
