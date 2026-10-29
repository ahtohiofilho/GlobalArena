using System.Security.Cryptography;
using System.Text;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3dMixedDurableV9CommitTests
{
    private sealed class J3nRecoveryProbe :
        IEconomicV9VersionedRecoveryPhaseProbe
    {
        private readonly Action<EconomicV9VersionedRecoveryPhase> _onPhase;
        public J3nRecoveryProbe(
            Action<EconomicV9VersionedRecoveryPhase> onPhase) =>
            _onPhase = onPhase;
        public void OnPhase(EconomicV9VersionedRecoveryPhase phase) =>
            _onPhase(phase);
    }

    private static string J3nParent()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "ga-j3n-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static EconomicV9ExternalAnchor J3nAnchor(
        EconomicCompleteV9DurableSaveHost host) =>
        new(host.AcceptedSha256Hex, host.AcceptedJournalSha256Hex,
            host.CommitSequence, host.LastAppliedTurn);

    private static EconomicV9FailoverAdmissionGate J3nGate(
        EconomicV9ExternalAnchor anchor, out J3lEpochWitness witness)
    {
        witness = new J3lEpochWitness(anchor);
        return new EconomicV9FailoverAdmissionGate("j3l-restore",
            new J3iStaticPrimary(
                new EconomicV9ExternalAnchorSnapshot(anchor)), witness);
    }

    [Fact]
    public void J3nSignedMixedAndEcologicalChainRequiresHistoricalVerifier()
    {
        var world = Generate();
        var genesis = Initial(world);
        var seal = Seal(world);
        var certificate = Issue(seal, genesis, world, new HostAuthority());
        var parent = J3nParent();
        var source = Path.Combine(parent, "source");
        Directory.CreateDirectory(source);
        try
        {
            EconomicV9ExternalAnchor finalAnchor;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                source, world, genesis))
            {
                var mixed = host.CommitMixed(genesis.Sha256Hex, Context(),
                    certificate, Demand(), Holds(), seal);
                Assert.Equal(1UL, host.CommitSequence);
                host.CommitNext(
                    mixed.ProposedCheckpointSha256Hex, 2UL, Demand(), Holds());
                finalAnchor = J3nAnchor(host);
            }
            Assert.Single(Directory.GetFiles(source, "*.gamixed"));
            Assert.Single(Directory.GetFiles(source, "*.gajournal"));
            var zip = J3mZipSave(source);
            var archive = new J3mArchiveSource(zip);
            var gate = J3nGate(finalAnchor, out _);
            var blocked = Path.Combine(parent, "no-historical-key");
            Assert.Throws<InvalidOperationException>(() =>
                gate.RestoreFromVerifiedVersionedBackup(
                    blocked, world, archive));
            Assert.False(Directory.Exists(blocked));

            var badKey = Seal(world, SHA256.HashData(
                Encoding.UTF8.GetBytes("bad-j3n-historical-key")));
            var wrongKeyTarget = Path.Combine(parent, "bad-historical-key");
            Assert.Throws<InvalidDataException>(() =>
                gate.RestoreFromVerifiedVersionedBackup(
                    wrongKeyTarget, world, archive,
                    historicalVerifier: badKey));
            Assert.False(Directory.Exists(wrongKeyTarget));

            var accepted = Path.Combine(parent, "accepted");
            using (var recovered = gate.RestoreFromVerifiedVersionedBackup(
                accepted, world, archive, historicalVerifier: seal))
            {
                Assert.Equal(finalAnchor.CheckpointSha256Hex,
                    recovered.AcceptedSha256Hex);
                Assert.Equal(finalAnchor.JournalSha256Hex,
                    recovered.AcceptedJournalSha256Hex);
                Assert.Equal(2UL, recovered.CommitSequence);
                Assert.Equal(2UL, recovered.LastAppliedTurn);
                Assert.Single(Directory.GetFiles(accepted, "*.gamixed"));
                Assert.Single(Directory.GetFiles(accepted, "*.gajournal"));
                Assert.False(File.Exists(Path.Combine(
                    accepted, ".ga-v9-not-admitted")));
            }
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void J3nJournalTamperedInsideInventoriedZipIsStillRejectedByReplay()
    {
        var world = Generate();
        var initial = Initial(world);
        var seal = Seal(world);
        var certificate = Issue(seal, initial, world, new HostAuthority());
        var parent = J3nParent();
        var source = Path.Combine(parent, "source");
        Directory.CreateDirectory(source);
        try
        {
            EconomicV9ExternalAnchor anchor;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                source, world, initial))
            {
                host.CommitMixed(initial.Sha256Hex, Context(), certificate,
                    Demand(), Holds(), seal);
                anchor = J3nAnchor(host);
            }
            var recordPath = new EconomicMixedV9TransitionArchive(source)
                .RecordPath(anchor.CheckpointSha256Hex);
            var bytes = File.ReadAllBytes(recordPath);
            bytes[bytes.Length / 2] ^= 1;
            File.WriteAllBytes(recordPath, bytes);

            // The inventory matches the tampered ZIP. The full replay must
            // still reject its journal against the INDEPENDENT root/HMAC.
            var sourceArchive = new J3mArchiveSource(J3mZipSave(source));
            var gate = J3nGate(anchor, out _);
            var destination = Path.Combine(parent, "tampered");
            Assert.Throws<InvalidDataException>(() =>
                gate.RestoreFromVerifiedVersionedBackup(
                    destination, world, sourceArchive,
                    historicalVerifier: seal));
            Assert.False(Directory.Exists(destination));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData("ArchiveVerified")]
    [InlineData("InventoryExtracted")]
    [InlineData("ReplayVerified")]
    [InlineData("BeforePublish")]
    [InlineData("AfterPublish")]
    [InlineData("BeforeAdmission")]
    public void J3nEveryPublicationFailureBoundaryFailsClosed(
        string crashAtName)
    {
        var crashAt = Enum.Parse<EconomicV9VersionedRecoveryPhase>(crashAtName);
        var world = Generate();
        var initial = Initial(world);
        var parent = J3nParent();
        var source = Path.Combine(parent, "source");
        var destination = Path.Combine(parent, "recovered");
        Directory.CreateDirectory(source);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                source, world, initial)) { }
            var anchor = new EconomicV9ExternalAnchor(
                initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
            var gate = J3nGate(anchor, out _);
            var hit = false;
            var probe = new J3nRecoveryProbe(phase =>
            {
                if (phase == crashAt)
                {
                    hit = true;
                    throw new IOException("Injected G3-J3n boundary interruption.");
                }
            });
            Assert.Throws<IOException>(() =>
                gate.RestoreFromVerifiedVersionedBackupObserved(
                    destination, world, new J3mArchiveSource(J3mZipSave(source)),
                    probe));
            Assert.True(hit);
            var published = crashAt is
                EconomicV9VersionedRecoveryPhase.AfterPublish or
                EconomicV9VersionedRecoveryPhase.BeforeAdmission;
            Assert.Equal(published, Directory.Exists(destination));
            if (published)
            {
                Assert.True(File.Exists(Path.Combine(
                    destination, ".ga-v9-not-admitted")));
                Assert.Throws<IOException>(() =>
                    gate.RestoreFromVerifiedVersionedBackup(
                        destination, world,
                        new J3mArchiveSource(J3mZipSave(source))));
            }
            Assert.DoesNotContain(Directory.EnumerateDirectories(parent),
                p => Path.GetFileName(p).StartsWith(
                    ".ga-j3m-quarantine-", StringComparison.Ordinal));

            // Source host was not overwritten or accepted by remote evidence.
            using var original = EconomicCompleteV9DurableSaveHost.Restore(
                source, world, initial.Sha256Hex);
            Assert.Equal(0UL, original.CommitSequence);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void J3nContendingWriterCannotReplaceAnOccupiedDestination()
    {
        var world = Generate();
        var initial = Initial(world);
        var parent = J3nParent();
        var source = Path.Combine(parent, "source");
        var destination = Path.Combine(parent, "claimed");
        Directory.CreateDirectory(source);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                source, world, initial)) { }
            var anchor = new EconomicV9ExternalAnchor(
                initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
            var gate = J3nGate(anchor, out _);
            var seen = false;
            var probe = new J3nRecoveryProbe(phase =>
            {
                if (phase != EconomicV9VersionedRecoveryPhase.BeforePublish)
                    return;
                seen = true;
                Directory.CreateDirectory(destination);
                File.WriteAllText(Path.Combine(destination, "sentinel.txt"),
                    "pre-existing independent user state");
            });
            Assert.Throws<IOException>(() =>
                gate.RestoreFromVerifiedVersionedBackupObserved(
                    destination, world, new J3mArchiveSource(J3mZipSave(source)),
                    probe));
            Assert.True(seen);
            Assert.Equal("pre-existing independent user state",
                File.ReadAllText(Path.Combine(destination, "sentinel.txt")));
            Assert.False(File.Exists(Path.Combine(
                destination, "full-v9-head.gahead")));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void J3nConcurrentRecoveryForSameDestinationIsSerialized()
    {
        var world = Generate();
        var initial = Initial(world);
        var parent = J3nParent();
        var source = Path.Combine(parent, "source");
        var destination = Path.Combine(parent, "recovered");
        Directory.CreateDirectory(source);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                source, world, initial)) { }
            var anchor = new EconomicV9ExternalAnchor(
                initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
            var gate = J3nGate(anchor, out _);
            var archived = new J3mArchiveSource(J3mZipSave(source));
            var collided = false;
            var probe = new J3nRecoveryProbe(phase =>
            {
                if (phase != EconomicV9VersionedRecoveryPhase.ArchiveVerified)
                    return;
                collided = true;
                Assert.Throws<IOException>(() =>
                    gate.RestoreFromVerifiedVersionedBackup(
                        destination, world, archived));
            });
            using var recovered = gate.RestoreFromVerifiedVersionedBackupObserved(
                destination, world, archived, probe);
            Assert.True(collided);
            Assert.Equal(1, archived.Downloads);
            Assert.Equal(0UL, recovered.CommitSequence);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData("ReplayVerified")]
    [InlineData("AfterPublish")]
    public void J3nEpochRaceNearPublicationNeverReturnsWriter(
        string changedAtName)
    {
        var changedAt = Enum.Parse<EconomicV9VersionedRecoveryPhase>(changedAtName);
        var world = Generate();
        var initial = Initial(world);
        var parent = J3nParent();
        var source = Path.Combine(parent, "source");
        var destination = Path.Combine(parent, "recovered");
        Directory.CreateDirectory(source);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                source, world, initial)) { }
            var anchor = new EconomicV9ExternalAnchor(
                initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
            var gate = J3nGate(anchor, out var witness);
            var probe = new J3nRecoveryProbe(phase =>
            {
                if (phase == changedAt)
                    witness.State = new EconomicV9WitnessEpochState(4L, anchor);
            });
            Assert.Throws<InvalidOperationException>(() =>
                gate.RestoreFromVerifiedVersionedBackupObserved(
                    destination, world, new J3mArchiveSource(J3mZipSave(source)),
                    probe));
            var forensic = changedAt ==
                EconomicV9VersionedRecoveryPhase.AfterPublish;
            Assert.Equal(forensic, Directory.Exists(destination));
            if (forensic)
                Assert.True(File.Exists(Path.Combine(
                    destination, ".ga-v9-not-admitted")));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}
