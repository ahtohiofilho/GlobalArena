using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// A settlement is an observable productive population in a physical tile.
/// It is not a founded city, building, legal claim or political institution.
/// </summary>
public sealed record EmergentSettlementSite(
    PhysicalTacticalTileId Tile,
    CivilizationId? Community,
    ulong Workers,
    ulong AgriculturalWorkers,
    ulong ForestryWorkers,
    ulong MiningWorkers,
    ulong FoodDemandRaw,
    ulong FoodStockRaw,
    ulong StoneStockRaw,
    ulong WoodStockRaw,
    ulong HouseholdMicrocredits,
    ulong ProducerMicrocredits)
{
    public ulong TotalLocalMoney => checked(HouseholdMicrocredits + ProducerMicrocredits);
}

/// <summary>
/// Read-only, deterministic overlay of existing workforce allocations and
/// economy. New communities appear only after physical arrival, not when an
/// order is issued. Uninhabited accounts/stocks remain in the canonical game
/// and are deliberately NOT synthesized into cities.
/// </summary>
public sealed class EmergentSettlementProjection
{
    private readonly IReadOnlyDictionary<PhysicalTacticalTileId, EmergentSettlementSite> _byTile;

    public IReadOnlyList<EmergentSettlementSite> Sites { get; }
    public ulong MovementTurn { get; }
    public ulong TotalResidentWorkers { get; }
    public ulong TotalTravellers { get; }
    public ulong UninhabitedAccountMoney { get; }
    public int UninhabitedStockRecords { get; }

    private EmergentSettlementProjection(
        EmergentSettlementSite[] sites, ulong turn, ulong residents,
        ulong travellers, ulong uninhabitedMoney, int uninhabitedStockRecords)
    {
        Sites = Array.AsReadOnly(sites);
        _byTile = sites.ToDictionary(x => x.Tile);
        MovementTurn = turn;
        TotalResidentWorkers = residents;
        TotalTravellers = travellers;
        UninhabitedAccountMoney = uninhabitedMoney;
        UninhabitedStockRecords = uninhabitedStockRecords;
    }

    public EmergentSettlementSite? At(PhysicalTacticalTileId tile) =>
        _byTile.GetValueOrDefault(tile);

    public static EmergentSettlementProjection FromSession(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var state = session.State;
        CivilizationId? civilization = null;
        if (session.HasInitialCivilization)
        {
            var records = session.Game.Snapshot.Civilizations.Records;
            if (records.Count != 1 || !records[0].IsMaterialized)
                throw new InvalidOperationException("Settlement community identity is inconsistent.");
            civilization = records[0].Id;
        }

        var sites = state.Allocations
            .GroupBy(x => x.TileId)
            .OrderBy(g => g.Key.FineStrategicCellId.Value)
            .Select(g =>
            {
                var tile = g.Key;
                var workers = g.Aggregate(0UL,
                    (sum, x) => checked(sum + x.Workers));
                var wallet = state.FoodCash?.At(tile);
                return new EmergentSettlementSite(
                    tile, civilization, workers,
                    g.Where(x => x.Activity == EconomicActivityKind.Agriculture)
                        .Aggregate(0UL, (sum, x) => checked(sum + x.Workers)),
                    g.Where(x => x.Activity == EconomicActivityKind.Forestry)
                        .Aggregate(0UL, (sum, x) => checked(sum + x.Workers)),
                    g.Where(x => x.Activity == EconomicActivityKind.Mining)
                        .Aggregate(0UL, (sum, x) => checked(sum + x.Workers)),
                    session.FoodDemandAt(tile),
                    session.StockAt(tile, new CommodityId(1U)),
                    session.StockAt(tile, new CommodityId(2U)),
                    session.StockAt(tile, new CommodityId(3U)),
                    wallet?.HouseholdMicrocredits ?? 0UL,
                    wallet?.ProducerMicrocredits ?? 0UL);
            }).ToArray();

        var residentCount = sites.Aggregate(0UL,
            (sum, x) => checked(sum + x.Workers));
        if (checked(residentCount + state.WorkersInTransit) != state.TotalWorkers ||
            sites.Any(x => x.Workers == 0UL ||
                checked(x.AgriculturalWorkers + x.ForestryWorkers + x.MiningWorkers) != x.Workers))
            throw new InvalidOperationException("Settlement projection duplicated or lost residents.");

        var inhabited = sites.Select(x => x.Tile).ToHashSet();
        var abandonedMoney = state.FoodCash?.Accounts
            .Where(x => !inhabited.Contains(x.Tile))
            .Aggregate(0UL, (sum, x) => checked(
                checked(sum + x.HouseholdMicrocredits) + x.ProducerMicrocredits)) ?? 0UL;
        var abandonedStocks = state.Stocks.Count(x => !inhabited.Contains(x.Tile));
        var activeMoney = sites.Aggregate(0UL,
            (sum, x) => checked(sum + x.TotalLocalMoney));
        if (state.FoodCash is { } ledger &&
            checked(activeMoney + abandonedMoney) != ledger.InitialMoneySupplyMicrocredits)
            throw new InvalidOperationException("Settlement projection lost private money.");

        return new EmergentSettlementProjection(
            sites, state.MovementTurn, residentCount, state.WorkersInTransit,
            abandonedMoney, abandonedStocks);
    }
}
