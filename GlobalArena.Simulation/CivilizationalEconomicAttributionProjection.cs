using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Civilization is a shared identity for the starting community. These rows
/// associate existing households, producers and resources with that identity
/// without converting their property to a state treasury.
/// </summary>
public sealed record CivilizationalWorkerAttribution(
    CivilizationId Civilization, PhysicalTacticalTileId Tile,
    EconomicActivityKind Activity, ulong Workers, bool InTransit);

public sealed record CivilizationalLocalAccountAttribution(
    CivilizationId Civilization, PhysicalTacticalTileId Tile,
    ulong HouseholdMicrocredits, ulong ProducerMicrocredits);

public sealed record CivilizationalStockAttribution(
    CivilizationId Civilization, PhysicalTacticalTileId Tile,
    CommodityId Commodity, ulong RawQuantity);

/// <summary>
/// Immutable economic provenance derived from exactly one initial community.
/// No hidden money/stock entries, no shadow economic state, no political owner
/// of households, and no arbitrary division of people over strategic borders.
/// </summary>
public sealed class CivilizationalEconomicAttributionProjection
{
    public CivilizationId Civilization { get; }
    public IReadOnlyList<CivilizationalWorkerAttribution> Workforce { get; }
    public IReadOnlyList<CivilizationalLocalAccountAttribution> Accounts { get; }
    public IReadOnlyList<CivilizationalStockAttribution> Stocks { get; }
    public ulong TotalWorkers { get; }
    public ulong WorkersInTransit { get; }
    public ulong HouseholdMoney { get; }
    public ulong ProducerMoney { get; }
    public ulong TotalMoney => checked(HouseholdMoney + ProducerMoney);

    private CivilizationalEconomicAttributionProjection(
        CivilizationId civilization, CivilizationalWorkerAttribution[] workforce,
        CivilizationalLocalAccountAttribution[] accounts,
        CivilizationalStockAttribution[] stocks,
        ulong totalWorkers, ulong inTransit, ulong household, ulong producer)
    {
        Civilization = civilization;
        Workforce = Array.AsReadOnly(workforce);
        Accounts = Array.AsReadOnly(accounts);
        Stocks = Array.AsReadOnly(stocks);
        TotalWorkers = totalWorkers;
        WorkersInTransit = inTransit;
        HouseholdMoney = household;
        ProducerMoney = producer;
    }

    public static CivilizationalEconomicAttributionProjection FromSession(
        CanonicalPhysicalTacticalSession game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.CivilizationMode != PhysicalCivilizationIdentityMode.BornWithPopulation)
            throw new InvalidOperationException("Legacy worker worlds have no civilizational attribution.");
        var world = game.Game.Snapshot;
        if (world.Civilizations.Records.Count != 1 ||
            !world.Civilizations.Records[0].IsMaterialized ||
            world.Territory.Controls.Count != 0)
            throw new InvalidOperationException("Initial civilization identity diverged from minimal political model.");
        var civilization = world.Civilizations.Records[0].Id;
        var physical = game.State;
        var workforce = physical.Allocations.Select(x =>
                new CivilizationalWorkerAttribution(
                    civilization, x.TileId, x.Activity, x.Workers, false))
            .Concat(physical.Transits.Select(x =>
                new CivilizationalWorkerAttribution(
                    civilization, x.Source, x.FromJob, x.Workers, true)))
            .OrderBy(x => x.Tile.FineStrategicCellId.Value)
            .ThenBy(x => (byte)x.Activity).ThenBy(x => x.InTransit).ToArray();
        var accountLedger = physical.FoodCash
            ?? throw new InvalidOperationException("Civilizational provenance requires canonical cash market.");
        var accounts = accountLedger.Accounts.Select(x =>
            new CivilizationalLocalAccountAttribution(
                civilization, x.Tile, x.HouseholdMicrocredits,
                x.ProducerMicrocredits)).ToArray();
        var stocks = physical.Stocks.Select(x =>
            new CivilizationalStockAttribution(
                civilization, x.TileId, x.Commodity, x.RawQuantity)).ToArray();
        var population = workforce.Aggregate(0UL,
            (sum, x) => checked(sum + x.Workers));
        var travelling = workforce.Where(x => x.InTransit)
            .Aggregate(0UL, (sum, x) => checked(sum + x.Workers));
        var household = accounts.Aggregate(0UL,
            (sum, x) => checked(sum + x.HouseholdMicrocredits));
        var producer = accounts.Aggregate(0UL,
            (sum, x) => checked(sum + x.ProducerMicrocredits));
        if (population != physical.TotalWorkers ||
            travelling != physical.WorkersInTransit ||
            checked(household + producer) !=
                accountLedger.InitialMoneySupplyMicrocredits)
            throw new InvalidOperationException("Civilizational attribution failed conservation.");
        return new CivilizationalEconomicAttributionProjection(
            civilization, workforce, accounts, stocks, population, travelling,
            household, producer);
    }
}
