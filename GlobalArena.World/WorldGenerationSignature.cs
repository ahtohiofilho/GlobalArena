namespace GlobalArena.World;

public sealed record WorldGenerationSignature
{
    public const int CurrentFormatVersion =
        1;

    public int FormatVersion { get; }

    public string Sha256Hex { get; }

    public WorldGenerationSignature(
        int formatVersion,
        string sha256Hex)
    {
        if (formatVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(formatVersion),
                "Signature format version must be positive.");
        }

        ArgumentNullException.ThrowIfNull(
            sha256Hex);

        if (sha256Hex.Length != 64
            || sha256Hex.Any(
                character =>
                    !(
                        character >= '0'
                        && character <= '9')
                    && !(
                        character >= 'a'
                        && character <= 'f')))
        {
            throw new ArgumentException(
                "SHA-256 digest must contain exactly 64 lowercase hexadecimal characters.",
                nameof(sha256Hex));
        }

        FormatVersion =
            formatVersion;

        Sha256Hex =
            sha256Hex;
    }

    public override string ToString()
    {
        return Sha256Hex;
    }
}
