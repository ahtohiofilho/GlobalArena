using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlobalArena.Simulation;

/// <summary>
/// Signed, nonce-bound statement returned by an independently administered
/// fencing control plane. These claims are NOT independently verifiable
/// physical facts unless the trusted signer actually performs fencing.
/// </summary>
public sealed record EconomicV9FencingAttestation(
    string ClusterId, string GameId, long FormerWriterEpoch,
    string ChallengeHex, string IsolationEventId,
    bool FormerPrimaryFenced, bool FormerGameWritersFenced,
    long IssuedAtUnixSeconds, long ExpiresAtUnixSeconds,
    string SignatureBase64);

/// <summary>
/// Deterministic length-delimited signed data. The domain separator prevents
/// a fencing signature from being accepted for a backup receipt (or vice versa).
/// </summary>
public static class EconomicV9OperationalProofCodec
{
    public static byte[] EncodeFencing(EconomicV9FencingAttestation statement) =>
        Encode("GA-V9-FENCING-1", statement.ClusterId, statement.GameId,
            statement.FormerWriterEpoch.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            statement.ChallengeHex, statement.IsolationEventId,
            statement.FormerPrimaryFenced ? "true" : "false",
            statement.FormerGameWritersFenced ? "true" : "false",
            statement.IssuedAtUnixSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            statement.ExpiresAtUnixSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

    public static byte[] EncodeBackup(EconomicV9ImmutableBackupReceipt receipt) =>
        Encode("GA-V9-BACKUP-1", receipt.GameId,
            receipt.WriterEpoch.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            receipt.Anchor.CheckpointSha256Hex,
            receipt.Anchor.JournalSha256Hex,
            receipt.Anchor.Sequence.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            receipt.Anchor.Turn.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            receipt.BackupId, receipt.ObjectVersionId,
            receipt.ArchiveSha256Hex,
            receipt.ArchiveLength.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            receipt.RetentionLocked ? "true" : "false",
            receipt.RetentionUntilUnixSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            receipt.IssuedAtUnixSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

    private static byte[] Encode(string domain, params string[] fields)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(domain);
            foreach (var field in fields)
                writer.Write(field ?? throw new InvalidDataException(
                    "Incomplete signed evidence."));
        }
        return buffer.ToArray();
    }
}

/// <summary>
/// HTTPS adapter for a real, independently authenticated operator API.
/// A fresh 256-bit random challenge binds each signed fencing statement to
/// this specific check. Redirects to a different origin are rejected.
/// Failures and ambiguous acknowledgments always result in false.
/// </summary>
public sealed class EconomicV9HttpSignedFencingVerifier :
    IEconomicV9FormerWriterIsolationVerifier, IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly ECDsa _trustedSigner;
    private readonly string _clusterId;
    private readonly TimeProvider _clock;
    private readonly object _keyGate = new();

    public EconomicV9HttpSignedFencingVerifier(
        HttpClient http,
        Uri evidenceEndpoint,
        string trustedFencingPublicKeyPem,
        string clusterId,
        TimeProvider? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _endpoint = evidenceEndpoint ??
            throw new ArgumentNullException(nameof(evidenceEndpoint));
        if (_endpoint.Scheme != Uri.UriSchemeHttps ||
            !_endpoint.IsAbsoluteUri || _endpoint.UserInfo.Length != 0 ||
            _endpoint.Fragment.Length != 0 || _endpoint.Query.Length != 0)
            throw new ArgumentException(
                "A fixed HTTPS evidence endpoint without credentials or query is required.",
                nameof(evidenceEndpoint));
        if (string.IsNullOrWhiteSpace(clusterId) || clusterId.Length > 128)
            throw new ArgumentException(
                "Trusted cluster identity is required.", nameof(clusterId));
        _clusterId = clusterId;
        _clock = clock ?? TimeProvider.System;
        _trustedSigner = ECDsa.Create();
        try
        {
            _trustedSigner.ImportFromPem(trustedFencingPublicKeyPem);
            if (_trustedSigner.KeySize != 256)
                throw new CryptographicException("Fencing signer must use P-256.");
        }
        catch
        {
            _trustedSigner.Dispose();
            throw;
        }
    }

    public bool IsIsolated(string gameId, long formerWriterEpoch)
    {
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 128 ||
            gameId.Any(c => !char.IsAsciiLetterOrDigit(c) &&
                c is not ('-' or '_' or '.' or ':')) ||
            formerWriterEpoch <= 0)
            return false;

        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var url = new UriBuilder(_endpoint)
        {
            Query = "gameId=" + Uri.EscapeDataString(gameId) +
                "&formerEpoch=" + formerWriterEpoch.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                "&challenge=" + challenge
        }.Uri;
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.CacheControl = new System.Net.Http.Headers
                .CacheControlHeaderValue { NoCache = true, NoStore = true };
            using var response = _http.Send(
                request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
            if (!response.IsSuccessStatusCode ||
                response.RequestMessage?.RequestUri is not Uri finalUri ||
                finalUri.Scheme != Uri.UriSchemeHttps ||
                finalUri.IdnHost != _endpoint.IdnHost ||
                finalUri.Port != _endpoint.Port)
                return false;
            response.Content.LoadIntoBufferAsync(16 * 1024, cancel.Token)
                .GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync(cancel.Token)
                .GetAwaiter().GetResult();
            var evidence = JsonSerializer.Deserialize<EconomicV9FencingAttestation>(body);
            if (evidence is null ||
                evidence.ClusterId != _clusterId ||
                evidence.GameId != gameId ||
                evidence.FormerWriterEpoch != formerWriterEpoch ||
                evidence.ChallengeHex != challenge ||
                evidence.IsolationEventId.Length is < 1 or > 128 ||
                !evidence.FormerPrimaryFenced ||
                !evidence.FormerGameWritersFenced)
                return false;
            var now = _clock.GetUtcNow().ToUnixTimeSeconds();
            if (evidence.IssuedAtUnixSeconds > now ||
                evidence.IssuedAtUnixSeconds < now - 60 ||
                evidence.ExpiresAtUnixSeconds <= now ||
                evidence.ExpiresAtUnixSeconds >
                    evidence.IssuedAtUnixSeconds + 60)
                return false;
            var signature = Convert.FromBase64String(evidence.SignatureBase64);
            if (signature.Length > 128)
                return false;
            lock (_keyGate)
                return _trustedSigner.VerifyData(
                    EconomicV9OperationalProofCodec.EncodeFencing(evidence),
                    signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception ex) when (ex is HttpRequestException or
            OperationCanceledException or InvalidOperationException or
            NotSupportedException or JsonException or FormatException or
            CryptographicException or IOException or ArgumentException)
        {
            return false;
        }
    }

    public void Dispose() => _trustedSigner.Dispose();
}
