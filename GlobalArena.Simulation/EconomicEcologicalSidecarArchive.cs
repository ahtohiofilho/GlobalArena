using System.Security.Cryptography;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable, content-addressed ecological sidecars in a host-controlled
/// directory. The logical name is the G3-G canonical checkpoint commitment;
/// the G3-H codec independently checks bytes, world binding and semantics.
/// This is NOT a complete WorldState save, nor an authenticated trust anchor.
/// </summary>
public sealed class EconomicEcologicalSidecarArchive
{
    public string DirectoryPath { get; }

    public EconomicEcologicalSidecarArchive(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) ||
            !Path.IsPathFullyQualified(directoryPath))
            throw new ArgumentException(
                "Sidecar archive requires an absolute directory path.",
                nameof(directoryPath));

        DirectoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(DirectoryPath);
    }

    public void Store(EconomicEcologicalCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var digest = NormalizeDigest(checkpoint.Sha256Hex);
        var target = Path.Combine(DirectoryPath, digest + ".gaeco");
        var bytes = EconomicEcologicalSidecarCodec.Encode(checkpoint);

        if (File.Exists(target))
        {
            VerifyExisting(target, bytes);
            return;
        }

        // Same directory: File.Move installs a complete blob or leaves the
        // prior state untouched. Orphaned temporary files from a crash
        // cannot be mistaken for committed checkpoint paths.
        var staging = Path.Combine(DirectoryPath,
            ".pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(staging, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            try
            {
                File.Move(staging, target);
            }
            catch (IOException) when (File.Exists(target))
            {
                // A concurrent writer may have installed the same digest.
                // Different bytes under the same digest are never accepted.
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
        WorldGenerationResult generatedWorld, WorldState world,
        string acceptedCheckpointSha256Hex)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        var digest = NormalizeDigest(acceptedCheckpointSha256Hex);
        var path = Path.Combine(DirectoryPath, digest + ".gaeco");
        var decoded = EconomicEcologicalSidecarCodec.Decode(
            generatedWorld, world, File.ReadAllBytes(path));
        decoded.RequireExpectedDigest(digest);
        return decoded;
    }

    public string CheckpointPath(string sha256Hex) =>
        Path.Combine(DirectoryPath, NormalizeDigest(sha256Hex) + ".gaeco");

    internal static string NormalizeDigest(string sha256Hex)
    {
        ArgumentNullException.ThrowIfNull(sha256Hex);
        if (sha256Hex.Length != 64 || !sha256Hex.All(Uri.IsHexDigit))
            throw new ArgumentException("Expected a 64-character SHA-256 digest.",
                nameof(sha256Hex));
        return sha256Hex.ToUpperInvariant();
    }

    private static void VerifyExisting(string path, byte[] expected)
    {
        var present = File.ReadAllBytes(path);
        if (!CryptographicOperations.FixedTimeEquals(present, expected))
            throw new InvalidDataException(
                "A checkpoint path already exists with different or corrupted bytes.");
    }
}
