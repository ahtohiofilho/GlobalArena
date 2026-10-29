using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Visual-only worker rings in the EXISTING flattened tactical renderer.
/// Physical IDs come from EconomicPhysicalTacticalWorkerSession and must match
/// its G(3,0)->G(18,0) incidence hierarchy; this class never changes game state.
/// </summary>
public partial class TacticalContextPreview
{
    private MeshInstance3D? _workerRings;
    private ulong? _workerSource;
    private ulong? _workerDestination;
    private ulong? _workerHovered;
    private HashSet<ulong> _workerEligible = new();
    public event Action<ulong?>? WorkerTileHovered;

    public bool HasVisiblePhysicalTile(ulong id) =>
        _polygons.Any(p => p.Id == id);

    public void SetWorkerRings(ulong? source, ulong? destination,
        IEnumerable<PhysicalTacticalTileId> eligible)
    {
        ArgumentNullException.ThrowIfNull(eligible);
        var parsed = new HashSet<ulong>();
        foreach (var tile in eligible)
        {
            if (!tile.IsValid || tile.FineGoldbergParameters != new GoldbergParameters(18, 0))
                throw new ArgumentException("Worker ring must use a G(18,0) physical tile.", nameof(eligible));
            parsed.Add(tile.FineStrategicCellId.Value);
        }
        _workerSource = source;
        _workerDestination = destination;
        _workerEligible = parsed;
        RefreshWorkerRings();
    }

    public void ClearWorkerRings()
    {
        _workerSource = null;
        _workerDestination = null;
        _workerHovered = null;
        _workerEligible.Clear();
        RefreshWorkerRings();
    }

    private void RefreshWorkerRings()
    {
        if (_world is null) return;
        if (_workerRings is null)
        {
            _workerRings = new MeshInstance3D
            {
                Name = "GA_VS05_PhysicalWorkerRings",
                MaterialOverride = MakeMaterial()
            };
            _world.AddChild(_workerRings);
        }
        var old = _workerRings.Mesh;
        _workerRings.Mesh = null;
        old?.Dispose();

        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        var colored = 0;
        foreach (var polygon in _polygons)
        {
            Color color;
            float width;
            if (_workerSource == polygon.Id)
            {
                color = new Color("#29e8ff"); width = 0.18f;
            }
            else if (_workerDestination == polygon.Id)
            {
                color = new Color("#ff9d3b"); width = 0.18f;
            }
            else if (_workerEligible.Contains(polygon.Id))
            {
                color = new Color("#baff6c"); width = 0.075f;
            }
            else if (_workerHovered == polygon.Id)
            {
                color = new Color("#ffffff"); width = 0.065f;
            }
            else continue;

            var center = Vector2.Zero;
            foreach (var vertex in polygon.Polygon) center += vertex;
            center /= polygon.Polygon.Length;

            for (int index = 0; index < polygon.Polygon.Length; index++)
            {
                var a = polygon.Polygon[index];
                var b = polygon.Polygon[(index + 1) % polygon.Polygon.Length];
                var innerA = a.Lerp(center, width);
                var innerB = b.Lerp(center, width);
                // Same flattened X/Z coordinate system as TacticalContextPreview.
                // Y remains slightly above all ordinary edges for clarity.
                AddColored(tool, new Vector3(a.X, 0.012f, a.Y), color);
                AddColored(tool, new Vector3(b.X, 0.012f, b.Y), color);
                AddColored(tool, new Vector3(innerB.X, 0.012f, innerB.Y), color);
                AddColored(tool, new Vector3(a.X, 0.012f, a.Y), color);
                AddColored(tool, new Vector3(innerB.X, 0.012f, innerB.Y), color);
                AddColored(tool, new Vector3(innerA.X, 0.012f, innerA.Y), color);
                colored += 2;
            }
        }
        if (colored > 0)
        {
            var mesh = tool.Commit();
            if (mesh is null) throw new InvalidOperationException("Physical worker ring mesh missing.");
            _workerRings.Mesh = mesh;
        }
        tool.Dispose();
        _workerRings.Visible = colored > 0;
    }

    // Tooltip hover identifies the physical tile without changing the selected
    // source or destination. A planar hit-test respects camera pan/yaw/zoom.
    private void HoverPhysicalWorkerTile(Vector2 cursor)
    {
        if (_workerSource is null || _camera is null) return;
        var origin = _camera.ProjectRayOrigin(cursor);
        var direction = _camera.ProjectRayNormal(cursor);
        if (Math.Abs(direction.Y) < 1e-6f) return;
        var distance = -origin.Y / direction.Y;
        ulong? next = null;
        if (distance >= 0f)
        {
            var p = origin + direction * distance;
            var test = new Vector2(p.X, p.Z);
            foreach (var polygon in _polygons)
                if (Contains(test, polygon.Polygon))
                {
                    next = polygon.Id;
                    break;
                }
        }
        if (_workerHovered == next) return;
        _workerHovered = next;
        WorkerTileHovered?.Invoke(next);
        RefreshWorkerRings();
    }
}
