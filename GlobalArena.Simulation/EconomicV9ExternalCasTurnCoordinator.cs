using System.Security.Cryptography;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Trusted external authority for ONE game. Implementations MUST provide
/// durable, linearizable snapshot reads and compare-and-swap transitions.
/// The store cannot live under (or derive authority from) the GAFULL09 save
/// directory; an in-memory fake is suitable for tests only.
/// </summary>
public interface IEconomicV9TrustedAnchorStore
{
    EconomicV9ExternalAnchorSnapshot ReadSnapshot();

    /// <summary>
    /// CAS from an exact, prepare-free snapshot to a pending intent.
    /// MUST durably persist the prepared intent before returning true.
    /// A conflicting prepare must fail without mutation.
    /// </summary>
    bool TryPrepare(
        EconomicV9ExternalAnchorSnapshot expected,
        EconomicV9PreparedExternalAdvance requested);

    /// <summary>
    /// CAS from the exact pending intent to its confirmed successor.
    /// Must atomically advance the confirmed anchor and clear the prepare.
    /// </summary>
    bool TryFinalize(EconomicV9PreparedExternalAdvance expected);
}

/// <summary>
/// G3-J3f orchestrator: external durable prepare, one existing local
/// GAFULL09 publisher, then external CAS finalize. No second game-session
/// publisher. Failures leave an explicit unresolved external prepare;
/// never infer success from a local head or cancel a prepare automatically.
/// </summary>
public sealed class EconomicV9ExternalCasTurnCoordinator
{
    private readonly object _gate = new();
    private readonly WorldGenerationResult _generated;
    private readonly IEconomicV9TrustedAnchorStore _trusted;
    private bool _requiresRecovery;

    public EconomicV9ExternalCasTurnCoordinator(
        WorldGenerationResult generatedWorld,
        IEconomicV9TrustedAnchorStore independentlyTrustedStore)
    {
        _generated = generatedWorld ??
            throw new ArgumentNullException(nameof(generatedWorld));
        _trusted = independentlyTrustedStore ??
            throw new ArgumentNullException(nameof(independentlyTrustedStore));
    }

    public EconomicEcologicalTurnCommitResult CommitEcological(
        EconomicCompleteV9DurableSaveHost host,
        ulong turnOrdinal,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        lock (_gate)
        {
            RequireUnblocked();
            var demand = workerDemand.ToArray();
            var holds = originHolds.ToArray();
            var before = Capture(host);
            var prior = EconomicCompleteV9SaveBundleCodec.Decode(
                _generated, host.CaptureCompleteSave());
            if (prior.Sha256Hex != before.CheckpointSha256Hex)
                throw new InvalidOperationException(
                    "Coordinator snapshot differs from its durable host.");
            var projection = new EconomicEcologicalTurnCommitResolver().Propose(
                _generated, prior, before.CheckpointSha256Hex,
                turnOrdinal, demand, holds);
            var record = new EconomicEcologicalTransitionJournalRecord(
                turnOrdinal, prior.Sha256Hex,
                projection.ProposedNext.Sha256Hex, demand, holds,
                before.JournalSha256Hex);
            var journal = Convert.ToHexString(SHA256.HashData(
                EconomicEcologicalTransitionJournalCodec.Encode(record)));
            var after = new EconomicV9ExternalAnchor(
                projection.ProposedNext.Sha256Hex, journal,
                checked(before.Sequence + 1UL), turnOrdinal);

            return PublishWithExternalCas(host, before, after, () =>
                host.CommitProjected(
                    before.CheckpointSha256Hex,
                    projection.ProposedNext.Sha256Hex,
                    turnOrdinal, demand, holds));
        }
    }

    public HistoricalMilitaryEcologicalTurnCandidate CommitMixed(
        EconomicCompleteV9DurableSaveHost host,
        SimulationContext independentlyTrustedContext,
        byte[] historicalAuthorizationCertificate,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds,
        HistoricalMilitaryMoveAuthorizationSeal historicalVerifier)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(historicalAuthorizationCertificate);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        ArgumentNullException.ThrowIfNull(historicalVerifier);
        lock (_gate)
        {
            RequireUnblocked();
            var demand = workerDemand.ToArray();
            var holds = originHolds.ToArray();
            var certificate = (byte[])historicalAuthorizationCertificate.Clone();
            var before = Capture(host);
            var prior = EconomicCompleteV9SaveBundleCodec.Decode(
                _generated, host.CaptureCompleteSave());
            if (prior.Sha256Hex != before.CheckpointSha256Hex)
                throw new InvalidOperationException(
                    "Coordinator snapshot differs from its durable host.");
            var projection = new HistoricalMilitaryEcologicalTurnProjector(
                _generated, historicalVerifier).Propose(
                    prior, before.CheckpointSha256Hex,
                    independentlyTrustedContext, certificate, demand, holds);
            var next = projection.EconomicProjection.ProposedNext;
            var record = new EconomicMixedV9TransitionRecord(
                independentlyTrustedContext.Turn.Value,
                independentlyTrustedContext.Seed.Value,
                before.CheckpointSha256Hex, next.Sha256Hex,
                before.JournalSha256Hex, certificate, demand, holds);
            var journal = Convert.ToHexString(SHA256.HashData(
                EconomicMixedV9TransitionCodec.Encode(record)));
            var after = new EconomicV9ExternalAnchor(
                next.Sha256Hex, journal,
                checked(before.Sequence + 1UL),
                independentlyTrustedContext.Turn.Value);

            return PublishWithExternalCas(host, before, after, () =>
                host.CommitMixed(before.CheckpointSha256Hex,
                    independentlyTrustedContext, certificate, demand, holds,
                    historicalVerifier));
        }
    }

    /// <summary>
    /// Recovery after an interrupted external finalize. The local writer must
    /// first be disposed: full replay acquires the exclusive durable lease.
    /// Only a fully verified, externally prepared local successor is eligible.
    /// Caller must read the new external snapshot before reopening gameplay.
    /// </summary>
    public EconomicV9ExternalAnchorSnapshot FinalizeRecoveredPublished(
        string directoryPath,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null)
    {
        lock (_gate)
        {
            var snapshot = _trusted.ReadSnapshot();
            var assessed =
                EconomicCompleteV9DurableSaveHost.InspectExternalReconciliation(
                    directoryPath, _generated, snapshot,
                    independentlyTrustedLegacyWorldHash,
                    independentlyTrustedLegacyEcologicalDigest,
                    historicalVerifier);
            if (assessed.State !=
                    EconomicV9ExternalReconciliationState
                        .LocalPublicationAwaitingExternalConfirmation ||
                snapshot.Prepared is null)
                throw new InvalidOperationException(
                    "Recovery may finalize only a replay-verified, externally prepared local successor.");

            if (!_trusted.TryFinalize(snapshot.Prepared))
                throw new InvalidOperationException(
                    "External recovery CAS did not confirm the prepared successor.");
            var confirmed = _trusted.ReadSnapshot();
            if (confirmed.Prepared is not null ||
                confirmed.Confirmed != snapshot.Prepared.After)
                throw new InvalidOperationException(
                    "External recovery confirmation is not independently observable.");
            _requiresRecovery = false;
            return confirmed;
        }
    }

    private T PublishWithExternalCas<T>(
        EconomicCompleteV9DurableSaveHost host,
        EconomicV9ExternalAnchor before,
        EconomicV9ExternalAnchor after,
        Func<T> publish)
    {
        var snapshot = _trusted.ReadSnapshot();
        if (snapshot.Prepared is not null || snapshot.Confirmed != before)
            throw new InvalidOperationException(
                "External anchor is stale, different, or already prepared.");
        var prepared = new EconomicV9PreparedExternalAdvance(before, after);

        // Durable write-ahead is the authority for exactly one candidate.
        if (!_trusted.TryPrepare(snapshot, prepared))
            throw new InvalidOperationException(
                "External prepare CAS rejected the proposed v9 transition.");

        // From here a crash or even an ambiguous external exception MUST NOT
        // release this coordinator for another turn. A fresh recovery decision
        // by the independent authority is necessary.
        _requiresRecovery = true;
        if (_trusted.ReadSnapshot() !=
            new EconomicV9ExternalAnchorSnapshot(before, prepared))
            throw new InvalidOperationException(
                "External prepare was not durably observable before publication.");

        // Existing host remains the ONLY local save/head writer.
        var result = publish();
        if (Capture(host) != after)
            throw new InvalidOperationException(
                "Local publication differs from externally prepared transition.");

        if (!_trusted.TryFinalize(prepared))
            throw new InvalidOperationException(
                "External finalize CAS failed after local publication; recovery required.");
        if (_trusted.ReadSnapshot() !=
            new EconomicV9ExternalAnchorSnapshot(after))
            throw new InvalidOperationException(
                "Externally finalized successor could not be independently confirmed.");
        _requiresRecovery = false;
        return result;
    }

    private void RequireUnblocked()
    {
        if (_requiresRecovery)
            throw new InvalidOperationException(
                "Coordinator has an unresolved external prepare. Reconcile before another turn.");
    }

    private static EconomicV9ExternalAnchor Capture(
        EconomicCompleteV9DurableSaveHost host) =>
        new(host.AcceptedSha256Hex, host.AcceptedJournalSha256Hex,
            host.CommitSequence, host.LastAppliedTurn);
}
