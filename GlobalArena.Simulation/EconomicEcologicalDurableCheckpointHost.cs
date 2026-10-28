using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Single-writer, process-owned durable head for an ecological SIDE-CAR.
/// A complete, content-addressed sidecar is written and flushed BEFORE
/// the head is atomically replaced in the same directory. A crash may
/// strand an unreferenced blob, but never publishes a head before its blob.
/// Does not persist WorldState and is not a multiprocess/distributed
/// transaction, cryptographic signature, or hardware power-loss guarantee.
/// </summary>
public sealed class EconomicEcologicalDurableCheckpointHost : IDisposable
{
    private static readonly byte[] HeadMagic =
        Encoding.ASCII.GetBytes("GAECHD01");
    private const uint HeadVersion = 1U;
    private const int HeadPayloadLength = 88;
    private const int HeadLength = HeadPayloadLength + 32;

    private readonly object _gate = new();
    private readonly EconomicEcologicalSidecarArchive _archive;
    private readonly WorldGenerationResult _generatedWorld;
    private readonly EconomicEcologicalTurnCommitResolver _turns = new();
    private readonly FileStream _writerLease;
    private EconomicEcologicalCheckpoint _accepted;
    private bool _disposed;

    private EconomicEcologicalDurableCheckpointHost(
        EconomicEcologicalSidecarArchive archive,
        WorldGenerationResult generatedWorld,
        EconomicEcologicalCheckpoint accepted, FileStream writerLease)
    {
        _archive = archive;
        _generatedWorld = generatedWorld;
        _accepted = accepted;
        _writerLease = writerLease;
    }

    /// <summary>
    /// Start a new sidecar head, refusing to overwrite any existing head.
    /// The caller must already have validated the initial WorldState.
    /// </summary>
    public static EconomicEcologicalDurableCheckpointHost Create(
        string directoryPath, WorldGenerationResult generatedWorld,
        EconomicEcologicalCheckpoint initial)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(initial);
        if (initial.World.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException("Cannot create a head for another generated world.");

        var archive = new EconomicEcologicalSidecarArchive(directoryPath);
        var lease = OpenWriterLease(archive.DirectoryPath);
        try
        {
            var headPath = HeadPath(archive.DirectoryPath);
            if (File.Exists(headPath))
                throw new IOException("A committed ecological head already exists.");
            archive.Store(initial);
            WriteHead(headPath, initial, overwrite: false);
            return new EconomicEcologicalDurableCheckpointHost(
                archive, generatedWorld, initial, lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The trusted digest must come from outside the potentially rewritable
    /// sidecar directory. This prevents restoring an older self-consistent
    /// head+blob pair after rollback by a malicious storage operator.
    /// </summary>
    public static EconomicEcologicalDurableCheckpointHost Restore(
        string directoryPath, WorldGenerationResult generatedWorld,
        WorldState world, string externallyTrustedAcceptedDigest)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        var trusted = EconomicEcologicalSidecarArchive.NormalizeDigest(
            externallyTrustedAcceptedDigest);

        var archive = new EconomicEcologicalSidecarArchive(directoryPath);
        var lease = OpenWriterLease(archive.DirectoryPath);
        try
        {
            var head = ReadHead(HeadPath(archive.DirectoryPath));
            if (head.AcceptedDigest != trusted)
                throw new InvalidOperationException(
                    "Durable head differs from independently trusted accepted digest.");
            var worldHash = new CanonicalWorldStateHasher().Compute(world);
            if (head.WorldHash != worldHash)
                throw new InvalidOperationException(
                    "Durable head references a different WorldState hash or format.");
            var accepted = archive.Load(generatedWorld, world, trusted);
            if (accepted.NaturalCapital.LastAppliedTurn != head.LastTurn)
                throw new InvalidDataException(
                    "Durable head turn differs from the decoded checkpoint.");

            return new EconomicEcologicalDurableCheckpointHost(
                archive, generatedWorld, accepted, lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public string AcceptedSha256Hex
    {
        get
        {
            lock (_gate)
            {
                EnsureOpen();
                return _accepted.Sha256Hex;
            }
        }
    }

    public ulong LastAppliedTurn
    {
        get
        {
            lock (_gate)
            {
                EnsureOpen();
                return _accepted.NaturalCapital.LastAppliedTurn;
            }
        }
    }

    public EconomicEcologicalTurnCommitResult CommitNext(
        string expectedSha256Hex, ulong turnOrdinal,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        lock (_gate)
        {
            EnsureOpen();
            _accepted.RequireExpectedDigest(expectedSha256Hex);

            var headPath = HeadPath(_archive.DirectoryPath);
            var head = ReadHead(headPath);
            if (head.AcceptedDigest != _accepted.Sha256Hex ||
                head.WorldHash != _accepted.WorldHash ||
                head.LastTurn != _accepted.NaturalCapital.LastAppliedTurn)
                throw new InvalidOperationException("Durable head changed outside this host.");

            var proposed = _turns.Propose(
                _generatedWorld, _accepted, expectedSha256Hex,
                turnOrdinal, workerDemand, originHolds);

            // Publish the complete blob before advancing the durable head.
            // If writing fails the accepted head remains unchanged.
            _archive.Store(proposed.ProposedNext);
            WriteHead(headPath, proposed.ProposedNext, overwrite: true);
            _accepted = proposed.ProposedNext;
            return proposed;
        }
    }

    public byte[] CaptureSidecar()
    {
        lock (_gate)
        {
            EnsureOpen();
            return EconomicEcologicalSidecarCodec.Encode(_accepted);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _writerLease.Dispose();
            _disposed = true;
        }
    }

    private void EnsureOpen()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(EconomicEcologicalDurableCheckpointHost));
    }

    private static FileStream OpenWriterLease(string root) =>
        new(Path.Combine(root, ".ecological-writer.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private static string HeadPath(string root) =>
        Path.Combine(root, "ecological-head.gahead");

    private static void WriteHead(
        string headPath, EconomicEcologicalCheckpoint checkpoint,
        bool overwrite)
    {
        var body = new byte[HeadLength];
        HeadMagic.CopyTo(body, 0);
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(8, 4), HeadVersion);
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(12, 4),
            checkpoint.WorldHash.FormatVersion);
        Convert.FromHexString(checkpoint.WorldHash.HexDigest).CopyTo(body, 16);
        Convert.FromHexString(checkpoint.Sha256Hex).CopyTo(body, 48);
        BinaryPrimitives.WriteUInt64BigEndian(body.AsSpan(80, 8),
            checkpoint.NaturalCapital.LastAppliedTurn);
        SHA256.HashData(body.AsSpan(0, HeadPayloadLength)).CopyTo(body, HeadPayloadLength);

        var dir = Path.GetDirectoryName(headPath)!;
        var staging = Path.Combine(dir,
            ".head-pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(staging, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(body);
                stream.Flush(flushToDisk: true);
            }
            File.Move(staging, headPath, overwrite);
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    private static HeadRecord ReadHead(string headPath)
    {
        var data = File.ReadAllBytes(headPath);
        if (data.Length != HeadLength ||
            !data.AsSpan(0, HeadMagic.Length).SequenceEqual(HeadMagic))
            throw new InvalidDataException("Durable ecological head has invalid shape or magic.");

        var expectedChecksum = SHA256.HashData(data.AsSpan(0, HeadPayloadLength));
        if (!CryptographicOperations.FixedTimeEquals(
            expectedChecksum, data.AsSpan(HeadPayloadLength, 32)))
            throw new InvalidDataException("Durable ecological head checksum failed.");

        var version = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8, 4));
        if (version != HeadVersion)
            throw new NotSupportedException("Unsupported durable ecological head version.");

        var worldVersion = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12, 4));
        var world = new WorldStateHash(worldVersion,
            Convert.ToHexString(data.AsSpan(16, 32)));
        var digest = Convert.ToHexString(data.AsSpan(48, 32));
        var lastTurn = BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(80, 8));
        return new HeadRecord(world, digest, lastTurn);
    }

    private readonly record struct HeadRecord(
        WorldStateHash WorldHash, string AcceptedDigest, ulong LastTurn);
}
