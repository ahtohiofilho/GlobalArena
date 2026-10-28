using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Exact snapshot codec for the CURRENT v9 WorldState runtime schema.
/// Includes PR #50's section 35: facility identity, fine tile, operational
/// state, and directed transfer capabilities.
/// </summary>
public static class EconomicWorldStateV9SnapshotCodec
{
    public const uint FormatVersion = 1U;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GAWSNP09");
    private const int DigestSize = 32;

    public static byte[] Encode(WorldState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        RequireV9();
        if (world.WorldBinding is null)
            throw new InvalidOperationException("Full-world save requires a bound generated world.");

        using var stream = new MemoryStream();
        var w = new Writer(stream);
        w.Bytes(Magic);
        w.U32(FormatVersion);
        var worldHash = new CanonicalWorldStateHasher().Compute(world);
        w.U32(worldHash.FormatVersion);
        w.Bytes(Convert.FromHexString(worldHash.HexDigest));
        w.U32(checked((uint)world.WorldBinding.WorldSignatureFormatVersion));
        w.Bytes(Convert.FromHexString(world.WorldBinding.WorldSignatureSha256Hex));
        w.U64(world.WorldBinding.StrategicCellCount);
        w.U64(world.Revision);

        w.Count(world.Civilizations.Records.Count);
        foreach (var c in world.Civilizations.Records)
        {
            w.U64(c.Id.Value);
            w.U32(c.StartCellId.HasValue ? 1U : 0U);
            if (c.StartCellId is StrategicCellId start)
                w.U64(start.Value);
        }

        w.Count(world.Territory.Controls.Count);
        foreach (var c in world.Territory.Controls)
        {
            w.U64(c.StrategicCellId.Value);
            w.U64(c.Controller.Value);
        }

        w.Count(world.Diplomacy.Relations.Count);
        foreach (var c in world.Diplomacy.Relations)
        {
            w.U64(c.First.Value);
            w.U64(c.Second.Value);
            w.U32((uint)c.Relation);
        }

        w.Count(world.Economy.EconomicPoints.Count);
        foreach (var point in world.Economy.EconomicPoints)
        {
            w.U64(point.Id.Value);
            w.U64(point.Owner.Value);
            w.U64(point.StrategicCellId.Value);
            w.U64(point.Workforce);
            w.Count(point.ActivityAllocations.Count);
            foreach (var a in point.ActivityAllocations)
            {
                w.U32((uint)a.Kind);
                w.U64(a.Workforce);
            }
        }

        w.U32(world.Population.IsInitialized ? 1U : 0U);
        w.Count(world.Population.PointPopulations.Count);
        foreach (var c in world.Population.PointPopulations)
        {
            w.U64(c.EconomicPointId.Value);
            w.U64(c.Residents);
        }

        w.Count(world.Warfare.Units.Count);
        foreach (var c in world.Warfare.Units)
        {
            w.U64(c.Id.Value);
            w.U64(c.Owner.Value);
            w.U64(c.StrategicCellId.Value);
        }

        // PR #50 facility domain: section 35 of canonical WorldState v9.
        w.Count(world.TransportFacilities.Facilities.Count);
        foreach (var facility in world.TransportFacilities.Facilities)
        {
            w.U64(facility.FacilityId);
            w.U32(checked((uint)facility.Tile.FineGoldbergParameters.M));
            w.U32(checked((uint)facility.Tile.FineGoldbergParameters.N));
            w.U64(facility.Tile.FineStrategicCellId.Value);
            w.U32(facility.IsOperational ? 1U : 0U);
            w.Count(facility.Transfers.Count);
            foreach (var capability in facility.Transfers)
            {
                w.U64(capability.FromTechnologyTypeId);
                w.U64(capability.ToTechnologyTypeId);
                w.U64(capability.TechnologyCatalogVersion);
            }
        }

        var payload = stream.ToArray();
        stream.Write(SHA256.HashData(payload));
        return stream.ToArray();
    }

    public static WorldState Decode(
        WorldGenerationResult generatedWorld, byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(encoded);
        RequireV9();

        if (encoded.Length < Magic.Length + 4 + 4 + DigestSize + DigestSize)
            throw new InvalidDataException("World snapshot is truncated.");

        var end = encoded.Length - DigestSize;
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(encoded.AsSpan(0, end)),
            encoded.AsSpan(end, DigestSize)))
            throw new InvalidDataException("World snapshot checksum mismatch.");

        var r = new Reader(encoded, end);
        if (!r.Bytes(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown world snapshot magic.");
        if (r.U32() != FormatVersion)
            throw new NotSupportedException("Unsupported full-world snapshot codec.");
        if (r.U32() != 9U)
            throw new NotSupportedException("Only WorldState canonical hash v9 is supported.");

        var recordedHash = r.Bytes(DigestSize).ToArray();
        var signatureVersion = r.U32();
        var signature = Convert.ToHexString(r.Bytes(DigestSize)).ToLowerInvariant();
        var cellCount = r.U64();

        var expectedBinding = RuntimeWorldBinding.FromGeneratedWorld(generatedWorld);
        if (signatureVersion != (uint)expectedBinding.WorldSignatureFormatVersion ||
            signature != expectedBinding.WorldSignatureSha256Hex ||
            cellCount != expectedBinding.StrategicCellCount)
            throw new InvalidOperationException("World snapshot is bound to another generated world.");

        var revision = r.U64();

        var civs = new CivilizationRuntimeRecord[r.Count(12)];
        for (var i = 0; i < civs.Length; i++)
        {
            var id = new CivilizationId(r.U64());
            var hasStart = r.U32();
            civs[i] = hasStart switch
            {
                0U => new CivilizationRuntimeRecord(id),
                1U => new CivilizationRuntimeRecord(id, new StrategicCellId(r.U64())),
                _ => throw new InvalidDataException("Invalid civilization start marker.")
            };
        }

        var territory = new StrategicTerritoryControlEntry[r.Count(16)];
        for (var i = 0; i < territory.Length; i++)
            territory[i] = new StrategicTerritoryControlEntry(
                new StrategicCellId(r.U64()), new CivilizationId(r.U64()));

        var diplomacy = new BaselineDiplomacyRelationEntry[r.Count(20)];
        for (var i = 0; i < diplomacy.Length; i++)
            diplomacy[i] = new BaselineDiplomacyRelationEntry(
                new CivilizationId(r.U64()), new CivilizationId(r.U64()),
                (BaselineDiplomacyRelationKind)r.U32());

        var points = new EconomicPointRuntimeState[r.Count(36)];
        for (var i = 0; i < points.Length; i++)
        {
            var id = new EconomicPointId(r.U64());
            var owner = new CivilizationId(r.U64());
            var cell = new StrategicCellId(r.U64());
            var workforce = r.U64();
            var allocations = new EconomicActivityWorkforceAllocation[r.Count(12)];
            for (var j = 0; j < allocations.Length; j++)
                allocations[j] = new EconomicActivityWorkforceAllocation(
                    (EconomicActivityKind)r.U32(), r.U64());

            points[i] = new EconomicPointRuntimeState(
                id, owner, cell, workforce, allocations);
        }

        var populationMarker = r.U32();
        if (populationMarker is not (0U or 1U))
            throw new InvalidDataException("Invalid resident census initialization marker.");
        var residents = new PopulationPointRuntimeState[r.Count(16)];
        for (var i = 0; i < residents.Length; i++)
            residents[i] = new PopulationPointRuntimeState(
                new EconomicPointId(r.U64()), r.U64());
        if (populationMarker == 0U && residents.Length > 0)
            throw new InvalidDataException("Uninitialized resident census has data.");

        var military = new MilitaryUnitRuntimeState[r.Count(24)];
        for (var i = 0; i < military.Length; i++)
            military[i] = new MilitaryUnitRuntimeState(
                new MilitaryUnitId(r.U64()), new CivilizationId(r.U64()),
                new StrategicCellId(r.U64()));

        // No v9 installation may be silently omitted on import.
        var facilities = new TransportFacilityRuntimeRecord[r.Count(32)];
        for (var i = 0; i < facilities.Length; i++)
        {
            var facilityId = r.U64();
            var fine = new GoldbergParameters(
                checked((int)r.U32()), checked((int)r.U32()));
            var tile = new PhysicalTacticalTileId(
                fine, new StrategicCellId(r.U64()));
            var status = r.U32();
            if (status is not (0U or 1U))
                throw new InvalidDataException("Invalid transport facility operational flag.");
            var transfers = new TransportFacilityTransferCapability[r.Count(24)];
            for (var j = 0; j < transfers.Length; j++)
                transfers[j] = new TransportFacilityTransferCapability(
                    r.U64(), r.U64(), r.U64());
            facilities[i] = new TransportFacilityRuntimeRecord(
                facilityId, tile, status == 1U, transfers);
        }

        r.RequireEnd();

        var world = WorldState.CreateBound(generatedWorld)
            .WithCivilizations(new CivilizationRuntimeState(civs))
            .WithTerritory(new StrategicTerritoryRuntimeState(territory))
            .WithDiplomacy(new BaselineDiplomacyRuntimeState(diplomacy))
            .WithEconomyAndPopulation(
                new EconomyRuntimeState(points),
                populationMarker == 1U
                    ? new PopulationRuntimeState(residents)
                    : PopulationRuntimeState.Uninitialized)
            .WithWarfare(new WarfareRuntimeState(military))
            .WithTransportFacilities(new TransportFacilitiesRuntimeState(facilities))
            .WithRestoredRevision(revision);

        var hash = new CanonicalWorldStateHasher().Compute(world);
        if (!CryptographicOperations.FixedTimeEquals(
            recordedHash, Convert.FromHexString(hash.HexDigest)))
            throw new InvalidDataException(
                "World snapshot does not reproduce its recorded canonical state hash.");

        if (!CryptographicOperations.FixedTimeEquals(Encode(world), encoded))
            throw new InvalidDataException("World snapshot is not canonically encoded.");

        return world;
    }

    private static void RequireV9()
    {
        if (CanonicalWorldStateHasher.FormatVersion != 9U)
            throw new NotSupportedException(
                "This snapshot codec is v9-only and includes all transport facility identities.");
    }

    private sealed class Writer
    {
        private readonly Stream _stream;
        internal Writer(Stream stream) => _stream = stream;
        internal void Bytes(ReadOnlySpan<byte> value) => _stream.Write(value);
        internal void U32(uint value)
        {
            Span<byte> data = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(data, value);
            Bytes(data);
        }
        internal void U64(ulong value)
        {
            Span<byte> data = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(data, value);
            Bytes(data);
        }
        internal void Count(int count) => U32(checked((uint)count));
    }

    private sealed class Reader
    {
        private readonly byte[] _bytes;
        private readonly int _end;
        private int _offset;
        internal Reader(byte[] bytes, int end)
        {
            _bytes = bytes;
            _end = end;
        }
        internal ReadOnlySpan<byte> Bytes(int length)
        {
            if (length < 0 || length > _end - _offset)
                throw new InvalidDataException("World snapshot has truncated records.");
            var data = _bytes.AsSpan(_offset, length);
            _offset += length;
            return data;
        }
        internal uint U32() => BinaryPrimitives.ReadUInt32BigEndian(Bytes(4));
        internal ulong U64() => BinaryPrimitives.ReadUInt64BigEndian(Bytes(8));
        internal int Count(int minimumRowLength)
        {
            var count = U32();
            if (count > (uint)((_end - _offset) / minimumRowLength))
                throw new InvalidDataException("World snapshot claims impossible record count.");
            return checked((int)count);
        }
        internal void RequireEnd()
        {
            if (_offset != _end)
                throw new InvalidDataException("World snapshot has unexpected trailing data.");
        }
    }
}
