using System.Text;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3dMixedDurableV9CommitTests
{
    private static string? J3oCrashRoot() =>
        Environment.GetEnvironmentVariable("GA_J3O_ROOT");

    [Fact]
    public void J3oPrepareRealCrashFixture()
    {
        var root = J3oCrashRoot();
        if (root is null) return; // Dedicated Linux drill supplies isolated fixture.
        var source = Path.Combine(root, "source");
        var world = Generate();
        var genesis = Initial(world);
        Directory.CreateDirectory(source);
        EconomicV9ExternalAnchor anchor;
        using (var host = EconomicCompleteV9DurableSaveHost.Create(
            source, world, genesis))
        {
            host.CommitNext(genesis.Sha256Hex, 1UL, Demand(), Holds());
            anchor = new EconomicV9ExternalAnchor(
                host.AcceptedSha256Hex, host.AcceptedJournalSha256Hex,
                host.CommitSequence, host.LastAppliedTurn);
        }
        File.WriteAllBytes(Path.Combine(root, "version.zip"),
            J3mZipSave(source));
        File.WriteAllLines(Path.Combine(root, "anchor.txt"),
            [anchor.CheckpointSha256Hex, anchor.JournalSha256Hex,
             anchor.Sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
             anchor.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
    }

    private static EconomicV9ExternalAnchor J3oReadAnchor(string root)
    {
        var lines = File.ReadAllLines(Path.Combine(root, "anchor.txt"));
        Assert.Equal(4, lines.Length);
        return new EconomicV9ExternalAnchor(
            lines[0], lines[1],
            ulong.Parse(lines[2], System.Globalization.CultureInfo.InvariantCulture),
            ulong.Parse(lines[3], System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void J3oCrashWorker()
    {
        var root = J3oCrashRoot();
        if (root is null) return; // Dedicated Linux drill supplies isolated fixture.
        var phase = Enum.Parse<EconomicV9VersionedRecoveryPhase>(
            Environment.GetEnvironmentVariable("GA_J3O_PHASE") ??
            throw new InvalidOperationException("Crash phase required."));
        var world = Generate();
        var anchor = J3oReadAnchor(root);
        var gate = J3nGate(anchor, out _);
        var zip = new J3mArchiveSource(
            File.ReadAllBytes(Path.Combine(root, "version.zip")));
        var probe = new J3nRecoveryProbe(observed =>
        {
            if (observed != phase)
                return;
            // The controller only terminates this process tree after a
            // real FILESYSTEM ready marker has been flushed to disk.
            var ready = Path.Combine(root, "worker-ready");
            using var signal = new FileStream(ready, FileMode.CreateNew,
                FileAccess.Write, FileShare.None);
            signal.Write(Encoding.UTF8.GetBytes(observed.ToString()));
            signal.Flush(flushToDisk: true);
            Thread.Sleep(Timeout.Infinite);
        });
        using var notExpected = gate.RestoreFromVerifiedVersionedBackupObserved(
            Path.Combine(root, "recovered"), world, zip, probe);
        throw new InvalidOperationException(
            "Crash harness worker unexpectedly returned an admitted save.");
    }

    [Fact]
    public void J3oNewProcessInspectsCrashWithoutAdmittingWriter()
    {
        var root = J3oCrashRoot();
        if (root is null) return; // Dedicated Linux drill supplies isolated fixture.
        var phase = Enum.Parse<EconomicV9VersionedRecoveryPhase>(
            Environment.GetEnvironmentVariable("GA_J3O_PHASE") ??
            throw new InvalidOperationException("Crash phase required."));
        var ready = Path.Combine(root, "worker-ready");
        Assert.True(File.Exists(ready));
        Assert.Equal(phase.ToString(), File.ReadAllText(ready));
        var scan = EconomicV9VersionedRecoveryCrashInspector.Inspect(
            root, "recovered");
        Assert.True(scan.DestinationRecoveryLeaseExists);
        var published = phase is
            EconomicV9VersionedRecoveryPhase.AfterPublish or
            EconomicV9VersionedRecoveryPhase.BeforeAdmission;
        if (published)
        {
            Assert.Equal(EconomicV9PublishedRecoveryState.Unadmitted,
                scan.DestinationState);
            var world = Generate();
            var anchor = J3oReadAnchor(root);
            var destination = Path.Combine(root, "recovered");
            Assert.Throws<InvalidOperationException>(() =>
                EconomicCompleteV9DurableSaveHost.Restore(
                    destination, world, anchor.CheckpointSha256Hex,
                    independentlyTrustedJournalRoot: anchor.JournalSha256Hex));
            Assert.Throws<InvalidOperationException>(() =>
                EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed(
                    destination, world,
                    new EconomicV9ExternalAnchorSnapshot(anchor)));
            var gate = J3nGate(anchor, out _);
            Assert.Throws<IOException>(() =>
                gate.RestoreFromVerifiedVersionedBackup(
                    destination, world,
                    new J3mArchiveSource(File.ReadAllBytes(
                        Path.Combine(root, "version.zip")))));
            // SIGKILL skips finally: the ZIP staging directory survives,
            // even though the extracted replay directory was renamed.
            Assert.Equal(1, scan.StrandedQuarantineCount);
        }
        else
        {
            Assert.Equal(EconomicV9PublishedRecoveryState.Absent,
                scan.DestinationState);
            Assert.Equal(1, scan.StrandedQuarantineCount);
        }
        // Verified original save was never overwritten by a killed child.
        using var original = EconomicCompleteV9DurableSaveHost.Restore(
            Path.Combine(root, "source"), Generate(),
            J3oReadAnchor(root).CheckpointSha256Hex,
            independentlyTrustedJournalRoot: J3oReadAnchor(root).JournalSha256Hex);
        Assert.Equal(1UL, original.CommitSequence);
    }

    [Fact]
    public void J3oInspectorNeverInterpretsUnmarkedSaveAsAuthority()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "ga-j3o-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var result = EconomicV9VersionedRecoveryCrashInspector.Inspect(
                root, "target");
            Assert.Equal(EconomicV9PublishedRecoveryState.Absent,
                result.DestinationState);
            Directory.CreateDirectory(Path.Combine(root, "target"));
            result = EconomicV9VersionedRecoveryCrashInspector.Inspect(
                root, "target");
            Assert.Equal(EconomicV9PublishedRecoveryState.PresentWithoutMarker,
                result.DestinationState);
            Assert.False(result.DestinationRecoveryLeaseExists);
            File.WriteAllText(Path.Combine(root, "target",
                ".ga-v9-not-admitted"), "pending");
            result = EconomicV9VersionedRecoveryCrashInspector.Inspect(
                root, "target");
            Assert.Equal(EconomicV9PublishedRecoveryState.Unadmitted,
                result.DestinationState);
            Assert.Throws<ArgumentException>(() =>
                EconomicV9VersionedRecoveryCrashInspector.Inspect(
                    root, "../outside"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
