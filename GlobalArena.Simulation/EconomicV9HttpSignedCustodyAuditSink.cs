using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlobalArena.Simulation;

/// <summary>
/// Request to an INDEPENDENT audit service. A random challenge authenticates
/// this exact append acknowledgment, not just a previously stored event.
/// </summary>
public sealed record EconomicV9CustodyAuditAppendRequest(
    string LedgerId, EconomicV9OrphanCustodyAuditEntry Entry,
    string ChallengeHex);

/// <summary>
/// Signed acknowledgment from the separately administered audit controller.
/// The controller MUST commit the exact event to its durable append-only
/// ledger and verify storage retention BEFORE signing. This client can verify
/// signatures/claims, not the controller's actual storage policy.
/// </summary>
public sealed record EconomicV9CustodyAuditReceipt(
    string LedgerId, string EntrySha256Hex, string ChallengeHex,
    string AppendId, long Sequence, bool DurablyCommitted,
    bool RetentionLocked, long IssuedAtUnixSeconds,
    long RetentionUntilUnixSeconds, string SignatureBase64);

/// <summary>
/// Canonical, length-delimited, domain-separated signature protocol.
/// All binary fields use .NET BinaryWriter little-endian encoding;
/// strings use UTF-8 with 7-bit encoded length prefixes.
/// </summary>
public static class EconomicV9CustodyAuditProofCodec
{
    public static byte[] EncodeEvent(EconomicV9OrphanCustodyAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("GA-V9-CUSTODY-AUDIT-EVENT-1");
            writer.Write(entry.CaseId);
            writer.Write(entry.Event);
            writer.Write(entry.GameId);
            writer.Write(entry.QuarantineName);
            writer.Write(entry.VaultName);
            writer.Write(entry.TreeSha256Hex);
            writer.Write(entry.OperatorId);
            writer.Write(entry.RecordedAtUnixSeconds);
        }
        return bytes.ToArray();
    }

    public static byte[] EncodeReceipt(EconomicV9CustodyAuditReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        using var bytes = new MemoryStream();
        using (var writer = new BinaryWriter(bytes, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("GA-V9-CUSTODY-AUDIT-RECEIPT-1");
            writer.Write(receipt.LedgerId);
            writer.Write(receipt.EntrySha256Hex);
            writer.Write(receipt.ChallengeHex);
            writer.Write(receipt.AppendId);
            writer.Write(receipt.Sequence);
            writer.Write(receipt.DurablyCommitted);
            writer.Write(receipt.RetentionLocked);
            writer.Write(receipt.IssuedAtUnixSeconds);
            writer.Write(receipt.RetentionUntilUnixSeconds);
        }
        return bytes.ToArray();
    }
}

/// <summary>
/// Adapts the J3p synchronous fail-closed audit sink to an independently
/// authenticated HTTPS ledger. An event is accepted ONLY after verifying
/// a fresh nonce-bound DER ECDSA P-256 receipt, a pinned ledger identity,
/// the exact event digest, durable-commit and retention assertions.
/// The private signing key must NEVER be available to the game/DB host.
/// </summary>
public sealed class EconomicV9HttpSignedCustodyAuditSink :
    IEconomicV9OrphanCustodyAuditSink, IDisposable
{
    private readonly HttpClient _http;
    private readonly Uri _appendEndpoint;
    private readonly string _ledgerId;
    private readonly ECDsa _trustedLedgerSigner;
    private readonly TimeProvider _clock;
    private readonly object _keyLock = new();

    public EconomicV9HttpSignedCustodyAuditSink(
        HttpClient http, Uri appendEndpoint, string ledgerId,
        string independentlyPinnedLedgerPublicKeyPem,
        TimeProvider? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentNullException.ThrowIfNull(appendEndpoint);
        if (!appendEndpoint.IsAbsoluteUri ||
            appendEndpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(appendEndpoint.UserInfo) ||
            !string.IsNullOrEmpty(appendEndpoint.Query) ||
            !string.IsNullOrEmpty(appendEndpoint.Fragment))
            throw new ArgumentException(
                "A fixed HTTPS ledger append endpoint is required.",
                nameof(appendEndpoint));
        if (string.IsNullOrWhiteSpace(ledgerId) || ledgerId.Length > 128 ||
            !ledgerId.All(c => char.IsAsciiLetterOrDigit(c) ||
                c is '-' or '_' or '.' or ':'))
            throw new ArgumentException("Trusted ledger identity required.",
                nameof(ledgerId));

        _appendEndpoint = appendEndpoint;
        _ledgerId = ledgerId;
        _clock = clock ?? TimeProvider.System;
        _trustedLedgerSigner = ECDsa.Create();
        try
        {
            _trustedLedgerSigner.ImportFromPem(
                independentlyPinnedLedgerPublicKeyPem);
            if (_trustedLedgerSigner.KeySize != 256)
                throw new CryptographicException(
                    "Independent audit signer must use ECDSA P-256.");
        }
        catch
        {
            _trustedLedgerSigner.Dispose();
            throw;
        }
    }

    public void Append(EconomicV9OrphanCustodyAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.CaseId) ||
            string.IsNullOrWhiteSpace(entry.GameId) ||
            string.IsNullOrWhiteSpace(entry.TreeSha256Hex) ||
            entry.Event is not ("Intent" or "Completed" or "Reconciled"))
            throw new InvalidDataException("Incomplete custody audit event.");

        // An ambiguous timeout is NOT treated as a successful append.
        // The remote ledger must support durable idempotency by exact event
        // identity; a retry is allowed only with a NEW challenge and receipt.
        var challenge = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var digest = Convert.ToHexString(SHA256.HashData(
            EconomicV9CustodyAuditProofCodec.EncodeEvent(entry)));
        var payload = new EconomicV9CustodyAuditAppendRequest(
            _ledgerId, entry, challenge);

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            using var request = new HttpRequestMessage(
                HttpMethod.Post, _appendEndpoint);
            request.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8,
                "application/json");
            using var response = _http.Send(
                request, HttpCompletionOption.ResponseHeadersRead,
                timeout.Token);
            // Do not accept a redirect to a different path, even on
            // the same origin, or an HTTP downgrade.
            if (!response.IsSuccessStatusCode ||
                response.RequestMessage?.RequestUri != _appendEndpoint)
                throw new InvalidOperationException(
                    "Audit ledger did not acknowledge the exact HTTPS endpoint.");
            response.Content.LoadIntoBufferAsync(
                16 * 1024, timeout.Token).GetAwaiter().GetResult();
            var json = response.Content.ReadAsStringAsync(timeout.Token)
                .GetAwaiter().GetResult();
            var receipt = JsonSerializer.Deserialize<
                EconomicV9CustodyAuditReceipt>(json);
            if (!VerifyReceipt(receipt, challenge, digest))
                throw new InvalidOperationException(
                    "Independent ledger signature or durable receipt rejected.");
        }
        catch (Exception ex) when (ex is HttpRequestException or
            OperationCanceledException or JsonException or FormatException or
            CryptographicException or IOException or ArgumentException or
            NotSupportedException)
        {
            throw new InvalidOperationException(
                "Independent custody audit is unavailable or invalid; deny operation.",
                ex);
        }
    }

    private bool VerifyReceipt(EconomicV9CustodyAuditReceipt? receipt,
        string challenge, string digest)
    {
        if (receipt is null ||
            receipt.LedgerId != _ledgerId ||
            receipt.EntrySha256Hex != digest ||
            receipt.ChallengeHex != challenge ||
            receipt.AppendId is not { Length: >= 1 and <= 128 } ||
            receipt.Sequence < 1 ||
            !receipt.DurablyCommitted || !receipt.RetentionLocked)
            return false;

        var now = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (receipt.IssuedAtUnixSeconds < now - 60 ||
            receipt.IssuedAtUnixSeconds > now + 5 ||
            receipt.RetentionUntilUnixSeconds <= now + 86400)
            return false;
        byte[] signature;
        try { signature = Convert.FromBase64String(receipt.SignatureBase64); }
        catch (FormatException) { return false; }
        if (signature.Length is < 8 or > 128)
            return false;
        lock (_keyLock)
            return _trustedLedgerSigner.VerifyData(
                EconomicV9CustodyAuditProofCodec.EncodeReceipt(receipt),
                signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
    }

    public void Dispose() => _trustedLedgerSigner.Dispose();
}
