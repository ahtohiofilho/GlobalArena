using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// An observable receipt for one accepted physical command. A tick only
/// contains a settlement when it closes the current six-movement window.
/// This journal is in-memory; it is not a canonical persisted event stream.
/// </summary>
public sealed record PhysicalEconomyCanonicalCommitReceipt(
    ulong Sequence,
    string CommandKind,
    ulong PreviousRevision,
    ulong ResultingRevision,
    WorldStateHash PreviousHash,
    WorldStateHash ResultingHash,
    ulong MovementTurn,
    ulong EconomicCycles,
    TacticalPrimitiveEconomicCycle? Settlement);

/// <summary>
/// Transitional single-writer command gateway for the physical VS07 resolver.
///
/// Commands run on a detached replay-verified candidate. Only after the
/// candidate, world binding, and hash have been validated is its complete
/// WorldState revision accepted. Failed and repeated commands cannot mutate
/// the accepted physical simulation, stocks, turn, or canonical snapshot.
///
/// IMPORTANT: this does not replace the primitive offline production
/// algorithm with a pure canonical reducer and does not implement canonical
/// V9 persistence. The owner must not write to CanonicalGameSession through
/// another path while this gate is active.
/// </summary>
public sealed class PhysicalEconomyCanonicalCommandGate
{
    private readonly CanonicalGameSession _session;
    private readonly CanonicalWorldStateHasher _hasher = new();
    private readonly List<PhysicalEconomyCanonicalCommitReceipt> _journal = new();
    private readonly object _sync = new();
    private EconomicPhysicalTacticalOfflineSession _offline;
    private WorldState _committed;

    public ulong NextSequence { get; private set; } = 1UL;
    public WorldState Snapshot => _committed;
    public IReadOnlyList<PhysicalEconomyCanonicalCommitReceipt> Journal
    {
        get
        {
            lock (_sync)
                return Array.AsReadOnly(_journal.ToArray());
        }
    }

    public PhysicalEconomyCanonicalCommandGate(
        CanonicalGameSession session,
        EconomicPhysicalTacticalOfflineSession initial)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(initial);

        // Never retain the mutable producer supplied by the caller.
        var candidate = EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
            initial.ExportSaveJson());
        var baseline = _session.Snapshot;
        var staged = PhysicalEconomyWorldStatePublisher.Stage(
            baseline, _session.GeneratedWorld, candidate);
        _ = _hasher.Compute(staged);

        if (!ReferenceEquals(_session.Snapshot, baseline))
            throw new InvalidOperationException("Canonical session changed during physical gate initialization.");

        _session.AcceptSnapshot(staged);
        _committed = staged;
        _offline = candidate;
    }

    /// <summary>
    /// A defensive reconstruction for the HUD only. Mutating it never writes
    /// to this gate or to the canonical game session.
    /// </summary>
    public EconomicPhysicalTacticalOfflineSession CopyForPresentation()
    {
        lock (_sync)
            return EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
                _offline.ExportSaveJson());
    }

    /// <summary>
    /// Legacy offline replay format, retained solely for the laboratory.
    /// This is NOT a canonical WorldState V9 save.
    /// </summary>
    public string ExportOfflineSaveJson()
    {
        lock (_sync)
            return _offline.ExportSaveJson();
    }

    public PhysicalEconomyCanonicalCommitReceipt MoveWorkers(
        ulong sequence,
        ulong expectedRevision,
        PhysicalTacticalTileId source,
        PhysicalTacticalTileId destination,
        EconomicActivityKind sourceActivity,
        EconomicActivityKind destinationActivity,
        ulong workers)
    {
        return Commit(sequence, expectedRevision, "move", candidate =>
        {
            candidate.MoveWorkers(source, destination, sourceActivity,
                destinationActivity, workers);
            return (candidate, null);
        });
    }

    public PhysicalEconomyCanonicalCommitReceipt AdvanceMovementTurn(
        ulong sequence,
        ulong expectedRevision)
    {
        return Commit(sequence, expectedRevision, "tick", candidate =>
        {
            var settlement = candidate.AdvanceMovementTurn();
            return (candidate, settlement);
        });
    }

    /// <summary>
    /// Explicit historical restore (possibly to an earlier movement turn).
    /// Reconstructs and validates the entire save before committing one new
    /// canonical revision; does not replay the same economic cycle twice.
    /// </summary>
    public PhysicalEconomyCanonicalCommitReceipt RestoreOfflineSave(
        ulong sequence, ulong expectedRevision, string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Commit(sequence, expectedRevision, "restore", _ =>
            (EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(json), null));
    }

    private PhysicalEconomyCanonicalCommitReceipt Commit(
        ulong sequence,
        ulong expectedRevision,
        string kind,
        Func<EconomicPhysicalTacticalOfflineSession,
            (EconomicPhysicalTacticalOfflineSession Next,
             TacticalPrimitiveEconomicCycle? Settlement)> apply)
    {
        lock (_sync)
        {
            if (sequence != NextSequence)
                throw new InvalidOperationException(
                    "Physical command is repeated or is not the next sequence.");
            if (expectedRevision != _committed.Revision ||
                !ReferenceEquals(_session.Snapshot, _committed))
                throw new InvalidOperationException(
                    "Physical command was planned against a stale or foreign canonical revision.");

            // Perform overflow checks before publication.
            var followingSequence = checked(sequence + 1UL);
            var previousHash = _hasher.Compute(_committed);
            var detached = EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
                _offline.ExportSaveJson());
            var (nextOffline, settlement) = apply(detached);

            var nextWorld = PhysicalEconomyWorldStatePublisher.Stage(
                _committed, _session.GeneratedWorld, nextOffline);
            var nextHash = _hasher.Compute(nextWorld);
            var receipt = new PhysicalEconomyCanonicalCommitReceipt(
                sequence, kind, _committed.Revision, nextWorld.Revision,
                previousHash, nextHash, nextOffline.MovementTurn,
                nextOffline.EconomicCycles, settlement);

            if (!ReferenceEquals(_session.Snapshot, _committed))
                throw new InvalidOperationException(
                    "Canonical session changed before physical command publication.");

            _session.AcceptSnapshot(nextWorld);
            _offline = nextOffline;
            _committed = nextWorld;
            _journal.Add(receipt);
            NextSequence = followingSequence;
            return receipt;
        }
    }
}
