using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Debug-only lifecycle of historical WorkShelter fixture saves.
/// This is NOT an approved real-production building, city tier or order.

/// </summary>
public enum TacticalWorksiteMapStage : byte
{
    MaterialFrame = 1,
    UnderConstruction = 2,
    Completed = 3,
    Operating = 4
}

/// <summary>
/// One aggregated LABORATORY marker per tile with historical material and
/// worker evidence. Real save/replay state does not authorize player-facing
/// buildings or manufacturing advantages.
/// </summary>
public sealed record CanonicalTacticalWorksiteMapTile(
    ulong FineTile,
    TacticalWorksiteMapStage Stage,
    int UnusedMaterialFrames,
    int BuildingFacilities,
    int CompletedFacilities,
    int OperatingFacilities,
    ulong ReservedConstructionWorkers,
    ulong OperatingWorkers)
{
    public bool IsOnlyPresentation => true;
    public bool IsApprovedGameplayFacility => false;
    public bool IsProductivityBonus => false;
    public bool GeneratesPeopleOrResources => false;
}

public sealed class CanonicalTacticalWorksiteMapProjection
{
    private readonly IReadOnlyDictionary<ulong, CanonicalTacticalWorksiteMapTile> _byTile;
    public IReadOnlyList<CanonicalTacticalWorksiteMapTile> Tiles { get; }
    public ulong MovementTurn { get; }
    public int PhysicalFacilityCount { get; }
    public bool MutatesCanonicalWorld => false;
    public bool ContainsApprovedGameplayFacilities => false;

    private CanonicalTacticalWorksiteMapProjection(
        CanonicalTacticalWorksiteMapTile[] tiles,
        ulong movementTurn, int physicalFacilityCount)
    {
        Tiles = Array.AsReadOnly(tiles);
        _byTile = tiles.ToDictionary(x => x.FineTile);
        MovementTurn = movementTurn;
        PhysicalFacilityCount = physicalFacilityCount;
    }

    public CanonicalTacticalWorksiteMapTile? At(PhysicalTacticalTileId tile) =>
        _byTile.GetValueOrDefault(tile.FineStrategicCellId.Value);

    public CanonicalTacticalWorksiteMapTile? At(ulong fineTile) =>
        _byTile.GetValueOrDefault(fineTile);

    public static CanonicalTacticalWorksiteMapProjection FromSession(
        CanonicalPhysicalTacticalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var before = session.CurrentHash;
        var state = session.State;

        var frames = session.ExecutedPrimitiveAssemblies;
        var facilities = state.PrimitiveFacilities;
        var frameOrdinals = frames.ToDictionary(x => x.ActionOrdinal);
        foreach (var facility in facilities)
        {
            if (!frameOrdinals.TryGetValue(facility.AssemblyActionOrdinal,
                    out var frame) ||
                frame.FineTile != facility.Tile.FineStrategicCellId.Value)
                throw new InvalidOperationException(
                    "A physical facility cannot be drawn without an actual incorporated material frame.");
        }
        var attached = facilities.Select(x => x.AssemblyActionOrdinal).ToHashSet();
        var availableFrames = frames.Where(x => !attached.Contains(x.ActionOrdinal))
            .GroupBy(x => x.FineTile)
            .ToDictionary(x => x.Key, x => x.Count());

        var activeSites = facilities
            .GroupBy(x => x.Tile.FineStrategicCellId.Value)
            .ToDictionary(x => x.Key, x => x.ToArray());
        var allTiles = availableFrames.Keys.Union(activeSites.Keys)
            .OrderBy(x => x).ToArray();

        var result = new List<CanonicalTacticalWorksiteMapTile>();
        foreach (var tile in allTiles)
        {
            var local = activeSites.GetValueOrDefault(tile) ??
                Array.Empty<PhysicalPrimitiveFacility>();
            var unused = availableFrames.GetValueOrDefault(tile);
            var building = local.Count(x => !x.IsOperational);
            var complete = local.Count(x => x.IsOperational);
            var operating = local.Count(x => x.IsOperational &&
                x.OperatingWorkers > 0UL);
            var stage = building > 0
                ? TacticalWorksiteMapStage.UnderConstruction
                : operating > 0
                    ? TacticalWorksiteMapStage.Operating
                    : complete > 0
                        ? TacticalWorksiteMapStage.Completed
                        : TacticalWorksiteMapStage.MaterialFrame;
            var crew = local.Aggregate(0UL, (n, x) =>
                checked(n + x.ReservedWorkers));
            var operators = local.Aggregate(0UL, (n, x) =>
                checked(n + x.OperatingWorkers));
            var visible = new CanonicalTacticalWorksiteMapTile(
                tile, stage, unused, building, complete, operating,
                crew, operators);
            if (unused + building + complete == 0)
                throw new InvalidOperationException(
                    "A worksite map marker needs actual canonical evidence.");
            result.Add(visible);
        }
        if (result.Sum(x => x.BuildingFacilities + x.CompletedFacilities) !=
            facilities.Count ||
            result.Sum(x => x.UnusedMaterialFrames) + facilities.Count !=
            frames.Count)
            throw new InvalidOperationException(
                "Worksite map markers duplicated or lost installed material/physical assets.");
        if (!before.Equals(session.CurrentHash))
            throw new InvalidOperationException(
                "Read-only worksite rendering changed the canonical world.");
        return new CanonicalTacticalWorksiteMapProjection(
            result.ToArray(), state.MovementTurn, facilities.Count);
    }
}
