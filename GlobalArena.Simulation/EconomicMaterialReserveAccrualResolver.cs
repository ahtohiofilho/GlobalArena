using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// G3-B reference transition: add only materials explicitly withheld from
/// current production. Additional destination market demand is NOT a physical
/// receipt and can never increase this book without later delivery evidence.
/// The caller still owns turn/flow provenance; no WorldState mutation occurs.
/// </summary>
public sealed class EconomicMaterialReserveAccrualResolver
{
    public EconomicMaterialReserveBook AccrueOriginHolds(
        WorldState world, CivilizationId owner, ulong turnOrdinal,
        EconomicMaterialReserveBook previous,
        EconomicDeliberateMaterialFlowResult plannedFlows)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(plannedFlows);
        if (turnOrdinal == 0UL)
            throw new ArgumentOutOfRangeException(nameof(turnOrdinal));
        if (!owner.IsValid || !world.Civilizations.Civilizations.Contains(owner))
            throw new ArgumentException("Reserve owner must exist in the world.", nameof(owner));
        if (turnOrdinal <= previous.LastAppliedTurn(owner))
            throw new InvalidOperationException("Same or older production turn cannot be accrued again.");

        var points = world.Economy.EconomicPoints.ToDictionary(x => x.Id);
        var balances = previous.Balances.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);

        // The G3-A planner validates supply identities and cannot introduce
        // a hold outside available production; keep the ownership guard here.
        foreach (var hold in plannedFlows.OriginHolds)
        {
            if (!points.TryGetValue(hold.EconomicPointId, out var point) ||
                point.Owner != owner)
                throw new InvalidOperationException("Only current owned production can be accrued.");
            var key = (hold.EconomicPointId, hold.CommodityId);
            balances.TryGetValue(key, out var existing);
            balances[key] = checked(existing + hold.RawQuantity);
        }

        var cursors = previous.AppliedTurnCursors
            .Where(x => x.CivilizationId != owner)
            .Append(new EconomicMaterialReserveTurnCursor(owner, turnOrdinal));

        return new EconomicMaterialReserveBook(
            balances.Where(x => x.Value > 0UL)
                .Select(x => new EconomicMaterialReserveBalance(
                    x.Key.EconomicPointId, x.Key.CommodityId, x.Value)),
            cursors);
    }
}

/// <summary>
/// Construction material requirement at a specific economic point. Numbers
/// belong to a versioned construction recipe once that catalog is approved.
/// </summary>
public sealed class EconomicConstructionMaterialRequirement
{
    public EconomicPointId EconomicPointId { get; }
    public CommodityId CommodityId { get; }
    public ulong RawQuantity { get; }

    public EconomicConstructionMaterialRequirement(
        EconomicPointId economicPointId, CommodityId commodityId, ulong rawQuantity)
    {
        if (!economicPointId.IsValid || !commodityId.IsValid)
            throw new ArgumentException("Construction input needs valid location and commodity.");
        if (rawQuantity == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawQuantity));
        EconomicPointId = economicPointId;
        CommodityId = commodityId;
        RawQuantity = rawQuantity;
    }
}

public sealed class EconomicConstructionMaterialQuoteLine
{
    public EconomicConstructionMaterialRequirement Requirement { get; }
    public ulong RawAvailable { get; }
    public ulong RawShortage { get; }

    internal EconomicConstructionMaterialQuoteLine(
        EconomicConstructionMaterialRequirement requirement, ulong available)
    {
        Requirement = requirement;
        RawAvailable = available;
        RawShortage = requirement.RawQuantity > available
            ? requirement.RawQuantity - available : 0UL;
    }
}

/// <summary>
/// Pure affordability check only: a quote never consumes resources, grants
/// an asset, proves ownership of a fine tile, or authorizes construction.
/// </summary>
public sealed class EconomicConstructionMaterialQuote
{
    public IReadOnlyList<EconomicConstructionMaterialQuoteLine> Lines { get; }
    public bool IsMateriallyAffordable => Lines.All(x => x.RawShortage == 0UL);

    internal EconomicConstructionMaterialQuote(
        EconomicConstructionMaterialQuoteLine[] lines)
    {
        Lines = Array.AsReadOnly(lines);
    }
}

public sealed class EconomicConstructionMaterialQuoteResolver
{
    public EconomicConstructionMaterialQuote Evaluate(
        WorldState world, CivilizationId owner,
        EconomicMaterialReserveBook reserves,
        IEnumerable<EconomicConstructionMaterialRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(reserves);
        ArgumentNullException.ThrowIfNull(requirements);
        if (!owner.IsValid || !world.Civilizations.Civilizations.Contains(owner))
            throw new ArgumentException("Construction owner must exist in world.", nameof(owner));

        var points = world.Economy.EconomicPoints.ToDictionary(x => x.Id);
        var ordered = requirements.ToArray();
        if (ordered.Any(x => x is null))
            throw new ArgumentException("Construction requirements cannot contain null.");

        ordered = ordered.OrderBy(x => x.EconomicPointId.Value)
            .ThenBy(x => x.CommodityId.Value).ToArray();
        for (var i = 0; i < ordered.Length; i++)
        {
            var req = ordered[i];
            if (!points.TryGetValue(req.EconomicPointId, out var point) ||
                point.Owner != owner)
                throw new InvalidOperationException("Construction input must belong to issuer's point.");
            if (i > 0 && ordered[i - 1].EconomicPointId == req.EconomicPointId &&
                ordered[i - 1].CommodityId == req.CommodityId)
                throw new ArgumentException("Duplicate construction material requirement.");
        }

        return new EconomicConstructionMaterialQuote(ordered
            .Select(x => new EconomicConstructionMaterialQuoteLine(
                x, reserves.RawAvailable(x.EconomicPointId, x.CommodityId)))
            .ToArray());
    }
}
