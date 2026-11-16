using Godot;
using GlobalArena.Runtime;
using GlobalArena.World;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

// GA-UI-FOUNDATION-01: presentation shell owns layout; GlobePreview owns only globe behavior.
public partial class Main : Control
{
    private Label? _status;
    // GA_LAYOUT_DIAGNOSTIC_02: temporary non-mutating runtime metrics.
    private Control? _layoutDiagnosticFrame;
    private Control? _layoutDiagnosticRoot;
    private string _lastLoggedLayoutSignature = "";
    private Label? _tacticalBridgeStatus;
    private Button? _openTacticalContext;
    // GA-UI-MVP: the globe-only calibration controls live in Main sidebar.
    private readonly System.Collections.Generic.List<Control> _strategicCalibrationGroups = new();
    private Button? _returnStrategicContext;
    private CheckBox? _tacticalTileEdges;
    private GlobePreview? _strategicGlobe;
    private TacticalContextPreview? _tacticalPreview;
    // Presentation-only sidebar ownership for map-free tactical controls.
    private VBoxContainer? _tacticalHudPanel;
    private VBoxContainer? _strategicHudPanel;
    private VBoxContainer? _tacticalInspector;
    private Label? _tacticalTileSummary;
    private Label? _tacticalTileTerrain;
    private Label? _tacticalTileRuntime;
    private WorldState? _attachedRuntimeState;
    // New-foundation strategic worker inspection: canonical v9 WorldState only.
    private VBoxContainer? _strategicWorkerInspector;
    private Label? _strategicWorkerSummary;
    private Label? _strategicWorkerDetails;
    // The game host alone owns the canonical session and movement/turn rules.
    // Main only holds presentation selection and submits a typed order intention.
    private FoundationHumanAiLocalPreview? _localFoundation;
    private OptionButton? _localWorkerSelector;
    private Button? _localMoveButton;
    private Button? _localProduceButton;
    private Button? _localEndTurnButton;
    private Button? _localCancelQueuedButton;
    private Label? _localPendingLabel;
    private Label? _localTurnLabel;
    private Label? _localAiStatus;
    private Label? _localOrderStatus;
    private ulong? _selectedLocalWorkerId;
    // R38: composition owner, not a second simulation state inside the renderer.
    private CanonicalGameSession? _canonicalSession;
    // GA-NEXT TACTICAL-WORKSPACE-01: view-only windows, no worker-location aliasing.
    private Control? _planetWindow;
    private Control? _workspaceWindow;
    private Label? _compactTurnPulse;

    // R35: only an external gameplay owner may supply a real runtime snapshot.
    // Never create a demonstration WorldState inside the UI.
    public void AttachRuntimeState(WorldState? state)
    {
        _attachedRuntimeState = state;
        RefreshStrategicWorkerInspector();
        RefreshLocalWorkerOrderControls();
        if (_tacticalPreview is not null && _tacticalPreview.SelectedCanonicalPhysicalTileId.HasValue)
            OnPhysicalTileInspected(_tacticalPreview.SelectedCanonicalPhysicalTileId,
                _tacticalTileSummary?.Text ?? "", _tacticalTileTerrain?.Text ?? "");
    }
    // R39: ingress for an external authoritative snapshot. Godot never resolves a turn.
    // CanonicalGameSession validates world binding and revision before publication.
    public void ReceiveAuthoritativeSnapshot(WorldState next)
    {
        if (_canonicalSession is null)
            throw new System.InvalidOperationException("Canonical session is not initialized.");
        _canonicalSession.AcceptSnapshot(next);
        // Preserve Main's single presentation ingress even if no frame has
        // elapsed between a valid external commit and the selected tile refresh.
        AttachRuntimeState(_canonicalSession.Snapshot);
    }
    private ulong? _bridgeSelection;

    private void OnCanonicalTerritorySelectionChanged(ulong? selectedId)
    {
        _bridgeSelection = selectedId;
        RefreshStrategicWorkerInspector();
        RefreshLocalWorkerOrderControls();
        if (_openTacticalContext is not null) _openTacticalContext.Disabled = !selectedId.HasValue;
        if (_tacticalBridgeStatus is not null)
            _tacticalBridgeStatus.Text = selectedId.HasValue
                ? $"Territory {selectedId.Value} selected | tactical context available"
                : "Select a canonical territory to enable tactical context";
        // The strategic globe is now a navigation window. A click returns to
        // the tactical neighborhood without issuing a worker movement order.
        if (selectedId.HasValue && _planetWindow?.Visible == true)
        {
            ClosePlanetWindow();
            OpenTacticalContext();
        }
    }

    // Only reads individual workers from the SAME canonical Goldberg identity as
    // the strategic globe. Tactical fine-cell IDs are deliberately excluded.
    // No producer/census is synthesized just to populate the screen.
    private void RefreshStrategicWorkerInspector()
    {
        if (_strategicWorkerSummary is null || _strategicWorkerDetails is null)
            return;
        if (!_bridgeSelection.HasValue || _canonicalSession is null)
        {
            _strategicWorkerSummary.Text =
                "Select a strategic territory on the globe";
            _strategicWorkerDetails.Text =
                "Physical workers will appear when an authoritative census is available";
            return;
        }

        var inspection = StrategicPhysicalWorkerInspector.Read(
            _canonicalSession.GeneratedWorld,
            new StrategicCellId(_bridgeSelection.Value),
            _attachedRuntimeState);
        var availability = inspection.Availability;
        if (availability != StrategicWorkerRuntimeAvailability.Bound)
        {
            _strategicWorkerSummary.Text =
                $"TILE {inspection.CellId.Value} | workers unavailable";
            _strategicWorkerDetails.Text = availability switch
            {
                StrategicWorkerRuntimeAvailability.NoRuntimeState =>
                    "Waiting for an authoritative game snapshot",
                StrategicWorkerRuntimeAvailability.UnboundWorld =>
                    "Runtime snapshot has no world binding",
                StrategicWorkerRuntimeAvailability.WorldMismatch =>
                    "Runtime belongs to a different Goldberg world",
                StrategicWorkerRuntimeAvailability.CensusUninitialized =>
                    "Physical worker census not initialized in this game",
                _ => "Worker census unavailable"
            };
            return;
        }

        _strategicWorkerSummary.Text =
            $"TILE {inspection.CellId.Value} | revision {inspection.Revision} | " +
            $"{inspection.DemandingWorkerCount} workers";
        if (inspection.Workers.Count == 0)
        {
            _strategicWorkerDetails.Text =
                "No physical workers in this territory";
            return;
        }

        const int maximumVisibleWorkers = 8;
        var lines = new System.Collections.Generic.List<string>
        {
            $"Producing {inspection.ProducingWorkerCount} | " +
            $"Traveling {inspection.TravelingWorkerCount} | " +
            $"Military {inspection.MilitaryWorkerCount}",
            "Each worker generates demand at this tile"
        };
        foreach (var worker in inspection.Workers.Take(maximumVisibleWorkers))
        {
            var output = (worker.OutputMicroPerTurn / 1_000_000m)
                .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var multiplier = (worker.DemandMultiplierMicro / 1_000_000m)
                .ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            lines.Add($"#{worker.Id.Value}  Civ {worker.Owner.Value} | " +
                $"{worker.Activity} | Item {worker.Commodity.Value} | " +
                $"Capacity {output}/turn | Demand x{multiplier}");
        }
        if (inspection.Workers.Count > maximumVisibleWorkers)
            lines.Add($"+{inspection.Workers.Count - maximumVisibleWorkers} more workers");
        _strategicWorkerDetails.Text = string.Join("\n", lines);
        GD.Print($"GA_UI_STRATEGIC_WORKERS_R1 tile={inspection.CellId.Value} " +
            $"revision={inspection.Revision} workers={inspection.DemandingWorkerCount} " +
            "source=authoritative_worldstate_v9 read_only=true");
    }

    private void RefreshLocalWorkerOrderControls()
    {
        if (_localFoundation is null || _localWorkerSelector is null ||
            _localMoveButton is null || _localProduceButton is null ||
            _localEndTurnButton is null || _localCancelQueuedButton is null ||
            _localPendingLabel is null || _localTurnLabel is null ||
            _localAiStatus is null)
            return;

        var workers = _localFoundation.ControllableWorkers;
        var rememberedId = _selectedLocalWorkerId;
        _localWorkerSelector.Clear();
        int selectedIndex = -1;
        for (int index = 0; index < workers.Count; index++)
        {
            var worker = workers[index];
            var queued = _localFoundation.PendingOrders.FirstOrDefault(
                order => order.WorkerId == worker.Id);
            _localWorkerSelector.AddItem(
                $"#{worker.Id.Value} | T{worker.CellId.Value} | {worker.Activity}" +
                (queued is null ? "" : " | QUEUED"));
            if (rememberedId == worker.Id.Value)
                selectedIndex = index;
        }
        if (selectedIndex < 0 && workers.Count > 0)
            selectedIndex = 0;
        if (selectedIndex >= 0)
        {
            _localWorkerSelector.Select(selectedIndex);
            _selectedLocalWorkerId = workers[selectedIndex].Id.Value;
        }
        else
        {
            _selectedLocalWorkerId = null;
        }

        _localTurnLabel.Text =
            $"LOCAL HUMAN + AI PREVIEW | turn {_localFoundation.NextTurn.Value} | " +
            $"rev {_localFoundation.Snapshot.Revision} | Human Civ {_localFoundation.PlayerCivilization.Value}";
        if (_compactTurnPulse is not null)
            _compactTurnPulse.Text = $"TURN {_localFoundation.NextTurn.Value} | REV {_localFoundation.Snapshot.Revision} | Human + IA";
        if (_localAiStatus is not null)
        {
            var ready = _localFoundation.ReadyCivilizations;
            var aiCount = _localFoundation.AiCivilizations.Count;
            var readyCount = _localFoundation.AiCivilizations.Count(ready.Contains);
            _localAiStatus.Text = $"AI CIVILIZATIONS | {aiCount} opponent | " +
                $"{readyCount} ready this turn | last AI actions {_localFoundation.LastAiEventCount}";
        }
        var pending = _localFoundation.PendingOrders;
        _localPendingLabel.Text = pending.Count == 0
            ? "No orders queued; end turn to advance the game clock"
            : $"{pending.Count} queued: " + string.Join(", ",
                pending.Select(order => $"#{order.WorkerId.Value} " +
                    (order.IsMovement
                        ? $"T{order.Source.Value}→T{order.Destination.Value}"
                        : $"{order.NextActivity} at T{order.Source.Value}")));
        _localEndTurnButton.Disabled = false; // an empty turn is a valid decision
        _localCancelQueuedButton.Disabled = !_selectedLocalWorkerId.HasValue ||
            !pending.Any(order => order.WorkerId.Value == _selectedLocalWorkerId.Value);
        _localWorkerSelector.Disabled = workers.Count == 0;
        var chosen = workers.FirstOrDefault(
            worker => worker.Id.Value == _selectedLocalWorkerId);
        _localMoveButton.Disabled = chosen is null ||
            !_bridgeSelection.HasValue ||
            !_localFoundation.CanMove(chosen.Id,
                new StrategicCellId(_bridgeSelection.Value));
        _localProduceButton.Disabled = chosen is null ||
            !_localFoundation.CanChangeActivity(
                chosen.Id, PhysicalWorkerActivity.Producing);
    }

    private void OnLocalWorkerSelected(long optionIndex)
    {
        if (_localFoundation is null)
            return;
        var workers = _localFoundation.ControllableWorkers;
        var index = checked((int)optionIndex);
        _selectedLocalWorkerId = index >= 0 && index < workers.Count
            ? workers[index].Id.Value : null;
        RefreshLocalWorkerOrderControls();
    }

    private void OnLocalWorkerMoveRequested()
    {
        if (_localFoundation is null || !_selectedLocalWorkerId.HasValue ||
            !_bridgeSelection.HasValue || _localOrderStatus is null)
            return;
        try
        {
            // Queue an intention; the authoritative snapshot remains untouched.
            var plan = _localFoundation.QueueMove(
                new PhysicalWorkerId(_selectedLocalWorkerId.Value),
                new StrategicCellId(_bridgeSelection.Value));
            _localOrderStatus.Text =
                $"Worker #{plan.WorkerId.Value} move queued: T{plan.Source.Value} " +
                $"to T{plan.Destination.Value}. Press E to close the turn.";
            GD.Print($"GA_UI_FOUNDATION_QUEUED_MOVE_R1 turn={_localFoundation.NextTurn.Value} " +
                $"worker={plan.WorkerId.Value} destination={plan.Destination.Value} " +
                $"queued={_localFoundation.PendingOrderCount} authoritative_mutation=false");
        }
        catch (System.Exception error)
        {
            _localOrderStatus.Text = $"Move rejected: {error.Message}";
            GD.Print($"GA_UI_FOUNDATION_MOVE_REJECTED_R1 {error.Message}");
        }
        RefreshLocalWorkerOrderControls();
    }

    private void OnLocalWorkerProduceRequested()
    {
        if (_localFoundation is null || !_selectedLocalWorkerId.HasValue ||
            _localOrderStatus is null)
            return;
        try
        {
            var plan = _localFoundation.QueueActivity(
                new PhysicalWorkerId(_selectedLocalWorkerId.Value),
                PhysicalWorkerActivity.Producing);
            _localOrderStatus.Text =
                $"Worker #{plan.WorkerId.Value} production queued at " +
                $"T{plan.Source.Value}. Press E to close the turn.";
            GD.Print($"GA_UI_FOUNDATION_QUEUED_PRODUCTION_R1 " +
                $"turn={_localFoundation.NextTurn.Value} worker={plan.WorkerId.Value} " +
                $"queued={_localFoundation.PendingOrderCount} authoritative_mutation=false");
        }
        catch (System.Exception error)
        {
            _localOrderStatus.Text = $"Activity rejected: {error.Message}";
            GD.Print($"GA_UI_FOUNDATION_ACTIVITY_REJECTED_R1 {error.Message}");
        }
        RefreshLocalWorkerOrderControls();
    }

    private void OnLocalCancelQueuedRequested()
    {
        if (_localFoundation is null || !_selectedLocalWorkerId.HasValue ||
            _localOrderStatus is null)
            return;
        try
        {
            var id = new PhysicalWorkerId(_selectedLocalWorkerId.Value);
            if (_localFoundation.CancelQueuedOrder(id))
                _localOrderStatus.Text =
                    $"Queued order for #{id.Value} cancelled; no game state changed";
        }
        catch (System.Exception error)
        {
            _localOrderStatus.Text = $"Cancel rejected: {error.Message}";
        }
        RefreshLocalWorkerOrderControls();
    }

    private void OnLocalTurnEndRequested()
    {
        if (_localFoundation is null || _localOrderStatus is null)
            return;
        try
        {
            var receipt = _localFoundation.EndTurn();
            AttachRuntimeState(_localFoundation.Snapshot);
            var aiEvents = _localFoundation.LastAiEventCount;
            var humanEvents = receipt.ExecutedEvents - aiEvents;
            _localOrderStatus.Text =
                $"Turn {receipt.Turn.Value} closed: Human {humanEvents} + AI {aiEvents} " +
                $"worker actions | journal replay verified | hash v{receipt.Hash.FormatVersion}";
            GD.Print($"GA_UI_FOUNDATION_BATCH_COMMIT_R1 turn={receipt.Turn.Value} " +
                $"events={receipt.ExecutedEvents} revision={receipt.Revision} " +
                $"journal_replayed={receipt.ReplayMatched.ToString().ToLowerInvariant()} " +
                $"authoritative=true");
            GD.Print($"GA_UI_FOUNDATION_HUMAN_AI_COMMIT_R1 turn={receipt.Turn.Value} " +
                $"human_events={humanEvents} ai_events={aiEvents} " +
                $"ready_all=true revision={receipt.Revision} hash_v={receipt.Hash.FormatVersion}");
        }
        catch (System.Exception error)
        {
            _localOrderStatus.Text = $"End turn rejected: {error.Message}";
            GD.Print($"GA_UI_FOUNDATION_BATCH_REJECTED_R1 {error.Message}");
        }
        RefreshLocalWorkerOrderControls();
    }

    // Keyboard-first: G queues a move, P queues work, X cancels a queued
    // order, E resolves the full batch. M remains strategic/tactical mode.
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;
        if (key.Keycode == Key.M && _planetWindow is not null)
        {
            TogglePlanetWindow();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Tab && _workspaceWindow is not null)
        {
            ToggleWorkspaceWindow();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (key.Keycode == Key.Escape && (_planetWindow?.Visible == true ||
            _workspaceWindow?.Visible == true))
        {
            ClosePlanetWindow();
            if (_workspaceWindow is not null) _workspaceWindow.Visible = false;
            GetViewport().SetInputAsHandled();
            return;
        }
        // E remains available on the tactical viewport. Only the domain
        // closes the turn; no strategic destination can be queued here.
        if (key.Keycode == Key.E && _planetWindow?.Visible != true &&
            _localEndTurnButton?.Disabled == false)
        {
            OnLocalTurnEndRequested();
            GetViewport().SetInputAsHandled();
            return;
        }
        // The old G/P/X bridge remains exclusively in the historical
        // Godot smoke until authoritative G(18,0) worker locations ship.
        if (_strategicHudPanel?.Visible != true) return;
        if (key.Keycode == Key.G && _localMoveButton?.Disabled == false)
        {
            OnLocalWorkerMoveRequested();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.P &&
            _localProduceButton?.Disabled == false)
        {
            OnLocalWorkerProduceRequested();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.X &&
            _localCancelQueuedButton?.Disabled == false)
        {
            OnLocalCancelQueuedRequested();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.E &&
            _localEndTurnButton?.Disabled == false)
        {
            OnLocalTurnEndRequested();
            GetViewport().SetInputAsHandled();
        }
    }

    // R30: physical tile facts are read-only display data, never a game order.
    private void OnPhysicalTileInspected(ulong? tileId, string summary, string terrain)
    {
        if (_tacticalTileSummary is null || _tacticalTileTerrain is null) return;
        _tacticalTileSummary.Text = tileId.HasValue ? summary : "Select a physical tile on the tactical map";
        _tacticalTileTerrain.Text = tileId.HasValue ? terrain : "Tile attributes will appear here";
        if (_tacticalTileRuntime is not null)
        {
            _tacticalTileRuntime.Text = "RUNTIME | Select a physical tile";
            if (tileId.HasValue && _tacticalPreview?.CanonicalPhysicalIncidenceMap is { } incidence &&
                _tacticalPreview.PresentedCoarseWorld is { } world)
            {
                var canonicalTile = new PhysicalTacticalTileId(
                    incidence.Refinement.FineParameters, new StrategicCellId(tileId.Value));
                var inspected = TacticalTileRuntimeInspector.Read(incidence, world, canonicalTile, _attachedRuntimeState);
                string availability = inspected.Availability switch
                {
                    TacticalRuntimeAvailability.NoRuntimeState => "Runtime not connected",
                    TacticalRuntimeAvailability.UnboundWorld => "Runtime world not bound",
                    TacticalRuntimeAvailability.WorldMismatch => "Runtime/world signature mismatch",
                    TacticalRuntimeAvailability.Bound => "Runtime connected",
                    _ => "Runtime unavailable"
                };
                string incidentFacts = string.Join(" | ", inspected.IncidentControls.Select(fact =>
                    inspected.Availability != TacticalRuntimeAvailability.Bound
                        ? $"T{fact.StrategicCellId.Value}: unavailable"
                        : fact.Controller.HasValue
                            ? $"T{fact.StrategicCellId.Value}: civilization {fact.Controller.Value.Value}"
                            : $"T{fact.StrategicCellId.Value}: no control"));
                _tacticalTileRuntime.Text = $"RUNTIME | {availability}\n{incidentFacts}";
                GD.Print($"GA_UI_TACTICAL_RUNTIME_R35 selected={tileId.Value} status={inspected.Availability} " +
                    $"incidence_count={inspected.IncidentControls.Count} revision={(inspected.Revision.HasValue ? inspected.Revision.Value.ToString() : "none")} mutation=false");
            }
        }
        GD.Print($"GA_UI_TACTICAL_INSPECTOR_R30 selected={(tileId.HasValue ? tileId.Value.ToString() : "none")} source=canonical_read_only");
    }

    private void SetStrategicCalibrationControlsVisible(bool visible)
    {
        if (_strategicCalibrationGroups.Count != 2)
            throw new System.InvalidOperationException("Strategic calibration control ownership is incomplete.");
        foreach (var control in _strategicCalibrationGroups)
            control.Visible = visible;
        GD.Print($"GA_UI_STRATEGIC_CONTROL_OWNERSHIP_R1 context={(visible ? "STRATEGIC" : "TACTICAL")} groups=2 controls=3 visible={visible.ToString().ToLowerInvariant()}");
    }

    private void OpenTacticalContext()
    {
        if (!_bridgeSelection.HasValue || _tacticalBridgeStatus is null ||
            _strategicGlobe is null || _tacticalPreview is null || _returnStrategicContext is null ||
            _openTacticalContext is null) return;
        try
        {
            _tacticalPreview.Open(_bridgeSelection.Value);
            _strategicGlobe.Visible = false;
            _tacticalPreview.Visible = true;
            if (_strategicHudPanel is not null) _strategicHudPanel.Visible = false;
            if (_tacticalHudPanel is not null) _tacticalHudPanel.Visible = true;
            SetStrategicCalibrationControlsVisible(false);
            if (_tacticalInspector is not null) _tacticalInspector.Visible = true;
            _openTacticalContext.Visible = false;
            _returnStrategicContext.Visible = true;
            if (_tacticalTileEdges is not null)
            {
                _tacticalTileEdges.ButtonPressed = false;
                _tacticalTileEdges.Visible = true;
            }
            _tacticalBridgeStatus.Text = $"LOCAL TACTICAL | strategic {_bridgeSelection.Value} and adjacent territories";
            GD.Print($"GA_UI_TACTICAL_TRANSITION_OPEN focus={_bridgeSelection.Value}");
        }
        catch (System.Exception exception)
        {
            _tacticalBridgeStatus.Text = "Tactical view failed to load; see Godot log";
            GD.PushError($"GA_UI_TACTICAL_TRANSITION_FAIL: {exception}");
        }
    }

    private void ReturnStrategicContext()
    {
        if (_strategicGlobe is null || _tacticalPreview is null ||
            _openTacticalContext is null || _returnStrategicContext is null ||
            _tacticalBridgeStatus is null) return;
        _tacticalPreview.Visible = false;
        if (_tacticalHudPanel is not null) _tacticalHudPanel.Visible = false;
        if (_strategicHudPanel is not null) _strategicHudPanel.Visible = true;
        if (_tacticalInspector is not null) _tacticalInspector.Visible = false;
        _strategicGlobe.Visible = true;
        SetStrategicCalibrationControlsVisible(true);
        _openTacticalContext.Visible = true;
        _returnStrategicContext.Visible = false;
        if (_tacticalTileEdges is not null) _tacticalTileEdges.Visible = false;
        _tacticalBridgeStatus.Text = _bridgeSelection.HasValue
            ? $"Territory {_bridgeSelection.Value} selected | tactical context available"
            : "Select a canonical territory to enable tactical context";
        GD.Print("GA_UI_TACTICAL_TRANSITION_RETURN globe_state_preserved=true");
    }

    // A local Control-based window, not a second OS window or a game-state owner.
    // The same GlobePreview instance is reparented; it never regenerates a world.
    private void CreateTacticalWorkspaceWindows(GlobePreview globe,
        PanelContainer rightPanel, HBoxContainer header)
    {
        var openPlanet = new Button { Text = "M | PLANET" };
        openPlanet.Pressed += TogglePlanetWindow;
        header.AddChild(openPlanet);
        var openWorkspace = new Button { Text = "TAB | PANELS" };
        openWorkspace.Pressed += ToggleWorkspaceWindow;
        header.AddChild(openWorkspace);
        _compactTurnPulse = new Label
        {
            Text = "TURN 1 | REV 0 | Human + IA",
            VerticalAlignment = VerticalAlignment.Center
        };
        header.AddChild(_compactTurnPulse);
        var quickEnd = new Button { Text = "E | END TURN" };
        quickEnd.Pressed += OnLocalTurnEndRequested;
        header.AddChild(quickEnd);

        var operations = new Control
        {
            Name = "GANextOperationsWindow",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(operations);
        operations.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var operationsPanel = new PanelContainer
        {
            Name = "GANextOperationsPanel", MouseFilter = MouseFilterEnum.Stop,
            AnchorLeft = 1f, AnchorRight = 1f,
            AnchorTop = 0.08f, AnchorBottom = 0.93f,
            OffsetLeft = -460f, OffsetRight = -16f
        };
        operations.AddChild(operationsPanel);
        var operationsColumn = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        operationsPanel.AddChild(operationsColumn);
        var operationsTitle = new HBoxContainer();
        operationsColumn.AddChild(operationsTitle);
        operationsTitle.AddChild(new Label
        {
            Text = "OPERATIONS | TACTICAL WORKSPACE",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        var closeOperations = new Button { Text = "X" };
        closeOperations.Pressed += () => operations.Visible = false;
        operationsTitle.AddChild(closeOperations);
        rightPanel.Reparent(operationsColumn);
        rightPanel.CustomMinimumSize = Vector2.Zero;
        rightPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        _workspaceWindow = operations;

        var planet = new Control
        {
            Name = "GANextPlanetWindow",
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(planet);
        planet.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var veil = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.63f),
            MouseFilter = MouseFilterEnum.Stop
        };
        planet.AddChild(veil);
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var planetPanel = new PanelContainer
        {
            Name = "GANextPlanetPanel", MouseFilter = MouseFilterEnum.Stop,
            AnchorLeft = 0.14f, AnchorRight = 0.86f,
            AnchorTop = 0.07f, AnchorBottom = 0.92f
        };
        planet.AddChild(planetPanel);
        var planetColumn = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        planetPanel.AddChild(planetColumn);
        var planetTitle = new HBoxContainer();
        planetColumn.AddChild(planetTitle);
        planetTitle.AddChild(new Label
        {
            Text = "PLANETARY MAP | select a region to navigate",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        var closePlanet = new Button { Text = "X / ESC" };
        closePlanet.Pressed += ClosePlanetWindow;
        planetTitle.AddChild(closePlanet);
        var planetSlot = new AspectRatioContainer
        {
            Ratio = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        planetColumn.AddChild(planetSlot);
        globe.Reparent(planetSlot);
        globe.Visible = false;
        _planetWindow = planet;
        GD.Print("GA_NEXT_TACTICAL_WORKSPACE_WINDOWS_READY " +
            "planet=M panels=TAB close=ESC native_windows=false simulation_mutation=false");
    }

    private void ClosePlanetWindow()
    {
        if (_planetWindow is null) return;
        _planetWindow.Visible = false;
        if (_strategicGlobe is not null) _strategicGlobe.Visible = false;
    }

    private void TogglePlanetWindow()
    {
        if (_planetWindow is null || _strategicGlobe is null) return;
        var open = !_planetWindow.Visible;
        if (_workspaceWindow is not null) _workspaceWindow.Visible = false;
        _planetWindow.Visible = open;
        _strategicGlobe.Visible = open;
        GD.Print($"GA_NEXT_PLANET_WINDOW visible={open.ToString().ToLowerInvariant()} " +
            "navigation_only=true worker_move=false");
    }

    private void ToggleWorkspaceWindow()
    {
        if (_workspaceWindow is null) return;
        ClosePlanetWindow();
        _workspaceWindow.Visible = !_workspaceWindow.Visible;
        GD.Print($"GA_NEXT_OPERATIONS_WINDOW visible={_workspaceWindow.Visible.ToString().ToLowerInvariant()}");
    }

    public override void _Ready()
    {
        GD.Print("GA_PROD_MAIN_READY_R6");
        // Prioritize a maximized production window; preserve windowed resizing and avoid headless changes.
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);
        var backdrop = new ColorRect { Color = new Color("#0a1725"), MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeConstantOverride("margin_left", 20);
        frame.AddThemeConstantOverride("margin_right", 20);
        frame.AddThemeConstantOverride("margin_top", 16);
        frame.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(frame);

        _layoutDiagnosticFrame = frame;
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _layoutDiagnosticRoot = root;
        root.AddThemeConstantOverride("separation", 12);
        frame.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 20);
        root.AddChild(header);
        var title = new Label { Text = "GLOBAL ARENA" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.AddThemeColorOverride("font_color", new Color("#d9e9f4"));
        header.AddChild(title);
        var subtitle = new Label { Text = "Planetary strategy  |  Production foundation", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        subtitle.AddThemeColorOverride("font_color", new Color("#7eabc4"));
        header.AddChild(subtitle);
        var button = new Button { Text = "SYSTEM STATUS" };
        button.Pressed += ShowStatus;
        header.AddChild(button);

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 12);
        root.AddChild(content);

        // GA-UI-MVP-SQUARE: the layout column stretches; its map remains 1:1.
        // AspectRatioContainer defaults to centered FIT, without clipping either viewport.
        var squareMapSlot = new AspectRatioContainer
        {
            Ratio = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.AddChild(squareMapSlot);
        var mapRegion = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        squareMapSlot.AddChild(mapRegion);
        var globe = new GlobePreview
        {
            Name = "GANextPlanetGlobe",
            CustomMinimumSize = new Vector2(360f, 300f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mapRegion.AddChild(globe);
        _strategicGlobe = globe;
        globe.CanonicalTerritorySelectionChanged += OnCanonicalTerritorySelectionChanged;
        _tacticalPreview = new TacticalContextPreview
        {
            Name = "GANextTacticalViewport",
            Visible = false,
            CustomMinimumSize = new Vector2(360f, 300f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mapRegion.AddChild(_tacticalPreview);
        _tacticalPreview.PhysicalTileInspected += OnPhysicalTileInspected;
        // The Foundation gameplay host materializes a *labeled local preview*
        // scenario. Main/Godot never creates, moves or settles workers directly.
        // This is NOT a claim of final world-population generation.
        _localFoundation = FoundationHumanAiLocalPreview.CreateLocalPreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        _canonicalSession = _localFoundation.Session;
        if (globe.PresentedCanonicalWorld is null ||
            RuntimeWorldBinding.FromGeneratedWorld(globe.PresentedCanonicalWorld) !=
            RuntimeWorldBinding.FromGeneratedWorld(_canonicalSession.GeneratedWorld))
            throw new System.InvalidOperationException(
                "Displayed Goldberg globe and local gameplay world do not match.");
        AttachRuntimeState(_canonicalSession.Snapshot);
        if (_canonicalSession.Snapshot.WorldBinding !=
            RuntimeWorldBinding.FromGeneratedWorld(_canonicalSession.GeneratedWorld))
            throw new System.InvalidOperationException("Canonical session signature mismatch.");
        GD.Print($"GA_UI_TACTICAL_SESSION_READY_R38 bound=true revision={_canonicalSession.Snapshot.Revision} " +
            $"civilizations={_canonicalSession.Snapshot.Civilizations.Civilizations.Count} " +
            $"controls={_canonicalSession.Snapshot.Territory.Controls.Count} " +
            "read_only_ui=true source=canonical_generated_world");
        GD.Print("GA_UI_VIEWPORT_READY_02 parent=map-region sizing=expand-fill");

        var rightPanel = new PanelContainer { CustomMinimumSize = new Vector2(390f, 0f), SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddChild(rightPanel);
        var rightMargin = new MarginContainer();
        rightMargin.AddThemeConstantOverride("margin_left", 12);
        rightMargin.AddThemeConstantOverride("margin_right", 12);
        rightMargin.AddThemeConstantOverride("margin_top", 12);
        rightMargin.AddThemeConstantOverride("margin_bottom", 12);
        rightPanel.AddChild(rightMargin);
        var rightLayout = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        rightLayout.AddThemeConstantOverride("separation", 8);
        rightMargin.AddChild(rightLayout);
        var panelTitle = new Label { Text = "TERRITORY & WORLD" };
        panelTitle.AddThemeFontSizeOverride("font_size", 17);
        rightLayout.AddChild(panelTitle);
        var tacticalBridgeTitle = new Label { Text = "TACTICAL CONTEXT" };
        tacticalBridgeTitle.AddThemeFontSizeOverride("font_size", 15);
        rightLayout.AddChild(tacticalBridgeTitle);
        _tacticalBridgeStatus = new Label { Text = "Select a canonical territory to enable tactical context" };
        _tacticalBridgeStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rightLayout.AddChild(_tacticalBridgeStatus);
        // MVP inspection in the existing right column; the tactical viewport stays unchanged.
        var inspector = new VBoxContainer { Visible = false };
        inspector.AddThemeConstantOverride("separation", 5);
        var inspectorTitle = new Label { Text = "SELECTED PHYSICAL TILE" };
        inspectorTitle.AddThemeFontSizeOverride("font_size", 15);
        inspector.AddChild(inspectorTitle);
        var inspectorSummary = new Label { Text = "Select a physical tile on the tactical map", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        inspector.AddChild(inspectorSummary);
        var inspectorTerrain = new Label { Text = "Tile attributes will appear here", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        inspector.AddChild(inspectorTerrain);
        var inspectorRuntime = new Label { Text = "RUNTIME | Select a physical tile", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        inspector.AddChild(inspectorRuntime);
        rightLayout.AddChild(inspector);
        _tacticalInspector = inspector;
        _tacticalTileSummary = inspectorSummary;
        _tacticalTileTerrain = inspectorTerrain;
        _tacticalTileRuntime = inspectorRuntime;
        GD.Print("GA_UI_TACTICAL_INSPECTOR_READY_R30 sidebar=true simulation_mutation=false");
        GD.Print("GA_UI_TACTICAL_RUNTIME_BRIDGE_READY_R35 state=unattached read_only=true");
        _openTacticalContext = new Button { Text = "OPEN TACTICAL CONTEXT", Disabled = true };
        _openTacticalContext.Pressed += OpenTacticalContext;
        rightLayout.AddChild(_openTacticalContext);
        _returnStrategicContext = new Button { Text = "RETURN TO STRATEGIC GLOBE", Visible = false };
        _returnStrategicContext.Pressed += () =>
        {
            if (_planetWindow is null) ReturnStrategicContext();
            else TogglePlanetWindow();
        };
        rightLayout.AddChild(_returnStrategicContext);
        _tacticalTileEdges = new CheckBox { Text = "TACTICAL TILE EDGES", ButtonPressed = false, Visible = false };
        _tacticalTileEdges.Toggled += enabled => _tacticalPreview?.SetEdgesVisible(enabled);
        rightLayout.AddChild(_tacticalTileEdges);
        var panelScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        rightLayout.AddChild(panelScroll);
        // GA_UI_TACTICAL_HUD_SIDEBAR_R1: one scroll region, distinct context-only panels.
        var hud = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hud.AddThemeConstantOverride("separation", 8);
        panelScroll.AddChild(hud);
        var tacticalHud = new VBoxContainer { Visible = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tacticalHud.AddThemeConstantOverride("separation", 5);
        hud.AddChild(tacticalHud);
        _tacticalPreview.TransferHud(tacticalHud);
        _tacticalHudPanel = tacticalHud;
        var strategicHud = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hud.AddChild(strategicHud);

        // Worker facts live in the existing right sidebar, never as labels
        // floating over the globe. The sole input is Main's accepted snapshot.
        var workerInspector = new VBoxContainer();
        workerInspector.AddThemeConstantOverride("separation", 5);
        strategicHud.AddChild(workerInspector);
        var workerTitle = new Label { Text = "PHYSICAL WORKERS | STRATEGIC TILE" };
        workerTitle.AddThemeFontSizeOverride("font_size", 15);
        workerInspector.AddChild(workerTitle);
        var workerSummary = new Label
        {
            Text = "Select a strategic territory on the globe",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        workerInspector.AddChild(workerSummary);
        var workerDetails = new Label
        {
            Text = "Physical workers will appear when an authoritative census is available",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        workerInspector.AddChild(workerDetails);
        _strategicWorkerInspector = workerInspector;
        _strategicWorkerSummary = workerSummary;
        _strategicWorkerDetails = workerDetails;
        RefreshStrategicWorkerInspector();

        // Compact strategic controls: player selects a physical worker here,
        // selects a destination directly on the map, then submits one intent.
        var orderSection = new VBoxContainer();
        orderSection.AddThemeConstantOverride("separation", 4);
        // Turn status is shared across both views. Strategic G/P/X orders are
        // retained as a historical fixture, not advertised as tactical moves.
        hud.AddChild(orderSection);
        var orderTitle = new Label { Text = "LOCAL FOUNDATION | WORKER ORDERS" };
        orderTitle.AddThemeFontSizeOverride("font_size", 15);
        orderSection.AddChild(orderTitle);
        _localTurnLabel = new Label
        {
            Text = "Local foundation scenario",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_localTurnLabel);
        _localAiStatus = new Label
        {
            Text = "AI CIVILIZATIONS | waiting for the next collective turn",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_localAiStatus);
        _localWorkerSelector = new OptionButton
        {
            CustomMinimumSize = new Vector2(0f, 32f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _localWorkerSelector.ItemSelected += OnLocalWorkerSelected;
        orderSection.AddChild(_localWorkerSelector);
        _localMoveButton = new Button
        {
            Text = "G | QUEUE MOVE TO SELECTED ADJACENT TILE",
            Disabled = true
        };
        _localMoveButton.Pressed += OnLocalWorkerMoveRequested;
        orderSection.AddChild(_localMoveButton);
        _localProduceButton = new Button
        {
            Text = "P | QUEUE START PRODUCTION",
            Disabled = true
        };
        _localProduceButton.Pressed += OnLocalWorkerProduceRequested;
        orderSection.AddChild(_localProduceButton);
        _localPendingLabel = new Label
        {
            Text = "No orders queued",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_localPendingLabel);
        _localCancelQueuedButton = new Button
        {
            Text = "X | CANCEL SELECTED WORKER ORDER",
            Disabled = true
        };
        _localCancelQueuedButton.Pressed += OnLocalCancelQueuedRequested;
        orderSection.AddChild(_localCancelQueuedButton);
        _localEndTurnButton = new Button
        {
            Text = "E | END TURN — HUMAN + AI TOGETHER"
        };
        _localEndTurnButton.Pressed += OnLocalTurnEndRequested;
        orderSection.AddChild(_localEndTurnButton);
        _localOrderStatus = new Label
        {
            Text = "Select a worker and tile, queue orders, then press E to end turn",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_localOrderStatus);
        RefreshLocalWorkerOrderControls();
        GD.Print("GA_UI_FOUNDATION_LOCAL_GAME_R1 workers_initial=" +
            _localFoundation!.Snapshot.Workers.Workers.Count +
            " authority=domain_host orders=journaled preview_scenario=true");
        GD.Print("GA_UI_FOUNDATION_HUMAN_AI_READY_R1 " +
            "human_civ=1 ai_civ=2 ai_policy=resume_existing_production " +
            "all_ready_gate=true preview_scenario=true");

        globe.TransferHud(strategicHud);
        _strategicHudPanel = strategicHud;
        // Keep the calibration buttons visible even when the context scrolls.
        foreach (var child in strategicHud.GetChildren())
        {
            if (child is HBoxContainer row)
            {
                bool calibrationRow = false;
                foreach (var entry in row.GetChildren())
                    if (entry is Button control && control.Text.StartsWith("LAND ")) calibrationRow = true;
                if (calibrationRow)
                {
                    row.Reparent(rightLayout);
                    _strategicCalibrationGroups.Add(row);
                }
            }
            else if (child is Button next && next.Text == "NEXT SEED")
            {
                next.Reparent(rightLayout);
                _strategicCalibrationGroups.Add(next);
            }
        }
        if (_strategicCalibrationGroups.Count != 2)
            throw new System.InvalidOperationException("Expected LAND +/- row and NEXT SEED control.");
        GD.Print("GA_UI_STRATEGIC_CONTROL_OWNERSHIP_READY_R1 groups=2 controls=3 context=STRATEGIC");
        GD.Print("GA_UI_CALIBRATION_ACCESS_READY_01 fixed-controls=true");

        _status = new Label { Text = "GA-UI-FOUNDATION-01  |  Map and context areas separated" };
        _status.AddThemeColorOverride("font_color", new Color("#90a9bc"));
        root.AddChild(_status);
        GD.Print("GA_UI_FOUNDATION_READY_01 layout=map,sidebar,header,footer");
        GD.Print("GA_UI_FOUNDATION_RESIZE_READY_02 responsive=true");

        // Preserve PR #361 historical strategic-order E2E as an independent
        // regression fixture. Ordinary play opens the tactical viewport first.
        bool legacySmoke = System.Environment.GetEnvironmentVariable(
            "GA_FOUNDATION_GODOT_SMOKE") == "1";
        if (!legacySmoke)
        {
            // The old v9 record locates workers on G(3,0). This is a
            // PRESENTATION REGION ANCHOR, not a fabricated G(18,0) worker tile.
            // Tactical movement remains locked until the typed-v10 migration.
            var first = _localFoundation!.ControllableWorkers.FirstOrDefault();
            if (first is null)
                throw new System.InvalidOperationException(
                    "No local physical worker available for tactical entry.");
            _bridgeSelection = first.CellId.Value;
            _openTacticalContext!.Disabled = false;
            OpenTacticalContext();
            CreateTacticalWorkspaceWindows(globe, rightPanel, header);
            // Keep the 1280x720 header usable: the long subtitle belongs in
            // secondary information, not beside all keyboard-first controls.
            subtitle.Visible = false;
            _returnStrategicContext!.Text = "M | OPEN PLANETARY MAP";
            _localMoveButton!.Visible = false;
            _localProduceButton!.Visible = false;
            _localCancelQueuedButton!.Visible = false;
            _localWorkerSelector!.Visible = false;
            orderSection.AddChild(new Label
            {
                Text = "Tactical worker orders: pending authoritative physical-tile migration. " +
                       "E still closes the human + AI turn; G/P/X moves are not offered here.",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });
            _status!.Text = "GA-NEXT TACTICAL WORKSPACE | M planet | TAB panels | E turn";
            RefreshLocalWorkerOrderControls();
            GD.Print($"GA_NEXT_TACTICAL_ENTRY_PASS local_civ={_localFoundation.PlayerCivilization.Value} " +
                $"worker={first.Id.Value} coarse_preview_focus={first.CellId.Value} " +
                "physical_worker_location_authoritative=false tactical_visible=true");
            if (System.Environment.GetEnvironmentVariable("GA_NEXT_WORKSPACE_SMOKE") == "1")
                _ = new FoundationGodotWorkspaceSmoke(this, _localFoundation).RunAsync();
        }

        // Only the explicit CI environment starts the historical E2E.
        if (legacySmoke)
        {
            _ = new FoundationGodotRuntimeSmoke(
                this, globe, _localFoundation!,
                _localWorkerSelector!, _localMoveButton!, _localProduceButton!,
                _localEndTurnButton!, _localCancelQueuedButton!, _localPendingLabel!,
                _strategicWorkerSummary!, _localTurnLabel!, _localAiStatus!, _localOrderStatus!)
                .RunAsync();
        }
    }

    // GA_LAYOUT_DIAGNOSTIC_02: capture geometry once on launch and after resize.
    public override void _Process(double delta)
    {
        // Pull accepted snapshots on the Godot thread; no polling of simulation rules.
        // Reference comparison also detects a new snapshot with an unchanged revision.
        var latest = _canonicalSession?.Snapshot;
        if (latest is not null && !object.ReferenceEquals(_attachedRuntimeState, latest))
        {
            AttachRuntimeState(latest);
            GD.Print($"GA_UI_RUNTIME_SNAPSHOT_REFRESH_R39 revision={latest.Revision} presentation_only=true");
        }
        if (_layoutDiagnosticFrame is null || _layoutDiagnosticRoot is null) return;
        var view = GetViewport();
        if (view is null) return;
        var current = view.GetVisibleRect().Size;
        var signature = $"{current}/{Size}/{_layoutDiagnosticFrame.Size}/{_layoutDiagnosticRoot.Size}/{DisplayServer.WindowGetSize()}";
        if (signature == _lastLoggedLayoutSignature) return;
        _lastLoggedLayoutSignature = signature;
        GD.Print($"GA_LAYOUT_DIAGNOSTIC_02 viewport={current} main={Size} frame={_layoutDiagnosticFrame.Size} root={_layoutDiagnosticRoot.Size} window={DisplayServer.WindowGetSize()} screen={DisplayServer.ScreenGetSize()}");
    }
    private void ShowStatus()
    {
        if (_status is null) return;
        _status.Text = $"Godot {Engine.GetVersionInfo()["string"]} | .NET production shell | World assembly referenced";
    }
}