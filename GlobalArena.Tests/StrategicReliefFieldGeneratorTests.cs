using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicReliefFieldGeneratorTests
{
    [Fact]
    public void NullElevationIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicReliefFieldGenerator.Generate(
                    null!));
    }

    [Fact]
    public void ReliefCountMatchesElevationCount()
    {
        var elevation =
            CreateGeneratedElevation();

        var relief =
            StrategicReliefFieldGenerator.Generate(
                elevation);

        Assert.Equal(
            elevation.Count,
            relief.Count);
    }

    [Fact]
    public void FlatElevationProducesZeroRelief()
    {
        var graph =
            CreateGraph();

        var elevation =
            new StrategicScalarField(
                graph,
                Enumerable.Repeat(
                    123L,
                    graph.NodeCount));

        var relief =
            StrategicReliefFieldGenerator.Generate(
                elevation);

        Assert.All(
            relief.RawValues,
            value =>
                Assert.Equal(
                    0L,
                    value));
    }

    [Fact]
    public void ReliefEqualsMaximumAbsoluteNeighborDelta()
    {
        var elevation =
            CreateGeneratedElevation();

        var graph =
            elevation.SurfaceGraph;

        var relief =
            StrategicReliefFieldGenerator.Generate(
                elevation);

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            var current =
                elevation.GetRawValue(
                    index);

            var expected =
                graph.GetNeighborIndexes(
                    index)
                    .Select(
                        neighborIndex =>
                            Math.Abs(
                                elevation.GetRawValue(
                                    neighborIndex)
                                - current))
                    .Max();

            Assert.Equal(
                expected,
                relief.GetRawValue(
                    index));
        }
    }

    [Fact]
    public void ReliefIsAlwaysNonNegativeForGeneratedElevation()
    {
        var relief =
            StrategicReliefFieldGenerator.Generate(
                CreateGeneratedElevation());

        Assert.All(
            relief.RawValues,
            value =>
                Assert.True(
                    value >= 0L));
    }

    [Fact]
    public void RepeatedReliefGenerationIsDeterministic()
    {
        var elevation =
            CreateGeneratedElevation();

        var first =
            StrategicReliefFieldGenerator.Generate(
                elevation);

        var second =
            StrategicReliefFieldGenerator.Generate(
                elevation);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void ReliefPreservesSurfaceGraphReference()
    {
        var elevation =
            CreateGeneratedElevation();

        var relief =
            StrategicReliefFieldGenerator.Generate(
                elevation);

        Assert.Same(
            elevation.SurfaceGraph,
            relief.SurfaceGraph);
    }

    private static StrategicScalarField CreateGeneratedElevation()
    {
        var graph =
            CreateGraph();

        var request =
            new WorldGenerationRequest(
                new WorldSeed(
                    123UL),
                WorldGenerationVersion.Initial,
                graph.StrategicTopology.Parameters);

        return StrategicElevationFieldGenerator.Generate(
            request,
            graph);
    }

    private static StrategicSurfaceGraph CreateGraph()
    {
        return new StrategicSurfaceGraph(
            GoldbergStrategicTopologyGenerator.Generate(
                new GoldbergParameters(
                    2,
                    1)));
    }
}
