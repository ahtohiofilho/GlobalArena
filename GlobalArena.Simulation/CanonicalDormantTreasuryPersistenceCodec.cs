using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Wire schema reserved for VS101. It is intentionally a SEPARATE canonical
/// archive and not yet part of V9-V18 physical session save/replay.
/// Every field is verified before a reserved WorldState can be reconstructed.
/// </summary>
public sealed record CanonicalDormantTreasuryArchivePayload(
    uint SchemaVersion,
    string WorldSignatureSha256Hex,
    ulong BaselineWorldRevision,
    string BaselineWorldHashHex,
    string ReservedWorldHashHex,
    ulong SpendableBalanceMinorUnits,
    int MonetaryReceiptCount);

public sealed record CanonicalDormantTreasuryArchiveEnvelope(
    uint EnvelopeVersion, string PayloadJson, string PayloadSha256Hex);

/// <summary>
/// VS101 read-only, opt-in canonical persistence contract:
/// serialize the zero/empty treasury reservation with its original physical
/// WorldState revision/hash, and restore it *onto that exact baseline*.
/// No live CanonicalPhysicalTacticalSession saves are upgraded here.
/// </summary>
public static class CanonicalDormantTreasuryPersistenceCodec
{
    public const uint FormatVersion = 1U;
    public const int MaximumArchiveUtf8Bytes = 64_000;

    public static WorldState Reserve(WorldState baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        if (baseline.TreasuryReservation is not null ||
            baseline.WorldBinding is null ||
            baseline.PhysicalEconomy is null)
            throw new InvalidOperationException(
                "VS101 reserve requires a physical baseline without an existing treasury.");
        return baseline.WithTreasuryReservation(
            CanonicalTreasuryReservationRuntimeState.CreateDormant(
                baseline.WorldBinding));
    }

    public static string Export(WorldState baseline)
    {
        var reserved = Reserve(baseline);
        var hasher = new CanonicalWorldStateHasher();
        var payload = new CanonicalDormantTreasuryArchivePayload(
            CanonicalTreasuryReservationRuntimeState.SchemaVersion,
            baseline.WorldBinding!.WorldSignatureSha256Hex,
            baseline.Revision,
            hasher.Compute(baseline).HexDigest,
            hasher.Compute(reserved).HexDigest,
            reserved.TreasuryReservation!.SpendableBalanceMinorUnits,
            reserved.TreasuryReservation.MonetaryReceiptCount);
        var payloadJson = JsonSerializer.Serialize(payload);
        var envelope = new CanonicalDormantTreasuryArchiveEnvelope(
            FormatVersion, payloadJson,
            Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(payloadJson))));
        var archive = JsonSerializer.Serialize(envelope);
        if (Encoding.UTF8.GetByteCount(archive) > MaximumArchiveUtf8Bytes)
            throw new InvalidOperationException(
                "VS101 dormant treasury archive exceeded bounded size.");
        return archive;
    }

    public static WorldState Restore(WorldState baseline, string archive)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(archive);
        if (baseline.TreasuryReservation is not null ||
            baseline.WorldBinding is null ||
            baseline.PhysicalEconomy is null)
            throw new InvalidOperationException(
                "VS101 must restore only onto the exact unreserved physical baseline.");
        if (Encoding.UTF8.GetByteCount(archive) > MaximumArchiveUtf8Bytes)
            throw new InvalidOperationException(
                "VS101 treasury archive exceeded bounded size.");

        CanonicalDormantTreasuryArchiveEnvelope envelope;
        CanonicalDormantTreasuryArchivePayload payload;
        try
        {
            envelope = JsonSerializer.Deserialize<CanonicalDormantTreasuryArchiveEnvelope>(
                archive) ?? throw new InvalidOperationException(
                    "Missing treasury archive envelope.");
            if (envelope.EnvelopeVersion != FormatVersion ||
                string.IsNullOrWhiteSpace(envelope.PayloadJson) ||
                !CanonicalTreasuryReservationRuntimeState.IsCanonicalDigest(
                    envelope.PayloadSha256Hex))
                throw new InvalidOperationException(
                    "Missing or invalid VS101 archive checksum or version.");
            var expected = SHA256.HashData(
                Encoding.UTF8.GetBytes(envelope.PayloadJson));
            if (!CryptographicOperations.FixedTimeEquals(expected,
                    Convert.FromHexString(envelope.PayloadSha256Hex)))
                throw new InvalidOperationException(
                    "VS101 archive checksum mismatch.");
            payload = JsonSerializer.Deserialize<CanonicalDormantTreasuryArchivePayload>(
                envelope.PayloadJson) ?? throw new InvalidOperationException(
                    "Missing treasury reservation payload.");

            // Canonical encodings also reject unknown fields, duplicate fields,
            // whitespace variants and other unratified version extensions.
            if (JsonSerializer.Serialize(payload) != envelope.PayloadJson ||
                JsonSerializer.Serialize(envelope) != archive)
                throw new InvalidOperationException(
                    "VS101 requires canonical envelope and payload serialization.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new InvalidOperationException(
                "Invalid VS101 dormant treasury persistence encoding.", ex);
        }

        var hasher = new CanonicalWorldStateHasher();
        var beforeHash = hasher.Compute(baseline);
        if (!CanonicalTreasuryReservationRuntimeState.IsCanonicalWorldSignature(
                payload.WorldSignatureSha256Hex) ||
            !CanonicalTreasuryReservationRuntimeState.IsCanonicalDigest(
                payload.BaselineWorldHashHex) ||
            !CanonicalTreasuryReservationRuntimeState.IsCanonicalDigest(
                payload.ReservedWorldHashHex) ||
            payload.WorldSignatureSha256Hex !=
                baseline.WorldBinding.WorldSignatureSha256Hex ||
            payload.BaselineWorldRevision != baseline.Revision ||
            payload.BaselineWorldHashHex != beforeHash.HexDigest)
            throw new InvalidOperationException(
                "VS101 archive does not belong to this exact world/revision/physical snapshot.");

        var reservation = CanonicalTreasuryReservationRuntimeState.RestoreDormant(
            baseline.WorldBinding, payload.SchemaVersion,
            payload.WorldSignatureSha256Hex,
            payload.SpendableBalanceMinorUnits, payload.MonetaryReceiptCount);
        var reserved = baseline.WithTreasuryReservation(reservation);
        var resulting = hasher.Compute(reserved);
        if (resulting.FormatVersion !=
                CanonicalWorldStateHasher.DormantTreasuryReservationHashFormatVersion ||
            resulting.HexDigest != payload.ReservedWorldHashHex)
            throw new InvalidOperationException(
                "VS101 restored treasury domain hash differs from archived signed state.");
        return reserved;
    }
}
