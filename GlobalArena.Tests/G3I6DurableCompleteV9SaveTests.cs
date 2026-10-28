using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-I6: one disk head publishes the whole v9 world, facilities, ecological
/// stock and intentionally held materials. Simulates crash boundaries by
/// prewriting unaccepted blobs and corrupting/missing accepted records.
/// </summary>
public sealed class G3I6DurableCompleteV9SaveTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId Site = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly EconomicPrimitiveCommodityMapping Commodities =
        new(Food, Wood, Stone);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(), "ga-g3i6-" + Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(2, 0)));

    private static WorldState State(WorldGenerationResult generated,
        bool withFacility = true)
    {
        var fields = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            fields.WoodyVegetation.GetRawValue(id) > 0L &&
            fields.ExposedStone.GetRawValue(id) > 0L);
        var world = WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
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
            .WithRestoredRevision(7UL);

        return withFacility
            ? world.WithTransportFacilities(new TransportFacilitiesRuntimeState(new[]
            {
                new TransportFacilityRuntimeRecord(42UL,
                    new PhysicalTacticalTileId(
                        new GoldbergParameters(3, 0), new StrategicCellId(7UL)),
                    true, new[]
                    {
                        new TransportFacilityTransferCapability(1UL, 2UL, 1UL)
                    })
            }))
            : world;
    }

    private static EconomicNaturalCapitalPolicy Policy() =>
        new(80UL * D, 80UL * D, 0L, 0L, 0L);

    private static EconomicEcologicalCheckpoint Initial(
        WorldGenerationResult generated, bool facilities = true)
    {
        var policy = Policy();
        return new EconomicEcologicalCheckpoint(
            generated, State(generated, facilities),
            EconomicNaturalCapitalInitializer.Initialize(generated, policy),
            EconomicMaterialReserveBook.Empty, policy, Commodities);
    }

    private static EconomicDemandProfile[] Demand() =>
        new[] { new EconomicDemandProfile(Site, Food, 500_000L) };

    private static EconomicMaterialOriginHoldOrder[] Holds() =>
        new[] { new EconomicMaterialOriginHoldOrder(
            Site, Wood, EconomicActivityKind.Forestry, D / 100UL) };

    private static EconomicEcologicalTurnCommitResult Commit(
        EconomicCompleteV9DurableSaveHost host, ulong turn,
        bool hold = true) =>
        host.CommitNext(host.AcceptedSha256Hex, turn, Demand(),
            hold ? Holds() : Array.Empty<EconomicMaterialOriginHoldOrder>());

    [Fact]
    public void CompleteWorldAndEcologyPublishAsOneHeadAndResumeAfterRestart()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var initial = Initial(generated);
        string committed;
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial))
        {
            Assert.Equal(0UL, host.CommitSequence);
            Assert.Equal(initial.Sha256Hex, host.AcceptedSha256Hex);
            Assert.False(host.HasLegacyMigration);
            var result = Commit(host, 1UL);
            committed = result.ProposedNext.Sha256Hex;
            Assert.Equal(1UL, host.LastAppliedTurn);
            Assert.Equal(1UL, host.CommitSequence);
            Assert.Equal(committed, host.AcceptedSha256Hex);
            Assert.NotEqual(initial.Sha256Hex, committed);
            var decoded = EconomicCompleteV9SaveBundleCodec.Decode(
                generated, host.CaptureCompleteSave());
            Assert.Equal(D / 100UL, decoded.MaterialReserves.RawAvailable(Site, Wood));
            var facility = Assert.Single(decoded.World.TransportFacilities.Facilities);
            Assert.Equal(42UL, facility.FacilityId);
            Assert.True(facility.CanTransfer(1UL, 2UL, 1UL));
        }

        using (var host = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, committed))
        {
            Assert.Equal(1UL, host.CommitSequence);
            Assert.Equal(1UL, host.LastAppliedTurn);
            var next = Commit(host, 2UL, hold: false);
            Assert.Equal(2UL, host.CommitSequence);
            Assert.Equal(2UL, host.LastAppliedTurn);
            Assert.NotEqual(committed, next.ProposedNext.Sha256Hex);
        }
    }

    [Fact]
    public void UnacceptedCompleteBundleCannotAdvanceTheDurableHead()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var initial = Initial(generated);
        var archive = new EconomicCompleteV9SaveBundleArchive(dir.Root);
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial))
        {
            var planned = new EconomicEcologicalTurnCommitResolver().Propose(
                generated, initial, initial.Sha256Hex, 1UL, Demand(), Holds());
            archive.Store(planned.ProposedNext); // crash before head replacement
            Assert.True(File.Exists(archive.BundlePath(
                planned.ProposedNext.Sha256Hex)));
            Assert.Equal(initial.Sha256Hex, host.AcceptedSha256Hex);
            Assert.Equal(0UL, host.LastAppliedTurn);
        }
        using var reopened = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, initial.Sha256Hex);
        Assert.Equal(0UL, reopened.CommitSequence);
        Assert.Equal(0UL, reopened.LastAppliedTurn);
        Commit(reopened, 1UL);
        Assert.Equal(1UL, reopened.LastAppliedTurn);
    }

    [Fact]
    public void StaleDigestAndSecondWriterAreRejectedWithoutChangingHead()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var initial = Initial(generated);
        using var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial);
        Assert.Throws<IOException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(
                dir.Root, generated, initial.Sha256Hex));
        var first = Commit(host, 1UL);
        var previous = initial.Sha256Hex;
        Assert.Throws<InvalidOperationException>(() =>
            host.CommitNext(previous, 2UL, Demand(),
                Array.Empty<EconomicMaterialOriginHoldOrder>()));
        Assert.Equal(first.ProposedNext.Sha256Hex, host.AcceptedSha256Hex);
        Assert.Equal(1UL, host.CommitSequence);
    }

    [Fact]
    public void InvalidTurnAndCorruptAlreadyWrittenCandidateDoNotPublishHead()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var initial = Initial(generated);
        using var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial);
        var path = Path.Combine(dir.Root, "full-v9-head.gahead");
        var headBytes = File.ReadAllBytes(path);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            host.CommitNext(initial.Sha256Hex, 1UL, Demand(), new[]
            {
                new EconomicMaterialOriginHoldOrder(
                    Site, Wood, EconomicActivityKind.Forestry, ulong.MaxValue)
            }));
        Assert.Equal(initial.Sha256Hex, host.AcceptedSha256Hex);
        Assert.Equal(headBytes, File.ReadAllBytes(path));

        var projection = new EconomicEcologicalTurnCommitResolver().Propose(
            generated, initial, initial.Sha256Hex, 1UL, Demand(), Holds());
        var archive = new EconomicCompleteV9SaveBundleArchive(dir.Root);
        archive.Store(projection.ProposedNext);
        var candidate = archive.BundlePath(projection.ProposedNext.Sha256Hex);
        var corrupt = File.ReadAllBytes(candidate);
        corrupt[corrupt.Length / 2] ^= 0x01;
        File.WriteAllBytes(candidate, corrupt);

        Assert.Throws<InvalidDataException>(() => Commit(host, 1UL));
        Assert.Equal(initial.Sha256Hex, host.AcceptedSha256Hex);
        Assert.Equal(headBytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void EarlierValidHeadAndBundleCannotPassLaterTrustedDigest()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var initial = Initial(generated);
        var path = Path.Combine(dir.Root, "full-v9-head.gahead");
        string latest;
        byte[] earlier;
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial))
        {
            earlier = File.ReadAllBytes(path);
            latest = Commit(host, 1UL).ProposedNext.Sha256Hex;
        }
        File.WriteAllBytes(path, earlier);
        Assert.Throws<InvalidOperationException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(dir.Root, generated, latest));
        using var historic = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, initial.Sha256Hex);
        Assert.Equal(0UL, historic.LastAppliedTurn);
    }

    [Fact]
    public void DamagedHeadAndMissingAcceptedOrPredecessorBundlesFailClosed()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var initial = Initial(generated);
        var archive = new EconomicCompleteV9SaveBundleArchive(dir.Root);
        string committed;
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, initial))
            committed = Commit(host, 1UL).ProposedNext.Sha256Hex;

        var headPath = Path.Combine(dir.Root, "full-v9-head.gahead");
        var head = File.ReadAllBytes(headPath);
        var damaged = (byte[])head.Clone();
        damaged[damaged.Length / 2] ^= 0x01;
        File.WriteAllBytes(headPath, damaged);
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(
                dir.Root, generated, committed));

        File.WriteAllBytes(headPath, head);
        File.Delete(archive.BundlePath(initial.Sha256Hex));
        Assert.Throws<FileNotFoundException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(
                dir.Root, generated, committed));

        // Restore previous, but remove the accepted complete bundle.
        archive.Store(initial);
        File.Delete(archive.BundlePath(committed));
        Assert.Throws<FileNotFoundException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(
                dir.Root, generated, committed));
    }

    [Fact]
    public void MigratedGenesisRequiresBothHistoricalTrustAnchorsOnEveryRestore()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var first = Initial(generated, facilities: false);
        var legacy = new EconomicLegacyV8EcologicalState(
            generated, first.World, first.NaturalCapital,
            first.MaterialReserves, first.Policy, first.Commodities);
        var bytes = EconomicLegacyV8CompleteSaveCodec.Encode(legacy);
        string acceptedV9;
        using (var host = EconomicCompleteV9DurableSaveHost.CreateFromLegacy(
            dir.Root, generated, bytes, legacy.WorldHash, legacy.Sha256Hex))
        {
            acceptedV9 = host.AcceptedSha256Hex;
            Assert.True(host.HasLegacyMigration);
            Assert.Equal(0UL, host.CommitSequence);
            Assert.Equal(0UL, host.LastAppliedTurn);
            Assert.NotEqual(legacy.Sha256Hex, acceptedV9);
            Assert.Empty(EconomicCompleteV9SaveBundleCodec.Decode(
                generated, host.CaptureCompleteSave())
                .World.TransportFacilities.Facilities);
            Commit(host, 1UL);
            acceptedV9 = host.AcceptedSha256Hex;
        }

        Assert.Throws<InvalidOperationException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(
                dir.Root, generated, acceptedV9));
        Assert.Throws<InvalidOperationException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(
                dir.Root, generated, acceptedV9, legacy.WorldHash,
                new string('A', 64)));
        using var recovered = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, acceptedV9, legacy.WorldHash, legacy.Sha256Hex);
        Assert.True(recovered.HasLegacyMigration);
        Assert.Equal(1UL, recovered.LastAppliedTurn);
        Assert.Equal(1UL, recovered.CommitSequence);
    }

    [Fact]
    public void NewGenesisRejectsPriorTurnAndNewMigrationRequiresTrustedV8()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var initial = Initial(generated);
        var advanced = new EconomicEcologicalTurnCommitResolver().Propose(
            generated, initial, initial.Sha256Hex, 1UL,
            Demand(), Holds()).ProposedNext;
        Assert.Throws<InvalidOperationException>(() =>
            EconomicCompleteV9DurableSaveHost.Create(dir.Root, generated, advanced));

        var rawLegacy = Initial(generated, facilities: false);
        var old = new EconomicLegacyV8EcologicalState(
            generated, rawLegacy.World, rawLegacy.NaturalCapital,
            rawLegacy.MaterialReserves, rawLegacy.Policy, rawLegacy.Commodities);
        var bytes = EconomicLegacyV8CompleteSaveCodec.Encode(old);
        Assert.Throws<InvalidOperationException>(() =>
            EconomicCompleteV9DurableSaveHost.CreateFromLegacy(
                dir.Root, generated, bytes,
                new WorldStateHash(8U, new string('F', 64)), old.Sha256Hex));
        Assert.False(File.Exists(Path.Combine(dir.Root, "full-v9-head.gahead")));
    }
}
