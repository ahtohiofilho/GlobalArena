using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-I: real local-disk ecological sidecar persistence with an immutable
/// blob first and a separately accepted single-writer head second.
/// This is NOT a full WorldState or networked multiplayer persistence layer.
/// </summary>
public sealed class G3IDurableEcologicalCheckpointTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly EconomicPrimitiveCommodityMapping Mapping =
        new(Food, Wood, Stone);

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Location { get; } = Path.Combine(
            Path.GetTempPath(), "ga-g3i-" + Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Location);

        public void Dispose()
        {
            if (Directory.Exists(Location))
                Directory.Delete(Location, recursive: true);
        }
    }

    private static WorldGenerationResult Generate(ulong seed = 0UL) =>
        new DeterministicWorldGenerator().Generate(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(2, 0)));

    private static WorldState World(WorldGenerationResult generated)
    {
        var endowments = StrategicPrimitiveNaturalEndowmentFieldSet.Derive(generated);
        var cell = generated.StrategicTopology.Cells.Select(x => x.Id).First(id =>
            endowments.WoodyVegetation.GetRawValue(id) > 0L &&
            endowments.ExposedStone.GetRawValue(id) > 0L);

        return WorldState.CreateBound(generated)
            .WithCivilizations(new CivilizationRuntimeState(new[] { Owner }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, Owner, cell, 4UL, new[]
                {
                    new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Forestry, 4UL)
                })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 4UL)
            }));
    }

    private static EconomicNaturalCapitalPolicy Policy() =>
        new(100UL * U, 100UL * U, 0L, 0L, 0L);

    private static EconomicEcologicalCheckpoint Initial(
        WorldGenerationResult generated, WorldState world) =>
        new(generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, Policy()),
            EconomicMaterialReserveBook.Empty, Policy(), Mapping);

    private static EconomicDemandProfile[] Demand() =>
        new[] { new EconomicDemandProfile(A, Food, 500_000L) };

    private static EconomicMaterialOriginHoldOrder[] Holds() =>
        new[] { new EconomicMaterialOriginHoldOrder(
            A, Wood, EconomicActivityKind.Forestry, U / 100UL) };

    private static EconomicEcologicalTurnCommitResult Commit(
        EconomicEcologicalDurableCheckpointHost host, ulong turn,
        bool hold = true) =>
        host.CommitNext(host.AcceptedSha256Hex, turn, Demand(),
            hold ? Holds() : Array.Empty<EconomicMaterialOriginHoldOrder>());

    [Fact]
    public void ArchiveIsContentAddressedIdempotentAndImmutable()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        var archive = new EconomicEcologicalSidecarArchive(dir.Location);

        archive.Store(initial);
        var path = archive.CheckpointPath(initial.Sha256Hex);
        Assert.True(File.Exists(path));
        var first = File.ReadAllBytes(path);
        archive.Store(initial);
        Assert.Equal(first, File.ReadAllBytes(path));

        var decoded = archive.Load(generated, world, initial.Sha256Hex);
        Assert.Equal(initial.Sha256Hex, decoded.Sha256Hex);
        Assert.Equal(first, EconomicEcologicalSidecarCodec.Encode(decoded));
        Assert.Throws<ArgumentException>(() => archive.CheckpointPath("../escape"));
        Assert.Throws<ArgumentException>(() =>
            new EconomicEcologicalSidecarArchive("relative/sidecar"));

        var damaged = (byte[])first.Clone();
        damaged[damaged.Length / 2] ^= 0x01;
        File.WriteAllBytes(path, damaged);
        Assert.Throws<InvalidDataException>(() => archive.Store(initial));
        Assert.Throws<InvalidDataException>(() =>
            archive.Load(generated, world, initial.Sha256Hex));
    }

    [Fact]
    public void DurableHeadAdvancesOnlyAfterCompleteBlobAndSurvivesRestart()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        var original = initial.Sha256Hex;
        string committed;
        using (var host = EconomicEcologicalDurableCheckpointHost.Create(
            dir.Location, generated, initial))
        {
            var result = Commit(host, 1UL);
            committed = result.ProposedNext.Sha256Hex;
            Assert.NotEqual(original, committed);
            Assert.Equal(committed, host.AcceptedSha256Hex);
            Assert.Equal(1UL, host.LastAppliedTurn);
            Assert.True(File.Exists(new EconomicEcologicalSidecarArchive(
                dir.Location).CheckpointPath(committed)));
            Assert.True(File.Exists(new EconomicEcologicalSidecarArchive(
                dir.Location).CheckpointPath(original)));
        }

        using (var restored = EconomicEcologicalDurableCheckpointHost.Restore(
            dir.Location, generated, world, committed))
        {
            Assert.Equal(committed, restored.AcceptedSha256Hex);
            Assert.Equal(1UL, restored.LastAppliedTurn);
            Assert.Equal(U / 100UL,
                EconomicEcologicalSidecarCodec.Decode(
                    generated, world, restored.CaptureSidecar())
                    .MaterialReserves.RawAvailable(A, Wood));
            var next = Commit(restored, 2UL, hold: false);
            Assert.Equal(2UL, restored.LastAppliedTurn);
            Assert.NotEqual(committed, next.ProposedNext.Sha256Hex);
        }
    }

    [Fact]
    public void WrittenButUnacceptedBlobIsAnOrphanAndCannotAdvanceTheHead()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        var archive = new EconomicEcologicalSidecarArchive(dir.Location);

        using (var host = EconomicEcologicalDurableCheckpointHost.Create(
            dir.Location, generated, initial))
        {
            var projection = new EconomicEcologicalTurnCommitResolver().Propose(
                generated, initial, initial.Sha256Hex, 1UL, Demand(), Holds());
            archive.Store(projection.ProposedNext);
            Assert.True(File.Exists(archive.CheckpointPath(
                projection.ProposedNext.Sha256Hex)));
            Assert.Equal(initial.Sha256Hex, host.AcceptedSha256Hex);
            Assert.Equal(0UL, host.LastAppliedTurn);
        }

        using var recovered = EconomicEcologicalDurableCheckpointHost.Restore(
            dir.Location, generated, world, initial.Sha256Hex);
        Assert.Equal(0UL, recovered.LastAppliedTurn);
        var resumed = Commit(recovered, 1UL);
        Assert.Equal(1UL, recovered.LastAppliedTurn);
        Assert.True(File.Exists(archive.CheckpointPath(
            resumed.ProposedNext.Sha256Hex)));
    }

    [Fact]
    public void RolledBackHeadAndSidecarCannotPassExternallyTrustedDigest()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        var headPath = Path.Combine(dir.Location, "ecological-head.gahead");
        byte[] oldHead;
        string latest;

        using (var host = EconomicEcologicalDurableCheckpointHost.Create(
            dir.Location, generated, initial))
        {
            oldHead = File.ReadAllBytes(headPath);
            latest = Commit(host, 1UL).ProposedNext.Sha256Hex;
        }

        // A storage operator can replace a local head with a valid older
        // version. Only the independently trusted digest blocks rollback.
        File.WriteAllBytes(headPath, oldHead);
        Assert.Throws<InvalidOperationException>(() =>
            EconomicEcologicalDurableCheckpointHost.Restore(
                dir.Location, generated, world, latest));
        using var old = EconomicEcologicalDurableCheckpointHost.Restore(
            dir.Location, generated, world, initial.Sha256Hex);
        Assert.Equal(0UL, old.LastAppliedTurn);
    }

    [Fact]
    public void ChangedWorldAndDamagedHeadAreRejected()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        using (EconomicEcologicalDurableCheckpointHost.Create(
            dir.Location, generated, initial)) { }

        Assert.Throws<InvalidOperationException>(() =>
            EconomicEcologicalDurableCheckpointHost.Restore(
                dir.Location, generated, world.AdvanceRevision(),
                initial.Sha256Hex));

        var head = Path.Combine(dir.Location, "ecological-head.gahead");
        var bytes = File.ReadAllBytes(head);
        bytes[bytes.Length / 2] ^= 0x01;
        File.WriteAllBytes(head, bytes);
        Assert.Throws<InvalidDataException>(() =>
            EconomicEcologicalDurableCheckpointHost.Restore(
                dir.Location, generated, world, initial.Sha256Hex));
    }

    [Fact]
    public void InvalidTurnDoesNotPublishAHeadOrChangeAcceptedCheckpoint()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        using var host = EconomicEcologicalDurableCheckpointHost.Create(
            dir.Location, generated, initial);

        var beforeHead = File.ReadAllBytes(
            Path.Combine(dir.Location, "ecological-head.gahead"));
        var beforeFiles = Directory.GetFiles(dir.Location, "*.gaeco").Length;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            host.CommitNext(initial.Sha256Hex, 1UL, Demand(), new[]
            {
                new EconomicMaterialOriginHoldOrder(
                    A, Wood, EconomicActivityKind.Forestry, ulong.MaxValue)
            }));

        Assert.Equal(initial.Sha256Hex, host.AcceptedSha256Hex);
        Assert.Equal(0UL, host.LastAppliedTurn);
        Assert.Equal(beforeHead, File.ReadAllBytes(
            Path.Combine(dir.Location, "ecological-head.gahead")));
        Assert.Equal(beforeFiles, Directory.GetFiles(dir.Location, "*.gaeco").Length);
    }

    [Fact]
    public void SecondWriterAndStaleExpectedDigestAreRejected()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        using var first = EconomicEcologicalDurableCheckpointHost.Create(
            dir.Location, generated, initial);

        Assert.Throws<IOException>(() =>
            EconomicEcologicalDurableCheckpointHost.Restore(
                dir.Location, generated, world, initial.Sha256Hex));

        Commit(first, 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            first.CommitNext(initial.Sha256Hex, 2UL, Demand(),
                Array.Empty<EconomicMaterialOriginHoldOrder>()));
        Assert.Equal(1UL, first.LastAppliedTurn);
    }

    [Fact]
    public void RejectedMissingBlobLeavesHeadIntactAndCannotRestore()
    {
        using var dir = new TemporaryDirectory();
        var generated = Generate();
        var world = World(generated);
        var initial = Initial(generated, world);
        var path = new EconomicEcologicalSidecarArchive(dir.Location)
            .CheckpointPath(initial.Sha256Hex);

        using (EconomicEcologicalDurableCheckpointHost.Create(
            dir.Location, generated, initial)) { }
        File.Delete(path);

        Assert.Throws<FileNotFoundException>(() =>
            EconomicEcologicalDurableCheckpointHost.Restore(
                dir.Location, generated, world, initial.Sha256Hex));
        Assert.True(File.Exists(Path.Combine(
            dir.Location, "ecological-head.gahead")));
    }
}
