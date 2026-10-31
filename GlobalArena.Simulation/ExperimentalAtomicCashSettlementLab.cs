using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace GlobalArena.Simulation;

/// <summary>
/// The host, not this in-memory laboratory, must authenticate actors and
/// decide whether a particular signed/approved decision has permission to
/// settle this exact economic snapshot. There is deliberately NO allow-all
/// default; a VS47 informational receipt alone is NOT permission.
/// </summary>
public interface IExperimentalCashSettlementAuthority
{
    bool CanSettle(
        ExperimentalCashAuthorityDecision decision,
        ExperimentalSelectedCashProposal selected);
}

public sealed record ExperimentalCashSettlementProof(
    string ActorId,
    string DecisionId,
    ulong CandidateId,
    ulong TurnOrdinal,
    ulong NetworkRevision,
    string WorldStateHash,
    string SnapshotDigest,
    string CandidateDigest,
    string AuditJournalDigest,
    string CommittedBalancesDigest,
    ulong CommittedStateRevision,
    int JournalEntries,
    FoodTransportCostFraction OpeningMoney,
    FoodTransportCostFraction ClosingMoney);

public sealed record ExperimentalCashSettlementOutcome(
    ExperimentalCashSettlementProof Proof,
    bool WasAlreadyCommitted);

public sealed record ExperimentalCashSettlementView(
    ulong StateRevision,
    string SnapshotDigest,
    string? CommittedDecisionId,
    IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
        FoodTransportCostFraction> Accounts,
    IReadOnlyList<ExperimentalCashAuditTransfer> Journal);

/// <summary>
/// VS48 laboratory SETTLE prototype: one atomic commit per immutable economic
/// snapshot, with a snapshot-revision compare-and-swap and idempotent replay.
/// No production WorldState, save format, network, inventory or historical
/// money ledger is mutated. In-process lock protects the entire PREPARE -> 
/// VERIFY -> COMMIT transition, including the single-use decision registry.
///
/// The caller supplies the whole OPENING subledger, including any producer
/// or carrier balances. The independent VS46 journal must match its opening
/// household balances. Previously owned producer/carrier money is not
/// reset to zero; beneficiary accounts absent from the supplied laboratory
/// subledger are explicitly initialized with zero.
/// </summary>
public sealed class ExperimentalAtomicCashSettlementLab
{
    private readonly object _gate = new();
    private readonly ExperimentalCashAuthorizationSnapshot _snapshot;
    private readonly IExperimentalCashSettlementAuthority _authority;
    private Dictionary<ExperimentalCashAuditAccountKey, FoodTransportCostFraction> _accounts;
    private readonly Dictionary<string, ExperimentalCashSettlementProof> _decisions =
        new(StringComparer.Ordinal);
    private ExperimentalCashAuditTransfer[] _journal = [];
    private string? _committedDecision;
    private ulong _version;

    public ExperimentalAtomicCashSettlementLab(
        ExperimentalCashAuthorizationSnapshot snapshot,
        IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction> openingAccounts,
        IExperimentalCashSettlementAuthority authority)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        ArgumentNullException.ThrowIfNull(openingAccounts);
        if (openingAccounts.Any(x => x.Value is null))
            throw new ArgumentException("All opening cash balances must be explicit.",
                nameof(openingAccounts));
        _accounts = new Dictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction>(openingAccounts);
    }

    public ExperimentalCashSettlementView Inspect()
    {
        lock (_gate)
            return new ExperimentalCashSettlementView(
                _version, _snapshot.ContentDigest, _committedDecision,
                new ReadOnlyDictionary<ExperimentalCashAuditAccountKey,
                    FoodTransportCostFraction>(
                    new Dictionary<ExperimentalCashAuditAccountKey,
                        FoodTransportCostFraction>(_accounts)),
                Array.AsReadOnly((ExperimentalCashAuditTransfer[])_journal.Clone()));
    }

    /// <summary>
    /// A replay with the same, independently revalidated decision returns the
    /// ORIGINAL proof and does not post a journal again. A different decision
    /// for an already-settled snapshot fails, even if its economic outcome
    /// would be identical. All failure exits occur before state replacement.
    /// </summary>
    public ExperimentalCashSettlementOutcome Commit(
        ExperimentalSelectedCashProposal selected,
        ExperimentalCashAuthorityDecision decision,
        ulong currentTurn, string currentWorldHash,
        ulong currentNetworkRevision, ulong expectedStateRevision)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(decision);

        lock (_gate)
        {
            if (!string.Equals(selected.SnapshotDigest,
                    _snapshot.ContentDigest, StringComparison.Ordinal) ||
                !ReferenceEquals(selected.Snapshot, _snapshot))
                throw new InvalidOperationException(
                    "The settlement candidate must originate from this exact snapshot.");
            _snapshot.VerifyCurrent(
                currentTurn, currentWorldHash, currentNetworkRevision);

            // VS47 authorizes a digest-bound decision and redoes VS46 AUDIT.
            // Neither the digest nor the informational receipt authenticates
            // the actor; the injected institution must grant that capability.
            var authorized = new ExperimentalAuditedCashCandidateAuthorization()
                .Authorize(selected, decision, currentTurn,
                    currentWorldHash, currentNetworkRevision);
            if (!_authority.CanSettle(decision, selected))
                throw new InvalidOperationException(
                    "The authenticated institutional settlement authority denied this decision.");

            if (_decisions.TryGetValue(decision.DecisionId, out var previous))
            {
                if (previous.ActorId != authorized.ActorId ||
                    previous.CandidateId != authorized.CandidateId ||
                    previous.CandidateDigest != authorized.CandidateDigest ||
                    previous.SnapshotDigest != authorized.SnapshotDigest ||
                    previous.AuditJournalDigest != authorized.AuditJournalDigest)
                    throw new InvalidOperationException(
                        "A settlement decision ID cannot be reused for a different authorization.");
                return new ExperimentalCashSettlementOutcome(previous, true);
            }

            if (_committedDecision is not null)
                throw new InvalidOperationException(
                    "The same economic snapshot cannot settle a second independent decision.");
            if (_version != expectedStateRevision)
                throw new InvalidOperationException(
                    "Stale compare-and-swap state revision; nothing was committed.");

            // Reconstruct from the independently audited, frozen proposal;
            // do not rely on mutable, caller-reported invoice totals.
            var audit = _snapshot.Audit(selected.Allocation);
            var rebound = _snapshot.Fingerprint(selected.Allocation, audit);
            if (!string.Equals(rebound, authorized.CandidateDigest,
                StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The journal no longer matches its authorized proposal.");
            var expectedJournalDigest = ExperimentalCashAuthorizationDigest.Hash(
                ["AUDITED-JOURNAL", selected.SnapshotDigest,
                    selected.CandidateDigest,
                    ExperimentalCashAuthorizationDigest.Rational(audit.TotalOpening),
                    ExperimentalCashAuthorizationDigest.Rational(audit.TotalClosing)]);
            if (expectedJournalDigest != authorized.AuditJournalDigest)
                throw new InvalidOperationException(
                    "The authorized journal summary is inconsistent.");

            foreach (var (account, amount) in audit.Opening)
            {
                if (!_accounts.TryGetValue(account, out var funded) ||
                    funded.CompareTo(amount) != 0)
                    throw new InvalidOperationException(
                        "Opening household accounts diverged from the audited cash snapshot.");
            }

            var before = Total(_accounts.Values);
            var prepared = new Dictionary<ExperimentalCashAuditAccountKey,
                FoodTransportCostFraction>(_accounts);
            foreach (var transfer in audit.Transfers)
            {
                if (transfer.Debit == transfer.Credit)
                    throw new InvalidOperationException(
                        "No journal entry may debit and credit the same role-scoped account.");
                if (!prepared.TryGetValue(transfer.Debit, out var debit))
                    throw new InvalidOperationException(
                        "A journal cannot withdraw from an undeclared account.");
                if (debit.CompareTo(transfer.Amount) < 0)
                    throw new InvalidOperationException(
                        "The prepared debit would overdraw a prefunded account.");
                prepared[transfer.Debit] = Subtract(debit, transfer.Amount);
                prepared.TryGetValue(transfer.Credit, out var credit);
                prepared[transfer.Credit] = Add(
                    credit ?? new FoodTransportCostFraction(0, 1),
                    transfer.Amount);
            }

            foreach (var buyer in audit.Buyers)
            {
                var key = ExperimentalCashAuditAccountKey.Household(buyer.Buyer);
                if (!prepared.TryGetValue(key, out var final) ||
                    final.CompareTo(buyer.ProtectedH) < 0 ||
                    final.CompareTo(buyer.ClosingGrossCash) != 0)
                    throw new InvalidOperationException(
                        "A prepared cash settlement must preserve H and exact household debits.");
            }

            var after = Total(prepared.Values);
            if (before.CompareTo(after) != 0)
                throw new InvalidOperationException(
                    "The complete prepared journal must conserve money.");
            var committedRevision = checked(_version + 1UL);
            var closingDigest = ExperimentalCashAuthorizationDigest.Hash(
                prepared
                    .Select(x => ExperimentalCashAuthorizationDigest.Key(x.Key) +
                        "=" + ExperimentalCashAuthorizationDigest.Rational(x.Value))
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .Prepend("SETTLEMENT-BALANCES-V1")
                    .Prepend(_snapshot.ContentDigest));
            var proof = new ExperimentalCashSettlementProof(
                authorized.ActorId, authorized.DecisionId,
                authorized.CandidateId, currentTurn, currentNetworkRevision,
                currentWorldHash, authorized.SnapshotDigest,
                authorized.CandidateDigest, authorized.AuditJournalDigest,
                closingDigest, committedRevision, audit.Transfers.Count,
                before, after);

            // The only mutation point, after all checking and allocation.
            // No await, I/O, callbacks or external state touches after this.
            _accounts = prepared;
            _journal = audit.Transfers.ToArray();
            _version = committedRevision;
            _committedDecision = decision.DecisionId;
            _decisions.Add(decision.DecisionId, proof);
            return new ExperimentalCashSettlementOutcome(proof, false);
        }
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static FoodTransportCostFraction Subtract(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator - b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static FoodTransportCostFraction Total(
        IEnumerable<FoodTransportCostFraction> items) =>
        items.Aggregate(new FoodTransportCostFraction(0, 1), Add);
}
