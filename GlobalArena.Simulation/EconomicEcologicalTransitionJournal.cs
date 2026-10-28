using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable, exactly ordered input to an ecological reference turn.
/// The resulting checkpoint is independently committed by GAFULL09; this
/// record records inputs, not authorization or actual market delivery.
/// </summary>
public sealed class EconomicEcologicalTransitionJournalRecord
{
    public ulong TurnOrdinal { get; }
    public string PreviousCheckpointSha256Hex { get; }
    public string ResultCheckpointSha256Hex { get; }
    public IReadOnlyList<EconomicDemandProfile> WorkerDemand { get; }
    public IReadOnlyList<EconomicMaterialOriginHoldOrder> OriginHolds { get; }

    public EconomicEcologicalTransitionJournalRecord(
        ulong turnOrdinal, string previousCheckpointSha256Hex,
        string resultCheckpointSha256Hex,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        var previous = EconomicEcologicalSidecarArchive.NormalizeDigest(
            previousCheckpointSha256Hex);
        var result = EconomicEcologicalSidecarArchive.NormalizeDigest(
            resultCheckpointSha256Hex);
        if (previous == result)
            throw new ArgumentException("A turn must produce a distinct committed checkpoint.");

        var demand = workerDemand.ToArray();
        var holds = originHolds.ToArray();
        if (demand.Any(x => x is null) || holds.Any(x => x is null))
            throw new ArgumentException("A replay journal cannot contain null input rows.");

        TurnOrdinal = turnOrdinal;
        PreviousCheckpointSha256Hex = previous;
        ResultCheckpointSha256Hex = result;
        WorkerDemand = Array.AsReadOnly(demand);
        OriginHolds = Array.AsReadOnly(holds);
    }
}

/// <summary>
/// Binary, canonical, checksum-protected replay journal record. Fixed-width,
/// big-endian schema, no reflection/JSON, no unbounded allocations based on
/// untrusted row counts. Digest is corruption detection, not a signature.
/// </summary>
public static class EconomicEcologicalTransitionJournalCodec
{
    public const uint FormatVersion = 1U;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GAJRN001");

    public static byte[] Encode(EconomicEcologicalTransitionJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        using var stream = new MemoryStream();
        stream.Write(Magic);
        WriteU32(stream, FormatVersion);
        WriteU64(stream, record.TurnOrdinal);
        stream.Write(Convert.FromHexString(record.PreviousCheckpointSha256Hex));
        stream.Write(Convert.FromHexString(record.ResultCheckpointSha256Hex));

        WriteU32(stream, checked((uint)record.WorkerDemand.Count));
        foreach (var demand in record.WorkerDemand)
        {
            WriteU64(stream, demand.EconomicPointId.Value);
            WriteU32(stream, demand.CommodityId.Value);
            WriteI64(stream, demand.RawDemandRatePerWorker);
        }

        WriteU32(stream, checked((uint)record.OriginHolds.Count));
        foreach (var hold in record.OriginHolds)
        {
            WriteU64(stream, hold.EconomicPointId.Value);
            WriteU32(stream, hold.CommodityId.Value);
            WriteU32(stream, (uint)hold.Activity);
            WriteU64(stream, hold.RawQuantity);
        }

        stream.Write(SHA256.HashData(stream.ToArray()));
        return stream.ToArray();
    }

    public static EconomicEcologicalTransitionJournalRecord Decode(byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        if (encoded.Length < 8 + 4 + 8 + 32 + 32 + 4 + 4 + 32)
            throw new InvalidDataException("Ecological journal is truncated.");

        var end = encoded.Length - 32;
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(encoded.AsSpan(0, end)), encoded.AsSpan(end, 32)))
            throw new InvalidDataException("Ecological journal checksum failed.");

        var reader = new Reader(encoded, end);
        if (!reader.Bytes(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown ecological journal magic.");
        if (reader.U32() != FormatVersion)
            throw new NotSupportedException("Unsupported ecological journal version.");

        var turn = reader.U64();
        var prior = Convert.ToHexString(reader.Bytes(32));
        var result = Convert.ToHexString(reader.Bytes(32));

        var count = reader.Count(20);
        var demand = new EconomicDemandProfile[count];
        for (var i = 0; i < count; i++)
            demand[i] = new EconomicDemandProfile(
                new EconomicPointId(reader.U64()),
                new CommodityId(reader.U32()), reader.I64());

        count = reader.Count(24);
        var holds = new EconomicMaterialOriginHoldOrder[count];
        for (var i = 0; i < count; i++)
            holds[i] = new EconomicMaterialOriginHoldOrder(
                new EconomicPointId(reader.U64()),
                new CommodityId(reader.U32()),
                (EconomicActivityKind)reader.U32(), reader.U64());
        reader.RequireEnd();

        var record = new EconomicEcologicalTransitionJournalRecord(
            turn, prior, result, demand, holds);
        if (!CryptographicOperations.FixedTimeEquals(
            Encode(record), encoded))
            throw new InvalidDataException("Ecological journal is not canonical.");
        return record;
    }

    private static void WriteU32(Stream target, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        target.Write(bytes);
    }
    private static void WriteU64(Stream target, ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        target.Write(bytes);
    }
    private static void WriteI64(Stream target, long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        target.Write(bytes);
    }

    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private readonly int _end;
        private int _offset;

        internal Reader(byte[] bytes, int payloadEnd)
        {
            _bytes = bytes;
            _end = payloadEnd;
        }

        internal ReadOnlySpan<byte> Bytes(int length)
        {
            if (length < 0 || length > _end - _offset)
                throw new InvalidDataException("Ecological journal has truncated rows.");
            var data = _bytes.AsSpan(_offset, length);
            _offset += length;
            return data;
        }
        internal uint U32() => BinaryPrimitives.ReadUInt32BigEndian(Bytes(4));
        internal ulong U64() => BinaryPrimitives.ReadUInt64BigEndian(Bytes(8));
        internal long I64() => BinaryPrimitives.ReadInt64BigEndian(Bytes(8));

        internal int Count(int bytesPerRow)
        {
            var count = U32();
            if (count > (uint)((_end - _offset) / bytesPerRow))
                throw new InvalidDataException("Ecological journal row count exceeds payload.");
            return checked((int)count);
        }

        internal void RequireEnd()
        {
            if (_offset != _end)
                throw new InvalidDataException("Ecological journal has trailing bytes.");
        }
    }
}

/// <summary>
/// Independent, immutable journal blob for each resulting ecological digest.
/// Files are published BEFORE the head which references the result. The
/// published head is still the ONLY turn acceptance record.
/// </summary>
public sealed class EconomicEcologicalTransitionJournalArchive
{
    public string DirectoryPath { get; }

    public EconomicEcologicalTransitionJournalArchive(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) ||
            !Path.IsPathFullyQualified(directoryPath))
            throw new ArgumentException("Journal archive needs an absolute directory.",
                nameof(directoryPath));
        DirectoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(DirectoryPath);
    }

    public string RecordPath(string resultCheckpointSha256Hex) =>
        Path.Combine(DirectoryPath,
            EconomicEcologicalSidecarArchive.NormalizeDigest(
                resultCheckpointSha256Hex) + ".gajournal");

    public void Store(EconomicEcologicalTransitionJournalRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var bytes = EconomicEcologicalTransitionJournalCodec.Encode(record);
        var target = RecordPath(record.ResultCheckpointSha256Hex);
        if (File.Exists(target))
        {
            RequireSameBytes(target, bytes);
            return;
        }

        var pending = Path.Combine(DirectoryPath,
            ".journal-pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(pending,
                FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            try
            {
                File.Move(pending, target);
            }
            catch (IOException) when (File.Exists(target))
            {
                RequireSameBytes(target, bytes);
            }
        }
        finally
        {
            if (File.Exists(pending))
                File.Delete(pending);
        }
    }

    public EconomicEcologicalTransitionJournalRecord Load(
        string resultCheckpointSha256Hex)
    {
        var digest = EconomicEcologicalSidecarArchive.NormalizeDigest(
            resultCheckpointSha256Hex);
        var record = EconomicEcologicalTransitionJournalCodec.Decode(
            File.ReadAllBytes(RecordPath(digest)));
        if (record.ResultCheckpointSha256Hex != digest)
            throw new InvalidDataException(
                "Journal filename disagrees with recorded resulting checkpoint.");
        return record;
    }

    private static void RequireSameBytes(string target, byte[] expected)
    {
        if (!CryptographicOperations.FixedTimeEquals(
            File.ReadAllBytes(target), expected))
            throw new InvalidDataException(
                "Journal name already exists with corrupted or different bytes.");
    }
}
