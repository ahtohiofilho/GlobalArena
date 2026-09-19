namespace GlobalArena.Simulation;

public readonly record struct WorldStateHash
{
    public const int DigestByteLength = 32;

    public const int DigestHexLength =
        DigestByteLength * 2;

    public uint FormatVersion { get; }

    public string HexDigest { get; }

    public WorldStateHash(
        uint formatVersion,
        string hexDigest)
    {
        if (formatVersion == 0U)
        {
            throw new ArgumentOutOfRangeException(
                nameof(formatVersion),
                "Format version must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(hexDigest);

        if (hexDigest.Length != DigestHexLength)
        {
            throw new ArgumentException(
                "Digest must contain exactly 64 hexadecimal characters.",
                nameof(hexDigest));
        }

        for (var index = 0;
             index < hexDigest.Length;
             index++)
        {
            if (!IsHexCharacter(
                hexDigest[index]))
            {
                throw new ArgumentException(
                    "Digest must contain only hexadecimal characters.",
                    nameof(hexDigest));
            }
        }

        FormatVersion = formatVersion;
        HexDigest = hexDigest.ToUpperInvariant();
    }

    private static bool IsHexCharacter(
        char value)
    {
        return value is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f';
    }
}
