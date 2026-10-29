using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable mixed military/ecological transition evidence. Previous journal
/// digest connects mixed and ecological entries into the SAME accepted head.
/// Signed military authority is embedded; economic orders are head-anchored
/// but not signed as player commands.
/// </summary>
public sealed class EconomicMixedV9TransitionRecord
{
    private readonly byte[] _certificate;
    public ulong TurnOrdinal { get; }
    public ulong Seed { get; }
    public string PreviousCheckpointSha256Hex { get; }
    public string ResultCheckpointSha256Hex { get; }
    public string PreviousJournalSha256Hex { get; }
    public byte[] MilitaryAuthorizationCertificate => (byte[])_certificate.Clone();
    public IReadOnlyList<EconomicDemandProfile> WorkerDemand { get; }
    public IReadOnlyList<EconomicMaterialOriginHoldOrder> OriginHolds { get; }

    public EconomicMixedV9TransitionRecord(
        ulong turnOrdinal, ulong seed, string previousCheckpointSha256Hex,
        string resultCheckpointSha256Hex, string previousJournalSha256Hex,
        byte[] militaryAuthorizationCertificate,
        IEnumerable<EconomicDemandProfile> workerDemand,
        IEnumerable<EconomicMaterialOriginHoldOrder> originHolds)
    {
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        ArgumentNullException.ThrowIfNull(militaryAuthorizationCertificate);
        ArgumentNullException.ThrowIfNull(workerDemand);
        ArgumentNullException.ThrowIfNull(originHolds);
        if (militaryAuthorizationCertificate.Length == 0 ||
            militaryAuthorizationCertificate.Length > 16 * 1024 * 1024)
            throw new ArgumentException("Invalid signed military evidence length.",
                nameof(militaryAuthorizationCertificate));
        TurnOrdinal = turnOrdinal;
        Seed = seed;
        PreviousCheckpointSha256Hex =
            EconomicEcologicalSidecarArchive.NormalizeDigest(previousCheckpointSha256Hex);
        ResultCheckpointSha256Hex =
            EconomicEcologicalSidecarArchive.NormalizeDigest(resultCheckpointSha256Hex);
        PreviousJournalSha256Hex =
            EconomicEcologicalSidecarArchive.NormalizeDigest(previousJournalSha256Hex);
        if (PreviousCheckpointSha256Hex == ResultCheckpointSha256Hex)
            throw new ArgumentException("Mixed transition must change its checkpoint.");
        _certificate = (byte[])militaryAuthorizationCertificate.Clone();
        var demand = workerDemand.ToArray();
        var holds = originHolds.ToArray();
        if (demand.Any(x => x is null) || holds.Any(x => x is null))
            throw new ArgumentException("Mixed transition cannot contain null orders.");
        WorkerDemand = Array.AsReadOnly(demand);
        OriginHolds = Array.AsReadOnly(holds);
    }
}

/// <summary>Canonical GJMX0001: fixed-width big-endian, bounds and checksum.</summary>
public static class EconomicMixedV9TransitionCodec
{
    public const uint FormatVersion = 1U;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GJMX0001");

    public static byte[] Encode(EconomicMixedV9TransitionRecord item)
    {
        ArgumentNullException.ThrowIfNull(item);
        using var stream = new MemoryStream();
        stream.Write(Magic);
        U32(stream, FormatVersion);
        U64(stream, item.TurnOrdinal);
        U64(stream, item.Seed);
        stream.Write(Convert.FromHexString(item.PreviousCheckpointSha256Hex));
        stream.Write(Convert.FromHexString(item.ResultCheckpointSha256Hex));
        stream.Write(Convert.FromHexString(item.PreviousJournalSha256Hex));
        var certificate = item.MilitaryAuthorizationCertificate;
        U32(stream, checked((uint)certificate.Length));
        stream.Write(certificate);
        U32(stream, checked((uint)item.WorkerDemand.Count));
        foreach (var x in item.WorkerDemand)
        {
            U64(stream, x.EconomicPointId.Value);
            U32(stream, x.CommodityId.Value);
            I64(stream, x.RawDemandRatePerWorker);
        }
        U32(stream, checked((uint)item.OriginHolds.Count));
        foreach (var x in item.OriginHolds)
        {
            U64(stream, x.EconomicPointId.Value);
            U32(stream, x.CommodityId.Value);
            U32(stream, (uint)x.Activity);
            U64(stream, x.RawQuantity);
        }
        stream.Write(SHA256.HashData(stream.ToArray()));
        return stream.ToArray();
    }

    public static EconomicMixedV9TransitionRecord Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 8 + 4 + 8 + 8 + 96 + 4 + 4 + 4 + 32)
            throw new InvalidDataException("Mixed journal truncated.");
        var end = bytes.Length - 32;
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(bytes.AsSpan(0, end)), bytes.AsSpan(end, 32)))
            throw new InvalidDataException("Mixed journal checksum failed.");
        var r = new Reader(bytes, end);
        if (!r.Read(8).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown mixed journal magic.");
        if (r.U32() != FormatVersion)
            throw new NotSupportedException("Unsupported mixed journal version.");
        var turn = r.U64();
        var seed = r.U64();
        var prev = Convert.ToHexString(r.Read(32));
        var current = Convert.ToHexString(r.Read(32));
        var prevJournal = Convert.ToHexString(r.Read(32));
        var certificateCount = r.U32();
        if (certificateCount == 0U || certificateCount > 16 * 1024 * 1024)
            throw new InvalidDataException("Mixed journal certificate length invalid.");
        var certificate = r.Read(checked((int)certificateCount)).ToArray();
        var count = r.Count(20);
        var demands = new EconomicDemandProfile[count];
        for (var i = 0; i < count; i++)
            demands[i] = new EconomicDemandProfile(
                new GlobalArena.Runtime.EconomicPointId(r.U64()),
                new GlobalArena.Runtime.CommodityId(r.U32()), r.I64());
        count = r.Count(24);
        var holds = new EconomicMaterialOriginHoldOrder[count];
        for (var i = 0; i < count; i++)
            holds[i] = new EconomicMaterialOriginHoldOrder(
                new GlobalArena.Runtime.EconomicPointId(r.U64()),
                new GlobalArena.Runtime.CommodityId(r.U32()),
                (GlobalArena.Runtime.EconomicActivityKind)r.U32(), r.U64());
        r.RequireEnd();
        var item = new EconomicMixedV9TransitionRecord(
            turn, seed, prev, current, prevJournal, certificate, demands, holds);
        if (!CryptographicOperations.FixedTimeEquals(Encode(item), bytes))
            throw new InvalidDataException("Mixed journal is not canonical.");
        return item;
    }

    private static void U32(Stream s, uint x)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(b, x);
        s.Write(b);
    }
    private static void U64(Stream s, ulong x)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(b, x);
        s.Write(b);
    }
    private static void I64(Stream s, long x)
    {
        Span<byte> b = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(b, x);
        s.Write(b);
    }

    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private readonly int _end;
        private int _cursor;
        internal Reader(byte[] bytes, int end)
        {
            _bytes = bytes;
            _end = end;
        }
        internal ReadOnlySpan<byte> Read(int size)
        {
            if (size < 0 || size > _end - _cursor)
                throw new InvalidDataException("Mixed journal has truncated field.");
            var field = _bytes.AsSpan(_cursor, size);
            _cursor += size;
            return field;
        }
        internal uint U32() => BinaryPrimitives.ReadUInt32BigEndian(Read(4));
        internal ulong U64() => BinaryPrimitives.ReadUInt64BigEndian(Read(8));
        internal long I64() => BinaryPrimitives.ReadInt64BigEndian(Read(8));
        internal int Count(int width)
        {
            var count = U32();
            if (count > (uint)((_end - _cursor) / width))
                throw new InvalidDataException("Mixed journal row count exceeds payload.");
            return checked((int)count);
        }
        internal void RequireEnd()
        {
            if (_cursor != _end)
                throw new InvalidDataException("Mixed journal has trailing bytes.");
        }
    }
}

public sealed class EconomicMixedV9TransitionArchive
{
    public string DirectoryPath { get; }

    public EconomicMixedV9TransitionArchive(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) ||
            !Path.IsPathFullyQualified(directoryPath))
            throw new ArgumentException("Mixed archive requires absolute directory.",
                nameof(directoryPath));
        DirectoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(DirectoryPath);
    }

    public string RecordPath(string resultDigest) =>
        Path.Combine(DirectoryPath,
            EconomicEcologicalSidecarArchive.NormalizeDigest(resultDigest) + ".gamixed");

    public void Store(EconomicMixedV9TransitionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var bytes = EconomicMixedV9TransitionCodec.Encode(record);
        var target = RecordPath(record.ResultCheckpointSha256Hex);
        if (File.Exists(target))
        {
            RequireSameBytes(target, bytes);
            return;
        }
        var staging = Path.Combine(DirectoryPath,
            ".mixed-pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(staging, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            try { File.Move(staging, target); }
            catch (IOException) when (File.Exists(target))
            {
                RequireSameBytes(target, bytes);
            }
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    public EconomicMixedV9TransitionRecord Load(string resultDigest)
    {
        var expected = EconomicEcologicalSidecarArchive.NormalizeDigest(resultDigest);
        var item = EconomicMixedV9TransitionCodec.Decode(
            File.ReadAllBytes(RecordPath(expected)));
        if (item.ResultCheckpointSha256Hex != expected)
            throw new InvalidDataException("Mixed journal record filename mismatch.");
        return item;
    }

    private static void RequireSameBytes(string path, byte[] expected)
    {
        if (!CryptographicOperations.FixedTimeEquals(
            File.ReadAllBytes(path), expected))
            throw new InvalidDataException("Mixed journal collision or corrupted evidence.");
    }
}
