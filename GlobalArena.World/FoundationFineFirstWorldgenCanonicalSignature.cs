using System.Buffers.Binary;
using System.Security.Cryptography;

namespace GlobalArena.World;

/// <summary>
/// R83 migration boundary: domain-separated canonical SHA-256 for the
/// independent G(fine) physical geography, its fine-to-coarse projection and
/// the exact existing V1 strategic world it was constructed beside.
/// Does NOT mutate the legacy WorldGenerationCanonicalSignature or claim
/// that WorldState and its event executor now support fine-first physics.
/// </summary>
public sealed record FoundationFineFirstWorldgenIdentity(
    int FormatVersion,
    string Sha256Hex,
    WorldGenerationSignature LegacyStrategicSignature,
    GoldbergParameters FineParameters,
    int FineTileCount);

public static class FoundationFineFirstWorldgenCanonicalSignature
{
    public const int CurrentFormatVersion = 1;
    private const ulong Magic = 0x474146494E453031UL; // GAFINE01

    public static FoundationFineFirstWorldgenIdentity Compute(
        WorldGenerationResult strategicWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine)
    {
        ArgumentNullException.ThrowIfNull(strategicWorld);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(fine);
        if (strategicWorld.Request.Seed != fine.Seed ||
            strategicWorld.Request.StrategicParameters != fine.CoarseParameters ||
            incidence.Refinement.FineParameters != fine.FineParameters ||
            incidence.Refinement.CoarseParameters != fine.CoarseParameters ||
            incidence.TileIncidences.Count != fine.FineElevationRaw.Count ||
            fine.FineMoistureRaw.Count != fine.FineElevationRaw.Count ||
            fine.FineTemperatureRaw.Count != fine.FineElevationRaw.Count ||
            fine.StrategicMeanElevationRaw.Count !=
                incidence.CoarseTopology.Cells.Count ||
            fine.StrategicLandTileCount.Count !=
                incidence.CoarseTopology.Cells.Count ||
            fine.StrategicIncidentTileCount.Count !=
                incidence.CoarseTopology.Cells.Count)
            throw new InvalidOperationException(
                "Fine worldgen identity cannot be issued for unrelated " +
                "seed, scales, topology or incomplete physical fields.");

        var legacy = WorldGenerationCanonicalSignature.Compute(strategicWorld);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        void WriteInt32(int n)
        {
            Span<byte> b = stackalloc byte[4];
            BinaryPrimitives.WriteInt32BigEndian(b, n);
            hash.AppendData(b);
        }
        void WriteInt64(long n)
        {
            Span<byte> b = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(b, n);
            hash.AppendData(b);
        }
        void WriteUInt64(ulong n)
        {
            Span<byte> b = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(b, n);
            hash.AppendData(b);
        }
        void WriteArray(IReadOnlyList<long> numbers)
        {
            WriteInt32(numbers.Count);
            foreach (long number in numbers) WriteInt64(number);
        }
        void WriteCounts(IReadOnlyList<int> numbers)
        {
            WriteInt32(numbers.Count);
            foreach (int number in numbers) WriteInt32(number);
        }

        WriteUInt64(Magic);
        WriteInt32(CurrentFormatVersion);
        hash.AppendData(Convert.FromHexString(legacy.Sha256Hex));
        WriteInt32(legacy.FormatVersion);
        WriteUInt64(fine.Seed.Value);
        WriteInt32(fine.CoarseParameters.M);
        WriteInt32(fine.CoarseParameters.N);
        WriteInt32(fine.FineParameters.M);
        WriteInt32(fine.FineParameters.N);
        WriteInt32(incidence.TileIncidences.Count);
        WriteInt32(incidence.FineTopology.Edges.Count);
        WriteInt32(incidence.CoarseTopology.Cells.Count);
        WriteInt64(fine.SeaLevelRaw);
        WriteInt32(fine.LandTileCount);
        WriteArray(fine.FineElevationRaw);
        WriteArray(fine.FineMoistureRaw);
        WriteArray(fine.FineTemperatureRaw);
        WriteArray(fine.StrategicMeanElevationRaw);
        WriteCounts(fine.StrategicLandTileCount);
        WriteCounts(fine.StrategicIncidentTileCount);

        // Topological membership is also part of provenance: do not claim
        // an identical signed physical map with different fine/coarse ties.
        foreach (var tile in incidence.TileIncidences)
        {
            WriteUInt64(tile.PhysicalTacticalTileId.FineStrategicCellId.Value);
            WriteInt32(tile.IncidentCoarseCellIds.Count);
            foreach (var coarse in tile.IncidentCoarseCellIds)
                WriteUInt64(coarse.Value);
        }

        string digest = Convert.ToHexString(hash.GetHashAndReset())
            .ToLowerInvariant();
        return new FoundationFineFirstWorldgenIdentity(
            CurrentFormatVersion, digest, legacy, fine.FineParameters,
            fine.FineElevationRaw.Count);
    }

    /// <summary>
    /// The migration may be admitted only by a caller that supplies the
    /// complete independently regenerated fine geography. In particular
    /// the legacy V1 world signature is NOT a sufficient proof of identity.
    /// </summary>
    public static void RequireExact(
        FoundationFineFirstWorldgenIdentity proposed,
        WorldGenerationResult strategicWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        if (proposed.FormatVersion != CurrentFormatVersion ||
            proposed.Sha256Hex.Length != 64 ||
            proposed.Sha256Hex != Compute(strategicWorld, incidence, fine)
                .Sha256Hex ||
            proposed.LegacyStrategicSignature !=
                WorldGenerationCanonicalSignature.Compute(strategicWorld))
            throw new InvalidOperationException(
                "A fine-first physical world must match its own " +
                "versioned signed fields, not only the V1 strategic world.");
    }
}
