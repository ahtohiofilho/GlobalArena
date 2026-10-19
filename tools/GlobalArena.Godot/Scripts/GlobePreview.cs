using System;
using System.Linq;
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
    private MeshInstance3D? _highResSurface; // Experimental G(12,0), never canonical ownership
    private StrategicSphericalGeometry? _finePreviewGeometry;
    private int _fineFrequency = 12;
    private int _fineLandTargetPercent = 35;
    private ulong _finePreviewSeed = 20261013UL;
    private Label? _fineCalibrationLabel;
    private Label? _detailsLabel;
    private ulong? _selectedCellId;
    // Bridge-01: emit only canonical strategic identities; experimental fine tiles are not strategic IDs.
    public event Action<ulong?>? CanonicalTerritorySelectionChanged;
    private readonly System.Collections.Generic.Dictionary<ulong, int> _cellDegree = new();
    // Read-only data sampled by canonical cell identity from WorldGenerationResult.
    private readonly System.Collections.Generic.Dictionary<ulong, (string Biome, long Habitability, long Elevation, long ResourcePotential)> _worldFacts = new();
    private bool _movedSincePress;
    private Vector2 _pressPosition;
    private const float ClickThresholdPixels = 6f;
    private int _mapMode; // 0 cells, 1 biomes, 2 habitability, 3 elevation, 4 resources, 5 experimental radial relief
    private readonly System.Collections.Generic.Dictionary<ulong, long> _experimentalElevation = new();
    private Label? _mapLegend;
    private Label? _zoomLabel;
    private OptionButton? _mapLayerSelector;
    private Button? _worldStatsButton;
    private ScrollContainer? _worldStatsScroll;

    private const float DragSensitivity = 0.006f;
    private const float MinZoom = 1.55f;
    private const float MaxZoom = 6.0f;

    public override void _Ready()
    {
        // Container layout owns the bounds; never position the map over the HUD.
        CustomMinimumSize = new Vector2(360f, 300f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;

        var viewport = new SubViewport
        {
            Size = new Vector2I(550, 490), // Stretch resizes the viewport to the containing map region.
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

        // Read-only experiment. Strategic world generation and biome authority remain unchanged.
        var sample = TacticalRadialElevationGenerator.Generate(geometry, 20261013UL);
        if (sample.Count != geometry.Cells.Count) throw new InvalidOperationException("Experimental radial sample size mismatch.");
        long lowestExperimental = long.MaxValue;
        long highestExperimental = long.MinValue;
        int dryExperimental = 0;
        int saturatedExperimental = 0;
        for (int index = 0; index < sample.Count; index++)
        {
            long elevation = sample[index];
            _experimentalElevation.Add(geometry.Cells[index].CellId.Value, elevation);
            lowestExperimental = Math.Min(lowestExperimental, elevation);
            highestExperimental = Math.Max(highestExperimental, elevation);
            if (elevation > 0L) dryExperimental++;
            if (Math.Abs(elevation) >= StrategicScalarField.Denominator) saturatedExperimental++;
        }
        GD.Print($"GA_WORLDGEN_01B_VISUAL_READY count={sample.Count} land_percent={(100.0 * dryExperimental / sample.Count):F1} min={lowestExperimental} max={highestExperimental} saturated={saturatedExperimental} seed=20261013 diagnostic_resolution=strategic");
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
        // Independent fine-grid diagnostic: same seeded field, different sample density.
        var fineRequest = new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(12, 0));
        var fineGeneration = new DeterministicWorldGenerator().Generate(fineRequest);
        var fineGeometry = fineGeneration.StrategicSphericalGeometry;
        _finePreviewGeometry = fineGeometry;
        var fineSample = TacticalRadialElevationGenerator.GenerateVaried(fineGeometry, 20261013UL);
        if (fineGeometry.Cells.Count != 1442 || fineSample.Count != 1442)
            throw new InvalidOperationException("High-res preview must contain exactly 1442 cells.");
        var fineTool = new SurfaceTool();
        fineTool.Begin(Mesh.PrimitiveType.Triangles);
        int fineTriangleCount = 0;
        int fineLandCount = 0;
        int fineSaturatedCount = 0;
        long fineMinimum = long.MaxValue;
        long fineMaximum = long.MinValue;
        for (int i = 0; i < fineGeometry.Cells.Count; i++)
        {
            var fineCell = fineGeometry.Cells[i];
            var elevation = fineSample[i];
            if (elevation > 0) fineLandCount++;
            if (Math.Abs(elevation) >= StrategicScalarField.Denominator) fineSaturatedCount++;
            fineMinimum = Math.Min(fineMinimum, elevation);
            fineMaximum = Math.Max(fineMaximum, elevation);
            var fineColor = ExperimentalReliefColor(elevation);
            var centerPoint = ToGodot(fineCell.Center);
            for (int j = 0; j < fineCell.BoundaryVertexIds.Count; j++)
            {
                var a = ToGodot(fineGeometry.GetVertex(fineCell.BoundaryVertexIds[j]).Position);
                var b = ToGodot(fineGeometry.GetVertex(fineCell.BoundaryVertexIds[(j + 1) % fineCell.BoundaryVertexIds.Count]).Position);
                AddVertex(fineTool, centerPoint, fineColor);
                AddVertex(fineTool, a, fineColor);
                AddVertex(fineTool, b, fineColor);
                fineTriangleCount++;
            }
        }
        var fineMesh = fineTool.Commit();
        if (fineMesh is null || fineTriangleCount <= 0)
            throw new InvalidOperationException("High-res experimental globe mesh was not produced.");
        _highResSurface = new MeshInstance3D
        {
            Mesh = fineMesh,
            Visible = false,
            MaterialOverride = _surfaceInstance.MaterialOverride
        };
        _planet.AddChild(_highResSurface);
        GD.Print($"GA_WORLDGEN_01B_HIGHRES_READY count={fineGeometry.Cells.Count} triangles={fineTriangleCount} land_percent={(100.0 * fineLandCount / fineSample.Count):F1} min={fineMinimum} max={fineMaximum} saturated={fineSaturatedCount} seed=20261013 mode=experimental");
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
        mapLayerSelector.AddItem("RELEVO EXP.", 5);
        mapLayerSelector.AddItem("RELEVO EXP. HD", 6);
        mapLayerSelector.AddItem("RELEVO EXP. ULTRA", 7);
        mapLayerSelector.AddItem("RELEVO INDEP. ULTRA", 8);
        mapLayerSelector.AddItem("RELEVO PESOS MED.", 9);
        mapLayerSelector.AddItem("RELEVO PESOS EQ.", 10);
        mapLayerSelector.Select(0);
        mapLayerSelector.ItemSelected += selectedIndex =>
        {
            _mapMode = mapLayerSelector.GetItemId((int)selectedIndex);
            if (_mapMode == 6 || _mapMode == 7 || _mapMode == 8 || _mapMode == 9 || _mapMode == 10)
            {
                EnsureFinePreviewResolution(_mapMode == 6 ? 12 : 48);
                RebuildCalibratedFinePreview();
            }
            RefreshSelectionColors();
            UpdateMapLegend();
        };
        AddChild(mapLayerSelector);
        _mapLayerSelector = mapLayerSelector;
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
        GD.Print("GA_PROD_LEGEND_READY_06 modes=5");
        GD.Print("GA_PROD_RESOURCE_VIEW_READY_08 modes=5 cells=" + _worldFacts.Count);
        // GA-PROD-09: optional strategic overview computed from canonical per-cell samples.
        int waterCount = 0;
        long lowestHabitability = long.MaxValue;
        long highestHabitability = long.MinValue;
        long lowestElevation = long.MaxValue;
        long highestElevation = long.MinValue;
        long lowestResources = long.MaxValue;
        long highestResources = long.MinValue;
        foreach (var facts in _worldFacts.Values)
        {
            if (facts.Biome == "Water") waterCount++;
            lowestHabitability = Math.Min(lowestHabitability, facts.Habitability);
            highestHabitability = Math.Max(highestHabitability, facts.Habitability);
            lowestElevation = Math.Min(lowestElevation, facts.Elevation);
            highestElevation = Math.Max(highestElevation, facts.Elevation);
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
        string BiomeCount(string kind)
        {
            var count = biomeCounts.TryGetValue(kind, out var present) ? present : 0;
            var percent = 100.0 * count / _worldFacts.Count;
            return kind + ": " + count + " (" + percent.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%)";
        }
        var biomeDistribution =
            "\n" + string.Join("  |  ", BiomeCount("Water"), BiomeCount("PolarIce"), BiomeCount("Tundra"))
            + "\n" + string.Join("  |  ", BiomeCount("Highland"), BiomeCount("Wetland"), BiomeCount("Desert"))
            + "\n" + string.Join("  |  ", BiomeCount("Rainforest"), BiomeCount("Forest"), BiomeCount("Grassland"));
        if (System.Linq.Enumerable.Sum(biomeCounts.Values) != _worldFacts.Count)
            throw new InvalidOperationException("Canonical biome distribution count mismatch.");
        string dominantBiome = "";
        int dominantCount = -1;
        foreach (var item in biomeCounts)
        {
            if (item.Value > dominantCount ||
                (item.Value == dominantCount && string.CompareOrdinal(item.Key, dominantBiome) < 0))
            {
                dominantBiome = item.Key;
                dominantCount = item.Value;
            }
        }
        if (dominantCount < 0) throw new InvalidOperationException("Canonical biome summary is empty.");
        var dominantShare = 100.0 * dominantCount / _worldFacts.Count;
        // Unweighted arithmetic mean across strategic cells; never imply geographic area weighting.
        long totalHabitability = 0;
        long totalElevation = 0;
        long totalResources = 0;
        foreach (var facts in _worldFacts.Values)
        {
            checked
            {
                totalHabitability += facts.Habitability;
                totalElevation += facts.Elevation;
                totalResources += facts.ResourcePotential;
            }
        }
        long landHabitabilityTotal = 0;
        long landResourceTotal = 0;
        int landCellCount = 0;
        foreach (var facts in _worldFacts.Values)
        {
            if (facts.Biome == "Water") continue;
            checked
            {
                landHabitabilityTotal += facts.Habitability;
                landResourceTotal += facts.ResourcePotential;
            }
            landCellCount++;
        }
        if (landCellCount != _worldFacts.Count - waterCount)
            throw new InvalidOperationException("Non-water cell count mismatch.");
        var landMeanHabitability = landCellCount > 0
            ? FormatMeanIndex((double)landHabitabilityTotal / landCellCount)
            : "N/A (no non-water cells)";
        var landMeanResources = landCellCount > 0
            ? FormatMeanIndex((double)landResourceTotal / landCellCount)
            : "N/A (no non-water cells)";
        var averageHabitability = (double)totalHabitability / _worldFacts.Count;
        var averageElevation = (double)totalElevation / _worldFacts.Count;
        var averageResources = (double)totalResources / _worldFacts.Count;
        var overviewLabel = new Label
        {
            CustomMinimumSize = new Vector2(500f, 360f),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            Text = $"WORLD OVERVIEW | {_worldFacts.Count} strategic cells"
                + $"\nWater: {waterCount}  |  Other biomes: {_worldFacts.Count - waterCount}"
                + biomeDistribution
                + $"\nMost common biome: {dominantBiome} ({dominantCount} cells, {dominantShare:F1}%)"
                + $"\nHabitability index: {FormatIndex(lowestHabitability)} - {FormatIndex(highestHabitability)}"
                + $"\nMean habitability: {FormatMeanIndex(averageHabitability)} (all cells)"
                + $"\nNon-water mean habitability: {landMeanHabitability} ({landCellCount} cells)"
                + $"\nRelative elevation index: {FormatIndex(lowestElevation)} - {FormatIndex(highestElevation)}"
                + $"\nMean relative elevation: {FormatMeanIndex(averageElevation)} (all cells; not meters)"
                + $"\nResource potential: {FormatIndex(lowestResources)} - {FormatIndex(highestResources)}"
                + $"\nMean resource potential: {FormatMeanIndex(averageResources)} (all cells)"
                + $"\nNon-water mean resource potential: {landMeanResources} ({landCellCount} cells)"
                + "\nIndices are procedural, not population or proven reserves"
        };
        overviewLabel.AddThemeColorOverride("font_color", new Color("#f5f7ff"));
        overviewLabel.AddThemeColorOverride("font_shadow_color", new Color("#08131e"));
        overviewLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        overviewLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        var overviewScroll = new ScrollContainer
        {
            Position = new Vector2(12f, 118f),
            Size = new Vector2(520f, 240f),
            Visible = false,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Stop
        };
        overviewScroll.AddChild(overviewLabel);
        AddChild(overviewScroll);
        _worldStatsScroll = overviewScroll;
        var overviewButton = new Button
        {
            Text = "WORLD STATS",
            Position = new Vector2(365f, 44f),
            CustomMinimumSize = new Vector2(174f, 32f),
            MouseFilter = MouseFilterEnum.Stop
        };
        overviewButton.Pressed += () =>
        {
            overviewScroll.Visible = !overviewScroll.Visible;
            if (overviewScroll.Visible) overviewScroll.ScrollVertical = 0;
            overviewButton.Text = overviewScroll.Visible ? "HIDE STATS" : "WORLD STATS";
        };
        AddChild(overviewButton);
        _worldStatsButton = overviewButton;
        // GA-PROD-12: player can explicitly clear the selected territory.
        var clearSelectionButton = new Button
        {
            Text = "CLEAR SELECTION",
            Position = new Vector2(365f, 80f),
            CustomMinimumSize = new Vector2(174f, 32f),
            MouseFilter = MouseFilterEnum.Stop
        };
        clearSelectionButton.Pressed += ClearTerritorySelection;
        AddChild(clearSelectionButton);
        GD.Print("GA_PROD_CLEAR_SELECTION_READY_12 button=true");
        // Keyboard shortcut discovery belongs to presentation only.
        var shortcutHint = new Label
        {
            Text = "1-5 MAPS | PGUP/PGDN ZOOM | ESC CLEAR | R RESET | L LEGEND | S STATS",
            Position = new Vector2(12f, 380f),
            Size = new Vector2(520f, 25f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        shortcutHint.AddThemeColorOverride("font_color", new Color("#d9e9f4"));
        AddChild(shortcutHint);
        GD.Print("GA_PROD_SHORTCUT_LAYOUT_READY_18 hint_y=380");
        GD.Print("GA_PROD_STATS_SHORTCUT_READY_17 key=S");
        GD.Print("GA_PROD_LEGEND_TOGGLE_READY_16 key=L");
        var zoomLabel = new Label
        {
            Position = new Vector2(12f, 340f),
            Size = new Vector2(210f, 24f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        zoomLabel.AddThemeColorOverride("font_color", new Color("#d9e9f4"));
        AddChild(zoomLabel);
        _zoomLabel = zoomLabel;
        UpdateZoomLabel();
        var zoomInButton = new Button
        {
            Text = "+",
            Position = new Vector2(230f, 335f),
            CustomMinimumSize = new Vector2(40f, 30f),
            MouseFilter = MouseFilterEnum.Stop
        };
        zoomInButton.Pressed += () => ChangeZoom(-0.25f);
        AddChild(zoomInButton);
        var zoomOutButton = new Button
        {
            Text = "-",
            Position = new Vector2(275f, 335f),
            CustomMinimumSize = new Vector2(40f, 30f),
            MouseFilter = MouseFilterEnum.Stop
        };
        zoomOutButton.Pressed += () => ChangeZoom(0.25f);
        AddChild(zoomOutButton);
        GD.Print("GA_PROD_ZOOM_KEYS_READY_22 keys=PageUp,PageDown");
        GD.Print("GA_PROD_ZOOM_BUTTONS_READY_21 count=2");
        GD.Print("GA_PROD_ZOOM_INDICATOR_READY_20 range=0-100");
        GD.Print("GA_PROD_VIEW_RESET_READY_15 key=R");
        GD.Print("GA_PROD_KEYBOARD_HINT_READY_14 keys=1-5,escape");
        GD.Print("GA_PROD_KEYBOARD_NAV_READY_13 keys=1-5,escape");
        GD.Print($"GA_PROD_WORLD_SUMMARY_READY_09 cells={_worldFacts.Count} water={waterCount} toggle=button");
        GD.Print($"GA_PROD_ELEVATION_STATS_READY_31 cells={_worldFacts.Count} measures=3");
        GD.Print("GA_PROD_STATS_KEYS_READY_30 keys=Home,End");
        GD.Print("GA_PROD_STATS_SCROLL_RESET_READY_29 on=open");
        GD.Print("GA_PROD_WORLD_STATS_SCROLL_READY_28 viewport=520x240");
        GD.Print($"GA_PROD_LAND_RESOURCES_READY_27 land={landCellCount} water={waterCount}");
        GD.Print($"GA_PROD_LAND_HABITABILITY_READY_26 land={landCellCount} water={waterCount}");
        GD.Print($"GA_PROD_WORLD_MEANS_READY_25 cells={_worldFacts.Count} measures=2");
        GD.Print($"GA_PROD_DOMINANT_BIOME_READY_24 cells={_worldFacts.Count} count={dominantCount}");
        GD.Print($"GA_PROD_BIOME_SHARE_READY_23 cells={_worldFacts.Count} categories={biomeCounts.Count}");
        GD.Print($"GA_PROD_BIOME_BREAKDOWN_READY_10 cells={_worldFacts.Count} categories={biomeCounts.Count}");
        GD.Print("GA_PROD_WORLD_LAYERS_READY_05 modes=5");
        GD.Print($"GA_PROD_GLOBE_READY_02B_NO_BORDERS cells={geometry.Cells.Count} vertices={geometry.Vertices.Count} triangles={triangleCount} borders=0");
    }

    public override void _Process(double delta)
    {
        // GA-PROD-03A: no automatic spin; player controls the globe.
    }

    // Accessible keyboard shortcuts: digits 1-5 choose a layer; Escape clears a selection.
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
        if (_worldStatsScroll is not null && _worldStatsScroll.Visible &&
            (key.Keycode == Key.Home || key.Keycode == Key.End))
        {
            _worldStatsScroll.ScrollVertical = key.Keycode == Key.Home
                ? 0
                : (int)_worldStatsScroll.GetVScrollBar().MaxValue;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.S && _worldStatsButton is not null)
        {
            _worldStatsButton.EmitSignal(BaseButton.SignalName.Pressed);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.L)
        {
            ToggleMapLegend();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Pageup || key.Keycode == Key.Pagedown)
        {
            ChangeZoom(key.Keycode == Key.Pageup ? -0.25f : 0.25f);
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.R)
        {
            ResetGlobeCamera();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape)
        {
            ClearTerritorySelection();
            GetViewport().SetInputAsHandled();
            return;
        }
        var targetMode = key.Keycode switch
        {
            Key.Key1 => 0,
            Key.Key2 => 1,
            Key.Key3 => 2,
            Key.Key4 => 3,
            Key.Key5 => 4,
            _ => -1
        };
        if (targetMode < 0 || _mapLayerSelector is null) return;
        _mapMode = targetMode;
        _mapLayerSelector.Select(targetMode);
        RefreshSelectionColors();
        UpdateMapLegend();
        GetViewport().SetInputAsHandled();
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
                    if (_mapMode != 6 && _dragging && !_movedSincePress) PickTerritory(button.Position);
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
            CanonicalTerritorySelectionChanged?.Invoke(hitId);
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

    private void ClearTerritorySelection()
    {
        _selectedCellId = null;
        CanonicalTerritorySelectionChanged?.Invoke(null);
        if (_pickedLabel is not null)
            _pickedLabel.Text = "Territory: none  |  Left click to inspect";
        if (_detailsLabel is not null)
            _detailsLabel.Text = "Select a territory to view its canonical identity";
        RefreshSelectionColors();
        GD.Print("GA_PROD_SELECTION_CLEARED_12");
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
    // Preview-only recalibration. Field values and canonical WorldGenerationResult are untouched.
    private void EnsureFinePreviewResolution(int frequency)
    {
        if (_fineFrequency == frequency) return;
        if (_highResSurface is null) throw new InvalidOperationException("Missing experimental mesh holder");
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var request = new WorldGenerationRequest(new WorldSeed(20261012UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(frequency, 0));
        var generated = new DeterministicWorldGenerator().Generate(request);
        var newGeometry = generated.StrategicSphericalGeometry;
        var expectedCount = 10 * frequency * frequency + 2;
        if (newGeometry.Cells.Count != expectedCount)
            throw new InvalidOperationException($"Fine topology mismatch: {newGeometry.Cells.Count} != {expectedCount}");
        _finePreviewGeometry = newGeometry;
        _fineFrequency = frequency;
        RebuildCalibratedFinePreview();
        watch.Stop();
        GD.Print($"GA_WORLDGEN_01B_ULTRA_READY frequency={frequency} cells={expectedCount} elapsed_ms={watch.ElapsedMilliseconds} seed={_finePreviewSeed}");
    }

    private void RebuildCalibratedFinePreview()
    {
        if (_finePreviewGeometry is null || _highResSurface is null) return;
        var geometry = _finePreviewGeometry;
        var generationWatch = System.Diagnostics.Stopwatch.StartNew();
        var values = (_mapMode == 9
            ? TacticalIndependentScaleGenerator.GenerateWeighted(geometry, _finePreviewSeed, 0.75, 1.50, 2.00)
            : _mapMode == 10
            ? TacticalIndependentScaleGenerator.GenerateWeighted(geometry, _finePreviewSeed, 0.50, 2.00, 3.00)
            : _mapMode == 8
            ? TacticalIndependentScaleGenerator.Generate(geometry, _finePreviewSeed)
            : TacticalRadialElevationGenerator.GenerateVaried(geometry, _finePreviewSeed)).ToArray();
        generationWatch.Stop();
        int expectedFineCells = 10 * _fineFrequency * _fineFrequency + 2;
        if (values.Length != expectedFineCells) throw new InvalidOperationException("HD calibration cell count mismatch.");
        var sorted = values.OrderBy(value => value).ToArray();
        int cutoffIndex = Math.Clamp((int)Math.Floor(values.Length * (100.0 - _fineLandTargetPercent) / 100.0), 0, values.Length - 1);
        long seaLevel = sorted[cutoffIndex];
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        int land = 0;
        int saturated = 0;
        for (int i = 0; i < geometry.Cells.Count; i++)
        {
            var cell = geometry.Cells[i];
            long height = values[i];
            if (height > seaLevel) land++;
            if (Math.Abs(height) >= StrategicScalarField.Denominator) saturated++;
            var color = ExperimentalReliefColor(height - seaLevel);
            var center = ToGodot(cell.Center);
            for (int j = 0; j < cell.BoundaryVertexIds.Count; j++)
            {
                var a = ToGodot(geometry.GetVertex(cell.BoundaryVertexIds[j]).Position);
                var b = ToGodot(geometry.GetVertex(cell.BoundaryVertexIds[(j + 1) % cell.BoundaryVertexIds.Count]).Position);
                AddVertex(surface, center, color);
                AddVertex(surface, a, color);
                AddVertex(surface, b, color);
            }
        }
        var mesh = surface.Commit();
        if (mesh is null) throw new InvalidOperationException("Calibration mesh could not be generated.");
        _highResSurface.Mesh = mesh;
        var topology = GoldbergStrategicTopologyGenerator.Generate(new GoldbergParameters(_fineFrequency, 0));
        double deltaSum = 0;
        long maxDelta = 0;
        int edgeCount = 0;
        for (int i = 0; i < topology.Cells.Count; i++)
        {
            foreach (var adjacent in topology.Cells[i].AdjacentCellIds)
            {
                int j = checked((int)adjacent.Value - 1);
                if (j <= i) continue;
                long delta = Math.Abs(values[i] - values[j]);
                deltaSum += delta;
                maxDelta = Math.Max(maxDelta, delta);
                edgeCount++;
            }
        }
        var visited = new bool[values.Length];
        int landComponents = 0;
        int largestComponent = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (visited[i] || values[i] <= seaLevel) continue;
            landComponents++;
            int size = 0;
            var queue = new System.Collections.Generic.Queue<int>();
            queue.Enqueue(i);
            visited[i] = true;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                size++;
                foreach (var adjacent in topology.Cells[current].AdjacentCellIds)
                {
                    int j = checked((int)adjacent.Value - 1);
                    if (visited[j] || values[j] <= seaLevel) continue;
                    visited[j] = true;
                    queue.Enqueue(j);
                }
            }
            largestComponent = Math.Max(largestComponent, size);
        }
        double actualLand = 100.0 * land / values.Length;
        if (_fineCalibrationLabel is not null)
        {
            _fineCalibrationLabel.Text = $"G({_fineFrequency},0) | HD SEED {_finePreviewSeed} | CENTERS {TacticalRadialElevationGenerator.GetVariedDeformationCount(_finePreviewSeed)} | TARGET {_fineLandTargetPercent}%\nLAND {actualLand:F1}% | CONTINENTS {landComponents} | LARGEST {largestComponent}\nNEIGHBOR MEAN {deltaSum / edgeCount:F0} | MAX {maxDelta} | SAT {saturated}";
        }
        GD.Print($"GA_WORLDGEN_01B_DIVERSITY_MODEL mode={_mapMode} seed={_finePreviewSeed} target={_fineLandTargetPercent} generation_ms={generationWatch.ElapsedMilliseconds}");
        GD.Print($"GA_WORLDGEN_01B_CALIBRATION_READY centers={TacticalRadialElevationGenerator.GetVariedDeformationCount(_finePreviewSeed)} seed={_finePreviewSeed} target={_fineLandTargetPercent} actual={actualLand:F2} sea={seaLevel} components={landComponents} largest={largestComponent} mean_delta={deltaSum / edgeCount:F1} max_delta={maxDelta} saturated={saturated} count={values.Length} generation_ms={generationWatch.ElapsedMilliseconds}");
    }

    private void RefreshSelectionColors()
    {
        if (_surfaceInstance is null) return;
        bool fineView = _mapMode == 6 || _mapMode == 7 || _mapMode == 8 || _mapMode == 9 || _mapMode == 10;
        _surfaceInstance.Visible = !fineView;
        if (_highResSurface is not null) _highResSurface.Visible = fineView;
        if (fineView)
        {
            _selectedCellId = null;
            if (_pickedLabel is not null) _pickedLabel.Text = "HD experimental relief | picking disabled";
            if (_detailsLabel is not null) _detailsLabel.Text = $"{10 * _fineFrequency * _fineFrequency + 2} fine-grid samples | not canonical territory";
            return;
        }
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
    private void ResetGlobeCamera()
    {
        if (_planet is null || _camera is null) return;
        _dragging = false;
        _movedSincePress = false;
        _planet.Quaternion = Quaternion.Identity;
        _camera.Position = new Vector3(0f, 0.05f, 3.15f);
        _camera.LookAt(Vector3.Zero, Vector3.Up);
        UpdateZoomLabel();
        GD.Print("GA_PROD_VIEW_RESET_15");
    }

    private void ChangeZoom(float delta)
    {
        if (_camera is null) return;
        var position = _camera.Position;
        position.Z = Mathf.Clamp(position.Z + delta, MinZoom, MaxZoom);
        _camera.Position = position;
        UpdateZoomLabel();
    }

    // Shows relative proximity within the configured camera range, not a physical scale.
    private void UpdateZoomLabel()
    {
        if (_camera is null || _zoomLabel is null) return;
        var closeness = Mathf.Clamp((MaxZoom - _camera.Position.Z) / (MaxZoom - MinZoom), 0f, 1f);
        _zoomLabel.Text = $"ZOOM: {Mathf.RoundToInt(closeness * 100f)}% (relative)";
    }

    // Read-only percentage display; underlying deterministic raw field remains unchanged.
    private static string FormatMeanIndex(double meanRaw)
    {
        return (meanRaw * 100.0 / StrategicScalarField.Denominator)
            .ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
    private static string FormatIndex(long raw)
    {
        var percent = (double)raw * 100.0 / StrategicScalarField.Denominator;
        return percent.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + "%";
    }
    private void ToggleMapLegend()
    {
        if (_mapLegend is null) return;
        _mapLegend.Visible = !_mapLegend.Visible;
        GD.Print("GA_PROD_LEGEND_TOGGLED_16 visible=" + _mapLegend.Visible);
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
            5 => "RELEVO EXP. | Blue: below datum  Green: lowland  Brown: elevated\nRadial prototype sampled at strategic preview resolution; NOT canonical terrain",
            6 => "RELEVO EXP. HD | G(12,0): 1442 cells vs G(3,0): 92\nSame seed & radial field; experimental only; territory picking disabled",
            7 => "RELEVO EXP. ULTRA | G(48,0): 23042 cells\nHierarchical baseline; experimental only; picking disabled",
            8 => "RELEVO INDEP. ULTRA | G(48,0): 23042 cells\nIndependent macro/medium/micro scales; same seed & sea-level target",
            9 => "RELEVO PESOS MED. | G(48,0)\nMacro 0.75, medium 1.50, micro 2.00; experimental",
            10 => "RELEVO PESOS EQ. | G(48,0)\nMacro 0.50, medium 2.00, micro 3.00; experimental",
            _ => "CELL COLORS | Deterministic colors distinguish adjacent territories\nColors do not imply ownership, biome, or political borders"
        };
    }
    // Diagnostic mode remains available; biome mode uses only canonical generated facts.
    private static Color ExperimentalReliefColor(long elevation)
    {
        var ratio = Mathf.Clamp((float)elevation / StrategicScalarField.Denominator, -1f, 1f);
        if (ratio < 0f) return new Color(0.04f, 0.18f + 0.28f * (1f + ratio), 0.42f + 0.38f * (1f + ratio));
        if (ratio < 0.35f) return new Color(0.23f + ratio * 0.5f, 0.52f - ratio * 0.22f, 0.23f);
        return new Color(0.45f + 0.32f * ratio, 0.34f + 0.26f * ratio, 0.24f + 0.25f * ratio);
    }

    private Color PresentationColor(ulong cellId)
    {
        if (!_worldFacts.TryGetValue(cellId, out var facts) || _mapMode == 0)
            return DiagnosticColor(cellId);
        if (_mapMode == 5 && _experimentalElevation.TryGetValue(cellId, out var experimental))
        {
            var ratio = Mathf.Clamp((float)experimental / StrategicScalarField.Denominator, -1f, 1f);
            if (ratio < 0f) return new Color(0.04f, 0.18f + 0.28f * (1f + ratio), 0.42f + 0.38f * (1f + ratio));
            if (ratio < 0.35f) return new Color(0.23f + ratio * 0.5f, 0.52f - ratio * 0.22f, 0.23f);
            return new Color(0.45f + 0.32f * ratio, 0.34f + 0.26f * ratio, 0.24f + 0.25f * ratio);
        }
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

    // UI foundation: controls belong to the context panel instead of overlapping the map.
    public void TransferHud(VBoxContainer sidebar)
    {
        foreach (var child in GetChildren())
        {
            if (child is not Control control) continue;
            control.Reparent(sidebar);
            control.Position = Vector2.Zero;
            control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (control is Label label)
            {
                label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                label.CustomMinimumSize = Vector2.Zero;
            }
            if (control is ScrollContainer stats)
            {
                stats.CustomMinimumSize = new Vector2(0f, 230f);
                stats.SizeFlagsVertical = SizeFlags.ExpandFill;
            }
            if (control is Button or OptionButton)
                control.CustomMinimumSize = new Vector2(0f, 32f);
        }
        var heading = new Label { Text = "HD CALIBRATION | experimental only" };
        heading.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        sidebar.AddChild(heading);
        _fineCalibrationLabel = new Label();
        _fineCalibrationLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        sidebar.AddChild(_fineCalibrationLabel);
        var coverage = new HBoxContainer();
        var minus = new Button { Text = "LAND -5%" };
        var plus = new Button { Text = "LAND +5%" };
        minus.Pressed += () => { _fineLandTargetPercent = Math.Max(25, _fineLandTargetPercent - 5); RebuildCalibratedFinePreview(); };
        plus.Pressed += () => { _fineLandTargetPercent = Math.Min(45, _fineLandTargetPercent + 5); RebuildCalibratedFinePreview(); };
        coverage.AddChild(minus);
        coverage.AddChild(plus);
        sidebar.AddChild(coverage);
        var nextSeed = new Button { Text = "NEXT SEED" };
        nextSeed.Pressed += () => { _finePreviewSeed++; RebuildCalibratedFinePreview(); };
        sidebar.AddChild(nextSeed);
        RebuildCalibratedFinePreview();
        GD.Print("GA_UI_HUD_TRANSFER_READY_01");
        GD.Print("GA_UI_VIEWPORT_READY_02 hud=sidebar map=retained");
    }
}
