using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class StrategicResourcePotentialFieldGeneratorTests
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
                StrategicResourcePotentialFieldGenerator.Generate(
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
                StrategicResourcePotentialFieldGenerator.Generate(
                    request,
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

        Assert.Throws<ArgumentException>(
            () =>
                StrategicResourcePotentialFieldGenerator.Generate(
                    request,
                    graph));
    }

    [Fact]
    public void GeneratedPotentialContainsOneValuePerStrategicNode()
    {
        var request =
            CreateRequest(
                11UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var potential =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            graph.NodeCount,
            potential.Count);
    }

    [Fact]
    public void GeneratedPotentialUsesCanonicalSurfaceGraph()
    {
        var request =
            CreateRequest(
                11UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var potential =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        Assert.Same(
            graph,
            potential.SurfaceGraph);
    }

    [Fact]
    public void GeneratedPotentialIsNormalized()
    {
        var request =
            CreateRequest(
                13UL,
                new GoldbergParameters(
                    3,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var potential =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        Assert.All(
            potential.RawValues,
            value =>
            {
                Assert.InRange(
                    value,
                    StrategicResourcePotentialFieldGenerator.MinimumRawPotential,
                    StrategicResourcePotentialFieldGenerator.MaximumRawPotential);
            });
    }

    [Fact]
    public void RepeatedGenerationIsDeterministic()
    {
        var request =
            CreateRequest(
                17UL,
                new GoldbergParameters(
                    2,
                    1));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var first =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        var second =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void DifferentSeedsCanChangePotentialWithoutChangingTopology()
    {
        var parameters =
            new GoldbergParameters(
                2,
                1);

        var firstGraph =
            CreateGraph(
                parameters);

        var secondGraph =
            CreateGraph(
                parameters);

        var first =
            StrategicResourcePotentialFieldGenerator.Generate(
                CreateRequest(
                    1UL,
                    parameters),
                firstGraph);

        var second =
            StrategicResourcePotentialFieldGenerator.Generate(
                CreateRequest(
                    2UL,
                    parameters),
                secondGraph);

        Assert.Equal(
            firstGraph.CellIds,
            secondGraph.CellIds);

        Assert.False(
            first.RawValues.SequenceEqual(
                second.RawValues));
    }

    [Fact]
    public void ResourcesDomainIsAuthoritativeForBaselinePotential()
    {
        var request =
            CreateRequest(
                19UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var potential =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Resources);

        var expected =
            new long[
                graph.NodeCount];

        for (var index = 0;
             index < expected.Length;
             index++)
        {
            expected[index] =
                checked(
                    (long)(
                        stream.NextUInt64()
                        % 1_000_001UL));
        }

        Assert.Equal(
            expected,
            potential.RawValues);
    }

    [Fact]
    public void HabitabilityDomainDoesNotDriveResourcePotential()
    {
        var request =
            CreateRequest(
                23UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var potential =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Habitability);

        var habitabilityDomainValues =
            new long[
                graph.NodeCount];

        for (var index = 0;
             index < habitabilityDomainValues.Length;
             index++)
        {
            habitabilityDomainValues[index] =
                checked(
                    (long)(
                        stream.NextUInt64()
                        % 1_000_001UL));
        }

        Assert.False(
            potential.RawValues.SequenceEqual(
                habitabilityDomainValues));
    }

    [Fact]
    public void ZeroSeedRemainsAValidDeterministicInput()
    {
        var request =
            CreateRequest(
                0UL,
                new GoldbergParameters(
                    1,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var first =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        var second =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        Assert.Equal(
            first.RawValues,
            second.RawValues);
    }

    [Fact]
    public void FieldSetWrapsTheAcceptedGeneralPotential()
    {
        var request =
            CreateRequest(
                29UL,
                new GoldbergParameters(
                    2,
                    0));

        var graph =
            CreateGraph(
                request.StrategicParameters);

        var direct =
            StrategicResourcePotentialFieldGenerator.Generate(
                request,
                graph);

        var fields =
            StrategicResourcePotentialFieldSet.Generate(
                request,
                graph);

        Assert.Same(
            graph,
            fields.GeneralPotential.SurfaceGraph);

        Assert.Equal(
            direct.RawValues,
            fields.GeneralPotential.RawValues);
    }

    [Fact]
    public void WorldGenerationResultIncludesStrategicResourcePotential()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    31UL,
                    new GoldbergParameters(
                        2,
                        1)));

        Assert.Same(
            result.StrategicSurfaceGraph,
            result.StrategicResourcePotentialFields.GeneralPotential.SurfaceGraph);

        Assert.Equal(
            result.StrategicSurfaceGraph.NodeCount,
            result.StrategicResourcePotentialFields.GeneralPotential.Count);
    }

    [Fact]
    public void ResultResourcePotentialCanBeRegeneratedFromItsRequest()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var result =
            generator.Generate(
                CreateRequest(
                    37UL,
                    new GoldbergParameters(
                        2,
                        1)));

        var regenerated =
            StrategicResourcePotentialFieldSet.Generate(
                result.Request,
                result.StrategicSurfaceGraph);

        Assert.Equal(
            result.StrategicResourcePotentialFields.GeneralPotential.RawValues,
            regenerated.GeneralPotential.RawValues);
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
