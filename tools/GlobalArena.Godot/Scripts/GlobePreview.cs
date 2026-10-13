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
    // Read-only data sampled by canonical cell identity from WorldGenerationResult.
    private readonly System.Collections.Generic.Dictionary<ulong, (string Biome, long Habitability, long Elevation, long ResourcePotential)> _worldFacts = new();
    private bool _movedSincePress;
    private Vector2 _pressPosition;
    private const float ClickThresholdPixels = 6f;
    private int _mapMode; // 0 cells, 1 biomes, 2 habitability, 3 elevation, 4 resources
    private Label? _mapLegend;

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
            _worldFacts.Add(cell.CellId.Value, (
                generation.StrategicBiomes.GetKind(cell.CellId).ToString(),
                generation.StrategicHabitability.GetRawValue(cell.CellId),
                generation.StrategicPhysicalFields.Elevation.GetRawValue(cell.CellId),
                generation.StrategicResourcePotentialFields.GeneralPotential.GetRawValue(cell.CellId)));
            if (degree != 5 && degree != 6)
                throw new InvalidOperationException("Unexpected Goldberg polygon degree.");
            var center = ToGodot(cell.Center);
            var color = PresentationColor(cell.CellId.Value);
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
        GD.Print("GA_PROD_WORLD_DATA_READY_04 cells=" + _worldFacts.Count);
        // GA-PROD-11: direct selection; no forced cycling through unrelated layers.
        var mapLayerSelector = new OptionButton
        {
            Position = new Vector2(365f, 8f),
            CustomMinimumSize = new Vector2(174f, 32f),
            MouseFilter = MouseFilterEnum.Stop
        };
        mapLayerSelector.AddItem("CELL COLORS", 0);
        mapLayerSelector.AddItem("BIOMES", 1);
        mapLayerSelector.AddItem("HABITABILITY", 2);
        mapLayerSelector.AddItem("ELEVATION", 3);
        mapLayerSelector.AddItem("RESOURCES", 4);
        mapLayerSelector.Select(0);
        mapLayerSelector.ItemSelected += selectedIndex =>
        {
            _mapMode = mapLayerSelector.GetItemId((int)selectedIndex);
            RefreshSelectionColors();
            UpdateMapLegend();
        };
        AddChild(mapLayerSelector);
        GD.Print("GA_PROD_MAP_SELECTOR_READY_11 options=5 initial=0");
        _mapLegend = new Label
        {
            Position = new Vector2(12f, 410f),
            Size = new Vector2(520f, 72f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _mapLegend.AddThemeColorOverride("font_color", new Color("#e2edf6"));
        _mapLegend.AddThemeColorOverride("font_shadow_color", new Color("#08131e"));
        _mapLegend.AddThemeConstantOverride("shadow_offset_x", 1);
        _mapLegend.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(_mapLegend);
        UpdateMapLegend();
        GD.Print("GA_PROD_LEGEND_READY_06 modes=4");
        GD.Print("GA_PROD_RESOURCE_VIEW_READY_08 modes=5 cells=" + _worldFacts.Count);
        // GA-PROD-09: optional strategic overview computed from canonical per-cell samples.
        int waterCount = 0;
        long lowestHabitability = long.MaxValue;
        long highestHabitability = long.MinValue;
        long lowestResources = long.MaxValue;
        long highestResources = long.MinValue;
        foreach (var facts in _worldFacts.Values)
        {
            if (facts.Biome == "Water") waterCount++;
            lowestHabitability = Math.Min(lowestHabitability, facts.Habitability);
            highestHabitability = Math.Max(highestHabitability, facts.Habitability);
            lowestResources = Math.Min(lowestResources, facts.ResourcePotential);
            highestResources = Math.Max(highestResources, facts.ResourcePotential);
        }
        // GA-PROD-10: count canonical biome labels; never synthesize a second biome model.
        var biomeCounts = new System.Collections.Generic.Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var facts in _worldFacts.Values)
        {
            if (!biomeCounts.TryAdd(facts.Biome, 1))
                biomeCounts[facts.Biome]++;
        }
        string BiomeCount(string kind) => kind + ": " +
            (biomeCounts.TryGetValue(kind, out var count) ? count : 0);
        var biomeDistribution =
            "\n" + string.Join("  |  ", BiomeCount("Water"), BiomeCount("PolarIce"), BiomeCount("Tundra"))
            + "\n" + string.Join("  |  ", BiomeCount("Highland"), BiomeCount("Wetland"), BiomeCount("Desert"))
            + "\n" + string.Join("  |  ", BiomeCount("Rainforest"), BiomeCount("Forest"), BiomeCount("Grassland"));
        if (System.Linq.Enumerable.Sum(biomeCounts.Values) != _worldFacts.Count)
            throw new InvalidOperationException("Canonical biome distribution count mismatch.");
        var overviewLabel = new Label
        {
            Position = new Vector2(12f, 118f),
            Size = new Vector2(520f, 240f),
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            Text = $"WORLD OVERVIEW | {_worldFacts.Count} strategic cells"
                + $"\nWater: {waterCount}  |  Other biomes: {_worldFacts.Count - waterCount}"
                + biomeDistribution
                + $"\nHabitability index: {FormatIndex(lowestHabitability)} - {FormatIndex(highestHabitability)}"
                + $"\nResource potential: {FormatIndex(lowestResources)} - {FormatIndex(highestResources)}"
                + "\nIndices are procedural, not population or proven reserves"
        };
        overviewLabel.AddThemeColorOverride("font_color", new Color("#f5f7ff"));
        overviewLabel.AddThemeColorOverride("font_shadow_color", new Color("#08131e"));
        overviewLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        overviewLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        AddChild(overviewLabel);
        var overviewButton = new Button
        {
            Text = "WORLD STATS",
            Position = new Vector2(365f, 44f),
            CustomMinimumSize = new Vector2(174f, 32f),
            MouseFilter = MouseFilterEnum.Stop
        };
        overviewButton.Pressed += () =>
        {
            overviewLabel.Visible = !overviewLabel.Visible;
            overviewButton.Text = overviewLabel.Visible ? "HIDE STATS" : "WORLD STATS";
        };
        AddChild(overviewButton);
        GD.Print($"GA_PROD_WORLD_SUMMARY_READY_09 cells={_worldFacts.Count} water={waterCount} toggle=button");
        GD.Print($"GA_PROD_BIOME_BREAKDOWN_READY_10 cells={_worldFacts.Count} categories={biomeCounts.Count}");
        GD.Print("GA_PROD_WORLD_LAYERS_READY_05 modes=4");
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
                _detailsLabel.Text = $"Canonical cell {hitId.Value}  |  {(degree == 5 ? "Pentagon" : "Hexagon")}  |  {degree} sides"
                    + (_worldFacts.TryGetValue(hitId.Value, out var facts)
                        ? $"\nBiome: {facts.Biome}  |  Habitability: {FormatIndex(facts.Habitability)}"
                          + $"\nRelative elevation index: {FormatIndex(facts.Elevation)} (not meters)"
                          + $"\nGeneral resource potential: {FormatIndex(facts.ResourcePotential)} (not reserves)"
                        : "\nWorld data unavailable");
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
                : PresentationColor(t.Id);
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

    // Read-only percentage display; underlying deterministic raw field remains unchanged.
    private static string FormatIndex(long raw)
    {
        var percent = (double)raw * 100.0 / StrategicScalarField.Denominator;
        return percent.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
    private void UpdateMapLegend()
    {
        if (_mapLegend is null) return;
        _mapLegend.Text = _mapMode switch
        {
            1 => "BIOMES | Blue: Water  White: Ice  Gray: Highland\nGreen: Forest/Wetland  Yellow: Desert/Grassland",
            2 => "HABITABILITY | Dark: lower  Light: higher\nRelative index 0-100%; not a population estimate",
            3 => "ELEVATION | Dark: lower  Light: higher\nRelative procedural index 0-100%; not meters",
            4 => "RESOURCES | Dark: lower  Light: higher\nGeneral potential index 0-100%; not confirmed deposits",
            _ => "CELL COLORS | Deterministic colors distinguish adjacent territories\nColors do not imply ownership, biome, or political borders"
        };
    }
    // Diagnostic mode remains available; biome mode uses only canonical generated facts.
    private Color PresentationColor(ulong cellId)
    {
        if (!_worldFacts.TryGetValue(cellId, out var facts) || _mapMode == 0)
            return DiagnosticColor(cellId);
        if (_mapMode == 2)
        {
            var normalized = Mathf.Clamp((float)facts.Habitability / StrategicScalarField.Denominator, 0f, 1f);
            return new Color(0.15f + 0.75f * normalized, 0.24f + 0.59f * normalized, 0.44f - 0.22f * normalized);
        }
        if (_mapMode == 3)
        {
            var normalized = Mathf.Clamp((float)facts.Elevation / StrategicScalarField.Denominator, 0f, 1f);
            return new Color(0.12f + 0.76f * normalized, 0.26f + 0.65f * normalized, 0.40f + 0.48f * normalized);
        }
        if (_mapMode == 4)
        {
            var normalized = Mathf.Clamp((float)facts.ResourcePotential / StrategicScalarField.Denominator, 0f, 1f);
            return new Color(0.20f + 0.70f * normalized, 0.18f + 0.46f * normalized, 0.32f - 0.15f * normalized);
        }
        return facts.Biome switch
        {
            "Water" => new Color("#306fba"),
            "PolarIce" => new Color("#dbeafa"),
            "Tundra" => new Color("#aebca7"),
            "Highland" => new Color("#8c8175"),
            "Wetland" => new Color("#538b77"),
            "Desert" => new Color("#d7b679"),
            "Rainforest" => new Color("#206b46"),
            "Forest" => new Color("#43834a"),
            "Grassland" => new Color("#9db963"),
            _ => DiagnosticColor(cellId)
        };
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
