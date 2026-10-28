using System.Buffers.Binary;
using System.Security.Cryptography;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;


/// <summary>
/// Historical v8 book snapshot. Verifies the original v8 commitment rather
/// than signing the old environmental data using the v9 world hash.
/// </summary>
public sealed class EconomicLegacyV8EcologicalState
{
    public WorldState World { get; }
    public WorldStateHash WorldHash { get; }
    public EconomicNaturalCapitalBook NaturalCapital { get; }
    public EconomicMaterialReserveBook MaterialReserves { get; }
    public EconomicNaturalCapitalPolicy Policy { get; }
    public EconomicPrimitiveCommodityMapping Commodities { get; }
    public string Sha256Hex { get; }

    public EconomicLegacyV8EcologicalState(
        WorldGenerationResult generatedWorld, WorldState world,
        EconomicNaturalCapitalBook naturalCapital,
        EconomicMaterialReserveBook materialReserves,
        EconomicNaturalCapitalPolicy policy,
        EconomicPrimitiveCommodityMapping commodities)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(naturalCapital);
        ArgumentNullException.ThrowIfNull(materialReserves);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(commodities);
        if (world.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException("Legacy ecology needs the exact generated world.");
        if (world.TransportFacilities.Facilities.Count != 0)
            throw new InvalidOperationException("Version 8 cannot encode transport facilities.");
        if (naturalCapital.Cells.Count != generatedWorld.StrategicSurfaceGraph.NodeCount)
            throw new InvalidOperationException("Legacy ecology needs all strategic cells.");
        for (var i = 0; i < naturalCapital.Cells.Count; i++)
            if (naturalCapital.Cells[i].CellId !=
                generatedWorld.StrategicSurfaceGraph.GetCellId(i))
                throw new InvalidOperationException("Legacy ecology has invalid cell topology.");
        var points = world.Economy.EconomicPoints.Select(x => x.Id).ToHashSet();
        foreach (var balance in materialReserves.Balances)
            if (!points.Contains(balance.EconomicPointId))
                throw new InvalidOperationException("Legacy reserve references unknown point.");
        var owners = world.Civilizations.Civilizations.ToHashSet();
        foreach (var cursor in materialReserves.AppliedTurnCursors)
            if (!owners.Contains(cursor.CivilizationId) ||
                cursor.LastAppliedTurn > naturalCapital.LastAppliedTurn)
                throw new InvalidOperationException("Legacy reserve cursor is invalid.");

        World = world;
        NaturalCapital = naturalCapital;
        MaterialReserves = materialReserves;
        Policy = policy;
        Commodities = commodities;
        WorldHash = new LegacyWorldStateV8Hasher().Compute(world);
        Sha256Hex = EconomicEcologicalCheckpoint.ComputeDigestForWorldHash(
            WorldHash, naturalCapital, materialReserves, policy, commodities);
    }
}

/// <summary>
/// G3-H: exact, versioned, big-endian companion snapshot for natural capital
/// and deliberately retained commodities. The containing WorldState MUST be
/// loaded and hash-verified independently. This is not a WorldState save file,
/// a new WorldState hash version, an authenticated signature, or replay log.
/// </summary>
public static class EconomicLegacyV8EcologicalSidecarCodec
{
    public const uint FormatVersion = 1U;
    private static readonly byte[] Magic =
    {
        (byte)'G', (byte)'A', (byte)'E', (byte)'C',
        (byte)'O', (byte)'1', 13, 10
    };
    private const int DigestBytes = 32;

    public static byte[] Encode(EconomicLegacyV8EcologicalState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var stream = new MemoryStream();
        var w = new Writer(stream);
        w.Bytes(Magic);
        w.U32(FormatVersion);
        w.U32(snapshot.WorldHash.FormatVersion);
        w.Bytes(Convert.FromHexString(snapshot.WorldHash.HexDigest));
        w.Bytes(Convert.FromHexString(snapshot.Sha256Hex));

        var natural = snapshot.NaturalCapital;
        w.U32(natural.DerivationVersion);
        w.U64(natural.LastAppliedTurn);
        w.U64(snapshot.Policy.RawWoodAtFullEndowment);
        w.U64(snapshot.Policy.RawStoneAtFullEndowment);
        w.I64(snapshot.Policy.RawWoodRecoveryFraction);
        w.I64(snapshot.Policy.RawSoilRecoveryFraction);
        w.I64(snapshot.Policy.RawSoilWearPerFoodUnit);
        w.U32(snapshot.Commodities.Food.Value);
        w.U32(snapshot.Commodities.Wood.Value);
        w.U32(snapshot.Commodities.Stone.Value);

        w.U32(checked((uint)natural.Cells.Count));
        foreach (var row in natural.Cells)
        {
            w.U64(row.CellId.Value);
            w.U64(row.RawWoodCarryingCapacity);
            w.U64(row.RawStandingWood);
            w.U64(row.RawSurfaceStone);
            w.I64(row.RawSoilVitality);
        }

        w.U32(checked((uint)snapshot.MaterialReserves.Balances.Count));
        foreach (var row in snapshot.MaterialReserves.Balances)
        {
            w.U64(row.EconomicPointId.Value);
            w.U32(row.CommodityId.Value);
            w.U64(row.RawQuantity);
        }

        w.U32(checked((uint)snapshot.MaterialReserves.AppliedTurnCursors.Count));
        foreach (var row in snapshot.MaterialReserves.AppliedTurnCursors)
        {
            w.U64(row.CivilizationId.Value);
            w.U64(row.LastAppliedTurn);
        }

        var payload = stream.ToArray();
        var checksum = SHA256.HashData(payload);
        stream.Write(checksum);
        return stream.ToArray();
    }

    /// <summary>
    /// Refuses different worlds, different WorldState hashes (including hash
    /// format), invalid versions, truncated/trailing bytes, corrupted checksums,
    /// illegal state rows and any changed canonical checkpoint commitment.
    /// Caller must trust the separately loaded WorldState and its accepted hash.
    /// </summary>
    public static EconomicLegacyV8EcologicalState Decode(
        WorldGenerationResult generatedWorld, WorldState world,
        byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(encoded);

        if (encoded.Length < Magic.Length + 4 + 4 + 2 * DigestBytes)
            throw new InvalidDataException("Sidecar is truncated.");

        var payloadLength = encoded.Length - DigestBytes;
        var checksum = SHA256.HashData(encoded.AsSpan(0, payloadLength));
        if (!CryptographicOperations.FixedTimeEquals(checksum,
            encoded.AsSpan(payloadLength, DigestBytes)))
            throw new InvalidDataException("Sidecar checksum does not match its contents.");

        var r = new Reader(encoded, payloadLength);
        if (!r.Bytes(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown sidecar magic.");
        if (r.U32() != FormatVersion)
            throw new NotSupportedException("Unsupported ecological sidecar version.");

        var encodedWorldHashVersion = r.U32();
        var encodedWorldDigest = r.Bytes(DigestBytes);
        var expectedCheckpointDigest = r.Bytes(DigestBytes);
        var actualWorldHash = new LegacyWorldStateV8Hasher().Compute(world);
        if (encodedWorldHashVersion != actualWorldHash.FormatVersion ||
            !CryptographicOperations.FixedTimeEquals(
                encodedWorldDigest, Convert.FromHexString(actualWorldHash.HexDigest)))
            throw new InvalidOperationException(
                "Sidecar belongs to another WorldState or hash format.");

        var derivation = r.U32();
        var lastTurn = r.U64();
        var policy = new EconomicNaturalCapitalPolicy(
            r.U64(), r.U64(), r.I64(), r.I64(), r.I64());
        var mapping = new EconomicPrimitiveCommodityMapping(
            new CommodityId(r.U32()), new CommodityId(r.U32()),
            new CommodityId(r.U32()));

        var graph = generatedWorld.StrategicSurfaceGraph;
        if (r.U32() != checked((uint)graph.NodeCount))
            throw new InvalidDataException(
                "Ecological sidecar has a different number of strategic cells.");

        var natural = new EconomicNaturalCapitalCellState[graph.NodeCount];
        for (var i = 0; i < natural.Length; i++)
            natural[i] = new EconomicNaturalCapitalCellState(
                new StrategicCellId(r.U64()),
                r.U64(), r.U64(), r.U64(), r.I64());

        // Each record's size is fixed; refuse malicious counts before
        // allocating any unbounded arrays.
        var balanceCount = r.Count(20);
        var balances = new EconomicMaterialReserveBalance[balanceCount];
        for (var i = 0; i < balances.Length; i++)
            balances[i] = new EconomicMaterialReserveBalance(
                new EconomicPointId(r.U64()),
                new CommodityId(r.U32()), r.U64());

        var cursorCount = r.Count(16);
        if (cursorCount > world.Civilizations.Civilizations.Count)
            throw new InvalidDataException("Too many civilization cursors.");
        var cursors = new EconomicMaterialReserveTurnCursor[cursorCount];
        for (var i = 0; i < cursors.Length; i++)
            cursors[i] = new EconomicMaterialReserveTurnCursor(
                new CivilizationId(r.U64()), r.U64());

        r.RequireEnd();
        var checkpoint = new EconomicLegacyV8EcologicalState(
            generatedWorld, world,
            new EconomicNaturalCapitalBook(lastTurn, derivation, natural),
            new EconomicMaterialReserveBook(balances, cursors),
            policy, mapping);

        if (!CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(checkpoint.Sha256Hex), expectedCheckpointDigest))
            throw new InvalidDataException(
                "Stored checkpoint commitment does not match decoded semantic state.");

        // Enforce one canonical encoding: row reordering, superfluous records,
        // or noncanonical integer representation cannot acquire a valid save.
        var canonical = Encode(checkpoint);
        if (!CryptographicOperations.FixedTimeEquals(canonical, encoded))
            throw new InvalidDataException("Sidecar does not have canonical encoding.");

        return checkpoint;
    }

    private sealed class Writer
    {
        private readonly Stream _stream;
        internal Writer(Stream stream) => _stream = stream;
        internal void Bytes(ReadOnlySpan<byte> data) => _stream.Write(data);
        internal void U32(uint value)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(b, value);
            Bytes(b);
        }
        internal void U64(ulong value)
        {
            Span<byte> b = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(b, value);
            Bytes(b);
        }
        internal void I64(long value)
        {
            Span<byte> b = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(b, value);
            Bytes(b);
        }
    }

    private sealed class Reader
    {
        private readonly byte[] _encoded;
        private readonly int _end;
        private int _offset;
        internal Reader(byte[] encoded, int payloadLength)
        {
            _encoded = encoded;
            _end = payloadLength;
        }
        internal int Remaining => _end - _offset;
        internal ReadOnlySpan<byte> Bytes(int count)
        {
            if (count < 0 || count > Remaining)
                throw new InvalidDataException("Sidecar ended inside a record.");
            var result = _encoded.AsSpan(_offset, count);
            _offset += count;
            return result;
        }
        internal uint U32() => BinaryPrimitives.ReadUInt32BigEndian(Bytes(4));
        internal ulong U64() => BinaryPrimitives.ReadUInt64BigEndian(Bytes(8));
        internal long I64() => BinaryPrimitives.ReadInt64BigEndian(Bytes(8));
        internal int Count(int bytesPerRow)
        {
            var count = U32();
            if (count > (uint)(Remaining / bytesPerRow))
                throw new InvalidDataException("Sidecar contains an impossible row count.");
            return checked((int)count);
        }
        internal void RequireEnd()
        {
            if (Remaining != 0)
                throw new InvalidDataException("Sidecar has trailing or unparsed data.");
        }
    }
}
