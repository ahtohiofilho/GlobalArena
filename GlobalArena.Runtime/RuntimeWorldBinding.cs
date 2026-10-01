using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed record RuntimeWorldBinding
{
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

    public bool Contains(
        StrategicCellId strategicCellId)
    {
        return strategicCellId.IsValid
            && strategicCellId.Value
                <= StrategicCellCount;
    }
}
