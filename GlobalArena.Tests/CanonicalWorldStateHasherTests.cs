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
            6U,
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
        "346FDBC6D4444D870BEA599E586932D6"
        + "9EE1E5E5139222AD74C11324FFA87912";

    private const string RevisionOneDigest =
        "A360F7D8A423283E01E2FF99228BDB9"
        + "BB6D9776547E02B6BFE52AF9866CD53FC";

    private const string PopulatedStateDigest =
        "A1D8A3F78B4ECF3C2119BECCACDC4681"
        + "676A901610188EDA1A50942122B8AEE5";

    private const string BoundInitialStateDigest =
        "AD76D9347063868451202A4F1C1C6D7C"
        + "48423E9152C408CE6ECD03B32CD75A19";

    private const string BoundPopulatedStateDigest =
        "B6CBB197AD69D3D9ADBAFB9EE1A2F1A4"
        + "A7796463B922F0115E82F537BF55A072";

    private const string MaterializedCivilizationDigest =
        "16E2ADC8F66DB3B9967F9E53304DFF43"
        + "1369CEAB7C117D86598D737255BC99E2";
}
