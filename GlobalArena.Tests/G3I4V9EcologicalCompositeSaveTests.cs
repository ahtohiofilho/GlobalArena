using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-I4: complete version-9 world state (including fine transport
/// facilities) plus independently versioned natural capital and material
/// reserves, tied to the SAME v9 state hash.
/// </summary>
public sealed class G3I4V9EcologicalCompositeSaveTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId Site = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(), "ga-g3i4-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(seed),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static WorldState World(WorldGenerationResult generated)
    {
        var natural = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            natural.WoodyVegetation.GetRawValue(id) > 0L &&
            natural.ExposedStone.GetRawValue(id) > 0L);

        return WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithTerritory(new StrategicTerritoryRuntimeState(new[]
            {
                new StrategicTerritoryControlEntry(cell, Owner)
            }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(Site, Owner, cell, 4UL, new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Forestry, 4UL)
                })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(Site, 4UL)
            }))
            .WithTransportFacilities(new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(42UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0),
                        new StrategicCellId(7UL)),
                    true, new[]
                    {
                        new TransportFacilityTransferCapability(1UL, 2UL, 1UL),
                        new TransportFacilityTransferCapability(3UL, 4UL, 2UL)
                    })
            })).WithRestoredRevision(5UL);
    }

    private static EconomicNaturalCapitalPolicy Policy() =>
        new(100UL * D, 100UL * D, 0L, 0L, 0L);

    private static EconomicEcologicalCheckpoint Snapshot(
        WorldGenerationResult generated, WorldState world) =>
        new(generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, Policy()),
            EconomicMaterialReserveBook.Empty, Policy(), Mapping);

    private static EconomicEcologicalCheckpoint Next(
        WorldGenerationResult generated, EconomicEcologicalCheckpoint prior,
        ulong turn) =>
        new EconomicEcologicalTurnCommitResolver().Propose(
            generated, prior, prior.Sha256Hex, turn,
            new[] { new EconomicDemandProfile(Site, Food, 500_000L) },
            new[]
            {
                new EconomicMaterialOriginHoldOrder(
                    Site, Wood, EconomicActivityKind.Forestry, D / 100UL)
            }).ProposedNext;

    [Fact]
    public void CompleteV9RoundTripRetainsFacilitiesWorkforceNaturalCapitalAndReserves()
    {
        var generated = Generate();
        var world = World(generated);
        var saved = Next(generated, Snapshot(generated, world), 1UL);
        var bytes = EconomicCompleteV9SaveBundleCodec.Encode(saved);
        var decoded = EconomicCompleteV9SaveBundleCodec.Decode(generated, bytes);

        Assert.Equal(9U, saved.WorldHash.FormatVersion);
        Assert.Equal(saved.WorldHash, decoded.WorldHash);
        Assert.Equal(saved.Sha256Hex, decoded.Sha256Hex);
        Assert.Equal(5UL, decoded.World.Revision);
        Assert.Equal(4UL, Assert.Single(decoded.World.Economy.EconomicPoints).Workforce);
        Assert.Equal(4UL, Assert.Single(decoded.World.Population.PointPopulations).Residents);
        Assert.Equal(1UL, decoded.NaturalCapital.LastAppliedTurn);
        Assert.Equal(D / 100UL, decoded.MaterialReserves.RawAvailable(Site, Wood));
        Assert.Equal(1UL, decoded.MaterialReserves.LastAppliedTurn(Owner));

        var facility = Assert.Single(decoded.World.TransportFacilities.Facilities);
        Assert.Equal(42UL, facility.FacilityId);
        Assert.True(facility.IsOperational);
        Assert.Equal(3, facility.Tile.FineGoldbergParameters.M);
        Assert.Equal(7UL, facility.Tile.FineStrategicCellId.Value);
        Assert.Equal(2, facility.Transfers.Count);
        Assert.True(facility.CanTransfer(1UL, 2UL, 1UL));
        Assert.False(facility.CanTransfer(2UL, 1UL, 1UL));

        Assert.Equal(bytes, EconomicCompleteV9SaveBundleCodec.Encode(decoded));
    }

    [Fact]
    public void FacilityChangesInvalidateTheEcologicalCommitmentForTheSameNaturalBook()
    {
        var generated = Generate();
        var world = World(generated);
        var snapshot = Snapshot(generated, world);
        var facility = Assert.Single(world.TransportFacilities.Facilities);
        var changed = world.WithTransportFacilities(
            new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(facility.FacilityId,
                    facility.Tile, false, facility.Transfers)
            }));
        var different = new EconomicEcologicalCheckpoint(
            generated, changed, snapshot.NaturalCapital,
            snapshot.MaterialReserves, snapshot.Policy, snapshot.Commodities);

        Assert.NotEqual(snapshot.WorldHash, different.WorldHash);
        Assert.NotEqual(snapshot.Sha256Hex, different.Sha256Hex);
        Assert.Throws<InvalidOperationException>(() =>
            EconomicEcologicalSidecarCodec.Decode(generated, changed,
                EconomicEcologicalSidecarCodec.Encode(snapshot)));
    }

    [Fact]
    public void V9BundleRejectsWrongWorldAndTamperingEvenAfterRecomputedOuterChecksum()
    {
        var generated = Generate();
        var bytes = EconomicCompleteV9SaveBundleCodec.Encode(
            Snapshot(generated, World(generated)));

        Assert.Throws<InvalidOperationException>(() =>
            EconomicCompleteV9SaveBundleCodec.Decode(Generate(1UL), bytes));
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV9SaveBundleCodec.Decode(generated, bytes[..^1]));

        var bad = (byte[])bytes.Clone();
        bad[bad.Length - 80] ^= 0x01; // modify ecological section
        SHA256.HashData(bad.AsSpan(0, bad.Length - 32))
            .CopyTo(bad, bad.Length - 32); // cover up the envelope checksum
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV9SaveBundleCodec.Decode(generated, bad));

        var wrongFormat = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(wrongFormat.AsSpan(8, 4), 2U);
        SHA256.HashData(wrongFormat.AsSpan(0, wrongFormat.Length - 32))
            .CopyTo(wrongFormat, wrongFormat.Length - 32);
        Assert.Throws<NotSupportedException>(() =>
            EconomicCompleteV9SaveBundleCodec.Decode(generated, wrongFormat));
    }

    [Fact]
    public void ArchivedV9BundleIsImmutableAndRequiresExplicitlyAcceptedDigest()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var original = Snapshot(generated, World(generated));
        var later = Next(generated, original, 1UL);
        var archive = new EconomicCompleteV9SaveBundleArchive(dir.Root);
        archive.Store(original);
        archive.Store(later);
        archive.Store(later);

        Assert.Equal(original.Sha256Hex,
            archive.Load(generated, original.Sha256Hex).Sha256Hex);
        Assert.Equal(later.Sha256Hex,
            archive.Load(generated, later.Sha256Hex).Sha256Hex);
        Assert.NotEqual(original.Sha256Hex, later.Sha256Hex);

        var target = archive.BundlePath(later.Sha256Hex);
        var changed = File.ReadAllBytes(target);
        changed[changed.Length / 2] ^= 0x01;
        File.WriteAllBytes(target, changed);
        Assert.Throws<InvalidDataException>(() => archive.Store(later));
        Assert.Throws<InvalidDataException>(() =>
            archive.Load(generated, later.Sha256Hex));
    }

    [Fact]
    public void ReplayedEcologicalTurnStillProducesIdenticalV9Bundle()
    {
        var generated = Generate();
        var original = Snapshot(generated, World(generated));
        var saved = Next(generated, original, 1UL);

        var initialBytes = EconomicEcologicalSidecarCodec.Encode(original);
        var steps = new[]
        {
            new EconomicEcologicalReplayStep(1UL, saved.Sha256Hex,
                new[] { new EconomicDemandProfile(Site, Food, 500_000L) },
                new[]
                {
                    new EconomicMaterialOriginHoldOrder(
                        Site, Wood, EconomicActivityKind.Forestry, D / 100UL)
                })
        };
        var replayed = new EconomicEcologicalSidecarReplayVerifier().Replay(
            generated, original.World, initialBytes, steps);

        Assert.Equal(saved.Sha256Hex, replayed.Sha256Hex);
        Assert.Equal(EconomicCompleteV9SaveBundleCodec.Encode(saved),
            EconomicCompleteV9SaveBundleCodec.Encode(replayed));
    }
}
