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
            4U,
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
                        new StrategicStockEntry(
                            new CivilizationId(1UL),
                            new CommodityId(2U),
                            50L),
                        new StrategicStockEntry(
                            new CivilizationId(1UL),
                            new CommodityId(1U),
                            25L)
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
        "9B8FE2B8C5FED45D34C98AD9D46B7C2F"
        + "27F129705ED1C80CB2AD321F4C6CDDFE";

    private const string RevisionOneDigest =
        "B371E034BB5A7AE82CA1AB492BFF28AE"
        + "F232F09E66185265D6397E283E2B4965";

    private const string PopulatedStateDigest =
        "78C89FDE844417C6F878C42BF25C2D80"
        + "73DD8E985F82DF412C9AFB9A618EDB42";

    private const string BoundInitialStateDigest =
        "C28AC884E42AA4EABEF5D838682CAB61"
        + "43F6F4F73BDD4C6AD90F290FAD21AC3D";

    private const string BoundPopulatedStateDigest =
        "5AAB0751879BC3987843918EC68FBEED"
        + "540D74E47A8692A046ED2339A5187F1C";

    private const string MaterializedCivilizationDigest =
        "88811860D6D3E14E26893153A30E5FF5"
        + "2C40A2C78AAEF4A1E8EF42505E11A7F6";
}
