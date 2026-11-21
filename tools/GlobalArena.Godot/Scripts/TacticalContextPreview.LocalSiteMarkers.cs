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
        ulong TileId, int WorkerCount, Label Dot, Label Visual);

    private readonly List<LocalSiteMarker> _localSiteMarkers = new();
    private bool _localSitesEnabled;

    // Tactical sites remain visible at every supported zoom.
    // Workforce belongs in the administration screen, not as map dots.
    public bool IsCartographicSiteMode => _localSitesEnabled;

    public int VisibleLocalSiteMarkerCount =>
        _localSiteMarkers.Count(x => x.Visual.Visible);

    public event Action<ulong>? CartographicLocalSiteSelected;

    // Hit-test uses the SAME on-screen label and anchor shown to the
    // player. Labels ignore mouse events; the map handles the click.
    // Site is a QA presentation proxy, never a new simulation entity.
    private bool PickVisibleCartographicLocalSite(Vector2 localPixel)
    {
        if (!_localSitesEnabled || _cachedPhysicalMap is null)
            return false;
        foreach (var site in _localSiteMarkers)
        {
            if (!site.Dot.Visible) continue;
            var label = site.Visual;
            var textBounds = new Rect2(label.Position,
                new Vector2(108f, 26f));
            // The dot is centered on the physical polygon; the name is
            // separately offset and both select the same canonical tile.
            var dotCenter = site.Dot.Position + new Vector2(8f, 8f);
            if (!textBounds.HasPoint(localPixel) &&
                dotCenter.DistanceSquaredTo(localPixel) > 100f)
                continue;
            SelectPhysicalTile(site.TileId);
            var id = new PhysicalTacticalTileId(
                _cachedPhysicalMap.Refinement.FineParameters,
                new StrategicCellId(site.TileId));
            CanonicalPhysicalTileClicked?.Invoke(id);
            CartographicLocalSiteSelected?.Invoke(site.TileId);
            GD.Print($"GA_NEXT_R129_LOCAL_SITE_SELECTED tile={site.TileId} " +
                "source=real_fine_worker_tile simulated_city=false mutation=false");
            return true;
        }
        return false;
    }

    public IReadOnlyList<ulong> LocalSiteMarkerTileIds =>
        Array.AsReadOnly(_localSiteMarkers.Select(x => x.TileId).ToArray());

    private void InitializeLocalSiteMarkers()
    {
        _localSitesEnabled =
            System.Environment.GetEnvironmentVariable("GA_NEXT_QA_LOCAL_SITE_MARKERS") == "1";
        SetProcess(_localSitesEnabled);
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

        if (_displayWorkers.Count == 0) return;
        var occupied = _displayWorkers
            .Where(w => w.TacticalTileId.HasValue)
            .GroupBy(w => w.TacticalTileId!.Value.FineStrategicCellId.Value)
            .OrderBy(x => x.Key)
            .ToArray();
        foreach (var place in occupied)
        {
            if (!_tileCenters.ContainsKey(place.Key))
                continue; // Only currently drawn physical tiles.
            int population = place.Count();
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
            var dot = new Label
            {
                Name = "GANextR133SiteDot" + place.Key.ToString(
                    CultureInfo.InvariantCulture),
                Text = "●",
                CustomMinimumSize = new Vector2(16f, 16f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false
            };
            dot.AddThemeFontSizeOverride("font_size", 13);
            dot.AddThemeColorOverride("font_color", new Color("#f6e6c4"));
            dot.AddThemeColorOverride("font_shadow_color",
                new Color("#16212c"));
            AddChild(dot);
            AddChild(label);
            _localSiteMarkers.Add(new LocalSiteMarker(
                place.Key, population, dot, label));
        }
        PositionLocalSiteMarkers();
        GD.Print($"GA_NEXT_R127_SITE_CENSUS sites={occupied.Length} " +
            $"labels={_localSiteMarkers.Count} workers={_displayWorkers.Count} " +
            "source=canonical_signed_fine no_fake_city=true");
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
        // Render every eligible physical locality within the tactical
        // viewport. No zoom threshold, population threshold, top-N budget,
        // or collision suppression: those previously hid real locations.
        foreach (var place in _localSiteMarkers)
        {
            var label = place.Visual;
            var dot = place.Dot;
            if (!_tileCenters.TryGetValue(place.TileId, out var center) ||
                !_renderedPhysicalTileIds.Contains(place.TileId))
            {
                label.Visible = false;
                dot.Visible = false;
                continue;
            }
            var projected = _camera.UnprojectPosition(center);
            Vector2 screen = new(projected.X * factorX,
                projected.Y * factorY);
            bool inMap = !_camera.IsPositionBehind(center) &&
                screen.X >= 0f && screen.Y >= 0f &&
                screen.X < Size.X && screen.Y < Size.Y;

            // The glyph itself is centered on the polygon anchor.
            // Only the name is shifted away from the marker.
            dot.Position = screen - new Vector2(8f, 8f);
            label.Position = screen + new Vector2(11f, -13f);
            dot.Visible = inMap;
            label.Visible = inMap;
        }
    }
}
