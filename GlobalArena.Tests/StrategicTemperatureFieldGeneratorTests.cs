using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicTemperatureFieldGeneratorTests
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
                StrategicTemperatureFieldGenerator.Generate(
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
                StrategicTemperatureFieldGenerator.Generate(
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
                StrategicTemperatureFieldGenerator.Generate(
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
            StrategicTemperatureFieldGenerator.Generate(
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
    public void EveryTemperatureValueIsInsideFrozenRange()
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
            StrategicTemperatureFieldGenerator.Generate(
                request,
                graph);

        Assert.All(
            field.RawValues,
            value =>
                Assert.InRange(
                    value,
                    StrategicTemperatureFieldGenerator.MinimumRawTemperature,
                    StrategicTemperatureFieldGenerator.MaximumRawTemperature));
    }

    [Fact]
    public void SameRequestProducesSameTemperatureSequence()
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
            StrategicTemperatureFieldGenerator.Generate(
                request,
                graph);

        var second =
            StrategicTemperatureFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void DifferentWorldSeedsProduceDifferentTemperature()
    {
        var parameters =
            new GoldbergParameters(
                2,
                1);

        var graph =
            CreateGraph(
                parameters);

        var first =
            StrategicTemperatureFieldGenerator.Generate(
                CreateRequest(
                    41UL,
                    parameters),
                graph);

        var second =
            StrategicTemperatureFieldGenerator.Generate(
                CreateRequest(
                    42UL,
                    parameters),
                graph);

        Assert.False(
            first.RawValues.SequenceEqual(
                second.RawValues));
    }

    [Fact]
    public void KnownTemperatureVectorIsStable()
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
            StrategicTemperatureFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            51_346L,
            field.GetRawValue(
                0));

        Assert.Equal(
            -621_255L,
            field.GetRawValue(
                1));

        Assert.Equal(
            170_718L,
            field.GetRawValue(
                2));
    }

    [Fact]
    public void TemperatureConsumesCanonicalTemperatureDomainSequence()
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
            StrategicTemperatureFieldGenerator.Generate(
                request,
                graph);

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Temperature);

        for (var index = 0;
             index < graph.NodeCount;
             index++)
        {
            var expected =
                checked(
                    (long)(
                        stream.NextUInt64()
                        % 2_000_001UL)
                    - StrategicScalarField.Denominator);

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
