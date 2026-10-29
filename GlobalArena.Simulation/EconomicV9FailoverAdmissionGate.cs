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
