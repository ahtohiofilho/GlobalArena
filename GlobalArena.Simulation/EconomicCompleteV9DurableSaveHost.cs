using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
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
    private const uint HeadVersion = 1U;
    private const int HeadBodyBytes = 200;
    private const int HeadBytes = HeadBodyBytes + 32;
    private static readonly string ZeroDigest = new('0', 64);

    private readonly object _gate = new();
    private readonly WorldGenerationResult _generated;
    private readonly EconomicCompleteV9SaveBundleArchive _archive;
    private readonly EconomicEcologicalTransitionJournalArchive _journal;
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
        string? independentlyTrustedLegacyEcologicalDigest = null)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        var trusted = EconomicEcologicalSidecarArchive.NormalizeDigest(
            independentlyTrustedCurrentDigest);
        var archive = new EconomicCompleteV9SaveBundleArchive(directoryPath);
        var lease = Acquire(archive.DirectoryPath);
        try
        {
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
                if (previous.NaturalCapital.LastAppliedTurn == ulong.MaxValue ||
                    previous.NaturalCapital.LastAppliedTurn + 1UL != head.Turn ||
                    previous.WorldHash != head.WorldHash)
                    throw new InvalidDataException(
                        "Complete-save predecessor is missing or inconsistent.");
            }

            // The externally trusted head is checked before verifying
            // the complete immutable input/output journal chain.
            new EconomicCompleteV9JournalReplayVerifier().VerifyChain(
                generatedWorld, archive,
                new EconomicEcologicalTransitionJournalArchive(
                    archive.DirectoryPath), trusted, head.Sequence);

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
            var recorded = new EconomicEcologicalTransitionJournalRecord(
                turnOrdinal, _accepted.Sha256Hex,
                proposed.ProposedNext.Sha256Hex, demand, holds);
            var next = new Head(
                proposed.ProposedNext.WorldHash, proposed.ProposedNext.Sha256Hex,
                _head.Current, proposed.ProposedNext.NaturalCapital.LastAppliedTurn,
                checked(_head.Sequence + 1UL), _head.Migrated,
                _head.LegacyWorld, _head.LegacyEcology);

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
            migrated, legacyWorld.ToUpperInvariant(), legacyEcology.ToUpperInvariant());

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
        if (sequence == 0UL && previous != ZeroDigest ||
            sequence > 0UL && previous == ZeroDigest ||
            current == ZeroDigest)
            throw new InvalidDataException("Invalid complete-v9 head predecessor.");
        return new Head(new WorldStateHash(format,
            Convert.ToHexString(bytes.AsSpan(16, 32))), current, previous,
            BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(112, 8)),
            sequence, migrationFlag == 1U, legacyWorld, legacyEcology);
    }

    private readonly record struct Head(
        WorldStateHash WorldHash, string Current, string Previous,
        ulong Turn, ulong Sequence, bool Migrated,
        string LegacyWorld, string LegacyEcology);
}
