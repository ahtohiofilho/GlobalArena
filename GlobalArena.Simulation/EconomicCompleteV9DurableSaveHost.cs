using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// G3-I6 single-writer local host for a COMPLETE GAFULL09 (world + facilities
/// + ecology + material reserves). The complete immutable bundle is flushed
/// before a single head is published by same-directory replace. This is a
/// local save acceptance boundary, not a trusted distributed transaction,
/// authenticated command journal, or turn-scheduler integration.
/// </summary>
public sealed class EconomicCompleteV9DurableSaveHost : IDisposable
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GAFH0901");
    private const uint HeadVersion = 2U;
    private const int HeadBodyBytes = 232;
    private const int HeadBytes = HeadBodyBytes + 32;
    private static readonly string ZeroDigest = new('0', 64);

    private readonly object _gate = new();
    private readonly WorldGenerationResult _generated;
    private readonly EconomicCompleteV9SaveBundleArchive _archive;
    private readonly EconomicEcologicalTransitionJournalArchive _journal;
    private readonly EconomicMixedV9TransitionArchive _mixed;
    private readonly FileStream _lease;
    private readonly EconomicEcologicalTurnCommitResolver _turns = new();
    private EconomicEcologicalCheckpoint _accepted;
    private Head _head;
    private bool _disposed;

    private EconomicCompleteV9DurableSaveHost(
        WorldGenerationResult generated, EconomicCompleteV9SaveBundleArchive archive,
        FileStream lease, EconomicEcologicalCheckpoint accepted, Head head)
    {
        _generated = generated;
        _archive = archive;
        _journal = new EconomicEcologicalTransitionJournalArchive(archive.DirectoryPath);
        _mixed = new EconomicMixedV9TransitionArchive(archive.DirectoryPath);
        _lease = lease;
        _accepted = accepted;
        _head = head;
    }

    public static EconomicCompleteV9DurableSaveHost Create(
        string directoryPath, WorldGenerationResult generatedWorld,
        EconomicEcologicalCheckpoint initial)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(initial);
        if (initial.NaturalCapital.LastAppliedTurn != 0UL ||
            initial.MaterialReserves.AppliedTurnCursors.Count != 0)
            throw new InvalidOperationException(
                "A new v9 genesis head requires turn zero. Use verified migration or restore.");
        var archive = new EconomicCompleteV9SaveBundleArchive(directoryPath);
        var lease = Acquire(archive.DirectoryPath);
        try
        {
            var headPath = HeadPath(archive.DirectoryPath);
            if (File.Exists(headPath))
                throw new IOException("A full-v9 save head already exists.");
            // Encode + decode makes both world/sidecar validation mandatory.
            var checkedSnapshot = EconomicCompleteV9SaveBundleCodec.Decode(
                generatedWorld, EconomicCompleteV9SaveBundleCodec.Encode(initial));
            var head = NewHead(checkedSnapshot, false, ZeroDigest, ZeroDigest);
            archive.Store(checkedSnapshot);
            WriteHead(headPath, head, overwrite: false);
            return new EconomicCompleteV9DurableSaveHost(
                generatedWorld, archive, lease, checkedSnapshot, head);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Explicit, independently trusted v8-to-v9 migration. The original
    /// complete v8 file and BOTH externally trusted v8 commitments must
    /// validate BEFORE the first durable v9 head is published.
    /// </summary>
    public static EconomicCompleteV9DurableSaveHost CreateFromLegacy(
        string directoryPath, WorldGenerationResult generatedWorld,
        byte[] acceptedLegacyCompleteV8Bundle,
        WorldStateHash independentlyAcceptedV8WorldHash,
        string independentlyAcceptedV8EcologicalDigest)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(acceptedLegacyCompleteV8Bundle);
        var acceptedLegacy = EconomicEcologicalSidecarArchive.NormalizeDigest(
            independentlyAcceptedV8EcologicalDigest);
        var migrated = new EconomicV8ToV9EcologicalSaveMigrator().Migrate(
            generatedWorld, acceptedLegacyCompleteV8Bundle,
            independentlyAcceptedV8WorldHash, acceptedLegacy);
        if (migrated.AcceptedLegacyWorldHash != independentlyAcceptedV8WorldHash ||
            migrated.AcceptedLegacyCheckpointSha256Hex != acceptedLegacy)
            throw new InvalidOperationException("Migration certificate disagrees with trusted v8 state.");

        var archive = new EconomicCompleteV9SaveBundleArchive(directoryPath);
        var lease = Acquire(archive.DirectoryPath);
        try
        {
            var headPath = HeadPath(archive.DirectoryPath);
            if (File.Exists(headPath))
                throw new IOException("A full-v9 save head already exists.");
            var checkpoint = EconomicCompleteV9SaveBundleCodec.Decode(
                generatedWorld, migrated.CompleteV9Bundle);
            var head = NewHead(checkpoint, true,
                independentlyAcceptedV8WorldHash.HexDigest, acceptedLegacy);
            archive.Store(checkpoint);
            WriteHead(headPath, head, overwrite: false);
            return new EconomicCompleteV9DurableSaveHost(
                generatedWorld, archive, lease, checkpoint, head);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The accepted digest MUST come from a trustworthy external store,
    /// not from the save head itself. Migrated heads additionally require
    /// independently trusted legacy commitments on every restore.
    /// </summary>
    public static EconomicCompleteV9DurableSaveHost Restore(
        string directoryPath, WorldGenerationResult generatedWorld,
        string independentlyTrustedCurrentDigest,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null,
        string? independentlyTrustedJournalRoot = null) =>
        RestoreCore(directoryPath, generatedWorld, independentlyTrustedCurrentDigest,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest,
            historicalVerifier, independentlyTrustedJournalRoot,
            allowPublishedUnadmitted: false);

    private static EconomicCompleteV9DurableSaveHost RestoreCore(
        string directoryPath, WorldGenerationResult generatedWorld,
        string independentlyTrustedCurrentDigest,
        WorldStateHash? independentlyTrustedLegacyWorldHash,
        string? independentlyTrustedLegacyEcologicalDigest,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier,
        string? independentlyTrustedJournalRoot,
        bool allowPublishedUnadmitted)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        var trusted = EconomicEcologicalSidecarArchive.NormalizeDigest(
            independentlyTrustedCurrentDigest);
        var archive = new EconomicCompleteV9SaveBundleArchive(directoryPath);
        var lease = Acquire(archive.DirectoryPath);
        try
        {
            if (!allowPublishedUnadmitted &&
                File.Exists(Path.Combine(archive.DirectoryPath,
                    ".ga-v9-not-admitted")))
                throw new InvalidOperationException(
                    "Published v9 recovery remains UNADMITTED after an interrupted promotion.");
            var head = ReadHead(HeadPath(archive.DirectoryPath));
            if (head.Current != trusted)
                throw new InvalidOperationException(
                    "Local head does not match the externally accepted checkpoint.");
            if (head.Migrated)
            {
                if (!independentlyTrustedLegacyWorldHash.HasValue ||
                    independentlyTrustedLegacyWorldHash.Value.FormatVersion != 8U ||
                    independentlyTrustedLegacyWorldHash.Value.HexDigest != head.LegacyWorld ||
                    independentlyTrustedLegacyEcologicalDigest is null ||
                    EconomicEcologicalSidecarArchive.NormalizeDigest(
                        independentlyTrustedLegacyEcologicalDigest) != head.LegacyEcology)
                    throw new InvalidOperationException(
                        "Migrated save requires independently trusted historical world and ecology.");
            }
            else if (independentlyTrustedLegacyWorldHash.HasValue ||
                     independentlyTrustedLegacyEcologicalDigest is not null)
                throw new InvalidOperationException(
                    "Genesis save must not claim unrelated migration ancestry.");

            var checkpoint = archive.Load(generatedWorld, trusted);
            if (checkpoint.WorldHash != head.WorldHash ||
                checkpoint.NaturalCapital.LastAppliedTurn != head.Turn)
                throw new InvalidDataException(
                    "Complete v9 bundle differs from the durable head.");
            if (head.Sequence > 0UL)
            {
                var previous = archive.Load(generatedWorld, head.Previous);
                var isMixed = File.Exists(
                    new EconomicMixedV9TransitionArchive(archive.DirectoryPath)
                        .RecordPath(head.Current));
                if (previous.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
                    previous.NaturalCapital.LastAppliedTurn + 1UL != head.Turn ||
                    !isMixed && previous.WorldHash != head.WorldHash)
                    throw new InvalidDataException(
                        "Complete-save predecessor is missing or inconsistent.");
            }

            // The externally trusted head is checked before verifying
            // the complete immutable input/output journal chain.
            new EconomicCompleteV9JournalReplayVerifier().VerifyChain(
                generatedWorld, archive,
                new EconomicEcologicalTransitionJournalArchive(
                    archive.DirectoryPath), trusted, head.Sequence,
                head.JournalDigest, historicalVerifier,
                independentlyTrustedJournalRoot);

            return new EconomicCompleteV9DurableSaveHost(
                generatedWorld, archive, lease, checkpoint, head);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public string AcceptedSha256Hex
    {
        get { lock (_gate) { EnsureOpen(); return _accepted.Sha256Hex; } }
    }

    /// <summary>
    /// Store this journal root independently from the local head when mixed
    /// commands are enabled. The checkpoint digest alone may not authenticate
    /// changes to inputs that leave the resulting state unchanged.
    /// </summary>
    public string AcceptedJournalSha256Hex
    {
        get { lock (_gate) { EnsureOpen(); return _head.JournalDigest; } }
    }

    public ulong LastAppliedTurn
    {
        get { lock (_gate) { EnsureOpen(); return _head.Turn; } }
    }

    public ulong CommitSequence
    {
        get { lock (_gate) { EnsureOpen(); return _head.Sequence; } }
    }

    public bool HasLegacyMigration
    {
        get { lock (_gate) { EnsureOpen(); return _head.Migrated; } }
    }

    public byte[] CaptureCompleteSave()
    {
        lock (_gate)
        {
            EnsureOpen();
            return EconomicCompleteV9SaveBundleCodec.Encode(_accepted);
        }
    }

    public EconomicEcologicalTurnCommitResult CommitNext(
        string expectedAcceptedDigest, ulong turnOrdinal,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds) =>
        CommitInternal(expectedAcceptedDigest, null,
            turnOrdinal, workerDemand, originHolds);

    /// <summary>
    /// Rejects a divergence between the audited phase proposal and the
    /// independently recalculated durable transition BEFORE publishing
    /// the complete bundle, journal or head.
    /// </summary>
    public EconomicEcologicalTurnCommitResult CommitProjected(
        string expectedAcceptedDigest, string expectedProposedDigest,
        ulong turnOrdinal, IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds) =>
        CommitInternal(expectedAcceptedDigest,
            EconomicEcologicalSidecarArchive.NormalizeDigest(expectedProposedDigest),
            turnOrdinal, workerDemand, originHolds);

    private EconomicEcologicalTurnCommitResult CommitInternal(
        string expectedAcceptedDigest, string? expectedProposedDigest,
        ulong turnOrdinal, IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        lock (_gate)
        {
            EnsureOpen();
            _accepted.RequireExpectedDigest(expectedAcceptedDigest);
            var path = HeadPath(_archive.DirectoryPath);
            var actual = ReadHead(path);
            if (actual != _head)
                throw new InvalidOperationException(
                    "Durable complete-save head was changed externally.");
            // Snapshot the exact inputs once: no mutable/re-enumerated
            // orders can diverge from the data written to the replay journal.
            ArgumentNullException.ThrowIfNull(workerDemand);
            ArgumentNullException.ThrowIfNull(originHolds);
            var demand = workerDemand.ToArray();
            var holds = originHolds.ToArray();
            var proposed = _turns.Propose(
                _generated, _accepted, expectedAcceptedDigest,
                turnOrdinal, demand, holds);
            if (expectedProposedDigest is not null &&
                proposed.ProposedNext.Sha256Hex != expectedProposedDigest)
                throw new InvalidOperationException(
                    "Audited turn proposal differs from durable ecological calculation.");
            if (File.Exists(_mixed.RecordPath(
                proposed.ProposedNext.Sha256Hex)))
                throw new InvalidDataException(
                    "A mixed journal already owns this proposed checkpoint.");
            var recorded = new EconomicEcologicalTransitionJournalRecord(
                turnOrdinal, _accepted.Sha256Hex,
                proposed.ProposedNext.Sha256Hex, demand, holds, _head.JournalDigest);
            var journalDigest = Convert.ToHexString(
                SHA256.HashData(EconomicEcologicalTransitionJournalCodec.Encode(
                    recorded)));
            var next = new Head(
                proposed.ProposedNext.WorldHash, proposed.ProposedNext.Sha256Hex,
                _head.Current, proposed.ProposedNext.NaturalCapital.LastAppliedTurn,
                checked(_head.Sequence + 1UL), _head.Migrated,
                _head.LegacyWorld, _head.LegacyEcology, journalDigest);

            // Failures before head publication cannot advance the accepted
            // state. Orphaned complete bundles remain unaccepted.
            _archive.Store(proposed.ProposedNext);
            _journal.Store(recorded);
            WriteHead(path, next, overwrite: true);
            _accepted = proposed.ProposedNext;
            _head = next;
            return proposed;
        }
    }

    /// <summary>
    /// ONE owner publishes a mixed military/ecological transition. A
    /// historical host MAC and trusted turn/seed are verified before any
    /// publication; the entire GAFULL09 and mixed journal are flushed before
    /// the SAME head is replaced. The caller must independently trust and
    /// persist the new journal root; this host is not a distributed commit.
    /// </summary>
    public HistoricalMilitaryEcologicalTurnCandidate CommitMixed(
        string expectedAcceptedDigest, SimulationContext trustedContext,
        byte[] historicalAuthorizationCertificate,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds,
        HistoricalMilitaryMoveAuthorizationSeal historicalVerifier)
    {
        ArgumentNullException.ThrowIfNull(historicalVerifier);
        ArgumentNullException.ThrowIfNull(historicalAuthorizationCertificate);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        lock (_gate)
        {
            EnsureOpen();
            _accepted.RequireExpectedDigest(expectedAcceptedDigest);
            var path = HeadPath(_archive.DirectoryPath);
            if (ReadHead(path) != _head)
                throw new InvalidOperationException(
                    "Durable v9 head was changed before mixed turn acceptance.");
            if (_accepted.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
                trustedContext.Turn.Value !=
                    _accepted.NaturalCapital.LastAppliedTurn + 1UL)
                throw new InvalidOperationException(
                    "Mixed turn must follow the last accepted ecological turn.");

            var demand = workerDemand.ToArray();
            var holds = originHolds.ToArray();
            var certificate = (byte[])historicalAuthorizationCertificate.Clone();
            var candidate = new HistoricalMilitaryEcologicalTurnProjector(
                _generated, historicalVerifier).Propose(
                _accepted, expectedAcceptedDigest, trustedContext,
                certificate, demand, holds);
            var nextCheckpoint = candidate.EconomicProjection.ProposedNext;
            var mixed = new EconomicMixedV9TransitionRecord(
                trustedContext.Turn.Value, trustedContext.Seed.Value,
                _accepted.Sha256Hex, nextCheckpoint.Sha256Hex,
                _head.JournalDigest, certificate, demand, holds);
            if (File.Exists(_journal.RecordPath(nextCheckpoint.Sha256Hex)))
                throw new InvalidDataException(
                    "An ecological journal already owns this mixed checkpoint.");
            var journalDigest = Convert.ToHexString(
                SHA256.HashData(EconomicMixedV9TransitionCodec.Encode(mixed)));
            var nextHead = new Head(
                nextCheckpoint.WorldHash, nextCheckpoint.Sha256Hex,
                _head.Current, nextCheckpoint.NaturalCapital.LastAppliedTurn,
                checked(_head.Sequence + 1UL), _head.Migrated,
                _head.LegacyWorld, _head.LegacyEcology, journalDigest);

            // No accepted mutation until both immutable blobs exist. Crash
            // before head publication leaves only unaccepted orphan files.
            _archive.Store(nextCheckpoint);
            _mixed.Store(mixed);
            WriteHead(path, nextHead, overwrite: true);
            _accepted = nextCheckpoint;
            _head = nextHead;
            return candidate;
        }
    }

    /// <summary>
    /// G3-J3e recovery preflight. The snapshot MUST originate from an external
    /// trusted authority. A local head may match either its confirmed anchor
    /// or the ONE externally prepared successor; a local-only advance is never
    /// accepted. Full replay is mandatory before reporting a recovery state.
    /// This method does not mutate either store and NEVER finalizes a prepare.
    /// </summary>
    public static EconomicV9ExternalReconciliationAssessment
        InspectExternalReconciliation(
            string directoryPath, WorldGenerationResult generatedWorld,
            EconomicV9ExternalAnchorSnapshot independentlyTrustedSnapshot,
            WorldStateHash? independentlyTrustedLegacyWorldHash = null,
            string? independentlyTrustedLegacyEcologicalDigest = null,
            HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null) =>
        InspectExternalReconciliationCore(
            directoryPath, generatedWorld, independentlyTrustedSnapshot,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest,
            historicalVerifier, allowPublishedUnadmitted: false);

    private static EconomicV9ExternalReconciliationAssessment
        InspectExternalReconciliationCore(
            string directoryPath, WorldGenerationResult generatedWorld,
            EconomicV9ExternalAnchorSnapshot independentlyTrustedSnapshot,
            WorldStateHash? independentlyTrustedLegacyWorldHash,
            string? independentlyTrustedLegacyEcologicalDigest,
            HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier,
            bool allowPublishedUnadmitted)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(independentlyTrustedSnapshot);
        var archive = new EconomicCompleteV9SaveBundleArchive(directoryPath);
        // The local head is only a candidate. Never derive external authority
        // from it, a journal, a pending blob, or an untrusted caller cache.
        var observedHead = ReadHead(HeadPath(archive.DirectoryPath));
        var confirmed = independentlyTrustedSnapshot.Confirmed;
        var pending = independentlyTrustedSnapshot.Prepared;
        var matchesConfirmed = observedHead.Current == confirmed.CheckpointSha256Hex &&
            observedHead.JournalDigest == confirmed.JournalSha256Hex &&
            observedHead.Sequence == confirmed.Sequence &&
            observedHead.Turn == confirmed.Turn;
        var matchesPrepared = pending is not null &&
            observedHead.Current == pending.After.CheckpointSha256Hex &&
            observedHead.JournalDigest == pending.After.JournalSha256Hex &&
            observedHead.Sequence == pending.After.Sequence &&
            observedHead.Turn == pending.After.Turn;
        if (!matchesConfirmed && !matchesPrepared)
            throw new InvalidOperationException(
                "Local v9 head is neither the externally confirmed anchor nor its authorized prepared successor.");

        var chosen = matchesConfirmed ? confirmed : pending!.After;
        var state = pending is null
            ? EconomicV9ExternalReconciliationState.Confirmed
            : matchesConfirmed
                ? EconomicV9ExternalReconciliationState.PreparedWithoutLocalPublication
                : EconomicV9ExternalReconciliationState.LocalPublicationAwaitingExternalConfirmation;

        // Restore acquires the exclusive writer lease and replays the full
        // anchored chain. Keeping its lease open while examining the head
        // avoids treating an unverified local candidate as confirmed.
        using var verified = RestoreCore(directoryPath, generatedWorld,
            chosen.CheckpointSha256Hex,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest,
            historicalVerifier, chosen.JournalSha256Hex, allowPublishedUnadmitted);
        if (verified._head != observedHead ||
            verified._head.JournalDigest != chosen.JournalSha256Hex ||
            verified._head.Sequence != chosen.Sequence ||
            verified._head.Turn != chosen.Turn)
            throw new InvalidDataException(
                "The v9 head changed during independent anchor verification.");

        if (state ==
            EconomicV9ExternalReconciliationState.LocalPublicationAwaitingExternalConfirmation)
        {
            // Full-chain replay alone is not enough: prove the prepared
            // successor is directly linked to the trusted confirmed anchor,
            // including the journal root (which binds no-op input changes).
            if (observedHead.Previous != confirmed.CheckpointSha256Hex)
                throw new InvalidDataException(
                    "Prepared local head does not descend from the independently confirmed checkpoint.");
            var mixed = new EconomicMixedV9TransitionArchive(archive.DirectoryPath);
            var ecological = new EconomicEcologicalTransitionJournalArchive(
                archive.DirectoryPath);
            var hasMixed = File.Exists(mixed.RecordPath(chosen.CheckpointSha256Hex));
            var hasEcological = File.Exists(
                ecological.RecordPath(chosen.CheckpointSha256Hex));
            if (hasMixed == hasEcological)
                throw new InvalidDataException(
                    "Prepared successor must have exactly one recognized journal type.");
            string precedingCheckpoint;
            string precedingRoot;
            ulong journalTurn;
            if (hasMixed)
            {
                var step = mixed.Load(chosen.CheckpointSha256Hex);
                precedingCheckpoint = step.PreviousCheckpointSha256Hex;
                precedingRoot = step.PreviousJournalSha256Hex;
                journalTurn = step.TurnOrdinal;
            }
            else
            {
                var step = ecological.Load(chosen.CheckpointSha256Hex);
                precedingCheckpoint = step.PreviousCheckpointSha256Hex;
                precedingRoot = step.PreviousJournalSha256Hex;
                journalTurn = step.TurnOrdinal;
            }
            if (precedingCheckpoint != confirmed.CheckpointSha256Hex ||
                precedingRoot != confirmed.JournalSha256Hex ||
                journalTurn != chosen.Turn)
                throw new InvalidDataException(
                    "Prepared successor journal does not link to the trusted confirmed anchor.");
        }

        return new EconomicV9ExternalReconciliationAssessment(
            state, chosen, confirmed, pending);
    }

    /// <summary>
    /// Safe admission gate. An unsettled external prepare is NEVER treated as
    /// authorization to resume gameplay; reconcile and finalize/abort it in
    /// the independently trusted authority first, then fetch a fresh snapshot.
    /// </summary>
    public static EconomicCompleteV9DurableSaveHost RestoreExternallyConfirmed(
        string directoryPath, WorldGenerationResult generatedWorld,
        EconomicV9ExternalAnchorSnapshot independentlyTrustedSnapshot,
        WorldStateHash? independentlyTrustedLegacyWorldHash = null,
        string? independentlyTrustedLegacyEcologicalDigest = null,
        HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null) =>
        RestoreExternallyConfirmedCore(
            directoryPath, generatedWorld, independentlyTrustedSnapshot,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest,
            historicalVerifier, allowPublishedUnadmitted: false);

    // Accessible only to the failover gate inside the simulation assembly.
    // The marker remains on disk until the gate independently rechecks
    // primary + witness and completes strict admission.
    internal static EconomicCompleteV9DurableSaveHost
        RestoreExternallyConfirmedForPublication(
            string directoryPath, WorldGenerationResult generatedWorld,
            EconomicV9ExternalAnchorSnapshot independentlyTrustedSnapshot,
            WorldStateHash? independentlyTrustedLegacyWorldHash = null,
            string? independentlyTrustedLegacyEcologicalDigest = null,
            HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier = null) =>
        RestoreExternallyConfirmedCore(
            directoryPath, generatedWorld, independentlyTrustedSnapshot,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest,
            historicalVerifier, allowPublishedUnadmitted: true);

    private static EconomicCompleteV9DurableSaveHost
        RestoreExternallyConfirmedCore(
            string directoryPath, WorldGenerationResult generatedWorld,
            EconomicV9ExternalAnchorSnapshot independentlyTrustedSnapshot,
            WorldStateHash? independentlyTrustedLegacyWorldHash,
            string? independentlyTrustedLegacyEcologicalDigest,
            HistoricalMilitaryMoveAuthorizationSeal? historicalVerifier,
            bool allowPublishedUnadmitted)
    {
        var assessment = InspectExternalReconciliationCore(
            directoryPath, generatedWorld, independentlyTrustedSnapshot,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest,
            historicalVerifier, allowPublishedUnadmitted);
        if (assessment.State != EconomicV9ExternalReconciliationState.Confirmed)
            throw new InvalidOperationException(
                "Cannot resume v9 gameplay until external prepare is independently settled.");
        return RestoreCore(directoryPath, generatedWorld,
            assessment.Confirmed.CheckpointSha256Hex,
            independentlyTrustedLegacyWorldHash,
            independentlyTrustedLegacyEcologicalDigest,
            historicalVerifier, assessment.Confirmed.JournalSha256Hex,
            allowPublishedUnadmitted);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _lease.Dispose();
            _disposed = true;
        }
    }

    private void EnsureOpen()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EconomicCompleteV9DurableSaveHost));
    }

    private static Head NewHead(EconomicEcologicalCheckpoint checkpoint,
        bool migrated, string legacyWorld, string legacyEcology) =>
        new(checkpoint.WorldHash, checkpoint.Sha256Hex, ZeroDigest,
            checkpoint.NaturalCapital.LastAppliedTurn, 0UL,
            migrated, legacyWorld.ToUpperInvariant(),
            legacyEcology.ToUpperInvariant(), ZeroDigest);

    private static FileStream Acquire(string root) =>
        new(Path.Combine(root, ".full-v9-writer.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private static string HeadPath(string root) =>
        Path.Combine(root, "full-v9-head.gahead");

    private static void WriteHead(string path, Head head, bool overwrite)
    {
        var bytes = new byte[HeadBytes];
        Magic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), HeadVersion);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(12, 4),
            head.WorldHash.FormatVersion);
        Convert.FromHexString(head.WorldHash.HexDigest).CopyTo(bytes, 16);
        Convert.FromHexString(head.Current).CopyTo(bytes, 48);
        Convert.FromHexString(head.Previous).CopyTo(bytes, 80);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(112, 8), head.Turn);
        BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(120, 8), head.Sequence);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(128, 4),
            head.Migrated ? 1U : 0U);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(132, 4),
            head.Migrated ? 8U : 0U);
        Convert.FromHexString(head.LegacyWorld).CopyTo(bytes, 136);
        Convert.FromHexString(head.LegacyEcology).CopyTo(bytes, 168);
        Convert.FromHexString(head.JournalDigest).CopyTo(bytes, 200);
        SHA256.HashData(bytes.AsSpan(0, HeadBodyBytes))
            .CopyTo(bytes, HeadBodyBytes);

        var pending = Path.Combine(Path.GetDirectoryName(path)!,
            ".full-v9-head-pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(pending, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(pending, path, overwrite);
        }
        finally
        {
            if (File.Exists(pending))
                File.Delete(pending);
        }
    }

    private static Head ReadHead(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length != HeadBytes ||
            !bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Invalid complete-v9 save head.");
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(bytes.AsSpan(0, HeadBodyBytes)),
            bytes.AsSpan(HeadBodyBytes, 32)))
            throw new InvalidDataException("Complete-v9 save head checksum mismatch.");
        if (BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4))
            != HeadVersion)
            throw new NotSupportedException("Unsupported complete-v9 head version.");
        var format = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(12, 4));
        if (format != 9U)
            throw new NotSupportedException("Complete-v9 head requires WorldState hash v9.");

        var migrationFlag = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(128, 4));
        var legacyFormat = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(132, 4));
        if (migrationFlag > 1U || legacyFormat != (migrationFlag == 1U ? 8U : 0U))
            throw new InvalidDataException("Invalid migration ancestry.");
        var legacyWorld = Convert.ToHexString(bytes.AsSpan(136, 32));
        var legacyEcology = Convert.ToHexString(bytes.AsSpan(168, 32));
        if (migrationFlag == 0U &&
            (legacyWorld != ZeroDigest || legacyEcology != ZeroDigest))
            throw new InvalidDataException("Unmigrated save has unexpected ancestry.");

        var current = Convert.ToHexString(bytes.AsSpan(48, 32));
        var previous = Convert.ToHexString(bytes.AsSpan(80, 32));
        var sequence = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(120, 8));
        var journalDigest = Convert.ToHexString(bytes.AsSpan(200, 32));
        if (sequence == 0UL && previous != ZeroDigest ||
            sequence == 0UL && journalDigest != ZeroDigest ||
            sequence > 0UL && previous == ZeroDigest ||
            sequence > 0UL && journalDigest == ZeroDigest ||
            current == ZeroDigest)
            throw new InvalidDataException("Invalid complete-v9 head predecessor.");
        return new Head(new WorldStateHash(format,
            Convert.ToHexString(bytes.AsSpan(16, 32))), current, previous,
            BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(112, 8)),
            sequence, migrationFlag == 1U, legacyWorld, legacyEcology,
            journalDigest);
    }

    private readonly record struct Head(
        WorldStateHash WorldHash, string Current, string Previous,
        ulong Turn, ulong Sequence, bool Migrated,
        string LegacyWorld, string LegacyEcology, string JournalDigest);
}
