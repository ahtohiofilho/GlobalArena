using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

// GA-UI-TACTICAL-LOCAL-01: renderer-only local view of the canonical scale-6 physical incidence map.
// No second topology, new physical identity, game orders, or mutable simulation state.
public partial class TacticalContextPreview : SubViewportContainer
{
    private SubViewport? _innerViewport;
    private Node3D? _world;
    private Camera3D? _camera;
    private MeshInstance3D? _surface;
    private MeshInstance3D? _edges;
    private MeshInstance3D? _selectedTileOutline;
    private Label? _information;
    private Label? _selection;
    private Label? _terrainDetails;
    private WorldGenerationV2PhysicalComposition? _terrainReference;
    private bool _showExperimentalTerrain = true;
    private const ulong TerrainExperimentSeed = 20261012UL;
    private ulong? _selectedFineTileId;
    // R30: presentation-only notification to the existing right sidebar.
    public event Action<ulong?, string, string>? PhysicalTileInspected;
    private readonly List<(ulong Id, Vector2[] Polygon, ulong[] Owners, int Degree, int Ring)> _polygons = new();
    // Framing geometry and resident geometry have separate scopes.
    private readonly List<Vector2[]> _framingPolygons = new();
    private const float InitialZoomToRing2Cover = 0.55f;
    private int _residentRingCount = 3;
    private Vector3 _basisEast;
    private Vector3 _basisNorth;
    private Vector2 _dragStart;
    private bool _pressed;
    private bool _moved;
    private float _originalSize = 2f;
    // EXPERIMENTAL CAMERA FRAMING: COVER fills the sampled viewport without changing the mesh.
    // FIT retains the existing whole-neighborhood framing for side-by-side human comparison.
    private bool _coverViewport = true;
    private float _coverSize = 2f;
    // EXPERIMENTAL PRESENTATION ONLY: accepted physical identities stay unchanged.
    private int _ringCount = 2;
    private bool _equidistant = true;
    private float _yawDegrees;
    private ulong _activeFocus;
    private Vector3 _surfaceNormal;
    private Vector3 _cameraTarget;
    // R23: projection-independent canonical prefetch, never a rendered chart or a camera owner.
    private PhysicalTacticalIncidenceMap? _cachedPhysicalMap;
    // R35: expose the SAME authoritative map used by rendering, no duplicate identity model.
    public PhysicalTacticalIncidenceMap? CanonicalPhysicalIncidenceMap => _cachedPhysicalMap;
    public WorldGenerationResult? PresentedCoarseWorld { get; private set; }
    public ulong? SelectedCanonicalPhysicalTileId => _selectedFineTileId;
    private readonly Dictionary<ulong, PhysicalTacticalResidentTransferPlan> _preparedTransfers = new();
    private readonly Dictionary<ulong, SphericalPoint3[]> _preparedCanonicalTileGeometry = new();
    // R25: pan intent is advisory; it selects existing canonical prefetch plans only.
    private StrategicSphericalGeometry? _cachedCoarseGeometry;
    private Vector3 _prefetchIntentOrigin;
    private ulong _lastPrefetchIntentFocus;
    // R29 MVP gate: preserve R27 implementation for post-MVP, disable visual expansion.
    private static readonly bool EnablePostMvpResidentExpansion = false;
    // R27: additions are rendered in the ORIGINAL projection; the base chart never moves.
    private MeshInstance3D? _extensionSurface;
    private MeshInstance3D? _extensionEdges;
    private readonly HashSet<ulong> _renderedPhysicalTileIds = new();
    private readonly SortedDictionary<ulong, (Vector3[] Points, Color Tint, ulong[] Owners, Vector2[] Polygon)> _extendedPhysicalTiles = new();


    private static Vector3 ConvertPoint(SphericalPoint3 point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);

    // Prepare only the *new* canonical data required by an adjacent focus.
    // No projection, scene mutation, eviction, camera movement or visible chart switch.
    private void PrepareAdjacentCanonicalData(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId focus,
        StrategicSphericalGeometry geometry,
        IReadOnlyList<PhysicalTacticalTileId> sourceResident)
    {
        _preparedTransfers.Clear();
        _preparedCanonicalTileGeometry.Clear();
        var sourceCell = map.CoarseTopology.Cells[checked((int)focus.Value - 1)];
        var residentSet = sourceResident.ToHashSet();
        int maximumAdded = 0;
        foreach (var neighbor in sourceCell.AdjacentCellIds.OrderBy(id => id.Value))
        {
            var plan = PhysicalTacticalResidentTransferPlanner.PlanAdjacent(
                map, focus, neighbor, _residentRingCount);
            if (!plan.SourceResident.SequenceEqual(sourceResident) ||
                plan.Reused.Count + plan.ToPrepare.Count != plan.DestinationResident.Count)
                throw new InvalidOperationException("Canonical prefetch transfer partition mismatch.");
            _preparedTransfers.Add(neighbor.Value, plan);
            maximumAdded = Math.Max(maximumAdded, plan.ToPrepare.Count);
            foreach (var tile in plan.ToPrepare)
            {
                if (residentSet.Contains(tile))
                    throw new InvalidOperationException("Canonical prefetch must exclude resident tiles.");
                var id = tile.FineStrategicCellId.Value;
                if (_preparedCanonicalTileGeometry.ContainsKey(id)) continue;
                var cell = geometry.GetCell(tile.FineStrategicCellId);
                var unprojected = new SphericalPoint3[cell.BoundaryVertexIds.Count + 1];
                unprojected[0] = cell.Center;
                for (int index = 0; index < cell.BoundaryVertexIds.Count; index++)
                    unprojected[index + 1] = geometry.GetVertex(cell.BoundaryVertexIds[index]).Position;
                _preparedCanonicalTileGeometry.Add(id, unprojected);
            }
        }
        if (_preparedTransfers.Count != sourceCell.AdjacentCellIds.Count ||
            _preparedCanonicalTileGeometry.Count > map.FineTopology.Cells.Count)
            throw new InvalidOperationException("Canonical prefetch coverage invariant failed.");
        GD.Print($"GA_TACTICAL_PREFETCH_READY_R23 focus={focus.Value} adjacent={_preparedTransfers.Count} prepared_unique={_preparedCanonicalTileGeometry.Count} max_new_per_transition={maximumAdded} projected=false chart_swapped=false camera_changed=false");
    }


    // R25: choose the prepared neighboring focus in the player's actual pan direction.
    // No mutation to the camera, projected mesh, topology or selected physical tile.
    private void ReportPreparedPrefetchIntent()
    {
        if (_camera is null || _cachedCoarseGeometry is null ||
            _cachedPhysicalMap is null || _activeFocus == 0UL) return;
        var travel = new Vector2(_cameraTarget.X - _prefetchIntentOrigin.X,
            _cameraTarget.Z - _prefetchIntentOrigin.Z);
        if (travel.Length() < Math.Max(0.03f, _camera.Size * 0.20f))
        {
            _lastPrefetchIntentFocus = 0UL;
            return;
        }
        var active = new StrategicCellId(_activeFocus);
        var reference = Flatten(_cachedCoarseGeometry.GetCell(active).Center);
        float bestAlignment = 0.50f;
        ulong bestFocus = 0UL;
        foreach (var neighbor in _cachedPhysicalMap.CoarseTopology.Cells[
            checked((int)_activeFocus - 1)].AdjacentCellIds.OrderBy(id => id.Value))
        {
            if (!_preparedTransfers.ContainsKey(neighbor.Value))
                throw new InvalidOperationException("Missing prepared canonical neighbor plan.");
            var projected = Flatten(_cachedCoarseGeometry.GetCell(neighbor).Center);
            var toward = new Vector2(projected.X - reference.X,
                projected.Z - reference.Z);
            if (toward.LengthSquared() <= 1e-10f) continue;
            var alignment = travel.Normalized().Dot(toward.Normalized());
            if (alignment <= bestAlignment) continue;
            bestAlignment = alignment;
            bestFocus = neighbor.Value;
        }
        if (bestFocus == 0UL)
        {
            _lastPrefetchIntentFocus = 0UL;
            return;
        }
        if (_lastPrefetchIntentFocus == bestFocus) return;
        _lastPrefetchIntentFocus = bestFocus;
        var plan = _preparedTransfers[bestFocus];
        if (plan.ToPrepare.Any(id => !_preparedCanonicalTileGeometry.ContainsKey(
            id.FineStrategicCellId.Value)))
            throw new InvalidOperationException("Pan intent selected an incomplete prefetch plan.");
        GD.Print($"GA_TACTICAL_PREFETCH_INTENT_R25 source={_activeFocus} next={bestFocus} " +
            $"reused={plan.Reused.Count} new={plan.ToPrepare.Count} " +
            $"alignment={bestAlignment:F3} camera_size={_camera.Size:F4} yaw_deg={_yawDegrees:F0} " +
            "chart_swapped=false camera_changed=false");
        if (EnablePostMvpResidentExpansion)
            ExpandResidentInCurrentProjection(bestFocus);
    }

    // R27: one-chart expansion only. Never modify the old surface, existing positions,
    // world state, camera, or physical tile identities. Tiles whose ENTIRE boundary
    // is beyond the original projection horizon stay canonical-only for a later rebase.
    private void ExpandResidentInCurrentProjection(ulong destinationFocus)
    {
        if (_camera is null || _extensionSurface is null || _extensionEdges is null ||
            _cachedPhysicalMap is null || !_preparedTransfers.TryGetValue(destinationFocus, out var plan))
            return;
        var originalPosition = _camera.Position;
        var originalTarget = _cameraTarget;
        var originalSize = _camera.Size;
        var originalYaw = _yawDegrees;
        int added = 0;
        int horizonRejected = 0;
        foreach (var tile in plan.ToPrepare)
        {
            var id = tile.FineStrategicCellId.Value;
            if (_renderedPhysicalTileIds.Contains(id)) continue;
            if (!_preparedCanonicalTileGeometry.TryGetValue(id, out var canonical))
                throw new InvalidOperationException("R27 missing canonical prefetch geometry.");
            // No partial polygon: rejecting all its vertices prevents cracked boundary triangles.
            if (_equidistant && canonical.Any(point =>
                ConvertPoint(point).Dot(_surfaceNormal) <= 0.00001f))
            {
                horizonRejected++;
                continue;
            }
            var points = canonical.Select(point => Flatten(point)).ToArray();
            if (points.Length < 6 || points.Length > 7)
                throw new InvalidOperationException("R27 unexpected physical polygon degree.");
            var polygon = points.Skip(1).Select(p => new Vector2(p.X, p.Z)).ToArray();
            var owners = _cachedPhysicalMap.TileIncidences[checked((int)id - 1)]
                .IncidentCoarseCellIds.OrderBy(owner => owner.Value).Select(owner => owner.Value).ToArray();
            if (owners.Length < 1 || owners.Length > 3)
                throw new InvalidOperationException("R27 invalid physical incidence.");
            Color tint;
            if (_showExperimentalTerrain)
            {
                if (_terrainReference is null)
                    throw new InvalidOperationException("R27 terrain data missing.");
                tint = TerrainColor(_terrainReference.Biomes[checked((int)id - 1)].VisibleBiome);
            }
            else
            {
                tint = owners.Contains(_activeFocus)
                    ? new Color("#4aa6b2")
                    : Color.FromHsv(((int)owners[0] % 11) / 11f, 0.27f, 0.70f);
            }
            _extendedPhysicalTiles.Add(id, (points, tint, owners, polygon));
            _renderedPhysicalTileIds.Add(id);
            _polygons.Add((id, polygon, owners, polygon.Length, _residentRingCount + 1));
            added++;
        }
        if (added > 0)
        {
            // Rebuild ONLY the small additive overlay, never the base three-ring chart.
            var faces = new SurfaceTool();
            faces.Begin(Mesh.PrimitiveType.Triangles);
            var lines = new SurfaceTool();
            lines.Begin(Mesh.PrimitiveType.Lines);
            var lineColor = new Color("#203949");
            foreach (var entry in _extendedPhysicalTiles.Values)
            {
                for (int j = 1; j < entry.Points.Length; j++)
                {
                    var next = j == entry.Points.Length - 1 ? 1 : j + 1;
                    AddColored(faces, entry.Points[0], entry.Tint);
                    AddColored(faces, entry.Points[j], entry.Tint);
                    AddColored(faces, entry.Points[next], entry.Tint);
                    AddColored(lines, entry.Points[j] + Vector3.Up * 0.006f, lineColor);
                    AddColored(lines, entry.Points[next] + Vector3.Up * 0.006f, lineColor);
                }
            }
            var faceMesh = faces.Commit();
            var edgeMesh = lines.Commit();
            faces.Dispose();
            lines.Dispose();
            if (faceMesh is null || edgeMesh is null)
                throw new InvalidOperationException("R27 additive mesh generation failed.");
            var previousFaceMesh = _extensionSurface.Mesh;
            var previousEdgeMesh = _extensionEdges.Mesh;
            _extensionSurface.Mesh = faceMesh;
            _extensionEdges.Mesh = edgeMesh;
            previousFaceMesh?.Dispose();
            previousEdgeMesh?.Dispose();
            _extensionEdges.Visible = _edges?.Visible ?? false;
        }
        if (_camera.Position != originalPosition || _cameraTarget != originalTarget ||
            _camera.Size != originalSize || _yawDegrees != originalYaw)
            throw new InvalidOperationException("R27 must never modify camera state.");
        GD.Print($"GA_TACTICAL_RESIDENT_EXTENSION_R27 source={_activeFocus} next={destinationFocus} " +
            $"prepared={plan.ToPrepare.Count} added={added} rejected_horizon={horizonRejected} " +
            $"rendered_total={_renderedPhysicalTileIds.Count} extension_total={_extendedPhysicalTiles.Count} " +
            "base_chart_unchanged=true camera_changed=false rebase=false");
    }

    private void ResetExtendedResident()
    {
        _extendedPhysicalTiles.Clear();
        _renderedPhysicalTileIds.Clear();
        if (_extensionSurface is not null)
        {
            var old = _extensionSurface.Mesh;
            _extensionSurface.Mesh = null;
            old?.Dispose();
        }
        if (_extensionEdges is not null)
        {
            var old = _extensionEdges.Mesh;
            _extensionEdges.Mesh = null;
            old?.Dispose();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(360f, 300f);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        Stretch = true;

        _innerViewport = new SubViewport
        {
            Size = new Vector2I(550, 490),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true,
            TransparentBg = true
        };
        AddChild(_innerViewport);
        _world = new Node3D();
        _innerViewport.AddChild(_world);

        _surface = new MeshInstance3D();
        _world.AddChild(_surface);
        _edges = new MeshInstance3D { Visible = false };
        _world.AddChild(_edges);
        _extensionSurface = new MeshInstance3D();
        _world.AddChild(_extensionSurface);
        _extensionEdges = new MeshInstance3D { Visible = false };
        _world.AddChild(_extensionEdges);
        _surface.MaterialOverride = MakeMaterial();
        _edges.MaterialOverride = MakeMaterial();
        _extensionSurface.MaterialOverride = _surface.MaterialOverride;
        _extensionEdges.MaterialOverride = _edges.MaterialOverride;
        // Presentation-only selection overlay. Never creates or replaces a physical tile ID.
        _selectedTileOutline = new MeshInstance3D { Visible = false };
        _selectedTileOutline.MaterialOverride = MakeMaterial();
        _world.AddChild(_selectedTileOutline);

        _camera = new Camera3D
        {
            Current = true,
            Projection = Camera3D.ProjectionType.Orthogonal,
            Near = 0.05f,
            Far = 25f
        };
        _world.AddChild(_camera);

        _information = new Label
        {
            Position = new Vector2(12f, 12f),
            Text = "TACTICAL LOCAL | awaiting canonical focus",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _information.AddThemeColorOverride("font_color", new Color("#d9e9f4"));
        AddChild(_information);
        _selection = new Label
        {
            Position = new Vector2(12f, 40f),
            Text = "Wheel: zoom | Drag: pan | Click: inspect physical tile",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _selection.AddThemeColorOverride("font_color", new Color("#9cbdc9"));
        AddChild(_selection);
        _terrainDetails = new Label
        {
            Position = new Vector2(12f, 64f),
            Text = "01C EXPERIMENT | tactical terrain only; strategic globe still V1",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _terrainDetails.AddThemeColorOverride("font_color", new Color("#f1d7a3"));
        AddChild(_terrainDetails);

        // Research controls live inside the tactical view; Main.cs and GlobePreview.cs remain untouched.
        var controls = new VBoxContainer { Position = new Vector2(12f, 100f) };
        controls.AddThemeConstantOverride("separation", 3);
        var primaryRow = new HBoxContainer();
        primaryRow.AddThemeConstantOverride("separation", 7);
        controls.AddChild(primaryRow);
        var orbitRow = new HBoxContainer();
        orbitRow.AddThemeConstantOverride("separation", 7);
        controls.AddChild(orbitRow);
        var ringButton = new Button { Text = "FRAME RINGS: 2" };
        ringButton.Pressed += () =>
        {
            _ringCount = _ringCount == 2 ? 1 : 2;
            ringButton.Text = $"FRAME RINGS: {_ringCount}";
            if (_activeFocus != 0) Open(_activeFocus);
        };
        primaryRow.AddChild(ringButton);
        var projectionButton = new Button { Text = "PROJECTION: EQUIDISTANT" };
        projectionButton.Pressed += () =>
        {
            _equidistant = !_equidistant;
            projectionButton.Text = _equidistant ? "PROJECTION: EQUIDISTANT" : "PROJECTION: DIRECT";
            if (_activeFocus != 0) Open(_activeFocus);
        };
        primaryRow.AddChild(projectionButton);
        var rotateLeft = new Button { Text = "ROTATE -15°" };
        rotateLeft.Pressed += () => RotateView(-15f);
        orbitRow.AddChild(rotateLeft);
        var rotateRight = new Button { Text = "ROTATE +15°" };
        rotateRight.Pressed += () => RotateView(15f);
        orbitRow.AddChild(rotateRight);
        var resetNorth = new Button { Text = "NORTH / RESET" };
        resetNorth.Pressed += () =>
        {
            _yawDegrees = 0f;
            UpdateCameraOrientation();
            UpdateCameraFraming();
        };
        orbitRow.AddChild(resetNorth);
        var terrainRow = new HBoxContainer();
        controls.AddChild(terrainRow);
        var terrainButton = new Button { Text = "TERRAIN: 01C EXP" };
        terrainButton.Pressed += () =>
        {
            _showExperimentalTerrain = !_showExperimentalTerrain;
            terrainButton.Text = _showExperimentalTerrain ? "TERRAIN: 01C EXP" : "TERRAIN: IDS";
            if (_activeFocus != 0) Open(_activeFocus);
            GD.Print($"GA_TACTICAL_EXPERIMENTAL_TERRAIN visible={_showExperimentalTerrain} reference_seed={TerrainExperimentSeed}");
        };
        terrainRow.AddChild(terrainButton);
        var framingButton = new Button { Text = "FRAME: COVER (EXP)" };
        framingButton.Pressed += () =>
        {
            _coverViewport = !_coverViewport;
            framingButton.Text = _coverViewport ? "FRAME: COVER (EXP)" : "FRAME: FIT";
            UpdateCameraFraming();
            GD.Print($"GA_TACTICAL_FRAME_TOGGLE cover={_coverViewport} focus={_activeFocus}");
        };
        terrainRow.AddChild(framingButton);
        AddChild(controls);
        GD.Print("GA_TACTICAL_LOCAL_VIEW_READY orthographic_camera=true pitch_from_horizontal_deg=45 equidistant_default=true framing_rings_default=2 resident_rings_default=3 zoom_ratio_to_ring2_cover=0.55 framing_cover_experimental_default=true");
        GD.Print("GA_TACTICAL_CAMERA_STATE_READY_R13 rotation_preserves_zoom=true probe_is_non_mutating=true");
        GD.Print("GA_TACTICAL_PAN_ANCHOR_READY_R15 cursor_follows_drag=true axis_independent=true yaw_aware=true");
        GD.Print("GA_TACTICAL_PREFETCH_COMPONENT_READY_R23 canonical_only=true visual_chart_unchanged=true");
        GD.Print("GA_TACTICAL_PREFETCH_INTENT_COMPONENT_READY_R25 directional=true advisory_only=true chart_unchanged=true");
        GD.Print("GA_TACTICAL_RESIDENT_EXTENSION_COMPONENT_READY_R27 additive=true original_projection=true safe_horizon_rejection=true base_chart_unchanged=true");
        GD.Print($"GA_TACTICAL_MVP_SCOPE_READY_R29 expansion_enabled={EnablePostMvpResidentExpansion.ToString().ToLowerInvariant()} ring_buffer={_residentRingCount} prefetch_intent=advisory");
    }

    private static StandardMaterial3D MakeMaterial() => new()
    {
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    // Experimental V2 physical classification sampled from ONE canonical G(18,0)
    // spherical geometry. These colors never alter V1 simulation, strategic tiles,
    // ownership, topological incidences or movement costs.
    private static Color TerrainColor(ExperimentalProceduralBiomeClassifier.Biome biome) => biome switch
    {
        ExperimentalProceduralBiomeClassifier.Biome.Ocean => new Color("#245b86"),
        ExperimentalProceduralBiomeClassifier.Biome.Desert => new Color("#d2bd75"),
        ExperimentalProceduralBiomeClassifier.Biome.Steppe => new Color("#a4a16b"),
        ExperimentalProceduralBiomeClassifier.Biome.Prairie => new Color("#7bad72"),
        ExperimentalProceduralBiomeClassifier.Biome.Forest => new Color("#336e53"),
        ExperimentalProceduralBiomeClassifier.Biome.Hills => new Color("#898b66"),
        ExperimentalProceduralBiomeClassifier.Biome.Mountains => new Color("#777f8a"),
        ExperimentalProceduralBiomeClassifier.Biome.Ice => new Color("#bddae8"),
        _ => throw new ArgumentOutOfRangeException(nameof(biome))
    };

    private Vector3 Flatten(SphericalPoint3 point, float lift = 0f)
    {
        var p = ConvertPoint(point);
        var east = p.Dot(_basisEast);
        var north = p.Dot(_basisNorth);
        if (_equidistant)
        {
            var depth = Mathf.Clamp(p.Dot(_surfaceNormal), -1f, 1f);
            if (depth <= 0f)
                throw new InvalidOperationException("Tactical equidistant projection crossed the visible hemisphere.");
            var radial = Mathf.Sqrt(east * east + north * north);
            if (radial > 1e-7f)
            {
                var angular = Mathf.Atan2(radial, depth);
                var factor = angular / radial;
                east *= factor;
                north *= factor;
            }
        }
        // The camera observes this flat map from -Z; its screen-right is local -X.
        // Negate presentation X only, so geographic east appears on screen-right.
        // All physical IDs, north direction, polygon selection and outlines follow Flatten.
        return new Vector3(-east, lift, north);
    }

    private void UpdateCameraOrientation()
    {
        if (_camera is null || _activeFocus == 0) return;
        var radians = Mathf.DegToRad(_yawDegrees);
        _camera.Position = _cameraTarget +
            new Vector3(2.5f * Mathf.Sin(radians), 2.5f, -2.5f * Mathf.Cos(radians));
        _camera.LookAt(_cameraTarget, Vector3.Up);
        GD.Print($"GA_TACTICAL_CAMERA_ORBIT focus={_activeFocus} yaw_deg={_yawDegrees:F0} north_far_default=true");
    }

    // Camera ownership contract R13: rotation is an orientation-only action.
    // Never rerun the initial COVER framing after a user-adjusted zoom or pan.
    private void RotateView(float degrees)
    {
        if (_camera is null || _activeFocus == 0) return;
        var previousYaw = _yawDegrees;
        var preservedSize = _camera.Size;
        var preservedTarget = _cameraTarget;
        _yawDegrees = Mathf.PosMod(_yawDegrees + degrees, 360f);
        UpdateCameraOrientation();
        if (_coverViewport && !ViewportSamplesHaveTerrain(preservedSize, 12, 8))
        {
            // Until dynamic prefetch exists, reject an unsafe rotation instead of
            // silently resetting user zoom, moving the camera, or showing background.
            _yawDegrees = previousYaw;
            _cameraTarget = preservedTarget;
            UpdateCameraOrientation();
            _camera.Size = preservedSize;
            GD.Print($"GA_TACTICAL_ROTATION_BLOCKED_COVER focus={_activeFocus} yaw_deg={_yawDegrees:F0} camera_size={preservedSize:F4}");
            return;
        }
        _camera.Size = preservedSize;
        GD.Print($"GA_TACTICAL_ROTATION_ZOOM_PRESERVED_R13 focus={_activeFocus} yaw_deg={_yawDegrees:F0} camera_size={_camera.Size:F4}");
    }

    // Estimate initial camera coverage against the actually rendered polygons, not a
    // spherical reference or just the mesh bounding box. This is a visual heuristic,
    // not a rule for terrain topology, physical IDs or movement.
    private bool ViewportSamplesHaveTerrain(float trialSize, int columns, int rows, bool framingOnly = false)
    {
        if (_camera is null || _innerViewport is null || _polygons.Count == 0) return false;
        if (framingOnly && _framingPolygons.Count == 0) return false;
        var dimensions = _innerViewport.Size;
        if (dimensions.X < 10 || dimensions.Y < 10) return false;
        var previousSize = _camera.Size;
        try
        {
            _camera.Size = trialSize;
            for (int y = 0; y <= rows; y++)
            {
                for (int x = 0; x <= columns; x++)
                {
                    // Border samples are slightly inset, avoiding floating-point edge ties.
                    var u = 0.01f + 0.98f * x / columns;
                    var v = 0.01f + 0.98f * y / rows;
                    var pixel = new Vector2(u * dimensions.X, v * dimensions.Y);
                    var origin = _camera.ProjectRayOrigin(pixel);
                    var direction = _camera.ProjectRayNormal(pixel);
                    if (Math.Abs(direction.Y) < 1e-5f) return false;
                    var distance = -origin.Y / direction.Y;
                    if (distance < 0f) return false;
                    var hit = origin + distance * direction;
                    var point = new Vector2(hit.X, hit.Z);
                    bool inside = false;
                    if (framingOnly)
                    {
                        foreach (var polygon in _framingPolygons)
                        {
                            if (!Contains(point, polygon)) continue;
                            inside = true;
                            break;
                        }
                    }
                    else
                    {
                        foreach (var item in _polygons)
                        {
                            if (!Contains(point, item.Polygon)) continue;
                            inside = true;
                            break;
                        }
                    }
                    if (!inside) return false;
                }
            }
            return true;
        }
        finally
        {
            _camera.Size = previousSize;
        }
    }

    private void UpdateCameraFraming()
    {
        if (_camera is null || _activeFocus == 0 || _polygons.Count == 0) return;
        if (!_coverViewport)
        {
            _camera.Size = _originalSize;
            GD.Print($"GA_TACTICAL_FRAME mode=FIT focus={_activeFocus} camera_size={_originalSize:F4}");
            return;
        }

        var minimum = Math.Max(0.05f, _originalSize * 0.10f);
        if (!ViewportSamplesHaveTerrain(minimum, 12, 8, framingOnly: true))
        {
            _camera.Size = _originalSize;
            _coverSize = _originalSize;
            GD.Print($"GA_TACTICAL_FRAME mode=COVER_FALLBACK_FIT focus={_activeFocus} reason=no_safe_sampled_cover");
            return;
        }

        float low = minimum, high = _originalSize;
        if (ViewportSamplesHaveTerrain(high, 12, 8, framingOnly: true)) low = high;
        else
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var mid = (low + high) * 0.5f;
                if (ViewportSamplesHaveTerrain(mid, 12, 8, framingOnly: true)) low = mid;
                else high = mid;
            }
        }
        _coverSize = Math.Max(minimum, low * 0.93f);
        bool sampledCoverage = ViewportSamplesHaveTerrain(_coverSize, 12, 8, framingOnly: true);
        _camera.Size = _coverSize * InitialZoomToRing2Cover;
        if (!ViewportSamplesHaveTerrain(_camera.Size, 12, 8))
            throw new InvalidOperationException("Resident buffer does not cover initial tactical viewport.");
        GD.Print($"GA_TACTICAL_FRAME mode=COVER focus={_activeFocus} framing_rings={_ringCount} resident_rings={_residentRingCount} projection={(_equidistant ? "EQUIDISTANT" : "DIRECT")} original_size={_originalSize:F4} ring2_cover_size={_coverSize:F4} initial_size={_camera.Size:F4} ratio={InitialZoomToRing2Cover:F2} samples=117 sampled_coverage={sampledCoverage} pitch_from_horizontal_deg=45");
    }

    private static void AddColored(SurfaceTool tool, Vector3 point, Color color)
    {
        tool.SetColor(color);
        tool.AddVertex(point);
    }

    public void SetEdgesVisible(bool visible)
    {
        if (_edges is not null) _edges.Visible = visible;
        if (_extensionEdges is not null) _extensionEdges.Visible = visible;
    }

    // A single selected physical tile receives a gold outline; all topology and game state
    // remain authoritative in GlobalArena.World, including 1/2/3 coarse incidence.
    private void SelectPhysicalTile(ulong? physicalId)
    {
        if (_selectedTileOutline is null || _selection is null)
            return;

        var matched = _polygons.FirstOrDefault(tile => physicalId.HasValue && tile.Id == physicalId.Value);
        var oldMesh = _selectedTileOutline.Mesh;
        _selectedTileOutline.Mesh = null;
        oldMesh?.Dispose();
        _selectedTileOutline.Visible = false;

        if (matched.Id == 0UL)
        {
            _selectedFineTileId = null;
            _selection.Text = "Wheel: zoom | Drag: pan | Click: inspect tile";
            if (_terrainDetails is not null)
                _terrainDetails.Text = _showExperimentalTerrain
                    ? "01C EXPERIMENT | tactical terrain only; strategic globe still V1"
                    : "DIAGNOSTIC COLORS | not a terrain classification";
            PhysicalTileInspected?.Invoke(null, _selection.Text, _terrainDetails?.Text ?? "");
            return;
        }

        var outline = new SurfaceTool();
        outline.Begin(Mesh.PrimitiveType.Lines);
        var tint = new Color("#ffe280");
        for (int i = 0; i < matched.Polygon.Length; i++)
        {
            var a = matched.Polygon[i];
            var b = matched.Polygon[(i + 1) % matched.Polygon.Length];
            AddColored(outline, new Vector3(a.X, 0.0003f, a.Y), tint);
            AddColored(outline, new Vector3(b.X, 0.0003f, b.Y), tint);
        }
        var mesh = outline.Commit();
        outline.Dispose();
        if (mesh is null) throw new InvalidOperationException("Selected physical tile outline missing.");
        _selectedTileOutline.Mesh = mesh;
        _selectedTileOutline.Visible = true;
        _selectedFineTileId = matched.Id;
        var shape = matched.Degree == 5 ? "Pentagon" : "Hexagon";
        _selection.Text = $"G(18,0)/{matched.Id} | {shape} | ring {matched.Ring} | strategic {string.Join(",", matched.Owners)} | selected";
        if (_terrainDetails is not null && _terrainReference is not null && _showExperimentalTerrain)
        {
            int index = checked((int)matched.Id - 1);
            var biome = _terrainReference.Biomes[index];
            var cover = biome.IsWater ? "WATER" : "LAND";
            _terrainDetails.Text = $"01C EXP | {cover} | biome {biome.VisibleBiome} | below ice {biome.UnderlyingBiome} | elevation {_terrainReference.Elevation[index]} | moisture {_terrainReference.Moisture[index]}";
        }
        else if (_terrainDetails is not null)
        {
            _terrainDetails.Text = "DIAGNOSTIC COLORS | not a terrain classification";
        }
        PhysicalTileInspected?.Invoke(matched.Id, _selection.Text, _terrainDetails?.Text ?? "");
    }

    public void Open(ulong focusValue)
    {
        if (_camera is null || _surface is null || _edges is null || _information is null || _selection is null || _selectedTileOutline is null)
            throw new InvalidOperationException("Tactical preview is not ready.");
        ResetExtendedResident();
        if (_activeFocus != focusValue) _selectedFineTileId = null;

        // Accepted scale-six hierarchy, not an experimental G(48,0) preview.
        var refinement = new GoldbergScaledRefinement(
            new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        var physical = _cachedPhysicalMap ??= PhysicalTacticalIncidenceMapper.Materialize(refinement);
        if (physical.Refinement.FineParameters != refinement.FineParameters ||
            physical.Refinement.CoarseParameters != refinement.CoarseParameters)
            throw new InvalidOperationException("Cached physical refinement changed.");
        var focus = new StrategicCellId(focusValue);
        var canonicalFirstRing = MeshPresentationPlan.TacticalNeighborhood(physical, focus);
        if (canonicalFirstRing.Focus != focus || canonicalFirstRing.PhysicalTileIds.Count == 0)
            throw new InvalidOperationException("Canonical tactical neighborhood contract failed.");

        // Reuse the authoritative physical incidence query; never infer fine IDs geometrically.
        _residentRingCount = Math.Min(3, _ringCount + 1);
        var selectedStrategic = new HashSet<StrategicCellId> { focus };
        var strategicRingDistances = new Dictionary<StrategicCellId, int> { [focus] = 0 };
        var frontier = new Queue<(StrategicCellId Id, int Step)>();
        frontier.Enqueue((focus, 0));
        while (frontier.Count > 0)
        {
            var (id, step) = frontier.Dequeue();
            if (step == _residentRingCount) continue;
            foreach (var adjacent in physical.CoarseTopology.Cells[checked((int)id.Value - 1)].AdjacentCellIds)
            {
                if (selectedStrategic.Add(adjacent))
                {
                    strategicRingDistances.Add(adjacent, step + 1);
                    frontier.Enqueue((adjacent, step + 1));
                }
            }
        }
        var framingTiles = PhysicalTacticalViewportQuery.SelectTiles(physical, focus, _ringCount);
        var physicalTiles = PhysicalTacticalViewportQuery.SelectTiles(physical, focus, _residentRingCount);
        if (physicalTiles.Count == 0 || framingTiles.Count == 0)
            throw new InvalidOperationException("Canonical physical viewport is empty.");
        if (_ringCount == 1 && !framingTiles.SequenceEqual(canonicalFirstRing.PhysicalTileIds))
            throw new InvalidOperationException("First-ring physical identities changed.");

        var coarseWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, refinement.CoarseParameters));
        var fineWorld = new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, refinement.FineParameters));
        _cachedCoarseGeometry = coarseWorld.StrategicSphericalGeometry;
        PresentedCoarseWorld = coarseWorld;
        if (coarseWorld.StrategicTopology.Cells.Count != physical.CoarseTopology.Cells.Count ||
            fineWorld.StrategicTopology.Cells.Count != physical.FineTopology.Cells.Count)
            throw new InvalidOperationException("Physical and geometric topology counts differ.");

        var normal = ConvertPoint(coarseWorld.StrategicSphericalGeometry.GetCell(focus).Center).Normalized();
        var reference = Math.Abs(normal.Dot(Vector3.Up)) > 0.95f ? Vector3.Forward : Vector3.Up;
        _basisEast = reference.Cross(normal).Normalized();
        _basisNorth = normal.Cross(_basisEast).Normalized();

        _surfaceNormal = normal;
        if (!selectedStrategic.Contains(focus))
            throw new InvalidOperationException("Tactical focus missing from canonical strategic selection.");
        var selectedPhysical = physicalTiles.Select(id => id.FineStrategicCellId).ToHashSet();
        foreach (var tile in physicalTiles) _renderedPhysicalTileIds.Add(tile.FineStrategicCellId.Value);
        if (selectedPhysical.Count != physicalTiles.Count)
            throw new InvalidOperationException("Duplicated physical tile identity.");
        _polygons.Clear();
        _framingPolygons.Clear();
        float lowX = float.MaxValue, highX = float.MinValue;
        float lowZ = float.MaxValue, highZ = float.MinValue;
        var faceTool = new SurfaceTool();
        faceTool.Begin(Mesh.PrimitiveType.Triangles);
        var geometry = fineWorld.StrategicSphericalGeometry;
        // Lazy one-time V2 preview. Worldgen V1 and strategic presentation remain unchanged.
        if (_showExperimentalTerrain && _terrainReference is null)
            _terrainReference = WorldGenerationV2PhysicalComposition.Generate(
                geometry, TerrainExperimentSeed, landTargetPercent: 35);
        if (_terrainReference is not null && _terrainReference.Geometry.Cells.Count != geometry.Cells.Count)
            throw new InvalidOperationException("Experimental physical terrain geometry count changed.");
        var countTriangles = 0;
        foreach (var id in physicalTiles)
        {
            if (id.FineGoldbergParameters != refinement.FineParameters)
                throw new InvalidOperationException("Physical tile refinement identity mismatch.");
            var index = checked((int)id.FineStrategicCellId.Value - 1);
            var cell = geometry.GetCell(id.FineStrategicCellId);
            var owners = physical.TileIncidences[index].IncidentCoarseCellIds;
            if (!owners.Any(selectedStrategic.Contains))
                throw new InvalidOperationException("Unowned tile entered the selected neighborhood.");

            // The terrain toggle is visual-only. Both variants use the same G(18,0) physical ID.
            Color tint;
            if (_showExperimentalTerrain)
            {
                if (_terrainReference is null)
                    throw new InvalidOperationException("Experimental terrain reference missing.");
                tint = TerrainColor(_terrainReference.Biomes[index].VisibleBiome);
            }
            else
            {
                tint = owners.Contains(focus)
                    ? new Color("#4aa6b2")
                    : Color.FromHsv(((int)owners[0].Value % 11) / 11f, 0.27f, 0.70f);
            }
            var center = Flatten(cell.Center);
            var polygon = new Vector2[cell.BoundaryVertexIds.Count];
            for (int k = 0; k < cell.BoundaryVertexIds.Count; k++)
            {
                var a = Flatten(geometry.GetVertex(cell.BoundaryVertexIds[k]).Position);
                var b = Flatten(geometry.GetVertex(cell.BoundaryVertexIds[(k + 1) % cell.BoundaryVertexIds.Count]).Position);
                polygon[k] = new Vector2(a.X, a.Z);
                // Camera framing is based on the requested ring count, not on the resident buffer.
                if (owners.Any(owner => strategicRingDistances.TryGetValue(owner, out var distance) && distance <= _ringCount))
                {
                    lowX = Math.Min(lowX, a.X);
                    highX = Math.Max(highX, a.X);
                    lowZ = Math.Min(lowZ, a.Z);
                    highZ = Math.Max(highZ, a.Z);
                }
                AddColored(faceTool, center, tint);
                AddColored(faceTool, a, tint);
                AddColored(faceTool, b, tint);
                countTriangles++;
            }
            var nearestRing = owners.Where(strategicRingDistances.ContainsKey)
                .Select(owner => strategicRingDistances[owner]).Min();
            _polygons.Add((id.FineStrategicCellId.Value, polygon,
                owners.Select(owner => owner.Value).ToArray(), polygon.Length, nearestRing));
            if (nearestRing <= _ringCount) _framingPolygons.Add(polygon);
        }
        if (_framingPolygons.Count != framingTiles.Count ||
            _polygons.Count != physicalTiles.Count ||
            _framingPolygons.Count >= _polygons.Count)
            throw new InvalidOperationException("Framing/resident physical incidence separation failed.");
        var faceMesh = faceTool.Commit();
        faceTool.Dispose();
        if (faceMesh is null || countTriangles == 0)
            throw new InvalidOperationException("Canonical tactical surface mesh was not built.");

        var lineTool = new SurfaceTool();
        lineTool.Begin(Mesh.PrimitiveType.Lines);
        var lineColor = new Color("#203949");
        int edgeCount = 0;
        foreach (var edge in physical.FineTopology.Edges)
        {
            if (!edge.IncidentCellIds.Any(selectedPhysical.Contains)) continue;
            var first = Flatten(geometry.GetVertex(edge.IncidentVertexIds[0]).Position, 0.006f);
            var second = Flatten(geometry.GetVertex(edge.IncidentVertexIds[1]).Position, 0.006f);
            AddColored(lineTool, first, lineColor);
            AddColored(lineTool, second, lineColor);
            edgeCount++;
        }
        var lineMesh = lineTool.Commit();
        lineTool.Dispose();
        if (lineMesh is null || edgeCount == 0)
            throw new InvalidOperationException("Canonical physical edge visualization missing.");

        var oldSurface = _surface.Mesh;
        var oldEdges = _edges.Mesh;
        _surface.Mesh = faceMesh;
        _edges.Mesh = lineMesh;
        oldSurface?.Dispose();
        oldEdges?.Dispose();
        // Preserve the optional edge toggle while changing rings or projection.
        SetEdgesVisible(_edges.Visible);

        var target = new Vector3((lowX + highX) * 0.5f, 0f, (lowZ + highZ) * 0.5f);
        var spanX = Math.Max(0.1f, highX - lowX);
        var spanZ = Math.Max(0.1f, highZ - lowZ);
        // Extra margin for a tilted camera. All selected cells fit at initial zoom.
        _originalSize = Math.Max(spanZ * 1.55f, spanX * 1.45f) + 0.10f;
        _camera.Size = _originalSize;
        _activeFocus = focusValue;
        _cameraTarget = target;
        _prefetchIntentOrigin = target;
        _lastPrefetchIntentFocus = 0UL;
        UpdateCameraOrientation();
        UpdateCameraFraming();
        PrepareAdjacentCanonicalData(physical, focus, geometry, physicalTiles);
        _information.Text = $"TACTICAL LOCAL | strategic {focusValue} | frame {_ringCount} / resident {_residentRingCount} rings | {selectedStrategic.Count} regions, {selectedPhysical.Count} physical tiles | {(_equidistant ? "EQUIDISTANT" : "DIRECT")} | {(_showExperimentalTerrain ? "01C EXP" : "IDS")}";
        // Recompute the presentation-only selection outline after a ring or projection change.
        // A tile outside the new region is deselected rather than inventing an alias.
        SelectPhysicalTile(_selectedFineTileId);
        GD.Print($"GA_TACTICAL_LOCAL_OPEN focus={focusValue} framing_rings={_ringCount} resident_rings={_residentRingCount} frame_tiles={framingTiles.Count} projection={(_equidistant ? "Equidistant" : "Orthographic")} regions={selectedStrategic.Count} physical_tiles={selectedPhysical.Count} triangles={countTriangles} edges={edgeCount} parameters=G(3,0)>G(18,0) duplicate_ids=0");
        GD.Print($"GA_TACTICAL_TERRAIN_01C focus={focusValue} visible={_showExperimentalTerrain} seed={TerrainExperimentSeed} land_target=35 physical_reference_cached={_terrainReference is not null} strategic_globe_v1_unchanged=true");
    }

    private static bool Contains(Vector2 p, Vector2[] vertices)
    {
        bool inside = false;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            var a = vertices[i];
            var b = vertices[j];
            if ((a.Y > p.Y) == (b.Y > p.Y)) continue;
            var crossingX = (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (p.X < crossingX) inside = !inside;
        }
        return inside;
    }

    private void PickPhysicalTile(Vector2 localPoint)
    {
        if (_camera is null || _selection is null) return;
        var rayOrigin = _camera.ProjectRayOrigin(localPoint);
        var rayDirection = _camera.ProjectRayNormal(localPoint);
        if (Math.Abs(rayDirection.Y) < 1e-5f) return;
        var t = -rayOrigin.Y / rayDirection.Y;
        if (t < 0f) return;
        var hit = rayOrigin + rayDirection * t;
        var point = new Vector2(hit.X, hit.Z);
        foreach (var item in _polygons)
        {
            if (!Contains(point, item.Polygon)) continue;
            SelectPhysicalTile(item.Id);
            GD.Print($"GA_TACTICAL_LOCAL_PICK fine_cell={item.Id} parameters=G(18,0) degree={item.Degree} ring={item.Ring} strategic_incidence={string.Join("-", item.Owners)} highlight=true");
            return;
        }
        SelectPhysicalTile(null);
        _selection.Text = "No physical tile under cursor";
    }

    // Screen-anchored pan: the map point under the pointer follows the pointer.
    // The tactical mesh lies on Y=0. Orthographic ray/plane intersections
    // automatically account for zoom, 45-degree pitch, and +/-15-degree yaw.
    private bool TryProjectCursorOnTacticalPlane(Vector2 localPixel, out Vector3 hit)
    {
        hit = Vector3.Zero;
        if (_camera is null) return false;
        var origin = _camera.ProjectRayOrigin(localPixel);
        var direction = _camera.ProjectRayNormal(localPixel);
        if (Math.Abs(direction.Y) < 1e-5f) return false;
        var distance = -origin.Y / direction.Y;
        if (distance < 0f) return false;
        hit = origin + direction * distance;
        return true;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_camera is null) return;
        if (@event is InputEventMouseButton button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp && button.Pressed)
            {
                _camera.Size = Mathf.Clamp(_camera.Size * 0.88f,
                    _coverViewport ? _coverSize * 0.30f : _originalSize * 0.40f,
                    _coverViewport ? _coverSize : _originalSize * 2.0f);
                AcceptEvent();
            }
            else if (button.ButtonIndex == MouseButton.WheelDown && button.Pressed)
            {
                var previousSize = _camera.Size;
                _camera.Size = Mathf.Clamp(_camera.Size * 1.12f,
                    _coverViewport ? _coverSize * 0.30f : _originalSize * 0.40f,
                    _coverViewport ? _coverSize : _originalSize * 2.0f);
                if (_coverViewport && !ViewportSamplesHaveTerrain(_camera.Size, 12, 8))
                    _camera.Size = previousSize;
                AcceptEvent();
            }
            else if (button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    _pressed = true;
                    _moved = false;
                    _dragStart = button.Position;
                }
                else
                {
                    if (_pressed && !_moved) PickPhysicalTile(button.Position);
                    _pressed = false;
                }
                AcceptEvent();
            }
            return;
        }
        if (@event is InputEventMouseMotion motion && _pressed)
        {
            if (motion.Position.DistanceTo(_dragStart) > 6f) _moved = true;
            if (_moved)
            {
                // Translate the camera oppositely to the cursor's world-plane
                // displacement, keeping the original map point under the cursor.
                // Never mix world X/Z with screen axes: yaw changes that mapping.
                if (!TryProjectCursorOnTacticalPlane(motion.Position - motion.Relative, out var previousHit) ||
                    !TryProjectCursorOnTacticalPlane(motion.Position, out var currentHit))
                {
                    AcceptEvent();
                    return;
                }
                var translation = previousHit - currentHit;
                translation.Y = 0f;
                _camera.Position += translation;
                _cameraTarget += translation;
                // The three-ring resident buffer permits free movement inside its coverage.
                // A coarse guard remains until recentering/prefetch is implemented.
                if (_coverViewport && !ViewportSamplesHaveTerrain(_camera.Size, 4, 4))
                {
                    _camera.Position -= translation;
                    _cameraTarget -= translation;
                }
                else
                {
                    ReportPreparedPrefetchIntent();
                }
                AcceptEvent();
            }
        }
    }
}
