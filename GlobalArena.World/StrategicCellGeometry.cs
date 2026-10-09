namespace GlobalArena.World;

public sealed class StrategicCellGeometry
{
    public StrategicCellId CellId { get; }

    public SphericalPoint3 Center { get; }

    public IReadOnlyList<StrategicVertexId> BoundaryVertexIds { get; }

    internal StrategicCellGeometry(
        StrategicCellId cellId,
        SphericalPoint3 center,
        IEnumerable<StrategicVertexId> boundaryVertexIds)
    {
        if (!cellId.IsValid)
        {
            throw new ArgumentException(
                "Strategic cell geometry requires a valid cell ID.",
                nameof(cellId));
        }

        if (!center.IsValid)
        {
            throw new ArgumentException(
                "Strategic cell geometry center must be a valid normalized spherical point.",
                nameof(center));
        }

        ArgumentNullException.ThrowIfNull(
            boundaryVertexIds);

        var boundary =
            boundaryVertexIds.ToArray();

        if (boundary.Length is < 5 or > 6
            || boundary.Any(id => !id.IsValid)
            || boundary.Distinct().Count()
                != boundary.Length)
        {
            throw new ArgumentException(
                "Strategic cell geometry boundary must contain five or six unique valid strategic vertex IDs.",
                nameof(boundaryVertexIds));
        }

        CellId =
            cellId;

        Center =
            center;

        BoundaryVertexIds =
            Array.AsReadOnly(
                boundary);
    }
}
