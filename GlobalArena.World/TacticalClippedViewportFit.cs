namespace GlobalArena.World;

/// <summary>Renderer-independent bounds of visible canonical tactical polygons.
/// Clipped vertices are presentation geometry; tile identity is unchanged.</summary>
public sealed class TacticalClippedViewportFit
{
    public double MinX { get; }
    public double MaxX { get; }
    public double MinY { get; }
    public double MaxY { get; }
    public int PhysicalTileCount { get; }
    public int VisibleTileCount { get; }
    public int HorizonCrossingTileCount { get; }

    private TacticalClippedViewportFit(double minX, double maxX, double minY,
        double maxY, int total, int visible, int crossing)
    {
        MinX = minX; MaxX = maxX; MinY = minY; MaxY = maxY;
        PhysicalTileCount = total; VisibleTileCount = visible;
        HorizonCrossingTileCount = crossing;
    }

    public static TacticalClippedViewportFit Create(
        TacticalCameraFrame camera, MeshPresentationPlan plan,
        StrategicSphericalGeometry fineGeometry, GoldbergParameters fineParameters,
        int pixelWidth, int pixelHeight, double marginFraction = 0.10d)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fineGeometry);
        if (plan.Mode != MeshPresentationMode.TacticalNeighborhood ||
            plan.Focus != camera.Focus || plan.PhysicalTileIds.Count == 0)
            throw new ArgumentException("A tactical plan with a matching focus is required.", nameof(plan));
        if (!fineParameters.IsValid ||
            fineGeometry.Cells.Count != checked((int)fineParameters.StrategicCellCount))
            throw new ArgumentException("Canonical fine geometry mismatch.", nameof(fineGeometry));
        if (pixelWidth <= 0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        if (pixelHeight <= 0) throw new ArgumentOutOfRangeException(nameof(pixelHeight));
        if (!double.IsFinite(marginFraction) || marginFraction < 0d || marginFraction >= 1d)
            throw new ArgumentOutOfRangeException(nameof(marginFraction));

        var ids = new HashSet<PhysicalTacticalTileId>();
        var minX = double.PositiveInfinity; var maxX = double.NegativeInfinity;
        var minY = double.PositiveInfinity; var maxY = double.NegativeInfinity;
        var visible = 0; var crossing = 0;
        foreach (var physical in plan.PhysicalTileIds)
        {
            if (!physical.IsValid || physical.FineGoldbergParameters != fineParameters ||
                !ids.Add(physical))
                throw new ArgumentException("Physical tile IDs must be unique and canonical.", nameof(plan));
            var cell = fineGeometry.GetCell(physical.FineStrategicCellId);
            var polygon = cell.BoundaryVertexIds
                .Select(vertex => fineGeometry.GetVertex(vertex).Position).ToArray();
            var forward = polygon.Select(point => TacticalSurfaceProjection.Project(camera, point).Forward).ToArray();
            if (forward.Any(value => value < 0d) && forward.Any(value => value > 0d)) crossing++;
            var clipped = TacticalHorizonClipper.Clip(camera, polygon);
            if (clipped.Count < 3) continue;
            visible++;
            foreach (var vertex in clipped)
            {
                var point = TacticalSurfaceProjection.Project(camera, vertex);
                if (point.Forward < -1e-10d)
                    throw new InvalidOperationException("Horizon clipping produced a hidden vertex.");
                minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
            }
        }
        if (visible == 0) throw new NotSupportedException("No visible tactical polygon to frame.");
        var centerX = (minX + maxX) * 0.5d;
        var centerY = (minY + maxY) * 0.5d;
        var halfX = (maxX - minX) * 0.5d / (1d - marginFraction);
        var halfY = (maxY - minY) * 0.5d / (1d - marginFraction);
        if (!double.IsFinite(halfX) || !double.IsFinite(halfY) ||
            halfX <= 0d || halfY <= 0d)
            throw new InvalidOperationException("Clipped bounds are degenerate.");
        var aspect = (double)pixelWidth / pixelHeight;
        halfX = Math.Max(halfX, halfY * aspect);
        halfY = halfX / aspect;
        return new TacticalClippedViewportFit(centerX - halfX, centerX + halfX,
            centerY - halfY, centerY + halfY, ids.Count, visible, crossing);
    }
}
