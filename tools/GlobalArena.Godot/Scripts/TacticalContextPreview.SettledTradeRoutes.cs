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
    private MeshInstance3D? _settledTradeMesh;
    private FoundationLastClosedEconomicTradeView? _lastAuthorizedTrade;
    private bool _showSettledTradeRoutes = true;

    public int VisibleSettledTradeCrossings { get; private set; }

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
        _settledTradeMesh = new MeshInstance3D { Visible = false };
        var material = MakeCoplanarOverlayMaterial();
        material.RenderPriority = 2;
        _settledTradeMesh.MaterialOverride = material;
        _world.AddChild(_settledTradeMesh);
    }

    public void SetSettledTradeRoutesVisible(bool enabled)
    {
        _showSettledTradeRoutes = enabled;
        if (_settledTradeMesh is not null)
            _settledTradeMesh.Visible =
                enabled && _settledTradeMesh.Mesh is not null;
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

    private static Color SettledTradeOwnerColor(
        FoundationPhysicalTradeEdgeLoad crossing)
    {
        var baseColor = crossing.OwnershipKind switch
        {
            FoundationInfrastructureOwnershipKind.Owned
                when crossing.Owner is { IsValid: true } owner =>
                GaCivilizationVisualIdentity.ColorFor(owner.Value),
            FoundationInfrastructureOwnershipKind.UnownedInfrastructure =>
                new Color("#c5ced8"),
            FoundationInfrastructureOwnershipKind.NoInfrastructure =>
                new Color("#a8b3bf"),
            _ => throw new InvalidOperationException(
                "Trade visualization cannot invent a road owner.")
        };
        baseColor.A = 0.86f;
        return baseColor;
    }

    private void RebuildSettledTradeMesh()
    {
        VisibleSettledTradeCrossings = 0;
        if (_settledTradeMesh is null) return;
        var old = _settledTradeMesh.Mesh;
        _settledTradeMesh.Mesh = null;
        _settledTradeMesh.Visible = false;
        old?.Dispose();

        var view = _lastAuthorizedTrade;
        if (view is null || _cachedPhysicalMap is null ||
            _tileCenters.Count == 0 || view.UsedEdges.Count == 0)
            return;
        if (view.FineTopology !=
            _cachedPhysicalMap.Refinement.FineParameters)
            throw new InvalidOperationException(
                "Visible trade edge belongs to another real topology.");
        // Only intersections of the private-authorized edges with the
        // CURRENT resident presentation chart are drawn. Off-screen
        // trade remains invisible; there is no inferred continuation.
        var visible = view.UsedEdges
            .Where(edge => _tileCenters.ContainsKey(
                    edge.FirstTile.FineStrategicCellId.Value) &&
                _tileCenters.ContainsKey(
                    edge.SecondTile.FineStrategicCellId.Value))
            .OrderBy(edge => edge.Edge.FineStrategicEdgeId.Value)
            .ToArray();
        if (visible.Length == 0) return;
        ulong maxVolume = visible.Max(e => e.TotalCargoMicro);
        if (maxVolume == 0UL)
            throw new InvalidOperationException(
                "Zero-volume crossings are not completed trade routes.");

        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        var seen = new HashSet<PhysicalTacticalEdgeId>();
        foreach (var edge in visible)
        {
            if (!seen.Add(edge.Edge) || !edge.Edge.IsValid ||
                edge.Edge.FineGoldbergParameters != view.FineTopology ||
                edge.TotalCargoMicro == 0UL)
                throw new InvalidOperationException(
                    "Only unique, nonempty canonical crossings are drawable.");
            Vector3 a = _tileCenters[
                edge.FirstTile.FineStrategicCellId.Value];
            Vector3 b = _tileCenters[
                edge.SecondTile.FineStrategicCellId.Value];
            Vector3 delta = b - a;
            if (delta.LengthSquared() < 1e-12f) continue;

            // Larger AUDITED participant cargo makes a thicker band.
            // sqrt compression is purely aesthetic and deterministic:
            // it never alters market cargo, geography or freight costs.
            float relative = MathF.Sqrt(
                (float)((double)edge.TotalCargoMicro / maxVolume));
            float fullWidth = 0.0018f + 0.0072f * relative;
            Vector3 side = new Vector3(-delta.Z, 0f, delta.X)
                .Normalized() * (fullWidth / 2f);
            Color color = SettledTradeOwnerColor(edge);
            AddColored(tool, a + side, color);
            AddColored(tool, b + side, color);
            AddColored(tool, b - side, color);
            AddColored(tool, a + side, color);
            AddColored(tool, b - side, color);
            AddColored(tool, a - side, color);
            VisibleSettledTradeCrossings++;
        }
        var mesh = tool.Commit();
        tool.Dispose();
        if (mesh is null || VisibleSettledTradeCrossings == 0)
        {
            mesh?.Dispose();
            return;
        }
        _settledTradeMesh.Mesh = mesh;
        _settledTradeMesh.Visible = _showSettledTradeRoutes;
    }
}
