using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-NEXT Workforce Visual 03: restores the actual GA-VS16 badge language
/// (coplanar filled disks, ink rims, selection halo, spatial group layout).
/// The old economic activity grouping MUST NOT be resurrected as fake data:
/// v10 currently stores individual identity, commodity and Producing/Traveling
/// /Military, not Forestry/Mining/Agriculture allocations. Here every badge
/// corresponds to exactly one canonical individual; when three share a tile
/// they appear in the three-slot VS16 formation. No additional worker exists.
/// </summary>
public partial class TacticalContextPreview
{
    private const float WorkerBadgePlaneY = 0f; // no physical elevation or parallax
    private MeshInstance3D? _workerBadgeMesh;
    private Label? _workerBadgeTooltip;
    private IReadOnlyList<PhysicalWorkerRuntimeRecord> _displayWorkers =
        Array.Empty<PhysicalWorkerRuntimeRecord>();
    private readonly Dictionary<ulong, (ulong FineTileId, Vector3 Center)>
        _workerGlyphPositions = new();
    private readonly Dictionary<ulong, Vector2> _badgeCenters = new();
    private readonly List<(ulong WorkerId, ulong TileId, Vector2 Center, float Radius)>
        _badgeHits = new();
    private ulong? _selectedWorkerGlyphId;
    private ulong? _hoveredBadgeId;

    /// <summary>Distinguish a click directly on an individual badge from a
    /// generic click on a polygon. The event is not a gameplay command.</summary>
    public event Action<ulong>? CanonicalWorkerBadgeClicked;

    public int VisibleWorkerMarkerCount => _workerGlyphPositions.Count;
    public IReadOnlyList<ulong> VisibleWorkerMarkerIds =>
        Array.AsReadOnly(_workerGlyphPositions.Keys.OrderBy(x => x).ToArray());
    public ulong? HoveredWorkerBadgeId => _hoveredBadgeId;
    public bool IsWorkerHoverVisible => _workerBadgeTooltip?.Visible ?? false;

    private void InitializeWorkerBadges()
    {
        if (_world is null) throw new InvalidOperationException("Tactical world unavailable.");
        _workerBadgeMesh = new MeshInstance3D
        {
            Name = "GANextCanonicalWorkerBadges",
            MaterialOverride = MakeCoplanarOverlayMaterial()
        };
        _world.AddChild(_workerBadgeMesh);
        _workerBadgeTooltip = new Label
        {
            Name = "GANextWorkerBadgeTooltip",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _workerBadgeTooltip.AddThemeColorOverride(
            "font_color", new Color("#fff6d2"));
        _workerBadgeTooltip.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_workerBadgeTooltip);
        MouseExited += () => HoverWorkerBadge(null);
    }

    public bool TryGetRenderedFineTilePixel(PhysicalTacticalTileId tile,
        out Vector2 pixel)
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

    // A badge may be displaced from its tile center when multiple workers
    // coexist. This is the correct center for individual badge selection.
    public bool TryGetWorkerBadgePixel(ulong workerId, out Vector2 pixel)
    {
        pixel = Vector2.Zero;
        if (_camera is null || !_badgeCenters.TryGetValue(workerId, out var center))
            return false;
        pixel = _camera.UnprojectPosition(
            new Vector3(center.X, WorkerBadgePlaneY, center.Y));
        return pixel.X >= 0f && pixel.Y >= 0f &&
            pixel.X < Size.X && pixel.Y < Size.Y;
    }

    public void PresentAuthoritativeWorkers(
        PhysicalWorkerRuntimeState census, ulong? selectedWorkerId)
    {
        ArgumentNullException.ThrowIfNull(census);
        if (!census.IsInitialized ||
            !census.HasAuthoritativeTacticalLocations ||
            census.Workers.Any(worker =>
                worker.TacticalTileId?.FineGoldbergParameters !=
                    new GoldbergParameters(18, 0)))
            throw new InvalidOperationException(
                "Tactical worker badges require a complete canonical G(18,0) census.");
        _displayWorkers = census.Workers.ToArray();
        _selectedWorkerGlyphId = selectedWorkerId;
        RebuildWorkerMarkers();
    }

    private static Color WorkerStatusColor(PhysicalWorkerRuntimeRecord worker) =>
        worker.Activity switch
        {
            PhysicalWorkerActivity.Producing => new Color("#61d98b"),
            PhysicalWorkerActivity.Traveling => new Color("#dfaf60"),
            PhysicalWorkerActivity.Military => new Color("#a8b8c7"),
            PhysicalWorkerActivity.Other => new Color("#b8a9d9"),
            _ => throw new InvalidOperationException("Unknown canonical worker activity.")
        };

    private static void AddBadgeDisk(
        SurfaceTool surface, Vector2 center, float radius, Color color)
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
        var diskCount = 0;
        foreach (var group in _displayWorkers
            .GroupBy(w => w.TacticalTileId!.Value.FineStrategicCellId.Value)
            .OrderBy(g => g.Key))
        {
            if (!_tileCenters.TryGetValue(group.Key, out var center))
                continue; // never move an offscreen worker to a guessed tile
            var polygon = _polygons.First(item => item.Id == group.Key).Polygon;
            var center2 = new Vector2(center.X, center.Z);
            var cellRadius = polygon.Min(point => point.DistanceTo(center2));
            var workers = group.OrderBy(w => w.Id.Value).ToArray();
            // Exact GA-VS16 geometry: 16% of inradius, 37% ring offset.
            // One individual gets the center; two or more get separated
            // badge centers with the first at the north position.
            var radius = cellRadius * 0.16f;
            var offsetRadius = workers.Length == 1 ? 0f :
                cellRadius * 0.37f;
            for (var i = 0; i < workers.Length; i++)
            {
                var worker = workers[i];
                var angle = -Mathf.Pi / 2f +
                    Mathf.Tau * i / workers.Length;
                var location = center2 + new Vector2(Mathf.Cos(angle),
                    Mathf.Sin(angle)) * offsetRadius;
                var selected = worker.Id.Value == _selectedWorkerGlyphId;
                var hovered = worker.Id.Value == _hoveredBadgeId;
                if (selected || hovered)
                {
                    AddBadgeDisk(surface, location,
                        radius * 1.63f, hovered
                            ? new Color("#ffffff") : new Color("#f3f9ff"));
                    diskCount++;
                }
                AddBadgeDisk(surface, location,
                    radius * 1.28f, new Color("#15212b"));
                AddBadgeDisk(surface, location,
                    radius, WorkerStatusColor(worker));
                diskCount += 2;
                _badgeHits.Add((worker.Id.Value, group.Key,
                    location, radius * 1.70f));
                _badgeCenters.Add(worker.Id.Value, location);
                if (!_workerGlyphPositions.TryAdd(worker.Id.Value,
                        (group.Key, center)))
                    throw new InvalidOperationException(
                        "Duplicate canonical individual worker badge.");
            }
        }
        if (diskCount > 0)
        {
            _workerBadgeMesh.Mesh = surface.Commit() ??
                throw new InvalidOperationException("Worker badge mesh missing.");
        }
        surface.Dispose();
        _workerBadgeMesh.Visible = diskCount > 0;
        if (_hoveredBadgeId.HasValue &&
            !_workerGlyphPositions.ContainsKey(_hoveredBadgeId.Value))
        {
            _hoveredBadgeId = null;
            if (_workerBadgeTooltip is not null)
                _workerBadgeTooltip.Visible = false;
        }
        GD.Print($"GA_NEXT_FINE_WORKER_GLYPHS count={_workerGlyphPositions.Count} " +
            $"census={_displayWorkers.Count} focused={_activeFocus} " +
            "source=authoritative_v10 units_not_coarse=true badges=vs16_coplanar");
    }

    private ulong? BadgeAt(ulong tileId, Vector2 point)
    {
        foreach (var badge in _badgeHits)
            if (badge.TileId == tileId &&
                badge.Center.DistanceSquaredTo(point) <=
                    badge.Radius * badge.Radius)
                return badge.WorkerId;
        return null;
    }

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
        var id = BadgeAt(tileId, planePoint);
        if (!id.HasValue) return;
        CanonicalWorkerBadgeClicked?.Invoke(id.Value);
        GD.Print($"GA_NEXT_WORKER_BADGE_PICK worker={id.Value} tile={tileId} " +
            "source=canonical_v10 selection_only=true");
    }

    private void HoverWorkerBadge(Vector2? localPixel)
    {
        ulong? next = null;
        if (localPixel.HasValue &&
            TryBadgePlanePoint(localPixel.Value, out var point, out var tileId))
            next = BadgeAt(tileId, point);

        if (_workerBadgeTooltip is not null)
        {
            _workerBadgeTooltip.Visible = next.HasValue;
            if (next.HasValue && localPixel.HasValue)
            {
                var worker = _displayWorkers.First(w => w.Id.Value == next.Value);
                var address = worker.TacticalTileId!.Value.FineStrategicCellId.Value;
                _workerBadgeTooltip.Text =
                    $"#{worker.Id.Value}  ·  {worker.Activity}  ·  Item {worker.Commodity.Value}  ·  G(18,0)/{address}";
                // Position in the tactical viewport itself, not in the
                // global screen or the movable Tab/atlas windows.
                _workerBadgeTooltip.Position = new Vector2(
                    Mathf.Clamp(localPixel.Value.X + 13f, 0f,
                        Mathf.Max(0f, Size.X - 270f)),
                    Mathf.Clamp(localPixel.Value.Y + 10f, 0f,
                        Mathf.Max(0f, Size.Y - 24f)));
            }
        }
        if (next == _hoveredBadgeId) return;
        _hoveredBadgeId = next;
        RebuildWorkerMarkers(); // hover halo only; no state or camera change
        GD.Print($"GA_NEXT_WORKER_BADGE_HOVER worker={(next?.ToString() ?? "none")} " +
            "mutation=false selection_unchanged=true");
    }
}
