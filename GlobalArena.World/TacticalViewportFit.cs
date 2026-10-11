namespace GlobalArena.World;

/// <summary>
/// Orthographic fitting bounds of actual canonical fine-mesh polygon vertices.
/// A horizon-crossing selection is deliberately rejected until clipping exists.
/// No GPU resources, new tile identities, or geometry mutation.
/// </summary>
public sealed class TacticalViewportFit
{
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }
    public double HalfSpan { get; }
    public int PhysicalTileCount { get; }

    private TacticalViewportFit(double minX, double maxX, double minY,
        double maxY, double halfSpan, int physicalTileCount)
    {
        MinX = minX;
        MaxX = maxX;
        MinY = minY;
        MaxY = maxY;
        HalfSpan = halfSpan;
        PhysicalTileCount = physicalTileCount;
    }

    public static TacticalViewportFit Create(
        TacticalCameraFrame camera,
        MeshPresentationPlan plan,
        StrategicSphericalGeometry fineGeometry,
        GoldbergParameters fineParameters,
        double marginFraction = 0.10d)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fineGeometry);
        if (plan.Mode != MeshPresentationMode.TacticalNeighborhood ||
            plan.Focus != camera.Focus || plan.PhysicalTileIds.Count == 0)
            throw new ArgumentException("Tactical plan must match camera focus and contain physical tiles.", nameof(plan));
        if (!fineParameters.IsValid ||
            fineGeometry.Cells.Count != checked((int)fineParameters.StrategicCellCount))
            throw new ArgumentException("Fine geometry does not match the required canonical fine Goldberg parameters.", nameof(fineGeometry));
        if (!double.IsFinite(marginFraction) || marginFraction < 0d || marginFraction >= 1d)
            throw new ArgumentOutOfRangeException(nameof(marginFraction));

        var vertices = new HashSet<StrategicVertexId>();
        var physicalIds = new HashSet<PhysicalTacticalTileId>();
        foreach (var physical in plan.PhysicalTileIds)
        {
            if (!physical.IsValid || physical.FineGoldbergParameters != fineParameters ||
                !physicalIds.Add(physical))
                throw new ArgumentException("Physical identities must be unique and use the fine geometry parameters.", nameof(plan));
            var cell = fineGeometry.GetCell(physical.FineStrategicCellId);
            foreach (var vertex in cell.BoundaryVertexIds)
                vertices.Add(vertex);
        }
        if (vertices.Count == 0)
            throw new ArgumentException("Selection has no fine boundary vertices.", nameof(plan));

        var minX = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity;
        var maxY = double.NegativeInfinity;
        foreach (var vertex in vertices)
        {
            var projected = TacticalSurfaceProjection.Project(
                camera, fineGeometry.GetVertex(vertex).Position);
            // An unseen vertex means that vertex-only bounds cannot guarantee full coverage.
            if (projected.Forward <= 1e-10d)
                throw new NotSupportedException("Tactical selection reaches the horizon; polygon clipping is required before reliable fitting.");
            minX = Math.Min(minX, projected.X);
            maxX = Math.Max(maxX, projected.X);
            minY = Math.Min(minY, projected.Y);
            maxY = Math.Max(maxY, projected.Y);
        }
        var centerX = (minX + maxX) * 0.5d;
        var centerY = (minY + maxY) * 0.5d;
        var halfSpan = Math.Max(maxX - minX, maxY - minY) * 0.5d / (1d - marginFraction);
        if (!double.IsFinite(halfSpan) || halfSpan <= 0d)
            throw new InvalidOperationException("Projected tactical extent is degenerate.");
        return new TacticalViewportFit(centerX - halfSpan, centerX + halfSpan,
            centerY - halfSpan, centerY + halfSpan, halfSpan, physicalIds.Count);
    }
}