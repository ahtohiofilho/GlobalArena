using System.Drawing.Drawing2D;
using GlobalArena.World;

namespace GlobalArena.TacticalViewport;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new TacticalForm());
    }
}

internal sealed class TacticalForm : Form
{
    private readonly PhysicalTacticalIncidenceMap _map;
    private readonly StrategicSphericalGeometry _coarseGeometry;
    private readonly StrategicSphericalGeometry _fineGeometry;
    private TacticalViewportDrawData? _drawData;
    private int _focusIndex;
    private float _zoom = 1.0f;
    private PointF _pan;
    private Point _mouseDown;
    private PointF _panDown;
    private bool _drag;
    private ulong? _selectedTile;
    private readonly Font _hudFont = new("Segoe UI", 11f);

    internal TacticalForm()
    {
        Text = "Global Arena | Tactical viewport prototype";
        ClientSize = new Size(1280, 800);
        MinimumSize = new Size(640, 420);
        BackColor = Color.FromArgb(12, 23, 35);
        DoubleBuffered = true;
        KeyPreview = true;

        var coarseParameters = new GoldbergParameters(6, 0);
        var fineParameters = new GoldbergParameters(36, 0);
        _map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(coarseParameters, fineParameters));
        var generator = new DeterministicWorldGenerator();
        _coarseGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(17UL), WorldGenerationVersion.Initial, coarseParameters))
            .StrategicSphericalGeometry;
        _fineGeometry = generator.Generate(new WorldGenerationRequest(
            new WorldSeed(17UL), WorldGenerationVersion.Initial, fineParameters))
            .StrategicSphericalGeometry;
        SetFocus(1);
    }

    private void SetFocus(int id)
    {
        if (id < 1 || id > _map.CoarseTopology.Cells.Count)
            return;
        _focusIndex = id;
        var focus = new StrategicCellId((ulong)id);
        var plan = MeshPresentationPlan.TacticalNeighborhood(_map, focus);
        var camera = TacticalCameraFrame.Create(_coarseGeometry, focus);
        var fit = TacticalClippedViewportFit.Create(camera, plan, _fineGeometry,
            _map.Refinement.FineParameters, Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        _drawData = TacticalViewportDrawData.Create(camera, plan, _fineGeometry,
            _map.Refinement.FineParameters, fit);
        _selectedTile = null;
        _pan = new PointF(0, 0);
        _zoom = 1.0f;
        Invalidate();
    }

    private PointF ToScreen(TacticalViewportDrawData.Point p)
    {
        // NDC coordinates are normalized independently in X and Y using
        // the viewport aspect ratio. Restore pixel aspect by scaling each
        // axis with its corresponding client dimension; do not use one
        // common pixel scale for both normalized axes.
        const float padding = 0.88f;
        return new PointF(ClientSize.Width * (0.5f + 0.5f * padding * _zoom * (float)p.X) + _pan.X,
            ClientSize.Height * (0.5f - 0.5f * padding * _zoom * (float)p.Y) + _pan.Y);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // Keep the fitted NDC aspect equal to the actual client rectangle.
        // During form construction the map/data have not been initialized.
        if (_drawData is null || _focusIndex < 1 || ClientSize.Width < 1 || ClientSize.Height < 1)
            return;
        var focus = new StrategicCellId((ulong)_focusIndex);
        var plan = MeshPresentationPlan.TacticalNeighborhood(_map, focus);
        var camera = TacticalCameraFrame.Create(_coarseGeometry, focus);
        var fit = TacticalClippedViewportFit.Create(camera, plan, _fineGeometry,
            _map.Refinement.FineParameters, ClientSize.Width, ClientSize.Height);
        _drawData = TacticalViewportDrawData.Create(camera, plan, _fineGeometry,
            _map.Refinement.FineParameters, fit);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_drawData is null) return;
        using var fill = new SolidBrush(Color.FromArgb(72, 111, 139));
        using var selectedFill = new SolidBrush(Color.FromArgb(210, 162, 69));
        using var border = new Pen(Color.FromArgb(177, 208, 218), 0.8f);
        using var selectedBorder = new Pen(Color.FromArgb(250, 221, 160), 1.4f);
        foreach (var polygon in _drawData.Polygons)
        {
            if (polygon.Vertices.Count < 3) continue;
            var points = polygon.Vertices.Select(ToScreen).ToArray();
            using var path = new GraphicsPath();
            path.AddPolygon(points);
            var selected = _selectedTile == polygon.TileId.FineStrategicCellId.Value;
            e.Graphics.FillPath(selected ? selectedFill : fill, path);
            e.Graphics.DrawPath(selected ? selectedBorder : border, path);
        }
        using var hudBrush = new SolidBrush(Color.White);
        e.Graphics.DrawString($"G(6,0) -> G(36,0) | Foco: {_focusIndex} | " +
            $"Tiles visiveis: {_drawData.Polygons.Count} | Tile: {_selectedTile?.ToString() ?? "-"}",
            _hudFont, hudBrush, 16f, 12f);
        e.Graphics.DrawString("Roda: zoom | Arrastar: deslocar | Setas: mudar foco | Home: reset | Clique: selecionar ID",
            _hudFont, hudBrush, 16f, 36f);
        e.Graphics.DrawString("Prototipo 2D ortografico. Camera obliqua 3D e terreno curvo: etapa futura.",
            _hudFont, hudBrush, 16f, ClientSize.Height - 31f);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.12f : 1f / 1.12f), 0.3f, 10f);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _mouseDown = e.Location;
        _panDown = _pan;
        _drag = e.Button == MouseButtons.Left;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_drag) return;
        _pan = new PointF(_panDown.X + e.X - _mouseDown.X,
            _panDown.Y + e.Y - _mouseDown.Y);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_drag) return;
        _drag = false;
        if (Math.Abs(e.X - _mouseDown.X) > 4 || Math.Abs(e.Y - _mouseDown.Y) > 4)
            return;
        _selectedTile = null;
        if (_drawData is null) return;
        foreach (var polygon in _drawData.Polygons.Reverse())
        {
            var points = polygon.Vertices.Select(ToScreen).ToArray();
            if (points.Length < 3) continue;
            using var path = new GraphicsPath();
            path.AddPolygon(points);
            if (!path.IsVisible(e.Location)) continue;
            _selectedTile = polygon.TileId.FineStrategicCellId.Value;
            break;
        }
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Down)
            SetFocus(_focusIndex == _map.CoarseTopology.Cells.Count ? 1 : _focusIndex + 1);
        else if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Up)
            SetFocus(_focusIndex == 1 ? _map.CoarseTopology.Cells.Count : _focusIndex - 1);
        else if (e.KeyCode == Keys.Home)
        {
            _zoom = 1f;
            _pan = new PointF();
            Invalidate();
        }
    }
}