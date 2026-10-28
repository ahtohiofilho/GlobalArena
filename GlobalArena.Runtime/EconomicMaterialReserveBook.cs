namespace GlobalArena.Runtime;

/// <summary>
/// Positive physical commodity balance located at an EconomicPoint. These
/// are intentionally retained materials, not every production flow and not
/// a city, warehouse, or tactical-tile identity.
/// </summary>
public sealed class EconomicMaterialReserveBalance
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawQuantity { get; }

    public EconomicMaterialReserveBalance(
        EconomicPointId economicPointId, CommodityId commodityId, ulong rawQuantity)
    {
        if (!economicPointId.IsValid || !commodityId.IsValid)
            throw new ArgumentException("Reserve balance needs valid economic point and commodity IDs.");
        if (rawQuantity == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawQuantity),
                "Reserve balances must be positive; omit empty rows.");
        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawQuantity = rawQuantity;
    }
}

/// <summary>
/// Minimal replay guard for application of one civilization's recurring
/// production withholding. Independent civilizations may settle the same turn.
/// </summary>
public sealed class EconomicMaterialReserveTurnCursor
{
    public CivilizationId CivilizationId { get; }
    public ulong LastAppliedTurn { get; }

    public EconomicMaterialReserveTurnCursor(
        CivilizationId civilizationId, ulong lastAppliedTurn)
    {
        if (!civilizationId.IsValid)
            throw new ArgumentException("Reserve cursor needs a valid civilization.");
        if (lastAppliedTurn == 0UL)
            throw new ArgumentOutOfRangeException(nameof(lastAppliedTurn));
        CivilizationId = civilizationId;
        LastAppliedTurn = lastAppliedTurn;
    }
}

/// <summary>
/// Immutable G3-B candidate for explicit material reserves. This type is NOT
/// yet a member of WorldState or its canonical hash; it cannot authenticate
/// a market turn, delivery or construction on its own.
/// </summary>
public sealed class EconomicMaterialReserveBook
{
    public static EconomicMaterialReserveBook Empty { get; } =
        new(Array.Empty<EconomicMaterialReserveBalance>(),
            Array.Empty<EconomicMaterialReserveTurnCursor>());

    public IReadOnlyList<EconomicMaterialReserveBalance> Balances { get; }
    public IReadOnlyList<EconomicMaterialReserveTurnCursor> AppliedTurnCursors { get; }

    public EconomicMaterialReserveBook(
        IEnumerable<EconomicMaterialReserveBalance> balances,
        IEnumerable<EconomicMaterialReserveTurnCursor> appliedTurnCursors)
    {
        ArgumentNullException.ThrowIfNull(balances);
        ArgumentNullException.ThrowIfNull(appliedTurnCursors);

        var entries = balances.ToArray();
        var cursors = appliedTurnCursors.ToArray();
        if (entries.Any(row => row is null) || cursors.Any(row => row is null))
            throw new ArgumentException("Reserve state cannot have null entries.");

        entries = entries
            .OrderBy(row => row.EconomicPointId.Value)
            .ThenBy(row => row.CommodityId.Value)
            .ToArray();
        cursors = cursors.OrderBy(row => row.CivilizationId.Value).ToArray();

        for (var i = 1; i < entries.Length; i++)
            if (entries[i - 1].EconomicPointId == entries[i].EconomicPointId &&
                entries[i - 1].CommodityId == entries[i].CommodityId)
                throw new ArgumentException("Duplicate reserve balance key.", nameof(balances));

        for (var i = 1; i < cursors.Length; i++)
            if (cursors[i - 1].CivilizationId == cursors[i].CivilizationId)
                throw new ArgumentException("Duplicate reserve turn cursor.", nameof(appliedTurnCursors));

        Balances = Array.AsReadOnly(entries);
        AppliedTurnCursors = Array.AsReadOnly(cursors);
    }

    public ulong RawAvailable(EconomicPointId point, CommodityId commodity)
    {
        if (!point.IsValid || !commodity.IsValid)
            throw new ArgumentException("Reserve lookup requires valid identities.");
        foreach (var row in Balances)
            if (row.EconomicPointId == point && row.CommodityId == commodity)
                return row.RawQuantity;
        return 0UL;
    }

    public ulong LastAppliedTurn(CivilizationId owner)
    {
        if (!owner.IsValid)
            throw new ArgumentException("Reserve turn lookup needs a valid civilization.");
        foreach (var cursor in AppliedTurnCursors)
            if (cursor.CivilizationId == owner)
                return cursor.LastAppliedTurn;
        return 0UL;
    }
}
