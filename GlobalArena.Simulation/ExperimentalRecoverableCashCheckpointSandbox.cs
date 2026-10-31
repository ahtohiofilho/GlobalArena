using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlobalArena.Simulation;

internal sealed record ExperimentalCashCheckpointAccount(string Key, string Balance);
internal sealed record ExperimentalCashCheckpointEntry(
    string Debit, string Credit, int Kind, ulong Commodity,
    ulong RawQuantity, string Amount);
internal sealed record ExperimentalCashCheckpointProof(
    string Actor, string Decision, ulong Candidate, ulong Turn, ulong Network,
    string World, string Snapshot, string Proposal, string Audit,
    string ClosingDigest, ulong Revision, int JournalEntries,
    string OpeningMoney, string ClosingMoney);
internal sealed record ExperimentalCashCheckpointDocument(
    int Schema, string Snapshot, ulong Turn, string World, ulong Network,
    ulong StateRevision, ExperimentalCashCheckpointAccount[] Accounts,
    ExperimentalCashCheckpointEntry[] Journal,
    ExperimentalCashCheckpointProof? Proof, string Checksum);

/// <summary>
/// GA-VS49: single-process, single-snapshot, isolated disk-checkpoint
/// sandbox. The file is NOT a Global Arena save and the receipt is not a
/// production payment authorization. A trusted authority and the ORIGINAL
/// input snapshot/selection are required to reopen and validate a commit.
///
/// Writes are same-directory staged with a flushed temp file and atomic-
/// replacement attempt. Actual power-loss durability (directory fsync,
/// filesystem/hardware guarantees), concurrent processes and external
/// effects remain OUT OF SCOPE. A crash may leave an orphan *.stage, which
/// cannot be interpreted as committed state. On reopen a checkpoint must
/// match a freshly re-audited VS48 settlement exactly or fail closed.
/// </summary>
public sealed class ExperimentalRecoverableCashCheckpointSandbox
{
    private const int SchemaVersion = 49;
    private const string Extension = ".ga-vs49-lab.json";
    private static readonly ConcurrentDictionary<string, object> PathLocks =
        new(OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly string _path;
    private readonly object _lock;
    private readonly ExperimentalCashAuthorizationSnapshot _snapshot;
    private readonly ExperimentalSelectedCashProposal _selected;
    private readonly IExperimentalCashSettlementAuthority _authority;
    private readonly Dictionary<ExperimentalCashAuditAccountKey,
        FoodTransportCostFraction> _opening;

    private ExperimentalRecoverableCashCheckpointSandbox(
        string path, ExperimentalCashAuthorizationSnapshot snapshot,
        ExperimentalSelectedCashProposal selected,
        IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction> opening,
        IExperimentalCashSettlementAuthority authority)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Only a dedicated .ga-vs49-lab.json sandbox path is permitted.",
                nameof(path));
        _path = Path.GetFullPath(path);
        _lock = PathLocks.GetOrAdd(_path, _ => new object());
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _selected = selected ?? throw new ArgumentNullException(nameof(selected));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        if (!ReferenceEquals(_snapshot, _selected.Snapshot) ||
            _selected.SnapshotDigest != _snapshot.ContentDigest)
            throw new InvalidOperationException(
                "The selected candidate must be bound to the original trusted snapshot.");
        ArgumentNullException.ThrowIfNull(opening);
        if (opening.Any(x => x.Value is null))
            throw new ArgumentException("Every cash opening balance must be explicit.",
                nameof(opening));
        _opening = new Dictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction>(opening);
    }

    public static ExperimentalRecoverableCashCheckpointSandbox Create(
        string path, ExperimentalCashAuthorizationSnapshot snapshot,
        ExperimentalSelectedCashProposal selected,
        IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction> opening,
        IExperimentalCashSettlementAuthority authority)
    {
        var store = new ExperimentalRecoverableCashCheckpointSandbox(
            path, snapshot, selected, opening, authority);
        lock (store._lock)
        {
            if (File.Exists(store._path))
                throw new IOException("An existing checkpoint cannot be overwritten.");
            var initial = store.ExpectedOpening();
            store.WriteAtomically(initial, replace: false);
            return store;
        }
    }

    /// <summary>
    /// On reopening a COMMITTED file, the caller must present the original
    /// trusted decision, and its authority must still approve that actor.
    /// The full posting is re-derived in a disposable in-memory VS48 lab;
    /// no debit occurs on disk during verification.
    /// </summary>
    public static ExperimentalRecoverableCashCheckpointSandbox Open(
        string path, ExperimentalCashAuthorizationSnapshot snapshot,
        ExperimentalSelectedCashProposal selected,
        IReadOnlyDictionary<ExperimentalCashAuditAccountKey,
            FoodTransportCostFraction> opening,
        IExperimentalCashSettlementAuthority authority,
        ExperimentalCashAuthorityDecision originalDecision)
    {
        var store = new ExperimentalRecoverableCashCheckpointSandbox(
            path, snapshot, selected, opening, authority);
        ArgumentNullException.ThrowIfNull(originalDecision);
        lock (store._lock)
        {
            _ = store.ReadVerified(originalDecision);
            return store;
        }
    }

    public ExperimentalCashSettlementOutcome Commit(
        ExperimentalCashAuthorityDecision decision,
        ulong currentTurn, string currentWorldHash,
        ulong currentNetworkRevision, ulong expectedStateRevision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        lock (_lock)
        {
            _snapshot.VerifyCurrent(currentTurn, currentWorldHash,
                currentNetworkRevision);

            var current = ReadVerified(decision);
            // Every attempt reauthorizes the exact decision, even on replay.
            // A non-authenticated record on disk is NOT a right to debit.
            var authorized = new ExperimentalAuditedCashCandidateAuthorization()
                .Authorize(_selected, decision, currentTurn,
                    currentWorldHash, currentNetworkRevision);
            if (!_authority.CanSettle(decision, _selected))
                throw new InvalidOperationException(
                    "The institutional settlement authority denied this decision.");

            if (current.StateRevision == 1)
            {
                var committed = current.Proof ??
                    throw new InvalidDataException("Missing committed proof.");
                if (committed.Decision != authorized.DecisionId ||
                    committed.Actor != authorized.ActorId ||
                    committed.Candidate != authorized.CandidateId ||
                    committed.Proposal != authorized.CandidateDigest ||
                    committed.Snapshot != authorized.SnapshotDigest ||
                    committed.Audit != authorized.AuditJournalDigest)
                    throw new InvalidOperationException(
                        "The persisted snapshot is already settled by another decision.");
                var outcome = ComputeExpected(decision);
                return new ExperimentalCashSettlementOutcome(outcome.Proof, true);
            }

            if (current.StateRevision != 0 || expectedStateRevision != 0)
                throw new InvalidOperationException(
                    "The checkpoint CAS revision is stale.");

            // Compute and audit on a disposable object first. If disk write
            // fails, this object is discarded, so no live state is published.
            var simulation = NewCoreLab();
            var result = simulation.Commit(_selected, decision,
                currentTurn, currentWorldHash, currentNetworkRevision, 0);
            var next = Format(simulation.Inspect(), result.Proof);
            WriteAtomically(next, replace: true);
            return result;
        }
    }

    public ExperimentalCashSettlementView Inspect(
        ExperimentalCashAuthorityDecision trustedDecision)
    {
        ArgumentNullException.ThrowIfNull(trustedDecision);
        lock (_lock)
        {
            var persisted = ReadVerified(trustedDecision);
            if (persisted.StateRevision == 0)
                return NewCoreLab().Inspect();
            var simulated = NewCoreLab();
            _ = simulated.Commit(_selected, trustedDecision,
                _snapshot.TurnOrdinal, _snapshot.WorldStateHash,
                _snapshot.NetworkRevision, 0);
            return simulated.Inspect();
        }
    }

    private ExperimentalAtomicCashSettlementLab NewCoreLab() =>
        new(_snapshot, _opening, _authority);

    private ExperimentalCashSettlementOutcome ComputeExpected(
        ExperimentalCashAuthorityDecision decision) =>
        NewCoreLab().Commit(_selected, decision,
            _snapshot.TurnOrdinal, _snapshot.WorldStateHash,
            _snapshot.NetworkRevision, 0);

    private ExperimentalCashCheckpointDocument ExpectedOpening() =>
        Format(NewCoreLab().Inspect(), proof: null);

    private ExperimentalCashCheckpointDocument ReadVerified(
        ExperimentalCashAuthorityDecision decision)
    {
        if (!File.Exists(_path))
            throw new FileNotFoundException("A committed checkpoint was not found.", _path);
        string text = File.ReadAllText(_path, Encoding.UTF8);
        ExperimentalCashCheckpointDocument persisted;
        try
        {
            persisted = JsonSerializer.Deserialize<ExperimentalCashCheckpointDocument>(
                text, JsonOptions) ??
                throw new InvalidDataException("The checkpoint is empty.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("The checkpoint is malformed.", e);
        }
        if (persisted.Schema != SchemaVersion ||
            persisted.Snapshot != _snapshot.ContentDigest ||
            persisted.Turn != _snapshot.TurnOrdinal ||
            persisted.World != _snapshot.WorldStateHash ||
            persisted.Network != _snapshot.NetworkRevision ||
            persisted.StateRevision > 1 ||
            persisted.Accounts is null || persisted.Journal is null ||
            persisted.Checksum is null)
            throw new InvalidDataException(
                "The checkpoint has an invalid version, provenance or structure.");

        // Reject malformed/corrupted JSON, duplicate/foreign fields and
        // noncanonical encodings. Digest alone is NOT authentication.
        if (!string.Equals(JsonSerializer.Serialize(persisted, JsonOptions),
                text, StringComparison.Ordinal) ||
            !string.Equals(persisted.Checksum, Checksum(persisted),
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "The checkpoint checksum or canonical representation is invalid.");

        ExperimentalCashCheckpointDocument expected;
        if (persisted.StateRevision == 0)
            expected = ExpectedOpening();
        else
        {
            // A file cannot assert that it committed any arbitrary money.
            // Revalidate it with the trusted original proposal, independent
            // audit, host authority and the same original opening balances.
            var simulation = NewCoreLab();
            var outcome = simulation.Commit(_selected, decision,
                _snapshot.TurnOrdinal, _snapshot.WorldStateHash,
                _snapshot.NetworkRevision, 0);
            expected = Format(simulation.Inspect(), outcome.Proof);
        }
        if (!string.Equals(
                JsonSerializer.Serialize(expected, JsonOptions), text,
                StringComparison.Ordinal))
            throw new InvalidDataException(
                "Persisted balances, journal or decision differ from the independently audited expected state.");
        return persisted;
    }

    private ExperimentalCashCheckpointDocument Format(
        ExperimentalCashSettlementView view,
        ExperimentalCashSettlementProof? proof)
    {
        var accounts = view.Accounts.Select(x =>
                new ExperimentalCashCheckpointAccount(
                    ExperimentalCashAuthorizationDigest.Key(x.Key),
                    ExperimentalCashAuthorizationDigest.Rational(x.Value)))
            .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        var entries = view.Journal.Select(x =>
            new ExperimentalCashCheckpointEntry(
                ExperimentalCashAuthorizationDigest.Key(x.Debit),
                ExperimentalCashAuthorizationDigest.Key(x.Credit),
                (int)x.Kind, x.Commodity.Value, x.RawQuantity,
                ExperimentalCashAuthorizationDigest.Rational(x.Amount))).ToArray();
        ExperimentalCashCheckpointProof? receipt = proof is null ? null :
            new ExperimentalCashCheckpointProof(
                proof.ActorId, proof.DecisionId, proof.CandidateId,
                proof.TurnOrdinal, proof.NetworkRevision, proof.WorldStateHash,
                proof.SnapshotDigest, proof.CandidateDigest,
                proof.AuditJournalDigest, proof.CommittedBalancesDigest,
                proof.CommittedStateRevision, proof.JournalEntries,
                ExperimentalCashAuthorizationDigest.Rational(proof.OpeningMoney),
                ExperimentalCashAuthorizationDigest.Rational(proof.ClosingMoney));
        var draft = new ExperimentalCashCheckpointDocument(
            SchemaVersion, _snapshot.ContentDigest, _snapshot.TurnOrdinal,
            _snapshot.WorldStateHash, _snapshot.NetworkRevision,
            view.StateRevision, accounts, entries, receipt, "");
        return draft with { Checksum = Checksum(draft) };
    }

    private static string Checksum(ExperimentalCashCheckpointDocument source) =>
        ExperimentalCashAuthorizationDigest.Hash(
            ["GA-VS49-CHECKPOINT-V1",
                JsonSerializer.Serialize(source with { Checksum = "" }, JsonOptions)]);

    private void WriteAtomically(ExperimentalCashCheckpointDocument checkpoint,
        bool replace)
    {
        var folder = Path.GetDirectoryName(_path)!;
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException(
                "The caller must create a dedicated VS49 laboratory directory.");
        var stage = _path + "." + Guid.NewGuid().ToString("N") + ".stage";
        var committed = false;
        try
        {
            using (var stream = new FileStream(stage, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                using var writer = new StreamWriter(stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    bufferSize: 1024, leaveOpen: true);
                writer.Write(JsonSerializer.Serialize(checkpoint, JsonOptions));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            if (replace)
            {
                if (!File.Exists(_path))
                    throw new InvalidOperationException(
                        "Cannot publish a settlement without its opening checkpoint.");
                File.Replace(stage, _path, destinationBackupFileName: null);
            }
            else
                File.Move(stage, _path); // fails if another file already exists
            committed = true;
        }
        finally
        {
            if (!committed && File.Exists(stage))
                File.Delete(stage);
        }
    }
}
