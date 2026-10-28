using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Versioned, canonical SHA-256 integrity commitment over the EXISTING
/// WorldState hash and the two G3 candidate books. This is not a new
/// CanonicalWorldStateHasher format, nor proof of genuine event provenance.
/// The caller must retain the accepted digest as its compare-and-swap gate.
/// </summary>
public sealed class EconomicEcologicalCheckpoint
{
    public const uint CommitmentVersion = 1U;

    public WorldState World { get; }
    public EconomicNaturalCapitalBook NaturalCapital { get; }
    public EconomicMaterialReserveBook MaterialReserves { get; }
    public EconomicNaturalCapitalPolicy Policy { get; }
    public EconomicPrimitiveCommodityMapping Commodities { get; }
    public WorldStateHash WorldHash { get; }
    public string Sha256Hex { get; }

    public EconomicEcologicalCheckpoint(
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
            throw new InvalidOperationException("Checkpoint needs the exact bound generated world.");
        if (naturalCapital.DerivationVersion !=
            StrategicPrimitiveNaturalEndowmentFieldSet.DerivationVersion)
            throw new InvalidOperationException("Checkpoint derivation version is unsupported.");
        if (naturalCapital.Cells.Count != generatedWorld.StrategicSurfaceGraph.NodeCount)
            throw new InvalidOperationException("Checkpoint must cover every strategic cell.");

        for (var i = 0; i < naturalCapital.Cells.Count; i++)
        {
            if (naturalCapital.Cells[i].CellId !=
                generatedWorld.StrategicSurfaceGraph.GetCellId(i))
                throw new InvalidOperationException("Checkpoint natural capital must match the world topology.");
        }

        var points = world.Economy.EconomicPoints.ToDictionary(x => x.Id);
        foreach (var balance in materialReserves.Balances)
            if (!points.ContainsKey(balance.EconomicPointId))
                throw new InvalidOperationException(
                    "Checkpoint material reserve references an unknown economic point.");

        var owners = world.Civilizations.Civilizations.ToHashSet();
        foreach (var cursor in materialReserves.AppliedTurnCursors)
        {
            if (!owners.Contains(cursor.CivilizationId) ||
                cursor.LastAppliedTurn > naturalCapital.LastAppliedTurn)
                throw new InvalidOperationException(
                    "Checkpoint reserve cursor references an unknown civilization or future turn.");
        }

        World = world;
        NaturalCapital = naturalCapital;
        MaterialReserves = materialReserves;
        Policy = policy;
        Commodities = commodities;
        WorldHash = new CanonicalWorldStateHasher().Compute(world);
        Sha256Hex = ComputeDigest();
    }

    public void RequireExpectedDigest(string expectedSha256Hex)
    {
        ArgumentNullException.ThrowIfNull(expectedSha256Hex);
        if (!string.Equals(expectedSha256Hex, Sha256Hex, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Stale or inconsistent economic-ecological checkpoint.");
    }

    private string ComputeDigest() => ComputeDigestForWorldHash(
        WorldHash, NaturalCapital, MaterialReserves, Policy, Commodities);

    /// <summary>
    /// Historical commitment algorithm: the same semantic content may be
    /// verified against the original, independently authenticated v8 hash.
    /// Never constructs a v9 checkpoint until verification is complete.
    /// </summary>
    internal static string ComputeDigestForWorldHash(
        WorldStateHash worldHash,
        EconomicNaturalCapitalBook naturalCapital,
        EconomicMaterialReserveBook materialReserves,
        EconomicNaturalCapitalPolicy policy,
        EconomicPrimitiveCommodityMapping commodities)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        void RawUInt32(uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
            hash.AppendData(bytes);
        }
        void RawUInt64(ulong value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
            hash.AppendData(bytes);
        }
        void RawInt64(long value)
        {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        hash.AppendData(Encoding.ASCII.GetBytes("GAECO-CHECKPOINT"));
        RawUInt32(CommitmentVersion);

        RawUInt32(10U); // Versioned WorldState hash (v9 on this integration branch).
        RawUInt32(worldHash.FormatVersion);
        hash.AppendData(Convert.FromHexString(worldHash.HexDigest));

        RawUInt32(20U); // Versioned ecological state.
        RawUInt32(naturalCapital.DerivationVersion);
        RawUInt64(naturalCapital.LastAppliedTurn);
        RawUInt32(checked((uint)naturalCapital.Cells.Count));
        foreach (var cell in naturalCapital.Cells)
        {
            RawUInt64(cell.CellId.Value);
            RawUInt64(cell.RawWoodCarryingCapacity);
            RawUInt64(cell.RawStandingWood);
            RawUInt64(cell.RawSurfaceStone);
            RawInt64(cell.RawSoilVitality);
        }

        RawUInt32(30U); // Policy determines every future turn.
        RawUInt64(policy.RawWoodAtFullEndowment);
        RawUInt64(policy.RawStoneAtFullEndowment);
        RawInt64(policy.RawWoodRecoveryFraction);
        RawInt64(policy.RawSoilRecoveryFraction);
        RawInt64(policy.RawSoilWearPerFoodUnit);

        RawUInt32(40U); // Deliberately retained physical commodities.
        RawUInt32(checked((uint)materialReserves.Balances.Count));
        foreach (var balance in materialReserves.Balances)
        {
            RawUInt64(balance.EconomicPointId.Value);
            RawUInt32(balance.CommodityId.Value);
            RawUInt64(balance.RawQuantity);
        }

        RawUInt32(checked((uint)materialReserves.AppliedTurnCursors.Count));
        foreach (var cursor in materialReserves.AppliedTurnCursors)
        {
            RawUInt64(cursor.CivilizationId.Value);
            RawUInt64(cursor.LastAppliedTurn);
        }

        RawUInt32(50U); // Versioned semantic commodity mapping.
        RawUInt32(commodities.Food.Value);
        RawUInt32(commodities.Wood.Value);
        RawUInt32(commodities.Stone.Value);

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
