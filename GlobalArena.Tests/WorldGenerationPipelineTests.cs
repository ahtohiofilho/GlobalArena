using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldGenerationPipelineTests
{
    [Fact]
    public void SameRequestAndDomainProduceSameSequence()
    {
        var request =
            CreateRequest(
                42UL,
                1,
                2,
                1);

        var first =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Elevation);

        var second =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Elevation);

        Assert.Equal(
            Take(
                first,
                4),
            Take(
                second,
                4));
    }

    [Fact]
    public void DifferentDomainsProduceDifferentSequences()
    {
        var request =
            CreateRequest(
                42UL,
                1,
                2,
                1);

        var elevation =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Elevation);

        var temperature =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Temperature);

        Assert.NotEqual(
            elevation.NextUInt64(),
            temperature.NextUInt64());
    }

    [Fact]
    public void DifferentWorldSeedsProduceDifferentSequences()
    {
        var first =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    41UL,
                    1,
                    2,
                    1),
                WorldGenerationRandomDomain.Hydrology);

        var second =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    42UL,
                    1,
                    2,
                    1),
                WorldGenerationRandomDomain.Hydrology);

        Assert.NotEqual(
            first.NextUInt64(),
            second.NextUInt64());
    }

    [Fact]
    public void DifferentGenerationVersionsProduceDifferentSequences()
    {
        var first =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    42UL,
                    1,
                    2,
                    1),
                WorldGenerationRandomDomain.Resources);

        var second =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    42UL,
                    2,
                    2,
                    1),
                WorldGenerationRandomDomain.Resources);

        Assert.NotEqual(
            first.NextUInt64(),
            second.NextUInt64());
    }

    [Fact]
    public void DifferentStrategicParametersProduceDifferentSequences()
    {
        var first =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    42UL,
                    1,
                    2,
                    1),
                WorldGenerationRandomDomain.Moisture);

        var second =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    42UL,
                    1,
                    3,
                    1),
                WorldGenerationRandomDomain.Moisture);

        Assert.NotEqual(
            first.NextUInt64(),
            second.NextUInt64());
    }

    [Fact]
    public void DomainSequenceDoesNotDependOnStreamCreationOrder()
    {
        var request =
            CreateRequest(
                77UL,
                1,
                2,
                1);

        var elevationBefore =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Elevation);

        _ =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Resources)
                .NextUInt64();

        var elevationAfter =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Elevation);

        Assert.Equal(
            Take(
                elevationBefore,
                5),
            Take(
                elevationAfter,
                5));
    }

    [Fact]
    public void AdvancingOneDomainDoesNotAdvanceAnotherDomain()
    {
        var request =
            CreateRequest(
                19UL,
                1,
                1,
                0);

        var resources =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Resources);

        var firstHabitability =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Habitability);

        _ =
            resources.NextUInt64();

        _ =
            resources.NextUInt64();

        var secondHabitability =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.Habitability);

        Assert.Equal(
            Take(
                firstHabitability,
                3),
            Take(
                secondHabitability,
                3));
    }

    [Fact]
    public void ElevationKnownVectorIsStable()
    {
        var stream =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    42UL,
                    1,
                    2,
                    1),
                WorldGenerationRandomDomain.Elevation);

        Assert.Equal(
            0x17BE8859387B7F97UL,
            stream.NextUInt64());

        Assert.Equal(
            0x9AF2084E0426D9E3UL,
            stream.NextUInt64());

        Assert.Equal(
            0x879E2AF57152C175UL,
            stream.NextUInt64());
    }

    [Fact]
    public void RandomStreamProgresses()
    {
        var stream =
            WorldGenerationRandomStreamFactory.Create(
                CreateRequest(
                    0UL,
                    1,
                    1,
                    0),
                WorldGenerationRandomDomain.Biomes);

        var first =
            stream.NextUInt64();

        var second =
            stream.NextUInt64();

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void UndefinedRandomDomainIsRejected()
    {
        var request =
            CreateRequest(
                1UL,
                1,
                1,
                0);

        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                WorldGenerationRandomStreamFactory.Create(
                    request,
                    (WorldGenerationRandomDomain)0UL));
    }

    [Fact]
    public void RandomStreamFactoryRejectsNullRequest()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                WorldGenerationRandomStreamFactory.Create(
                    null!,
                    WorldGenerationRandomDomain.Elevation));
    }

    [Fact]
    public void GeneratorRejectsNullRequest()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        Assert.Throws<ArgumentNullException>(
            () =>
                generator.Generate(
                    null!));
    }

    [Fact]
    public void GeneratorRejectsUnsupportedFutureVersion()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var request =
            CreateRequest(
                1UL,
                2,
                1,
                0);

        Assert.Throws<NotSupportedException>(
            () =>
                generator.Generate(
                    request));
    }

    [Fact]
    public void GeneratorUsesAuthoritativeClassITopology()
    {
        AssertGeneratorTopology(
            new GoldbergParameters(
                2,
                0));
    }

    [Fact]
    public void GeneratorUsesAuthoritativeClassIITopology()
    {
        AssertGeneratorTopology(
            new GoldbergParameters(
                2,
                2));
    }

    [Fact]
    public void GeneratorUsesAuthoritativeClassIIITopology()
    {
        AssertGeneratorTopology(
            new GoldbergParameters(
                2,
                1));
    }

    [Fact]
    public void GeneratorPreservesOriginalRequestReference()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var request =
            CreateRequest(
                9UL,
                1,
                2,
                1);

        var result =
            generator.Generate(
                request);

        Assert.Same(
            request,
            result.Request);
    }

    [Fact]
    public void RepeatedGenerationProducesEquivalentCanonicalTopology()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var request =
            CreateRequest(
                111UL,
                1,
                2,
                1);

        var first =
            generator.Generate(
                request);

        var second =
            generator.Generate(
                request);

        AssertEquivalentTopology(
            first.StrategicTopology,
            second.StrategicTopology);
    }

    [Fact]
    public void DifferentSeedsDoNotPerturbAuthoritativeTopologyGeometry()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var first =
            generator.Generate(
                CreateRequest(
                    1UL,
                    1,
                    2,
                    1));

        var second =
            generator.Generate(
                CreateRequest(
                    2UL,
                    1,
                    2,
                    1));

        Assert.NotEqual(
            first.Request.Seed,
            second.Request.Seed);

        AssertEquivalentTopology(
            first.StrategicTopology,
            second.StrategicTopology);
    }

    [Fact]
    public void GeneratorResultTopologyMatchesRequestParameters()
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var request =
            CreateRequest(
                ulong.MaxValue,
                1,
                3,
                2);

        var result =
            generator.Generate(
                request);

        Assert.Equal(
            request.StrategicParameters,
            result.StrategicTopology.Parameters);
    }

    private static WorldGenerationRequest CreateRequest(
        ulong seed,
        int version,
        int m,
        int n)
    {
        return new WorldGenerationRequest(
            new WorldSeed(
                seed),
            new WorldGenerationVersion(
                version),
            new GoldbergParameters(
                m,
                n));
    }

    private static ulong[] Take(
        WorldGenerationRandomStream stream,
        int count)
    {
        var values =
            new ulong[count];

        for (var index = 0;
             index < count;
             index++)
        {
            values[index] =
                stream.NextUInt64();
        }

        return values;
    }

    private static void AssertGeneratorTopology(
        GoldbergParameters parameters)
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        var request =
            new WorldGenerationRequest(
                new WorldSeed(
                    123UL),
                WorldGenerationVersion.Initial,
                parameters);

        var expected =
            GoldbergStrategicTopologyGenerator.Generate(
                parameters);

        var actual =
            generator.Generate(
                request);

        AssertEquivalentTopology(
            expected,
            actual.StrategicTopology);
    }

    private static void AssertEquivalentTopology(
        StrategicTopology expected,
        StrategicTopology actual)
    {
        Assert.Equal(
            expected.Parameters,
            actual.Parameters);

        Assert.Equal(
            expected.Cells.Count,
            actual.Cells.Count);

        Assert.Equal(
            expected.Edges.Count,
            actual.Edges.Count);

        Assert.Equal(
            expected.Vertices.Count,
            actual.Vertices.Count);

        for (var index = 0;
             index < expected.Cells.Count;
             index++)
        {
            var expectedCell =
                expected.Cells[index];

            var actualCell =
                actual.Cells[index];

            Assert.Equal(
                expectedCell.Id,
                actualCell.Id);

            Assert.Equal(
                expectedCell.Kind,
                actualCell.Kind);

            Assert.Equal(
                expectedCell.AdjacentCellIds,
                actualCell.AdjacentCellIds);

            Assert.Equal(
                expectedCell.IncidentEdgeIds,
                actualCell.IncidentEdgeIds);

            Assert.Equal(
                expectedCell.IncidentVertexIds,
                actualCell.IncidentVertexIds);
        }

        for (var index = 0;
             index < expected.Edges.Count;
             index++)
        {
            var expectedEdge =
                expected.Edges[index];

            var actualEdge =
                actual.Edges[index];

            Assert.Equal(
                expectedEdge.Id,
                actualEdge.Id);

            Assert.Equal(
                expectedEdge.IncidentCellIds,
                actualEdge.IncidentCellIds);

            Assert.Equal(
                expectedEdge.IncidentVertexIds,
                actualEdge.IncidentVertexIds);
        }

        for (var index = 0;
             index < expected.Vertices.Count;
             index++)
        {
            var expectedVertex =
                expected.Vertices[index];

            var actualVertex =
                actual.Vertices[index];

            Assert.Equal(
                expectedVertex.Id,
                actualVertex.Id);

            Assert.Equal(
                expectedVertex.IncidentCellIds,
                actualVertex.IncidentCellIds);

            Assert.Equal(
                expectedVertex.IncidentEdgeIds,
                actualVertex.IncidentEdgeIds);
        }
    }
}
