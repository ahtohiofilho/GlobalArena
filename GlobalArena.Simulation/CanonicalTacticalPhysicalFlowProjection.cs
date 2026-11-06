using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// UI-safe local physical economy read model. Every quantity is derived from
/// the current canonical session. A global-cycle aggregate is never labelled
/// as a local commodity price, local output or a financial sale.
/// </summary>
public sealed record CanonicalTacticalPhysicalFlowReport(
    ulong FineTile, ulong MovementTurn, int ClosedEconomicCycles,
    ulong ResidentPeople, BigInteger FreeFoodRaw, BigInteger FreeWoodRaw,
    BigInteger FreeStoneRaw, BigInteger CommittedMarketRaw,
    BigInteger DeliberatelyRetainedRaw, BigInteger ExecutedLocalMaterialUseRaw,
    int ActiveMarketOffers, int ActiveRetentions,
    int ExecutedLocalMaterialUses, ulong LastClosedCycleTurn,
    ulong LastGlobalProducedRaw, ulong LastGlobalFoodConsumedRaw,
    ulong LastGlobalFoodUnmetRaw,
    IReadOnlyList<CanonicalPhysicalStockCommitment> ActiveLocalCommitments,
    CanonicalExecutedCommittedMaterialUse? LastExecutedLocalUse)
{
    public bool ProvesPhysicalLocalInventory => true;
    public bool AuthenticatesExecutedLocalMaterialUse => true;
    public bool ProvesCommercialSaleOrMarketRevenue => false;
    public bool UsesLegacyCashBalancesAsPowerN => false;
    public bool ModifiesCanonicalSimulation => false;
    // The existing aggregate-food ledger cannot attest six-variety nutrition.
    public bool ProvesNutritionalEfficiency => false;
    // Default admission is a product policy, NOT an executed sale in V9-V34.
    public bool ProvesAutomaticMarketSale => false;

    /// <summary>
    /// GA-ECON-CONVERGENCE-01: read-only candidate pool for the target
    /// offer-by-default policy. Includes uncommitted physical local stock and
    /// material already escrowed for an explicit market offer; deliberately
    /// retained material and expedition cargo are excluded. This is NOT an
    /// actual market offer, commercial sale, transfer or revenue receipt.
    /// </summary>
    public BigInteger PotentialMarketRaw(uint commodity)
    {
        var free = commodity switch
        {
            1U => FreeFoodRaw,
            2U => FreeStoneRaw,
            3U => FreeWoodRaw,
            _ => throw new ArgumentOutOfRangeException(nameof(commodity),
                "Only physically tracked local commodities can be projected.")
        };
        var offeredCustody = ActiveLocalCommitments
            .Where(x => x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer &&
                x.Commodity == commodity)
            .Aggregate(BigInteger.Zero, (sum, row) => sum + row.RawQuantity);
        return free + offeredCustody;
    }
}

/// <summary>
/// GA-UI-ECON-CYCLE-01: safe read model for the tactical right rail.
/// No invented commerce or stock. The real commitment ledger is independently
/// verified before exposing it to a presentation component.
/// </summary>
public static class CanonicalTacticalPhysicalFlowProjection
{
    public static CanonicalTacticalPhysicalFlowReport Read(
        CanonicalPhysicalTacticalSession session, PhysicalTacticalTileId tile)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!tile.IsValid ||
            tile.FineGoldbergParameters != session.State.FineParameters)
            throw new ArgumentException(
                "Tactical physical flow must refer to a tile in the current session.",
                nameof(tile));

        var beforeHash = session.CurrentHash;
        var beforeEvents = session.Events.Count;
        session.CapturePhysicalStockCommitmentWitness().VerifyIndependent();

        var state = session.State;
        var live = session.PhysicalStockCommitments
            .Where(x => x.FineTile == tile.FineStrategicCellId.Value && x.IsLive)
            .OrderBy(x => x.ActionOrdinal).ToArray();
        var consumed = session.ExecutedCommittedMaterialUses
            .Where(x => x.FineTile == tile.FineStrategicCellId.Value)
            .OrderBy(x => x.ActionOrdinal).ToArray();
        var lastCycle = state.Cycles.LastOrDefault();

        var result = new CanonicalTacticalPhysicalFlowReport(
            tile.FineStrategicCellId.Value,
            state.MovementTurn, state.Cycles.Count, session.WorkersAt(tile),
            new BigInteger(session.StockAt(tile, new CommodityId(1U))),
            new BigInteger(session.StockAt(tile, new CommodityId(3U))),
            new BigInteger(session.StockAt(tile, new CommodityId(2U))),
            live.Where(x => x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer)
                .Aggregate(BigInteger.Zero, (total, x) => total + x.RawQuantity),
            live.Where(x => x.Kind == CanonicalPhysicalCommitmentKind.DeliberateRetention)
                .Aggregate(BigInteger.Zero, (total, x) => total + x.RawQuantity),
            consumed.Aggregate(BigInteger.Zero, (total, x) => total + x.UsedRaw),
            live.Count(x => x.Kind == CanonicalPhysicalCommitmentKind.MarketOffer),
            live.Count(x => x.Kind == CanonicalPhysicalCommitmentKind.DeliberateRetention),
            consumed.Length, lastCycle?.MovementTurn ?? 0UL,
            lastCycle?.ProducedRaw ?? 0UL,
            lastCycle?.FoodConsumedRaw ?? 0UL,
            lastCycle?.FoodUnmetRaw ?? 0UL,
            Array.AsReadOnly(live),
            consumed.LastOrDefault());

        if (result.CommittedMarketRaw < 0 ||
            result.DeliberatelyRetainedRaw < 0 ||
            session.CurrentHash != beforeHash ||
            session.Events.Count != beforeEvents)
            throw new InvalidOperationException(
                "Read-only tactical flow projection altered the canonical game.");
        return result;
    }
}
