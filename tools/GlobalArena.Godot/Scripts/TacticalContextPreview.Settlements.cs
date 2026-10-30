using System;
using System.Linq;
using GlobalArena.Simulation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// VS20: one restrained halo per genuinely inhabited tactical tile.
/// Existing colored workforce badges remain selectable. Geometry derives
/// from the same canonical 01C polygons as all other tactical overlays.
/// </summary>
public partial class TacticalContextPreview
{
    private const float SettlementHaloHeight = 0.00069f;
    private MeshInstance3D? _settlementHalos;
    private EmergentSettlementProjection? _settlementProjection;

    public void SetSettlementSites(EmergentSettlementProjection? projection)
    {
        _settlementProjection = projection;
        RefreshSettlementHalos();
    }

    private static void AddSettlementHalo(SurfaceTool tool, Vector2 center,
        float radius, float halfWidth, Color tint)
    {
        const int steps = 20;
        for (var i = 0; i < steps; i++)
        {
            var a = Mathf.Tau * i / steps;
            var b = Mathf.Tau * (i + 1) / steps;
            var dirA = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var dirB = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            var outerA = center + dirA * (radius + halfWidth);
            var innerA = center + dirA * (radius - halfWidth);
            var outerB = center + dirB * (radius + halfWidth);
            var innerB = center + dirB * (radius - halfWidth);
            AddColored(tool, new Vector3(outerA.X, SettlementHaloHeight, outerA.Y), tint);
            AddColored(tool, new Vector3(innerA.X, SettlementHaloHeight, innerA.Y), tint);
            AddColored(tool, new Vector3(outerB.X, SettlementHaloHeight, outerB.Y), tint);
            AddColored(tool, new Vector3(outerB.X, SettlementHaloHeight, outerB.Y), tint);
            AddColored(tool, new Vector3(innerA.X, SettlementHaloHeight, innerA.Y), tint);
            AddColored(tool, new Vector3(innerB.X, SettlementHaloHeight, innerB.Y), tint);
        }
    }

    private void RefreshSettlementHalos()
    {
        if (_world is null) return;
        _settlementHalos ??= new MeshInstance3D
        {
            Name = "GA_VS20_EmergentSettlements",
            MaterialOverride = MakeMaterial()
        };
        if (_settlementHalos.GetParent() is null) _world.AddChild(_settlementHalos);

        var old = _settlementHalos.Mesh;
        _settlementHalos.Mesh = null;
        old?.Dispose();
        if (_settlementProjection is null || _polygons.Count == 0)
        {
            _settlementHalos.Visible = false;
            return;
        }

        var polygons = _polygons.ToDictionary(x => x.Id, x => x.Polygon);
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var rings = 0;
        foreach (var site in _settlementProjection.Sites)
        {
            if (!polygons.TryGetValue(site.Tile.FineStrategicCellId.Value,
                    out var polygon)) continue;
            var center = CenterOf(polygon);
            var inradius = RadiusOf(polygon, center);
            // Small and muted: no political border, no invented buildings,
            // and no obstruction of the existing selectable worker badges.
            AddSettlementHalo(surface, center, inradius * 0.68f,
                inradius * 0.027f, new Color("#d9c27d"));
            rings++;
        }
        if (rings > 0) _settlementHalos.Mesh = surface.Commit();
        surface.Dispose();
        _settlementHalos.Visible = rings > 0;
    }
}
