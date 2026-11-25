using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// View-only mesh of REAL settled freight on canonical physical center-to-
/// center edges. The tactical map NEVER calls the economic solver, receives
/// a global freight audit, invents an edge or infers diplomatic visibility.
/// The game host supplies a PRIVATE participant-authorized read view.
/// </summary>
public partial class TacticalContextPreview
{
    private Control? _tradeInkClip;
    private Node2D? _tradeInk;
    private readonly List<(PhysicalTacticalEdgeId Id, Vector3 A, Vector3 B, float Width)> _tradeBands = new();
    private Transform3D _lastInkCamera;
    private Vector2 _lastInkSize;
    private float _lastInkZoom = -1f;

    internal IReadOnlyList<PhysicalTacticalEdgeId> DrawnTradeEdgeIds =>
        _tradeBands.Select(x => x.Id).ToArray();

    internal IReadOnlyList<PhysicalTacticalEdgeId> ExpectedResidentTradeEdges(FoundationLastClosedEconomicTradeView view) =>
        view.UsedEdges.Where(e => _tileCenters.ContainsKey(e.FirstTile.FineStrategicCellId.Value) &&
            _tileCenters.ContainsKey(e.SecondTile.FineStrategicCellId.Value))
            .Select(e => e.Edge).OrderBy(e => e.FineStrategicEdgeId.Value).ToArray();
    private FoundationLastClosedEconomicTradeView? _lastAuthorizedTrade;
    private bool _showSettledTradeRoutes = true;

    public int VisibleSettledTradeCrossings { get; private set; }
    public bool IsSettledTradeLayerVisible =>
        _tradeInkClip?.Visible ?? false;

    public bool HasSettledTradeData =>
        _lastAuthorizedTrade is not null &&
        _lastAuthorizedTrade.Availability !=
            FoundationEconomicTradeOverlayAvailability.NoClosedCycle;

    public ulong? PresentedEconomicCycleOrdinal =>
        HasSettledTradeData
            ? _lastAuthorizedTrade!.EconomicCycleOrdinal : null;

    private void InitializeSettledTradeMesh()
    {
        if (_world is null)
            throw new InvalidOperationException(
                "Trade overlay needs the existing tactical world.");
        // Screen-space layer: the white outline remains one logical pixel
        // on each side at every zoom. Clip to the tactical viewport, and
        // keep it beneath the later site/worker labels and above terrain.
        _tradeInkClip = new Control { Name = "GANextTradeInk", MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true, Visible = false };
        AddChild(_tradeInkClip);
        _tradeInkClip.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _tradeInk = new Node2D();
        _tradeInkClip.AddChild(_tradeInk);
        _tradeInk.Draw += DrawTradeInk;
    }

    public void SetSettledTradeRoutesVisible(bool enabled)
    {
        _showSettledTradeRoutes = enabled;
        if (_tradeInkClip is not null)
            _tradeInkClip.Visible = enabled && _tradeBands.Count > 0;
        _tradeInk?.QueueRedraw();
    }

    /// <summary>
    /// Receives ONLY the authenticated view authorized for ONE civilization.
    /// Called from Main's host read boundary, never from the event's
    /// underlying global audit or the next-cycle congestion quote memory.
    /// </summary>
    public void PresentSettledTradeRoutes(
        FoundationLastClosedEconomicTradeView? authorizedView)
    {
        _lastAuthorizedTrade = authorizedView is
            { Availability:
                FoundationEconomicTradeOverlayAvailability.NoClosedCycle }
            ? null : authorizedView;
        if (_lastAuthorizedTrade is not null)
        {
            if (!_lastAuthorizedTrade.FineTopology.IsValid ||
                _lastAuthorizedTrade.EconomicCycleOrdinal is not > 0UL ||
                _lastAuthorizedTrade.PublishedHash is null ||
                string.IsNullOrWhiteSpace(_lastAuthorizedTrade.CargoUnitCode))
                throw new InvalidOperationException(
                    "Only an actual closed economic cycle may draw routes.");
            if (_cachedPhysicalMap is not null &&
                _lastAuthorizedTrade.FineTopology !=
                    _cachedPhysicalMap.Refinement.FineParameters)
                throw new InvalidOperationException(
                    "Trade route belongs to a foreign fine physical topology.");
        }
        RebuildSettledTradeMesh();
    }

    private void UpdateTradeInkProjection()
    {
        if (_tradeInkClip?.Visible != true || _camera is null) return;
        if (_lastInkCamera == _camera.GlobalTransform && _lastInkZoom == _camera.Size && _lastInkSize == Size) return;
        _lastInkCamera = _camera.GlobalTransform;
        _lastInkZoom = _camera.Size;
        _lastInkSize = Size;
        _tradeInk?.QueueRedraw();
    }

    private void DrawTradeInk()
    {
        if (_tradeInk is null || _camera is null || _innerViewport is null || _innerViewport.Size.Y <= 0 ||
            _innerViewport.Size.X <= 0) return;
        Vector2 scale = new(Size.X / _innerViewport.Size.X, Size.Y / _innerViewport.Size.Y);
        var screen = _tradeBands.Select(b => (A: _camera.UnprojectPosition(b.A) * scale,
            B: _camera.UnprojectPosition(b.B) * scale, b.Width)).ToArray();
        // All outlines first, then all cores: no white transverse seams
        // where two real crossings join at a physical node.
        foreach (var line in screen)
            _tradeInk.DrawLine(line.A, line.B, Colors.White, line.Width + 2f * GaCartographicStyle.TradeOutlinePixels, true);
        foreach (var line in screen)
            _tradeInk.DrawLine(line.A, line.B, Colors.Black, line.Width, true);
    }

    private void RebuildSettledTradeMesh()
    {
        VisibleSettledTradeCrossings = 0;
        _tradeBands.Clear();
        if (_tradeInkClip is null) return;
        _tradeInkClip.Visible = false;
        _tradeInk?.QueueRedraw();
        var view = _lastAuthorizedTrade;
        if (view is null || _cachedPhysicalMap is null || _tileCenters.Count == 0 || view.UsedEdges.Count == 0) return;
        if (view.FineTopology != _cachedPhysicalMap.Refinement.FineParameters)
            throw new InvalidOperationException("Visible trade edge belongs to another real topology.");
        var expected = ExpectedResidentTradeEdges(view).ToHashSet();
        var seen = new HashSet<PhysicalTacticalEdgeId>();
        foreach (var edge in view.UsedEdges.OrderBy(e => e.Edge.FineStrategicEdgeId.Value))
        {
            if (!expected.Contains(edge.Edge)) continue;
            if (!seen.Add(edge.Edge) || !edge.Edge.IsValid ||
                edge.Edge.FineGoldbergParameters != view.FineTopology || edge.TotalCargoMicro == 0UL)
                throw new InvalidOperationException("Only unique, nonempty canonical crossings are drawable.");
            ulong first = edge.FirstTile.FineStrategicCellId.Value, second = edge.SecondTile.FineStrategicCellId.Value;
            Vector3 a = TryGetSitePolygonCentroid(first, out var ca) ? ca : _tileCenters[first];
            Vector3 b = TryGetSitePolygonCentroid(second, out var cb) ? cb : _tileCenters[second];
            if (a.DistanceSquaredTo(b) < 1e-12f)
                throw new InvalidOperationException("Degenerate freight edge.");
            _tradeBands.Add((edge.Edge, a, b, GaCartographicStyle.TradeWidth(edge.TotalCargoMicro)));
        }
        VisibleSettledTradeCrossings = _tradeBands.Count;
        _tradeInkClip.Visible = _showSettledTradeRoutes && _tradeBands.Count > 0;
        _tradeInk?.QueueRedraw();
    }
}
