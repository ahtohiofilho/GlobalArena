using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// G3-I2 opt-in COMPLETE v9 snapshot envelope: serialized WorldState v8 and
/// G3-H ecological sidecar are held in one self-contained, canonical blob.
/// The container is data only: loading it does not authorize a player's
/// command, prove an untrusted storage device is latest, or upgrade to v9.
/// </summary>
public static class EconomicCompleteV9SaveBundleCodec
{
    public const uint FormatVersion = 1U;
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GAFULL09");
    private const int HeaderLength = 8 + 4 + 4 + 4;
    private const int DigestSize = 32;

    public static byte[] Encode(EconomicEcologicalCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var world = EconomicWorldStateV9SnapshotCodec.Encode(checkpoint.World);
        var ecology = EconomicEcologicalSidecarCodec.Encode(checkpoint);
        using var stream = new MemoryStream();
        stream.Write(Magic);
        Span<byte> field = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(field, FormatVersion);
        stream.Write(field);
        BinaryPrimitives.WriteUInt32BigEndian(field, checked((uint)world.Length));
        stream.Write(field);
        BinaryPrimitives.WriteUInt32BigEndian(field, checked((uint)ecology.Length));
        stream.Write(field);
        stream.Write(world);
        stream.Write(ecology);
        stream.Write(SHA256.HashData(stream.ToArray()));
        return stream.ToArray();
    }

    public static EconomicEcologicalCheckpoint Decode(
        WorldGenerationResult generatedWorld, byte[] encoded)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(encoded);
        if (encoded.Length < HeaderLength + DigestSize)
            throw new InvalidDataException("Full save bundle is truncated.");

        var end = encoded.Length - DigestSize;
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(encoded.AsSpan(0, end)),
            encoded.AsSpan(end, DigestSize)))
            throw new InvalidDataException("Full save bundle checksum mismatch.");

        if (!encoded.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Unknown full save bundle format.");
        var version = BinaryPrimitives.ReadUInt32BigEndian(
            encoded.AsSpan(8, 4));
        if (version != FormatVersion)
            throw new NotSupportedException("Unsupported full save bundle version.");

        var worldLength = BinaryPrimitives.ReadUInt32BigEndian(
            encoded.AsSpan(12, 4));
        var ecologyLength = BinaryPrimitives.ReadUInt32BigEndian(
            encoded.AsSpan(16, 4));
        if ((ulong)worldLength + ecologyLength != (ulong)end - HeaderLength)
            throw new InvalidDataException("Full save bundle has mismatched lengths.");

        var worldBytes = encoded.AsSpan(
            HeaderLength, checked((int)worldLength)).ToArray();
        var ecologyBytes = encoded.AsSpan(
            HeaderLength + checked((int)worldLength),
            checked((int)ecologyLength)).ToArray();

        // World is reconstructed and checked against its authoritative
        // v9 hash BEFORE allowing the ecological sidecar to be decoded.
        var world = EconomicWorldStateV9SnapshotCodec.Decode(
            generatedWorld, worldBytes);
        var snapshot = EconomicEcologicalSidecarCodec.Decode(
            generatedWorld, world, ecologyBytes);

        // A signed-looking but non-canonical bundle cannot be silently
        // normalized into a save whose bytes were never reviewed.
        if (!CryptographicOperations.FixedTimeEquals(
            Encode(snapshot), encoded))
            throw new InvalidDataException(
                "Full save bundle is not canonically encoded.");

        return snapshot;
    }
}

/// <summary>
/// Immutable content-addressed archive of complete v9 save bundles.
/// Unlike a mutable head, this does not know which checkpoint is the latest:
/// the caller must store the accepted digest in a trusted independent place.
/// </summary>
public sealed class EconomicCompleteV9SaveBundleArchive
{
    public string DirectoryPath { get; }

    public EconomicCompleteV9SaveBundleArchive(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) ||
            !Path.IsPathFullyQualified(directoryPath))
            throw new ArgumentException("A full save archive needs an absolute path.",
                nameof(directoryPath));
        DirectoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(DirectoryPath);
    }

    public void Store(EconomicEcologicalCheckpoint snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var bytes = EconomicCompleteV9SaveBundleCodec.Encode(snapshot);
        var target = BundlePath(snapshot.Sha256Hex);

        if (File.Exists(target))
        {
            VerifyExisting(target, bytes);
            return;
        }

        var staging = Path.Combine(DirectoryPath,
            ".full-pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var file = new FileStream(
                staging, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            try
            {
                File.Move(staging, target);
            }
            catch (IOException) when (File.Exists(target))
            {
                VerifyExisting(target, bytes);
            }
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    public EconomicEcologicalCheckpoint Load(
        WorldGenerationResult generatedWorld,
        string externallyTrustedAcceptedSha256Hex)
    {
        var digest = EconomicEcologicalSidecarArchive.NormalizeDigest(
            externallyTrustedAcceptedSha256Hex);
        var result = EconomicCompleteV9SaveBundleCodec.Decode(
            generatedWorld, File.ReadAllBytes(BundlePath(digest)));
        result.RequireExpectedDigest(digest);
        return result;
    }

    public string BundlePath(string sha256Hex) =>
        Path.Combine(DirectoryPath,
            EconomicEcologicalSidecarArchive.NormalizeDigest(sha256Hex) + ".gafull09");

    private static void VerifyExisting(string path, byte[] bytes)
    {
        if (!CryptographicOperations.FixedTimeEquals(
            File.ReadAllBytes(path), bytes))
            throw new InvalidDataException(
                "Complete save bundle already exists with different bytes.");
    }
}
