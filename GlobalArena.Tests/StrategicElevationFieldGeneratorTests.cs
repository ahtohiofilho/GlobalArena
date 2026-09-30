using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicElevationFieldGeneratorTests
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
                StrategicElevationFieldGenerator.Generate(
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
                StrategicElevationFieldGenerator.Generate(
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
                StrategicElevationFieldGenerator.Generate(
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

        var elevation =
            StrategicElevationFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            graph.NodeCount,
            elevation.Count);

        Assert.Same(
            graph,
            elevation.SurfaceGraph);
    }

    [Fact]
    public void EveryElevationValueIsInsideFrozenRange()
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

        var elevation =
            StrategicElevationFieldGenerator.Generate(
                request,
                graph);

        Assert.All(
            elevation.RawValues,
            value =>
            {
                Assert.InRange(
                    value,
                    StrategicElevationFieldGenerator.MinimumRawElevation,
                    StrategicElevationFieldGenerator.MaximumRawElevation);
            });
    }

    [Fact]
    public void SameRequestProducesSameElevationSequence()
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
            StrategicElevationFieldGenerator.Generate(
                request,
                graph);

        var second =
            StrategicElevationFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void DifferentWorldSeedsProduceDifferentElevation()
    {
        var parameters =
            new GoldbergParameters(
                2,
                1);

        var graph =
            CreateGraph(
                parameters);

        var first =
            StrategicElevationFieldGenerator.Generate(
                CreateRequest(
                    41UL,
                    parameters),
                graph);

        var second =
            StrategicElevationFieldGenerator.Generate(
                CreateRequest(
                    42UL,
                    parameters),
                graph);

        Assert.NotEqual(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void KnownElevationVectorIsStable()
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

        var elevation =
            StrategicElevationFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            -227_194L,
            elevation.GetRawValue(
                0));

        Assert.Equal(
            587_434L,
            elevation.GetRawValue(
                1));

        Assert.Equal(
            -673_957L,
            elevation.GetRawValue(
                2));
    }

    [Fact]
    public void ElevationConsumesCanonicalElevationDomainSequence()
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

        var elevation =
            StrategicElevationFieldGenerator.Generate(
                request,
                graph);

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Elevation);

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
                elevation.GetRawValue(
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
