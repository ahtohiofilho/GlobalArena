using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using GlobalArena.World;

namespace GlobalArena.TacticalViewport3D;
internal static class Program
{
    [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new ThreeDForm()); }
}
internal sealed class ThreeDForm : Form
{
    readonly PhysicalTacticalIncidenceMap map;
    readonly StrategicSphericalGeometry coarseGeometry;
    readonly StrategicSphericalGeometry fineGeometry;
    readonly Font hud = new("Segoe UI", 11f);
    MeshPresentationPlan? plan;
    int focus = 29;
    double elevation = 45d, distance = 0.4d;
    ulong selected;
    int[] pickBuffer = Array.Empty<int>();
    int viewWidth, viewHeight;
    Point dragStart;
    bool dragging;
    public ThreeDForm()
    {
        Text = "Global Arena | Tactical 3D | spherical mesh";
        ClientSize = new Size(1280, 800); MinimumSize = new Size(640, 420);
        BackColor = Color.FromArgb(10, 22, 34);
        DoubleBuffered = true; KeyPreview = true;
        var coarse = new GoldbergParameters(6, 0);
        var fine = new GoldbergParameters(36, 0);
        map = PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(coarse, fine));
        var generator = new DeterministicWorldGenerator();
        coarseGeometry = generator.Generate(new WorldGenerationRequest(new WorldSeed(17UL), WorldGenerationVersion.Initial, coarse)).StrategicSphericalGeometry;
        fineGeometry = generator.Generate(new WorldGenerationRequest(new WorldSeed(17UL), WorldGenerationVersion.Initial, fine)).StrategicSphericalGeometry;
        UpdatePlan();
    }
    void UpdatePlan()
    {
        plan = MeshPresentationPlan.TacticalNeighborhood(map, new StrategicCellId((ulong)focus));
        selected = 0;
        Invalidate();
    }
    readonly record struct P(double X, double Y, double Depth);
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (plan == null) return;
        int w = Math.Max(1, ClientSize.Width), h = Math.Max(1, ClientSize.Height);
        viewWidth = w; viewHeight = h;
        var pixels = new int[w * h]; var depth = new double[w * h];
        pickBuffer = new int[w * h];
        Array.Fill(pixels, unchecked((int)0xff0b1825));
        Array.Fill(depth, double.PositiveInfinity);
        var cameraFrame = TacticalCameraFrame.Create(coarseGeometry, new StrategicCellId((ulong)focus));
        var camera = TacticalObliqueCamera.Create(cameraFrame, elevation, distance);
        int drawn = 0;
        var outlines = new List<(P[] Vertices, int Id)>();
        foreach (var tile in plan.PhysicalTileIds)
        {
            var geo = fineGeometry.GetCell(tile.FineStrategicCellId);
            var projected = new P[geo.BoundaryVertexIds.Count];
            bool valid = true;
            for (int i = 0; i < projected.Length; ++i)
            {
                var point = fineGeometry.GetVertex(geo.BoundaryVertexIds[i]).Position;
                try
                {
                    var q = camera.Project(point, 60d, (double)w / h);
                    if (!double.IsFinite(q.X) || !double.IsFinite(q.Y) || !double.IsFinite(q.Depth) || q.Depth <= 0)
                    { valid = false; break; }
                    projected[i] = new P((q.X + 1d) * 0.5d * w, (1d - q.Y) * 0.5d * h, q.Depth);
                }
                catch (NotSupportedException) { valid = false; break; }
            }
            if (!valid) continue; // Near-plane clipping is a follow-on feature; no invalid triangles.
            int id = checked((int)tile.FineStrategicCellId.Value);
            int fill = selected == (ulong)id ? unchecked((int)0xffe0a94e) : unchecked((int)0xff527d99);
            for (int i = 1; i + 1 < projected.Length; ++i)
                Raster(projected[0], projected[i], projected[i + 1], w, h, id, fill, pixels, depth, pickBuffer);
            outlines.Add((projected, id));
            drawn++;
        }
        // Draw canonical boundaries after filled faces; honor the final depth and ID buffers.
        foreach (var outline in outlines)
        {
            for (int i = 0; i < outline.Vertices.Length; i++)
                RasterEdge(outline.Vertices[i], outline.Vertices[(i + 1) % outline.Vertices.Length],
                    w, h, outline.Id, pixels, depth, pickBuffer);
        }
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, w, h);
        var bits = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try { Marshal.Copy(pixels, 0, bits.Scan0, pixels.Length); }
        finally { bmp.UnlockBits(bits); }
        e.Graphics.DrawImageUnscaled(bmp, 0, 0);
        using var brush = new SolidBrush(Color.White);
        e.Graphics.DrawString($"G(6,0) -> G(36,0) | Foco: {focus} | Desenhados: {drawn} | Tile: {(selected == 0 ? "-" : selected.ToString())} | Angulo: {elevation:F0}Â°", hud, brush, 16, 12);
        e.Graphics.DrawString("Roda: zoom | Setas esq/dir: foco | Setas cima/baixo: inclinacao | Clique: ID | Home: reset", hud, brush, 16, 38);
        e.Graphics.DrawString("Superficie esferica 3D e Z-buffer por pixel | QA visual experimental", hud, brush, 16, h - 32);
    }
    static void Raster(P a, P b, P c, int w, int h, int id, int fill, int[] pixels, double[] depth, int[] picks)
    {
        double area = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        if (Math.Abs(area) < 1e-12d) return;
        int xmin = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
        int xmax = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
        int ymin = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
        int ymax = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
        for (int y = ymin; y <= ymax; y++) for (int x = xmin; x <= xmax; x++)
        {
            double px = x + .5, py = y + .5;
            double wa = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) / area;
            double wb = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) / area;
            double wc = 1d - wa - wb;
            if (wa < -1e-9 || wb < -1e-9 || wc < -1e-9) continue;
            // Perspective-correct depth: reciprocal eye distance interpolates linearly in screen space.
            double invZ = wa / a.Depth + wb / b.Depth + wc / c.Depth;
            if (invZ <= 0) continue;
            double z = 1d / invZ;
            int offset = y * w + x;
            if (z >= depth[offset]) continue;
            depth[offset] = z; pixels[offset] = fill; picks[offset] = id;
        }
    }
    static void RasterEdge(P a, P b, int w, int h, int id,
        int[] pixels, double[] depth, int[] picks)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        int steps = (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) * 2d);
        if (steps <= 0 || steps > 100000) return;
        const int edgeColor = unchecked((int)0xffc2d6e0);
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            int x = (int)Math.Floor(a.X + dx * t);
            int y = (int)Math.Floor(a.Y + dy * t);
            if (x < 0 || x >= w || y < 0 || y >= h) continue;
            double invZ = (1d - t) / a.Depth + t / b.Depth;
            if (invZ <= 0d) continue;
            double z = 1d / invZ;
            int index = y * w + x;
            if (picks[index] != id || z > depth[index] + 0.002d) continue;
            pixels[index] = edgeColor;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); dragStart = e.Location; dragging = e.Button == MouseButtons.Left;
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!dragging || Math.Abs(e.X - dragStart.X) > 4 || Math.Abs(e.Y - dragStart.Y) > 4) { dragging = false; return; }
        dragging = false;
        if (e.X < 0 || e.X >= viewWidth || e.Y < 0 || e.Y >= viewHeight || pickBuffer.Length != viewWidth * viewHeight) return;
        selected = (ulong)pickBuffer[e.Y * viewWidth + e.X]; Invalidate();
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        distance = Math.Clamp(distance * (e.Delta > 0 ? 0.88d : 1d / 0.88d), .12d, 3d);
        Invalidate();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Right) focus = focus == map.CoarseTopology.Cells.Count ? 1 : focus + 1;
        else if (e.KeyCode == Keys.Left) focus = focus == 1 ? map.CoarseTopology.Cells.Count : focus - 1;
        else if (e.KeyCode == Keys.Up) elevation = Math.Min(85d, elevation + 5d);
        else if (e.KeyCode == Keys.Down) elevation = Math.Max(15d, elevation - 5d);
        else if (e.KeyCode == Keys.Home) { focus = 29; elevation = 45d; distance = .4d; }
        else return;
        UpdatePlan();
    }
}
