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
            9U,
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
        "630BD0AB635727D6B1A71708379DEFA1"
        + "9AB9077360CB21D4D64F5434C6BC9D99";

    private const string RevisionOneDigest =
        "245073CCBC8F034830AEB734AAB6CCE5"
        + "4F71685051BE9D731718A9BB4E7FFB50";

    private const string PopulatedStateDigest =
        "1B651BE1AB1C80FB627BB3426C9B90DB"
        + "CC9521915A62F0EFBC0B504AA94F8887";

    private const string BoundInitialStateDigest =
        "F742F6A4828F16AD3A4B013FD4CCB47C"
        + "D94D2D3111BA5069685DBFD558174C3D";

    private const string BoundPopulatedStateDigest =
        "D5072396920054FA91D6077786920EE1"
        + "1818C51274C7387A7D944D027EB41C6A";

    private const string MaterializedCivilizationDigest =
        "70B618E71CB3858C8BF2AE9F9167D87C"
        + "FFE4F5D9303B707705FDF8CAC706B18B";
}