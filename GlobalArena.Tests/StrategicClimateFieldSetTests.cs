using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicClimateFieldSetTests
{
    [Fact]
    public void NullRequestIsRejected()
    {
        var graph =
            CreateGraph(
                new GoldbergParameters(
                    1,
                    0));

        var physical =
            StrategicPhysicalFieldSet.Generate(
                CreateRequest(
                    7UL,
                    graph.StrategicTopology.Parameters),
                graph);

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicClimateFieldSet.Generate(
                    null!,
                    graph,
                    physical));
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

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var physical =
            StrategicPhysicalFieldSet.Generate(
                request,
                graph);

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicClimateFieldSet.Generate(
                    request,
                    null!,
                    physical));
    }

    [Fact]
    public void NullPhysicalFieldsAreRejected()
    {
        var request =
            CreateRequest(
                7UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicClimateFieldSet.Generate(
                    request,
                    graph,
                    null!));
    }

    [Fact]
    public void MismatchedRequestAndGraphAreRejected()
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

        var physical =
            StrategicPhysicalFieldSet.Generate(
                CreateRequest(
                    7UL,
                    graph.StrategicTopology.Parameters),
                graph);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicClimateFieldSet.Generate(
                    request,
                    graph,
                    physical));
    }

    [Fact]
    public void PhysicalFieldsFromDifferentGraphAreRejected()
    {
        var request =
            CreateRequest(
                7UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var otherGraph =
            CreateGraph(
                request.StrategicParameters);

        var physical =
            StrategicPhysicalFieldSet.Generate(
                request,
                otherGraph);

        Assert.Throws<ArgumentException>(
            () =>
                StrategicClimateFieldSet.Generate(
                    request,
                    graph,
                    physical));
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

        var physical =
            StrategicPhysicalFieldSet.Generate(
                request,
                graph);

        var climate =
            StrategicClimateFieldSet.Generate(
                request,
                graph,
                physical);

        Assert.Same(
            graph,
            climate.Temperature.SurfaceGraph);

        Assert.Same(
            graph,
            climate.Moisture.SurfaceGraph);

        Assert.Same(
            graph,
            climate.WaterAvailability.SurfaceGraph);
    }

    [Fact]
    public void WorldGenerationResultIncludesStrategicClimateFields()
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
            result.StrategicClimateFields.Temperature.SurfaceGraph);

        Assert.Same(
            result.StrategicSurfaceGraph,
            result.StrategicClimateFields.Moisture.SurfaceGraph);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicClimateFields.WaterAvailability.Count);
    }

    [Fact]
    public void DifferentSeedsChangeTemperatureAndMoistureWithoutChangingTopology()
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

        Assert.False(
            first.StrategicClimateFields.Temperature.RawValues.SequenceEqual(
                second.StrategicClimateFields.Temperature.RawValues));

        Assert.False(
            first.StrategicClimateFields.Moisture.RawValues.SequenceEqual(
                second.StrategicClimateFields.Moisture.RawValues));

        Assert.Equal(
            first.StrategicSurfaceGraph.CellIds,
            second.StrategicSurfaceGraph.CellIds);
    }

    [Fact]
    public void WaterAvailabilityCanBeReDerivedWithoutRandomState()
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

        var physical =
            StrategicPhysicalFieldSet.Generate(
                request,
                graph);

        var climate =
            StrategicClimateFieldSet.Generate(
                request,
                graph,
                physical);

        var derived =
            StrategicWaterAvailabilityFieldGenerator.Generate(
                climate.Moisture,
                physical);

        Assert.Equal(
            climate.WaterAvailability.RawValues,
            derived.RawValues);
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
