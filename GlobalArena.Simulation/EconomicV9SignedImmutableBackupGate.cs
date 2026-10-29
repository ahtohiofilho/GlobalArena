using System.Security.Cryptography;
using System.Text.Json;

namespace GlobalArena.Simulation;

/// <summary>
/// Operator-signed evidence about a versioned backup object. The independent
/// issuer must check actual provider-side Object Lock / retention configuration
/// before signing; a signature alone cannot make an object immutable.
/// </summary>
public sealed record EconomicV9ImmutableBackupReceipt(
    string GameId, long WriterEpoch, EconomicV9ExternalAnchor Anchor,
    string BackupId, string ObjectVersionId, string ArchiveSha256Hex,
    long ArchiveLength, bool RetentionLocked,
    long RetentionUntilUnixSeconds, long IssuedAtUnixSeconds,
    string SignatureBase64);

public interface IEconomicV9ImmutableBackupRecoveryGate
{
    bool VerifyBackup(string gameId, long writerEpoch,
        EconomicV9ExternalAnchor independentlyAcknowledged);
}

/// <summary>
/// HTTPS client to an independently operated backup attestation service.
/// Checks pinned ECDSA P-256 signature, game/epoch/anchor, provider-signed
/// retention assertion, and the actual bytes of a specific archive version.
/// The archive is read and hashed only; never extracted or trusted as a
/// replacement for full local GAFULL09 journal replay.
/// </summary>
public sealed class EconomicV9SignedImmutableBackupGate :
    IEconomicV9ImmutableBackupRecoveryGate, IDisposable
{
    private const long MaximumArchiveBytes = 256L * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly Uri _manifestEndpoint;
    private readonly Uri _archiveEndpoint;
    private readonly ECDsa _trustedBackupSigner;
    private readonly TimeProvider _clock;
    private readonly object _keyGate = new();

    public EconomicV9SignedImmutableBackupGate(
        HttpClient http, Uri manifestEndpoint, Uri archiveEndpoint,
        string independentlyTrustedBackupPublicKeyPem,
        TimeProvider? clock = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _manifestEndpoint = ValidateEndpoint(manifestEndpoint);
        _archiveEndpoint = ValidateEndpoint(archiveEndpoint);
        if (_manifestEndpoint.IdnHost != _archiveEndpoint.IdnHost ||
            _manifestEndpoint.Port != _archiveEndpoint.Port)
            throw new ArgumentException(
                "Manifest and versioned archive must share the trusted origin.");
        _clock = clock ?? TimeProvider.System;
        _trustedBackupSigner = ECDsa.Create();
        try
        {
            _trustedBackupSigner.ImportFromPem(
                independentlyTrustedBackupPublicKeyPem);
            if (_trustedBackupSigner.KeySize != 256)
                throw new CryptographicException("Backup signer must use P-256.");
        }
        catch
        {
            _trustedBackupSigner.Dispose();
            throw;
        }
    }

    public bool VerifyBackup(string gameId, long writerEpoch,
        EconomicV9ExternalAnchor independentlyAcknowledged)
    {
        if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > 128 ||
            gameId.Any(c => !char.IsAsciiLetterOrDigit(c) &&
                c is not ('-' or '_' or '.' or ':')) ||
            writerEpoch <= 0 || independentlyAcknowledged is null)
            return false;

        try
        {
            using var cancel = new CancellationTokenSource(
                TimeSpan.FromSeconds(15));
            var manifestUri = WithQuery(_manifestEndpoint,
                "gameId=" + Uri.EscapeDataString(gameId) +
                "&epoch=" + writerEpoch.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) +
                "&checkpoint=" + independentlyAcknowledged.CheckpointSha256Hex);
            using var manifestResponse = Get(manifestUri, cancel.Token);
            if (!TrustedResponse(manifestResponse, manifestUri))
                return false;
            manifestResponse.Content.LoadIntoBufferAsync(
                16 * 1024, cancel.Token).GetAwaiter().GetResult();
            var json = manifestResponse.Content.ReadAsStringAsync(cancel.Token)
                .GetAwaiter().GetResult();
            var receipt = JsonSerializer.Deserialize<EconomicV9ImmutableBackupReceipt>(
                json);
            if (receipt is null || receipt.GameId != gameId ||
                receipt.WriterEpoch != writerEpoch ||
                receipt.Anchor != independentlyAcknowledged ||
                !receipt.RetentionLocked ||
                string.IsNullOrWhiteSpace(receipt.BackupId) ||
                receipt.BackupId.Length > 128 ||
                string.IsNullOrWhiteSpace(receipt.ObjectVersionId) ||
                receipt.ObjectVersionId.Length > 256 ||
                receipt.ArchiveLength is <= 0 or > MaximumArchiveBytes ||
                !IsUpperSha256(receipt.ArchiveSha256Hex))
                return false;
            var now = _clock.GetUtcNow().ToUnixTimeSeconds();
            if (receipt.IssuedAtUnixSeconds > now ||
                receipt.IssuedAtUnixSeconds < now - 86400 ||
                receipt.RetentionUntilUnixSeconds <= now + 86400)
                return false;
            var signature = Convert.FromBase64String(receipt.SignatureBase64);
            if (signature.Length > 128)
                return false;
            bool authentic;
            lock (_keyGate)
                authentic = _trustedBackupSigner.VerifyData(
                    EconomicV9OperationalProofCodec.EncodeBackup(receipt),
                    signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
            if (!authentic)
                return false;

            var archiveUri = WithQuery(_archiveEndpoint,
                "gameId=" + Uri.EscapeDataString(gameId) +
                "&backupId=" + Uri.EscapeDataString(receipt.BackupId) +
                "&version=" + Uri.EscapeDataString(receipt.ObjectVersionId));
            using var archiveResponse = Get(archiveUri, cancel.Token);
            if (!TrustedResponse(archiveResponse, archiveUri) ||
                archiveResponse.Content.Headers.ContentLength is long length &&
                    length != receipt.ArchiveLength)
                return false;
            using var stream = archiveResponse.Content
                .ReadAsStreamAsync(cancel.Token).GetAwaiter().GetResult();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536];
            long total = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                total = checked(total + read);
                if (total > MaximumArchiveBytes ||
                    total > receipt.ArchiveLength)
                    return false;
                hash.AppendData(buffer, 0, read);
            }
            return total == receipt.ArchiveLength &&
                Convert.ToHexString(hash.GetHashAndReset()) ==
                    receipt.ArchiveSha256Hex;
        }
        catch (Exception ex) when (ex is HttpRequestException or
            OperationCanceledException or InvalidOperationException or
            NotSupportedException or JsonException or FormatException or
            CryptographicException or IOException or ArgumentException or
            OverflowException)
        {
            return false;
        }
    }

    private HttpResponseMessage Get(Uri uri, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.CacheControl = new System.Net.Http.Headers
            .CacheControlHeaderValue { NoCache = true, NoStore = true };
        return _http.Send(request,
            HttpCompletionOption.ResponseHeadersRead, cancellation);
    }

    private bool TrustedResponse(HttpResponseMessage response, Uri requested) =>
        response.IsSuccessStatusCode &&
        response.RequestMessage?.RequestUri is Uri final &&
        final.Scheme == Uri.UriSchemeHttps &&
        final.IdnHost == requested.IdnHost &&
        final.Port == requested.Port;

    private static Uri WithQuery(Uri endpoint, string query) =>
        new UriBuilder(endpoint) { Query = query }.Uri;

    private static Uri ValidateEndpoint(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 ||
            uri.Fragment.Length != 0)
            throw new ArgumentException("Expected a fixed HTTPS backup endpoint.");
        return uri;
    }

    private static bool IsUpperSha256(string value) =>
        value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    public void Dispose() => _trustedBackupSigner.Dispose();
}
