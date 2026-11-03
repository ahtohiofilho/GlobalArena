using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// VS104: a deterministic, read-only preview of an EXPLICIT future V20 save
/// migration. The live session and original archive are never changed.
/// No real treasury balance, tax, transfer or settlement is authorized.
/// </summary>
public sealed record CanonicalDormantTreasuryMigrationPreview(
    int SourceEnvelopeVersion,
    int PhysicalPayloadVersion,
    int CandidateEnvelopeVersion,
    int HistoricalActionCount,
    int ActivationAfterActionCount,
    ulong SourceWorldRevision,
    ulong CandidateWorldRevision,
    string WorldSignatureSha256Hex,
    string SourceWorldHashHex,
    string CandidateWorldHashHex,
    string SourceArchiveSha256Hex,
    string CandidateArchiveSha256Hex,
    bool AlreadyReserved)
{
    public bool RequiresExplicitPlayerRegistration => !AlreadyReserved;
    public bool WritesAnySave => false;
    public bool PostsRealStateRevenue => false;
    public bool DebitsPopulationMoney => false;
}

/// <summary>
/// The input is a previously serialized V9-V18 or V20 canonical physical save.
/// Only a temporary, independently replayed session gets an empty reservation.
/// The resulting V20 bytes are validated but never written or returned.
/// </summary>
public static class CanonicalDormantTreasuryMigrationPreflight
{
    public static CanonicalDormantTreasuryMigrationPreview Inspect(string archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        // Validate the complete historical world, action journal, checksum,
        // version and transport commands before calculating any evidence.
        var source = CanonicalPhysicalTacticalSession.RestoreSaveJson(archive);
        var canonicalSource = source.ExportSaveJson();
        var sourceEnvelope = ParseEnvelope(canonicalSource);
        var sourcePayload = ParsePayload(sourceEnvelope);
        var originalHash = source.CurrentHash;
        var historicalActions = source.Events.ToArray();
        var alreadyReserved = source.Game.Snapshot.TreasuryReservation is not null;
        if (alreadyReserved !=
            (sourceEnvelope.Version ==
                CanonicalPhysicalTacticalSession.IntegratedDormantTreasuryEnvelopeFormat))
            throw new InvalidOperationException(
                "VS104 found an inconsistent treasury save envelope.");

        // Important: this is a NEW replayed session, never the caller's
        // WorldState or the original source session.
        var sandbox = CanonicalPhysicalTacticalSession.RestoreSaveJson(canonicalSource);
        if (!alreadyReserved)
            sandbox.EnableDormantTreasuryReservation();

        var candidate = sandbox.ExportSaveJson();
        var candidateEnvelope = ParseEnvelope(candidate);
        var candidatePayload = ParsePayload(candidateEnvelope);
        if (candidateEnvelope.Version !=
                CanonicalPhysicalTacticalSession.IntegratedDormantTreasuryEnvelopeFormat ||
            candidatePayload.TreasuryReservation is not { } reservation ||
            reservation.SchemaVersion !=
                CanonicalTreasuryReservationRuntimeState.SchemaVersion ||
            reservation.WorldSignatureSha256Hex != sourcePayload.WorldBindingHex ||
            reservation.SpendableBalanceMinorUnits != 0UL ||
            reservation.MonetaryReceiptCount != 0 ||
            reservation.ActivatedAfterActionCount !=
                (alreadyReserved
                    ? sourcePayload.TreasuryReservation!.ActivatedAfterActionCount
                    : historicalActions.Length) ||
            candidatePayload.Revision !=
                checked(sourcePayload.Revision + (alreadyReserved ? 0UL : 1UL)) ||
            candidatePayload.Actions.Length != historicalActions.Length ||
            !candidatePayload.Actions.SequenceEqual(historicalActions))
            throw new InvalidOperationException(
                "VS104 candidate changed historical actions or dormant-only treasury invariants.");

        // Compare every physical field, including legacy market account
        // arrays and transport commands, after removing ONLY the expected
        // administrative revision/hash/treasury overlay.
        var normalized = candidatePayload with
        {
            Revision = sourcePayload.Revision,
            FinalHashHex = sourcePayload.FinalHashHex,
            TreasuryReservation = sourcePayload.TreasuryReservation
        };
        if (JsonSerializer.Serialize(normalized) !=
            JsonSerializer.Serialize(sourcePayload))
            throw new InvalidOperationException(
                "VS104 preview modified canonical physical history.");

        // Validate the complete V20 replay independently, including the
        // activation boundary and the hash domain switch.
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(candidate);
        if (reopened.ExportSaveJson() != candidate ||
            reopened.CurrentHash != sandbox.CurrentHash ||
            !reopened.Events.SequenceEqual(historicalActions) ||
            source.ExportSaveJson() != canonicalSource ||
            source.CurrentHash != originalHash ||
            (alreadyReserved && candidate != canonicalSource) ||
            (!alreadyReserved && sandbox.CurrentHash == originalHash))
            throw new InvalidOperationException(
                "VS104 save migration preflight is not a deterministic read-only replay.");

        return new CanonicalDormantTreasuryMigrationPreview(
            sourceEnvelope.Version,
            sourcePayload.Version,
            candidateEnvelope.Version,
            historicalActions.Length,
            reservation.ActivatedAfterActionCount,
            sourcePayload.Revision,
            candidatePayload.Revision,
            sourcePayload.WorldBindingHex,
            originalHash.HexDigest,
            sandbox.CurrentHash.HexDigest,
            Digest(archive),
            Digest(candidate),
            alreadyReserved);
    }

    private static CanonicalPhysicalSaveEnvelope ParseEnvelope(string archive) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(archive)
            ?? throw new InvalidOperationException("VS104 missing save envelope.");

    private static CanonicalPhysicalSavePayload ParsePayload(
        CanonicalPhysicalSaveEnvelope envelope) =>
        JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(envelope.PayloadJson)
            ?? throw new InvalidOperationException("VS104 missing save payload.");

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
