using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// A presentation grouping ONLY. All individual canonical workers remain
/// separately addressed and journaled. A group exists iff there are real
/// workers of this occupation, owner and fine tile; never one per empty role.
/// </summary>
public sealed class FoundationWorkforceActivityGroup
{
    public PhysicalTacticalTileId Tile { get; }
    public CivilizationId Owner { get; }
    public PhysicalWorkerOccupation? Occupation { get; }
    public IReadOnlyList<PhysicalWorkerRuntimeRecord> Members { get; }
    public int Count => Members.Count;
    public ulong FirstWorkerId => Members[0].Id.Value;
    public decimal CapacityUnits => Members.Sum(
        worker => (decimal)worker.OutputMicroPerTurn) / 1_000_000m;
    public decimal DemandMultiplierTotal => Members.Sum(
        worker => (decimal)worker.DemandMultiplierMicro) / 1_000_000m;

    internal FoundationWorkforceActivityGroup(
        PhysicalTacticalTileId tile, CivilizationId owner,
        PhysicalWorkerOccupation? occupation,
        PhysicalWorkerRuntimeRecord[] members)
    {
        if (!tile.IsValid || !owner.IsValid || members.Length == 0 ||
            members.Any(w => w.TacticalTileId != tile ||
                w.Owner != owner || w.Occupation != occupation))
            throw new ArgumentException("Group must contain only actual matching workers.");
        Tile = tile;
        Owner = owner;
        Occupation = occupation;
        Members = Array.AsReadOnly(members.OrderBy(w => w.Id.Value).ToArray());
    }
}

public static class FoundationWorkforceActivityGroups
{
    public static IReadOnlyList<FoundationWorkforceActivityGroup> Project(
        PhysicalWorkerRuntimeState census)
    {
        ArgumentNullException.ThrowIfNull(census);
        if (!census.IsInitialized || !census.HasAuthoritativeTacticalLocations)
            throw new InvalidOperationException(
                "Grouped workforce presentation requires real physical fine tiles.");
        // Legacy v10 had no productive occupation. Its individuals remain
        // one marker each rather than being mislabeled into groups.
        var projected = census.Workers
            .GroupBy(w => (
                Tile: w.TacticalTileId!.Value,
                Owner: w.Owner,
                HasOccupation: w.Occupation.HasValue,
                RoleOrIndividual: w.Occupation.HasValue
                    ? (ulong)w.Occupation.Value : w.Id.Value))
            .OrderBy(g => g.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(g => g.Key.Owner.Value)
            .ThenBy(g => g.Key.HasOccupation ? 0 : 1)
            .ThenBy(g => g.Key.RoleOrIndividual)
            .Select(g => new FoundationWorkforceActivityGroup(
                g.Key.Tile, g.Key.Owner, g.First().Occupation, g.ToArray()))
            .ToArray();
        if (projected.Sum(g => g.Count) != census.Workers.Count ||
            projected.SelectMany(g => g.Members).Select(w => w.Id)
                .Distinct().Count() != census.Workers.Count)
            throw new InvalidOperationException(
                "Workforce presentation grouping duplicated or lost physical workers.");
        return Array.AsReadOnly(projected);
    }
}
