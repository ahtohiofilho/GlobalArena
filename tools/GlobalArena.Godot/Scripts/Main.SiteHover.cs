using Godot;
using GlobalArena.Runtime;

namespace GlobalArena.Godot;

public partial class Main
{
    private PanelContainer? _r218Hud;
    private VBoxContainer? _r218Rows;
    private ulong? _r218Candidate;
    private Vector2 _r218Anchor;
    private double _r218Elapsed;
    private bool _r218ButtonHeld;
    private WorldState? _r218Rendered;
    private const double R218Delay = 0.75;
    private const float R218Width = 240f;

    private void CreateR218SiteHover()
    {
        var map = _tacticalPreview!;
        _r218Hud = new PanelContainer { Name = "GANextSiteHover", Visible = false,
            ZIndex = 20, MouseFilter = MouseFilterEnum.Stop, CustomMinimumSize = new Vector2(R218Width, 0) };
        _r218Hud.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        AddChild(_r218Hud);
        _r218Rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Pass };
        _r218Hud.AddChild(_r218Rows);
        map.R218HoverMotion += point =>
        {
            if (!R218Available()) { DismissR218SiteHover(); return; }
            if (_r218Hud.Visible && _r218Hud.GetGlobalRect().Grow(6).HasPoint(GetGlobalMousePosition())) return;
            var site = map.R218SiteAt(point);
            if (site == _r218Candidate) return;
            DismissR218SiteHover();
            _r218Candidate = site;
            _r218Anchor = map.GetGlobalTransform() * point;
        };
        map.R218CameraInteraction += DismissR218SiteHover;
    }

    private bool R218Available() => _tacticalHost?.PersistentOrdersEnabled == true &&
        _tacticalPreview?.IsVisibleInTree() == true && _tacticalPreview.IsCartographicSiteMode &&
        _cityWindowLayer?.Visible != true && _civilizationWindowLayer?.Visible != true;

    private void DismissR218SiteHover()
    {
        _r218Candidate = null;
        _r218ButtonHeld = false;
        _r218Elapsed = 0;
        _r218Rendered = null;
        if (_r218Hud is not null) _r218Hud.Visible = false;
    }

    private void UpdateR218SiteHover(double delta)
    {
        if (_r218Hud is null || _r218Candidate is not { } site) return;
        var mouse = GetGlobalMousePosition();
        var overHud = _r218Hud.Visible && _r218Hud.GetGlobalRect().Grow(6).HasPoint(mouse);
        if (!R218Available() || Input.IsMouseButtonPressed(MouseButton.Right))
        { DismissR218SiteHover(); return; }
        // The actual GUI button owns its press until release, even when
        // the pointer leaves its rectangle during the gesture.
        if (_r218ButtonHeld) return;
        if (Input.IsMouseButtonPressed(MouseButton.Left) && !overHud)
        { DismissR218SiteHover(); return; }
        // Keep the actual button alive between mouse-down and mouse-up.
        // Rebuilding its roster during a held click would also lose release.
        if (overHud && Input.IsMouseButtonPressed(MouseButton.Left)) return;
        var overSite = _tacticalPreview!.GetGlobalRect().HasPoint(mouse) &&
            _tacticalPreview.R218SiteAt(_tacticalPreview.GetGlobalTransform().AffineInverse() * mouse) == site;
        // The HUD overlaps its anchor by a few pixels, allowing direct pointer transfer.
        if (!overHud && !overSite) { DismissR218SiteHover(); return; }
        _r218Elapsed += delta;
        if (_r218Elapsed < R218Delay) return;
        if (!_r218Hud.Visible || !ReferenceEquals(_r218Rendered, _tacticalHost!.Snapshot))
            RenderR218SiteHover(site);
    }

    private void RenderR218SiteHover(ulong site)
    {
        var snapshot = _tacticalHost!.Snapshot;
        var rows = _r218Rows!;
        foreach (var child in rows.GetChildren()) { rows.RemoveChild(child); child.QueueFree(); }
        var city = snapshot.CityResidences.Cities.Any(c => c.Owner == _humanCivilization &&
            c.Tile.FineStrategicCellId.Value == site);
        rows.AddChild(new Label { Text = city ? $"CITY {site:D4}" : $"SITE {site:D4}" });
        var people = snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization &&
            w.TacticalTileId?.FineStrategicCellId.Value == site &&
            !snapshot.EdgeTravels.Pending.Any(p => p.Worker == w.Id)).OrderBy(w => w.Id.Value).ToArray();
        rows.AddChild(new Label { Text = $"{people.Length} available at node" });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, Math.Min(people.Length, 5) * 40),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        rows.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(list);
        foreach (var worker in people)
        {
            var id = worker.Id.Value;
            var row = new HBoxContainer();
            list.AddChild(row);
            var silhouette = new R218WorkerSilhouette { CustomMinimumSize = new Vector2(28, 32), MouseFilter = MouseFilterEnum.Ignore };
            row.AddChild(silhouette);
            var activity = worker.Activity == PhysicalWorkerActivity.Producing ? worker.Occupation.ToString() :
                worker.Occupation == PhysicalWorkerOccupation.Unassigned ? "Idle / assigned" : worker.Occupation.ToString();
            var button = new Button { Name = $"R218Worker{id}", Text = $"Worker #{id} | {activity}",
                SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true };
            row.AddChild(button);
            button.ButtonDown += () => _r218ButtonHeld = true;
            button.ButtonUp += () => _r218ButtonHeld = false;
            button.Pressed += () => { OnOperationalWorkerSelected(id); DismissR218SiteHover(); };
        }
        _r218Rendered = snapshot;
        _r218Hud!.ResetSize();
        var mapRect = _tacticalPreview!.GetGlobalRect();
        var extent = _r218Hud.GetCombinedMinimumSize();
        var x = Mathf.Clamp(_r218Anchor.X - 8, mapRect.Position.X,
            Math.Max(mapRect.Position.X, mapRect.End.X - extent.X));
        var y = Mathf.Clamp(_r218Anchor.Y - 8, mapRect.Position.Y,
            Math.Max(mapRect.Position.Y, mapRect.End.Y - extent.Y));
        _r218Hud.GlobalPosition = new Vector2(x, y);
        _r218Hud.Visible = true;
        // ZIndex alone only changes drawing, not GUI input traversal.
        // Place the contextual HUD after other root controls for hit testing.
        MoveChild(_r218Hud, GetChildCount() - 1);
    }
}

public partial class R218WorkerSilhouette : Control
{
    public override void _Draw()
    {
        var ink = new Color(0.84f, 0.89f, 0.93f);
        DrawCircle(new Vector2(14, 7), 4, ink);
        DrawLine(new Vector2(14, 12), new Vector2(14, 22), ink, 2, true);
        DrawLine(new Vector2(6, 17), new Vector2(22, 17), ink, 2, true);
        DrawLine(new Vector2(14, 22), new Vector2(8, 30), ink, 2, true);
        DrawLine(new Vector2(14, 22), new Vector2(20, 30), ink, 2, true);
    }
}
