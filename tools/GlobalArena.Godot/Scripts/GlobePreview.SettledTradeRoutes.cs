using System;
using System.Collections.Generic;
using System.Linq;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// R100: only the participant-authorized LAST CLOSED economic cycle may
/// supply sphere routes. No world-wide audit, guessed trade, solver call,
/// hidden network, simulated cargo or external ownership data reaches here.
/// </summary>
public partial class GlobePreview
{
    private StrategicSphericalGeometry? _signedG18RouteGeometry;
    private RuntimeWorldBinding? _signedG18WorldBinding;
    private Control? _sphericalTradeInk;
    private readonly List<(PhysicalTacticalEdgeId Id, Vector3 A, Vector3 B, float Width)> _sphericalTradeBands = new();
    private Transform3D _lastTradePlanet, _lastTradeCamera;
    private Vector2 _lastTradeSize;
    private float _lastTradeFov = -1f;
    private FoundationLastClosedEconomicTradeView? _authorizedGlobeTrade;
    private bool _showSettledGlobeRoutes = true;

    public int VisibleSphericalSettledTradeCrossings { get; private set; }
    internal ulong? PresentedSphericalTradeCycle => _authorizedGlobeTrade?.EconomicCycleOrdinal;
    internal bool HasSphericalTradeInk => _sphericalTradeInk is not null && _sphericalTradeBands.Count > 0;
    internal IReadOnlyList<PhysicalTacticalEdgeId> SphericalTradeEdgeIds => _sphericalTradeBands.Select(x => x.Id).ToArray();

    private void InitializeSphericalTradeInk(SubViewport viewport)
    {
        _sphericalTradeInk = new Control { Name = "GANextSphericalTradeInk",
            MouseFilter = MouseFilterEnum.Ignore, ClipContents = true, Visible = false };
        // Composite the ink INSIDE the same render target as the physical
        // sphere. A Control under SubViewportContainer competes with its
        // viewport texture and container layout, especially after reparent.
        var overlay = new CanvasLayer { Name = "GANextSphericalTradeCanvas", Layer = 10 };
        viewport.AddChild(overlay);
        overlay.AddChild(_sphericalTradeInk);
        _sphericalTradeInk.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _sphericalTradeInk.Draw += DrawSphericalTradeInk;
        _sphericalTradeInk.Resized += _sphericalTradeInk.QueueRedraw;
    }

    private void RefreshSphericalTradeVisibility()
    {
        if (_sphericalTradeInk is null) return;
        _sphericalTradeInk.Visible = _showSettledGlobeRoutes &&
            _mapMode == SignedG18MapMode && _sphericalTradeBands.Count > 0;
        _sphericalTradeInk.QueueRedraw();
    }

    public void SetSphericalSettledTradeRoutesVisible(bool enabled)
    {
        _showSettledGlobeRoutes = enabled;
        RefreshSphericalTradeVisibility();
    }

    /// <summary>
    /// Read boundary is the same host-authenticated participant view used
    /// by the tactical chart. Foreign world and topology are rejected.
    /// </summary>
    public void PresentSphericalSettledTradeRoutes(
        FoundationLastClosedEconomicTradeView? authorizedView)
    {
        if (_signedG18RouteGeometry is null) return; // Legacy V1 untouched.
        if (authorizedView is not null &&
            authorizedView.Availability !=
                FoundationEconomicTradeOverlayAvailability.NoClosedCycle &&
            (_signedG18WorldBinding != authorizedView.WorldBinding ||
             _signedG18Geography is null ||
             authorizedView.FineTopology != _signedG18Geography.FineParameters ||
             authorizedView.EconomicCycleOrdinal is not > 0UL ||
             authorizedView.PublishedHash is null ||
             string.IsNullOrWhiteSpace(authorizedView.CargoUnitCode)))
            throw new InvalidOperationException(
                "Spherical trade must use the signed participant-private fine topology.");
        _authorizedGlobeTrade = authorizedView is
            { Availability: FoundationEconomicTradeOverlayAvailability.NoClosedCycle }
                ? null : authorizedView;
        RebuildSphericalSettledTradeRoutes();
    }

    private void UpdateSphericalTradeProjection()
    {
        if (_sphericalTradeInk?.IsVisibleInTree() != true || _planet is null || _camera is null) return;
        if (_lastTradePlanet == _planet.GlobalTransform && _lastTradeCamera == _camera.GlobalTransform &&
            _lastTradeSize == Size && _lastTradeFov == _camera.Fov) return;
        _lastTradePlanet = _planet.GlobalTransform;
        _lastTradeCamera = _camera.GlobalTransform;
        _lastTradeSize = Size;
        _lastTradeFov = _camera.Fov;
        _sphericalTradeInk.QueueRedraw();
    }

    // For a unit sphere and an external perspective camera C, P is on
    // the visible cap precisely when P dot C >= 1. Clip the great-circle
    // arc, not a straight chord, before projection. Find its stationary
    // maximum as well, so a small visible interior is not discarded.
    private static bool ClipTradeArc(Vector3 a, Vector3 b, Vector3 camera,
        out Vector3 first, out Vector3 last)
    {
        first = a; last = b;
        bool visibleA = a.Dot(camera) >= 1f, visibleB = b.Dot(camera) >= 1f;
        Vector3 inside = visibleA ? a : b;
        if (!visibleA && !visibleB)
        {
            double angle = Math.Acos(Math.Clamp((double)a.Dot(b), -1d, 1d));
            var tangent = b - a * a.Dot(b);
            if (angle < 1e-8 || tangent.LengthSquared() < 1e-12f) return false;
            double maximum = Math.Atan2(tangent.Normalized().Dot(camera), a.Dot(camera));
            if (maximum <= 0d || maximum >= angle) return false;
            inside = a.Slerp(b, (float)(maximum / angle)).Normalized();
            if (inside.Dot(camera) < 1f) return false;
        }
        if (!visibleA) first = HorizonPoint(a, inside, camera);
        if (!visibleB) last = HorizonPoint(b, inside, camera);
        return first.DistanceSquaredTo(last) > 1e-12f;
    }

    private static Vector3 HorizonPoint(Vector3 outside, Vector3 inside, Vector3 camera)
    {
        for (int i = 0; i < 18; i++)
        {
            var mid = outside.Slerp(inside, 0.5f).Normalized();
            if (mid.Dot(camera) >= 1f) inside = mid;
            else outside = mid;
        }
        return inside;
    }

    private void DrawSphericalTradeInk()
    {
        if (_sphericalTradeInk is null || _camera is null || _planet is null) return;
        var viewport = _camera.GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;
        // The ink now uses the camera viewport's own pixel coordinates.
        // Stretch is 1:1 in GlobePreview; no second projection scale.
        var camera = _planet.ToLocal(_camera.GlobalPosition);
        var projected = new List<(Vector2 A, Vector2 B, float Width)>();
        const int segments = 8;
        foreach (var route in _sphericalTradeBands)
        {
            for (int i = 0; i < segments; i++)
            {
                var a = route.A.Slerp(route.B, (float)i / segments).Normalized();
                var b = route.A.Slerp(route.B, (float)(i + 1) / segments).Normalized();
                if (!ClipTradeArc(a, b, camera, out var first, out var last)) continue;
                projected.Add((_camera.UnprojectPosition(_planet.ToGlobal(first)),
                    _camera.UnprojectPosition(_planet.ToGlobal(last)), route.Width));
            }
        }
        // Both passes use logical screen pixels, with lighter atlas-specific ink.
        // Draw all rims first so route junctions have no transverse white seam.
        foreach (var line in projected)
            _sphericalTradeInk.DrawLine(line.A, line.B, Colors.White,
                line.Width + 2f * GaCartographicStyle.StrategicTradeOutlinePixels, true);
        foreach (var line in projected)
            _sphericalTradeInk.DrawLine(line.A, line.B, Colors.Black, line.Width, true);
    }

    internal void FocusFirstPublishedTradeForRenderSmoke()
    {
        if (_sphericalTradeBands.Count == 0) throw new InvalidOperationException("Missing actual trade.");
        var band = _sphericalTradeBands[0];
        CenterOnGeography((band.A + band.B).Normalized());
    }

    internal void AssertSphericalTradeGeometry(FoundationLastClosedEconomicTradeView view)
    {
        if (!SphericalTradeEdgeIds.SequenceEqual(view.UsedEdges.OrderBy(e => e.Edge.FineStrategicEdgeId.Value).Select(e => e.Edge)))
            throw new InvalidOperationException("Strategic ink must contain precisely the authorized edges.");
        foreach (var route in _sphericalTradeBands.Zip(view.UsedEdges.OrderBy(e => e.Edge.FineStrategicEdgeId.Value)))
        {
            var band = route.First;
            if (band.Width != GaCartographicStyle.StrategicTradeWidth(route.Second.TotalCargoMicro) ||
                !ClipTradeArc(band.A, band.B, (band.A + band.B).Normalized() * 3f, out _, out _) ||
                ClipTradeArc(band.A, band.B, -(band.A + band.B).Normalized() * 3f, out _, out _))
                throw new InvalidOperationException("Strategic trade width or front/back visibility contract failed.");
        }
        var near = Vector3.Back;
        var far = Vector3.Right;
        var eye = near * 2f;
        if (!ClipTradeArc(near, far, eye, out var first, out var last) ||
            first.DistanceSquaredTo(near) > 1e-6f || Math.Abs(last.Dot(eye) - 1f) > 1e-4f ||
            !ClipTradeArc(far, near, eye, out first, out last) ||
            Math.Abs(first.Dot(eye) - 1f) > 1e-4f || last.DistanceSquaredTo(near) > 1e-6f)
            throw new InvalidOperationException("Strategic horizon clipping must work in both directions.");
    }

    private void RebuildSphericalSettledTradeRoutes()
    {
        VisibleSphericalSettledTradeCrossings = 0;
        _sphericalTradeBands.Clear();
        if (_planet is null || _signedG18RouteGeometry is null) { RefreshSphericalTradeVisibility(); return; }
        var view = _authorizedGlobeTrade;
        if (view is null || view.UsedEdges.Count == 0) { RefreshSphericalTradeVisibility(); return; }

        var seen = new HashSet<PhysicalTacticalEdgeId>();
        foreach (var edge in view.UsedEdges.OrderBy(e => e.Edge.FineStrategicEdgeId.Value))
        {
            if (!edge.Edge.IsValid || !seen.Add(edge.Edge) ||
                edge.Edge.FineGoldbergParameters != view.FineTopology ||
                edge.FirstTile.FineGoldbergParameters != view.FineTopology ||
                edge.SecondTile.FineGoldbergParameters != view.FineTopology || edge.TotalCargoMicro == 0UL)
                throw new InvalidOperationException("Globe routes require unique, real, positive physical crossings.");
            Vector3 a = ToGodot(_signedG18RouteGeometry.GetCell(edge.FirstTile.FineStrategicCellId).Center).Normalized();
            Vector3 b = ToGodot(_signedG18RouteGeometry.GetCell(edge.SecondTile.FineStrategicCellId).Center).Normalized();
            if (a.DistanceSquaredTo(b) < 1e-12f) throw new InvalidOperationException("Degenerate freight edge.");
            _sphericalTradeBands.Add((edge.Edge, a, b, GaCartographicStyle.StrategicTradeWidth(edge.TotalCargoMicro)));
        }
        VisibleSphericalSettledTradeCrossings = _sphericalTradeBands.Count;
        RefreshSphericalTradeVisibility();
        GD.Print($"GA_NEXT_R100_SPHERICAL_SETTLED_ROUTES_READY cycle={view.EconomicCycleOrdinal} " +
            $"visible_edges={VisibleSphericalSettledTradeCrossings} projection=great_circle privacy=participant_authorized " +
            "line_width=log_audited_cargo color=black_white horizon=clipped");
    }
}
