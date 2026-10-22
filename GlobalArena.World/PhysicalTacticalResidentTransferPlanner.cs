namespace GlobalArena.World;

/// <summary>
/// Canonical data-only transfer between adjacent strategic foci.
/// Does not own projected coordinates, camera state, rendering or eviction timing.
/// </summary>
public sealed class PhysicalTacticalResidentTransferPlan
{
    public StrategicCellId SourceFocus { get; }
    public StrategicCellId DestinationFocus { get; }
    public IReadOnlyList<PhysicalTacticalTileId> SourceResident { get; }
    public IReadOnlyList<PhysicalTacticalTileId> DestinationResident { get; }
    public IReadOnlyList<PhysicalTacticalTileId> Reused { get; }
    public IReadOnlyList<PhysicalTacticalTileId> ToPrepare { get; }
    public IReadOnlyList<PhysicalTacticalTileId> EligibleForRetirement { get; }

    internal PhysicalTacticalResidentTransferPlan(
        StrategicCellId sourceFocus,
        StrategicCellId destinationFocus,
        IReadOnlyList<PhysicalTacticalTileId> sourceResident,
        IReadOnlyList<PhysicalTacticalTileId> destinationResident,
        IReadOnlyList<PhysicalTacticalTileId> reused,
        IReadOnlyList<PhysicalTacticalTileId> toPrepare,
        IReadOnlyList<PhysicalTacticalTileId> eligibleForRetirement)
    {
        SourceFocus = sourceFocus;
        DestinationFocus = destinationFocus;
        SourceResident = sourceResident;
        DestinationResident = destinationResident;
        Reused = reused;
        ToPrepare = toPrepare;
        EligibleForRetirement = eligibleForRetirement;
    }
}

/// <summary>
/// Deterministic planning only. No automatic mesh swap or mutation of authoritative topology.
/// The caller MUST keep visible tiles resident until the replacement chart is visually safe.
/// </summary>
public static class PhysicalTacticalResidentTransferPlanner
{
    public static PhysicalTacticalResidentTransferPlan PlanAdjacent(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId sourceFocus,
        StrategicCellId destinationFocus,
        int residentRings = 3)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (residentRings < 1 || residentRings > 3)
            throw new ArgumentOutOfRangeException(nameof(residentRings));

        var source = map.CoarseTopology.Cells.SingleOrDefault(cell => cell.Id == sourceFocus);
        var destination = map.CoarseTopology.Cells.SingleOrDefault(cell => cell.Id == destinationFocus);
        if (source is null || destination is null)
            throw new ArgumentOutOfRangeException(nameof(destinationFocus), "Unknown strategic focus.");
        if (sourceFocus != destinationFocus && !source.AdjacentCellIds.Contains(destinationFocus))
            throw new ArgumentException("Transfer is limited to the same or adjacent strategic focus.", nameof(destinationFocus));

        var sourceTiles = PhysicalTacticalViewportQuery.SelectTiles(map, sourceFocus, residentRings).ToArray();
        var targetTiles = PhysicalTacticalViewportQuery.SelectTiles(map, destinationFocus, residentRings).ToArray();
        var oldSet = sourceTiles.ToHashSet();
        var targetSet = targetTiles.ToHashSet();

        static IReadOnlyList<PhysicalTacticalTileId> Frozen(IEnumerable<PhysicalTacticalTileId> values) =>
            Array.AsReadOnly(values.OrderBy(id => id.FineStrategicCellId.Value).ToArray());

        var reused = Frozen(targetTiles.Where(oldSet.Contains));
        var prepare = Frozen(targetTiles.Where(id => !oldSet.Contains(id)));
        var retire = Frozen(sourceTiles.Where(id => !targetSet.Contains(id)));
        return new PhysicalTacticalResidentTransferPlan(
            sourceFocus, destinationFocus, Frozen(sourceTiles), Frozen(targetTiles), reused, prepare, retire);
    }
}