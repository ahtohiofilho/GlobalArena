namespace GlobalArena.World;

/// <summary>Renderer-independent, normalized orthographic polygons with canonical physical IDs.</summary>
public sealed class TacticalViewportDrawData
{
    public readonly record struct Point(double X, double Y);
    public sealed class Polygon
    {
        public PhysicalTacticalTileId TileId { get; }
        public IReadOnlyList<Point> Vertices { get; }
        internal Polygon(PhysicalTacticalTileId id, Point[] vertices)
        {
            TileId = id;
            Vertices = Array.AsReadOnly(vertices);
        }
    }

    public IReadOnlyList<Polygon> Polygons { get; }
    public int SelectedPhysicalTileCount { get; }

    private TacticalViewportDrawData(Polygon[] polygons, int selectedCount)
    {
        Polygons = Array.AsReadOnly(polygons);
        SelectedPhysicalTileCount = selectedCount;
    }

    /// <summary>Outputs NDC positions in [-1,+1]; does not perform GPU drawing or terrain occlusion.</summary>
    public static TacticalViewportDrawData Create(
        TacticalCameraFrame camera,
        MeshPresentationPlan plan,
        StrategicSphericalGeometry fineGeometry,
        GoldbergParameters fineParameters,
        TacticalClippedViewportFit fit)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fineGeometry);
        ArgumentNullException.ThrowIfNull(fit);
        if (plan.Mode != MeshPresentationMode.TacticalNeighborhood ||
            plan.Focus != camera.Focus || plan.PhysicalTileIds.Count == 0)
            throw new ArgumentException("A matching nonempty tactical plan is required.", nameof(plan));
        if (!fineParameters.IsValid || fineGeometry.Cells.Count != checked((int)fineParameters.StrategicCellCount))
            throw new ArgumentException("Fine geometry parameters mismatch.", nameof(fineGeometry));
        var dx = fit.MaxX - fit.MinX;
        var dy = fit.MaxY - fit.MinY;
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || dx <= 0d || dy <= 0d ||
            fit.PhysicalTileCount != plan.PhysicalTileIds.Count)
            throw new ArgumentException("Viewport fit does not match physical tile count or has invalid bounds.", nameof(fit));

        var seen = new HashSet<PhysicalTacticalTileId>();
        var output = new List<Polygon>();
        foreach (var tileId in plan.PhysicalTileIds)
        {
            if (!tileId.IsValid || tileId.FineGoldbergParameters != fineParameters || !seen.Add(tileId))
                throw new ArgumentException("Physical tile IDs must be distinct and match fine parameters.", nameof(plan));
            var boundary = fineGeometry.GetCell(tileId.FineStrategicCellId).BoundaryVertexIds;
            var vertices = boundary.Select(id => fineGeometry.GetVertex(id).Position).ToArray();
            var visible = TacticalHorizonClipper.Clip(camera, vertices);
            if (visible.Count < 3) continue;
            var points = new Point[visible.Count];
            for (var i = 0; i < visible.Count; i++)
            {
                var projected = TacticalSurfaceProjection.Project(camera, visible[i]);
                var x = 2d * (projected.X - fit.MinX) / dx - 1d;
                var y = 2d * (projected.Y - fit.MinY) / dy - 1d;
                if (!double.IsFinite(x) || !double.IsFinite(y) ||
                    x < -1d - 1e-9d || x > 1d + 1e-9d ||
                    y < -1d - 1e-9d || y > 1d + 1e-9d)
                    throw new InvalidOperationException("Visible polygon vertex lies outside the computed fit.");
                points[i] = new Point(x, y);
            }
            output.Add(new Polygon(tileId, points));
        }
        if (output.Count != fit.VisibleTileCount)
            throw new ArgumentException("Viewport fit and projected visible tile count differ.", nameof(fit));
        return new TacticalViewportDrawData(output.ToArray(), seen.Count);
    }
}
