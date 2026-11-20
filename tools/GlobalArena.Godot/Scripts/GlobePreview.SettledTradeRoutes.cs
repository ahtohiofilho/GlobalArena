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
    private MeshInstance3D? _settledGlobeRoutes;
    private FoundationLastClosedEconomicTradeView? _authorizedGlobeTrade;
    private bool _showSettledGlobeRoutes = true;

    public int VisibleSphericalSettledTradeCrossings { get; private set; }

    public void SetSphericalSettledTradeRoutesVisible(bool enabled)
    {
        _showSettledGlobeRoutes = enabled;
        if (_settledGlobeRoutes is not null)
            _settledGlobeRoutes.Visible = enabled &&
                _mapMode == SignedG18MapMode &&
                _settledGlobeRoutes.Mesh is not null;
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
             authorizedView.FineTopology != new GoldbergParameters(18, 0) ||
             authorizedView.EconomicCycleOrdinal is not > 0UL ||
             authorizedView.PublishedHash is null ||
             string.IsNullOrWhiteSpace(authorizedView.CargoUnitCode)))
            throw new InvalidOperationException(
                "Spherical trade must use a published participant-private G18 cycle.");
        _authorizedGlobeTrade = authorizedView is
            { Availability: FoundationEconomicTradeOverlayAvailability.NoClosedCycle }
                ? null : authorizedView;
        RebuildSphericalSettledTradeRoutes();
    }

    private static Color SphericalTradeOwnerColor(
        FoundationPhysicalTradeEdgeLoad edge)
    {
        Color tint = edge.OwnershipKind switch
        {
            FoundationInfrastructureOwnershipKind.Owned
                when edge.Owner is { IsValid: true } owner =>
                GaCivilizationVisualIdentity.ColorFor(owner.Value),
            FoundationInfrastructureOwnershipKind.UnownedInfrastructure =>
                new Color("#c5ced8"),
            FoundationInfrastructureOwnershipKind.NoInfrastructure =>
                new Color("#a8b3bf"),
            _ => throw new InvalidOperationException(
                "A trade edge cannot fabricate an infrastructure owner.")
        };
        tint.A = 0.92f;
        return tint;
    }

    private void RebuildSphericalSettledTradeRoutes()
    {
        VisibleSphericalSettledTradeCrossings = 0;
        if (_planet is null || _signedG18RouteGeometry is null) return;
        if (_settledGlobeRoutes is null)
        {
            var material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                RenderPriority = 2
            };
            _settledGlobeRoutes = new MeshInstance3D
            {
                MaterialOverride = material, Visible = false
            };
            _planet.AddChild(_settledGlobeRoutes);
        }

        var previous = _settledGlobeRoutes.Mesh;
        _settledGlobeRoutes.Mesh = null;
        _settledGlobeRoutes.Visible = false;
        previous?.Dispose();

        var view = _authorizedGlobeTrade;
        if (view is null || view.UsedEdges.Count == 0) return;
        var routes = view.UsedEdges
            .OrderBy(e => e.Edge.FineStrategicEdgeId.Value).ToArray();
        ulong peak = routes.Max(e => e.TotalCargoMicro);
        if (peak == 0UL)
            throw new InvalidOperationException("Zero-volume routes are not settled.");

        var seen = new HashSet<PhysicalTacticalEdgeId>();
        var mesh = new SurfaceTool();
        mesh.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var edge in routes)
        {
            if (!edge.Edge.IsValid || !seen.Add(edge.Edge) ||
                edge.Edge.FineGoldbergParameters != view.FineTopology ||
                edge.FirstTile.FineGoldbergParameters != view.FineTopology ||
                edge.SecondTile.FineGoldbergParameters != view.FineTopology ||
                edge.TotalCargoMicro == 0UL)
                throw new InvalidOperationException(
                    "Globe routes require unique, real, positive G18 crossings.");

            Vector3 a = ToGodot(_signedG18RouteGeometry
                .GetCell(edge.FirstTile.FineStrategicCellId).Center).Normalized();
            Vector3 b = ToGodot(_signedG18RouteGeometry
                .GetCell(edge.SecondTile.FineStrategicCellId).Center).Normalized();
            if (a.DistanceSquaredTo(b) < 1e-12f)
                throw new InvalidOperationException("Degenerate freight edge.");
            float relative = MathF.Sqrt(
                (float)((double)edge.TotalCargoMicro / peak));
            float width = 0.0016f + 0.0064f * relative;
            Color color = SphericalTradeOwnerColor(edge);

            // Eight small great-circle segments hug the real 3D sphere.
            // The lift is purely visual: it does not change terrain shape.
            const int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                Vector3 centerA = a.Slerp(b, (float)i / segments).Normalized();
                Vector3 centerB = a.Slerp(b, (float)(i + 1) / segments).Normalized();
                var tangent = (centerB - centerA).Normalized();
                var sideA = centerA.Cross(tangent).Normalized() * (width * 0.5f);
                var sideB = centerB.Cross(tangent).Normalized() * (width * 0.5f);
                Vector3 p1 = centerA * 1.003f + sideA;
                Vector3 p2 = centerB * 1.003f + sideB;
                Vector3 p3 = centerB * 1.003f - sideB;
                Vector3 p4 = centerA * 1.003f - sideA;
                AddVertex(mesh, p1, color);
                AddVertex(mesh, p2, color);
                AddVertex(mesh, p3, color);
                AddVertex(mesh, p1, color);
                AddVertex(mesh, p3, color);
                AddVertex(mesh, p4, color);
            }
            VisibleSphericalSettledTradeCrossings++;
        }
        var committed = mesh.Commit();
        mesh.Dispose();
        if (committed is null)
            throw new InvalidOperationException("Spherical routes did not form a mesh.");
        _settledGlobeRoutes.Mesh = committed;
        _settledGlobeRoutes.Visible = _showSettledGlobeRoutes &&
            _mapMode == SignedG18MapMode;
        GD.Print($"GA_NEXT_R100_SPHERICAL_SETTLED_ROUTES_READY " +
            $"cycle={view.EconomicCycleOrdinal} " +
            $"visible_edges={VisibleSphericalSettledTradeCrossings} " +
            "projection=great_circle privacy=participant_authorized " +
            "line_width=audited_cargo color=infrastructure_owner");
    }
}
