using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed record RuntimeWorldBinding
{
    // Deliberately outside legacy V1 signature format namespace. Existing
    // worlds keep their original signature and canonical state hash.
    public const int FineFirstSignatureFormatVersion = 1001;

    public bool IsFineFirstWorld =>
        WorldSignatureFormatVersion == FineFirstSignatureFormatVersion;

    public int WorldSignatureFormatVersion { get; }

    public string WorldSignatureSha256Hex { get; }

    public ulong StrategicCellCount { get; }

    private RuntimeWorldBinding(
        int worldSignatureFormatVersion,
        string worldSignatureSha256Hex,
        ulong strategicCellCount)
    {
        if (worldSignatureFormatVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(worldSignatureFormatVersion),
                "World signature format version must be positive.");
        }

        ArgumentNullException.ThrowIfNull(
            worldSignatureSha256Hex);

        if (worldSignatureSha256Hex.Length != 64
            || worldSignatureSha256Hex.Any(
                character =>
                    !(
                        character >= '0'
                        && character <= '9')
                    && !(
                        character >= 'a'
                        && character <= 'f')))
        {
            throw new ArgumentException(
                "World signature must contain exactly 64 lowercase hexadecimal characters.",
                nameof(worldSignatureSha256Hex));
        }

        if (strategicCellCount == 0UL)
        {
            throw new ArgumentOutOfRangeException(
                nameof(strategicCellCount),
                "Strategic cell count must be positive.");
        }

        WorldSignatureFormatVersion =
            worldSignatureFormatVersion;

        WorldSignatureSha256Hex =
            worldSignatureSha256Hex;

        StrategicCellCount =
            strategicCellCount;
    }

    public static RuntimeWorldBinding FromGeneratedWorld(
        WorldGenerationResult generatedWorld)
    {
        ArgumentNullException.ThrowIfNull(
            generatedWorld);

        var signature =
            WorldGenerationCanonicalSignature.Compute(
                generatedWorld);

        return new RuntimeWorldBinding(
            signature.FormatVersion,
            signature.Sha256Hex,
            generatedWorld
                .Request
                .StrategicParameters
                .StrategicCellCount);
    }

    /// <summary>
    /// R84: explicit versioned identity for a physical-first world. Exact
    /// G(fine) fields and incidence must be independently regenerated and
    /// validated before this binding can exist. The *same* three hashed
    /// fields now identify fine-first or legacy worldgen unambiguously.
    /// This does not by itself migrate save files or authorize settled
    /// economic event replay against the new geography.
    /// </summary>
    public static RuntimeWorldBinding FromFineFirst(
        WorldGenerationResult strategicWorld,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine,
        FoundationFineFirstWorldgenIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(strategicWorld);
        FoundationFineFirstWorldgenCanonicalSignature.RequireExact(
            identity, strategicWorld, incidence, fine);
        return new RuntimeWorldBinding(
            FineFirstSignatureFormatVersion,
            identity.Sha256Hex,
            strategicWorld.Request.StrategicParameters.StrategicCellCount);
    }

    public bool Contains(
        StrategicCellId strategicCellId)
    {
        return strategicCellId.IsValid
            && strategicCellId.Value
                <= StrategicCellCount;
    }
}
