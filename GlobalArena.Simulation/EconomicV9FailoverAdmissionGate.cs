using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Separately administered, monotonic acknowledgment witness. Implementations
/// MUST reside outside BOTH PostgreSQL failover members and local GAFULL09
/// storage. Never derive a witness from the candidate primary being checked.
/// The witness is expected to be updated only after successful external CAS
/// confirmation, with a durable monotonic history.
/// </summary>
public interface IEconomicV9IndependentFailoverWitness
{
    EconomicV9ExternalAnchor ReadLastAcknowledged(string gameId);
}

/// <summary>
/// Fail-closed admission for a promoted PostgreSQL primary. This does not
/// promote hosts or fence a former primary: the operator/cluster manager MUST
/// guarantee that the old primary cannot accept writes before invoking it.
/// An acknowledgment witness prevents accepting a stale promoted snapshot
/// after an acknowledged turn was lost in asynchronous replication.
/// </summary>
public sealed class EconomicV9FailoverAdmissionGate
{
    private readonly string _gameId;
    private readonly IEconomicV9TrustedAnchorStore _promotedPrimary;
    private readonly IEconomicV9IndependentFailoverWitness _witness;

    public EconomicV9FailoverAdmissionGate(
        string gameId,
        IEconomicV9TrustedAnchorStore promotedPrimary,
        IEconomicV9IndependentFailoverWitness independentWitness)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game identity required.", nameof(gameId));
        _gameId = gameId;
        _promotedPrimary = promotedPrimary ??
            throw new ArgumentNullException(nameof(promotedPrimary));
        _witness = independentWitness ??
            throw new ArgumentNullException(nameof(independentWitness));
    }

    public EconomicCompleteV9DurableSaveHost RestoreAfterPromotion(
        string directoryPath, WorldGenerationResult generatedWorld,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        // Independent receipt is NOT a value read from the PostgreSQL node.
        // The primary adapter rejects recovery-mode standby reads.
        var acknowledged = _witness.ReadLastAcknowledged(_gameId);
        var observed = _promotedPrimary.ReadSnapshot();
        RequireAgreement(observed, acknowledged);

        // This acquires the exclusive local writer lease and fully replays
        // the mixed/ecological chain against the independently witnessed root.
        var host = EconomicCompleteV9DurableSaveHost.RestoreExternallyConfirmed(
            directoryPath, generatedWorld, observed,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest, historicalVerifier);
        try
        {
            // Detect changes between the two independent observations while
            // restoring. A stale witness or an in-flight prepare is not a
            // viable authorization to admit gameplay.
            var latestWitness = _witness.ReadLastAcknowledged(_gameId);
            var latestPrimary = _promotedPrimary.ReadSnapshot();
            if (latestWitness != acknowledged || latestPrimary != observed)
                throw new InvalidOperationException(
                    "Failover admission changed while verifying the local save.");
            RequireAgreement(latestPrimary, latestWitness);
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Strict recovery entry path for sites requiring independent,
    /// cryptographically verified immutable backup evidence. Checks the
    /// separately witnessed writer epoch, the promoted primary, the signed
    /// backup receipt and actual archive bytes BEFORE starting local replay.
    /// The legacy RestoreAfterPromotion remains for sites not yet using this
    /// opt-in operational policy; production hosts must route exclusively
    /// through this method when the policy is mandatory.
    /// </summary>
    public EconomicCompleteV9DurableSaveHost RestoreAfterPromotionWithVerifiedBackup(
        string directoryPath, WorldGenerationResult generatedWorld,
        IEconomicV9ImmutableBackupRecoveryGate independentlyTrustedBackup,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null)
    {
        ArgumentNullException.ThrowIfNull(independentlyTrustedBackup);
        if (_witness is not IEconomicV9AcknowledgmentWitness versionedWitness)
            throw new InvalidOperationException(
                "Verified backup recovery requires an independently witnessed writer epoch.");
        var expected = versionedWitness.ReadState(_gameId);
        var primary = _promotedPrimary.ReadSnapshot();
        RequireAgreement(primary, expected.LastAcknowledged);
        if (!independentlyTrustedBackup.VerifyBackup(
                _gameId, expected.WriterEpoch, expected.LastAcknowledged))
            throw new InvalidOperationException(
                "Independent immutable backup verification failed; recovery blocked.");

        if (versionedWitness.ReadState(_gameId) != expected ||
            _promotedPrimary.ReadSnapshot() != primary)
            throw new InvalidOperationException(
                "External authorities changed while verifying immutable backup.");

        var host = RestoreAfterPromotion(directoryPath, generatedWorld,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest, historicalVerifier);
        try
        {
            if (versionedWitness.ReadState(_gameId) != expected ||
                _promotedPrimary.ReadSnapshot() != primary)
                throw new InvalidOperationException(
                    "External authorities changed during backup-gated replay.");
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Restores the EXACT independently signed/versioned backup, never a
    /// second download of its archive URL. All bytes are streamed into an
    /// isolated sibling quarantine. Inventory + replay complete before a
    /// new destination directory becomes visible. No existing save is
    /// replaced. A failed post-promotion check leaves the target blocked.
    /// </summary>
    public EconomicCompleteV9DurableSaveHost RestoreFromVerifiedVersionedBackup(
        string newDestinationDirectory, WorldGenerationResult generatedWorld,
        IEconomicV9VerifiedBackupArchiveSource signedVersionedArchive,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null) =>
        RestoreFromVerifiedVersionedBackupObserved(
            newDestinationDirectory, generatedWorld, signedVersionedArchive,
            null, independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest, historicalVerifier);

    /// <summary>
    /// Internal deterministic failpoint, deliberately inaccessible to
    /// production callers. Observers may throw to simulate process failure.
    /// </summary>
    internal EconomicCompleteV9DurableSaveHost RestoreFromVerifiedVersionedBackupObserved(
        string newDestinationDirectory, WorldGenerationResult generatedWorld,
        IEconomicV9VerifiedBackupArchiveSource signedVersionedArchive,
        IEconomicV9VersionedRecoveryPhaseProbe? probe,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(signedVersionedArchive);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDestinationDirectory);
        if (_witness is not IEconomicV9AcknowledgmentWitness versionedWitness)
            throw new InvalidOperationException(
                "Versioned recovery requires a durable, epoch-aware independent witness.");

        var destination = Path.GetFullPath(newDestinationDirectory);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException(
                "Versioned recovery must not overwrite or reuse an existing save.");
        var parent = Path.GetDirectoryName(destination);
        if (parent is null || !Directory.Exists(parent))
            throw new DirectoryNotFoundException(
                "A preprovisioned, access-controlled recovery parent is required.");
        if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Recovery parent cannot be a symlink/reparse point.");

        // Advisory cross-process serialization of cooperating recovery tools.
        // A trusted ACL on this parent (and its ancestors) is still required:
        // this lock cannot stop a hostile process with write permissions.
        var lockName = ".ga-j3n-recovery-" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(
                    Path.GetFileName(destination)))) + ".lock";
        using var destinationLease = new FileStream(
            Path.Combine(parent, lockName),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("Recovery destination was already claimed.");

        var expected = versionedWitness.ReadState(_gameId);
        var primary = _promotedPrimary.ReadSnapshot();
        RequireAgreement(primary, expected.LastAcknowledged);
        if (expected.WriterEpoch <= 0)
            throw new InvalidOperationException(
                "Independently witnessed writer generation is invalid.");

        var quarantine = Path.Combine(parent,
            ".ga-j3m-quarantine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(quarantine);
        var moved = false;
        try
        {
            var zipPath = Path.Combine(quarantine, "verified-version.zip");
            using (var downloaded = new FileStream(zipPath,
                FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (!signedVersionedArchive.TryDownloadVerifiedArchive(
                        _gameId, expected.WriterEpoch,
                        expected.LastAcknowledged, downloaded))
                    throw new InvalidOperationException(
                        "Versioned immutable backup receipt or archive digest was rejected.");
                downloaded.Flush(flushToDisk: true);
            }
            probe?.OnPhase(EconomicV9VersionedRecoveryPhase.ArchiveVerified);

            var extracted = Path.Combine(quarantine, "replay");
            EconomicV9VersionedSaveArchive.ExtractVerifiedArchive(
                zipPath, extracted);
            probe?.OnPhase(EconomicV9VersionedRecoveryPhase.InventoryExtracted);
            RequireUnchanged(expected, primary, versionedWitness);

            // Full GAFULL09 journal replay in the quarantined directory,
            // including historical mixed-command authentication.
            using (var checkedSave = RestoreAfterPromotion(
                extracted, generatedWorld,
                independentlyTrustedLegacyWorldHash,
                independentlyTrustedLegacyEcologicalDigest,
                historicalVerifier))
            {
                if (checkedSave.AcceptedSha256Hex !=
                        expected.LastAcknowledged.CheckpointSha256Hex ||
                    checkedSave.AcceptedJournalSha256Hex !=
                        expected.LastAcknowledged.JournalSha256Hex ||
                    checkedSave.CommitSequence !=
                        expected.LastAcknowledged.Sequence ||
                    checkedSave.LastAppliedTurn !=
                        expected.LastAcknowledged.Turn)
                    throw new InvalidDataException(
                        "Replayed versioned save differs from acknowledged head.");
            }
            RequireUnchanged(expected, primary, versionedWitness);
            probe?.OnPhase(EconomicV9VersionedRecoveryPhase.ReplayVerified);

            // Lease from the replay was released. Publishing is a single
            // same-filesystem directory rename; only an absent target is
            // eligible. A secure recovery parent must exclude other writers.
            if (File.Exists(destination) || Directory.Exists(destination))
                throw new IOException("Another writer claimed the recovery destination.");
            // Persistent marker travels with the directory rename. A crash
            // after publication leaves an explicit UNADMITTED forensic save.
            // Only this strict gate may remove the marker on full success.
            var marker = Path.Combine(extracted, ".ga-v9-not-admitted");
            File.WriteAllText(marker, "G3-J3n recovery awaiting authority checks.");
            probe?.OnPhase(EconomicV9VersionedRecoveryPhase.BeforePublish);
            RequireUnchanged(expected, primary, versionedWitness);
            Directory.Move(extracted, destination);
            moved = true;
            probe?.OnPhase(EconomicV9VersionedRecoveryPhase.AfterPublish);

            // Re-acquire the writer lease against the published directory
            // and revalidate both authorities before handing it to gameplay.
            var admitted = RestorePublishedQuarantineForAdmission(
                destination, generatedWorld,
                independentlyTrustedLegacyWorldHash,
                independentlyTrustedLegacyEcologicalDigest,
                historicalVerifier);
            try
            {
                probe?.OnPhase(EconomicV9VersionedRecoveryPhase.BeforeAdmission);
                RequireUnchanged(expected, primary, versionedWitness);
                File.Delete(Path.Combine(destination, ".ga-v9-not-admitted"));
                return admitted;
            }
            catch
            {
                admitted.Dispose();
                throw;
            }
        }
        finally
        {
            if (Directory.Exists(quarantine))
                Directory.Delete(quarantine, recursive: true);
            // If moved is true and admission fails, the new directory is
            // retained as an UNADMITTED forensic artifact, never replaced
            // or silently rolled back. The caller must handle that incident.
            _ = moved;
        }
    }

    /// <summary>
    /// Internal continuation after a marked directory has been atomically
    /// published. Public/legacy Restore paths reject the marker; this
    /// private entry point performs full replay under the marker, and
    /// only the strict caller may clear it after all authority rechecks.
    /// </summary>
    private EconomicCompleteV9DurableSaveHost RestorePublishedQuarantineForAdmission(
        string directoryPath, WorldGenerationResult generatedWorld,
        WorldStateHash? independentlyTrustedLegacyWorldHash,
        string? independentlyTrustedLegacyEcologicalDigest,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier)
    {
        if (!File.Exists(Path.Combine(directoryPath, ".ga-v9-not-admitted")))
            throw new InvalidOperationException(
                "Published recovery marker was removed before strict admission.");
        var acknowledged = _witness.ReadLastAcknowledged(_gameId);
        var observed = _promotedPrimary.ReadSnapshot();
        RequireAgreement(observed, acknowledged);
        var host = EconomicCompleteV9DurableSaveHost
            .RestoreExternallyConfirmedForPublication(
                directoryPath, generatedWorld, observed,
                independentlyTrustedLegacyWorldHash,
                independentlyTrustedLegacyEcologicalDigest, historicalVerifier);
        try
        {
            if (_witness.ReadLastAcknowledged(_gameId) != acknowledged ||
                _promotedPrimary.ReadSnapshot() != observed)
                throw new InvalidOperationException(
                    "Authorities changed during marked publication replay.");
            return host;
        }
        catch
        {
            host.Dispose();
            throw;
        }
    }

    private void RequireUnchanged(
        EconomicV9WitnessEpochState expected,
        EconomicV9ExternalAnchorSnapshot primary,
        IEconomicV9AcknowledgmentWitness versionedWitness)
    {
        if (versionedWitness.ReadState(_gameId) != expected ||
            _promotedPrimary.ReadSnapshot() != primary)
            throw new InvalidOperationException(
                "Primary or independent witness changed during versioned recovery.");
    }

    private static void RequireAgreement(
        EconomicV9ExternalAnchorSnapshot snapshot,
        EconomicV9ExternalAnchor acknowledged)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(acknowledged);
        if (snapshot.Prepared is not null ||
            snapshot.Confirmed != acknowledged)
            throw new InvalidOperationException(
                "Failover is not safe: promoted primary differs from an independently acknowledged v9 anchor or has an unresolved prepare.");
    }
}
