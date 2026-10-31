using System.Numerics;

namespace GlobalArena.Simulation;

public enum ExperimentalAuditedSelectionRule : byte
{
    /// <summary>
    /// Select the greatest total delivered fixed-point quantity; break ties
    /// by lowest independently audited freight, then lowest candidate ID.
    /// This does not assert a welfare optimum or cross-commodity equivalence.
    /// </summary>
    MaxDeliveredRawVolumeThenLowestFreight = 1,

    /// <summary>
    /// Only compare proposals carrying exactly the same aggregate fixed-point
    /// quantity; choose minimum independently audited freight, then ID.
    /// A different volume makes the whole selection request ineligible.
    /// </summary>
    LowestFreightAtEqualRawVolume = 2
}

public sealed class ExperimentalAuditedSelectionPolicy
{
    public string PolicyId { get; }
    public ulong PolicyVersion { get; }
    public ExperimentalAuditedSelectionRule Rule { get; }

    public ExperimentalAuditedSelectionPolicy(
        string policyId, ulong policyVersion,
        ExperimentalAuditedSelectionRule rule)
    {
        if (string.IsNullOrWhiteSpace(policyId) ||
            policyVersion == 0 || !Enum.IsDefined(rule))
            throw new ArgumentException(
                "Selection requires an explicit institutional policy ID, version and supported rule.");
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        Rule = rule;
    }
}

public sealed record ExperimentalAuditedCandidate(
    ulong CandidateId, ExperimentalSimultaneousFreightAllocation Allocation);

public sealed class ExperimentalSelectedCashProposal
{
    internal ExperimentalSimultaneousFreightAllocation Allocation { get; }
    public ExperimentalCashAuthorizationSnapshot Snapshot { get; }
    public ExperimentalAuditedSelectionPolicy Policy { get; }
    public ulong CandidateId { get; }
    public string CandidateDigest { get; }
    public string SnapshotDigest { get; }
    public ExperimentalCashAuditResult Audit { get; }
    public BigInteger DeliveredRawQuantity { get; }

    internal ExperimentalSelectedCashProposal(
        ExperimentalCashAuthorizationSnapshot snapshot,
        ExperimentalAuditedSelectionPolicy policy,
        ExperimentalAuditedCandidate candidate,
        ExperimentalCashAuditResult audit,
        string digest, BigInteger deliveredRawQuantity)
    {
        Snapshot = snapshot;
        Policy = policy;
        CandidateId = candidate.CandidateId;
        Allocation = candidate.Allocation;
        Audit = audit;
        SnapshotDigest = snapshot.ContentDigest;
        CandidateDigest = digest;
        DeliveredRawQuantity = deliveredRawQuantity;
    }
}

/// <summary>
/// Intent expressed by an external policy authority, NOT an authenticated
/// signature. The caller must authenticate ActorId and prevent reusing
/// DecisionId at the real permission/settlement boundary.
/// </summary>
public sealed record ExperimentalCashAuthorityDecision(
    string ActorId,
    string DecisionId,
    bool Approves,
    ulong ExpectedTurnOrdinal,
    string ExpectedSnapshotDigest,
    string ExpectedCandidateDigest,
    ulong ExpectedCandidateId,
    string ExpectedPolicyId,
    ulong ExpectedPolicyVersion);

/// <summary>
/// An informational authorization certificate, not a spendable financial
/// credential: no state, balance, inventory, event or persistent account has
/// been changed, and identity authentication remains external.
/// </summary>
public sealed record ExperimentalCashAuthorizationReceipt(
    string ActorId,
    string DecisionId,
    ulong TurnOrdinal,
    ulong NetworkRevision,
    string WorldStateHash,
    string SnapshotDigest,
    string CandidateDigest,
    ulong CandidateId,
    string PolicyId,
    ulong PolicyVersion,
    string AuditJournalDigest);

/// <summary>
/// AUDIT -> SELECT -> explicit AUTHORIZE, all read-only. Any non-audited
/// candidate makes the entire selection fail closed. Selection rules use
/// only independently reconstructed quantities and monetary freight; the
/// candidate's self-reported net economic value is deliberately NOT used
/// for ranking because VS46 does not independently certify utility.
/// </summary>
public sealed class ExperimentalAuditedCashCandidateAuthorization
{
    private sealed record Verified(
        ExperimentalAuditedCandidate Candidate,
        ExperimentalCashAuditResult Audit,
        BigInteger Quantity,
        string Digest);

    public ExperimentalSelectedCashProposal Select(
        ExperimentalCashAuthorizationSnapshot snapshot,
        IEnumerable<ExperimentalAuditedCandidate> candidates,
        ExperimentalAuditedSelectionPolicy policy,
        ulong currentTurn, string currentWorldHash,
        ulong currentNetworkRevision)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(policy);
        snapshot.VerifyCurrent(currentTurn, currentWorldHash,
            currentNetworkRevision);

        var supplied = candidates.ToArray();
        if (supplied.Length == 0 ||
            supplied.Any(x => x is null || x.CandidateId == 0 ||
                x.Allocation is null) ||
            supplied.Select(x => x.CandidateId).Distinct().Count() !=
                supplied.Length)
            throw new ArgumentException(
                "Selection requires unique, nonzero, complete candidate identities.",
                nameof(candidates));

        var verified = new List<Verified>(supplied.Length);
        foreach (var candidate in supplied)
        {
            // Independent VS46 accounting. Any false ledger, stale cash or
            // invalid route aborts the whole batch, not just this proposal.
            var audit = snapshot.Audit(candidate.Allocation);
            var delivered = candidate.Allocation.Trades.Aggregate(
                BigInteger.Zero, (sum, trade) => sum + trade.RawQuantity);
            var fingerprint = snapshot.Fingerprint(candidate.Allocation, audit);
            verified.Add(new Verified(candidate, audit, delivered, fingerprint));
        }

        if (policy.Rule == ExperimentalAuditedSelectionRule.LowestFreightAtEqualRawVolume &&
            verified.Any(x => x.Quantity != verified[0].Quantity))
            throw new InvalidOperationException(
                "A lowest-freight comparison cannot silently trade away delivery volume.");

        var order = policy.Rule switch
        {
            ExperimentalAuditedSelectionRule.MaxDeliveredRawVolumeThenLowestFreight =>
                verified.OrderByDescending(x => x.Quantity)
                    .ThenBy(x => x.Audit.TotalFreight,
                        Comparer<FoodTransportCostFraction>.Create(
                            (a, b) => a.CompareTo(b)))
                    .ThenBy(x => x.Candidate.CandidateId),
            ExperimentalAuditedSelectionRule.LowestFreightAtEqualRawVolume =>
                verified.OrderBy(x => x.Audit.TotalFreight,
                        Comparer<FoodTransportCostFraction>.Create(
                            (a, b) => a.CompareTo(b)))
                    .ThenBy(x => x.Candidate.CandidateId),
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
        var selected = order.First();
        return new ExperimentalSelectedCashProposal(snapshot, policy,
            selected.Candidate, selected.Audit,
            selected.Digest, selected.Quantity);
    }

    public ExperimentalCashAuthorizationReceipt Authorize(
        ExperimentalSelectedCashProposal selected,
        ExperimentalCashAuthorityDecision authority,
        ulong currentTurn, string currentWorldHash,
        ulong currentNetworkRevision)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(authority);
        selected.Snapshot.VerifyCurrent(
            currentTurn, currentWorldHash, currentNetworkRevision);
        if (!authority.Approves ||
            string.IsNullOrWhiteSpace(authority.ActorId) ||
            string.IsNullOrWhiteSpace(authority.DecisionId) ||
            authority.ExpectedTurnOrdinal != currentTurn ||
            authority.ExpectedCandidateId != selected.CandidateId ||
            !string.Equals(authority.ExpectedSnapshotDigest,
                selected.SnapshotDigest, StringComparison.Ordinal) ||
            !string.Equals(authority.ExpectedCandidateDigest,
                selected.CandidateDigest, StringComparison.Ordinal) ||
            !string.Equals(authority.ExpectedPolicyId,
                selected.Policy.PolicyId, StringComparison.Ordinal) ||
            authority.ExpectedPolicyVersion != selected.Policy.PolicyVersion)
            throw new InvalidOperationException(
                "Authorization requires a positive, explicit policy decision bound to the audited snapshot and exact candidate.");

        // Re-audit rather than trusting the selected record, in case a caller
        // retained and modified a mutable input collection after Select.
        var fresh = selected.Snapshot.Audit(selected.Allocation);
        var fingerprint = selected.Snapshot.Fingerprint(selected.Allocation, fresh);
        if (!string.Equals(fingerprint, selected.CandidateDigest,
            StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The selected candidate changed after its independent audit.");

        var journal = ExperimentalCashAuthorizationDigest.Hash(
            new[] { "AUDITED-JOURNAL", selected.SnapshotDigest,
                selected.CandidateDigest,
                ExperimentalCashAuthorizationDigest.Rational(fresh.TotalOpening),
                ExperimentalCashAuthorizationDigest.Rational(fresh.TotalClosing) });
        return new ExperimentalCashAuthorizationReceipt(
            authority.ActorId, authority.DecisionId,
            currentTurn, currentNetworkRevision, currentWorldHash,
            selected.SnapshotDigest, selected.CandidateDigest,
            selected.CandidateId, selected.Policy.PolicyId,
            selected.Policy.PolicyVersion, journal);
    }
}
