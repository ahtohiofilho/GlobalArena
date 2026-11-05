using System;
using System.Linq;
using GlobalArena.Simulation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-UI-PRODUCTION-STATES-01: purely visual worksite-stage silhouettes.
/// Nothing here invents facilities, camps, production, employment or money.
/// A single overlay per tile summarizes all actual canonical worksite assets.
/// </summary>
public partial class TacticalContextPreview
{
    private const float WorksiteMarkerGround = 0.00081f;
    private MeshInstance3D? _worksiteStageMesh;
    private CanonicalTacticalWorksiteMapProjection? _worksiteMapProjection;

    public void SetWorksiteMapMarkers(CanonicalTacticalWorksiteMapProjection? projection)
    {
        _worksiteMapProjection = projection;
        RefreshWorksiteMapMarkers();
        UpdateSettlementHover(_hoveredSettlementTile, GetLocalMousePosition());
    }

    private static void AddWorksiteBox(SurfaceTool tool, Vector2 center,
        float width, float depth, float elevation, float height, Color shade)
    {
        var x0 = center.X - width * 0.5f;
        var x1 = center.X + width * 0.5f;
        var z0 = center.Y - depth * 0.5f;
        var z1 = center.Y + depth * 0.5f;
        var y0 = WorksiteMarkerGround + elevation;
        var y1 = y0 + height;
        Vector3 P(float x, float y, float z) => new(x,y,z);
        var side = shade.Darkened(0.24f);
        AddSettlementQuad(tool, P(x0,y0,z0),P(x1,y0,z0),
            P(x1,y1,z0),P(x0,y1,z0), shade);
        AddSettlementQuad(tool, P(x1,y0,z1),P(x0,y0,z1),
            P(x0,y1,z1),P(x1,y1,z1), side);
        AddSettlementQuad(tool, P(x0,y0,z1),P(x0,y0,z0),
            P(x0,y1,z0),P(x0,y1,z1), side);
        AddSettlementQuad(tool, P(x1,y0,z0),P(x1,y0,z1),
            P(x1,y1,z1),P(x1,y1,z0), shade);
        AddSettlementQuad(tool, P(x0,y1,z0),P(x1,y1,z0),
            P(x1,y1,z1),P(x0,y1,z1), shade.Lightened(0.14f));
    }

    private static void AddWorksiteStageGlyph(
        SurfaceTool tool, Vector2 tileCenter, float inradius,
        TacticalWorksiteMapStage stage)
    {
        // Beside, never on top of, the two-house settlement glyph. A worksite
        // remains visible even if its resident workers leave the tile.
        var r = inradius;
        var center = tileCenter + new Vector2(r * 0.48f, -r * 0.23f);
        var width = r * 0.22f;
        var depth = r * 0.20f;
        var baseColor = stage switch
        {
            TacticalWorksiteMapStage.MaterialFrame => new Color("#97816c"),
            TacticalWorksiteMapStage.UnderConstruction => new Color("#e6ad55"),
            TacticalWorksiteMapStage.Completed => new Color("#b7bbaf"),
            TacticalWorksiteMapStage.Operating => new Color("#62c79b"),
            _ => new Color("#97816c")
        };

        AddWorksiteBox(tool, center, width, depth, 0f, r * 0.018f,
            new Color("#4d463d"));
        if (stage == TacticalWorksiteMapStage.MaterialFrame)
        {
            // Incorporated wood and stone, but NO physical building yet.
            AddWorksiteBox(tool, center + new Vector2(-r*.045f,0f),
                r*.09f,r*.13f,r*.018f,r*.04f,baseColor);
            AddWorksiteBox(tool, center + new Vector2(r*.045f,0f),
                r*.08f,r*.12f,r*.018f,r*.025f,new Color("#b6b9b8"));
            return;
        }

        if (stage == TacticalWorksiteMapStage.UnderConstruction)
        {
            // Visible vertical posts with an unfinished crossbeam. They are
            // a physical-process icon, NOT a completed production shelter.
            foreach (var dx in new[]{-0.065f, 0.065f})
            {
                foreach (var dz in new[]{-0.055f, 0.055f})
                    AddWorksiteBox(tool,center+new Vector2(r*dx,r*dz),
                        r*.020f,r*.023f,r*.018f,r*.16f,baseColor);
            }
            AddWorksiteBox(tool,center+new Vector2(0f,-r*.055f),
                r*.16f,r*.025f,r*.167f,r*.024f,new Color("#ae7b42"));
            return;
        }

        // One completed physical shelter, not a new population tier.
        // Base timber supports + pitched roof visibly distinguish it from
        // the original material frame. Green roof = staffed, gray = idle.
        foreach (var dx in new[]{-0.07f, 0.07f})
        {
            foreach (var dz in new[]{-0.066f, 0.066f})
                AddWorksiteBox(tool,center+new Vector2(r*dx,r*dz),
                    r*.018f,r*.018f,r*.018f,r*.08f,new Color("#a58c63"));
        }
        var eave = WorksiteMarkerGround+r*.11f;
        var peak = eave+r*.075f;
        var x0 = center.X-r*.10f;
        var x1 = center.X+r*.10f;
        var z0 = center.Y-r*.095f;
        var z1 = center.Y+r*.095f;
        AddSettlementQuad(tool,new Vector3(x0,eave,z0),
            new Vector3(center.X,peak,z0),
            new Vector3(center.X,peak,z1),
            new Vector3(x0,eave,z1),baseColor);
        AddSettlementQuad(tool,new Vector3(center.X,peak,z0),
            new Vector3(x1,eave,z0),
            new Vector3(x1,eave,z1),
            new Vector3(center.X,peak,z1),baseColor.Darkened(.32f));
        if (stage == TacticalWorksiteMapStage.Operating)
        {
            // A tiny working-color flag denotes actual staffed operation.
            var poleCenter = center + new Vector2(r*.093f,-r*.065f);
            AddWorksiteBox(tool,poleCenter,r*.008f,r*.008f,r*.085f,
                r*.13f,new Color("#d5d2bc"));
            AddSettlementQuad(tool,
                new Vector3(poleCenter.X,WorksiteMarkerGround+r*.20f,poleCenter.Y),
                new Vector3(poleCenter.X+r*.068f,WorksiteMarkerGround+r*.20f,poleCenter.Y),
                new Vector3(poleCenter.X+r*.068f,WorksiteMarkerGround+r*.16f,poleCenter.Y),
                new Vector3(poleCenter.X,WorksiteMarkerGround+r*.16f,poleCenter.Y),
                new Color("#7ce3ab"));
        }
    }

    private void RefreshWorksiteMapMarkers()
    {
        if (_world is null) return;
        _worksiteStageMesh ??= new MeshInstance3D
        {
            Name = "GAWorksiteProgressMarkers",
            MaterialOverride = MakeMaterial()
        };
        if (_worksiteStageMesh.GetParent() is null)
            _world.AddChild(_worksiteStageMesh);
        var previous = _worksiteStageMesh.Mesh;
        _worksiteStageMesh.Mesh = null;
        previous?.Dispose();
        if (_worksiteMapProjection is null || _polygons.Count == 0 ||
            _worksiteMapProjection.Tiles.Count == 0)
        {
            _worksiteStageMesh.Visible = false;
            return;
        }

        var polygons = _polygons.ToDictionary(x => x.Id, x => x.Polygon);
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        int rendered = 0;
        foreach (var tile in _worksiteMapProjection.Tiles)
        {
            if (!polygons.TryGetValue(tile.FineTile,out var polygon)) continue;
            var center = CenterOf(polygon);
            var inradius = RadiusOf(polygon,center);
            AddWorksiteStageGlyph(mesh,center,inradius,tile.Stage);
            rendered++;
        }
        if (rendered > 0)
            _worksiteStageMesh.Mesh = mesh.Commit();
        mesh.Dispose();
        _worksiteStageMesh.Visible = rendered > 0;
    }
}
