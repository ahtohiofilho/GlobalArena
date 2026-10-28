using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-I2: a complete v8 world + natural-capital/material save file.
/// Strictly refuses v9, wrong generated worlds and modified records.
/// </summary>
public sealed class G3I2FullV8SaveBundleTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId C1 = new(1UL);
    private static readonly CivilizationId C2 = new(2UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);

    private sealed class TempFolder : IDisposable
    {
        public string PathName { get; } = Path.Combine(
            Path.GetTempPath(), "ga-g3i2-" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(PathName);
        public void Dispose()
        {
            if (Directory.Exists(PathName))
                Directory.Delete(PathName, true);
        }
    }

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static (WorldState World, StrategicCellId Site1, StrategicCellId Site2)
        World(WorldGenerationResult generated, bool initializedPopulation = true)
    {
        var lands = generated.StrategicTopology.Cells.Select(c => c.Id).Where(id =>
            generated.StrategicPhysicalFields.LandWater.GetKind(id) ==
                StrategicLandWaterKind.Land).Take(2).ToArray();
        Assert.Equal(2, lands.Length);

        var baseWorld = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[]
            {
                new CivilizationRuntimeRecord(C2, lands[1]),
                new CivilizationRuntimeRecord(C1, lands[0])
            }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(lands[1], C2),
                new StrategicTerritoryControlEntry(lands[0], C1)
            }))
            .WithDiplomacy(new BaselineDiplomacyRuntimeState(new[]
            {
                new BaselineDiplomacyRelationEntry(
                    C2, C1, BaselineDiplomacyRelationKind.Ally)
            }));

        var economy = new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(B, C2, lands[1], 3UL,
                new[] { new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Forestry, 3UL) }),
            new EconomicPointRuntimeState(A, C1, lands[0], 4UL,
                new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture, 2UL),
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Mining, 2UL)
                })
        });

        baseWorld = initializedPopulation
            ? baseWorld.WithEconomyAndPopulation(
                economy, new PopulationRuntimeState(new[]
                {
                    new PopulationPointRuntimeState(B, 3UL),
                    new PopulationPointRuntimeState(A, 4UL)
                }))
            : baseWorld.WithEconomy(economy);

        return (baseWorld
            .WithWarfare(new WarfareRuntimeState(new[]
            {
                new MilitaryUnitRuntimeState(new MilitaryUnitId(3UL),
                    C2, lands[1])
            }))
            .WithRestoredRevision(11UL), lands[0], lands[1]);
    }

    private static EconomicEcologicalCheckpoint Snapshot(
        WorldGenerationResult generated, WorldState world) =>
        new(generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated,
                new EconomicNaturalCapitalPolicy(20UL * U, 40UL * U,
                    200_000L, 100_000L, 50_000L)),
            new EconomicMaterialReserveBook(new[]
            {
                new EconomicMaterialReserveBalance(A, Wood, U / 4UL),
                new EconomicMaterialReserveBalance(B, Stone, U / 8UL)
            }, Array.Empty<EconomicMaterialReserveTurnCursor>()),
            new EconomicNaturalCapitalPolicy(20UL * U, 40UL * U,
                200_000L, 100_000L, 50_000L),
            new EconomicPrimitiveCommodityMapping(Food, Wood, Stone));

    [Fact]
    public void V8WorldAndEcologyRoundTripWithAllDomainsAndExactCanonicalBytes()
    {
        var generated = Generate();
        var (world, _, _) = World(generated);
        var snapshot = Snapshot(generated, world);
        var bytes = EconomicCompleteV8SaveBundleCodec.Encode(snapshot);

        var loaded = EconomicCompleteV8SaveBundleCodec.Decode(
            generated, bytes);
        Assert.Equal(11UL, loaded.World.Revision);
        Assert.Equal(snapshot.WorldHash, loaded.WorldHash);
        Assert.Equal(snapshot.Sha256Hex, loaded.Sha256Hex);
        Assert.Equal(8U, loaded.WorldHash.FormatVersion);
        Assert.Equal(2, loaded.World.Civilizations.Records.Count);
        Assert.Equal(2, loaded.World.Territory.Controls.Count);
        Assert.Single(loaded.World.Diplomacy.Relations);
        Assert.Equal(2, loaded.World.Economy.EconomicPoints.Count);
        Assert.True(loaded.World.Population.IsInitialized);
        Assert.Equal(2, loaded.World.Population.PointPopulations.Count);
        Assert.Single(loaded.World.Warfare.Units);
        Assert.Equal(EconomicActivityKind.Forestry,
            Assert.Single(loaded.World.Economy.EconomicPoints
                .Single(x => x.Id == B).ActivityAllocations).Kind);
        Assert.Equal(U / 4UL,
            loaded.MaterialReserves.RawAvailable(A, Wood));
        Assert.Equal(U / 8UL,
            loaded.MaterialReserves.RawAvailable(B, Stone));
        Assert.Equal(bytes, EconomicCompleteV8SaveBundleCodec.Encode(loaded));
    }

    [Fact]
    public void UninitializedLegacyCensusRemainsUninitializedRatherThanInventingPeople()
    {
        var generated = Generate();
        var (world, _, _) = World(generated, initializedPopulation: false);
        var bytes = EconomicWorldStateV8SnapshotCodec.Encode(world);
        var reloaded = EconomicWorldStateV8SnapshotCodec.Decode(
            generated, bytes);

        Assert.False(reloaded.Population.IsInitialized);
        Assert.Empty(reloaded.Population.PointPopulations);
        Assert.Equal(7UL, reloaded.Economy.EconomicPoints.Aggregate(
            0UL, (sum, p) => sum + p.Workforce));
        Assert.Equal(new CanonicalWorldStateHasher().Compute(world),
            new CanonicalWorldStateHasher().Compute(reloaded));
        Assert.Equal(bytes, EconomicWorldStateV8SnapshotCodec.Encode(reloaded));
    }

    [Fact]
    public void WrongSeedAndCorruptionAreRejected()
    {
        var generated = Generate();
        var snapshot = Snapshot(generated, World(generated).World);
        var bytes = EconomicCompleteV8SaveBundleCodec.Encode(snapshot);

        Assert.Throws<InvalidOperationException>(() =>
            EconomicCompleteV8SaveBundleCodec.Decode(Generate(1UL), bytes));

        var tampered = (byte[])bytes.Clone();
        tampered[tampered.Length / 2] ^= 0x01;
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV8SaveBundleCodec.Decode(generated, tampered));
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV8SaveBundleCodec.Decode(generated, bytes[..^1]));
    }

    [Fact]
    public void UnknownBundleVersionAndWorldHashV9AreExplicitlyUnsupported()
    {
        var generated = Generate();
        var snapshot = Snapshot(generated, World(generated).World);
        var source = EconomicCompleteV8SaveBundleCodec.Encode(snapshot);

        var bundleVersion = (byte[])source.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(
            bundleVersion.AsSpan(8, 4), 2U);
        Rechecksum(bundleVersion);
        Assert.Throws<NotSupportedException>(() =>
            EconomicCompleteV8SaveBundleCodec.Decode(generated, bundleVersion));

        // Outer envelope: 8 magic + 4 format + 4 world-length + 4 ecology
        // length; inner WorldState codec: 8 magic + 4 format + 4 hash version.
        var v9 = (byte[])source.Clone();
        const int worldHashVersionOffset = 20 + 8 + 4;
        BinaryPrimitives.WriteUInt32BigEndian(
            v9.AsSpan(worldHashVersionOffset, 4), 9U);

        var worldLength = BinaryPrimitives.ReadUInt32BigEndian(
            v9.AsSpan(12, 4));
        var worldBytes = v9.AsSpan(20, checked((int)worldLength));
        var innerDigest = SHA256.HashData(worldBytes[..^32]);
        innerDigest.CopyTo(worldBytes[^32..]);
        Rechecksum(v9);

        Assert.Throws<NotSupportedException>(() =>
            EconomicCompleteV8SaveBundleCodec.Decode(generated, v9));
    }

    [Fact]
    public void RecomputedOuterChecksumCannotHideWorldOrSidecarTampering()
    {
        var generated = Generate();
        var snapshot = Snapshot(generated, World(generated).World);
        var bytes = EconomicCompleteV8SaveBundleCodec.Encode(snapshot);
        var mutated = (byte[])bytes.Clone();
        // Change a byte of the inner ecological record without updating
        // its own checksum or semantic commitment.
        mutated[mutated.Length - 80] ^= 0x01;
        Rechecksum(mutated);

        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV8SaveBundleCodec.Decode(generated, mutated));
    }

    [Fact]
    public void ArchiveIsImmutableAndCanLoadEntireWorldWithoutExternalWorldObject()
    {
        using var dir = new TempFolder();
        var generated = Generate();
        var snapshot = Snapshot(generated, World(generated).World);
        var archive = new EconomicCompleteV8SaveBundleArchive(dir.PathName);
        archive.Store(snapshot);
        archive.Store(snapshot);

        var restored = archive.Load(generated, snapshot.Sha256Hex);
        Assert.Equal(snapshot.WorldHash, restored.WorldHash);
        Assert.Equal(snapshot.Sha256Hex, restored.Sha256Hex);
        Assert.Equal(snapshot.World.Revision, restored.World.Revision);

        Assert.Throws<ArgumentException>(() =>
            archive.BundlePath("../../escape"));
        var path = archive.BundlePath(snapshot.Sha256Hex);
        var data = File.ReadAllBytes(path);
        data[data.Length / 2] ^= 0x01;
        File.WriteAllBytes(path, data);
        Assert.Throws<InvalidDataException>(() => archive.Store(snapshot));
        Assert.Throws<InvalidDataException>(() =>
            archive.Load(generated, snapshot.Sha256Hex));
    }

    [Fact]
    public void AValidEarlierCompleteSaveNeedsItsOwnTrustedAcceptedDigest()
    {
        using var dir = new TempFolder();
        var generated = Generate();
        var snapshot = Snapshot(generated, World(generated).World);
        var archive = new EconomicCompleteV8SaveBundleArchive(dir.PathName);
        archive.Store(snapshot);

        var changedWorld = snapshot.World.AdvanceRevision();
        var newer = Snapshot(generated, changedWorld);
        archive.Store(newer);
        Assert.NotEqual(snapshot.Sha256Hex, newer.Sha256Hex);

        Assert.Equal(newer.Sha256Hex,
            archive.Load(generated, newer.Sha256Hex).Sha256Hex);
        // The archive intentionally cannot determine which is latest; the
        // caller's independent trusted digest selects the accepted checkpoint.
        Assert.Equal(snapshot.Sha256Hex,
            archive.Load(generated, snapshot.Sha256Hex).Sha256Hex);
    }

    private static void Rechecksum(byte[] bytes) =>
        SHA256.HashData(bytes.AsSpan(0, bytes.Length - 32))
            .CopyTo(bytes, bytes.Length - 32);
}
