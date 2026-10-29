using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3dMixedDurableV9CommitTests
{
    private sealed class J3mArchiveSource : IEconomicV9VerifiedBackupArchiveSource
    {
        private readonly byte[] _bytes;
        public bool Accepted { get; set; } = true;
        public int Downloads { get; private set; }
        public J3mArchiveSource(byte[] bytes) => _bytes = bytes;

        public bool VerifyBackup(string gameId, long writerEpoch,
            EconomicV9ExternalAnchor anchor) => Accepted;

        public bool TryDownloadVerifiedArchive(string gameId, long writerEpoch,
            EconomicV9ExternalAnchor anchor, Stream destination)
        {
            Downloads++;
            Assert.Equal("j3l-restore", gameId);
            Assert.Equal(3L, writerEpoch);
            if (!Accepted) return false;
            destination.Write(_bytes);
            return true;
        }
    }

    private sealed class J3mHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public J3mHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = respond;
        protected override HttpResponseMessage Send(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _respond(request);
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }

    private static byte[] J3mZipSave(string source)
    {
        using var output = new MemoryStream();
        EconomicV9VersionedSaveArchive.WriteArchive(source, output);
        return output.ToArray();
    }

    [Fact]
    public void J3mVersionedEcologicalJournalReplaysBeforePublishingNewHead()
    {
        var world = Generate();
        var genesis = Initial(world);
        var parent = Path.Combine(Path.GetTempPath(),
            "ga-j3m-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(parent, "original");
        var recovered = Path.Combine(parent, "recovered");
        Directory.CreateDirectory(source);
        try
        {
            EconomicV9ExternalAnchor anchor;
            using (var host = EconomicCompleteV9DurableSaveHost.Create(
                source, world, genesis))
            {
                host.CommitNext(genesis.Sha256Hex, 1UL, Demand(), Holds());
                anchor = new EconomicV9ExternalAnchor(
                    host.AcceptedSha256Hex, host.AcceptedJournalSha256Hex,
                    host.CommitSequence, host.LastAppliedTurn);
            }
            var archive = new J3mArchiveSource(J3mZipSave(source));
            var witness = new J3lEpochWitness(anchor);
            var primary = new J3iStaticPrimary(
                new EconomicV9ExternalAnchorSnapshot(anchor));
            var recovery = new EconomicV9FailoverAdmissionGate(
                "j3l-restore", primary, witness);

            Assert.False(Directory.Exists(recovered));
            using (var host = recovery.RestoreFromVerifiedVersionedBackup(
                recovered, world, archive))
            {
                Assert.Equal(anchor.CheckpointSha256Hex,
                    host.AcceptedSha256Hex);
                Assert.Equal(anchor.JournalSha256Hex,
                    host.AcceptedJournalSha256Hex);
                Assert.Equal(1UL, host.CommitSequence);
                Assert.Equal(1UL, host.LastAppliedTurn);
                Assert.True(File.Exists(Path.Combine(
                    recovered, "full-v9-head.gahead")));
            }
            Assert.Equal(1, archive.Downloads);
            Assert.Throws<IOException>(() =>
                recovery.RestoreFromVerifiedVersionedBackup(
                    recovered, world, archive));
            Assert.Equal(1, archive.Downloads);
            Assert.DoesNotContain(Directory.EnumerateDirectories(parent),
                p => Path.GetFileName(p).StartsWith(
                    ".ga-j3m-quarantine-", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void J3mRejectedSourceAndStaleAuthorityNeverPublishDestination()
    {
        var world = Generate();
        var initial = Initial(world);
        var parent = Path.Combine(Path.GetTempPath(),
            "ga-j3m-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(parent, "original");
        var destination = Path.Combine(parent, "blocked");
        Directory.CreateDirectory(source);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                source, world, initial)) { }
            var anchor = new EconomicV9ExternalAnchor(
                initial.Sha256Hex, new string('0', 64), 0UL, 0UL);
            var archive = new J3mArchiveSource(J3mZipSave(source))
            {
                Accepted = false
            };
            var witness = new J3lEpochWitness(anchor);
            var primary = new J3iStaticPrimary(
                new EconomicV9ExternalAnchorSnapshot(anchor));
            var recovery = new EconomicV9FailoverAdmissionGate(
                "j3l-restore", primary, witness);
            Assert.Throws<InvalidOperationException>(() =>
                recovery.RestoreFromVerifiedVersionedBackup(
                    destination, world, archive));
            Assert.False(Directory.Exists(destination));

            archive.Accepted = true;
            witness.State = new EconomicV9WitnessEpochState(4, anchor);
            // The source may not decide the writer epoch. If a real
            // provider cannot supply that version, verification is denied.
            var rejectedEpoch = new J3mArchiveSource(Array.Empty<byte>())
            {
                Accepted = false
            };
            Assert.Throws<InvalidOperationException>(() =>
                recovery.RestoreFromVerifiedVersionedBackup(
                    destination, world, rejectedEpoch));
            Assert.False(Directory.Exists(destination));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Theory]
    [InlineData("../escape.gahead")]
    [InlineData("subdir/escape.gahead")]
    [InlineData("subdir\\escape.gahead")]
    [InlineData("/absolute.gahead")]
    [InlineData("C:drive.gahead")]
    public void J3mZipSlipAndUnsafeNamesAreDenied(string evilEntry)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "ga-j3m-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var zipPath = Path.Combine(root, "hostile.zip");
            using (var file = File.Create(zipPath))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using (var head = zip.CreateEntry(
                    "full-v9-head.gahead").Open())
                    head.WriteByte(72);
                using (var inventory = zip.CreateEntry(
                    EconomicV9VersionedSaveArchive.InventoryEntry).Open())
                    inventory.WriteByte(123);
                using var output = zip.CreateEntry(evilEntry).Open();
                output.WriteByte(88);
            }
            Assert.Throws<InvalidDataException>(() =>
                EconomicV9VersionedSaveArchive.ExtractVerifiedArchive(
                    zipPath, Path.Combine(root, "unpacked")));
            Assert.False(File.Exists(Path.Combine(root, "escape.gahead")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void J3mArchiveWithUnexpectedEntryCannotBypassSignedInventory()
    {
        var world = Generate();
        var initial = Initial(world);
        var root = Path.Combine(Path.GetTempPath(),
            "ga-j3m-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "original");
        Directory.CreateDirectory(source);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                source, world, initial)) { }
            var zipPath = Path.Combine(root, "altered.zip");
            File.WriteAllBytes(zipPath, J3mZipSave(source));
            using (var file = new FileStream(zipPath,
                FileMode.Open, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Update))
            using (var output = zip.CreateEntry("unexpected.txt").Open())
                output.WriteByte(99);
            Assert.Throws<InvalidDataException>(() =>
                EconomicV9VersionedSaveArchive.ExtractVerifiedArchive(
                    zipPath, Path.Combine(root, "unpacked")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void J3mRealSignedVersionedHttpArchiveRestoresWithoutSecondFetch()
    {
        var world = Generate();
        var genesis = Initial(world);
        var root = Path.Combine(Path.GetTempPath(),
            "ga-j3m-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(root, "original");
        var target = Path.Combine(root, "restored");
        Directory.CreateDirectory(source);
        try
        {
            using (EconomicCompleteV9DurableSaveHost.Create(
                source, world, genesis)) { }
            var anchor = new EconomicV9ExternalAnchor(
                genesis.Sha256Hex, new string('0', 64), 0UL, 0UL);
            var archiveBytes = J3mZipSave(source);
            using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var requestsManifest = 0;
            var requestsArchive = 0;
            var sabotageArchive = false;
            using var http = new HttpClient(new J3mHttpHandler(request =>
            {
                if (request.RequestUri!.AbsolutePath.Contains("manifest"))
                {
                    requestsManifest++;
                    var receipt = new EconomicV9ImmutableBackupReceipt(
                        "j3l-restore", 3L, anchor,
                        "backup-approved", "version-7",
                        Convert.ToHexString(SHA256.HashData(archiveBytes)),
                        archiveBytes.Length, true, now + 86400 * 7,
                        now - 1, "");
                    var signature = signer.SignData(
                        EconomicV9OperationalProofCodec.EncodeBackup(receipt),
                        HashAlgorithmName.SHA256,
                        DSASignatureFormat.Rfc3279DerSequence);
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = request,
                        Content = new StringContent(JsonSerializer.Serialize(
                            receipt with {
                                SignatureBase64 = Convert.ToBase64String(signature)
                            }), Encoding.UTF8, "application/json")
                    };
                }
                requestsArchive++;
                var served = (byte[])archiveBytes.Clone();
                if (sabotageArchive) served[0] ^= 1;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = request,
                    Content = new ByteArrayContent(served)
                };
            }));
            using var verified = new EconomicV9SignedImmutableBackupGate(
                http, new Uri("https://backup.example.invalid/manifest"),
                new Uri("https://backup.example.invalid/archive"),
                signer.ExportSubjectPublicKeyInfoPem());
            var witness = new J3lEpochWitness(anchor);
            var primary = new J3iStaticPrimary(
                new EconomicV9ExternalAnchorSnapshot(anchor));
            var recovery = new EconomicV9FailoverAdmissionGate(
                "j3l-restore", primary, witness);

            sabotageArchive = true;
            Assert.Throws<InvalidOperationException>(() =>
                recovery.RestoreFromVerifiedVersionedBackup(
                    target, world, verified));
            Assert.False(Directory.Exists(target));
            sabotageArchive = false;

            using (var host = recovery.RestoreFromVerifiedVersionedBackup(
                target, world, verified))
                Assert.Equal(anchor.CheckpointSha256Hex,
                    host.AcceptedSha256Hex);
            Assert.Equal(2, requestsManifest);
            Assert.Equal(2, requestsArchive);
            Assert.True(File.Exists(Path.Combine(
                target, "full-v9-head.gahead")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
