using System.Security.Cryptography;

namespace GlobalArena.Simulation;

/// <summary>
/// Immutable content-addressed archival store for PRE-PUBLICATION military
/// command audit witnesses. No head file is modified and this archive never
/// makes a candidate WorldState authoritative. Digest trust and real player
/// authentication are responsibilities of the caller.
/// </summary>
public sealed class AuthorizedMilitaryMoveAuditArchive
{
    public string DirectoryPath { get; }

    public AuthorizedMilitaryMoveAuditArchive(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) ||
            !Path.IsPathFullyQualified(directoryPath))
            throw new ArgumentException(
                "Military command audit archive requires an absolute directory.",
                nameof(directoryPath));
        DirectoryPath = Path.GetFullPath(directoryPath);
        Directory.CreateDirectory(DirectoryPath);
    }

    public string WitnessPath(string sha256Hex) =>
        Path.Combine(DirectoryPath,
            EconomicEcologicalSidecarArchive.NormalizeDigest(sha256Hex) + ".gamove");

    public void Store(AuthorizedMilitaryMoveAuditProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var path = WitnessPath(projection.WitnessSha256Hex);
        var bytes = projection.Witness;
        if (File.Exists(path))
        {
            RequireSameBytes(path, bytes);
            return;
        }

        var staging = Path.Combine(DirectoryPath,
            ".gamove-pending-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(staging,
                FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            try
            {
                File.Move(staging, path);
            }
            catch (IOException) when (File.Exists(path))
            {
                RequireSameBytes(path, bytes);
            }
        }
        finally
        {
            if (File.Exists(staging))
                File.Delete(staging);
        }
    }

    /// <summary>
    /// Re-executes the recorded commands against the accepted v9 snapshot,
    /// including a fresh authorization decision, rather than trusting file
    /// checksum alone. No mutation or head publication.
    /// </summary>
    public AuthorizedMilitaryMoveAuditProjection LoadVerified(
        AuthorizedMilitaryMoveTurnAudit verifier,
        EconomicEcologicalCheckpoint accepted,
        string independentlyTrustedAcceptedCheckpointSha256Hex,
        string independentlyTrustedWitnessSha256Hex)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        var name = EconomicEcologicalSidecarArchive.NormalizeDigest(
            independentlyTrustedWitnessSha256Hex);
        var bytes = File.ReadAllBytes(WitnessPath(name));
        if (!CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(bytes), Convert.FromHexString(name)))
            throw new InvalidDataException(
                "Military movement witness does not match its trusted content hash.");
        var result = verifier.VerifyReplay(accepted,
            independentlyTrustedAcceptedCheckpointSha256Hex, bytes);
        if (result.WitnessSha256Hex != name)
            throw new InvalidDataException("Replayed witness commitment differs.");
        return result;
    }

    private static void RequireSameBytes(string path, byte[] expected)
    {
        if (!CryptographicOperations.FixedTimeEquals(
            File.ReadAllBytes(path), expected))
            throw new InvalidDataException(
                "Existing command witness path contains different or damaged bytes.");
    }
}
