using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>New games may opt into physical food settlement. Legacy saves keep the old direct consumption.</summary>
public enum PhysicalFoodMarketMode : byte
{
    LegacyDirectConsumption = 0,
    LocalCashSettlement = 1
}

/// <summary>Two distinct local accounts: household buyers and private producers. No debt or banking.</summary>
public sealed record PhysicalFoodCashAccount(
    PhysicalTacticalTileId Tile, ulong HouseholdMicrocredits, ulong ProducerMicrocredits);

/// <summary>Exactly settled physical food and cash. Zero-price transfers are explicit, not hidden income.</summary>
public sealed record PhysicalFoodCashReceipt(
    ulong MovementTurn, PhysicalTacticalTileId Tile, ulong FoodRaw,
    ulong PriceMicrocreditsPerUnit, ulong PaidMicrocredits);

/// <summary>
/// Immutable monetary subledger of the physical WorldState. Initial money is
/// an explicit once-only endowment; subsequent commands can only redistribute
/// it. No salaries, banks, credit, interest, cash creation, or remote trade.
/// </summary>
public sealed class PhysicalFoodCashMarketState
{
    public const ulong InitialMicrocreditsPerWorker = 40_000_000UL;
    public const ulong MonetaryScale = 1_000_000UL;

    public ulong InitialMoneySupplyMicrocredits { get; }
    public IReadOnlyList<PhysicalFoodCashAccount> Accounts { get; }
    public IReadOnlyList<PhysicalFoodCashReceipt> Receipts { get; }

    public PhysicalFoodCashMarketState(
        ulong initialMoneySupplyMicrocredits,
        IEnumerable<PhysicalFoodCashAccount> accounts,
        IEnumerable<PhysicalFoodCashReceipt> receipts,
        GoldbergParameters fine,
        ulong currentMovementTurn)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(receipts);
        if (initialMoneySupplyMicrocredits == 0UL || !fine.IsValid)
            throw new ArgumentOutOfRangeException(nameof(initialMoneySupplyMicrocredits));
        var rows = accounts.ToArray();
        if (rows.Length == 0 || rows.Any(x => x is null ||
            !x.Tile.IsValid || x.Tile.FineGoldbergParameters != fine))
            throw new ArgumentException("Market accounts must reference physical tiles.");
        rows = rows.OrderBy(x => x.Tile.FineStrategicCellId.Value).ToArray();
        ulong total = 0UL;
        var known = new HashSet<PhysicalTacticalTileId>();
        foreach (var row in rows)
        {
            if (!known.Add(row.Tile))
                throw new ArgumentException("Duplicate food-cash account.");
            total = checked(total + row.HouseholdMicrocredits);
            total = checked(total + row.ProducerMicrocredits);
        }
        if (total != initialMoneySupplyMicrocredits)
            throw new ArgumentException("Physical market money supply was not conserved.");

        var log = receipts.ToArray();
        if (log.Any(x => x is null ||
            x.MovementTurn == 0UL || x.MovementTurn > currentMovementTurn ||
            !x.Tile.IsValid || x.Tile.FineGoldbergParameters != fine ||
            x.FoodRaw == 0UL))
            throw new ArgumentException("Invalid physical cash settlement receipt.");
        if (!log.SequenceEqual(log.OrderBy(x => x.MovementTurn)
                .ThenBy(x => x.Tile.FineStrategicCellId.Value)) ||
            log.Select(x => (x.MovementTurn, x.Tile)).Distinct().Count() != log.Length ||
            log.Any(x => !known.Contains(x.Tile)))
            throw new ArgumentException("Food settlement receipts are not unique and ordered.");
        InitialMoneySupplyMicrocredits = initialMoneySupplyMicrocredits;
        Accounts = Array.AsReadOnly(rows);
        Receipts = Array.AsReadOnly(log);
    }

    public PhysicalFoodCashAccount At(PhysicalTacticalTileId tile) =>
        Accounts.FirstOrDefault(x => x.Tile == tile) ??
        new PhysicalFoodCashAccount(tile, 0UL, 0UL);
}
