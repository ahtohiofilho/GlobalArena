using GlobalArena.Kernel;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class WorldGenerationContractTests
{
    [Fact]
    public void ZeroIsValidWorldSeed()
    {
        var seed =
            new WorldSeed(
                0UL);

        Assert.Equal(
            0UL,
            seed.Value);
    }

    [Fact]
    public void MaximumValueIsValidWorldSeed()
    {
        var seed =
            new WorldSeed(
                ulong.MaxValue);

        Assert.Equal(
            ulong.MaxValue,
            seed.Value);
    }

    [Fact]
    public void EqualWorldSeedValuesCompareEqual()
    {
        Assert.Equal(
            new WorldSeed(42UL),
            new WorldSeed(42UL));
    }

    [Fact]
    public void WorldSeedAndSimulationSeedRemainDistinctTypes()
    {
        Assert.NotEqual(
            typeof(WorldSeed),
            typeof(SimulationSeed));
    }

    [Fact]
    public void PositiveWorldGenerationVersionIsValid()
    {
        var version =
            new WorldGenerationVersion(
                7);

        Assert.True(
            version.IsValid);

        Assert.Equal(
            7,
            version.Value);
    }

    [Fact]
    public void InitialWorldGenerationVersionIsOne()
    {
        var version =
            WorldGenerationVersion.Initial;

        Assert.True(
            version.IsValid);

        Assert.Equal(
            1,
            version.Value);
    }

    [Fact]
    public void ZeroWorldGenerationVersionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new WorldGenerationVersion(
                    0));
    }

    [Fact]
    public void NegativeWorldGenerationVersionIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () =>
                new WorldGenerationVersion(
                    -1));
    }

    [Fact]
    public void DefaultWorldGenerationVersionIsInvalid()
    {
        var version =
            default(WorldGenerationVersion);

        Assert.False(
            version.IsValid);
    }

    [Fact]
    public void DefaultWorldGenerationVersionValueCannotBeConsumed()
    {
        var version =
            default(WorldGenerationVersion);

        Assert.Throws<InvalidOperationException>(
            () =>
                _ = version.Value);
    }

    [Fact]
    public void RequestPreservesIdentityAndStrategicParameters()
    {
        var seed =
            new WorldSeed(
                123UL);

        var version =
            WorldGenerationVersion.Initial;

        var parameters =
            new GoldbergParameters(
                2,
                1);

        var request =
            new WorldGenerationRequest(
                seed,
                version,
                parameters);

        Assert.Equal(
            seed,
            request.Seed);

        Assert.Equal(
            version,
            request.Version);

        Assert.Equal(
            parameters,
            request.StrategicParameters);
    }

    [Fact]
    public void RequestRejectsDefaultGenerationVersion()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new WorldGenerationRequest(
                    new WorldSeed(1UL),
                    default,
                    new GoldbergParameters(
                        1,
                        0)));
    }

    [Fact]
    public void RequestRejectsDefaultGoldbergParameters()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new WorldGenerationRequest(
                    new WorldSeed(1UL),
                    WorldGenerationVersion.Initial,
                    default));
    }

    [Fact]
    public void EquivalentRequestsCompareEqual()
    {
        var first =
            new WorldGenerationRequest(
                new WorldSeed(5UL),
                new WorldGenerationVersion(2),
                new GoldbergParameters(
                    3,
                    0));

        var second =
            new WorldGenerationRequest(
                new WorldSeed(5UL),
                new WorldGenerationVersion(2),
                new GoldbergParameters(
                    3,
                    0));

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void DifferentWorldSeedsProduceDifferentRequests()
    {
        var first =
            new WorldGenerationRequest(
                new WorldSeed(5UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    3,
                    0));

        var second =
            new WorldGenerationRequest(
                new WorldSeed(6UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    3,
                    0));

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void ResultPreservesRequestAndAuthoritativeTopology()
    {
        var request =
            CreateRequest(
                new GoldbergParameters(
                    1,
                    0));

        var topology =
            GoldbergStrategicTopologyGenerator.Generate(
                request.StrategicParameters);

        var result =
            new WorldGenerationResult(
                request,
                topology);

        Assert.Same(
            request,
            result.Request);

        Assert.Same(
            topology,
            result.StrategicTopology);
    }

    [Fact]
    public void ResultRejectsNullRequest()
    {
        var topology =
            GoldbergStrategicTopologyGenerator.Generate(
                new GoldbergParameters(
                    1,
                    0));

        Assert.Throws<ArgumentNullException>(
            () =>
                new WorldGenerationResult(
                    null!,
                    topology));
    }

    [Fact]
    public void ResultRejectsNullStrategicTopology()
    {
        var request =
            CreateRequest(
                new GoldbergParameters(
                    1,
                    0));

        Assert.Throws<ArgumentNullException>(
            () =>
                new WorldGenerationResult(
                    request,
                    null!));
    }

    [Fact]
    public void ResultRejectsTopologyThatDoesNotMatchRequest()
    {
        var request =
            CreateRequest(
                new GoldbergParameters(
                    1,
                    0));

        var topology =
            GoldbergStrategicTopologyGenerator.Generate(
                new GoldbergParameters(
                    2,
                    0));

        Assert.Throws<ArgumentException>(
            () =>
                new WorldGenerationResult(
                    request,
                    topology));
    }

    [Fact]
    public void WorldGeneratorInterfaceCarriesRequestToResultBoundary()
    {
        var request =
            CreateRequest(
                new GoldbergParameters(
                    1,
                    0));

        IWorldGenerator generator =
            new ContractGenerator();

        var result =
            generator.Generate(
                request);

        Assert.Equal(
            request,
            result.Request);

        Assert.Equal(
            request.StrategicParameters,
            result.StrategicTopology.Parameters);
    }

    private static WorldGenerationRequest CreateRequest(
        GoldbergParameters parameters)
    {
        return new WorldGenerationRequest(
            new WorldSeed(99UL),
            WorldGenerationVersion.Initial,
            parameters);
    }

    private sealed class ContractGenerator
        : IWorldGenerator
    {
        public WorldGenerationResult Generate(
            WorldGenerationRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var topology =
                GoldbergStrategicTopologyGenerator.Generate(
                    request.StrategicParameters);

            return new WorldGenerationResult(
                request,
                topology);
        }
    }
}
