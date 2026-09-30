using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicMoistureFieldGeneratorTests
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
                StrategicMoistureFieldGenerator.Generate(
                    null!,
                    graph));
    }

    [Fact]
    public void NullSurfaceGraphIsRejected()
    {
        var request =
            CreateRequest(
                42UL,
                new GoldbergParameters(
                    1,
                    0));

        Assert.Throws<ArgumentNullException>(
            () =>
                StrategicMoistureFieldGenerator.Generate(
                    request,
                    null!));
    }

    [Fact]
    public void MismatchedSurfaceGraphIsRejected()
    {
        var request =
            CreateRequest(
                42UL,
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
                StrategicMoistureFieldGenerator.Generate(
                    request,
                    graph));
    }

    [Fact]
    public void ProducesExactlyOneValuePerSurfaceNode()
    {
        var request =
            CreateRequest(
                42UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var field =
            StrategicMoistureFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            graph.NodeCount,
            field.Count);

        Assert.Same(
            graph,
            field.SurfaceGraph);
    }

    [Fact]
    public void EveryMoistureValueIsInsideFrozenRange()
    {
        var request =
            CreateRequest(
                42UL,
                new GoldbergParameters(
                    3,
                    2));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var field =
            StrategicMoistureFieldGenerator.Generate(
                request,
                graph);

        Assert.All(
            field.RawValues,
            value =>
                Assert.InRange(
                    value,
                    StrategicMoistureFieldGenerator.MinimumRawMoisture,
                    StrategicMoistureFieldGenerator.MaximumRawMoisture));
    }

    [Fact]
    public void SameRequestProducesSameMoistureSequence()
    {
        var request =
            CreateRequest(
                42UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var first =
            StrategicMoistureFieldGenerator.Generate(
                request,
                graph);

        var second =
            StrategicMoistureFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void DifferentWorldSeedsProduceDifferentMoisture()
    {
        var parameters =
            new GoldbergParameters(
                2,
                1);

        var graph =
            CreateGraph(
                parameters);

        var first =
            StrategicMoistureFieldGenerator.Generate(
                CreateRequest(
                    41UL,
                    parameters),
                graph);

        var second =
            StrategicMoistureFieldGenerator.Generate(
                CreateRequest(
                    42UL,
                    parameters),
                graph);

        Assert.False(
            first.RawValues.SequenceEqual(
                second.RawValues));
    }

    [Fact]
    public void KnownMoistureVectorIsStable()
    {
        var request =
            CreateRequest(
                42UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var field =
            StrategicMoistureFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            588_220L,
            field.GetRawValue(
                0));

        Assert.Equal(
            93_903L,
            field.GetRawValue(
                1));

        Assert.Equal(
            380_340L,
            field.GetRawValue(
                2));
    }

    [Fact]
    public void MoistureConsumesCanonicalMoistureDomainSequence()
    {
        var request =
            CreateRequest(
                42UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var field =
            StrategicMoistureFieldGenerator.Generate(
                request,
                graph);

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Moisture);

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            var expected =
                checked(
                    (long)(
                        stream.NextUInt64()
                        % 1_000_001UL));

            Assert.Equal(
                expected,
                field.GetRawValue(
                    index));
        }
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
