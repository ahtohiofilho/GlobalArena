using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A fully journaled, opt-in market-custody batch built in an isolated
/// canonical session. The caller's legacy game is never mutated, and the
/// resulting session is NOT automatically promoted into live gameplay.
/// </summary>
public sealed class CanonicalStagedAutomaticMarketOfferBatch
{
    public CanonicalPhysicalTacticalSession PreparedSession { get; }
    public IReadOnlyList<CanonicalPhysicalStockCommitment> NewOfferCommitments { get; }
    public string OriginalHashHex { get; }
    public string PreparedHashHex => PreparedSession.CurrentHash.HexDigest;
    public BigInteger NewlyOfferedRaw { get; }
    public BigInteger TotalEligibleRaw { get; }
    public BigInteger ExplicitRetentionRaw { get; }

    public bool AuthenticatesMarketOfferCustody => true;
    public bool ExecutesCommercialSales => false;
    public bool TransfersOwnershipOrMoney => false;
    public bool ActivatesNewGameplayRuleset => false;

    private CanonicalStagedAutomaticMarketOfferBatch(
        CanonicalPhysicalTacticalSession prepared,
        IReadOnlyList<CanonicalPhysicalStockCommitment> newOffers,
        string originalHash, BigInteger eligible, BigInteger retention)
    {
        PreparedSession = prepared;
        NewOfferCommitments = Array.AsReadOnly(newOffers.ToArray());
        OriginalHashHex = originalHash;
        NewlyOfferedRaw = newOffers.Aggregate(BigInteger.Zero,
            (total, x) => total + x.RawQuantity);
        TotalEligibleRaw = eligible;
        ExplicitRetentionRaw = retention;
    }

    /// <summary>
    /// GA-ECON-MARKET-DEFAULT-01D: stage every currently free physical
    /// commodity as a real MarketOffer commitment in a REPLAYABLE CLONE.
    /// An already escrowed offer remains locked once; deliberate retention
    /// remains outside the market. All source goods must have a real local
    /// population to authorize custody. No consumer, sale or price invented.
    ///
    /// Isolation makes a failure atomic relative to the source session;
    /// the caller may consider promoting the returned state only through
    /// a separately approved, versioned game-session handoff.
    /// </summary>
    public static CanonicalStagedAutomaticMarketOfferBatch Prepare(
        CanonicalPhysicalTacticalSession source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = source.State;
        if (state.Cycles.Count == 0 ||
            state.MovementTurn != state.Cycles[^1].MovementTurn)
            throw new InvalidOperationException(
                "The automatic market offer batch requires a completed current cycle.");

        var sourceHash = source.CurrentHash.HexDigest;
        var sourceSave = source.ExportSaveJson();
        // Never attempt a sequence of partial writes against the user's game.
        var staged = CanonicalPhysicalTacticalSession.RestoreSaveJson(sourceSave);
        if (staged.CurrentHash.HexDigest != sourceHash ||
            staged.ExportSaveJson() != sourceSave)
            throw new InvalidOperationException(
                "Staged market offer must start with a bit-exact canonical replay.");

        var before = CanonicalDefaultMarketPoolPreflight.FromCurrent(staged);
        var newOffers = new List<CanonicalPhysicalStockCommitment>();
        foreach (var row in before.Rows.Where(x => x.UncommittedLocalRaw > 0))
        {
            var tile = new PhysicalTacticalTileId(
                staged.State.FineParameters, new StrategicCellId(row.FineTile));
            if (staged.WorkersAt(tile) == 0UL)
                throw new InvalidOperationException(
                    "Uninhabited source stock cannot acquire a fictional market seller.");

            if (row.UncommittedLocalRaw > ulong.MaxValue)
                throw new InvalidOperationException(
                    "Stock exceeds the physical commitment ledger's raw domain.");
            var raw = (ulong)row.UncommittedLocalRaw;
            var committed = staged.CommitPhysicalStock(
                tile, new CommodityId(row.Commodity),
                CanonicalPhysicalCommitmentKind.MarketOffer, raw);
            newOffers.Add(committed);
        }

        // Executed custody is not an executed market sale: the entire
        // eligible physical pool is merely segregated for future clearing.
        var after = CanonicalDefaultMarketPoolPreflight.FromCurrent(staged);
        if (after.TotalAccountedRaw != before.TotalAccountedRaw ||
            after.EligibleByDefaultRaw != before.EligibleByDefaultRaw ||
            after.DeliberatelyRetainedRaw != before.DeliberatelyRetainedRaw ||
            after.Rows.Any(x => x.UncommittedLocalRaw != BigInteger.Zero) ||
            newOffers.Aggregate(BigInteger.Zero,
                (total, x) => total + x.RawQuantity) !=
                before.Rows.Aggregate(BigInteger.Zero,
                    (total, x) => total + x.UncommittedLocalRaw))
            throw new InvalidOperationException(
                "Staged automatic offers did not conserve physical custody.");

        staged.CapturePhysicalStockCommitmentWitness().VerifyIndependent();
        var saved = staged.ExportSaveJson();
        var replay = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        if (replay.CurrentHash != staged.CurrentHash ||
            replay.ExportSaveJson() != saved)
            throw new InvalidOperationException(
                "Staged market custody cannot survive deterministic replay.");

        if (source.CurrentHash.HexDigest != sourceHash ||
            source.ExportSaveJson() != sourceSave)
            throw new InvalidOperationException(
                "Preparing market offers changed the source game.");

        return new CanonicalStagedAutomaticMarketOfferBatch(
            staged, newOffers, sourceHash,
            before.EligibleByDefaultRaw, before.DeliberatelyRetainedRaw);
    }
}
