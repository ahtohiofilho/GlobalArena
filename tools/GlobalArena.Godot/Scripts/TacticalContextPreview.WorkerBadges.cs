using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Coplanar VS16 workforce circles, grouped ONLY by actual people with the
/// same fine tile, civilization and canonical productive occupation.
/// Every person remains independently journaled; clicking a group cycles
/// its individual IDs. No dummy circles or counterfeit market activities.
/// </summary>
public partial class TacticalContextPreview
{
    private const float WorkerBadgePlaneY = 0f;
    private MeshInstance3D? _workerBadgeMesh;
    private IReadOnlyList<PhysicalWorkerRuntimeRecord> _displayWorkers =
        Array.Empty<PhysicalWorkerRuntimeRecord>();
    private readonly Dictionary<ulong, (ulong FineTileId, Vector3 Center)>
        _workerGlyphPositions = new();
    private readonly Dictionary<ulong, Vector2> _badgeCenters = new();
    private readonly List<(FoundationWorkforceActivityGroup Group,
        Vector2 Center, float Radius)> _badgeHits = new();
    private readonly Dictionary<(ulong Tile, ulong Owner, bool HasRole, ulong RoleOrId), ulong>
        _lastClickedGroupMember = new();
    private ulong? _selectedWorkerGlyphId;
    private ulong? _hoveredBadgeId;

    public event Action<ulong>? CanonicalWorkerBadgeClicked;

    // Preserve the previous API: the count below is of REAL individuals,
    // not rendered circles. Group count is a separate visual statistic.
    public int VisibleWorkerMarkerCount => _workerGlyphPositions.Count;
    public int VisibleActivityGroupCount => _badgeHits.Count;
    public int TotalVisibleGroupMembers => _badgeHits.Sum(hit => hit.Group.Count);
    public IReadOnlyList<ulong> VisibleWorkerMarkerIds =>
        Array.AsReadOnly(_workerGlyphPositions.Keys.OrderBy(x => x).ToArray());
    public ulong? HoveredWorkerBadgeId => _hoveredBadgeId;
    public bool IsWorkerHoverVisible => IsWorkerInspectorVisible;
    public string? HoveredWorkerBadgeDescription => WorkerInspectorDescription;
    public int ActivityGroupMemberCount(ulong tile, ulong owner,
        PhysicalWorkerOccupation occupation) =>
        _badgeHits.Where(hit =>
            hit.Group.Tile.FineStrategicCellId.Value == tile &&
            hit.Group.Owner.Value == owner &&
            hit.Group.Occupation == occupation).Sum(hit => hit.Group.Count);

    public bool TryGetActivityGroupPixel(ulong tile, ulong owner,
        PhysicalWorkerOccupation occupation, out Vector2 pixel)
    {
        pixel = Vector2.Zero;
        if (_camera is null) return false;
        foreach (var hit in _badgeHits)
            if (hit.Group.Tile.FineStrategicCellId.Value == tile &&
                hit.Group.Owner.Value == owner &&
                hit.Group.Occupation == occupation)
            {
                pixel = _camera.UnprojectPosition(new Vector3(
                    hit.Center.X, WorkerBadgePlaneY, hit.Center.Y));
                return pixel.X >= 0f && pixel.Y >= 0f &&
                    pixel.X < Size.X && pixel.Y < Size.Y;
            }
        return false;
    }

    public Color? RenderedWorkerCivilizationColor(ulong workerId)
    {
        var worker = _displayWorkers.FirstOrDefault(w => w.Id.Value == workerId);
        return worker is not null && _workerGlyphPositions.ContainsKey(workerId)
            ? GaCivilizationVisualIdentity.ColorFor(worker.Owner.Value) : null;
    }

    public Color? RenderedWorkerBadgeFill(ulong workerId)
    {
        var worker = _displayWorkers.FirstOrDefault(w => w.Id.Value == workerId);
        return worker is not null && _workerGlyphPositions.ContainsKey(workerId)
            ? WorkerStatusColor(worker) : null;
    }

    private void InitializeWorkerBadges()
    {
        if (_world is null) throw new InvalidOperationException(
            "Tactical world unavailable.");
        _workerBadgeMesh = new MeshInstance3D
        {
            Name = "GANextCanonicalWorkerBadges",
            MaterialOverride = MakeCoplanarOverlayMaterial()
        };
        _world.AddChild(_workerBadgeMesh);
        InitializeWorkerInspector();
        MouseExited += () => HoverWorkerBadge(null);
    }

    public bool TryGetRenderedFineTilePixel(
        PhysicalTacticalTileId tile, out Vector2 pixel)
    {
        pixel = Vector2.Zero;
        if (_camera is null || !tile.IsValid ||
            _cachedPhysicalMap?.Refinement.FineParameters !=
                tile.FineGoldbergParameters ||
            !_tileCenters.TryGetValue(tile.FineStrategicCellId.Value, out var center))
            return false;
        pixel = _camera.UnprojectPosition(center);
        return pixel.X >= 0f && pixel.Y >= 0f &&
            pixel.X < Size.X && pixel.Y < Size.Y;
    }

    public bool TryGetWorkerMarkerPosition(ulong workerId,
        out ulong fineTileId, out Vector3 position)
    {
        if (_workerGlyphPositions.TryGetValue(workerId, out var marker))
        {
            fineTileId = marker.FineTileId;
            position = marker.Center;
            return true;
        }
        fineTileId = 0UL;
        position = Vector3.Zero;
        return false;
    }

    // Multiple workers in one group intentionally share the same badge
    // screen coordinates. Repeated clicks on it select them in ID order.
    public bool TryGetWorkerBadgePixel(ulong workerId, out Vector2 pixel)
    {
        pixel = Vector2.Zero;
        if (_camera is null || !_badgeCenters.TryGetValue(workerId, out var center))
            return false;
        pixel = _camera.UnprojectPosition(new Vector3(
            center.X, WorkerBadgePlaneY, center.Y));
        return pixel.X >= 0f && pixel.Y >= 0f &&
            pixel.X < Size.X && pixel.Y < Size.Y;
    }

    public void PresentAuthoritativeWorkers(
        PhysicalWorkerRuntimeState census, ulong? selectedWorkerId)
    {
        ArgumentNullException.ThrowIfNull(census);
        var signedFine = _cachedPhysicalMap?.Refinement.FineParameters ??
            _presentationRefinement.FineParameters;
        if (!census.IsInitialized || !census.HasAuthoritativeTacticalLocations ||
            census.Workers.Any(worker =>
                worker.TacticalTileId?.FineGoldbergParameters != signedFine))
            throw new InvalidOperationException(
                "Worker activity markers require physical tiles from " +
                "the SAME signed fine Goldberg topology as this map.");
        _displayWorkers = census.Workers.ToArray();
        _selectedWorkerGlyphId = selectedWorkerId;
        RebuildWorkerMarkers();
        UpdateLocalSiteCensus();
        RefreshWorkerInspectorFromCensus();
        RefreshTileClickMenuAfterSnapshot();
    }

    private static string WorkerOccupationLabel(PhysicalWorkerOccupation occupation) =>
        occupation switch
        {
            PhysicalWorkerOccupation.Agriculture => "Agricultura",
            PhysicalWorkerOccupation.Forestry => "Extração vegetal",
            PhysicalWorkerOccupation.Mining => "Mineração",
            PhysicalWorkerOccupation.LandTransport => "Transporte terrestre",
            PhysicalWorkerOccupation.PortOperations => "Operação portuária",
            PhysicalWorkerOccupation.Unassigned => "Sem profissão produtiva",
            _ => throw new InvalidOperationException("Unknown physical occupation.")
        };

    private static Color WorkerStatusColor(PhysicalWorkerRuntimeRecord worker)
    {
        if (worker.Occupation.HasValue)
            return worker.Occupation.Value switch
            {
                PhysicalWorkerOccupation.Agriculture => new Color("#61d98b"),
                PhysicalWorkerOccupation.Forestry => new Color("#b88656"),
                PhysicalWorkerOccupation.Mining => new Color("#c4c7d4"),
                PhysicalWorkerOccupation.LandTransport => new Color("#4da6ad"),
                PhysicalWorkerOccupation.PortOperations => new Color("#6c9ad1"),
                PhysicalWorkerOccupation.Unassigned => new Color("#7b91aa"),
                _ => throw new InvalidOperationException("Unknown v11 occupation.")
            };
        return worker.Activity switch
        {
            PhysicalWorkerActivity.Producing => new Color("#61d98b"),
            PhysicalWorkerActivity.Traveling => new Color("#dfaf60"),
            PhysicalWorkerActivity.Military => new Color("#a8b8c7"),
            PhysicalWorkerActivity.Other => new Color("#b8a9d9"),
            _ => throw new InvalidOperationException("Unknown worker status.")
        };
    }

    private static void AddBadgeDisk(SurfaceTool surface,
        Vector2 center, float radius, Color color)
    {
        const int segments = 16;
        for (var i = 0; i < segments; i++)
        {
            var a = Mathf.Tau * i / segments;
            var b = Mathf.Tau * (i + 1) / segments;
            var pa = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            var pb = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
            AddColored(surface, new Vector3(center.X, WorkerBadgePlaneY, center.Y), color);
            AddColored(surface, new Vector3(pa.X, WorkerBadgePlaneY, pa.Y), color);
            AddColored(surface, new Vector3(pb.X, WorkerBadgePlaneY, pb.Y), color);
        }
    }

    // Draw exact counts as coplanar seven-segment digits. No floating Label3D
    // or additional 2D screen-space layer that drifts at oblique camera angles.
    // R135: in NORTH UP, camera roll is driven by the geodesic direction,
    // not merely _yawDegrees. Invert the actual X/Z -> screen projection
    // to keep legacy count numerals upright in BOTH orientation modes.
    // The digits remain coplanar with the physical tiles.
    public (Vector2 Right, Vector2 Down) WorkerCountScreenAxes
    {
        get
        {
            if (_camera is null)
                return (Vector2.Left, Vector2.Down);
            var basis = _camera.GlobalTransform.Basis;
            Vector3 right = basis.X;
            Vector3 up = basis.Y;
            float determinant = right.Z * up.X - right.X * up.Z;
            if (Math.Abs(determinant) < 0.00001f)
                throw new InvalidOperationException(
                    "Tactical camera cannot produce upright planar glyphs.");
            var planeRight = new Vector2(-up.Z / determinant,
                up.X / determinant).Normalized();
            var planeDown = new Vector2(-right.Z / determinant,
                right.X / determinant).Normalized();
            return (planeRight, planeDown);
        }
    }

    public bool AreWorkerCountDigitsScreenUpright()
    {
        if (_camera is null || _activeFocus == 0) return false;
        var axes = WorkerCountScreenAxes;
        var origin = _camera.UnprojectPosition(_cameraTarget);
        var right = _camera.UnprojectPosition(_cameraTarget +
            new Vector3(axes.Right.X, 0f, axes.Right.Y));
        var down = _camera.UnprojectPosition(_cameraTarget +
            new Vector3(axes.Down.X, 0f, axes.Down.Y));
        return right.X > origin.X && down.Y > origin.Y &&
            Mathf.Abs(right.Y - origin.Y) < 0.05f &&
            Mathf.Abs(down.X - origin.X) < 0.05f;
    }

    private static void AddCountSegment(
        SurfaceTool surface, Vector2 from, Vector2 to, float thickness)
    {
        var tangent = (to - from).Normalized();
        var n = new Vector2(-tangent.Y, tangent.X) * thickness * 0.5f;
        foreach (var v in new[] { from-n, to-n, to+n, from-n, to+n, from+n })
            AddColored(surface, new Vector3(v.X, WorkerBadgePlaneY, v.Y),
                new Color("#13222c"));
    }

    private void AddBadgeCount(
        SurfaceTool surface, Vector2 center, float radius, int count)
    {
        var digits = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var width = radius * (digits.Length > 2 ? .40f : .57f);
        var height = radius * 1.02f;
        var gap = radius * .14f;
        var thickness = radius * .12f;
        // Express glyph points in a screen-aligned local coordinate frame.
        // The markers themselves remain flush with the tactical surface.
        var start = -(digits.Length * width +
            (digits.Length - 1) * gap) / 2f;
        var top = -height / 2f;
        var axes = WorkerCountScreenAxes;
        Vector2 OnPlane(float horizontal, float vertical) =>
            center + axes.Right * horizontal + axes.Down * vertical;
        var masks = new[] {
            0b0111111, 0b0000110, 0b1011011, 0b1001111, 0b1100110,
            0b1101101, 0b1111101, 0b0000111, 0b1111111, 0b1101111
        };
        for (var i = 0; i < digits.Length; i++)
        {
            var mask = masks[digits[i] - '0'];
            var x = start + i * (width + gap);
            var tl = OnPlane(x, top);
            var tr = OnPlane(x + width, top);
            var ml = OnPlane(x, top + height / 2f);
            var mr = OnPlane(x + width, top + height / 2f);
            var bl = OnPlane(x, top + height);
            var br = OnPlane(x + width, top + height);
            if ((mask & 1) != 0) AddCountSegment(surface, tl, tr, thickness);
            if ((mask & 2) != 0) AddCountSegment(surface, tr, mr, thickness);
            if ((mask & 4) != 0) AddCountSegment(surface, mr, br, thickness);
            if ((mask & 8) != 0) AddCountSegment(surface, bl, br, thickness);
            if ((mask & 16) != 0) AddCountSegment(surface, ml, bl, thickness);
            if ((mask & 32) != 0) AddCountSegment(surface, tl, ml, thickness);
            if ((mask & 64) != 0) AddCountSegment(surface, ml, mr, thickness);
        }
    }

    private void RebuildWorkerMarkers()
    {
        if (_workerBadgeMesh is null) return;
        var oldMesh = _workerBadgeMesh.Mesh;
        _workerBadgeMesh.Mesh = null;
        oldMesh?.Dispose();
        _workerGlyphPositions.Clear();
        _badgeHits.Clear();
        _badgeCenters.Clear();

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        int diskCount = 0;
        var census = new PhysicalWorkerRuntimeState(_displayWorkers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: _displayWorkers.Count > 0 &&
                _displayWorkers.All(w => w.Occupation.HasValue));
        var projected = FoundationWorkforceActivityGroups.Project(census);
        foreach (var tileGroups in projected.GroupBy(
            group => group.Tile.FineStrategicCellId.Value).OrderBy(g => g.Key))
        {
            if (!_tileCenters.TryGetValue(tileGroups.Key, out var center))
                continue;
            var polygon = _polygons.First(x => x.Id == tileGroups.Key).Polygon;
            var center2 = new Vector2(center.X, center.Z);
            var inradius = polygon.Min(p => p.DistanceTo(center2));
            var groups = tileGroups.OrderBy(g => g.Owner.Value)
                .ThenBy(g => g.Occupation.HasValue
                    ? (int)g.Occupation.Value : int.MaxValue)
                .ThenBy(g => g.FirstWorkerId).ToArray();
            var radius = inradius * (groups.Length > 5 ? .12f : .16f);
            var offset = groups.Length == 1 ? 0f :
                inradius * (groups.Length > 5 ? .46f : .37f);
            for (var i = 0; i < groups.Length; i++)
            {
                var group = groups[i];
                var angle = -Mathf.Pi / 2f + Mathf.Tau * i / groups.Length;
                var position = center2 + new Vector2(
                    Mathf.Cos(angle), Mathf.Sin(angle)) * offset;
                var selected = group.Members.Any(w =>
                    w.Id.Value == _selectedWorkerGlyphId);
                var hovered = group.Members.Any(w =>
                    w.Id.Value == _hoveredBadgeId);
                if (selected || hovered)
                {
                    AddBadgeDisk(surface, position, radius * 1.70f,
                        hovered ? new Color("#ffffff") : new Color("#f3f9ff"));
                    diskCount++;
                }
                // Two independent channels: the OUTER rim identifies
                // civilization; the INNER disk identifies occupation.
                // Selection/Travel state overlays remain distinct.
                AddBadgeDisk(surface, position, radius * 1.56f,
                    GaCivilizationVisualIdentity.ColorFor(group.Owner.Value));
                diskCount++;
                if (group.Members.Any(w =>
                    w.Activity == PhysicalWorkerActivity.Traveling))
                {
                    AddBadgeDisk(surface, position, radius * 1.42f,
                        new Color("#dfaf60"));
                    diskCount++;
                }
                AddBadgeDisk(surface, position, radius * 1.28f,
                    new Color("#15212b"));
                AddBadgeDisk(surface, position, radius,
                    WorkerStatusColor(group.Members[0]));
                diskCount += 2;
                if (group.Count > 1)
                {
                    AddBadgeCount(surface, position, radius, group.Count);
                    diskCount++;
                }
                _badgeHits.Add((group, position, radius * 1.70f));
                foreach (var worker in group.Members)
                {
                    _badgeCenters.Add(worker.Id.Value, position);
                    if (!_workerGlyphPositions.TryAdd(worker.Id.Value,
                            (tileGroups.Key, center)))
                        throw new InvalidOperationException(
                            "Canonical worker duplicated in group rendering.");
                }
            }
        }
        if (diskCount > 0)
            _workerBadgeMesh.Mesh = surface.Commit() ??
                throw new InvalidOperationException("Workforce group mesh missing.");
        surface.Dispose();
        // Cartographic mode never renders individual workforce dots; real
        // workers remain available via the operational workforce panels.
        _workerBadgeMesh.Visible = diskCount > 0 && !_localSitesEnabled;
        if (_hoveredBadgeId.HasValue &&
            !_workerGlyphPositions.ContainsKey(_hoveredBadgeId.Value))
        {
            _hoveredBadgeId = null;
            HideWorkerInspector();
        }
        GD.Print($"GA_NEXT_FINE_WORKER_GLYPHS count={_workerGlyphPositions.Count} " +
            $"groups={_badgeHits.Count} census={_displayWorkers.Count} " +
            $"focused={_activeFocus} source=authoritative_fine " +
            "aggregation=tile_owner_occupation no_phantom_workers=true");
    }

    private FoundationWorkforceActivityGroup? BadgeAt(ulong tileId, Vector2 point)
    {
        foreach (var hit in _badgeHits)
            if (hit.Group.Tile.FineStrategicCellId.Value == tileId &&
                hit.Center.DistanceSquaredTo(point) <=
                    hit.Radius * hit.Radius)
                return hit.Group;
        return null;
    }

    private static (ulong Tile, ulong Owner, bool HasRole, ulong RoleOrId)
        GroupClickKey(FoundationWorkforceActivityGroup group) =>
        (group.Tile.FineStrategicCellId.Value, group.Owner.Value,
            group.Occupation.HasValue, group.Occupation.HasValue
                ? (ulong)group.Occupation.Value : group.FirstWorkerId);

    private bool TryBadgePlanePoint(Vector2 pixel,
        out Vector2 point, out ulong tile)
    {
        point = Vector2.Zero;
        tile = 0UL;
        if (_camera is null) return false;
        var origin = _camera.ProjectRayOrigin(pixel);
        var ray = _camera.ProjectRayNormal(pixel);
        if (Math.Abs(ray.Y) < 1e-6f) return false;
        var length = -origin.Y / ray.Y;
        if (length < 0f) return false;
        var hit = origin + ray * length;
        point = new Vector2(hit.X, hit.Z);
        foreach (var polygon in _polygons)
            if (Contains(point, polygon.Polygon))
            {
                tile = polygon.Id;
                return true;
            }
        return false;
    }

    private void PickWorkerBadge(ulong tileId, Vector2 planePoint)
    {
        var group = BadgeAt(tileId, planePoint);
        if (group is null) return;
        var key = GroupClickKey(group);
        int index = 0;
        if (_lastClickedGroupMember.TryGetValue(key, out var prior))
        {
            var previous = Array.FindIndex(group.Members.ToArray(),
                member => member.Id.Value == prior);
            if (previous >= 0) index = (previous + 1) % group.Count;
        }
        var workerId = group.Members[index].Id.Value;
        _lastClickedGroupMember[key] = workerId;
        CanonicalWorkerBadgeClicked?.Invoke(workerId);
        GD.Print($"GA_NEXT_WORKFORCE_GROUP_CLICK group_count={group.Count} " +
            $"role={(group.Occupation?.ToString() ?? "v10_individual")} " +
            $"selected_worker={workerId} tile={tileId} " +
            "source=canonical_worker selection_only=true");
    }

    private void HoverWorkerBadge(Vector2? localPixel)
    {
        FoundationWorkforceActivityGroup? group = null;
        if (localPixel.HasValue &&
            TryBadgePlanePoint(localPixel.Value, out var point, out var tileId))
            group = BadgeAt(tileId, point);

        ulong? next = null;
        if (group is not null && localPixel.HasValue)
        {
            var worker = group.Members.FirstOrDefault(w =>
                w.Id.Value == _selectedWorkerGlyphId) ?? group.Members[0];
            next = worker.Id.Value;
            ShowWorkerInspector(worker, localPixel.Value, group.Members);
        }
        else
            HideWorkerInspector();

        if (next == _hoveredBadgeId) return;
        _hoveredBadgeId = next;
        RebuildWorkerMarkers();
        GD.Print($"GA_NEXT_WORKER_BADGE_HOVER worker={(next?.ToString() ?? "none")} " +
            $"group_count={group?.Count ?? 0} mutation=false " +
            "selection_unchanged=true inspector=non_modal");
    }
}
