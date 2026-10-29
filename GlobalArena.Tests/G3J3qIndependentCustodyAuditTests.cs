using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed partial class G3J3pSupervisedOrphanCustodyTests
{
    private sealed class J3qLedgerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _reply;
        public J3qLedgerHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
            _reply = reply;
        protected override HttpResponseMessage Send(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _reply(request);
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_reply(request));
    }

    private static EconomicV9CustodyAuditAppendRequest J3qRead(
        HttpRequestMessage request) =>
        JsonSerializer.Deserialize<EconomicV9CustodyAuditAppendRequest>(
            request.Content!.ReadAsStringAsync().GetAwaiter().GetResult())!;

    private static EconomicV9CustodyAuditReceipt J3qSignReceipt(
        EconomicV9CustodyAuditAppendRequest request, ECDsa signingKey,
        long sequence, string mode = "")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var digest = Convert.ToHexString(SHA256.HashData(
            EconomicV9CustodyAuditProofCodec.EncodeEvent(request.Entry)));
        var receipt = new EconomicV9CustodyAuditReceipt(
            mode == "ledger" ? "wrong-ledger" : request.LedgerId,
            mode == "digest" ? new string('F', 64) : digest,
            mode == "nonce" ? new string('A', 64) : request.ChallengeHex,
            "receipt-" + sequence, sequence,
            mode != "not-durable",
            mode != "not-locked",
            mode == "stale" ? now - 120 : now,
            mode == "short-retention" ? now + 1 : now + 86400 * 7,
            "");
        var signedBytes = EconomicV9CustodyAuditProofCodec.EncodeReceipt(receipt);
        var signature = signingKey.SignData(signedBytes,
            HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        return receipt with
        {
            SignatureBase64 = mode == "tampered-signature"
                ? Convert.ToBase64String(new byte[64])
                : Convert.ToBase64String(signature)
        };
    }

    private static HttpResponseMessage J3qJson(
        HttpRequestMessage request, EconomicV9CustodyAuditReceipt receipt) =>
        new(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StringContent(
                JsonSerializer.Serialize(receipt), Encoding.UTF8,
                "application/json")
        };

    private static EconomicV9OrphanCustodyAuditEntry J3qEntry(
        string action = "Intent") =>
        new("case-" + new string('a', 32), action,
            "j3p-game", ".ga-j3m-quarantine-" + new string('a', 32),
            "case-" + new string('a', 32), new string('B', 64),
            "operator-incident-17",
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    [Fact]
    public void J3qIndependentSignedLedgerAcknowledgesIntentCompletedAndReconciliation()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var operatorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var auditKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            Assert.NotEqual(operatorKey.ExportSubjectPublicKeyInfoPem(),
                auditKey.ExportSubjectPublicKeyInfoPem());
            var challenges = new HashSet<string>(StringComparer.Ordinal);
            var actions = new List<string>();
            using var http = new HttpClient(new J3qLedgerHandler(request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://audit.example.invalid/v1/custody/append",
                    request.RequestUri!.AbsoluteUri);
                var incoming = J3qRead(request);
                Assert.Equal("ga-independent-ledger", incoming.LedgerId);
                Assert.True(challenges.Add(incoming.ChallengeHex));
                Assert.Equal(64, incoming.ChallengeHex.Length);
                actions.Add(incoming.Entry.Event);
                return J3qJson(request, J3qSignReceipt(
                    incoming, auditKey, actions.Count));
            }));
            using var sink = new EconomicV9HttpSignedCustodyAuditSink(
                http, new Uri("https://audit.example.invalid/v1/custody/append"),
                "ga-independent-ledger", auditKey.ExportSubjectPublicKeyInfoPem());
            using var custody = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", operatorKey.ExportSubjectPublicKeyInfoPem(),
                new Isolation(), sink);
            var approved = Signed(
                custody.Plan(caseId, "j3p-game", 7, name), operatorKey);
            Assert.Equal(EconomicV9OrphanCustodyOutcome.Archived,
                custody.ArchiveOnly(approved));
            Assert.Equal(EconomicV9OrphanCustodyOutcome.ReconciledAfterInterruption,
                custody.ArchiveOnly(approved));
            Assert.Equal(new[] { "Intent", "Completed", "Reconciled" }, actions);
            Assert.Equal(3, challenges.Count);
            Assert.True(Directory.Exists(
                Path.Combine(root, ".ga-j3p-vault", caseId)));
            Assert.False(Directory.Exists(Path.Combine(root, name)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Theory]
    [InlineData("ledger")]
    [InlineData("digest")]
    [InlineData("nonce")]
    [InlineData("not-durable")]
    [InlineData("not-locked")]
    [InlineData("stale")]
    [InlineData("short-retention")]
    [InlineData("tampered-signature")]
    [InlineData("wrong-key")]
    [InlineData("http-failure")]
    [InlineData("transport-failure")]
    [InlineData("redirect")]
    public void J3qRefusesNonDurableForgedOrReplayedLedgerAcknowledgments(
        string failure)
    {
        using var trusted = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var http = new HttpClient(new J3qLedgerHandler(request =>
        {
            if (failure == "transport-failure")
                throw new HttpRequestException("Ledger offline");
            if (failure == "http-failure")
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    RequestMessage = request
                };
            var receipt = J3qSignReceipt(J3qRead(request),
                failure == "wrong-key" ? stranger : trusted, 42, failure);
            if (failure == "redirect")
                return J3qJson(new HttpRequestMessage(HttpMethod.Post,
                    "https://other.example.invalid/attacker"), receipt);
            return J3qJson(request, receipt);
        }));
        using var sink = new EconomicV9HttpSignedCustodyAuditSink(
            http, new Uri("https://audit.example.invalid/v1/custody/append"),
            "ga-independent-ledger", trusted.ExportSubjectPublicKeyInfoPem());
        Assert.Throws<InvalidOperationException>(() =>
            sink.Append(J3qEntry()));
    }

    [Fact]
    public void J3qNoSignedRemoteIntentMeansNoForensicMove()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var operatorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var auditKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var http = new HttpClient(new J3qLedgerHandler(request =>
                J3qJson(request, J3qSignReceipt(
                    J3qRead(request), auditKey, 1, "not-durable"))));
            using var sink = new EconomicV9HttpSignedCustodyAuditSink(
                http, new Uri("https://audit.example.invalid/v1/custody/append"),
                "ga-independent-ledger", auditKey.ExportSubjectPublicKeyInfoPem());
            using var custody = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", operatorKey.ExportSubjectPublicKeyInfoPem(),
                new Isolation(), sink);
            var approved = Signed(
                custody.Plan(caseId, "j3p-game", 7, name), operatorKey);
            Assert.Throws<InvalidOperationException>(() =>
                custody.ArchiveOnly(approved));
            Assert.True(Directory.Exists(Path.Combine(root, name)));
            Assert.False(Directory.Exists(
                Path.Combine(root, ".ga-j3p-vault", caseId)));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3qFailedRemoteCompletedLeavesVaultAndNewSignedReconciliation()
    {
        var (root, name, caseId) = Fixture();
        try
        {
            using var operatorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using var auditKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var actions = new List<string>();
            using var http = new HttpClient(new J3qLedgerHandler(request =>
            {
                var incoming = J3qRead(request);
                actions.Add(incoming.Entry.Event);
                if (incoming.Entry.Event == "Completed")
                    return J3qJson(request, J3qSignReceipt(
                        incoming, auditKey, actions.Count, "not-locked"));
                return J3qJson(request, J3qSignReceipt(
                    incoming, auditKey, actions.Count));
            }));
            using var sink = new EconomicV9HttpSignedCustodyAuditSink(
                http, new Uri("https://audit.example.invalid/v1/custody/append"),
                "ga-independent-ledger", auditKey.ExportSubjectPublicKeyInfoPem());
            using var custody = new EconomicV9SupervisedOrphanCustody(
                root, ".ga-j3p-vault", operatorKey.ExportSubjectPublicKeyInfoPem(),
                new Isolation(), sink);
            var approved = Signed(
                custody.Plan(caseId, "j3p-game", 7, name), operatorKey);
            Assert.Throws<InvalidOperationException>(() =>
                custody.ArchiveOnly(approved));
            Assert.True(Directory.Exists(
                Path.Combine(root, ".ga-j3p-vault", caseId)));
            Assert.False(Directory.Exists(Path.Combine(root, name)));
            Assert.Equal(EconomicV9OrphanCustodyOutcome.ReconciledAfterInterruption,
                custody.ArchiveOnly(approved));
            Assert.Equal(new[] { "Intent", "Completed", "Reconciled" }, actions);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void J3qRejectsInsecureEndpointOrWrongPublicKeyAtConstruction()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var http = new HttpClient(new J3qLedgerHandler(_ =>
            throw new HttpRequestException("unused")));
        Assert.Throws<ArgumentException>(() =>
            new EconomicV9HttpSignedCustodyAuditSink(
                http, new Uri("http://audit.example.invalid/append"),
                "ga-ledger", signer.ExportSubjectPublicKeyInfoPem()));
        Assert.Throws<ArgumentException>(() =>
            new EconomicV9HttpSignedCustodyAuditSink(
                http, new Uri("https://audit.example.invalid/append?session=x"),
                "ga-ledger", signer.ExportSubjectPublicKeyInfoPem()));
        Assert.Throws<ArgumentException>(() =>
            new EconomicV9HttpSignedCustodyAuditSink(
                http, new Uri("https://audit.example.invalid/append"),
                "ga-ledger", "not a public key"));
    }
}
