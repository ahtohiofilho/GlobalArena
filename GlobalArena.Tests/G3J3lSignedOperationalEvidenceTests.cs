using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class G3J3lSignedOperationalEvidenceTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = respond;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
        protected override HttpResponseMessage Send(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _respond(request);
    }

    private static HttpResponseMessage JsonResponse(
        HttpRequestMessage request, object value) =>
        new(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(
                JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };

    private static EconomicV9ExternalAnchor Anchor() =>
        new(new string('A', 64), new string('0', 64), 0UL, 0UL);

    [Fact]
    public void J3lFreshSignedFencingIsRequiredAtEachAdministrativeCheck()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        EconomicV9FencingAttestation? saved = null;
        var forceReplay = false;
        var denyWriter = false;
        var expired = false;
        var corruptSignature = false;
        using var http = new HttpClient(new Handler(request =>
        {
            var query = request.RequestUri!.Query;
            var challenge = query.Split("challenge=", StringSplitOptions.None)[1];
            var record = new EconomicV9FencingAttestation(
                "ga-prod-cluster", "j3l-game", 7L, challenge,
                "fence-event-932", true, !denyWriter,
                now - 1, expired ? now - 1 : now + 30, "");
            if (forceReplay && saved is not null)
                record = saved;
            else
            {
                var signature = key.SignData(
                    EconomicV9OperationalProofCodec.EncodeFencing(record),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
                record = record with
                {
                    SignatureBase64 = corruptSignature
                        ? Convert.ToBase64String(new byte[64])
                        : Convert.ToBase64String(signature)
                };
                saved = record;
            }
            return JsonResponse(request, record);
        }));

        using var verifier = new EconomicV9HttpSignedFencingVerifier(
            http, new Uri("https://fencing.example.invalid/v1/fencing/evidence"),
            key.ExportSubjectPublicKeyInfoPem(), "ga-prod-cluster");
        Assert.True(verifier.IsIsolated("j3l-game", 7L));
        Assert.True(verifier.IsIsolated("j3l-game", 7L));

        forceReplay = true;
        Assert.False(verifier.IsIsolated("j3l-game", 7L));
        forceReplay = false;
        denyWriter = true;
        Assert.False(verifier.IsIsolated("j3l-game", 7L));
        denyWriter = false;
        expired = true;
        Assert.False(verifier.IsIsolated("j3l-game", 7L));
        expired = false;
        corruptSignature = true;
        Assert.False(verifier.IsIsolated("j3l-game", 7L));
        corruptSignature = false;
        Assert.False(verifier.IsIsolated("j3l-other", 7L));
        Assert.False(verifier.IsIsolated("j3l-game", 8L));

        Assert.Throws<ArgumentException>(() =>
            new EconomicV9HttpSignedFencingVerifier(http,
                new Uri("http://fencing.example.invalid/v1/fencing/evidence"),
                key.ExportSubjectPublicKeyInfoPem(), "ga-prod-cluster"));
    }

    [Fact]
    public void J3lSignedRetentionAndActualVersionedArchiveMustBothMatch()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var epoch = 4L;
        var game = "j3l-backup";
        var anchor = Anchor();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var original = Encoding.UTF8.GetBytes(
            "GA GAFULL09 versioned backup - independent immutable test object");
        byte[] fetched = (byte[])original.Clone();
        bool retentionLocked = true;
        bool expireReceipt = false;
        bool forgeSignature = false;
        bool useOldAnchor = false;

        using var http = new HttpClient(new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("manifest",
                    StringComparison.Ordinal))
            {
                var proof = new EconomicV9ImmutableBackupReceipt(
                    game, epoch,
                    useOldAnchor
                        ? new EconomicV9ExternalAnchor(new string('B', 64),
                            new string('C', 64), 1UL, 1UL)
                        : anchor,
                    "backup-127", "object-version-321",
                    Convert.ToHexString(SHA256.HashData(original)),
                    original.Length, retentionLocked,
                    expireReceipt ? now + 1 : now + 86400 * 7,
                    now - 1, "");
                var signature = signer.SignData(
                    EconomicV9OperationalProofCodec.EncodeBackup(proof),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
                return JsonResponse(request, proof with
                {
                    SignatureBase64 = forgeSignature
                        ? Convert.ToBase64String(new byte[64])
                        : Convert.ToBase64String(signature)
                });
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(fetched)
            };
        }));
        using var gate = new EconomicV9SignedImmutableBackupGate(
            http, new Uri("https://backups.example.invalid/v1/manifest"),
            new Uri("https://backups.example.invalid/v1/archive"),
            signer.ExportSubjectPublicKeyInfoPem());

        Assert.True(gate.VerifyBackup(game, epoch, anchor));
        fetched = (byte[])original.Clone();
        fetched[0] ^= 1;
        Assert.False(gate.VerifyBackup(game, epoch, anchor));
        fetched = original;
        retentionLocked = false;
        Assert.False(gate.VerifyBackup(game, epoch, anchor));
        retentionLocked = true;
        expireReceipt = true;
        Assert.False(gate.VerifyBackup(game, epoch, anchor));
        expireReceipt = false;
        forgeSignature = true;
        Assert.False(gate.VerifyBackup(game, epoch, anchor));
        forgeSignature = false;
        useOldAnchor = true;
        Assert.False(gate.VerifyBackup(game, epoch, anchor));
        useOldAnchor = false;
        Assert.False(gate.VerifyBackup(game, epoch + 1, anchor));
        Assert.False(gate.VerifyBackup("other-game", epoch, anchor));
    }

    [Fact]
    public void J3lNetworkFailureAndUntrustedFencingSignatureFailClosed()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var another = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var http = new HttpClient(new Handler(request =>
        {
            var challenge = request.RequestUri!.Query.Split("challenge=")[1];
            var statement = new EconomicV9FencingAttestation(
                "cluster", "j3l-game", 1, challenge, "event", true, true,
                now, now + 30, "");
            var signature = signer.SignData(
                EconomicV9OperationalProofCodec.EncodeFencing(statement),
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            return JsonResponse(request, statement with
            {
                SignatureBase64 = Convert.ToBase64String(signature)
            });
        }));
        using var verifier = new EconomicV9HttpSignedFencingVerifier(
            http, new Uri("https://fencing.example.invalid/evidence"),
            another.ExportSubjectPublicKeyInfoPem(), "cluster");
        Assert.False(verifier.IsIsolated("j3l-game", 1));

        using var offline = new HttpClient(new Handler(_ =>
            throw new HttpRequestException("unreachable")));
        using var blocked = new EconomicV9HttpSignedFencingVerifier(
            offline, new Uri("https://fencing.example.invalid/evidence"),
            signer.ExportSubjectPublicKeyInfoPem(), "cluster");
        Assert.False(blocked.IsIsolated("j3l-game", 1));
    }
}
