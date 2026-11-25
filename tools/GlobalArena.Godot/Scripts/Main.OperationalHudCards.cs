using Godot;

namespace GlobalArena.Godot;

/// <summary>R230.6.2: nested object cards and distinct action strips.
/// Keep the existing authoritative controls and optional detailed widgets.
/// The map/City/planner remain the other entry surfaces for commands.</summary>
public partial class Main
{
    private VBoxContainer? _hudInfrastructureSummary, _hudTerrainSummary;
    private TextureRect? _hudInfrastructurePicture;
    private Label? _hudInfrastructureKind, _hudFeedback;
    private PanelContainer? _hudUnitObject, _hudInfrastructureObject, _hudTerrainObject;
    private HBoxContainer? _hudUnitActions;

    private static PanelContainer HudObject(VBoxContainer parent, string name, out HBoxContainer row)
    {
        var panel = new PanelContainer { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", GaVisualFoundation.HudObjectSurface());
        parent.AddChild(panel);
        row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 6);
        panel.AddChild(row);
        return panel;
    }

    private static VBoxContainer HudObjectFacts(HBoxContainer row)
    {
        var facts = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter };
        facts.AddThemeConstantOverride("separation", 2);
        row.AddChild(facts);
        return facts;
    }

    private static void HudPicture(TextureRect picture, string tooltip)
    {
        picture.CustomMinimumSize = new Vector2(52, 52);
        picture.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        picture.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        picture.TooltipText = tooltip;
    }

    private void ComposeOperationalCards(VBoxContainer unit, VBoxContainer infrastructure, VBoxContainer terrain, Button city)
    {
        // Unit: existing silhouette + identity/selector/status inside one
        // object; the three real actions sit together underneath.
        var oldUnitRow = (HBoxContainer)_selectedUnitIdentity!.GetParent();
        _hudUnitActions = (HBoxContainer)_localMoveButton!.GetParent();
        var orderPanel = _hudUnitActions.GetParent();
        while (orderPanel.GetParent() != unit) orderPanel = orderPanel.GetParent();
        _hudUnitObject = HudObject(unit, "GANextHudUnitObject", out var unitRow);
        var portrait = (Control)oldUnitRow.GetChild(0);
        portrait.Reparent(unitRow);
        portrait.CustomMinimumSize = new Vector2(52, 62);
        portrait.SizeFlagsVertical = SizeFlags.ExpandFill;
        var unitFacts = HudObjectFacts(unitRow);
        _localWorkerSelector!.Reparent(unitFacts);
        _localWorkerSelector.CustomMinimumSize = new Vector2(0, 26);
        _selectedUnitIdentity.Reparent(unitFacts);
        _worldUnitReadout!.Reparent(unitFacts);
        oldUnitRow.Visible = false;
        _hudUnitActions.Reparent(unit);
        if (orderPanel is Control legacyOrders) legacyOrders.Visible = false;
        // The existing full status string remains available to the original
        // logic. Never show the QA tutorial or per-turn journal prose here.
        _localOrderStatus!.Visible = false;
        _hudFeedback = new Label { Name = "GANextHudOrderFeedback", Visible = false,
            MouseFilter = MouseFilterEnum.Pass };
        _hudFeedback.AddThemeColorOverride("font_color", GaVisualFoundation.Warm);
        unit.AddChild(_hudFeedback);

        // Infrastructure: actual selected connection type, capital/freight
        // and conditional work status. Detailed geographic picker is optional.
        _hudInfrastructureSummary = new VBoxContainer { Name = "GANextHudInfrastructureSummary" };
        _hudInfrastructureSummary.AddThemeConstantOverride("separation", 2);
        infrastructure.AddChild(_hudInfrastructureSummary);
        _hudInfrastructureObject = HudObject(_hudInfrastructureSummary, "GANextHudInfrastructureObject", out var infraRow);
        _hudInfrastructurePicture = GaHudWidgets.MetricIcon("connection");
        HudPicture(_hudInfrastructurePicture, "Selected physical connection");
        infraRow.AddChild(_hudInfrastructurePicture);
        var infraFacts = HudObjectFacts(infraRow);
        _hudInfrastructureKind = new Label { Text = "Connection" };
        infraFacts.AddChild(_hudInfrastructureKind);
        _worldInfrastructureMetrics!.Reparent(infraFacts);
        _worldInfrastructureMetrics.AddThemeConstantOverride("separation", 1);
        _worldInfrastructureManage!.Reparent(_hudInfrastructureSummary);
        RefreshWorldInfrastructureDetail();

        // Terrain: surface and traversal are immediate context. The index
        // grid and crop potentials remain in the existing expandable details.
        _hudTerrainSummary = new VBoxContainer { Name = "GANextHudTerrainSummary" };
        terrain.AddChild(_hudTerrainSummary);
        _hudTerrainObject = HudObject(_hudTerrainSummary, "GANextHudTerrainObject", out var terrainRow);
        var oldSurface = (Control)_terrainSurface!.GetParent();
        _terrainSurfaceIcon!.Reparent(terrainRow);
        HudPicture(_terrainSurfaceIcon, "Selected signed terrain");
        var terrainFacts = HudObjectFacts(terrainRow);
        _terrainSurface.Reparent(terrainFacts);
        oldSurface.Visible = false;
        var access = (HBoxContainer)_terrainFootState!.GetParent().GetParent();
        access.Reparent(terrainFacts);
        city.Reparent(access);
        city.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        _operationalContext!.Visible = false;
        RefreshWorldTerrain();
    }

    private void PinOperationalSummary(string name, VBoxContainer summary)
    {
        var section = _widgetSections[name];
        var parent = section.Header.GetParent();
        summary.Reparent(parent);
        parent.MoveChild(summary, section.Header.GetIndex() + 1);
    }

    private void RefreshOperationalFeedback()
    {
        if (_hudFeedback is null || _localOrderStatus is null) return;
        _localOrderStatus.Visible = false;
        var message = _localOrderStatus.Text;
        // Only exceptions require a persistent warning. Positive order
        // state is already represented by the queue/pending widgets.
        bool rejected = message.Contains("rejected", StringComparison.OrdinalIgnoreCase);
        _hudFeedback.Text = rejected ? "Order rejected" : "";
        _hudFeedback.TooltipText = rejected ? message : "";
        _hudFeedback.Visible = rejected;
    }

    private void AssertOperationalCardLayout(Rect2 bounds)
    {
        var objects = new[] { _hudUnitObject, _hudInfrastructureObject, _hudTerrainObject };
        // Record full geometry before asserting so any remaining overflow
        // can be diagnosed in one run. Require the complete local cards,
        // not just their headings or the upper edge of the last object.
        GD.Print($"GA_NEXT_R23062A_LAYOUT viewport={bounds} unit={_worldUnitCard!.GetGlobalRect()} infrastructure={_worldInfrastructureCard!.GetGlobalRect()} terrain={_worldTerrainCard!.GetGlobalRect()} planet={_worldPlanetCard!.GetGlobalRect()}");
        foreach (var panel in objects)
        {
            if (panel?.IsVisibleInTree() != true)
                throw new InvalidOperationException("Operational object summary missing at startup.");
            var rect = panel.GetGlobalRect();
            if (rect.Position.Y < bounds.Position.Y - 1f || rect.End.Y > bounds.End.Y + 1f)
                throw new InvalidOperationException($"Operational object clipped: {panel.Name} {rect} in {bounds}");
        }
        foreach (var card in new[] { _worldUnitCard!, _worldInfrastructureCard!, _worldTerrainCard! })
        {
            var rect = card.GetGlobalRect();
            if (rect.Position.Y < bounds.Position.Y - 1f || rect.End.Y > bounds.End.Y + 1f)
                throw new InvalidOperationException($"Complete operational card clipped: {card.Name} {rect} in {bounds}");
        }
        if (_hudUnitActions?.IsVisibleInTree() != true ||
            _hudUnitObject!.IsAncestorOf(_hudUnitActions) ||
            !_hudUnitObject.IsAncestorOf(_localWorkerSelector!) ||
            _hudInfrastructureSummary?.IsAncestorOf(_worldInfrastructureManage!) != true ||
            _worldInfrastructureDiagram?.IsVisibleInTree() == true ||
            _terrainWidgets?.IsVisibleInTree() == true ||
            _localOrderStatus?.IsVisibleInTree() != false ||
            _hudFeedback?.IsVisibleInTree() != false)
            throw new InvalidOperationException("Operational card hierarchy or instruction suppression failed.");
        var actions = _hudUnitActions.GetGlobalRect();
        if (actions.End.Y > bounds.End.Y + 1f ||
            _worldInfrastructureManage!.GetGlobalRect().End.Y > bounds.End.Y + 1f)
            throw new InvalidOperationException("Default operational actions clipped.");
    }

    private void AssertOperationalFeedback()
    {
        var original = _localOrderStatus!.Text;
        _localOrderStatus.Text = "Move rejected: test presentation";
        RefreshOperationalFeedback();
        if (_hudFeedback?.Visible != true || _hudFeedback.Text != "Order rejected" ||
            _hudFeedback.TooltipText != _localOrderStatus.Text || _localOrderStatus.Visible)
            throw new InvalidOperationException("Operational error feedback was lost.");
        _localOrderStatus.Text = original;
        RefreshOperationalFeedback();
        if (_hudFeedback.Visible)
            throw new InvalidOperationException("Instruction text leaked into compact feedback.");
    }
}
