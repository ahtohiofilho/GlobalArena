using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Strategic population presence, NOT political ownership. Each resident is
/// counted exactly once at strategic scale, even at 2-way or 3-way incidence
/// boundaries. The display unit is one sixth of a worker-equivalent.
/// </summary>
public sealed record StrategicTerritorialPresenceEntry(
    StrategicCellId Territory, ulong WorkingSixths,
    ulong InTransitSixths, int OccupiedPhysicalTiles)
{
    public ulong TotalSixths => checked(WorkingSixths + InTransitSixths);
    public double WorkingEquivalent => WorkingSixths / 6.0;
    public double TravellingEquivalent => InTransitSixths / 6.0;
    public double TotalEquivalent => TotalSixths / 6.0;
}

/// <summary>
/// Projection only: the authoritative physical worker allocations and transit
/// orders determine population presence; the existing 01C incidence map
/// supplies territory overlap. No claims, owners, diplomacy or new game state.
/// </summary>
public sealed class StrategicTerritorialPresenceProjection
{
    public const ulong FractionScale = 6UL;

    private readonly IReadOnlyDictionary<StrategicCellId, StrategicTerritorialPresenceEntry>
        _byTerritory;

    public IReadOnlyList<StrategicTerritorialPresenceEntry> Territories { get; }
    public ulong TotalWorkers { get; }
    public ulong MovementTurn { get; }
    public int StrategicTerritoryCount { get; }

    private StrategicTerritorialPresenceProjection(
        StrategicTerritorialPresenceEntry[] entries, ulong totalWorkers,
        ulong movementTurn, int territoryCount)
    {
        Territories = Array.AsReadOnly(entries);
        _byTerritory = entries.ToDictionary(x => x.Territory);
        TotalWorkers = totalWorkers;
        MovementTurn = movementTurn;
        StrategicTerritoryCount = territoryCount;
    }

    public StrategicTerritorialPresenceEntry At(StrategicCellId territory)
    {
        if (!territory.IsValid || territory.Value > (ulong)StrategicTerritoryCount)
            throw new ArgumentException("Strategic territory does not exist.", nameof(territory));
        return _byTerritory.TryGetValue(territory, out var entry)
            ? entry : new StrategicTerritorialPresenceEntry(territory, 0UL, 0UL, 0);
    }

    public static StrategicTerritorialPresenceProjection FromPhysicalState(
        PhysicalTacticalEconomyRuntimeState physical,
        PhysicalTacticalIncidenceMap incidence)
    {
        ArgumentNullException.ThrowIfNull(physical);
        ArgumentNullException.ThrowIfNull(incidence);
        if (incidence.Refinement.CoarseParameters != physical.CoarseParameters ||
            incidence.Refinement.FineParameters != physical.FineParameters)
            throw new ArgumentException("Territorial presence must use the same canonical geography.");
        var presence = new Dictionary<StrategicCellId,
            (ulong Working, ulong Travelling, HashSet<ulong> Sites)>();

        void Add(PhysicalTacticalTileId tile, ulong count, bool travelling)
        {
            if (count == 0UL || !tile.IsValid ||
                tile.FineGoldbergParameters != physical.FineParameters ||
                tile.FineStrategicCellId.Value > (ulong)incidence.TileIncidences.Count)
                throw new ArgumentException("Worker presence references an invalid physical tile.");
            var tileIncidence = incidence.TileIncidences[
                checked((int)tile.FineStrategicCellId.Value - 1)];
            var owners = tileIncidence.IncidentCoarseCellIds;
            if (owners.Count is < 1 or > 3 || FractionScale % (ulong)owners.Count != 0)
                throw new InvalidOperationException("Unexpected strategic incidence denominator.");
            // 1 coarse incident -> 6/6, 2 -> 3/6 each, 3 -> 2/6 each.
            var share = checked(count * (FractionScale / (ulong)owners.Count));
            foreach (var coarse in owners)
            {
                if (!presence.TryGetValue(coarse, out var row))
                    row = (0UL, 0UL, new HashSet<ulong>());
                if (travelling)
                    row.Travelling = checked(row.Travelling + share);
                else
                    row.Working = checked(row.Working + share);
                row.Sites.Add(tile.FineStrategicCellId.Value);
                presence[coarse] = row;
            }
        }

        foreach (var group in physical.Allocations) Add(group.TileId, group.Workers, false);
        // While en route, retain strategic anchor at the departure tile until
        // arrival. No anticipatory control or presence at the destination.
        foreach (var transit in physical.Transits) Add(transit.Source, transit.Workers, true);

        var entries = presence.OrderBy(x => x.Key.Value)
            .Select(x => new StrategicTerritorialPresenceEntry(
                x.Key, x.Value.Working, x.Value.Travelling, x.Value.Sites.Count))
            .ToArray();
        var total = entries.Aggregate(0UL,
            (sum, x) => checked(sum + x.TotalSixths));
        if (total != checked(physical.TotalWorkers * FractionScale))
            throw new InvalidOperationException("Territorial presence duplicated or lost residents.");
        return new StrategicTerritorialPresenceProjection(
            entries, physical.TotalWorkers, physical.MovementTurn,
            incidence.CoarseTopology.Cells.Count);
    }
}
