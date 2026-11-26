using Godot;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>First widget-first pass. All local cards remain reachable,
/// and collapsed content is the original control tree, not a duplicate UI.</summary>
public partial class Main
{
    private readonly Dictionary<string, (Button Header, VBoxContainer Content)> _widgetSections = new();

    private void AddWidgetSection(VBoxContainer root, string name, string icon, bool expanded)
    {
        // The previous first child was a plain heading. Keep all other
        // controls and their original signals, with only one new container.
        var old = root.GetChildren().OfType<Control>().ToArray();
        var heading = old.FirstOrDefault() as Label
            ?? throw new InvalidOperationException("Layer heading missing.");
        root.RemoveChild(heading);
        heading.QueueFree();
        var header = new Button { Name = $"GANextWidget{name}Header", Text = name.ToUpperInvariant(),
            Icon = GaHudWidgets.Icon("right"), Alignment = HorizontalAlignment.Left,
            IconAlignment = HorizontalAlignment.Right, CustomMinimumSize = new Vector2(0, 24),
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        GaVisualFoundation.StyleHudSectionHeader(header);
        root.AddThemeConstantOverride("separation", 2);
        root.AddChild(header);
        root.MoveChild(header, 0);
        var content = new VBoxContainer { Name = $"GANextWidget{name}Content",
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 2);
        root.AddChild(content);
        foreach (var control in old.Skip(1)) control.Reparent(content);
        _widgetSections.Add(name, (header, content));
        header.Pressed += () => SetWidgetSectionExpanded(name, !content.Visible);
        SetWidgetSectionExpanded(name, expanded);
    }

    private void SetWidgetSectionExpanded(string name, bool expanded)
    {
        if (!_widgetSections.TryGetValue(name, out var section)) return;
        bool changed = section.Content.Visible != expanded;
        section.Content.Visible = expanded;
        if (changed) GaHudMotion.SetDisclosure(section.Content, expanded);
        section.Header.Text = name.ToUpperInvariant();
        section.Header.Icon = GaHudWidgets.Icon(expanded ? "down" : "right");
        section.Header.TooltipText = expanded ? $"Collapse {name}" : $"Expand {name}";
    }

    private static void WidgetActionRow(Button first, Button second)
    {
        var parent = first.GetParent() ?? throw new InvalidOperationException("Action parent missing.");
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 8);
        int index = first.GetIndex();
        parent.AddChild(row);
        parent.MoveChild(row, index);
        first.Reparent(row);
        second.Reparent(row);
    }

    private void ConfigureWidgetDock(VBoxContainer infrastructure, VBoxContainer terrain,
        Button city, Button civilization)
    {
        var unit = _selectedUnitSlot!;
        // Actual buttons retain their original signal handlers, shortcuts
        // and disabled state. Refresh routines must not restore long captions.
        GaHudWidgets.IconButton(_localMoveButton!, "move", "Move (G): arm, then right-click destination on map; Esc cancels");
        GaHudWidgets.IconButton(_localCancelQueuedButton!, "cancel", "Remove or interrupt first order (X)");
        WidgetActionRow(_localMoveButton!, _localCancelQueuedButton!);
        _worldUnitDetails = new Button { Name = "GANextUnitDetails", Disabled = true };
        GaHudWidgets.IconButton(_worldUnitDetails, "details", "Selected worker: detailed planning");
        _worldUnitDetails.Pressed += () =>
        {
            if (_tacticalHost?.Snapshot.Workers.Workers.Any(w =>
                w.Owner == _humanCivilization && w.Id.Value == _selectedLocalWorkerId) == true)
                OpenR224WorkerWindow();
        };
        _localMoveButton!.GetParent().AddChild(_worldUnitDetails);
        GaHudWidgets.IconButton(_worldInfrastructureManage!, "connection", "Manage selected connection");
        GaHudWidgets.IconButton(city, "city", "Open selected city (Y)");
        GaHudWidgets.IconButton(_worldPlanetAtlas!, "globe", "Planet map (M)");
        GaHudWidgets.IconButton(civilization, "civilization", "Civilization (C)");
        WidgetActionRow(_worldPlanetAtlas!, civilization);
        // Integrate the existing navigation actions in the Planet header,
        // rather than paying for an additional full-width row in the footer.
        var planetActions = (HBoxContainer)_worldPlanetAtlas!.GetParent();
        var planetBody = (VBoxContainer)planetActions.GetParent();
        var planetHeading = (Label)planetBody.GetChild(0);
        planetHeading.Reparent(planetActions);
        planetActions.MoveChild(planetHeading, 0);
        planetHeading.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _worldPlanetAtlas.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        civilization.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        planetBody.MoveChild(planetActions, 0);

        // Compact only the widget-mode cards, not the shared windows/theme.
        foreach (var card in new[] { _worldUnitCard, _worldInfrastructureCard, _worldTerrainCard })
            card!.AddThemeStyleboxOverride("panel", GaVisualFoundation.HudOperationalCardSurface());
        _worldPlanetCard!.AddThemeStyleboxOverride("panel", GaVisualFoundation.HudCardSurface());
        GaHudEffects.AttachSurface(_worldPlanetCard, GaHudPlane.Footer);
        _worldLayerStack!.AddThemeConstantOverride("separation", 14);
        if (_worldUnitActivity is not null) _worldUnitActivity.Visible = false; // same activity in identity
        if (_localPendingLabel is not null) _localPendingLabel.Visible = false; // same queue in Unit
        if (_fineMovementDecision is not null) _fineMovementDecision.Visible = false; // move tooltip
        if (_localOrderStatus is not null && _localOrderStatus.Text.StartsWith("Select a worker and tile", StringComparison.Ordinal))
            _localOrderStatus.Text = ""; // retain subsequent command/rejection feedback
        if (_worldUnitProgress is not null) GaHudWidgets.CompactProgress(_worldUnitProgress);
        if (_worldInfrastructureProgress is not null) GaHudWidgets.CompactProgress(_worldInfrastructureProgress);
        ComposeOperationalCards(unit, infrastructure, terrain, city);
        AddWidgetSection(unit, "Unit", "unit", true);
        AddWidgetSection(infrastructure, "Infrastructure", "connection", false);
        AddWidgetSection(terrain, "Terrain", "terrain", false);
        // Local summaries stay visible. The latter two headings expand only
        // the optional detail widgets, not the primary object and action.
        PinOperationalSummary("Infrastructure", _hudInfrastructureSummary!);
        PinOperationalSummary("Terrain", _hudTerrainSummary!);
        RefreshOperationalFeedback();
    }

    private void RefreshWidgetUnitActions()
    {
        if (!_widgetSections.ContainsKey("Unit")) return;
        GaHudWidgets.IconButton(_localMoveButton!, "move", "Move (G): arm, then right-click destination on map; Esc cancels");
        GaHudWidgets.IconButton(_localCancelQueuedButton!, "cancel", "Remove or interrupt first order (X)");
        RefreshOperationalFeedback();
        RefreshHudMoveVisual();
    }

    private async Task ExerciseWidgetDock(FoundationTacticalHumanAiTurnHost host)
    {
        var hash = host.StateHash;
        var camera = _tacticalPreview!.TacticalCameraState;
        var queues = host.GetHumanPersistentQueues(_humanCivilization).ToArray();
        var scroll = _worldLayerScroll!;
        scroll.ScrollVertical = 0;
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_widgetSections.Count != 3 || !_widgetSections["Unit"].Content.Visible ||
            _widgetSections["Infrastructure"].Content.Visible || _widgetSections["Terrain"].Content.Visible)
            throw new InvalidOperationException("Widget defaults invalid.");
        var bounds = scroll.GetGlobalRect();
        // At minimum the three local headers must fit in the default smoke viewport.
        foreach (var section in _widgetSections.Values)
        {
            var rect = section.Header.GetGlobalRect();
            if (rect.Position.Y < bounds.Position.Y - 1f || rect.End.Y > bounds.End.Y + 1f)
                throw new InvalidOperationException($"Widget header clipped at startup: {section.Header.Text}; bounds={bounds}; rect={rect}");
        }
        AssertOperationalCardLayout(bounds);
        AssertMonochromeHud();
        GaHudEffects.ExerciseContract(_hudPortraitFrames, new[] { _hudUnitObject!, _hudInfrastructureObject!, _hudTerrainObject! },
            _worldPlanetCard!, new[] { _localMoveButton!, _localCancelQueuedButton!, _worldUnitDetails!, _worldInfrastructureManage! });
        var footer = _worldPlanetCard!.GetGlobalRect();
        var atlas = _worldPlanetAtlas ?? throw new InvalidOperationException("Planet action missing.");
        var planetHeader = atlas.GetParent() as HBoxContainer
            ?? throw new InvalidOperationException("Planet action is not in its header row.");
        if (planetHeader.GetParent()?.GetChild(0) != planetHeader ||
            planetHeader.GetChild(0) is not Label planetTitle || planetTitle.Text != "PLANET" ||
            !atlas.IsVisibleInTree() || !planetHeader.IsVisibleInTree())
            throw new InvalidOperationException("Compact Planet header or atlas action missing.");
        GD.Print($"GA_NEXT_R23061A1_LAYOUT local={bounds} planet={footer} terrain={_widgetSections["Terrain"].Header.GetGlobalRect()}");
        foreach (var name in new[] { "Unit", "Infrastructure", "Terrain" })
        {
            var section = _widgetSections[name];
            bool was = section.Content.Visible;
            section.Header.EmitSignal(Button.SignalName.Pressed);
            if (section.Content.Visible == was) throw new InvalidOperationException("Widget toggle failed.");
            section.Header.EmitSignal(Button.SignalName.Pressed);
            if (section.Content.Visible != was) throw new InvalidOperationException("Widget restore failed.");
        }
        foreach (var button in new[] { _localMoveButton, _localCancelQueuedButton, _worldUnitDetails, _worldInfrastructureManage, _worldPlanetAtlas })
            if (button?.Icon is null || !string.IsNullOrEmpty(button.Text) || string.IsNullOrEmpty(button.TooltipText))
                throw new InvalidOperationException("Icon action contract failed.");
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_worldPlanetCard.GetGlobalRect().Position.DistanceTo(footer.Position) > 1f ||
            host.StateHash != hash || _tacticalPreview.TacticalCameraState != camera ||
            !host.GetHumanPersistentQueues(_humanCivilization).SequenceEqual(queues))
            throw new InvalidOperationException("Widget layout mutated world or displaced Planet.");
        AssertOperationalFeedback();
        GD.Print("GA_NEXT_VISUAL01_MATERIAL_PASS neutral_planes=true cached_light=true independent_action_states=true motion_attached=true");
        GD.Print("GA_NEXT_R23064_MONO_HUD_PASS neutral_palette=true framed_portraits=true compact_actions=true separate_slots=true");
        GD.Print("GA_NEXT_R23062_OPERATIONAL_CARD_PASS visual_summaries=true action_strips=true instruction_hidden=true feedback_preserved=true");
        GD.Print("GA_NEXT_R23061A_WIDGET_SHELL_PASS icon_actions=true collapsible=true headers_visible=true fixed_planet=true read_only=true");
    }
}
