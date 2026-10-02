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
            7U,
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
        "0510E5244C8FD1169D5D966B793C68BA"
        + "0C296822DB6A75FA111846B8395194C5";

    private const string RevisionOneDigest =
        "3B8485AD1C79F885106592AA91F5C456"
        + "3796E7F5A067C4A8C1FAC268D557F18E";

    private const string PopulatedStateDigest =
        "BC7066464A5BA40B77B7B3111D660EFB"
        + "3F0BF4DD3EF67C7AF8E13F5AC9E84BEB";

    private const string BoundInitialStateDigest =
        "CC9353E179581E49A49BD8F27E6F9335"
        + "18D7C65738E3EE9D7DC5D7B5FF7BECD3";

    private const string BoundPopulatedStateDigest =
        "90A1FDFF0CD336904126A2D00E1D56B6"
        + "76B0B706A0DF49DF8479A6C3009C2C8F";

    private const string MaterializedCivilizationDigest =
        "9EF9ADDC283CF27BA843FC1CED7A78C4"
        + "DFC7104273D880EAB9B5F5699FB60C13";
}