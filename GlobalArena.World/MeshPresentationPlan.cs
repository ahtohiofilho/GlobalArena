namespace GlobalArena.World;

/// <summary>The intended level of identity detail, not an OpenGL draw command.</summary>
public enum MeshPresentationMode
{
    StrategicOverview = 0,
    TacticalNeighborhood = 1
}

/// <summary>
/// Renderer-neutral, immutable selection of canonical identities.
/// Geometry, camera, color, GPU resources and simulation state remain separate.
/// </summary>
public sealed class MeshPresentationPlan
{
    public MeshPresentationMode Mode { get; }
    public StrategicCellId? Focus { get; }
    public IReadOnlyList<StrategicCellId> StrategicCellIds { get; }
    public IReadOnlyList<PhysicalTacticalTileId> PhysicalTileIds { get; }

    private MeshPresentationPlan(
        MeshPresentationMode mode,
        StrategicCellId? focus,
        StrategicCellId[] strategic,
        PhysicalTacticalTileId[] physical)
    {
        Mode = mode;
        Focus = focus;
        StrategicCellIds = Array.AsReadOnly(strategic);
        PhysicalTileIds = Array.AsReadOnly(physical);
    }

    /// <summary>Plan for the entire coarse world; no fine tactical data requested.</summary>
    public static MeshPresentationPlan StrategicOverview(StrategicTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        var cells = topology.Cells
            .Select(cell => cell.Id)
            .OrderBy(id => id.Value)
            .ToArray();
        return new MeshPresentationPlan(MeshPresentationMode.StrategicOverview,
            null, cells, Array.Empty<PhysicalTacticalTileId>());
    }

    // Compatibility overload: callers should prefer the topology-only API.
    public static MeshPresentationPlan StrategicOverview(PhysicalTacticalIncidenceMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return StrategicOverview(map.CoarseTopology);
    }

    /// <summary>Plan for focus + actual adjacent strategic cells, without re-identifying fine tiles.</summary>
    public static MeshPresentationPlan TacticalNeighborhood(
        PhysicalTacticalIncidenceMap map, StrategicCellId focus)
    {
        ArgumentNullException.ThrowIfNull(map);
        var frame = PhysicalTacticalViewportFrame.Create(map, focus);
        return new MeshPresentationPlan(MeshPresentationMode.TacticalNeighborhood,
            focus, frame.StrategicCellIds.ToArray(), frame.PhysicalTileIds.ToArray());
    }
}