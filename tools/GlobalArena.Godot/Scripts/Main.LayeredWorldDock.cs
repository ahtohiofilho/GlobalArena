using Godot;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>R230.2: presentation shell only. One bounded scroll for the
/// three local layers; Planet is a fixed footer while the dock is open.
/// Reparent existing command controls rather than duplicate handlers.</summary>
public partial class Main
{
    private bool LayeredWorldDock => CompactOperationalDock &&
        _tacticalHost?.ExperimentalLocalEconomyEnabled == true;

    private VBoxContainer? _worldLayerStack;
    private PanelContainer? _worldUnitCard, _worldInfrastructureCard, _worldTerrainCard, _worldPlanetCard;
    private ScrollContainer? _worldLayerScroll;

    private static PanelContainer AddWorldLayer(VBoxContainer parent, string name,
        string heading, out VBoxContainer body)
    {
        var frame = new PanelContainer { Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        frame.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        parent.AddChild(frame);
        body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        frame.AddChild(body);
        body.AddChild(new Label { Text = heading });
        return frame;
    }

    private static Label WorldLayerPlaceholder(string message) => new()
    {
        Text = message, AutowrapMode = TextServer.AutowrapMode.WordSmart,
        SizeFlagsHorizontal = SizeFlags.ExpandFill
    };

    private void CreateLayeredWorldDock(VBoxContainer dock, ScrollContainer scroll,
        VBoxContainer scrollBody, VBoxContainer orders, Button city, Button civilization,
        GlobePreview globe)
    {
        _worldLayerScroll = scroll;
        _worldLayerStack = new VBoxContainer { Name = "GANextWorldLayerStack",
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _worldLayerStack.AddThemeConstantOverride("separation", 8);
        scrollBody.AddChild(_worldLayerStack);
        scrollBody.MoveChild(_worldLayerStack, 0);

        _worldUnitCard = (PanelContainer)_selectedUnitSlot!.GetParent();
        _worldUnitCard.Reparent(_worldLayerStack);
        // Reuse the existing controls, pending labels and detail parking.
        // There is still exactly one worker selector and one move handler.
        orders.Reparent(_selectedUnitSlot);
        CreateWorldUnitReadout(_selectedUnitSlot);

        _worldInfrastructureCard = AddWorldLayer(_worldLayerStack,
            "GANextInfrastructureSlot", "INFRASTRUCTURE", out var infrastructure);
        CreateWorldInfrastructureReadout(infrastructure);

        _worldTerrainCard = AddWorldLayer(_worldLayerStack,
            "GANextTerrainSlot", "TERRAIN", out var terrain);
        _operationalContext!.Reparent(terrain);
        CreateWorldTerrainReadout(terrain);
        city.Reparent(terrain);

        _worldPlanetCard = AddWorldLayer(dock, "GANextPlanetSlot", "PLANET", out var planet);
        CreateWorldPlanetReadout(planet, globe, civilization);
        GD.Print("GA_NEXT_R2302_LAYERED_WORLD_DOCK_READY unit=true infrastructure=true terrain=true planet_fixed=true");
    }

    private async Task ExerciseLayeredWorldDock(FoundationTacticalHumanAiTurnHost host)
    {
        var hash = host.StateHash;
        var camera = _tacticalPreview!.TacticalCameraState;
        var stack = _worldLayerStack ?? throw new InvalidOperationException("Missing local layer stack.");
        var scroll = _worldLayerScroll ?? throw new InvalidOperationException("Missing layer scroll.");
        var unit = _worldUnitCard ?? throw new InvalidOperationException("Missing Unit slot.");
        var infra = _worldInfrastructureCard ?? throw new InvalidOperationException("Missing Infrastructure slot.");
        var terrain = _worldTerrainCard ?? throw new InvalidOperationException("Missing Terrain slot.");
        var planet = _worldPlanetCard ?? throw new InvalidOperationException("Missing Planet slot.");
        var inset = _auxiliaryPlanet ?? throw new InvalidOperationException("Missing signed planet preview.");
        if (!stack.GetChildren().SequenceEqual(new Node[] { unit, infra, terrain }) ||
            !scroll.IsAncestorOf(stack) || scroll.IsAncestorOf(planet) ||
            planet.GetParent() != scroll.GetParent() || planet.GetIndex() <= scroll.GetIndex() ||
            !planet.IsAncestorOf(inset) || !unit.IsAncestorOf(_localWorkerSelector!) ||
            !unit.IsAncestorOf(_localMoveButton!) || !unit.IsAncestorOf(_localCancelQueuedButton!) ||
            _operationalContext?.GetParent()?.GetParent() != terrain ||
            _workspaceWindow?.CustomMinimumSize.X != R2212DockWidth)
            throw new InvalidOperationException("Layered dock order, controls or fixed footer contract failed.");
        if (!unit.IsVisibleInTree() || !infra.IsVisibleInTree() || !terrain.IsVisibleInTree() ||
            !planet.IsVisibleInTree() || !inset.IsVisibleInTree() ||
            _r224Open?.IsVisibleInTree() == true || _persistentQueuePanel?.IsVisibleInTree() == true)
            throw new InvalidOperationException("Layer visibility or private detail parking regressed.");
        var footer = planet.GetGlobalRect();
        var scrollBounds = scroll.GetGlobalRect();
        if (footer.Position.Y < scrollBounds.End.Y - 1f ||
            footer.End.Y > _workspaceWindow!.GetGlobalRect().End.Y + 1f)
            throw new InvalidOperationException("Planet footer overlaps scroll or escapes dock.");
        var bar = scroll.GetVScrollBar();
        var maxScroll = Math.Max(0, (int)Math.Floor(bar.MaxValue - bar.Page));
        scroll.ScrollVertical = maxScroll;
        for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if ((maxScroll > 0 && scroll.ScrollVertical <= 0) ||
            planet.GetGlobalRect().Position.DistanceTo(footer.Position) > 1f ||
            planet.GetGlobalRect().Size.DistanceTo(footer.Size) > 1f)
            throw new InvalidOperationException("Local layer scrolling displaced Planet footer.");
        scroll.ScrollVertical = 0;
        if (host.StateHash != hash || _tacticalPreview.TacticalCameraState != camera)
            throw new InvalidOperationException("Layered dock layout mutated canonical state or camera.");
        GD.Print("GA_NEXT_R2302_LAYERED_WORLD_DOCK_PASS four_slots=true reused_controls=true fixed_planet=true hash_unchanged=true");
    }
}
