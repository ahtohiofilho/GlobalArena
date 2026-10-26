using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class CanonicalWorldStateHasherTests
{
    [Fact]
    public void NullWorldStateIsRejected()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        Assert.Throws<ArgumentNullException>(
            () => hasher.Compute(
                null!));
    }

    [Fact]
    public void InitialStateHasKnownCanonicalHash()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var hash =
            hasher.Compute(
                WorldState.CreateInitial());

        Assert.Equal(
            8U,
            hash.FormatVersion);

        Assert.Equal(
            InitialStateDigest,
            hash.HexDigest);
    }

    [Fact]
    public void EquivalentStatesProduceSameHash()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var first =
            hasher.Compute(
                WorldState.CreateInitial());

        var second =
            hasher.Compute(
                WorldState.CreateInitial());

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void AdvancedStateHasKnownCanonicalHash()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var initial =
            WorldState.CreateInitial();

        var advanced =
            initial.AdvanceRevision();

        var initialHash =
            hasher.Compute(
                initial);

        var advancedHash =
            hasher.Compute(
                advanced);

        Assert.Equal(
            RevisionOneDigest,
            advancedHash.HexDigest);

        Assert.NotEqual(
            initialHash,
            advancedHash);
    }

    [Fact]
    public void RepeatedHashingOfSameStateIsStable()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var worldState =
            WorldState.CreateInitial()
                .AdvanceRevision()
                .AdvanceRevision();

        var first =
            hasher.Compute(
                worldState);

        var second =
            hasher.Compute(
                worldState);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void PopulatedRuntimeStateHasKnownCanonicalHash()
    {
        var state =
            CreatePopulatedState(
                WorldState.CreateInitial());

        var hash =
            new CanonicalWorldStateHasher()
                .Compute(
                    state);

        Assert.Equal(
            PopulatedStateDigest,
            hash.HexDigest);
    }

    [Fact]
    public void CanonicalStateOrderingProducesSameHash()
    {
        var first =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(2UL),
                            new CivilizationId(1UL)
                        }));

        var second =
            WorldState.CreateInitial()
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationId(1UL),
                            new CivilizationId(2UL)
                        }));

        var hasher =
            new CanonicalWorldStateHasher();

        Assert.Equal(
            hasher.Compute(first),
            hasher.Compute(second));
    }

    [Fact]
    public void BoundInitialStateHasKnownCanonicalHash()
    {
        var generatedWorld =
            GenerateWorld(
                0UL);

        var binding =
            RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld);

        Assert.Equal(
            KnownWorldSignature,
            binding.WorldSignatureSha256Hex);

        var hash =
            new CanonicalWorldStateHasher()
                .Compute(
                    WorldState.CreateBound(
                        generatedWorld));

        Assert.Equal(
            BoundInitialStateDigest,
            hash.HexDigest);
    }

    [Fact]
    public void BoundPopulatedStateHasKnownCanonicalHash()
    {
        var state =
            CreatePopulatedState(
                WorldState.CreateBound(
                    GenerateWorld(
                        0UL)));

        var hash =
            new CanonicalWorldStateHasher()
                .Compute(
                    state);

        Assert.Equal(
            BoundPopulatedStateDigest,
            hash.HexDigest);
    }

    [Fact]
    public void DifferentWorldBindingChangesCanonicalHash()
    {
        var hasher =
            new CanonicalWorldStateHasher();

        var first =
            hasher.Compute(
                WorldState.CreateBound(
                    GenerateWorld(
                        0UL)));

        var second =
            hasher.Compute(
                WorldState.CreateBound(
                    GenerateWorld(
                        1UL)));

        Assert.NotEqual(
            first,
            second);
    }

    [Fact]
    public void MaterializedCivilizationStartsHaveKnownCanonicalHash()
    {
        var state =
            WorldState.CreateBound(
                    GenerateWorld(
                        0UL))
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationRuntimeRecord(
                                new CivilizationId(2UL),
                                new StrategicCellId(10UL)),
                            new CivilizationRuntimeRecord(
                                new CivilizationId(1UL),
                                new StrategicCellId(3UL))
                        }));

        var hash =
            new CanonicalWorldStateHasher()
                .Compute(
                    state);

        Assert.Equal(
            MaterializedCivilizationDigest,
            hash.HexDigest);
    }

    [Fact]
    public void DifferentCivilizationStartChangesCanonicalHash()
    {
        var first =
            WorldState.CreateBound(
                    GenerateWorld(
                        0UL))
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationRuntimeRecord(
                                new CivilizationId(1UL),
                                new StrategicCellId(3UL)),
                            new CivilizationRuntimeRecord(
                                new CivilizationId(2UL),
                                new StrategicCellId(10UL))
                        }));

        var second =
            WorldState.CreateBound(
                    GenerateWorld(
                        0UL))
                .WithCivilizations(
                    new CivilizationRuntimeState(
                        new[]
                        {
                            new CivilizationRuntimeRecord(
                                new CivilizationId(1UL),
                                new StrategicCellId(4UL)),
                            new CivilizationRuntimeRecord(
                                new CivilizationId(2UL),
                                new StrategicCellId(10UL))
                        }));

        var hasher =
            new CanonicalWorldStateHasher();

        Assert.NotEqual(
            hasher.Compute(first),
            hasher.Compute(second));
    }

    private static WorldState CreatePopulatedState(
        WorldState state)
    {
        return state
            .WithCivilizations(
                new CivilizationRuntimeState(
                    new[]
                    {
                        new CivilizationId(2UL),
                        new CivilizationId(1UL)
                    }))
            .WithEconomy(
                new EconomyRuntimeState(
                    new[]
                    {
                        new EconomicPointRuntimeState(
                            new EconomicPointId(2UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(4UL),
                            10UL,
                            new[]
                            {
                                new EconomicActivityWorkforceAllocation(
                                    EconomicActivityKind.TradeLogistics,
                                    3UL),
                                new EconomicActivityWorkforceAllocation(
                                    EconomicActivityKind.Agriculture,
                                    7UL)
                            }),
                        new EconomicPointRuntimeState(
                            new EconomicPointId(1UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(3UL),
                            6UL,
                            new[]
                            {
                                new EconomicActivityWorkforceAllocation(
                                    EconomicActivityKind.Mining,
                                    6UL)
                            })
                    }))
            .WithWarfare(
                new WarfareRuntimeState(
                    new[]
                    {
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(5UL),
                            new CivilizationId(2UL),
                            new StrategicCellId(10UL)),
                        new MilitaryUnitRuntimeState(
                            new MilitaryUnitId(2UL),
                            new CivilizationId(1UL),
                            new StrategicCellId(3UL))
                    }));
    }

    private static WorldGenerationResult GenerateWorld(
        ulong seed)
    {
        IWorldGenerator generator =
            new DeterministicWorldGenerator();

        return generator.Generate(
            new WorldGenerationRequest(
                new WorldSeed(
                    seed),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(
                    2,
                    0)));
    }

    private const string KnownWorldSignature =
        "fa9677da59098b4eccc4a3911299520ad"
        + "0549780b4470b2fae96f607da774c52";

    private const string InitialStateDigest =
        "7BB4EFA9FEEC9EF715AED6A3BB0BF22B"
        + "934CC876D684331807B0CCA49E05D276";

    private const string RevisionOneDigest =
        "D22BA1D3D2FB2DBBDBAB372AF644C8BA"
        + "8457A2E2C3D7EE3EA86EF8463DC65CEF";

    private const string PopulatedStateDigest =
        "D7224B08ADBC47C1EA65C5CFD03A9491"
        + "335491284900C86893845C528B1B0905";

    private const string BoundInitialStateDigest =
        "4C7C83838A66C90FDA3065A72E476128"
        + "59EA7627C3D5B8BFEEB8A8D3D63CD896";

    private const string BoundPopulatedStateDigest =
        "2C3356F4AD889E4942C5FA0D4008E721"
        + "62F44F0698078A58AE9059442970EBF3";

    private const string MaterializedCivilizationDigest =
        "40544A77B7F0DAF81F6A6BFFD57696C7"
        + "057B2DD36821E18F966B56135C4F25E0";
}