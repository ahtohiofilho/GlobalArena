using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// An anchor supplied by an independently protected authority, not reconstructed
/// from the local GAFULL09 directory or its head. Sequence and turn are bound
/// alongside the checkpoint and complete input-journal root.
/// </summary>
public sealed record EconomicV9ExternalAnchor
{
    private static readonly string Zero = new('0', 64);

    public string CheckpointSha256Hex { get; }
    public string JournalSha256Hex { get; }
    public ulong Sequence { get; }
    public ulong Turn { get; }

    public EconomicV9ExternalAnchor(
        string checkpointSha256Hex, string journalSha256Hex,
        ulong sequence, ulong turn)
    {
        CheckpointSha256Hex =
            EconomicEcologicalSidecarArchive.NormalizeDigest(checkpointSha256Hex);
        JournalSha256Hex =
            EconomicEcologicalSidecarArchive.NormalizeDigest(journalSha256Hex);
        if (CheckpointSha256Hex == Zero ||
            (sequence == 0UL) != (JournalSha256Hex == Zero))
            throw new ArgumentException("External v9 anchor has an invalid root or sequence.");
        Sequence = sequence;
        Turn = turn;
    }
}

/// <summary>
/// A durable, independently authorized prepare for exactly ONE transition.
/// This is a trusted external intent, never an inference from orphaned files.
/// </summary>
public sealed record EconomicV9PreparedExternalAdvance
{
    public EconomicV9ExternalAnchor Before { get; }
    public EconomicV9ExternalAnchor After { get; }

    public EconomicV9PreparedExternalAdvance(
        EconomicV9ExternalAnchor before, EconomicV9ExternalAnchor after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Sequence == ulong.MaxValue || before.Turn == ulong.MaxValue ||
            after.Sequence != before.Sequence + 1UL ||
            after.Turn != before.Turn + 1UL ||
            after.CheckpointSha256Hex == before.CheckpointSha256Hex ||
            after.JournalSha256Hex == before.JournalSha256Hex)
            throw new ArgumentException("External prepare must describe one new turn.");
        Before = before;
        After = after;
    }
}

/// <summary>
/// Snapshot read atomically from an independently trusted, CAS-capable store.
/// A pending transition must reference the same confirmed anchor.
/// </summary>
public sealed record EconomicV9ExternalAnchorSnapshot
{
    public EconomicV9ExternalAnchor Confirmed { get; }
    public EconomicV9PreparedExternalAdvance? Prepared { get; }

    public EconomicV9ExternalAnchorSnapshot(
        EconomicV9ExternalAnchor confirmed,
        EconomicV9PreparedExternalAdvance? prepared = null)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        if (prepared is not null && prepared.Before != confirmed)
            throw new ArgumentException(
                "Prepared transition does not descend from the independently confirmed anchor.");
        Confirmed = confirmed;
        Prepared = prepared;
    }
}

public enum EconomicV9ExternalReconciliationState
{
    /// <summary>Local head and external confirmed anchor match; no pending prepare.</summary>
    Confirmed,

    /// <summary>External prepare exists but the local head is still the old head.</summary>
    PreparedWithoutLocalPublication,

    /// <summary>
    /// Local head is the externally prepared successor, fully replayed, but
    /// not yet externally confirmed. Caller must finalize via external CAS.
    /// </summary>
    LocalPublicationAwaitingExternalConfirmation
}

public sealed record EconomicV9ExternalReconciliationAssessment(
    EconomicV9ExternalReconciliationState State,
    EconomicV9ExternalAnchor VerifiedLocal,
    EconomicV9ExternalAnchor Confirmed,
    EconomicV9PreparedExternalAdvance? Prepared);
