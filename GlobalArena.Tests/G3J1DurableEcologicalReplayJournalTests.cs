using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-J1: complete-v9 reference ecological inputs are journaled BEFORE
/// head publication; the head anchors a complete immutable journal chain.
/// </summary>
public sealed class G3J1DurableEcologicalReplayJournalTests
{
    private const ulong D = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CivilizationId Owner = new(1UL);
    private static readonly EconomicPointId Site = new(1UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Wood = new(3U);
    private static readonly CommodityId Stone = new(2U);

    private sealed class Folder : IDisposable
    {
        public string Root { get; } = Path.Combine(
            Path.GetTempPath(), "ga-g3j1-" + Guid.NewGuid().ToString("N"));
        public Folder() => Directory.CreateDirectory(Root);
        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private static WorldGenerationResult Generate() =>
        new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(0UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(2, 0)));

    private static EconomicEcologicalCheckpoint Genesis(WorldGenerationResult generated)
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
            }));
        var policy = new EconomicNaturalCapitalPolicy(
            100UL * D, 100UL * D, 0L, 0L, 0L);
        return new EconomicEcologicalCheckpoint(
            generated, world,
            EconomicNaturalCapitalInitializer.Initialize(generated, policy),
            EconomicMaterialReserveBook.Empty, policy,
            new EconomicPrimitiveCommodityMapping(Food, Wood, Stone));
    }

    private static EconomicDemandProfile[] Demand(long rate = 500_000L) =>
        new[] { new EconomicDemandProfile(Site, Food, rate) };

    private static EconomicMaterialOriginHoldOrder[] Holds() =>
        new[] { new EconomicMaterialOriginHoldOrder(
            Site, Wood, EconomicActivityKind.Forestry, D / 100UL) };

    private static EconomicEcologicalTurnCommitResult Commit(
        EconomicCompleteV9DurableSaveHost host, ulong ordinal) =>
        host.CommitNext(host.AcceptedSha256Hex, ordinal, Demand(), Holds());

    [Fact]
    public void JournalRoundTripAndMultipleCommittedTurnsReplayAfterRestart()
    {
        using var dir = new Folder();
        var generated = Generate();
        var genesis = Genesis(generated);
        var journal = new EconomicEcologicalTransitionJournalArchive(dir.Root);
        var archive = new EconomicCompleteV9SaveBundleArchive(dir.Root);
        string latest;
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, genesis))
        {
            var first = Commit(host, 1UL).ProposedNext;
            var second = Commit(host, 2UL).ProposedNext;
            latest = second.Sha256Hex;
            var entry = journal.Load(latest);
            Assert.Equal(2UL, entry.TurnOrdinal);
            Assert.Equal(first.Sha256Hex, entry.PreviousCheckpointSha256Hex);
            Assert.Single(entry.WorkerDemand);
            Assert.Single(entry.OriginHolds);
            Assert.Equal(500_000L, entry.WorkerDemand[0].RawDemandRatePerWorker);
            var entryBytes = EconomicEcologicalTransitionJournalCodec.Encode(entry);
            Assert.Equal(entryBytes, EconomicEcologicalTransitionJournalCodec.Encode(
                EconomicEcologicalTransitionJournalCodec.Decode(entryBytes)));
            new EconomicCompleteV9JournalReplayVerifier().VerifyChain(
                generated, archive, journal, latest, 2UL,
                CurrentJournalDigest(dir.Root));
        }

        using var restored = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, latest);
        Assert.Equal(2UL, restored.LastAppliedTurn);
        Assert.Equal(2UL, restored.CommitSequence);
        Commit(restored, 3UL);
        Assert.Equal(3UL, restored.CommitSequence);
    }

    [Fact]
    public void SelfConsistentAlteredDemandIsRejectedByHeadAnchoredJournalHash()
    {
        using var dir = new Folder();
        var generated = Generate();
        var genesis = Genesis(generated);
        string latest;
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, genesis))
            latest = Commit(host, 1UL).ProposedNext.Sha256Hex;

        var journal = new EconomicEcologicalTransitionJournalArchive(dir.Root);
        var original = journal.Load(latest);
        // Different worker demand can leave ecological stocks unchanged;
        // checking ONLY the resulting checkpoint hash would miss this.
        var forged = new EconomicEcologicalTransitionJournalRecord(
            original.TurnOrdinal, original.PreviousCheckpointSha256Hex,
            original.ResultCheckpointSha256Hex,
            Demand(rate: 250_000L), original.OriginHolds,
            original.PreviousJournalSha256Hex);
        File.WriteAllBytes(journal.RecordPath(latest),
            EconomicEcologicalTransitionJournalCodec.Encode(forged));
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(
                dir.Root, generated, latest));
    }

    [Fact]
    public void MissingOrCorruptJournalNeverRestoresAcceptedTurn()
    {
        using var dir = new Folder();
        var generated = Generate();
        var genesis = Genesis(generated);
        string latest;
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, genesis))
            latest = Commit(host, 1UL).ProposedNext.Sha256Hex;

        var archive = new EconomicEcologicalTransitionJournalArchive(dir.Root);
        var target = archive.RecordPath(latest);
        var original = File.ReadAllBytes(target);
        var damaged = (byte[])original.Clone();
        damaged[damaged.Length / 2] ^= 1;
        File.WriteAllBytes(target, damaged);
        Assert.Throws<InvalidDataException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(dir.Root, generated, latest));

        File.Delete(target);
        Assert.Throws<FileNotFoundException>(() =>
            EconomicCompleteV9DurableSaveHost.Restore(dir.Root, generated, latest));

        File.WriteAllBytes(target, original);
        using var restored = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, latest);
        Assert.Equal(latest, restored.AcceptedSha256Hex);
    }

    [Fact]
    public void OrphanJournalAndBundleCannotAdvanceTheAcceptedHead()
    {
        using var dir = new Folder();
        var generated = Generate();
        var genesis = Genesis(generated);
        var archive = new EconomicCompleteV9SaveBundleArchive(dir.Root);
        var journal = new EconomicEcologicalTransitionJournalArchive(dir.Root);
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, genesis))
        {
            var predicted = new EconomicEcologicalTurnCommitResolver().Propose(
                generated, genesis, genesis.Sha256Hex,
                1UL, Demand(), Holds()).ProposedNext;
            archive.Store(predicted);
            journal.Store(new EconomicEcologicalTransitionJournalRecord(
                1UL, genesis.Sha256Hex, predicted.Sha256Hex, Demand(), Holds()));
            Assert.Equal(genesis.Sha256Hex, host.AcceptedSha256Hex);
            Assert.Equal(0UL, host.CommitSequence);
        }
        using var resumed = EconomicCompleteV9DurableSaveHost.Restore(
            dir.Root, generated, genesis.Sha256Hex);
        Assert.Equal(0UL, resumed.LastAppliedTurn);
        Commit(resumed, 1UL);
        Assert.Equal(1UL, resumed.LastAppliedTurn);
    }

    [Fact]
    public void ForgedJournalAtExpectedPathPreventsHeadPublication()
    {
        using var dir = new Folder();
        var generated = Generate();
        var genesis = Genesis(generated);
        using var host = EconomicCompleteV9DurableSaveHost.Create(
            dir.Root, generated, genesis);
        var predicted = new EconomicEcologicalTurnCommitResolver().Propose(
            generated, genesis, genesis.Sha256Hex,
            1UL, Demand(), Holds()).ProposedNext;

        var journal = new EconomicEcologicalTransitionJournalArchive(dir.Root);
        journal.Store(new EconomicEcologicalTransitionJournalRecord(
            1UL, genesis.Sha256Hex, predicted.Sha256Hex,
            Demand(rate: 250_000L), Holds()));
        var head = Path.Combine(dir.Root, "full-v9-head.gahead");
        var original = File.ReadAllBytes(head);

        Assert.Throws<InvalidDataException>(() => Commit(host, 1UL));
        Assert.Equal(genesis.Sha256Hex, host.AcceptedSha256Hex);
        Assert.Equal(original, File.ReadAllBytes(head));
    }

    [Fact]
    public void JournalRejectsUnknownVersionTruncationAndMismatchedResultName()
    {
        using var dir = new Folder();
        var generated = Generate();
        var original = Genesis(generated);
        var result = new EconomicEcologicalTurnCommitResolver().Propose(
            generated, original, original.Sha256Hex,
            1UL, Demand(), Holds()).ProposedNext;
        var record = new EconomicEcologicalTransitionJournalRecord(
            1UL, original.Sha256Hex, result.Sha256Hex, Demand(), Holds());
        var bytes = EconomicEcologicalTransitionJournalCodec.Encode(record);
        Assert.Throws<InvalidDataException>(() =>
            EconomicEcologicalTransitionJournalCodec.Decode(bytes[..^1]));

        var unknown = (byte[])bytes.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(unknown.AsSpan(8, 4), 2U);
        SHA256.HashData(unknown.AsSpan(0, unknown.Length - 32))
            .CopyTo(unknown, unknown.Length - 32);
        Assert.Throws<NotSupportedException>(() =>
            EconomicEcologicalTransitionJournalCodec.Decode(unknown));

        var journal = new EconomicEcologicalTransitionJournalArchive(dir.Root);
        journal.Store(record);
        Assert.Throws<FileNotFoundException>(() =>
            journal.Load(original.Sha256Hex));
    }

    private static string CurrentJournalDigest(string root)
    {
        var bytes = File.ReadAllBytes(
            Path.Combine(root, "full-v9-head.gahead"));
        // G3-J1 head v2: linked journal digest at offset 200.
        return Convert.ToHexString(bytes.AsSpan(200, 32));
    }
}
