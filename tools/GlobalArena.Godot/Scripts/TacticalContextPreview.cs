using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.World;
using GlobalArena.Runtime;
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
    private MeshInstance3D? _territoryBoundaries;
    private IReadOnlyList<PhysicalTacticalTerritoryBoundaryPath>? _cachedBoundaryPaths;
    private bool _showTerritoryBoundaries = false;
    // All tactical edge/selection geometry is coplanar with its color mesh.
    // The old 0.006f raised mesh caused visible parallax at the 45-degree camera.
    private const float TacticalSurfacePlaneY = 0f;
    private MeshInstance3D? _selectedTileOutline;
    private Label? _information;
    private VBoxContainer? _hudControls;
    private bool _hudTransferred;
    private Label? _selection;
    private Label? _terrainDetails;
    private WorldGenerationV2PhysicalComposition? _terrainReference;
    private bool _showExperimentalTerrain = true;
    private const ulong TerrainExperimentSeed = 20261012UL;
    // The decorative 01C biome experiment has an independent land/sea
    // composition. Economy QA must show the ACTUAL V1 physical sea-level
    // used to classify canonical freight edges, or terrestrial paths will
    // appear to sail over unrelated blue experimental tiles.
    private WorldGenerationResult? _authoritativeEconomyTerrainWorld;
    private long _economyReliefMinimum;
    private long _economyReliefMaximum;
    private CanonicalEconomyFineVisualDetail? _economyFineDetail;
    private FoundationFineFirstPhysicalGeography? _authoritativeFineEconomy;
    // R101: the physical resolution is supplied by the signed runtime world,
    // rather than inferred from this visualization's former G3/G18 fixture.
    // Legacy demo retains its existing G3/G18 default until its host migrates.
    private GoldbergScaledRefinement _presentationRefinement =
        new(new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
    internal FoundationFineFirstPhysicalGeography? AuthoritativeFineEconomy =>
        _authoritativeFineEconomy;
    internal long PresentedFineElevation(ulong tileId) =>
        _authoritativeFineEconomy is { } fine &&
        tileId > 0UL && tileId <= (ulong)fine.FineElevationRaw.Count
            ? fine.FineElevationRaw[checked((int)tileId - 1)]
            : throw new InvalidOperationException(
                "No authoritative G18 elevation available for this tile.");
    internal int FineVisualDetailTileCount =>
        _economyFineDetail?.FineTileCount ?? 0;
    private Button? _terrainModeButton;

    /// <summary>
    /// R87: the renderer reads the exact signed G18 physical elevation used
    /// to seed economic freight, not V1 coarse interpolation and not 01C
    /// decorative microdetail. Only the signed host may configure this.
    /// </summary>
    public void ConfigureFineFirstEconomicTerrain(
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography physical,
        FoundationFineFirstWorldgenIdentity signature,
        WorldState snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.WorldBinding != RuntimeWorldBinding.FromFineFirst(
                world, incidence, physical, signature) ||
            physical.FineElevationRaw.Count != incidence.FineTopology.Cells.Count ||
            (CanonicalPhysicalIncidenceMap is not null &&
             CanonicalPhysicalIncidenceMap.Refinement != incidence.Refinement))
            throw new InvalidOperationException(
                "Godot cannot paint a terrain different from the signed " +
                "G18 world used by the authoritative economic session.");
        _presentationRefinement = incidence.Refinement;
        _cachedPhysicalMap = incidence;
        _cachedCoarseWorld = null;
        _cachedFineWorld = null;
        _cachedCoarseGeometry = null;
        _authoritativeEconomyTerrainWorld = world;
        _authoritativeFineEconomy = physical;
        _economyFineDetail = null;
        _economyReliefMinimum = physical.FineElevationRaw.Min();
        _economyReliefMaximum = physical.FineElevationRaw.Max();
        if (_terrainModeButton is not null)
            _terrainModeButton.Text = _showExperimentalTerrain
                ? "TERRAIN: AUTHORITATIVE G18" : "TERRAIN: IDS";
        GD.Print("GA_NEXT_R87_G18_TERRAIN_BOUND source=signed_g18 " +
            $"tiles={physical.FineElevationRaw.Count} " +
            $"land={physical.LandTileCount} sea={physical.FineElevationRaw.Count - physical.LandTileCount} " +
            "freight_map_aligned=true decorative_microdetail=false");
    }

    public void ConfigureAuthoritativeEconomyTerrain(
        WorldGenerationResult? world)
    {
        if (world is not null &&
            world.Request.StrategicParameters.StrategicCellCount == 0)
            throw new ArgumentException(
                "Canonical economy terrain requires a generated bound world.",
                nameof(world));
        // One authoritative coarse elevation range per world. The fine
        // tactical cells sample that SAME field by incidence; no second
        // geography, 01C seed, or discrete biome classification is involved.
        if (world is not null)
        {
            var heights = world.StrategicTopology.Cells.Select(
                cell => world.StrategicPhysicalFields.Elevation
                    .GetRawValue(cell.Id)).ToArray();
            if (heights.Length == 0)
                throw new InvalidOperationException(
                    "Economy terrain has no canonical elevations.");
            _economyReliefMinimum = heights.Min();
            _economyReliefMaximum = heights.Max();
        }
        _authoritativeEconomyTerrainWorld = world;
        _authoritativeFineEconomy = null;
        _economyFineDetail = null;
        if (_terrainModeButton is not null)
            _terrainModeButton.Text = !_showExperimentalTerrain
                ? "TERRAIN: IDS"
                : world is null ? "TERRAIN: 01C EXP" : "TERRAIN: ECONOMY RELIEF + DETAIL";
        GD.Print($"GA_TACTICAL_TERRAIN_SOURCE source={(world is null ? "DECORATIVE_01C" : "AUTH_V1_ECONOMY")} freight_map_aligned={world is not null}");
    }

    private long AuthoritativeEconomyElevation(
        ulong tileId, PhysicalTacticalIncidenceMap physical)
    {
        var world = _authoritativeEconomyTerrainWorld ??
            throw new InvalidOperationException(
                "Economy terrain requires its authoritative world.");
        if (physical.Refinement.CoarseParameters !=
                world.Request.StrategicParameters)
            throw new InvalidOperationException(
                "Economic terrain world disagrees with the routed fine topology.");
        var tile = physical.TileIncidences[checked((int)tileId - 1)];
        var elevation = world.StrategicPhysicalFields.Elevation;
        long total = 0L;
        foreach (var coarse in tile.IncidentCoarseCellIds)
            total = checked(total + elevation.GetRawValue(coarse));
        return total / tile.IncidentCoarseCellIds.Count;
    }

    private bool IsAuthoritativeEconomyLand(
        ulong tileId, PhysicalTacticalIncidenceMap physical) =>
        _authoritativeFineEconomy is { } fine
            ? fine.FineElevationRaw[checked((int)tileId - 1)] >
                fine.SeaLevelRaw
            : AuthoritativeEconomyElevation(tileId, physical) >
                _authoritativeEconomyTerrainWorld!.StrategicPhysicalFields
                    .SeaLevel.RawValue;

    // R91 continuous land snow band; frozen ocean uses a crisp movement edge.
    // Temperature, including altitude cooling, comes directly from the signed
    // G18 field. No discrete ice/biome assignment is persisted.
    internal static float FineIceWeight(long temperatureRaw) =>
        CanonicalEconomyReliefPalette.ContinuousLandSnowWeight(temperatureRaw);

    private Color EconomicTerrainTint(
        ulong tileId, PhysicalTacticalIncidenceMap physical)
    {
        var world = _authoritativeEconomyTerrainWorld ??
            throw new InvalidOperationException(
                "Detailed economy preview requires a canonical world.");
        if (_authoritativeFineEconomy is { } physicalWorld)
        {
            return CanonicalEconomyReliefPalette.FromSignedFineSurface(
                physicalWorld, checked((int)tileId - 1),
                _economyReliefMinimum, _economyReliefMaximum);
        }
        // Legacy V1-only path retained for old experimental tests.
        _economyFineDetail ??= CanonicalEconomyFineVisualDetail.Create(
            world, physical, _economyReliefMinimum,
            _economyReliefMaximum);
        return CanonicalEconomyReliefPalette.FromElevation(
            _economyFineDetail.Elevation(tileId),
            world.StrategicPhysicalFields.SeaLevel.RawValue,
            _economyReliefMinimum, _economyReliefMaximum);
    }
    private ulong? _selectedFineTileId;
    // R30: presentation-only notification to the existing right sidebar.
    public event Action<ulong?, string, string>? PhysicalTileInspected;
    // A click is distinguishable from read-only programmatic tile inspection.
    public event Action<PhysicalTacticalTileId>? CanonicalPhysicalTileClicked;
    private readonly List<(ulong Id, Vector2[] Polygon, ulong[] Owners, int Degree, int Ring)> _polygons = new();
    private readonly Dictionary<ulong, Vector3> _tileCenters = new();
    // Framing geometry and resident geometry have separate scopes.
    private readonly List<Vector2[]> _framingPolygons = new();
    private const float InitialZoomToRing2Cover = 0.95f;
    private int _residentRingCount = 3;
    private Vector3 _basisEast;
    private Vector3 _basisNorth;
    // R97: camera orientation is a presentation policy, not a property of G3.
    // LOCKED parallel-transports the previous viewing direction across charts.
    // NORTH follows geographic north at the actual spherical camera center.
    private bool _geographicNorthUp;
    private Vector3? _navigationOrientationAnchor;
    // R98: independent of strategic/tactical tile centers. The continuous
    // screen-center geography is computed from the inverse spherical chart
    // on every accepted pointer motion; chart focus is only a mesh cache key.
    private Vector3 _exactViewPointOnSphere;
    private Vector3 _lastValidGeographicNorth = Vector3.Forward;
    public Vector3 ExactViewPointOnSphere => _exactViewPointOnSphere;
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
    // R96: a chart switch can change the tangent-plane location of the
    // camera center just enough to immediately select the previous focus.
    // Prevent that A/B oscillation until the user has actually dragged
    // beyond a small screen-space distance after the switch.
    private ulong _previousHandoffFocus;
    private float _dragPixelsAfterHandoff;
    private const float ReverseHandoffDragPixels = 48f;
    private ulong _continuousNavigationHandoffs;
    public ulong ActiveStrategicFocus => _activeFocus;
    public ulong ContinuousNavigationHandoffs => _continuousNavigationHandoffs;
    private Vector3 _surfaceNormal;
    private Vector3 _cameraTarget;
    // R23: projection-independent canonical prefetch, never a rendered chart or a camera owner.
    private PhysicalTacticalIncidenceMap? _cachedPhysicalMap;
    // R35: expose the SAME authoritative map used by rendering, no duplicate identity model.
    public PhysicalTacticalIncidenceMap? CanonicalPhysicalIncidenceMap => _cachedPhysicalMap;
    public WorldGenerationResult? PresentedCoarseWorld { get; private set; }
    public ulong? SelectedCanonicalPhysicalTileId => _selectedFineTileId;
    // Presentation-only inspection to prove dock reflow cannot teleport the
    // camera or silently mutate orthographic zoom.
    public (Vector3 Target, float Zoom, float YawDegrees) TacticalCameraState =>
        (_cameraTarget, _camera?.Size ?? 0f, _yawDegrees);
    public Vector2I RenderViewportSize => _innerViewport?.Size ?? Vector2I.Zero;
    private readonly Dictionary<ulong, PhysicalTacticalResidentTransferPlan> _preparedTransfers = new();
    private readonly Dictionary<ulong, SphericalPoint3[]> _preparedCanonicalTileGeometry = new();
    // R25: pan intent is advisory; it selects existing canonical prefetch plans only.
    private StrategicSphericalGeometry? _cachedCoarseGeometry;
    // The world geometry is deterministic for this presentation seed and
    // refinement. Generate it only once, not at every focus boundary.
    private WorldGenerationResult? _cachedCoarseWorld;
    private WorldGenerationResult? _cachedFineWorld;
    private Vector3 _prefetchIntentOrigin;
    private ulong _lastPrefetchIntentFocus;
    // R29 additive resident expansion remains disabled: R43 now performs a
    // bounded, camera-anchored focus handoff instead of unbounded extension.
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


    // R45: the camera travels on the canonical sphere. The chart is only a
    // bounded local projection; its planar coordinates are NEVER used to
    // decide which strategic territory owns the ongoing drag.
    private bool TryUnprojectOnSphere(Vector3 planar, out Vector3 spherical)
    {
        spherical = Vector3.Zero;
        var tangent = _basisEast * -planar.X + _basisNorth * planar.Z;
        var radius = tangent.Length();
        if (_equidistant)
        {
            // R102: azimuthal equidistant charts are valid beyond 90
            // degrees. Smaller coarse grids can require broader resident
            // neighborhoods. Reject only the antipodal singularity.
            if (radius >= Mathf.Pi - 0.01f) return false;
            spherical = (_surfaceNormal * Mathf.Cos(radius) +
                tangent * (radius < 1e-6f ? 1f : Mathf.Sin(radius) / radius))
                .Normalized();
        }
        else
        {
            if (radius >= 0.999f) return false;
            spherical = (_surfaceNormal * Mathf.Sqrt(1f - radius * radius) +
                tangent).Normalized();
        }
        return spherical.IsFinite();
    }

    // Invoked on EVERY motion while the mouse button remains down. Handoff
    // happens at the geodesic Voronoi boundary, *before* the viewport can
    // reach the bounded three-ring mesh edge. No one-handoff-per-drag latch.
    private bool TryContinuousFocusHandoff(Vector2 cursorPixel)
    {
        if (_camera is null || _cachedCoarseGeometry is null ||
            _cachedPhysicalMap is null || _activeFocus == 0UL ||
            !TryProjectCursorOnTacticalPlane(cursorPixel, out var cursorPoint) ||
            !TryUnprojectOnSphere(_cameraTarget, out var sphereCenter) ||
            !TryUnprojectOnSphere(cursorPoint, out var sphereCursor)) return false;

        var focus = new StrategicCellId(_activeFocus);
        var current = ConvertPoint(_cachedCoarseGeometry.GetCell(focus).Center)
            .Normalized();
        // A small hysteresis margin avoids repeated A/B handoffs at a border.
        var bestScore = current.Dot(sphereCenter) + 0.002f;
        ulong nextFocus = 0UL;
        foreach (var neighbor in _cachedPhysicalMap.CoarseTopology.Cells[
            checked((int)_activeFocus - 1)].AdjacentCellIds.OrderBy(id => id.Value))
        {
            if (!_preparedTransfers.ContainsKey(neighbor.Value))
                throw new InvalidOperationException("Missing prepared canonical neighbor plan.");
            var candidate = ConvertPoint(_cachedCoarseGeometry.GetCell(neighbor).Center)
                .Normalized();
            var score = candidate.Dot(sphereCenter);
            if (score <= bestScore) continue;
            bestScore = score;
            nextFocus = neighbor.Value;
        }
        if (nextFocus == 0UL) return false;
        if (nextFocus == _previousHandoffFocus &&
            _dragPixelsAfterHandoff < ReverseHandoffDragPixels)
            return false;

        var plan = _preparedTransfers[nextFocus];
        if (plan.ToPrepare.Any(id => !_preparedCanonicalTileGeometry.ContainsKey(
            id.FineStrategicCellId.Value)))
            throw new InvalidOperationException("R45 incomplete adjacent prefetch.");
        return TryTransferProjection(nextFocus, plan, cursorPixel, sphereCursor);
    }

    // Preserve the real geographic point under the cursor across two local
    // projections. This is stronger than preserving a planar offset from a
    // shared tile (R43), which introduced visible jumps and empty corners.
    private bool TryTransferProjection(
        ulong nextFocus, PhysicalTacticalResidentTransferPlan plan,
        Vector2 cursorPixel, Vector3 sphereCursor)
    {
        if (_camera is null) return false;
        var sourceFocus = _activeFocus;
        var previousTarget = _cameraTarget;
        var previousZoom = _camera.Size;
        var previousYaw = _yawDegrees;
        var previousExact = _exactViewPointOnSphere;
        var previousNorth = _lastValidGeographicNorth;
        var selectedId = _selectedFineTileId;

        if (!plan.Reused.Any(tile =>
            _tileCenters.ContainsKey(tile.FineStrategicCellId.Value)))
            throw new InvalidOperationException("Focus transfer lacks shared physical identities.");

        // Capture the exact spherical center BEFORE replacing the local chart.
        // North mode uses this position, not the discrete strategic focus.
        if (TryUnprojectOnSphere(previousTarget, out var centerOnGlobe))
            _navigationOrientationAnchor = centerOnGlobe;
        Open(nextFocus);
        _camera.Size = previousZoom;
        _yawDegrees = previousYaw;
        UpdateCameraOrientation();

        // In the destination chart the same globe point obtains new XY
        // coordinates. Translate the camera until it lands under the very
        // same mouse pixel. Do not touch _pressed or _dragStart.
        var nextPoint = FlattenUnitVector(sphereCursor, TacticalSurfacePlaneY);
        if (!TryProjectCursorOnTacticalPlane(cursorPixel, out var hit))
        {
            RestoreProjection(sourceFocus, previousTarget, previousZoom,
                previousYaw, selectedId);
            _exactViewPointOnSphere = previousExact;
            _lastValidGeographicNorth = previousNorth;
            return false;
        }
        var shift = nextPoint - hit;
        shift.Y = 0f;
        _cameraTarget += shift;
        UpdateCameraOrientation();

        if (_coverViewport && !ViewportSamplesHaveTerrain(_camera.Size, 6, 5))
        {
            RestoreProjection(sourceFocus, previousTarget, previousZoom,
                previousYaw, selectedId);
            _exactViewPointOnSphere = previousExact;
            _lastValidGeographicNorth = previousNorth;
            GD.Print($"GA_TACTICAL_HANDOFF_DEFERRED_R45 source={sourceFocus} next={nextFocus} reason=viewport_coverage");
            return false;
        }

        if (selectedId.HasValue && _tileCenters.ContainsKey(selectedId.Value))
            SelectPhysicalTile(selectedId);
        _prefetchIntentOrigin = _cameraTarget;
        _lastPrefetchIntentFocus = 0UL;
        _previousHandoffFocus = sourceFocus;
        _dragPixelsAfterHandoff = 0f;
        _continuousNavigationHandoffs++;
        GD.Print($"GA_TACTICAL_FOCUS_HANDOFF_R45 source={sourceFocus} next={nextFocus} " +
            $"zoom={previousZoom:F6} yaw={previousYaw:F3} " +
            "cursor_geography_preserved=true drag_continues=true " +
            "viewport_covered=true simulation_mutation=false");
        return true;
    }

    private void RestoreProjection(
        ulong focus, Vector3 target, float zoom, float yaw, ulong? selection)
    {
        Open(focus);
        if (_camera is null)
            throw new InvalidOperationException("Tactical camera missing during rollback.");
        _cameraTarget = target;
        _camera.Size = zoom;
        _yawDegrees = yaw;
        UpdateCameraOrientation();
        if (selection.HasValue && _tileCenters.ContainsKey(selection.Value))
            SelectPhysicalTile(selection);
        _prefetchIntentOrigin = _cameraTarget;
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
                if (_authoritativeEconomyTerrainWorld is not null)
                    tint = EconomicTerrainTint(id, _cachedPhysicalMap);
                else
                {
                    if (_terrainReference is null)
                        throw new InvalidOperationException("R27 terrain data missing.");
                    tint = TerrainColor(_terrainReference.Biomes[checked((int)id - 1)].VisibleBiome);
                }
            }
            else
            {
                // Keep the dormant post-MVP extension visually neutral as well.
                tint = Color.FromHsv(((int)owners[0] % 11) / 11f, 0.27f, 0.70f);
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
                    AddColored(lines, entry.Points[j], lineColor);
                    AddColored(lines, entry.Points[next], lineColor);
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
        _territoryBoundaries = new MeshInstance3D();
        _world.AddChild(_territoryBoundaries);
        _extensionSurface = new MeshInstance3D();
        _world.AddChild(_extensionSurface);
        _extensionEdges = new MeshInstance3D { Visible = false };
        _world.AddChild(_extensionEdges);
        _surface.MaterialOverride = MakeMaterial();
        _edges.MaterialOverride = MakeCoplanarOverlayMaterial();
        _territoryBoundaries.MaterialOverride = MakeCoplanarOverlayMaterial();
        _extensionSurface.MaterialOverride = _surface.MaterialOverride;
        _extensionEdges.MaterialOverride = _edges.MaterialOverride;
        // Presentation-only selection overlay. Never creates or replaces a physical tile ID.
        _selectedTileOutline = new MeshInstance3D { Visible = false };
        _selectedTileOutline.MaterialOverride = MakeCoplanarOverlayMaterial();
        _world.AddChild(_selectedTileOutline);
        InitializeSettledTradeMesh();
        InitializeWorkerBadges();

        _camera = new Camera3D
        {
            Current = true,
            Projection = Camera3D.ProjectionType.Orthogonal,
            Near = 0.05f,
            Far = 25f
        };
        _world.AddChild(_camera);
        InitializeLocalSiteMarkers();

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
        // Full physical-tile text is presented in the existing sidebar inspector.
        // Keep the label data source live for PhysicalTileInspected, but not over terrain.
        _selection.Visible = false;
        AddChild(_selection);
        _terrainDetails = new Label
        {
            Position = new Vector2(12f, 64f),
            Text = "01C EXPERIMENT | tactical terrain only; strategic globe still V1",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _terrainDetails.AddThemeColorOverride("font_color", new Color("#f1d7a3"));
        // Terrain details remain available to the sidebar via the existing event.
        _terrainDetails.Visible = false;
        AddChild(_terrainDetails);

        // Controls begin in the tactical view and are transferred to the sidebar by Main.
        var controls = new VBoxContainer { Position = new Vector2(12f, 48f) };
        _hudControls = controls;
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
        var northModeButton = new Button { Text = "ORIENTATION: LOCKED" };
        northModeButton.Pressed += () =>
        {
            _geographicNorthUp = !_geographicNorthUp;
            northModeButton.Text = _geographicNorthUp
                ? "ORIENTATION: NORTH UP" : "ORIENTATION: LOCKED";
            // Reorient in place at the actual spherical view point. A mode
            // toggle must not rebuild G18 tiles, change zoom or reset focus.
            UpdateCameraOrientation();
            GD.Print($"GA_NEXT_R97_ORIENTATION_MODE mode=" +
                (_geographicNorthUp ? "north_up" : "locked"));
        };
        orbitRow.AddChild(northModeButton);
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
            RebuildWorkerMarkers();
        };
        orbitRow.AddChild(resetNorth);
        var terrainRow = new HBoxContainer();
        controls.AddChild(terrainRow);
        var terrainButton = new Button { Text = "TERRAIN: 01C EXP" };
        _terrainModeButton = terrainButton;
        terrainButton.Pressed += () =>
        {
            _showExperimentalTerrain = !_showExperimentalTerrain;
            terrainButton.Text = !_showExperimentalTerrain ? "TERRAIN: IDS" :
                _authoritativeEconomyTerrainWorld is null
                    ? "TERRAIN: 01C EXP" : "TERRAIN: ECONOMY RELIEF + DETAIL";
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
        var bordersToggle = new CheckBox { Text = "TERRITORY LIMITS", ButtonPressed = false };
        bordersToggle.Toggled += visible =>
        {
            _showTerritoryBoundaries = visible;
            if (_territoryBoundaries is not null) _territoryBoundaries.Visible = visible;
            GD.Print($"GA_TACTICAL_TERRITORY_BORDERS_TOGGLE visible={visible}");
        };
        terrainRow.AddChild(bordersToggle);
        AddChild(controls);
        GD.Print("GA_TACTICAL_LOCAL_VIEW_READY orthographic_camera=true pitch_from_horizontal_deg=45 equidistant_default=true framing_rings_default=2 resident_rings_default=3 zoom_ratio_to_ring2_cover=0.55 framing_cover_experimental_default=true");
        GD.Print("GA_TACTICAL_CAMERA_STATE_READY_R13 rotation_preserves_zoom=true probe_is_non_mutating=true");
        GD.Print("GA_TACTICAL_PAN_ANCHOR_READY_R15 cursor_follows_drag=true axis_independent=true yaw_aware=true");
        GD.Print("GA_TACTICAL_PREFETCH_COMPONENT_READY_R23 canonical_only=true visual_chart_unchanged=true");
        GD.Print("GA_TACTICAL_PREFETCH_INTENT_COMPONENT_READY_R45 sphere_geodesic=true continuous_drag=true");
        GD.Print("GA_TACTICAL_RESIDENT_EXTENSION_COMPONENT_READY_R27 additive=true original_projection=true safe_horizon_rejection=true base_chart_unchanged=true");
        GD.Print($"GA_TACTICAL_MVP_SCOPE_READY_R45 expansion_enabled=false ring_buffer={_residentRingCount} handoff=geodesic");
    }

    // GA_UI_TACTICAL_HUD_SIDEBAR_R1: move existing controls, never recreate handlers.
    // The map retains exclusive responsibility for rendering and tile interaction.
    public void TransferHud(VBoxContainer sidebar)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        if (_hudTransferred || _hudControls is null || _information is null)
            throw new InvalidOperationException("Tactical HUD transfer requires a live untransferred view.");
        if (_hudControls.GetChildCount() != 3 ||
            _hudControls.GetChild(2) is not HBoxContainer terrainRow ||
            terrainRow.GetChildCount() != 3 ||
            terrainRow.GetChild(2) is not CheckBox boundaries ||
            boundaries.Text != "TERRITORY LIMITS")
            throw new InvalidOperationException("Unexpected tactical controls; refusing unsafe HUD transfer.");
        // Three buttons do not fit in the 390px sidebar; keep the border toggle on its own row.
        boundaries.Reparent(_hudControls);
        _information.Reparent(sidebar);
        _information.Position = Vector2.Zero;
        _information.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hudControls.Reparent(sidebar);
        _hudControls.Position = Vector2.Zero;
        _hudTransferred = true;
        GD.Print("GA_UI_TACTICAL_HUD_SIDEBAR_READY_R1 toolbar=sidebar map_overlay=false handlers_preserved=true gameplay_unchanged=true");
    }

    private static StandardMaterial3D MakeMaterial() => new()
    {
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    // Coplanar lines are drawn in the transparent pass with depth-test
    // disabled so their vertices can occupy *exactly* the surface plane.
    // This avoids both 3D lift/parallax and coplanar z-fighting.
    private static StandardMaterial3D MakeCoplanarOverlayMaterial() => new()
    {
        VertexColorUseAsAlbedo = true,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true,
        RenderPriority = 1
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
        return FlattenUnitVector(ConvertPoint(point), lift);
    }

    private Vector3 FlattenUnitVector(Vector3 p, float lift)
    {
        var east = p.Dot(_basisEast);
        var north = p.Dot(_basisNorth);
        if (_equidistant)
        {
            var depth = Mathf.Clamp(p.Dot(_surfaceNormal), -1f, 1f);
            // On a sphere, equidistant projection remains defined on the
            // far hemisphere. Only the antipode has no unique bearing.
            if (depth < -0.99999f)
                throw new InvalidOperationException(
                    "Tactical equidistant projection reached the antipode.");
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

    // R97 CI-only: exercise both policies on real local geometry without
    // changing the game, zoom, or selected physical identities.
    internal void ExerciseDualOrientationSmoke()
    {
        if (_camera is null || _activeFocus == 0UL)
            throw new InvalidOperationException("Orientation smoke requires a map.");
        bool priorMode = _geographicNorthUp;
        ulong focus = _activeFocus;
        float zoom = _camera.Size;
        try
        {
            _geographicNorthUp = true;
            Open(focus);
            if (!_basisNorth.IsFinite() || !_basisEast.IsFinite() ||
                Math.Abs(_basisNorth.Dot(_basisEast)) > 0.001f)
                throw new InvalidOperationException("North-up projection is not orthogonal.");
            _geographicNorthUp = false;
            Open(focus);
            if (!_basisNorth.IsFinite() || !_basisEast.IsFinite() ||
                Math.Abs(_basisNorth.Dot(_basisEast)) > 0.001f)
                throw new InvalidOperationException("Locked projection is not orthogonal.");
            GD.Print("GA_NEXT_R97_DUAL_ORIENTATION_PASS " +
                "north_up=true locked=true finite_basis=true");
        }
        finally
        {
            _geographicNorthUp = priorMode;
            Open(focus);
            _camera.Size = zoom;
            UpdateCameraOrientation();
        }
    }

    // R98: update from the actual map point under the optical center. A
    // geographical focus is a continuous unit vector, not a G3/G18 tile.
    // This is O(1) vector math and never rebuilds the rendered mesh.
    private void UpdateCameraOrientation()
    {
        if (_camera is null || _activeFocus == 0) return;
        var radians = Mathf.DegToRad(_yawDegrees);
        _camera.Position = _cameraTarget +
            new Vector3(2.5f * Mathf.Sin(radians), 2.5f, -2.5f * Mathf.Cos(radians));

        if (TryUnprojectOnSphere(_cameraTarget, out var exact))
            _exactViewPointOnSphere = exact;
        if (!_geographicNorthUp || _exactViewPointOnSphere.LengthSquared() < 0.9f)
        {
            // LOCKED: the viewing frame does not rotate during ordinary pan.
            _camera.LookAt(_cameraTarget, Vector3.Up);
            return;
        }

        var north = Vector3.Up -
            _exactViewPointOnSphere * _exactViewPointOnSphere.Dot(Vector3.Up);
        // Geographic north has no unique direction at a pole. Preserve the
        // last valid tangent direction, and project it onto the new tangent
        // plane; do not flip the compass by sampling a different G3 center.
        if (north.LengthSquared() < 1e-6f)
            north = _lastValidGeographicNorth -
                _exactViewPointOnSphere *
                _lastValidGeographicNorth.Dot(_exactViewPointOnSphere);
        if (north.LengthSquared() < 1e-6f)
        {
            _camera.LookAt(_cameraTarget, Vector3.Up);
            return;
        }
        north = north.Normalized();
        _lastValidGeographicNorth = north;
        // Transform the physical north tangent to the CURRENT local chart
        // using a small geodesic differential. Only the camera rotates.
        // G18 terrain and the underlying sphere remain untouched.
        var nearbyNorth = (_exactViewPointOnSphere + 0.001f * north).Normalized();
        Vector3 planarNorth;
        if (nearbyNorth.Dot(_surfaceNormal) > 0.001f)
            planarNorth = FlattenUnitVector(nearbyNorth, 0f) -
                FlattenUnitVector(_exactViewPointOnSphere, 0f);
        else
            planarNorth = new Vector3(-north.Dot(_basisEast), 0f,
                north.Dot(_basisNorth));
        planarNorth.Y = 0f;
        if (planarNorth.LengthSquared() < 1e-10f || !planarNorth.IsFinite())
        {
            _camera.LookAt(_cameraTarget, Vector3.Up);
            return;
        }
        _camera.LookAt(_cameraTarget, planarNorth.Normalized());
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
        // Count glyphs are coplanar but screen-oriented. Update only the
        // presentation mesh after accepted yaw; do not move any worker.
        RebuildWorkerMarkers();
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

    // Actual Godot mesh contract for the visual smoke: every displayed
    // tactical-grid endpoint must be a vertex of the SAME surface mesh,
    // at identical X/Y/Z. No separately projected or lifted overlay.
    public bool HasSurfaceAlignedTacticalGrid()
    {
        if (_surface?.Mesh is not ArrayMesh faceMesh ||
            _edges?.Mesh is not ArrayMesh edgeMesh ||
            faceMesh.GetSurfaceCount() == 0 || edgeMesh.GetSurfaceCount() == 0)
            return false;
        var facePositions = faceMesh.SurfaceGetArrays(0)
            [(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var linePositions = edgeMesh.SurfaceGetArrays(0)
            [(int)Mesh.ArrayType.Vertex].AsVector3Array();
        if (facePositions.Length == 0 || linePositions.Length == 0)
            return false;
        var canonicalGeometry = new HashSet<Vector3>(facePositions);
        return linePositions.All(vertex =>
            vertex.Y == TacticalSurfacePlaneY && canonicalGeometry.Contains(vertex));
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
                    ? (_authoritativeEconomyTerrainWorld is not null
                        ? (_authoritativeFineEconomy is null
                            ? "ECONOMY RELIEF | V1 coast; 01C detail is visual only"
                            : "ECONOMY RELIEF | authoritative G18 terrain and freight")
                        : "01C EXPERIMENT | tactical terrain only; strategic globe still V1")
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
            AddColored(outline, new Vector3(a.X, TacticalSurfacePlaneY, a.Y), tint);
            AddColored(outline, new Vector3(b.X, TacticalSurfacePlaneY, b.Y), tint);
        }
        var mesh = outline.Commit();
        outline.Dispose();
        if (mesh is null) throw new InvalidOperationException("Selected physical tile outline missing.");
        _selectedTileOutline.Mesh = mesh;
        _selectedTileOutline.Visible = true;
        _selectedFineTileId = matched.Id;
        var shape = matched.Degree == 5 ? "Pentagon" : "Hexagon";
        var fineResolution = _cachedPhysicalMap?.Refinement.FineParameters ??
            _presentationRefinement.FineParameters;
        _selection.Text = $"G({fineResolution.M},{fineResolution.N})/{matched.Id} | {shape} | ring {matched.Ring} | strategic {string.Join(",", matched.Owners)} | selected";
        if (_terrainDetails is not null && _showExperimentalTerrain &&
            _authoritativeEconomyTerrainWorld is not null &&
            _cachedPhysicalMap is not null)
        {
            bool land = IsAuthoritativeEconomyLand(matched.Id, _cachedPhysicalMap);
            _terrainDetails.Text = land
                ? (_authoritativeFineEconomy is null
                    ? "ECONOMY RELIEF | LAND | V1 visual detail"
                    : "ECONOMY RELIEF | LAND | real G18 elevation")
                : (_authoritativeFineEconomy is null
                    ? "ECONOMY RELIEF | WATER | V1 visual detail"
                    : "ECONOMY RELIEF | WATER | real G18 depth");
        }
        else if (_terrainDetails is not null && _terrainReference is not null && _showExperimentalTerrain)
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
        if (_camera is null || _surface is null || _edges is null || _territoryBoundaries is null || _information is null || _selection is null || _selectedTileOutline is null)
            throw new InvalidOperationException("Tactical preview is not ready.");
        ResetExtendedResident();
        if (_activeFocus != focusValue) _selectedFineTileId = null;

        // Accepted scale-six hierarchy, not an experimental G(48,0) preview.
        var refinement = _presentationRefinement;
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

        var coarseWorld = _cachedCoarseWorld ??= new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, refinement.CoarseParameters));
        var fineWorld = _cachedFineWorld ??= new DeterministicWorldGenerator().Generate(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, refinement.FineParameters));
        _cachedCoarseGeometry = coarseWorld.StrategicSphericalGeometry;
        PresentedCoarseWorld = coarseWorld;
        if (coarseWorld.StrategicTopology.Cells.Count != physical.CoarseTopology.Cells.Count ||
            fineWorld.StrategicTopology.Cells.Count != physical.FineTopology.Cells.Count)
            throw new InvalidOperationException("Physical and geometric topology counts differ.");

        var normal = ConvertPoint(coarseWorld.StrategicSphericalGeometry.GetCell(focus).Center).Normalized();
        // At every focus transfer, derive the new local frame from either
        // the transported previous screen-up direction or the geographic
        // north vector at the actual camera center. This avoids the abrupt
        // reference-axis flip (Up -> Forward) near polar latitudes.
        Vector3 desiredNorth = Vector3.Zero;
        if (_geographicNorthUp)
        {
            Vector3 position = _navigationOrientationAnchor ?? normal;
            // North is tangent to the sphere; at the exact pole its direction
            // is undefined. Retain the last valid orientation there.
            desiredNorth = Vector3.Up - position * position.Dot(Vector3.Up);
        }
        else if (_basisNorth.LengthSquared() > 0.01f)
            desiredNorth = _basisNorth;
        _navigationOrientationAnchor = null;
        desiredNorth -= normal * desiredNorth.Dot(normal);
        if (desiredNorth.LengthSquared() < 1e-6f)
        {
            desiredNorth = _basisNorth -
                normal * _basisNorth.Dot(normal);
            if (desiredNorth.LengthSquared() < 1e-6f)
            {
                var fallback = Math.Abs(normal.Dot(Vector3.Up)) > 0.95f
                    ? Vector3.Forward : Vector3.Up;
                desiredNorth = fallback - normal * fallback.Dot(normal);
            }
        }
        _basisNorth = desiredNorth.Normalized();
        _basisEast = _basisNorth.Cross(normal).Normalized();
        _basisNorth = normal.Cross(_basisEast).Normalized();

        _surfaceNormal = normal;
        if (!selectedStrategic.Contains(focus))
            throw new InvalidOperationException("Tactical focus missing from canonical strategic selection.");
        var selectedPhysical = physicalTiles.Select(id => id.FineStrategicCellId).ToHashSet();
        foreach (var tile in physicalTiles) _renderedPhysicalTileIds.Add(tile.FineStrategicCellId.Value);
        if (selectedPhysical.Count != physicalTiles.Count)
            throw new InvalidOperationException("Duplicated physical tile identity.");
        _polygons.Clear();
        _tileCenters.Clear();
        _framingPolygons.Clear();
        float lowX = float.MaxValue, highX = float.MinValue;
        float lowZ = float.MaxValue, highZ = float.MinValue;
        var faceTool = new SurfaceTool();
        faceTool.Begin(Mesh.PrimitiveType.Triangles);
        var geometry = fineWorld.StrategicSphericalGeometry;
        // Lazy one-time V2 preview. Worldgen V1 and strategic presentation remain unchanged.
        if (_showExperimentalTerrain && _terrainReference is null &&
            _authoritativeEconomyTerrainWorld is null)
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
                if (_authoritativeEconomyTerrainWorld is not null)
                    tint = EconomicTerrainTint(id.FineStrategicCellId.Value, physical);
                else
                {
                    if (_terrainReference is null)
                        throw new InvalidOperationException("Experimental terrain reference missing.");
                    tint = TerrainColor(_terrainReference.Biomes[index].VisibleBiome);
                }
            }
            else
            {
                // Diagnostic mode keeps per-incidence coloring without central-focus bias.
                tint = Color.FromHsv(((int)owners[0].Value % 11) / 11f, 0.27f, 0.70f);
            }
            var center = Flatten(cell.Center);
            _tileCenters.Add(id.FineStrategicCellId.Value, center);
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
            var first = Flatten(geometry.GetVertex(edge.IncidentVertexIds[0]).Position);
            var second = Flatten(geometry.GetVertex(edge.IncidentVertexIds[1]).Position);
            AddColored(lineTool, first, lineColor);
            AddColored(lineTool, second, lineColor);
            edgeCount++;
        }
        var lineMesh = lineTool.Commit();
        lineTool.Dispose();
        if (lineMesh is null || edgeCount == 0)
            throw new InvalidOperationException("Canonical physical edge visualization missing.");

        // Mesh-derived MVP border paths: canonical physical centers only.
        // Presentation boundary is not an obstacle or a territory ownership rewrite.
        bool auditedLegacyBorders =
            refinement.CoarseParameters == new GoldbergParameters(3, 0) &&
            refinement.FineParameters == new GoldbergParameters(18, 0);
        // The historical five-center border materializer has its OWN G3/G18
        // fixture contract. An unsupported decorative overlay must never
        // prevent a different signed physical world from opening.
        var boundaryPaths = auditedLegacyBorders
            ? _cachedBoundaryPaths ??=
                PhysicalTacticalTerritoryBoundaryMaterializer.Materialize(physical)
            : Array.Empty<PhysicalTacticalTerritoryBoundaryPath>();
        var boundaryTool = new SurfaceTool();
        boundaryTool.Begin(Mesh.PrimitiveType.Lines);
        var boundaryTint = new Color("#84969a");
        int strategicEdgesRendered = 0;
        int boundarySegments = 0;
        const float surfaceEpsilon = TacticalSurfacePlaneY;
        foreach (var path in boundaryPaths)
        {
            var coarseEdge = physical.CoarseTopology.Edges[checked((int)path.StrategicEdgeId.Value - 1)];
            if (!coarseEdge.IncidentCellIds.Any(selectedStrategic.Contains)) continue;
            // VISUAL-ONLY SHAPE R1: 3-owner junction -> 2-owner edge midpoint ->
            // 3-owner junction. Do not change physical traversal or territory incidence.
            if (path.FineCellIds.Count != 5)
                throw new InvalidOperationException("Territory visual anchors require the audited five-center MVP path.");
            var visualAnchors = new[]
            {
                path.FineCellIds[0],
                path.FineCellIds[2],
                path.FineCellIds[4]
            };
            var midpointOwners = physical.TileIncidences[checked((int)visualAnchors[1].Value - 1)]
                .IncidentCoarseCellIds;
            if (!midpointOwners.SequenceEqual(coarseEdge.IncidentCellIds))
                throw new InvalidOperationException("Visual territory midpoint must share the expected two strategic cells.");
            for (int junctionIndex = 0; junctionIndex < 2; junctionIndex++)
            {
                var expectedJunction = physical.CoarseTopology.Vertices[
                    checked((int)coarseEdge.IncidentVertexIds[junctionIndex].Value - 1)].IncidentCellIds;
                var actualJunction = physical.TileIncidences[
                    checked((int)visualAnchors[junctionIndex * 2].Value - 1)].IncidentCoarseCellIds;
                if (!actualJunction.SequenceEqual(expectedJunction))
                    throw new InvalidOperationException("Visual territory junction must match the expected three-way incidence.");
            }
            bool contributed = false;
            for (int index = 1; index < visualAnchors.Length; index++)
            {
                var firstId = visualAnchors[index - 1];
                var secondId = visualAnchors[index];
                // Only overlay physical cells actually resident in this chart.
                if (!selectedPhysical.Contains(firstId) || !selectedPhysical.Contains(secondId)) continue;
                var firstCenter = geometry.GetCell(firstId).Center;
                var secondCenter = geometry.GetCell(secondId).Center;
                if (_equidistant &&
                    (ConvertPoint(firstCenter).Dot(_surfaceNormal) <= 0.00001f ||
                     ConvertPoint(secondCenter).Dot(_surfaceNormal) <= 0.00001f)) continue;
                AddColored(boundaryTool, Flatten(firstCenter, surfaceEpsilon), boundaryTint);
                AddColored(boundaryTool, Flatten(secondCenter, surfaceEpsilon), boundaryTint);
                boundarySegments++;
                contributed = true;
            }
            if (contributed) strategicEdgesRendered++;
        }
        var boundaryMesh = boundaryTool.Commit();
        boundaryTool.Dispose();
        if (auditedLegacyBorders &&
            (boundaryMesh is null || strategicEdgesRendered == 0 ||
             boundarySegments == 0))
            throw new InvalidOperationException(
                "Audited G3/G18 strategic border overlay is empty.");
        if (!auditedLegacyBorders && boundarySegments != 0)
            throw new InvalidOperationException(
                "Unsupported strategic boundary may not invent decorative lines.");

        var oldSurface = _surface.Mesh;
        var oldEdges = _edges.Mesh;
        var oldBoundaries = _territoryBoundaries.Mesh;
        _surface.Mesh = faceMesh;
        _edges.Mesh = lineMesh;
        _territoryBoundaries.Mesh = boundaryMesh;
        _territoryBoundaries.Visible =
            _showTerritoryBoundaries && auditedLegacyBorders &&
            boundaryMesh is not null;
        oldSurface?.Dispose();
        oldEdges?.Dispose();
        oldBoundaries?.Dispose();
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
        // Constrain the on-map status to concise navigation data. Full diagnostics stay in GD.Print.
        _information.Text = $"TACTICAL | territory {focusValue} | G({refinement.FineParameters.M},{refinement.FineParameters.N}) | {(_equidistant ? "EQUIDISTANT" : "DIRECT")} | frame {_ringCount}";
        RebuildWorkerMarkers();
        // Same private closed-cycle evidence, reprojected at the new focus
        // and ring size. No rerun of the economic solver or publication.
        RebuildSettledTradeMesh();
        // Recompute the presentation-only selection outline after a ring or projection change.
        // A tile outside the new region is deselected rather than inventing an alias.
        SelectPhysicalTile(_selectedFineTileId);
        GD.Print($"GA_TACTICAL_LOCAL_OPEN focus={focusValue} framing_rings={_ringCount} resident_rings={_residentRingCount} frame_tiles={framingTiles.Count} projection={(_equidistant ? "Equidistant" : "Orthographic")} regions={selectedStrategic.Count} physical_tiles={selectedPhysical.Count} triangles={countTriangles} edges={edgeCount} parameters=G({refinement.CoarseParameters.M},{refinement.CoarseParameters.N})>G({refinement.FineParameters.M},{refinement.FineParameters.N}) duplicate_ids=0");
        GD.Print($"GA_TACTICAL_TERRITORY_BORDERS_MESH_R1 focus={focusValue} strategic_edges={strategicEdgesRendered} segments={boundarySegments} source=physical_centers surface_epsilon=0 toggle={_showTerritoryBoundaries} movement_unchanged=true");
        GD.Print($"GA_TACTICAL_TERRITORY_SHAPE_ANCHORS_R1 focus={focusValue} anchors=3-2-3 segments={boundarySegments} feature=visual_only highlight=false movement_unchanged=true");
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
            if (_cachedPhysicalMap is not null)
                CanonicalPhysicalTileClicked?.Invoke(
                    new PhysicalTacticalTileId(
                        _cachedPhysicalMap.Refinement.FineParameters,
                        new StrategicCellId(item.Id)));
            // The dedicated badge hit-test can select the second/third
            // individual on this same tile AFTER the tile-level callback.
            PickWorkerBadge(item.Id, point);
            // A deliberate click (not mouse movement) opens the persistent
            // inhabited-tile menu. Empty tiles close it and still remain
            // available as ordinary fine movement destinations.
            if (_tileClickHypothesis)
                OpenTileClickMenu(item.Id, localPoint);
            GD.Print($"GA_TACTICAL_LOCAL_PICK fine_cell={item.Id} parameters=G(18,0) degree={item.Degree} ring={item.Ring} strategic_incidence={string.Join("-", item.Owners)} highlight=true");
            return;
        }
        SelectPhysicalTile(null);
        HoverWorkerBadge(null);
        if (_tileClickHypothesis) HideTileClickMenu();
        _selection.Text = "No physical tile under cursor";
    }


    // CI-only interaction probe. It deliberately holds one simulated mouse
    // gesture across two adjacent strategic territories. This checks the
    // same _GuiInput handler used by real users, not a mock movement method.
    internal void ExerciseContinuousNavigationSmoke()
    {
        if (_camera is null || _cachedCoarseGeometry is null ||
            _cachedPhysicalMap is null || _innerViewport is null)
            throw new InvalidOperationException("Continuous nav smoke requires the real map.");
        var source = _activeFocus;
        var zoom = _camera.Size;
        var yaw = _yawDegrees;
        var initialHandoffs = _continuousNavigationHandoffs;
        var centerPixel = new Vector2(_innerViewport.Size.X * 0.5f,
            _innerViewport.Size.Y * 0.5f);
        var motionPixel = centerPixel + new Vector2(15, 15);
        _GuiInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true, Position = centerPixel
        });
        try
        {
            for (int crossing = 0; crossing < 2; crossing++)
            {
                var previousFocus = _activeFocus;
                var adjacent = _cachedPhysicalMap.CoarseTopology.Cells[
                    checked((int)previousFocus - 1)].AdjacentCellIds
                    .OrderBy(id => id.Value)
                    .FirstOrDefault(id => id.Value != source);
                if (adjacent.Value == 0)
                    throw new InvalidOperationException("No second navigation neighbor.");
                var desired = Flatten(_cachedCoarseGeometry.GetCell(adjacent).Center);
                var direction = _camera.UnprojectPosition(desired) -
                    _camera.UnprojectPosition(_cameraTarget);
                if (direction.LengthSquared() < 0.001f)
                    throw new InvalidOperationException("Navigation direction collapsed.");
                // Drag the map opposite the desired destination.
                var relative = -direction.Normalized() * 12f;
                bool crossed = false;
                for (int step = 0; step < 140; step++)
                {
                    _GuiInput(new InputEventMouseMotion
                    {
                        Position = motionPixel, Relative = relative
                    });
                    if (!_pressed || !_moved)
                        throw new InvalidOperationException("Drag capture was lost during handoff.");
                    if (_camera.Size != zoom || _yawDegrees != yaw)
                        throw new InvalidOperationException("Drag changed zoom/yaw.");
                    if (_coverViewport &&
                        !ViewportSamplesHaveTerrain(_camera.Size, 4, 4))
                        throw new InvalidOperationException("Unrendered viewport corner during pan.");
                    if (_activeFocus != previousFocus)
                    {
                        crossed = true;
                        break;
                    }
                }
                if (!crossed)
                    throw new InvalidOperationException(
                        $"Focus never handed off during drag from {previousFocus}.");
            }
        }
        finally
        {
            _GuiInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left, Pressed = false, Position = motionPixel
            });
        }
        if (_continuousNavigationHandoffs - initialHandoffs < 2 ||
            _activeFocus == source || _pressed)
            throw new InvalidOperationException(
                "Continuous multi-focus mouse gesture was not completed.");
        GD.Print($"GA_NEXT_CONTINUOUS_NAVIGATION_INPUT_PASS start={source} " +
            $"end={_activeFocus} handoffs={_continuousNavigationHandoffs - initialHandoffs} " +
            "one_held_drag=true zoom_preserved=true no_empty_corners=true");
    }

    // R98: exercise the exact optical-center focus on the real event path.
    // The map must move continuously WITHIN a single strategic chart:
    // neither a tile center nor a handoff may be required for orientation.
    internal void ExerciseContinuousSphericalFocusSmoke()
    {
        if (_camera is null || _innerViewport is null || _activeFocus == 0UL)
            throw new InvalidOperationException("R98 requires a real camera.");
        ulong initialFocus = _activeFocus;
        float zoom = _camera.Size;
        float yaw = _yawDegrees;
        bool mode = _geographicNorthUp;
        var initialExact = _exactViewPointOnSphere;
        var center = new Vector2(_innerViewport.Size.X * 0.5f,
            _innerViewport.Size.Y * 0.5f);
        try
        {
            _geographicNorthUp = true;
            UpdateCameraOrientation();
            _GuiInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left, Pressed = true, Position = center
            });
            _GuiInput(new InputEventMouseMotion
            {
                Position = center + new Vector2(12f, 0f),
                Relative = new Vector2(12f, 0f)
            });
            var afterFirst = _exactViewPointOnSphere;
            _GuiInput(new InputEventMouseMotion
            {
                Position = center + new Vector2(24f, 0f),
                Relative = new Vector2(12f, 0f)
            });
            var afterSecond = _exactViewPointOnSphere;
            if (!afterFirst.IsFinite() || !afterSecond.IsFinite() ||
                afterFirst.LengthSquared() < 0.99f ||
                afterSecond.LengthSquared() < 0.99f ||
                afterFirst.DistanceTo(initialExact) < 1e-6f ||
                afterSecond.DistanceTo(afterFirst) < 1e-6f ||
                _camera.Size != zoom || _yawDegrees != yaw)
                throw new InvalidOperationException(
                    "Exact sphere view focus must change on each mouse motion.");
            GD.Print("GA_NEXT_R98_EXACT_SPHERE_FOCUS_PASS " +
                $"initial_chart={initialFocus} ending_chart={_activeFocus} " +
                "per_motion=true no_tile_center_snap=true preserved_zoom=true");
        }
        finally
        {
            _GuiInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left, Pressed = false,
                Position = center + new Vector2(24f, 0f)
            });
            _geographicNorthUp = mode;
            Open(initialFocus);
            _camera.Size = zoom;
            _yawDegrees = yaw;
            UpdateCameraOrientation();
        }
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
            // A camera interaction must dismiss the transient inspector;
            // selected worker, selected tile and authority stay untouched.
            if (button.Pressed && (button.ButtonIndex == MouseButton.WheelUp ||
                    button.ButtonIndex == MouseButton.WheelDown ||
                    button.ButtonIndex == MouseButton.Left))
                HoverWorkerBadge(null);
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
                    _previousHandoffFocus = 0UL;
                    _dragPixelsAfterHandoff = 0f;
                    _dragStart = button.Position;
                }
                else
                {
                    if (_pressed && !_moved) PickPhysicalTile(button.Position);
                    _pressed = false;
                    _previousHandoffFocus = 0UL;
                    _dragPixelsAfterHandoff = 0f;
                }
                AcceptEvent();
            }
            return;
        }
        if (@event is InputEventMouseMotion hoverMotion && !_pressed)
        {
            // Click-only prototype: no tooltip/menus or role expansion by
            // hovering, even above an existing coplanar worker marker.
            if (!_tileClickHypothesis)
                HoverWorkerBadge(hoverMotion.Position);
            return;
        }
        if (@event is InputEventMouseMotion motion && _pressed)
        {
            _dragPixelsAfterHandoff += motion.Relative.Length();
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
                var previousTarget = _cameraTarget;
                _cameraTarget += translation;
                // Refresh spherical focus and geographic north on EVERY
                // drag update, without tile search or mesh regeneration.
                UpdateCameraOrientation();

                // Transfer the map to the nearest strategic focus BEFORE
                // enforcing viewport coverage, never at the map's visible edge.
                // The drag gesture stays active throughout the transition.
                var transferred = TryContinuousFocusHandoff(motion.Position);
                if (!transferred && _coverViewport &&
                    !ViewportSamplesHaveTerrain(_camera.Size, 4, 4))
                {
                    _cameraTarget = previousTarget;
                    UpdateCameraOrientation();
                    GD.Print("GA_TACTICAL_NAV_COVERAGE_GUARD_R45 no_missing_tiles=true");
                }
                AcceptEvent();
            }
        }
    }
}
