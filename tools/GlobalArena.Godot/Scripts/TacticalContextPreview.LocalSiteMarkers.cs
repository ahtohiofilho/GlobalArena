using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// R127 cartographic UX experiment: a 'site' is simply a canonical fine
/// physical tile occupied by actual workers. No city/settlement entity is
/// created, and no old procedural toponym or banner rules are replaced.
/// Enabled ONLY by the explicit map-marker QA flag.
/// </summary>
public partial class TacticalContextPreview
{
    private sealed record LocalSiteMarker(
        ulong TileId, int WorkerCount, Vector3 PolygonCentroid, Panel Dot, Label Visual);

    private readonly List<LocalSiteMarker> _localSiteMarkers = new();
    private bool _localSitesEnabled;
    private bool _siteLabelLayoutDirty = true;
    private Vector2 _siteLabelViewportSize;

    // Tactical sites remain visible at every supported zoom.
    // Workforce belongs in the administration screen, not as map dots.
    public bool IsCartographicSiteMode => _localSitesEnabled;

    public int VisibleLocalSiteMarkerCount =>
        _localSiteMarkers.Count(x => x.Dot.Visible);

    // A marker's actual physical identity and rendered geometric centroid
    // can be inspected by visual E2E tests without inventing settlement IDs.
    public IReadOnlyList<(ulong TileId, Vector3 Centroid)> LocalSiteAnchors =>
        Array.AsReadOnly(_localSiteMarkers.Select(x =>
            (x.TileId, x.PolygonCentroid)).ToArray());

    internal float? PresentedSiteDiameter(ulong tileId) =>
        _localSiteMarkers.SingleOrDefault(m => m.TileId == tileId)?.Dot.Size.X;

    // Acceptance only: inspect the actual rendered dot, not a calculated
    // substitute. Use the existing geographic camera path for remote sites.
    internal float InspectPopulationMarkerForSmoke(ulong tileId)
    {
        var marker = _localSiteMarkers.SingleOrDefault(m => m.TileId == tileId);
        if (marker?.Dot.IsVisibleInTree() != true)
        {
            var geometry = _cachedFineWorld?.StrategicSphericalGeometry
                ?? throw new InvalidOperationException("Population inspection requires signed geometry.");
            CenterOnGeography(ConvertPoint(geometry.GetCell(new StrategicCellId(tileId)).Center).Normalized());
            PositionLocalSiteMarkers();
            marker = _localSiteMarkers.SingleOrDefault(m => m.TileId == tileId);
        }
        if (marker?.Dot.IsVisibleInTree() != true)
            throw new InvalidOperationException($"Occupied site {tileId} has no visible dot after geographic focus.");
        AssertSitePopulationPresentation();
        return marker.Dot.Size.X;
    }

    internal void AssertSitePopulationPresentation()
    {
        foreach (var marker in _localSiteMarkers)
        {
            var occupants = _displayWorkers.Where(w => w.TacticalTileId.HasValue &&
                w.TacticalTileId.Value.FineStrategicCellId.Value == marker.TileId).ToArray();
            int count = occupants.Length;
            if (count <= 0 || marker.WorkerCount != count ||
                marker.Dot.Size != new Vector2(GaCartographicStyle.SiteDiameter(count), GaCartographicStyle.SiteDiameter(count)))
                throw new InvalidOperationException("Site marker must represent its actual occupied population.");
            if (marker.Dot.GetThemeStylebox("panel") is not StyleBoxFlat disc ||
                disc.BgColor != GaCivilizationVisualIdentity.SiteColor(occupants.Select(w => w.Owner.Value)) ||
                disc.BorderColor != new Color("#f5f5f5"))
                throw new InvalidOperationException("Site marker color differs from displayed physical presence.");
        }
    }

    public event Action<ulong>? CartographicLocalSiteSelected;

    // Hit-test uses the SAME on-screen label and anchor shown to the
    // player. Labels ignore mouse events; the map handles the click.
    // Site is a QA presentation proxy, never a new simulation entity.
    private bool PickVisibleCartographicLocalSite(Vector2 localPixel)
    {
        if (!_localSitesEnabled || _cachedPhysicalMap is null)
            return false;
        // A physical dot always takes precedence over a displaced name.
        // A hidden name has no hitbox, and nearby dots cannot steal a click
        // from an actually visible label.
        LocalSiteMarker? picked = null;
        float nearest = float.PositiveInfinity;
        foreach (var site in _localSiteMarkers)
        {
            if (!site.Dot.Visible) continue;
            float distance = (site.Dot.Position + site.Dot.Size * 0.5f).DistanceSquaredTo(localPixel);
            float radius = site.Dot.Size.X * 0.5f;
            if (distance <= radius * radius && distance < nearest)
            { picked = site; nearest = distance; }
        }
        picked ??= _localSiteMarkers.FirstOrDefault(s => s.Dot.Visible && s.Visual.Visible &&
            new Rect2(s.Visual.Position, s.Visual.Size).HasPoint(localPixel));
        if (picked is null)
        {
            foreach (var site in _localSiteMarkers)
            {
                if (!site.Dot.Visible) continue;
                float distance = (site.Dot.Position + site.Dot.Size * 0.5f).DistanceSquaredTo(localPixel);
                if (distance <= 100f && distance < nearest)
                { picked = site; nearest = distance; }
            }
        }
        if (picked is null) return false;
        SelectPhysicalTile(picked.TileId);
        var id = new PhysicalTacticalTileId(_cachedPhysicalMap.Refinement.FineParameters,
            new StrategicCellId(picked.TileId));
        CanonicalPhysicalTileClicked?.Invoke(id);
        CartographicLocalSiteSelected?.Invoke(picked.TileId);
        GD.Print($"GA_NEXT_R129_LOCAL_SITE_SELECTED tile={picked.TileId} " +
            "source=real_fine_worker_tile simulated_city=false mutation=false");
        return true;
    }

    public IReadOnlyList<ulong> LocalSiteMarkerTileIds =>
        Array.AsReadOnly(_localSiteMarkers.Select(x => x.TileId).ToArray());

    private void InitializeLocalSiteMarkers()
    {
        _localSitesEnabled =
            System.Environment.GetEnvironmentVariable("GA_NEXT_QA_LOCAL_SITE_MARKERS") == "1";
        // True geographic +Z north for the experimental city map only.
        // Do not change legacy test/gameplay modes in this UI gate.
        _geographicNorthUp = _localSitesEnabled;
        SetProcess(true); // screen-space trade ink also follows the live camera
        if (_localSitesEnabled)
            GD.Print("GA_NEXT_R127_SITE_MARKERS source=real_fine_worker_tiles " +
                "placeholder_names=true no_settlement_state=true");
    }

    private void UpdateLocalSiteCensus()
    {
        if (!_localSitesEnabled) return;
        foreach (var prior in _localSiteMarkers)
        {
            RemoveChild(prior.Dot);
            RemoveChild(prior.Visual);
            prior.Dot.QueueFree();
            prior.Visual.QueueFree();
        }
        _localSiteMarkers.Clear();
        _siteLabelLayoutDirty = true;

        if (_displayWorkers.Count == 0) return;
        var occupied = _displayWorkers
            .Where(w => w.TacticalTileId.HasValue)
            .GroupBy(w => w.TacticalTileId!.Value.FineStrategicCellId.Value)
            .OrderBy(x => x.Key)
            .ToArray();
        foreach (var place in occupied)
        {
            // The displayed chart loads only a few strategic rings.
            // A genuine inhabited tile MUST NOT disappear merely because
            // its physical polygon is outside that small resident cache.
            // Reuse the canonical fine topology's visual geometry instead.
            if (!TryGetSitePolygonCentroid(place.Key, out var centroid))
                continue;
            int population = place.Count();
            int diameter = GaCartographicStyle.SiteDiameter(population);
            int radius = diameter / 2;
            var label = new Label
            {
                Name = "GANextR127Site" + place.Key.ToString(
                    CultureInfo.InvariantCulture),
                Text = "Site " + place.Key.ToString("D4",
                    CultureInfo.InvariantCulture),
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            // Importance is determined by ACTUAL workers, not RNG.
            int size = population >= 8 ? 17 : population >= 4 ? 15 : 12;
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color",
                new Color("#f6e6c4"));
            label.AddThemeColorOverride("font_shadow_color",
                new Color("#16212c"));
            label.AddThemeConstantOverride("shadow_offset_x", 1);
            label.AddThemeConstantOverride("shadow_offset_y", 1);
            // A circular Panel has exact geometry. A bullet CHARACTER
            // does not: glyph ascent, baseline and margins make it appear
            // above the polygon even with an ostensibly centered Label.
            var dot = new Panel
            {
                Name = "GANextR134SiteDot" + place.Key.ToString(
                    CultureInfo.InvariantCulture),
                CustomMinimumSize = new Vector2(diameter, diameter),
                Size = new Vector2(diameter, diameter),
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            var disc = new StyleBoxFlat
            {
                BgColor = GaCivilizationVisualIdentity.SiteColor(place.Select(w => w.Owner.Value)),
                BorderColor = new Color("#f5f5f5"),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = radius,
                CornerRadiusTopRight = radius,
                CornerRadiusBottomLeft = radius,
                CornerRadiusBottomRight = radius
            };
            dot.AddThemeStyleboxOverride("panel", disc);
            AddChild(dot);
            AddChild(label);
            _localSiteMarkers.Add(new LocalSiteMarker(
                place.Key, population, centroid, dot, label));
        }
        PositionLocalSiteMarkers();
        GD.Print($"GA_NEXT_R134_SITE_CENSUS sites={occupied.Length} " +
            $"projected={_localSiteMarkers.Count} workers={_displayWorkers.Count} " +
            "source=all_occupied_fine_tiles centroid=polygon_area " +
            "resident_cache_not_required=true no_fake_city=true");
    }

    // Use the *same* canonical fine spherical geometry and Flatten()
    // as the rendered tile mesh. The polygon's area centroid, not the
    // spherical cell origin or the bounding-box corner, defines its dot.
    // The local chart can be rebuilt on focus handoff without generating
    // any new names, cities, or authoritative economic objects.
    private bool TryGetSitePolygonCentroid(ulong tileId, out Vector3 center)
    {
        center = Vector3.Zero;
        if (_cachedFineWorld is null || _cachedPhysicalMap is null)
            return false;
        var geometry = _cachedFineWorld.StrategicSphericalGeometry;
        if (tileId == 0UL || tileId > (ulong)geometry.Cells.Count)
            return false;
        var cell = geometry.GetCell(new StrategicCellId(tileId));
        // Avoid projecting the antipode of the current local chart.
        if (ConvertPoint(cell.Center).Dot(_surfaceNormal) <= 0.00001f)
            return false;
        var contour = cell.BoundaryVertexIds.Select(v =>
        {
            var point = Flatten(geometry.GetVertex(v).Position);
            return new Vector2(point.X, point.Z);
        }).ToArray();
        var centroid = SitePolygonAreaCentroid(contour);
        center = new Vector3(centroid.X, 0f, centroid.Y);
        return true;
    }

    internal static Vector2 SitePolygonAreaCentroid(
        IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count < 3)
            throw new ArgumentException("A physical site needs a polygon.");
        double areaTwice = 0, weightedX = 0, weightedY = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            double cross = (double)a.X * b.Y - (double)b.X * a.Y;
            areaTwice += cross;
            weightedX += ((double)a.X + b.X) * cross;
            weightedY += ((double)a.Y + b.Y) * cross;
        }
        if (Math.Abs(areaTwice) < 1e-12)
            throw new InvalidOperationException(
                "Canonical physical site has degenerate polygon area.");
        return new Vector2((float)(weightedX / (3d * areaTwice)),
            (float)(weightedY / (3d * areaTwice)));
    }

    /// <summary>
    /// R133 tactical presentation contract: all in-view eligible localities
    /// remain visible at every supported tactical zoom. The strategic
    /// atlas can implement its own decluttering later if evidence warrants.
    /// </summary>
    internal static int SiteLabelBudget(float zoomRatio, int siteCount) =>
        Math.Max(0, siteCount);

    // The orthographic view pans/rotates/zooms independently from the
    // marker labels. Reproject only the few occupied, rendered sites;
    // no per-frame market or worldgen calculations.
    public override void _Process(double delta)
    {
        if (_localSitesEnabled)
            PositionLocalSiteMarkers();
        UpdateTradeInkProjection();
    }

    private void PositionLocalSiteMarkers()
    {
        if (!_localSitesEnabled || _camera is null ||
            _innerViewport is null || Size.X <= 0f || Size.Y <= 0f)
            return;

        // UnprojectPosition returns coordinates of the inner SubViewport.
        // The container stretches it, so apply the identical presentation
        // transform rather than guessing any world-space map dimensions.
        var baseSize = _innerViewport.Size;
        if (baseSize.X <= 0 || baseSize.Y <= 0) return;
        float factorX = Size.X / baseSize.X;
        float factorY = Size.Y / baseSize.Y;
        // All eligible dots remain visible. Only names may move or be
        // temporarily omitted when every adjacent text position is blocked.
        bool dirty = _siteLabelLayoutDirty || _siteLabelViewportSize != Size;
        foreach (var place in _localSiteMarkers)
        {
            var label = place.Visual;
            var dot = place.Dot;
            var center = place.PolygonCentroid;
            var projected = _camera.UnprojectPosition(center);
            Vector2 screen = new(projected.X * factorX,
                projected.Y * factorY);
            bool inMap = !_camera.IsPositionBehind(center) &&
                screen.X >= 0f && screen.Y >= 0f &&
                screen.X < Size.X && screen.Y < Size.Y;

            // The glyph itself is centered on the polygon anchor.
            // Only the name is shifted away from the marker.
            var position = screen - dot.Size * 0.5f;
            if (dot.Position != position || dot.Visible != inMap) dirty = true;
            dot.Position = position;
            dot.Visible = inMap;
            if (!inMap) label.Visible = false;
        }
        if (!dirty) return;
        _siteLabelLayoutDirty = false;
        _siteLabelViewportSize = Size;
        var sites = _localSiteMarkers.Where(s => s.Dot.Visible).Select(s =>
            new GaSiteLabelLayout.Site(s.TileId, s.WorkerCount,
                new Rect2(s.Dot.Position, s.Dot.Size), s.Visual.GetCombinedMinimumSize())).ToArray();
        var placements = GaSiteLabelLayout.Place(Size, sites);
        foreach (var site in _localSiteMarkers)
        {
            if (placements.TryGetValue(site.TileId, out var rect))
            {
                site.Visual.Position = rect.Position;
                site.Visual.Size = rect.Size;
                site.Visual.Visible = true;
            }
            else site.Visual.Visible = false;
        }
    }

    internal void AssertSiteLabelLayout()
    {
        PositionLocalSiteMarkers();
        var dots = _localSiteMarkers.Where(s => s.Dot.Visible).ToArray();
        var names = dots.Where(s => s.Visual.Visible).ToArray();
        if (dots.Length > 0 && names.Length == 0)
            throw new InvalidOperationException("Expected at least one readable name in the actual scale fixture.");
        var bounds = new Rect2(Vector2.Zero, Size);
        for (int i = 0; i < names.Length; i++)
        {
            var r = new Rect2(names[i].Visual.Position, names[i].Visual.Size);
            if (!bounds.Encloses(r) || dots.Any(s => r.Intersects(new Rect2(s.Dot.Position, s.Dot.Size).Grow(2f))) ||
                names.Skip(i + 1).Any(s => r.Intersects(new Rect2(s.Visual.Position, s.Visual.Size).Grow(2f))))
                throw new InvalidOperationException("Visible site labels collide or leave the map.");
        }
        if (_localSiteMarkers.Any(s => !s.Dot.Visible && s.Visual.Visible))
            throw new InvalidOperationException("Offscreen dot has a visible label.");
    }
}
