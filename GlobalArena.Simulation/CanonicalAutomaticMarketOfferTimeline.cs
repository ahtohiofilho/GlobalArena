using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit ruleset opt-in envelope. The inner canonical save retains its
/// historic format, while this wrapper persists the opt-in identity and
/// refuses to interpret a legacy save as a new-ruleset checkpoint.
/// </summary>
public sealed record CanonicalAutomaticMarketOfferCheckpoint(
    int RulesetVersion,
    string RulesetId,
    string CanonicalSaveJson,
    string CanonicalWorldHashHex,
    string CanonicalSaveSha256Hex);

public sealed record CanonicalAutomaticMarketOfferTurnResult(
    ulong MovementTurn, bool ClosedEconomicCycle, int ClosedCycleCount,
    int NewlyCreatedOffers, BigInteger NewlyOfferedRaw,
    string BeforeWorldHashHex, string AfterWorldHashHex)
{
    public bool ProvesAutomaticMarketCustody => ClosedEconomicCycle;
    public bool ProvesExecutedCommercialSales => false;
    public bool AuthorizesMonetaryRevenue => false;
}

/// <summary>
/// GA-ECON-MARKET-DEFAULT-01E: opt-in laboratory timeline that atomically
/// switches to a fully replayed session only after the movement tick AND any
/// just-closed cycle's automatic market-custody batch both succeed.
///
/// A failed tick or batch never alters the current session. Historic saves
/// are never silently promoted. Does not perform sale/clearing/food nutrition.
/// </summary>
public sealed class CanonicalAutomaticMarketOfferTimeline
{
    public const int CheckpointVersion = 1;
    public const string RulesetIdentity = "GA-ECON-MARKET-DEFAULT-01E-CUSTODY-V1";

    private CanonicalPhysicalTacticalSession _current;

    public ulong MovementTurn => _current.State.MovementTurn;
    public int ClosedEconomicCycles => _current.State.Cycles.Count;
    public string WorldHashHex => _current.CurrentHash.HexDigest;
    public bool IsExplicitOptIn => true;
    public bool ExecutesAutomaticSales => false;
    public bool ReplacesLegacyFoodConsumption => false;

    private CanonicalAutomaticMarketOfferTimeline(
        CanonicalPhysicalTacticalSession session) => _current = session;

    private static CanonicalPhysicalTacticalSession CloneValidated(
        CanonicalPhysicalTacticalSession original)
    {
        var json = original.ExportSaveJson();
        var copy = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        if (copy.ExportSaveJson() != json ||
            copy.CurrentHash != original.CurrentHash)
            throw new InvalidOperationException(
                "The canonical state cannot be cloned bit-exactly.");
        return copy;
    }

    /// <summary>
    /// Do not turn an existing populated historic save into a new policy
    /// without an explicit migration milestone. Initial empty games are
    /// intentionally opted into this LAB-only custody schedule.
    /// </summary>
    public static CanonicalAutomaticMarketOfferTimeline StartOptIn(
        CanonicalPhysicalTacticalSession initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        if (initial.State.MovementTurn != 0UL ||
            initial.State.Cycles.Count != 0 ||
            initial.Events.Count != 0 ||
            initial.PhysicalStockCommitments.Count != 0)
            throw new InvalidOperationException(
                "Automatic offer mode requires explicit opt-in before the first turn.");
        return new(CloneValidated(initial));
    }

    /// <summary>
    /// Return a detached snapshot. Mutating the snapshot never changes the
    /// authoritative timeline owned by this opt-in laboratory wrapper.
    /// </summary>
    public CanonicalPhysicalTacticalSession CaptureCanonicalSnapshot() =>
        CloneValidated(_current);

    public CanonicalAutomaticMarketOfferTurnResult AdvanceMovementTurn()
    {
        var beforeHash = WorldHashHex;
        var candidate = CloneValidated(_current);
        var previousCycleCount = candidate.State.Cycles.Count;
        var closed = candidate.AdvanceMovementTurn();

        if (candidate.State.Cycles.Count != previousCycleCount +
            (closed is null ? 0 : 1))
            throw new InvalidOperationException(
                "Economic cycle closure is inconsistent with canonical turn evidence.");

        var newlyOfferedRaw = BigInteger.Zero;
        var newOffers = 0;
        if (closed is not null)
        {
            if (candidate.State.MovementTurn != closed.MovementTurn)
                throw new InvalidOperationException(
                    "Offer custody must follow the actual closed economic cycle.");
            var staged = CanonicalStagedAutomaticMarketOfferBatch.Prepare(candidate);
            candidate = staged.PreparedSession;
            newlyOfferedRaw = staged.NewlyOfferedRaw;
            newOffers = staged.NewOfferCommitments.Count;
        }

        // Replay must certify the exact final world before any live swap.
        candidate = CloneValidated(candidate);
        var result = new CanonicalAutomaticMarketOfferTurnResult(
            candidate.State.MovementTurn, closed is not null,
            candidate.State.Cycles.Count, newOffers, newlyOfferedRaw,
            beforeHash, candidate.CurrentHash.HexDigest);
        _current = candidate;
        return result;
    }

    private static string HashSave(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));

    public string ExportCheckpointJson()
    {
        var save = _current.ExportSaveJson();
        var envelope = new CanonicalAutomaticMarketOfferCheckpoint(
            CheckpointVersion, RulesetIdentity, save,
            _current.CurrentHash.HexDigest, HashSave(save));
        return JsonSerializer.Serialize(envelope);
    }

    public static CanonicalAutomaticMarketOfferTimeline RestoreCheckpointJson(
        string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Checkpoint cannot be empty.", nameof(json));
        CanonicalAutomaticMarketOfferCheckpoint? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<CanonicalAutomaticMarketOfferCheckpoint>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Automatic market checkpoint is malformed.", ex);
        }
        if (envelope is null ||
            envelope.RulesetVersion != CheckpointVersion ||
            envelope.RulesetId != RulesetIdentity ||
            string.IsNullOrWhiteSpace(envelope.CanonicalSaveJson) ||
            string.IsNullOrWhiteSpace(envelope.CanonicalWorldHashHex) ||
            string.IsNullOrWhiteSpace(envelope.CanonicalSaveSha256Hex) ||
            !string.Equals(HashSave(envelope.CanonicalSaveJson),
                envelope.CanonicalSaveSha256Hex, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Checkpoint lacks valid explicit automatic market ruleset identity.");

        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            envelope.CanonicalSaveJson);
        if (restored.ExportSaveJson() != envelope.CanonicalSaveJson ||
            restored.CurrentHash.HexDigest != envelope.CanonicalWorldHashHex)
            throw new InvalidOperationException(
                "Checkpoint's canonical save or world hash does not match.");

        // The wrapper is not derivable from legacy save bytes alone; only the
        // authenticated, explicitly versioned envelope restores opt-in.
        return new(restored);
    }
}
