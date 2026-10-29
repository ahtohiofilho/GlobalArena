namespace GlobalArena.Simulation;

public enum EconomicV9PublishedRecoveryState
{
    Absent,
    Unadmitted,
    PresentWithoutMarker,
    UnsafeReparsePoint
}

/// <summary>
/// Read-only local crash inventory. Never treats an existing directory or
/// an absent marker as proof of admission: independent primary/witness,
/// historical verifier and GAFULL09 replay are ALWAYS required.
/// </summary>
public sealed record EconomicV9VersionedCrashInspection(
    EconomicV9PublishedRecoveryState DestinationState,
    int StrandedQuarantineCount,
    bool DestinationRecoveryLeaseExists);

public static class EconomicV9VersionedRecoveryCrashInspector
{
    public static EconomicV9VersionedCrashInspection Inspect(
        string recoveryParent, string destinationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recoveryParent);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);
        if (destinationName is "." or ".." ||
            destinationName.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
            Path.GetFileName(destinationName) != destinationName)
            throw new ArgumentException(
                "A single destination directory name is required.",
                nameof(destinationName));
        var parent = Path.GetFullPath(recoveryParent);
        if (!Directory.Exists(parent) ||
            (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Unsafe or absent recovery parent.");
        var destination = Path.Combine(parent, destinationName);
        var state = EconomicV9PublishedRecoveryState.Absent;
        if (File.Exists(destination))
            throw new IOException("A file, not a directory, claims the destination.");
        if (Directory.Exists(destination))
        {
            var attr = File.GetAttributes(destination);
            state = (attr & FileAttributes.ReparsePoint) != 0
                ? EconomicV9PublishedRecoveryState.UnsafeReparsePoint
                : File.Exists(Path.Combine(destination, ".ga-v9-not-admitted"))
                    ? EconomicV9PublishedRecoveryState.Unadmitted
                    : EconomicV9PublishedRecoveryState.PresentWithoutMarker;
        }
        var quarantines = 0;
        foreach (var entry in Directory.EnumerateFileSystemEntries(
            parent, ".ga-j3m-quarantine-*", SearchOption.TopDirectoryOnly))
        {
            var attr = File.GetAttributes(entry);
            if ((attr & FileAttributes.ReparsePoint) != 0 ||
                (attr & FileAttributes.Directory) == 0)
                throw new IOException("Untrusted quarantine filesystem entry.");
            quarantines++;
        }
        var leaseName = ".ga-j3n-recovery-" + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(destinationName))) + ".lock";
        return new EconomicV9VersionedCrashInspection(
            state, quarantines, File.Exists(Path.Combine(parent, leaseName)));
    }
}
