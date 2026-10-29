using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlobalArena.Simulation;

/// <summary>
/// The signed outer backup receipt authenticates the WHOLE versioned ZIP.
/// Its canonical internal inventory additionally binds every file name,
/// uncompressed byte length and SHA-256, before any gameplay restore.
/// Only flat, regular GAFULL09 files are accepted. No links or subdirectories.
/// </summary>
public sealed record EconomicV9BackupInventoryEntry(
    string Name, long Length, string Sha256Hex);

public sealed record EconomicV9BackupInventory(
    int FormatVersion, EconomicV9BackupInventoryEntry[] Files);

public static class EconomicV9VersionedSaveArchive
{
    public const string InventoryEntry = "ga-v9-inventory.json";
    private const string HeadFile = "full-v9-head.gahead";
    private const string LeaseFile = ".full-v9-writer.lock";
    private const int MaximumFiles = 512;
    private const long MaximumExpandedBytes = 512L * 1024 * 1024;
    private const long MaximumFileBytes = 256L * 1024 * 1024;
    private const int MaximumManifestBytes = 96 * 1024;

    /// <summary>
    /// Trusted backup publisher; excludes the writer lease and refuses
    /// nested directories, symlinks and noncanonical filenames.
    /// The independently administered backup service MUST separately
    /// inspect Object Lock and sign the exact SHA-256/version of this ZIP.
    /// </summary>
    public static void WriteArchive(string saveDirectory, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(saveDirectory);
        ArgumentNullException.ThrowIfNull(destination);
        if (!destination.CanWrite)
            throw new ArgumentException("Writable ZIP stream required.",
                nameof(destination));
        if (Directory.EnumerateDirectories(saveDirectory).Any())
            throw new InvalidDataException("Nested save directories are not supported.");

        var paths = Directory.GetFiles(saveDirectory)
            .Where(path => Path.GetFileName(path) != LeaseFile)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
        if (paths.Length is < 1 or > MaximumFiles ||
            !paths.Any(p => Path.GetFileName(p) == HeadFile))
            throw new InvalidDataException("A complete v9 head and bounded files are required.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<EconomicV9BackupInventoryEntry>();
        long cumulative = 0;
        foreach (var path in paths)
        {
            var name = Path.GetFileName(path);
            ValidateName(name);
            if (name == InventoryEntry || !names.Add(name) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Unsafe save backup entry.");
            var info = new FileInfo(path);
            if (info.Length < 0 || info.Length > MaximumFileBytes ||
                checked(cumulative += info.Length) > MaximumExpandedBytes)
                throw new InvalidDataException("Save backup exceeds file limits.");
            using var file = File.OpenRead(path);
            files.Add(new EconomicV9BackupInventoryEntry(
                name, info.Length,
                Convert.ToHexString(SHA256.HashData(file))));
        }

        var manifest = new EconomicV9BackupInventory(1, files.ToArray());
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        if (manifestBytes.Length > MaximumManifestBytes)
            throw new InvalidDataException("Backup inventory too large.");

        using var zip = new ZipArchive(destination, ZipArchiveMode.Create,
            leaveOpen: true);
        var inventory = zip.CreateEntry(InventoryEntry,
            CompressionLevel.Optimal);
        using (var output = inventory.Open())
            output.Write(manifestBytes);
        foreach (var path in paths)
        {
            var entry = zip.CreateEntry(Path.GetFileName(path),
                CompressionLevel.Optimal);
            using var input = File.OpenRead(path);
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }

    /// <summary>
    /// The caller MUST first verify the external signature and SHA-256 of
    /// the exact versioned ZIP into a private quarantine. A new, empty
    /// quarantine/extraction directory is created by this method; never
    /// point it at a live game's save directory.
    /// </summary>
    public static EconomicV9BackupInventory ExtractVerifiedArchive(
        string trustedVersionedZip, string newEmptyExtractionDirectory)
    {
        ArgumentNullException.ThrowIfNull(trustedVersionedZip);
        ArgumentNullException.ThrowIfNull(newEmptyExtractionDirectory);
        if (Directory.Exists(newEmptyExtractionDirectory) ||
            File.Exists(newEmptyExtractionDirectory))
            throw new IOException("Refusing to extract into an existing path.");
        var archiveInfo = new FileInfo(trustedVersionedZip);
        if (archiveInfo.Length <= 0 ||
            archiveInfo.Length > 256L * 1024 * 1024)
            throw new InvalidDataException("Backup ZIP size outside limits.");
        using var file = new FileStream(trustedVersionedZip,
            FileMode.Open, FileAccess.Read, FileShare.Read);
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        if (zip.Entries.Count is < 2 or > MaximumFiles + 1)
            throw new InvalidDataException("Invalid ZIP entry count.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new Dictionary<string, ZipArchiveEntry>(
            StringComparer.Ordinal);
        long cumulative = 0;
        foreach (var entry in zip.Entries)
        {
            ValidateName(entry.FullName);
            if (!names.Add(entry.FullName) ||
                entry.Length < 0 || entry.Length > MaximumFileBytes ||
                checked(cumulative += entry.Length) >
                    MaximumExpandedBytes + MaximumManifestBytes ||
                IsLinkOrDirectory(entry))
                throw new InvalidDataException("Unsafe or duplicate ZIP entry.");
            entries.Add(entry.FullName, entry);
        }
        if (!entries.TryGetValue(InventoryEntry, out var inventoryEntry) ||
            inventoryEntry.Length is <= 0 or > MaximumManifestBytes ||
            !entries.ContainsKey(HeadFile))
            throw new InvalidDataException("Missing GAFULL09 head or inventory.");
        byte[] manifestBytes;
        using (var input = inventoryEntry.Open())
        using (var limited = new MemoryStream())
        {
            input.CopyTo(limited);
            if (limited.Length != inventoryEntry.Length)
                throw new InvalidDataException("Truncated ZIP inventory.");
            manifestBytes = limited.ToArray();
        }

        EconomicV9BackupInventory manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<EconomicV9BackupInventory>(
                manifestBytes) ?? throw new InvalidDataException(
                    "Missing backup inventory.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Unparseable backup inventory.", ex);
        }
        if (manifest.FormatVersion != 1 || manifest.Files is null ||
            manifest.Files.Length != entries.Count - 1 ||
            manifest.Files.Length is < 1 or > MaximumFiles ||
            !manifestBytes.AsSpan().SequenceEqual(
                JsonSerializer.SerializeToUtf8Bytes(manifest)))
            throw new InvalidDataException("Noncanonical or incomplete inventory.");

        var namesInManifest = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        string? preceding = null;
        long listedTotal = 0;
        foreach (var item in manifest.Files)
        {
            if (item is null || !entries.TryGetValue(item.Name, out var entry) ||
                item.Name == InventoryEntry ||
                !namesInManifest.Add(item.Name) ||
                item.Length != entry.Length ||
                item.Length < 0 || item.Length > MaximumFileBytes ||
                checked(listedTotal += item.Length) > MaximumExpandedBytes ||
                !IsUpperDigest(item.Sha256Hex) ||
                preceding is not null &&
                    string.CompareOrdinal(preceding, item.Name) >= 0)
                throw new InvalidDataException(
                    "Inventory does not match the bounded, ordered ZIP files.");
            preceding = item.Name;
        }
        if (namesInManifest.Count != entries.Count - 1 ||
            !namesInManifest.Contains(HeadFile))
            throw new InvalidDataException("ZIP contains uninventoried files.");

        Directory.CreateDirectory(newEmptyExtractionDirectory);
        foreach (var item in manifest.Files)
        {
            var entry = entries[item.Name];
            // Flat validated names and CreateNew under a private empty
            // directory: no archive path can escape the quarantine.
            var target = Path.Combine(newEmptyExtractionDirectory, item.Name);
            using var input = entry.Open();
            using var output = new FileStream(target,
                FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long copied = 0;
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                copied = checked(copied + count);
                if (copied > item.Length ||
                    copied > MaximumFileBytes)
                    throw new InvalidDataException("ZIP entry exceeded its declared length.");
                output.Write(buffer, 0, count);
                hash.AppendData(buffer, 0, count);
            }
            output.Flush(flushToDisk: true);
            if (copied != item.Length ||
                Convert.ToHexString(hash.GetHashAndReset()) !=
                    item.Sha256Hex)
                throw new InvalidDataException(
                    "Extracted save differs from the signed archive inventory.");
        }
        return manifest;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160 ||
            name.StartsWith(".", StringComparison.Ordinal) ||
            name.Contains("..", StringComparison.Ordinal) ||
            !name.All(c => c is >= 'A' and <= 'Z' or
                                >= 'a' and <= 'z' or
                                >= '0' and <= '9' or
                                '.' or '-' or '_'))
            throw new InvalidDataException("Unsafe or noncanonical ZIP entry name.");
    }

    private static bool IsLinkOrDirectory(ZipArchiveEntry entry)
    {
        var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixType == 0xA000 || unixType == 0x4000 ||
            entry.FullName.EndsWith("/", StringComparison.Ordinal);
    }

    private static bool IsUpperDigest(string value) =>
        value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
