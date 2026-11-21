using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GlobalArena.Runtime;
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
        ulong TileId, int WorkerCount, Label Visual);

    private readonly List<LocalSiteMarker> _localSiteMarkers = new();
    private bool _localSitesEnabled;

    // Semantic map zoom: a distant view may intentionally show no sites.
    // Detailed workforce always belongs in the administrative interface.
    public bool IsCartographicSiteMode => _localSitesEnabled;

    public int VisibleLocalSiteMarkerCount =>
        _localSiteMarkers.Count(x => x.Visual.Visible);

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
            RemoveChild(prior.Visual);
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
                Text = "\u25cf Site " + place.Key.ToString("D4",
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
            AddChild(label);
            _localSiteMarkers.Add(new LocalSiteMarker(
                place.Key, population, label));
        }
        PositionLocalSiteMarkers();
        GD.Print($"GA_NEXT_R127_SITE_CENSUS sites={occupied.Length} " +
            $"labels={_localSiteMarkers.Count} workers={_displayWorkers.Count} " +
            "source=canonical_signed_fine no_fake_city=true");
    }

    /// <summary>
    /// R128 experimental cartographic decluttering. No fixed number of
    /// workers is required to appear on the map. Very distant views can
    /// suppress every locality; intermediate views reveal the highest-
    /// ranked sites, then all at close zoom. Never mutates worker state.
    /// </summary>
    internal static int SiteLabelBudget(float zoomRatio, int siteCount)
    {
        if (siteCount <= 0 || zoomRatio >= 0.96f) return 0;
        if (zoomRatio >= 0.76f) return 1;
        if (zoomRatio >= 0.56f)
            return Math.Max(1, (int)Math.Ceiling(siteCount * 0.4));
        return siteCount;
    }

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
        // A wide map should not render every settlement label. Relative
        // thresholds use camera framing, not absolute kilometers or G18 IDs.
        float zoomRatio = _camera.Size / Math.Max(0.001f,
            _coverViewport ? _coverSize : _originalSize);
        var ranking = _localSiteMarkers
            .OrderByDescending(x => x.WorkerCount)
            .ThenBy(x => x.TileId).ToArray();
        // A symbolic cartographic priority, NOT a city category or a
        // gameplay population rule. The fixture has few real workers:
        // fixed absolute population thresholds would hide all its labels.
        int budget = SiteLabelBudget(zoomRatio, ranking.Length);
        var occupiedScreenRects = new List<Rect2>();
        for (int index = 0; index < ranking.Length; index++)
        {
            var place = ranking[index];
            var label = place.Visual;
            if (!_tileCenters.TryGetValue(place.TileId, out var center) ||
                !_renderedPhysicalTileIds.Contains(place.TileId))
            {
                label.Visible = false;
                continue;
            }
            var projected = _camera.UnprojectPosition(center);
            Vector2 screen = new(projected.X * factorX,
                projected.Y * factorY);
            // Place labels next to the canonical site anchor, not any worker.
            label.Position = screen + new Vector2(12f, -18f);
            bool candidate = index < budget &&
                !_camera.IsPositionBehind(center) &&
                screen.X >= 6f && screen.Y >= 18f &&
                screen.X < Size.X - 100f && screen.Y < Size.Y - 24f;
            if (!candidate)
            {
                label.Visible = false;
                continue;
            }
            // Stable, high-importance-first collision avoidance.
            var rect = new Rect2(label.Position,
                new Vector2(108f, 26f));
            label.Visible = !occupiedScreenRects.Any(other =>
                other.Intersects(rect));
            if (label.Visible) occupiedScreenRects.Add(rect);
        }
    }
}
