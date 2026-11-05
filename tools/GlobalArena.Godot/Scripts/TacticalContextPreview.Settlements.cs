using System;
using System.Linq;
using GlobalArena.Simulation;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// GA-UI-SETTLEMENT-GLYPH-01: one unified inhabited-place marker, from
/// early settlement through megalopolis, derived only from actual resident
/// population. There is no gameplay site tier, founded city or new save.
/// Travel camps are temporary population/route states, NOT founded cities.
/// The pitched-roof structures are presentation-only at camera pitch 45°.
/// </summary>
public partial class TacticalContextPreview
{
    private const float SettlementHaloHeight = 0.00069f;
    private const float SettlementStructureGround = 0.00076f;
    private MeshInstance3D? _settlementHalos;
    private MeshInstance3D? _settlementStructures;
    private EmergentSettlementProjection? _settlementProjection;
    private PanelContainer? _settlementHoverPanel;
    private Label? _settlementHoverText;
    private ulong? _hoveredSettlementTile;

    private static void AddSettlementTriangle(SurfaceTool tool, Vector3 a,
        Vector3 b, Vector3 c, Color shade)
    {
        AddColored(tool, a, shade);
        AddColored(tool, b, shade);
        AddColored(tool, c, shade);
    }

    private static void AddSettlementQuad(SurfaceTool tool, Vector3 a,
        Vector3 b, Vector3 c, Vector3 d, Color shade)
    {
        AddSettlementTriangle(tool, a, b, c, shade);
        AddSettlementTriangle(tool, a, c, d, shade);
    }

    /// <summary>
    /// Fixed-scale schematic of a late Neolithic / pre-metal-age sedentary
    /// dwelling: vertical wattle-and-daub walls and a pitched thatch roof.
    /// Actual 3D roof/walls (not camera-facing screen sprites) read naturally
    /// at the existing 45-degree camera and ±15-degree yaw.
    /// The icon represents inhabited people, NOT a physical building asset.
    /// </summary>
    private static void AddSedentaryDwelling(SurfaceTool tool,
        Vector2 center, float halfWidth, float halfDepth, float wallHeight,
        Color roofTint)
    {
        var ground = SettlementStructureGround;
        var eave = ground + wallHeight;
        var ridge = eave + wallHeight * 0.58f;
        var x0 = center.X - halfWidth;
        var x1 = center.X + halfWidth;
        var z0 = center.Y - halfDepth;
        var z1 = center.Y + halfDepth;
        Vector3 P(float x, float y, float z) => new(x, y, z);
        var faceFront = new Color("#d6bb89");
        var faceBack = new Color("#9f825f");
        var side = new Color("#b39b76");
        var roofDark = new Color("#8d633d");
        var entrance = new Color("#453527");
        // Four solid wall sides; the roof is raised above them, not a
        // flat tile decal or a floating halo.
        AddSettlementQuad(tool, P(x0,ground,z0), P(x1,ground,z0),
            P(x1,eave,z0), P(x0,eave,z0), faceFront);
        AddSettlementQuad(tool, P(x1,ground,z1), P(x0,ground,z1),
            P(x0,eave,z1), P(x1,eave,z1), faceBack);
        AddSettlementQuad(tool, P(x0,ground,z1), P(x0,ground,z0),
            P(x0,eave,z0), P(x0,eave,z1), side);
        AddSettlementQuad(tool, P(x1,ground,z0), P(x1,ground,z1),
            P(x1,eave,z1), P(x1,eave,z0), faceBack);
        // Gables complete the sedentary rectangular construction.
        AddSettlementTriangle(tool, P(x0,eave,z0),
            P(x1,eave,z0), P(center.X,ridge,z0), faceFront);
        AddSettlementTriangle(tool, P(x1,eave,z1),
            P(x0,eave,z1), P(center.X,ridge,z1), side);
        // Overhanging thatch slopes, pitch remains visible at yaw changes.
        var overhang = halfWidth * 0.12f;
        var overdepth = halfDepth * 0.09f;
        AddSettlementQuad(tool, P(x0-overhang,eave,z0-overdepth),
            P(center.X,ridge,z0-overdepth),
            P(center.X,ridge,z1+overdepth),
            P(x0-overhang,eave,z1+overdepth), roofTint);
        AddSettlementQuad(tool, P(center.X,ridge,z0-overdepth),
            P(x1+overhang,eave,z0-overdepth),
            P(x1+overhang,eave,z1+overdepth),
            P(center.X,ridge,z1+overdepth), roofDark);
        // Small front entrance, reinforces permanent hut vs traveller tent.
        var doorHalf = halfWidth * 0.18f;
        var face = z0 - halfDepth * 0.012f;
        AddSettlementQuad(tool,
            P(center.X-doorHalf,ground,face),
            P(center.X+doorHalf,ground,face),
            P(center.X+doorHalf,ground+wallHeight*0.67f,face),
            P(center.X-doorHalf,ground+wallHeight*0.67f,face), entrance);
    }

    private static void AddSedentarySettlementGlyph(
        SurfaceTool tool, Vector2 center, float radius)
    {
        // Identical silhouette for every occupied tile regardless of size,
        // era or economic capacity. Presentation tiers, if any, are future
        // thresholds, NEVER distinct settlement logic.
        var r = radius;
        var yard = new Color("#554937");
        var clay = new Color("#b18c5b");
        var a = new Vector3(center.X-r*0.34f, SettlementStructureGround,
            center.Y-r*0.27f);
        var b = new Vector3(center.X+r*0.32f, SettlementStructureGround,
            center.Y-r*0.27f);
        var c = new Vector3(center.X+r*0.32f, SettlementStructureGround,
            center.Y+r*0.33f);
        var d = new Vector3(center.X-r*0.34f, SettlementStructureGround,
            center.Y+r*0.33f);
        AddSettlementQuad(tool, a,b,c,d,yard);
        AddSedentaryDwelling(tool, center + new Vector2(-r*0.15f,-r*0.07f),
            r*0.145f,r*0.17f,r*0.14f,new Color("#cdaa69"));
        AddSedentaryDwelling(tool, center + new Vector2(r*0.17f,r*0.13f),
            r*0.115f,r*0.135f,r*0.12f,new Color("#d3b679"));
        // Permanent granary / wattle storage footprint. Not a new stock.
        var stock = center + new Vector2(r*0.20f,-r*0.20f);
        AddSettlementQuad(tool,
            new Vector3(stock.X-r*0.06f,SettlementStructureGround+r*0.012f,stock.Y-r*0.05f),
            new Vector3(stock.X+r*0.06f,SettlementStructureGround+r*0.012f,stock.Y-r*0.05f),
            new Vector3(stock.X+r*0.06f,SettlementStructureGround+r*0.012f,stock.Y+r*0.05f),
            new Vector3(stock.X-r*0.06f,SettlementStructureGround+r*0.012f,stock.Y+r*0.05f),clay);
    }

    /// <summary>
    /// This tooltip is deliberately an interaction-layer presentation of
    /// current allocations; the world does not contain a "city" class.
    /// </summary>
    private void InitializeSettlementHover()
    {
        if (_settlementHoverPanel is not null) return;
        var surface = new StyleBoxFlat
        {
            BgColor = new Color("#14212ce9"),
            BorderColor = new Color("#d1bd8c"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4
        };
        _settlementHoverPanel = new PanelContainer
        {
            Name = "GASettlementHover",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 24,
            CustomMinimumSize = new Vector2(176f, 0f)
        };
        _settlementHoverPanel.AddThemeStyleboxOverride("panel", surface);
        _settlementHoverText = new Label
        {
            Text = "",
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _settlementHoverText.AddThemeFontSizeOverride("font_size", 12);
        _settlementHoverText.AddThemeColorOverride("font_color",
            new Color("#f3eddf"));
        _settlementHoverPanel.AddChild(_settlementHoverText);
        AddChild(_settlementHoverPanel);
        MouseExited += () => UpdateSettlementHover(null, Vector2.Zero);
    }

    private void UpdateSettlementHover(ulong? hoveredTile, Vector2 cursor)
    {
        _hoveredSettlementTile = hoveredTile;
        if (_settlementHoverPanel is null || _settlementHoverText is null)
            return;
        var site = hoveredTile.HasValue
            ? _settlementProjection?.Sites.FirstOrDefault(x =>
                x.Tile.FineStrategicCellId.Value == hoveredTile.Value)
            : null;
        if (site is null)
        {
            _settlementHoverPanel.Visible = false;
            return;
        }
        _settlementHoverText.Text =
            $"ASSENTAMENTO · TILE {hoveredTile!.Value}\n" +
            $"Pessoas: {site.Workers}\n" +
            $"Agricultura: {site.AgriculturalWorkers}\n" +
            $"Madeira: {site.ForestryWorkers}\n" +
            $"Pedra: {site.MiningWorkers}";
        var x = Mathf.Clamp(cursor.X + 16f, 0f,
            Mathf.Max(0f, Size.X - 188f));
        var y = Mathf.Clamp(cursor.Y + 16f, 0f,
            Mathf.Max(0f, Size.Y - 122f));
        _settlementHoverPanel.Position = new Vector2(x, y);
        _settlementHoverPanel.Visible = true;
    }

    public void SetSettlementSites(EmergentSettlementProjection? projection)
    {
        _settlementProjection = projection;
        RefreshSettlementHalos();
        UpdateSettlementHover(_hoveredSettlementTile, GetLocalMousePosition());
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
        _settlementStructures ??= new MeshInstance3D
        {
            Name = "GASettlementBuiltForms",
            MaterialOverride = MakeMaterial()
        };
        if (_settlementStructures.GetParent() is null)
            _world.AddChild(_settlementStructures);

        var oldStructures = _settlementStructures.Mesh;
        _settlementStructures.Mesh = null;
        oldStructures?.Dispose();
        var old = _settlementHalos.Mesh;
        _settlementHalos.Mesh = null;
        old?.Dispose();
        if (_settlementProjection is null || _polygons.Count == 0)
        {
            _settlementHalos.Visible = false;
            _settlementStructures.Visible = false;
            return;
        }

        var polygons = _polygons.ToDictionary(x => x.Id, x => x.Polygon);
        var surface = new SurfaceTool();
        var structures = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        structures.Begin(Mesh.PrimitiveType.Triangles);
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
            AddSedentarySettlementGlyph(structures, center, inradius);
            rings++;
        }
        if (rings > 0)
        {
            _settlementHalos.Mesh = surface.Commit();
            _settlementStructures.Mesh = structures.Commit();
        }
        surface.Dispose();
        structures.Dispose();
        _settlementHalos.Visible = rings > 0;
        _settlementStructures.Visible = rings > 0;
    }
}
