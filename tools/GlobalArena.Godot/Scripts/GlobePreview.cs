using System;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

// GA-PROD-02B: continuous canonical Goldberg surface; deterministic per-cell diagnostic colors.
// No inset, borders, duplicate topology or simulation ownership in the renderer.
public partial class GlobePreview : SubViewportContainer
{
    private Node3D? _planet;
    private Camera3D? _camera;
    private bool _dragging;
    private readonly System.Collections.Generic.List<(ulong Id, Vector3 A, Vector3 B, Vector3 C)> _pickTriangles = new();
    private Label? _pickedLabel;
    private MeshInstance3D? _surfaceInstance;
    private Label? _detailsLabel;
    private ulong? _selectedCellId;
    private readonly System.Collections.Generic.Dictionary<ulong, int> _cellDegree = new();
    private bool _movedSincePress;
    private Vector2 _pressPosition;
    private const float ClickThresholdPixels = 6f;

    private const float DragSensitivity = 0.006f;
    private const float MinZoom = 1.55f;
    private const float MaxZoom = 6.0f;

    public override void _Ready()
    {
        Position = new Vector2(660, 135);
        Size = new Vector2(550, 490);
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;

        var viewport = new SubViewport
        {
            Size = new Vector2I(550, 490),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true,
            TransparentBg = true
        };
        AddChild(viewport);
        var world = new Node3D();
        viewport.AddChild(world);

        var request = new WorldGenerationRequest(
            new WorldSeed(20261012UL),
            WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0));
        var generation = new DeterministicWorldGenerator().Generate(request);
        var geometry = generation.StrategicSphericalGeometry;
        var topology = generation.StrategicTopology;
        if (geometry.Cells.Count != topology.Cells.Count ||
            geometry.Vertices.Count != topology.Vertices.Count ||
            geometry.Cells.Count != 92 || geometry.Vertices.Count != 180)
            throw new InvalidOperationException("Authoritative globe geometry/count contract failed.");

        var surface = new SurfaceTool();
        _pickTriangles.Clear();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var triangleCount = 0;
        foreach (var cell in geometry.Cells)
        {
            var degree = cell.BoundaryVertexIds.Count;
            _cellDegree.Add(cell.CellId.Value, degree);
            if (degree != 5 && degree != 6)
                throw new InvalidOperationException("Unexpected Goldberg polygon degree.");
            var center = ToGodot(cell.Center);
            var color = DiagnosticColor(cell.CellId.Value);
            for (var index = 0; index < degree; index++)
            {
                var first = ToGodot(geometry.GetVertex(cell.BoundaryVertexIds[index]).Position);
                var second = ToGodot(geometry.GetVertex(cell.BoundaryVertexIds[(index + 1) % degree]).Position);
                _pickTriangles.Add((cell.CellId.Value, center, first, second));
                AddVertex(surface, center, color);
                AddVertex(surface, first, color);
                AddVertex(surface, second, color);
                triangleCount++;
            }
        }
        var mesh = surface.Commit();
        if (mesh is null || triangleCount != 540)
            throw new InvalidOperationException($"Canonical globe triangulation mismatch: {triangleCount}.");
        _planet = new Node3D();
        world.AddChild(_planet);
        _surfaceInstance = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Disabled,
                // Double-sided opaque triangles: depth testing, not winding, owns occlusion.
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            }
        };
        _planet.AddChild(_surfaceInstance);
        var camera = new Camera3D
        {
            Position = new Vector3(0f, 0.05f, 3.15f),
            Current = true,
            Fov = 48f
        };
        world.AddChild(camera);
        _camera = camera;
        camera.LookAt(Vector3.Zero, Vector3.Up);
        GD.Print("GA_PROD_NAV_READY_03A orbit=drag zoom=wheel");
        _pickedLabel = new Label
        {
            Text = "Territory: none  |  Left click to inspect",
            Position = new Vector2(12f, 12f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _pickedLabel.AddThemeColorOverride("font_color", new Color("#ffffff"));
        _pickedLabel.AddThemeColorOverride("font_shadow_color", new Color("#000000"));
        _pickedLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _pickedLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_pickedLabel);
        GD.Print("GA_PROD_PICK_READY_03B triangles=" + _pickTriangles.Count);
        _detailsLabel = new Label
        {
            Text = "Select a territory to view its canonical identity",
            Position = new Vector2(12f, 38f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _detailsLabel.AddThemeColorOverride("font_color", new Color("#d9e9f4"));
        AddChild(_detailsLabel);
        GD.Print("GA_PROD_TERRITORY_PANEL_READY_03C diagnostic=true");
        GD.Print($"GA_PROD_GLOBE_READY_02B_NO_BORDERS cells={geometry.Cells.Count} vertices={geometry.Vertices.Count} triangles={triangleCount} borders=0");
    }

    public override void _Process(double delta)
    {
        // GA-PROD-03A: no automatic spin; player controls the globe.
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_planet is null || _camera is null) return;
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    _dragging = true;
                    _movedSincePress = false;
                    _pressPosition = button.Position;
                }
                else
                {
                    if (_dragging && !_movedSincePress) PickTerritory(button.Position);
                    _dragging = false;
                }
                AcceptEvent();
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp)
            {
                ChangeZoom(-0.25f);
                AcceptEvent();
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown)
            {
                ChangeZoom(0.25f);
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion motion && _dragging &&
                 (motion.ButtonMask & MouseButtonMask.Left) != 0)
        {
            // Screen-space orbital dragging: axes stay aligned to camera, not planet Euler angles.
            // Compose in world space so the same mouse direction works at every longitude.
            var cameraBasis = _camera.GlobalTransform.Basis.Orthonormalized();
            var yaw = new Quaternion(cameraBasis.Y.Normalized(), motion.Relative.X * DragSensitivity);
            var pitch = new Quaternion(cameraBasis.X.Normalized(), motion.Relative.Y * DragSensitivity);
            if (!_movedSincePress && motion.Position.DistanceTo(_pressPosition) >= ClickThresholdPixels)
                _movedSincePress = true;
            if (!_movedSincePress) return;
            _planet.Quaternion = (yaw * pitch * _planet.Quaternion).Normalized();
            AcceptEvent();
        }
    }

    private void PickTerritory(Vector2 localPoint)
    {
        if (_planet is null || _camera is null || _pickedLabel is null) return;
        var origin = _camera.ProjectRayOrigin(localPoint);
        var direction = _camera.ProjectRayNormal(localPoint).Normalized();
        var transform = _planet.GlobalTransform;
        var nearest = float.PositiveInfinity;
        ulong? hitId = null;
        foreach (var triangle in _pickTriangles)
        {
            if (!RayTriangle(origin, direction, transform * triangle.A, transform * triangle.B,
                    transform * triangle.C, out var distance)) continue;
            if (distance >= nearest) continue;
            nearest = distance;
            hitId = triangle.Id;
        }
        _pickedLabel.Text = hitId.HasValue ? $"Territory ID: {hitId.Value}" : "Territory: none";
        if (hitId != _selectedCellId)
        {
            _selectedCellId = hitId;
            RefreshSelectionColors();
        }
        if (_detailsLabel is not null)
        {
            if (hitId.HasValue && _cellDegree.TryGetValue(hitId.Value, out var degree))
                _detailsLabel.Text = $"Canonical cell {hitId.Value}  |  {(degree == 5 ? "Pentagon" : "Hexagon")}  |  {degree} sides";
            else
                _detailsLabel.Text = "Select a territory to view its canonical identity";
        }
        GD.Print(hitId.HasValue ? $"GA_PROD_PICK_03B cell={hitId.Value}" : "GA_PROD_PICK_03B miss");
    }

    private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b,
        Vector3 c, out float distance)
    {
        distance = 0f;
        var edge1 = b - a;
        var edge2 = c - a;
        var cross = direction.Cross(edge2);
        var det = edge1.Dot(cross);
        if (Mathf.Abs(det) < 0.000001f) return false;
        var inverse = 1f / det;
        var fromA = origin - a;
        var u = inverse * fromA.Dot(cross);
        if (u < 0f || u > 1f) return false;
        var q = fromA.Cross(edge1);
        var v = inverse * direction.Dot(q);
        if (v < 0f || u + v > 1f) return false;
        var t = inverse * edge2.Dot(q);
        if (t <= 0.0001f) return false;
        distance = t;
        return true;
    }
    // Repaint existing canonical triangles in place; never create overlay outlines or new topology.
    private void RefreshSelectionColors()
    {
        if (_surfaceInstance is null) return;
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var t in _pickTriangles)
        {
            var color = _selectedCellId == t.Id
                ? new Color(1f, 0.95f, 0.65f)
                : DiagnosticColor(t.Id);
            AddVertex(tool, t.A, color);
            AddVertex(tool, t.B, color);
            AddVertex(tool, t.C, color);
        }
        var replacement = tool.Commit();
        if (replacement is null) throw new InvalidOperationException("Selection recolor mesh unavailable.");
        _surfaceInstance.Mesh = replacement;
    }
    private void ChangeZoom(float delta)
    {
        if (_camera is null) return;
        var position = _camera.Position;
        position.Z = Mathf.Clamp(position.Z + delta, MinZoom, MaxZoom);
        _camera.Position = position;
    }

    // Stable pseudo-random palette, determined only by canonical cell ID (no mutable RNG).
    private static Color DiagnosticColor(ulong cellId)
    {
        unchecked
        {
            var x = cellId + 0x9E3779B97F4A7C15UL;
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
            x ^= x >> 31;
            var hue = (float)((x & 0xFFFFFFUL) / 16777216.0);
            var sat = 0.45f + (float)(((x >> 24) & 255UL) / 255.0) * 0.30f;
            var val = 0.62f + (float)(((x >> 32) & 255UL) / 255.0) * 0.25f;
            return Color.FromHsv(hue, sat, val);
        }
    }

    private static void AddVertex(SurfaceTool tool, Vector3 position, Color color)
    {
        tool.SetColor(color);
        tool.AddVertex(position);
    }

    private static Vector3 ToGodot(SphericalPoint3 point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);
}
