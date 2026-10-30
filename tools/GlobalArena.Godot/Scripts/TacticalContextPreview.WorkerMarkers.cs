using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-VS16: selectable workforce-group markers and turn-progress route markers.
/// All geometry uses the EXISTING physical tactical polygons and terrain plane.
/// No worker entities or movement state are invented by this view.
/// </summary>
public partial class TacticalContextPreview
{
    private const float WorkerBadgeSurfaceLift = 0.00078f;
    private const float WorkerPathSurfaceLift = 0.00065f;
    private MeshInstance3D? _workerBadges;
    private MeshInstance3D? _workerPaths;
    private TacticalWorkerMarkerProjection? _workerProjection;
    private ulong? _selectedMarkerTile;
    private EconomicActivityKind? _selectedMarkerActivity;
    private readonly List<(ulong Tile, EconomicActivityKind Activity,
        Vector2 Center, float Radius)> _workerBadgeHits = new();

    /// <summary>Raised only by a click directly on a colored workforce group marker.</summary>
    public event Action<ulong, EconomicActivityKind>? WorkerGroupPicked;

    public void SetWorkerMarkers(TacticalWorkerMarkerProjection? projection,
        ulong? selectedTile, EconomicActivityKind? selectedActivity)
    {
        _workerProjection = projection;
        _selectedMarkerTile = selectedTile;
        _selectedMarkerActivity = selectedActivity;
        RefreshWorkerMarkers();
    }

    private static Color WorkerActivityColor(EconomicActivityKind activity) =>
        activity switch
        {
            EconomicActivityKind.Agriculture => new Color("#61d98b"),
            EconomicActivityKind.Forestry => new Color("#b88656"),
            EconomicActivityKind.Mining => new Color("#c4c7d4"),
            _ => new Color("#ffffff")
        };

    private static Vector2 CenterOf(Vector2[] polygon)
    {
        var sum = Vector2.Zero;
        foreach (var vertex in polygon) sum += vertex;
        return sum / polygon.Length;
    }

    private static float RadiusOf(Vector2[] polygon, Vector2 center)
    {
        var min = float.PositiveInfinity;
        foreach (var vertex in polygon)
            min = MathF.Min(min, center.DistanceTo(vertex));
        return min;
    }

    private static void AddDisk(SurfaceTool surface, Vector2 center,
        float radius, float height, Color tint)
    {
        const int segments = 12;
        for (var i = 0; i < segments; ++i)
        {
            var angleA = Mathf.Tau * i / segments;
            var angleB = Mathf.Tau * (i + 1) / segments;
            var a = center + new Vector2(Mathf.Cos(angleA), Mathf.Sin(angleA)) * radius;
            var b = center + new Vector2(Mathf.Cos(angleB), Mathf.Sin(angleB)) * radius;
            AddColored(surface, new Vector3(center.X, height, center.Y), tint);
            AddColored(surface, new Vector3(a.X, height, a.Y), tint);
            AddColored(surface, new Vector3(b.X, height, b.Y), tint);
        }
    }

    private static void AddRouteRibbon(SurfaceTool surface,
        Vector2 from, Vector2 to, float halfWidth, Color tint)
    {
        var direction = to - from;
        if (direction.LengthSquared() < 1e-10f) return;
        var normal = new Vector2(-direction.Y, direction.X).Normalized() * halfWidth;
        var a = from + normal;
        var b = from - normal;
        var c = to + normal;
        var d = to - normal;
        AddColored(surface, new Vector3(a.X, WorkerPathSurfaceLift, a.Y), tint);
        AddColored(surface, new Vector3(b.X, WorkerPathSurfaceLift, b.Y), tint);
        AddColored(surface, new Vector3(c.X, WorkerPathSurfaceLift, c.Y), tint);
        AddColored(surface, new Vector3(c.X, WorkerPathSurfaceLift, c.Y), tint);
        AddColored(surface, new Vector3(b.X, WorkerPathSurfaceLift, b.Y), tint);
        AddColored(surface, new Vector3(d.X, WorkerPathSurfaceLift, d.Y), tint);
    }

    private void RefreshWorkerMarkers()
    {
        _workerBadgeHits.Clear();
        if (_world is null) return;

        _workerBadges ??= new MeshInstance3D
        {
            Name = "GA_VS16_WorkerGroupMarkers",
            MaterialOverride = MakeMaterial()
        };
        if (_workerBadges.GetParent() is null) _world.AddChild(_workerBadges);

        _workerPaths ??= new MeshInstance3D
        {
            Name = "GA_VS16_WorkerTravelPaths",
            MaterialOverride = MakeMaterial()
        };
        if (_workerPaths.GetParent() is null) _world.AddChild(_workerPaths);

        var priorBadges = _workerBadges.Mesh;
        var priorPaths = _workerPaths.Mesh;
        _workerBadges.Mesh = null;
        _workerPaths.Mesh = null;
        priorBadges?.Dispose();
        priorPaths?.Dispose();

        if (_workerProjection is null || _polygons.Count == 0)
        {
            _workerBadges.Visible = false;
            _workerPaths.Visible = false;
            return;
        }

        var localPolygons = _polygons.ToDictionary(x => x.Id, x => x.Polygon);
        var badges = new SurfaceTool();
        var paths = new SurfaceTool();
        badges.Begin(Mesh.PrimitiveType.Triangles);
        paths.Begin(Mesh.PrimitiveType.Triangles);
        var badgeTriangles = 0;
        var pathTriangles = 0;

        foreach (var tileGroups in _workerProjection.Groups.GroupBy(x => x.Tile))
        {
            var id = tileGroups.Key.FineStrategicCellId.Value;
            if (!localPolygons.TryGetValue(id, out var polygon)) continue;
            var center = CenterOf(polygon);
            var cellRadius = RadiusOf(polygon, center);
            var groups = tileGroups.OrderBy(x => (byte)x.Activity).ToArray();
            var badgeRadius = cellRadius * 0.16f;
            var offsetRadius = groups.Length == 1 ? 0f : cellRadius * 0.37f;

            for (var index = 0; index < groups.Length; index++)
            {
                var angle = -Mathf.Pi / 2f + Mathf.Tau * index / groups.Length;
                var location = center + new Vector2(Mathf.Cos(angle),
                    Mathf.Sin(angle)) * offsetRadius;
                // A selected worker group receives its own bright halo,
                // distinguishing group selection from the whole-tile ring.
                if (_selectedMarkerTile == id &&
                    _selectedMarkerActivity == groups[index].Activity)
                {
                    AddDisk(badges, location, badgeRadius * 1.63f,
                        WorkerBadgeSurfaceLift, new Color("#f3f9ff"));
                    badgeTriangles += 12;
                }
                AddDisk(badges, location, badgeRadius * 1.28f,
                    WorkerBadgeSurfaceLift + 0.000005f, new Color("#15212b"));
                AddDisk(badges, location, badgeRadius,
                    WorkerBadgeSurfaceLift + 0.00001f,
                    WorkerActivityColor(groups[index].Activity));
                badgeTriangles += 24;
                _workerBadgeHits.Add((id, groups[index].Activity, location,
                    badgeRadius * 1.7f));
            }
        }

        foreach (var journey in _workerProjection.Journeys)
        {
            if (!localPolygons.TryGetValue(journey.Source.FineStrategicCellId.Value,
                    out var fromPolygon) ||
                !localPolygons.TryGetValue(journey.Destination.FineStrategicCellId.Value,
                    out var toPolygon)) continue;

            var from = CenterOf(fromPolygon);
            var to = CenterOf(toPolygon);
            var radius = MathF.Min(RadiusOf(fromPolygon, from),
                RadiusOf(toPolygon, to));
            AddRouteRibbon(paths, from, to, radius * 0.075f,
                new Color("#edb65d"));
            pathTriangles += 2;
            // Each economic movement turn moves the amber marker along the
            // commanded edge. It vanishes at arrival; the active group then
            // appears on the destination tile.
            var fraction = (float)journey.Fraction;
            var position = from.Lerp(to, fraction);
            AddDisk(badges, position, radius * 0.19f,
                WorkerBadgeSurfaceLift + 0.00003f, new Color("#ffd16d"));
            badgeTriangles += 12;
        }

        if (badgeTriangles > 0) _workerBadges.Mesh = badges.Commit();
        if (pathTriangles > 0) _workerPaths.Mesh = paths.Commit();
        badges.Dispose();
        paths.Dispose();
        _workerBadges.Visible = badgeTriangles > 0;
        _workerPaths.Visible = pathTriangles > 0;
    }

    /// <summary>Called from the existing map hit-test after tile inspection.</summary>
    private void PickWorkerGroupMarker(ulong tileId, Vector2 localPoint)
    {
        foreach (var marker in _workerBadgeHits)
        {
            if (marker.Tile != tileId ||
                marker.Center.DistanceSquaredTo(localPoint) > marker.Radius * marker.Radius)
                continue;
            WorkerGroupPicked?.Invoke(marker.Tile, marker.Activity);
            return;
        }
    }
}
