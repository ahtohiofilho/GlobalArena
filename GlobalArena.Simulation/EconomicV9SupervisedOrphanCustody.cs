using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlobalArena.Simulation;

/// <summary>
/// Binds explicit signed operator approval to ONE exact crashed quarantine
/// and ONE archival destination. It never authorizes gameplay promotion,
/// cleanup, rollback, or deletion.
/// </summary>
public sealed record EconomicV9OrphanCustodyPlan(
    string CaseId, string GameId, long FormerWriterEpoch,
    string QuarantineName, string VaultName, string TreeSha256Hex,
    int FileCount, long TotalBytes);

public sealed record EconomicV9OrphanCustodyApproval(
    EconomicV9OrphanCustodyPlan Plan,
    string OperatorId, long IssuedAtUnixSeconds, long ExpiresAtUnixSeconds,
    string SignatureBase64);

public enum EconomicV9OrphanCustodyOutcome
{
    Archived,
    ReconciledAfterInterruption
}

public sealed record EconomicV9OrphanCustodyAuditEntry(
    string CaseId, string Event, string GameId,
    string QuarantineName, string VaultName, string TreeSha256Hex,
    string OperatorId, long RecordedAtUnixSeconds);

public interface IEconomicV9OrphanCustodyAuditSink
{
    // The sink must synchronously persist or throw. Production needs an
    // independently administered append-only destination, not just this
    // machine's local disk.
    void Append(EconomicV9OrphanCustodyAuditEntry entry);
}

public sealed class EconomicV9LocalOrphanCustodyAuditSink :
    IEconomicV9OrphanCustodyAuditSink
{
    private readonly string _filePath;
    public EconomicV9LocalOrphanCustodyAuditSink(string filePath) =>
        _filePath = Path.GetFullPath(filePath);

    public void Append(EconomicV9OrphanCustodyAuditEntry entry)
    {
        var json = JsonSerializer.Serialize(entry);
        using var file = new FileStream(_filePath, FileMode.Append,
            FileAccess.Write, FileShare.Read);
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        file.Write(bytes);
        file.Flush(flushToDisk: true);
    }
}

internal enum EconomicV9OrphanCustodyPhase
{
    IntentPersisted,
    DirectoryMoved
}

internal interface IEconomicV9OrphanCustodyProbe
{
    void OnPhase(EconomicV9OrphanCustodyPhase phase);
}

/// <summary>
/// Operator-only, non-destructive chain-of-custody for a crashed backup
/// staging directory. Requires explicit ECDSA approval, independently
/// attested former-writer isolation, same-parent atomic rename and
/// a durable synchronous audit sink. The source NEVER becomes a save.
/// </summary>
public sealed class EconomicV9SupervisedOrphanCustody : IDisposable
{
    private const int MaximumFiles = 1024;
    private const long MaximumBytes = 512L * 1024 * 1024;
    private readonly string _parent;
    private readonly string _vault;
    private readonly ECDsa _authorizedOperatorKey;
    private readonly IEconomicV9FormerWriterIsolationVerifier _isolation;
    private readonly IEconomicV9OrphanCustodyAuditSink _audit;
    private readonly TimeProvider _clock;
    private readonly object _signatureGate = new();

    public EconomicV9SupervisedOrphanCustody(
        string preprovisionedRecoveryParent, string forensicVaultName,
        string pinnedOperatorApprovalPublicKeyPem,
        IEconomicV9FormerWriterIsolationVerifier independentFencing,
        IEconomicV9OrphanCustodyAuditSink durableAudit,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(preprovisionedRecoveryParent);
        ValidateSimpleName(forensicVaultName);
        _parent = Path.GetFullPath(preprovisionedRecoveryParent);
        _vault = Path.Combine(_parent, forensicVaultName);
        if (!Directory.Exists(_parent) || !Directory.Exists(_vault) ||
            (File.GetAttributes(_parent) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(_vault) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("A trusted, existing non-symlink parent and vault are required.");
        _isolation = independentFencing ??
            throw new ArgumentNullException(nameof(independentFencing));
        _audit = durableAudit ?? throw new ArgumentNullException(nameof(durableAudit));
        _clock = clock ?? TimeProvider.System;
        _authorizedOperatorKey = ECDsa.Create();
        try
        {
            _authorizedOperatorKey.ImportFromPem(pinnedOperatorApprovalPublicKeyPem);
            if (_authorizedOperatorKey.KeySize != 256)
                throw new CryptographicException("Operator key must be P-256.");
        }
        catch
        {
            _authorizedOperatorKey.Dispose();
            throw;
        }
    }

    public EconomicV9OrphanCustodyPlan Plan(
        string caseId, string gameId, long formerWriterEpoch,
        string quarantineName)
    {
        ValidateCaseId(caseId);
        ValidateSimpleName(gameId);
        ValidateQuarantineName(quarantineName);
        if (formerWriterEpoch <= 0)
            throw new ArgumentOutOfRangeException(nameof(formerWriterEpoch));
        var source = Path.Combine(_parent, quarantineName);
        var (digest, files, size) = DigestTree(source);
        return new EconomicV9OrphanCustodyPlan(
            caseId, gameId, formerWriterEpoch, quarantineName,
            caseId, digest, files, size);
    }

    public EconomicV9OrphanCustodyOutcome ArchiveOnly(
        EconomicV9OrphanCustodyApproval approval) =>
        ArchiveOnlyObserved(approval, null);

    internal EconomicV9OrphanCustodyOutcome ArchiveOnlyObserved(
        EconomicV9OrphanCustodyApproval approval,
        IEconomicV9OrphanCustodyProbe? probe)
    {
        ArgumentNullException.ThrowIfNull(approval);
        var plan = approval.Plan ??
            throw new InvalidDataException("Missing approved quarantine plan.");
        ValidateCaseId(plan.CaseId);
        ValidateSimpleName(plan.GameId);
        ValidateQuarantineName(plan.QuarantineName);
        if (plan.VaultName != plan.CaseId ||
            plan.FormerWriterEpoch <= 0 ||
            plan.FileCount is < 1 or > MaximumFiles ||
            plan.TotalBytes is <= 0 or > MaximumBytes ||
            !IsDigest(plan.TreeSha256Hex) ||
            string.IsNullOrWhiteSpace(approval.OperatorId) ||
            approval.OperatorId.Length > 128)
            throw new InvalidDataException("Invalid or unauthorized custody plan.");

        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (approval.IssuedAtUnixSeconds > now ||
            approval.IssuedAtUnixSeconds < now - 600 ||
            approval.ExpiresAtUnixSeconds <= now ||
            approval.ExpiresAtUnixSeconds >
                approval.IssuedAtUnixSeconds + 600)
            throw new InvalidOperationException("Custody approval is stale or expired.");

        byte[] signature;
        try { signature = Convert.FromBase64String(approval.SignatureBase64); }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Invalid signed custody authorization.", ex);
        }
        if (signature.Length is < 8 or > 128)
            throw new InvalidOperationException("Invalid signed custody authorization.");
        bool verified;
        lock (_signatureGate)
            verified = _authorizedOperatorKey.VerifyData(
                EncodeApproval(approval), signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
        if (!verified)
            throw new InvalidOperationException("Operator custody approval signature rejected.");

        // Re-verify operator approval and active fencing for every attempt.
        RequireIsolation(plan);
        using var custodyLease = new FileStream(
            Path.Combine(_parent, ".ga-j3p-custody-" + plan.CaseId + ".lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var source = Path.Combine(_parent, plan.QuarantineName);
        var target = Path.Combine(_vault, plan.VaultName);
        if (Directory.Exists(source) && !Directory.Exists(target))
        {
            VerifyTree(source, plan);
            RequireIsolation(plan);
            VerifyTree(source, plan);
            _audit.Append(Event(plan, approval.OperatorId, "Intent", now));
            probe?.OnPhase(EconomicV9OrphanCustodyPhase.IntentPersisted);
            RequireIsolation(plan);
            VerifyTree(source, plan);
            // A same-filesystem rename, not copy+delete. It never
            // touches game head, backup archive or active save.
            Directory.Move(source, target);
            probe?.OnPhase(EconomicV9OrphanCustodyPhase.DirectoryMoved);
            VerifyTree(target, plan);
            _audit.Append(Event(plan, approval.OperatorId, "Completed",
                _clock.GetUtcNow().ToUnixTimeSeconds()));
            return EconomicV9OrphanCustodyOutcome.Archived;
        }

        if (!Directory.Exists(source) && Directory.Exists(target))
        {
            RequireIsolation(plan);
            VerifyTree(target, plan);
            _audit.Append(Event(plan, approval.OperatorId, "Reconciled",
                _clock.GetUtcNow().ToUnixTimeSeconds()));
            return EconomicV9OrphanCustodyOutcome.ReconciledAfterInterruption;
        }

        throw new InvalidOperationException(
            "Ambiguous custody: both source/vault present or neither exists.");
    }

    private static EconomicV9OrphanCustodyAuditEntry Event(
        EconomicV9OrphanCustodyPlan plan, string operatorId,
        string eventName, long time) =>
        new(plan.CaseId, eventName, plan.GameId,
            plan.QuarantineName, plan.VaultName,
            plan.TreeSha256Hex, operatorId, time);

    private void RequireIsolation(EconomicV9OrphanCustodyPlan plan)
    {
        if (!_isolation.IsIsolated(plan.GameId, plan.FormerWriterEpoch))
            throw new InvalidOperationException(
                "Independent former-writer fencing not confirmed; custody denied.");
    }

    private static void VerifyTree(
        string root, EconomicV9OrphanCustodyPlan expected)
    {
        var (digest, files, total) = DigestTree(root);
        if (digest != expected.TreeSha256Hex ||
            files != expected.FileCount || total != expected.TotalBytes)
            throw new InvalidDataException(
                "Source/vault contents changed after signed operator approval.");
    }

    public static byte[] EncodeApproval(EconomicV9OrphanCustodyApproval approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        var p = approval.Plan ?? throw new InvalidDataException("Missing plan.");
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("GA-V9-ORPHAN-ARCHIVE-1");
            writer.Write(p.CaseId);
            writer.Write(p.GameId);
            writer.Write(p.FormerWriterEpoch);
            writer.Write(p.QuarantineName);
            writer.Write(p.VaultName);
            writer.Write(p.TreeSha256Hex);
            writer.Write(p.FileCount);
            writer.Write(p.TotalBytes);
            writer.Write(approval.OperatorId);
            writer.Write(approval.IssuedAtUnixSeconds);
            writer.Write(approval.ExpiresAtUnixSeconds);
        }
        return memory.ToArray();
    }

    private static (string Digest, int Files, long TotalBytes)
        DigestTree(string directory)
    {
        if (!Directory.Exists(directory) ||
            (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Custody directory is absent or a symlink.");
        var records = new List<(string RelativeName, byte[] Hash, long Length)>();
        long total = 0;
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((directory, 0));
        while (pending.Count > 0)
        {
            var (current, depth) = pending.Pop();
            if (depth > 3)
                throw new InvalidDataException("Quarantine tree depth exceeded.");
            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Custody refuses symlinks or reparse points.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push((path, depth + 1));
                    continue;
                }
                var relative = Path.GetRelativePath(directory, path)
                    .Replace('\\', '/');
                if (relative.StartsWith("/", StringComparison.Ordinal) ||
                    relative.Split('/').Any(part => part is "." or ".." or "" ||
                        part.Length > 160) || relative.Length > 400)
                    throw new InvalidDataException("Unsafe quarantine filename.");
                var file = new FileInfo(path);
                if (file.Length > 256L * 1024 * 1024 ||
                    checked(total += file.Length) > MaximumBytes ||
                    records.Count >= MaximumFiles)
                    throw new InvalidDataException("Quarantine inventory exceeds limits.");
                using var stream = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read);
                var hash = SHA256.HashData(stream);
                if (stream.Length != file.Length)
                    throw new InvalidDataException("Quarantine file mutated during hashing.");
                records.Add((relative, hash, file.Length));
            }
        }
        if (records.Count == 0)
            throw new InvalidDataException("Empty quarantine cannot be approved.");
        records.Sort((a, b) => StringComparer.Ordinal.Compare(
            a.RelativeName, b.RelativeName));
        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("GA-V9-ORPHAN-TREE-1");
            writer.Write(records.Count);
            foreach (var (name, hash, length) in records)
            {
                writer.Write(name);
                writer.Write(length);
                writer.Write(hash.Length);
                writer.Write(hash);
            }
        }
        return (Convert.ToHexString(SHA256.HashData(memory.ToArray())),
            records.Count, total);
    }

    private static void ValidateQuarantineName(string name)
    {
        if (!name.StartsWith(".ga-j3m-quarantine-", StringComparison.Ordinal) ||
            name.Length != ".ga-j3m-quarantine-".Length + 32 ||
            !name[".ga-j3m-quarantine-".Length..].All(c =>
                c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            throw new ArgumentException("Expected a canonical G3-J3m quarantine name.");
    }

    private static void ValidateCaseId(string caseId)
    {
        if (!caseId.StartsWith("case-", StringComparison.Ordinal) ||
            caseId.Length != 37 ||
            !caseId[5..].All(c => c is >= '0' and <= '9' or
                                   >= 'a' and <= 'f'))
            throw new ArgumentException("Expected case- followed by a UUID hex32.");
    }

    private static void ValidateSimpleName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 ||
            !name.All(c => char.IsAsciiLetterOrDigit(c) ||
                c is '-' or '_' or '.' or ':') ||
            name is "." or "..")
            throw new ArgumentException("Invalid custody identity/path.");
    }

    private static bool IsDigest(string value) =>
        value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    public void Dispose() => _authorizedOperatorKey.Dispose();
}
