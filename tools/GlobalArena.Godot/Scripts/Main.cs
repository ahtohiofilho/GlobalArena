using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

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
    private Label? _territorialPresenceSummary;
    private WorldState? _attachedRuntimeState;
    // R38: composition owner, not a second simulation state inside the renderer.
    private CanonicalGameSession? _canonicalSession;

    // R35: only an external gameplay owner may supply a real runtime snapshot.
    // Never create a demonstration WorldState inside the UI.
    public void AttachRuntimeState(WorldState? state)
    {
        _attachedRuntimeState = state;
        UpdateTerritorialPresenceSummary();
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
    }
    private ulong? _bridgeSelection;

    private void OnCanonicalTerritorySelectionChanged(ulong? selectedId)
    {
        _bridgeSelection = selectedId;
        if (_openTacticalContext is not null) _openTacticalContext.Disabled = !selectedId.HasValue;
        if (_tacticalBridgeStatus is not null)
            _tacticalBridgeStatus.Text = selectedId.HasValue
                ? $"Territory {selectedId.Value} selected | tactical context available"
                : "Select a canonical territory to enable tactical context";
        UpdateTerritorialPresenceSummary();
    }

    /// <summary>
    /// Presence is demographic and weighted at 1/6 per worker-equivalent.
    /// It never changes sovereignty. Physical V13 world remains authoritative.
    /// </summary>
    private void UpdateTerritorialPresenceSummary()
    {
        if (_territorialPresenceSummary is null) return;
        if (!_bridgeSelection.HasValue)
        {
            _territorialPresenceSummary.Text =
                "PRESENÇA TERRITORIAL | Selecione um território estratégico.";
            return;
        }
        if (_attachedRuntimeState?.PhysicalEconomy is not { } physical ||
            _tacticalPreview?.CanonicalPhysicalIncidenceMap is not { } incidence)
        {
            _territorialPresenceSummary.Text =
                $"T{_bridgeSelection.Value} | Sem população física inicializada.";
            return;
        }
        var presence = StrategicTerritorialPresenceProjection.FromPhysicalState(
            physical, incidence);
        var territory = new StrategicCellId(_bridgeSelection.Value);
        var row = presence.At(territory);
        var sovereignty = _attachedRuntimeState.Territory.TryGetController(
            territory, out var controller)
            ? $"civilização {controller.Value}" : "sem controle político definido";
        _territorialPresenceSummary.Text =
            $"T{territory.Value} | presença ponderada {row.TotalEquivalent:F2} trabalhadores " +
            $"(ativos {row.WorkingEquivalent:F2}, em trânsito {row.TravellingEquivalent:F2}) | " +
            $"tiles físicos {row.OccupiedPhysicalTiles}\n" +
            $"SOBERANIA: {sovereignty}. Presença populacional não é conquista.";
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
                var populationFacts = "";
                if (inspected.Availability == TacticalRuntimeAvailability.Bound &&
                    _attachedRuntimeState?.PhysicalEconomy is { } physical)
                {
                    var presence = StrategicTerritorialPresenceProjection.FromPhysicalState(
                        physical, incidence);
                    populationFacts = "\nPRESENÇA DEMOGRÁFICA PONDERADA: " +
                        string.Join(" | ", inspected.IncidentControls.Select(fact =>
                        {
                            var row = presence.At(fact.StrategicCellId);
                            return $"T{fact.StrategicCellId.Value}: " +
                                $"{row.TotalEquivalent:F2} (viajando {row.TravellingEquivalent:F2})";
                        }));
                }
                _tacticalTileRuntime.Text =
                    $"RUNTIME | {availability}\n{incidentFacts}{populationFacts}";
                GD.Print($"GA_UI_TACTICAL_RUNTIME_R35 selected={tileId.Value} status={inspected.Availability} " +
                    $"incidence_count={inspected.IncidentControls.Count} revision={(inspected.Revision.HasValue ? inspected.Revision.Value.ToString() : "none")} mutation=false");
            }
        }
        GD.Print($"GA_UI_TACTICAL_INSPECTOR_R30 selected={(tileId.HasValue ? tileId.Value.ToString() : "none")} source=canonical_read_only");
        HandleWorkerLabSelection(tileId);
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
            if (_canonicalPhysicalLab is not null) UpdateWorkerLabHud();
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
            UpdateTerritorialPresenceSummary();
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
        UpdateTerritorialPresenceSummary();
        _tacticalBridgeStatus.Text = _bridgeSelection.HasValue
            ? $"Territory {_bridgeSelection.Value} selected | tactical context available"
            : "Select a canonical territory to enable tactical context";
        GD.Print("GA_UI_TACTICAL_TRANSITION_RETURN globe_state_preserved=true");
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
            CustomMinimumSize = new Vector2(360f, 300f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mapRegion.AddChild(globe);
        _strategicGlobe = globe;
        globe.CanonicalTerritorySelectionChanged += OnCanonicalTerritorySelectionChanged;
        _tacticalPreview = new TacticalContextPreview
        {
            Visible = false,
            CustomMinimumSize = new Vector2(360f, 300f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mapRegion.AddChild(_tacticalPreview);
        _tacticalPreview.PhysicalTileInspected += OnPhysicalTileInspected;
        // R38: the session owns a real canonical bound WorldState. Zero controllers
        // means "no control", not invented gameplay. Worldgen 01C stays experimental.
        _canonicalSession = CanonicalGameSession.Start(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        // A civilization and its community exist as soon as the world exists;
        // the UI never requires a declaration, city foundation or a click to
        // bring the initial population, production and cash into being.
        _canonicalPhysicalLab = new CanonicalPhysicalTacticalSession(
            _canonicalSession, new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);
        _workerOrigin = _canonicalPhysicalLab.InitialTile;
        _tacticalPreview.BindEconomicTacticalGeography(
            _canonicalPhysicalLab.FineWorld, _canonicalPhysicalLab.CanonicalTerrain);
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
        var territorialPresence = new Label
        {
            Text = "PRESENÇA TERRITORIAL | Selecione um território estratégico.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        rightLayout.AddChild(territorialPresence);
        _territorialPresenceSummary = territorialPresence;
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
        _returnStrategicContext.Pressed += ReturnStrategicContext;
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
        tacticalHud.AddThemeConstantOverride("separation", 3);
        hud.AddChild(tacticalHud);
        _tacticalPreview.TransferHud(tacticalHud);
        InitializeWorkerLabControls(tacticalHud);
        // QA density only: typography belongs to the right sidebar, not the map.
        ApplyCompactTacticalTypography(tacticalHud);
        ApplyCompactTacticalTypography(inspector);
        _tacticalBridgeStatus.AddThemeFontSizeOverride("font_size", 13);
        _territorialPresenceSummary.AddThemeFontSizeOverride("font_size", 13);
        _returnStrategicContext.AddThemeFontSizeOverride("font_size", 13);
        _tacticalTileEdges.AddThemeFontSizeOverride("font_size", 13);
        _tacticalHudPanel = tacticalHud;
        var strategicHud = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hud.AddChild(strategicHud);
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
    }

    /// <summary>
    /// Compact the tactical/inspector sidebar for repeatable QA screenshots.
    /// Presentation-only: does not touch the map viewport, selectors, events,
    /// worker commands, game state, save format or production UI contract.
    /// A separate future UI milestone may replace this temporary layout.
    /// </summary>
    private static void ApplyCompactTacticalTypography(Node root)
    {
        if (root is Label label)
        {
            // Preserve a small distinction between existing headings and data.
            var heading = label.HasThemeFontSizeOverride("font_size");
            var size = heading
                ? System.Math.Min(14, label.GetThemeFontSize("font_size"))
                : 13;
            label.AddThemeFontSizeOverride("font_size", size);
        }
        else if (root is Button button)
        {
            button.AddThemeFontSizeOverride("font_size", 13);
        }
        else if (root is SpinBox numeric)
        {
            // SpinBox renders its digits through an internal LineEdit.
            numeric.GetLineEdit().AddThemeFontSizeOverride("font_size", 13);
        }

        foreach (Node child in root.GetChildren())
            ApplyCompactTacticalTypography(child);
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