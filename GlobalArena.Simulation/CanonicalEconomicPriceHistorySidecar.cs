using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Compact versioned OBSERVATION in a separate JSON sidecar, never a
/// canonical WorldState property. Source prices are VS78 HYPOTHESES,
/// not confirmed transactions or fiat balances.
/// </summary>
public sealed record CanonicalEconomicPriceObservationRow(
    byte Activity,
    string PreviousLocallyAbsorbedRaw,
    string HypotheticalPriceNumerator,
    string HypotheticalPriceDenominator,
    int PreviousUtilizationPermille,
    int ResponseDeltaPermille,
    int NextUtilizationPermille,
    int UnchangedRetentionPermille);

public sealed record CanonicalEconomicPriceHistoryPayload(
    int Version,
    ulong SourceMovementTurn,
    ulong TargetMovementTurn,
    int SourceEconomicCycleCount,
    int TargetEconomicCycleCount,
    int SourceActionCount,
    int TargetActionCount,
    ulong SourceWorkerRevision,
    ulong SourceNetworkRevision,
    int MaximumResponseStepPermille,
    string SourceWorldHashHex,
    string TargetWorldHashHex,
    CanonicalEconomicPriceObservationRow[] Signals);

public sealed record CanonicalEconomicPriceHistoryEnvelope(
    int Version, string PayloadJson, string PayloadSha256Hex);

/// <summary>
/// A read-only post-restore inspection. The timeline is authenticated against
/// the canonical replayed event journal. Source prices cannot be independently
/// reconstructed from a canonical save: they are declared hypothetical
/// observations, and MUST NEVER authorize production, trade or SETTLE.
/// </summary>
public sealed class CanonicalEconomicPriceHistoryInspection
{
    public IReadOnlyList<CanonicalEconomicPriceObservationRow> Observations { get; }
    public ulong SourceMovementTurn { get; }
    public ulong TargetMovementTurn { get; }
    public bool HasValidCanonicalChronology => true;
    public bool SidecarChecksumIsDigitalSignature => false;
    public bool ConfirmsPricesWereActuallyPaid => false;
    public bool IndependentlyProvesArchivedPriceQuotation => false;
    public bool CanApplyProductionOrFreight => false;
    public bool CanAuthorizeRealSettlement => false;
    public bool ModifiesCanonicalSave => false;

    internal CanonicalEconomicPriceHistoryInspection(
        CanonicalEconomicPriceHistoryPayload payload)
    {
        SourceMovementTurn = payload.SourceMovementTurn;
        TargetMovementTurn = payload.TargetMovementTurn;
        Observations = Array.AsReadOnly(
            (CanonicalEconomicPriceObservationRow[])payload.Signals.Clone());
    }
}

/// <summary>
/// VS86: explicit sidecar boundary between authenticated HISTORICAL CYCLES
/// and merely recorded HYPOTHETICAL economic price/response evidence.
/// Import is for audit/display only, never for economic execution.
/// </summary>
public static class CanonicalEconomicPriceHistorySidecar
{
    public const int Format = 1;
    public const int MaximumSidecarBytes = 131_072;

    private static string Decimal(BigInteger value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static BigInteger ParseNonNegative(string value, bool positive)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 ||
            !BigInteger.TryParse(value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var parsed) ||
            parsed < 0 || (positive && parsed == 0) ||
            Decimal(parsed) != value)
            throw new InvalidOperationException(
                "Invalid, noncanonical or negative hypothetical economic quantity/index.");
        return parsed;
    }

    private static bool IsHexHash(string? value) =>
        value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');

    private static CanonicalEconomicPriceObservationRow CaptureRow(
        CanonicalLaggedProductionActivitySignal signal) => new(
        checked((byte)signal.Activity),
        Decimal(signal.PreviouslyLocallyAbsorbedRaw),
        Decimal(signal.FrozenWeightedLocalPriceIndex.Numerator),
        Decimal(signal.FrozenWeightedLocalPriceIndex.Denominator),
        signal.PriorUtilizationPermille,
        signal.BoundedUtilizationDeltaPermille,
        signal.NextUtilizationPermille,
        signal.UnchangedRetentionPermille);

    private static void ValidateRows(CanonicalEconomicPriceHistoryPayload payload)
    {
        if (payload.MaximumResponseStepPermille is < 1 or >
            CanonicalLaggedProductionResponse.MaximumResponseStepPermille)
            throw new InvalidOperationException(
                "Invalid frozen economic feedback gain.");
        if (payload.Signals is not { Length: >= 1 and <= 3 })
            throw new InvalidOperationException(
                "An economic observation must have 1..3 actual primitive activity signals.");
        EconomicActivityKind? previous = null;
        foreach (var s in payload.Signals)
        {
            if (s is null)
                throw new InvalidOperationException(
                    "Null observed economic activity.");
            var activity = (EconomicActivityKind)s.Activity;
            if (activity is not (EconomicActivityKind.Agriculture or
                EconomicActivityKind.Forestry or EconomicActivityKind.Mining) ||
                (previous.HasValue && activity <= previous.Value) ||
                s.PreviousUtilizationPermille is < 0 or > 1000 ||
                s.NextUtilizationPermille is < 0 or > 1000 ||
                s.UnchangedRetentionPermille is < 0 or > 1000)
                throw new InvalidOperationException(
                    "Duplicated, unordered or invalid primitive source activity.");
            previous = activity;
            _ = ParseNonNegative(s.PreviousLocallyAbsorbedRaw, positive: true);
            var numerator = ParseNonNegative(
                s.HypotheticalPriceNumerator, positive: false);
            var denominator = ParseNonNegative(
                s.HypotheticalPriceDenominator, positive: true);
            if (numerator > denominator)
                throw new InvalidOperationException(
                    "Hypothetical local relative price must remain between 0 and 1.");
            var p = new CanonicalEndogenousPriceIndex(numerator, denominator);
            var delta = CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                p, payload.MaximumResponseStepPermille);
            if (s.ResponseDeltaPermille != delta ||
                s.NextUtilizationPermille != Math.Clamp(
                    s.PreviousUtilizationPermille + delta, 0, 1000))
                throw new InvalidOperationException(
                    "Archived macro price response is inconsistent with the frozen VS84 policy.");
        }
    }

    private static void ValidateChronology(
        CanonicalPhysicalTacticalSession session,
        CanonicalEconomicPriceHistoryPayload payload)
    {
        if (payload.Version != Format ||
            !IsHexHash(payload.SourceWorldHashHex) ||
            !IsHexHash(payload.TargetWorldHashHex) ||
            payload.SourceMovementTurn >= payload.TargetMovementTurn ||
            payload.SourceActionCount <= 0 ||
            payload.TargetActionCount <= payload.SourceActionCount ||
            payload.SourceEconomicCycleCount <= 0 ||
            payload.TargetEconomicCycleCount != payload.SourceEconomicCycleCount + 1 ||
            payload.TargetMovementTurn != session.State.MovementTurn ||
            payload.TargetWorldHashHex != session.CurrentHash.HexDigest ||
            payload.TargetActionCount != session.Events.Count ||
            payload.SourceWorkerRevision != session.State.WorkerRevision ||
            payload.SourceNetworkRevision != session.TransportNetwork.Revision)
            throw new InvalidOperationException(
                "Price sidecar does not identify the current canonical world and exactly one completed real-cycle transition.");
        var actions = session.Events;
        if (payload.SourceActionCount >= actions.Count ||
            actions[payload.SourceActionCount - 1].Kind != "tick" ||
            actions[payload.SourceActionCount - 1].AfterHashHex !=
                payload.SourceWorldHashHex ||
            actions[^1].Kind != "tick" ||
            actions[^1].AfterHashHex != payload.TargetWorldHashHex ||
            (ulong)actions.Count - (ulong)payload.SourceActionCount !=
                payload.TargetMovementTurn - payload.SourceMovementTurn)
            throw new InvalidOperationException(
                "Price observation is not bound to the canonical source/target tick boundaries.");

        // Count actual ticks through the source boundary, not just local
        // metadata. This rejects any source hash substituted from elsewhere.
        var ticksBefore = actions.Take(payload.SourceActionCount)
            .Count(a => a.Kind == "tick");
        if ((ulong)ticksBefore != payload.SourceMovementTurn ||
            actions.Take(payload.SourceActionCount).Last().Kind != "tick")
            throw new InvalidOperationException(
                "Source movement turn does not match authenticated historical tick count.");
        var previousHash = payload.SourceWorldHashHex;
        foreach (var a in actions.Skip(payload.SourceActionCount))
        {
            if (a.Kind != "tick" || a.Source != 0UL ||
                a.Destination != 0UL || a.FromJob != 0 ||
                a.ToJob != 0 || a.Workers != 0UL ||
                a.BeforeHashHex != previousHash)
                throw new InvalidOperationException(
                    "The observation crosses a non-tick change or a broken chain.");
            previousHash = a.AfterHashHex;
        }
        if (previousHash != payload.TargetWorldHashHex)
            throw new InvalidOperationException(
                "Historical world hash sequence cannot be reconciled.");

        var cycles = session.State.Cycles;
        if (cycles.Count != payload.TargetEconomicCycleCount ||
            cycles[payload.SourceEconomicCycleCount - 1].MovementTurn !=
                payload.SourceMovementTurn ||
            cycles[^1].MovementTurn != payload.TargetMovementTurn)
            throw new InvalidOperationException(
                "Economic-cycle register does not prove exactly the recorded source and target settlements.");
        ValidateRows(payload);
    }

    /// <summary>
    /// Export a recording ONLY from a current VS85 independently proved
    /// result. Returns JSON sidecar; it NEVER modifies canonical save.
    /// </summary>
    public static string Export(
        CanonicalPhysicalTacticalSession session,
        CanonicalCycleStampedProductionProjection completedProof)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(completedProof);
        completedProof.RequireCurrent(session);
        completedProof.VerifyIndependent();
        var previous = completedProof.SourcePriceReceipt;
        var signals = previous.HypotheticalPriorLocalPriceSignals
            .OrderBy(x => x.Activity).Select(CaptureRow).ToArray();
        var payload = new CanonicalEconomicPriceHistoryPayload(
            Format,
            previous.SourceMovementTurn,
            completedProof.NextRealCompletedEconomicCycleTurn,
            previous.SourceEconomicCycleCount,
            session.State.Cycles.Count,
            previous.SourceJournalCount,
            session.Events.Count,
            previous.SourceWorkerRevision,
            previous.SourceNetworkRevision,
            previous.MaximumResponseStepPermille,
            previous.SourceWorldHashHex,
            completedProof.NextRealCompletedEconomicCycleWorldHashHex,
            signals);
        ValidateChronology(session, payload);
        var body = JsonSerializer.Serialize(payload);
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(body)));
        var envelope = JsonSerializer.Serialize(
            new CanonicalEconomicPriceHistoryEnvelope(Format, body, hash));
        if (Encoding.UTF8.GetByteCount(envelope) > MaximumSidecarBytes)
            throw new InvalidOperationException(
                "Economic observation exceeds sidecar inspection size limit.");
        return envelope;
    }

    /// <summary>
    /// An independent, observation-only check against the canonical
    /// restored world. A rehashed forged sidecar might pass structural
    /// checks, so checksum+timeline NEVER authenticates its quotation
    /// values; those are not reconstructible from the canonical save.
    /// </summary>
    public static CanonicalEconomicPriceHistoryInspection Inspect(
        CanonicalPhysicalTacticalSession canonicalSession,
        string sidecarJson)
    {
        ArgumentNullException.ThrowIfNull(canonicalSession);
        ArgumentNullException.ThrowIfNull(sidecarJson);
        if (Encoding.UTF8.GetByteCount(sidecarJson) > MaximumSidecarBytes)
            throw new InvalidOperationException("Observation sidecar too large.");
        CanonicalEconomicPriceHistoryEnvelope envelope;
        CanonicalEconomicPriceHistoryPayload payload;
        try
        {
            envelope = JsonSerializer.Deserialize<CanonicalEconomicPriceHistoryEnvelope>(
                sidecarJson) ?? throw new InvalidOperationException(
                    "No sidecar envelope.");
            if (envelope.Version != Format ||
                string.IsNullOrEmpty(envelope.PayloadJson) ||
                !IsHexHash(envelope.PayloadSha256Hex))
                throw new InvalidOperationException("Invalid sidecar envelope.");
            var actual = SHA256.HashData(
                Encoding.UTF8.GetBytes(envelope.PayloadJson));
            var stated = Convert.FromHexString(envelope.PayloadSha256Hex);
            if (!CryptographicOperations.FixedTimeEquals(actual, stated))
                throw new InvalidOperationException(
                    "Observation sidecar integrity checksum mismatch.");
            payload = JsonSerializer.Deserialize<CanonicalEconomicPriceHistoryPayload>(
                envelope.PayloadJson) ?? throw new InvalidOperationException(
                    "Missing observation payload.");
            if (JsonSerializer.Serialize(payload) != envelope.PayloadJson)
                throw new InvalidOperationException(
                    "Observation contains noncanonical or unsupported fields.");
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            throw new InvalidOperationException(
                "Unreadable or malformed economic observation JSON.", ex);
        }
        ValidateChronology(canonicalSession, payload);
        return new CanonicalEconomicPriceHistoryInspection(payload);
    }
}
