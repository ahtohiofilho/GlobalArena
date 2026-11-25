using Godot;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>Presentation only. Actual projected boundary and edge endpoints;
/// one accessible button at each shared boundary, no parallel topology.</summary>
public partial class GaLocalConnectionDiagram : Control
{
    private Vector2[] _boundary = Array.Empty<Vector2>();
    private readonly List<(FoundationLocalConnection Link, Vector2 A, Vector2 B, Vector2 Neighbor, Button Button)> _links = new();
    private ulong? _hover;
    private float _extent = 1f;
    private bool _compact;
    private ulong? _selected;

    internal void UseCompactLayout()
    {
        _compact = true;
        CustomMinimumSize = new Vector2(116, 116);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    internal void SelectEdge(ulong? id)
    {
        _selected = id;
        foreach (var link in _links)
            if (_compact) link.Button.SetPressedNoSignal(link.Link.Edge.FineStrategicEdgeId.Value == id);
        QueueRedraw();
    }

    public GaLocalConnectionDiagram()
    {
        Name = "GANextConnectionDiagram";
        CustomMinimumSize = new Vector2(330, 248);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        Resized += LayoutLinks;
    }

    internal void Configure(Vector2[] boundary,
        Dictionary<ulong, (Vector2 A, Vector2 B, Vector2 Neighbor)> geometry,
        IReadOnlyList<FoundationLocalConnection> links, Action<FoundationLocalConnection> open)
    {
        if (boundary.Length is not (5 or 6) || links.Count != boundary.Length ||
            geometry.Count != links.Count || links.Any(l => !geometry.ContainsKey(l.Edge.FineStrategicEdgeId.Value)))
            throw new InvalidOperationException("Connection diagram requires exact canonical incidence.");
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        _links.Clear();
        _hover = null;
        _selected = null;
        _boundary = boundary.ToArray();
        _extent = Math.Max(boundary.Max(p => p.Length()), geometry.Values.Max(p => p.Neighbor.Length()));
        if (!float.IsFinite(_extent) || _extent <= 0f)
            throw new InvalidOperationException("Invalid connection diagram extent.");
        foreach (var link in links)
        {
            var id = link.Edge.FineStrategicEdgeId.Value;
            var (a,b,neighbor) = geometry[id];
            var button = new Button
            {
                Name = $"ConnectionEdge{id}",
                Text = _compact ? "" : "•",
                Icon = _compact ? GaHudWidgets.Icon("edge") : null,
                IconAlignment = HorizontalAlignment.Center,
                ExpandIcon = _compact,
                ToggleMode = _compact,
                TooltipText = $"Connection to node {link.Neighbor.FineStrategicCellId.Value}",
                MouseDefaultCursorShape = CursorShape.PointingHand,
                CustomMinimumSize = _compact ? new Vector2(24,24) : new Vector2(30,30)
            };
            button.MouseEntered += () => { _hover = id; QueueRedraw(); };
            button.MouseExited += () => { if (_hover == id) _hover = null; QueueRedraw(); };
            button.FocusEntered += () => { _hover = id; QueueRedraw(); };
            button.FocusExited += () => { if (_hover == id) _hover = null; QueueRedraw(); };
            button.Pressed += () => open(link);
            AddChild(button);
            _links.Add((link,a,b,neighbor,button));
        }
        LayoutLinks();
    }

    private Vector2 Map(Vector2 p) => Size * 0.5f + p * (Math.Max(1f, Math.Min(Size.X - 50, Size.Y - 48)) * 0.5f / _extent);

    private void LayoutLinks()
    {
        foreach (var link in _links)
        {
            // In the compact view put hit targets at neighbor centers, not
            // shared boundaries, to retain separation between adjacent buttons.
            var center = Map(_compact ? link.Neighbor : (link.A + link.B) * 0.5f);
            link.Button.Size = _compact ? new Vector2(24,24) : new Vector2(30,30);
            link.Button.Position = center - link.Button.Size * 0.5f;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_boundary.Length == 0) return;
        var polygon = _boundary.Select(Map).ToArray();
        DrawColoredPolygon(polygon, new Color(0.24f,0.36f,0.30f,0.85f));
        DrawPolyline(polygon.Append(polygon[0]).ToArray(), new Color(0.62f,0.72f,0.65f), 1.5f, true);
        var center = Map(Vector2.Zero);
        foreach (var link in _links)
        {
            bool hovered = _hover == link.Link.Edge.FineStrategicEdgeId.Value ||
                (_compact && _selected == link.Link.Edge.FineStrategicEdgeId.Value);
            var color = hovered ? new Color(1f,0.82f,0.42f) : new Color(0.64f,0.73f,0.68f);
            DrawLine(center, Map(link.Neighbor), color, hovered ? 3f : 1.5f, true);
            DrawLine(Map(link.A), Map(link.B), color, hovered ? 4f : 1.5f, true);
            DrawCircle(Map(link.Neighbor), 4f, color);
        }
        DrawCircle(center, 6f, new Color(0.95f,0.97f,0.90f));
        if (_compact) return; // same North-Up geometry; omit redundant compass at HUD scale
        var n = new Vector2(Size.X - 20, 28);
        DrawLine(n, n - new Vector2(0,15), Colors.White, 1.5f, true);
        DrawLine(n - new Vector2(0,15), n - new Vector2(4,9), Colors.White, 1.5f);
        DrawLine(n - new Vector2(0,15), n + new Vector2(4,-9), Colors.White, 1.5f);
        DrawString(ThemeDB.FallbackFont, n + new Vector2(-4,13), "N", fontSize: 11);
    }

    internal int BoundaryCount => _boundary.Length;
    internal IReadOnlyList<ulong> EdgeIds => _links.Select(l => l.Link.Edge.FineStrategicEdgeId.Value).ToArray();
    internal Button EdgeButton(ulong id) => _links.Single(l => l.Link.Edge.FineStrategicEdgeId.Value == id).Button;
}
