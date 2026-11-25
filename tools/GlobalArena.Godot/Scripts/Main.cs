using Godot;
using BigInteger = System.Numerics.BigInteger;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
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
    private CheckBox? _tacticalSettledTradeRoutes;
    private GlobePreview? _strategicGlobe;
    private GaGoldbergStartupConfiguration? _goldbergStartup;
    private bool _resolutionBootstrapSmoke;
    internal GoldbergParameters StartupCoarseParameters =>
        _goldbergStartup?.Coarse ??
        throw new InvalidOperationException("Goldberg startup not initialized.");
    internal GoldbergParameters? ActiveFineParameters =>
        _tacticalHost?.Incidence.Refinement.FineParameters;
    internal GoldbergParameters? GlobeCoarseParameters =>
        _strategicGlobe?.PresentedCanonicalWorld?.Request.StrategicParameters;
    // Read-only QA evidence; private participant routes never leave the host.
    internal int VisibleSphericalTradeCrossings =>
        _strategicGlobe?.VisibleSphericalSettledTradeCrossings ?? 0;
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
    // The ordinary visual checkpoint now uses the fully journaled v10 fine
    // collective host. Legacy v9 Godot smokes stay on their old host.
    private FoundationTacticalHumanAiTurnHost? _tacticalHost;
    private FoundationGameplayEconomicConfiguration? _configuredEconomicGameplay;
    private readonly CivilizationId _humanCivilization = new(1UL);

    /// <summary>
    /// TRUSTED pre-scene bootstrap: the session/world owner may supply a
    /// complete explicitly approved policy pack before Godot _Ready.
    /// No policy pack -> the historical v11 demonstration remains unchanged.
    /// No ordinary client ever gets unapproved laboratory tax coefficients.
    /// </summary>
    public void ConfigureAuthoritativeEconomicGameplay(
        FoundationGameplayEconomicConfiguration profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (IsNodeReady() || _tacticalHost is not null ||
            _configuredEconomicGameplay is not null)
            throw new InvalidOperationException(
                "Economic profile must be supplied once by trusted bootstrap, " +
                "BEFORE scene startup, never in an active collective turn.");
        _configuredEconomicGameplay = profile;
    }
    public FoundationTacticalHumanAiTurnHost? ActiveTacticalHost => _tacticalHost;
    public ulong? SelectedTacticalWorkerId => _tacticalHost is null ? null : _selectedLocalWorkerId;
    private OptionButton? _localWorkerSelector;
    private Button? _localMoveButton;
    private Button? _localProduceButton;
    private Button? _localAgricultureButton;
    private Button? _localForestryButton;
    private Button? _localMiningButton;
    private Button? _r149LandWorkButton;
    private Button? _r149PortWorkButton;
    private Button? _localEndTurnButton;
    private Button? _localCancelQueuedButton;
    private Label? _localPendingLabel;
    private Label? _localTurnLabel;
    private Label? _localAiStatus;
    private Label? _localOrderStatus;
    // R178: presentation-only authoritative temporal close for the human
    // civilization. Hidden before a committed temporal sale; never a preview.
    private Label? _lastTemporalEconomyLabel;
    internal string LastTemporalEconomyText =>
        _lastTemporalEconomyLabel?.Text ?? "";
    internal bool LastTemporalEconomyVisible =>
        _lastTemporalEconomyLabel?.Visible == true;
    // R140: read-only order feasibility before a real human movement decision.
    private Label? _fineMovementDecision;
    private ulong? _selectedLocalWorkerId;
    // R38: composition owner, not a second simulation state inside the renderer.
    private CanonicalGameSession? _canonicalSession;
    // GA-NEXT TACTICAL-WORKSPACE-01: view-only windows, no worker-location aliasing.
    private Control? _planetWindow;
    private Control? _workspaceWindow;
    private Label? _operationalContext;
    private Label? _selectedUnitIdentity;
    private GaAuxiliaryPlanet? _auxiliaryPlanet;
    private VBoxContainer? _selectedUnitSlot;
    private const float R2212DockWidth = 304f;
    private bool _operationalCitySelected;
    private ulong? _operationalSelectedTile;
    private bool CompactOperationalDock => System.Environment.GetEnvironmentVariable("GA_NEXT_CONSTRUCTION_PREVIEW") == "1";
    private void RefreshOperationalContext()
    {
        if (_operationalContext is null) return;
        _operationalContext.Text = _operationalCitySelected && _selectedCartographicSite.HasValue
            ? $"City | Node {_selectedCartographicSite.Value}"
            : _operationalSelectedTile.HasValue ? $"Node {_operationalSelectedTile.Value}"
            : "Select a destination on the map";
        RefreshSelectedUnitSlot();
    }
    private void RefreshSelectedUnitSlot()
    {
        if (_selectedUnitIdentity is null) return;
        var worker = _tacticalHost?.Snapshot.Workers.Workers.FirstOrDefault(w =>
            w.Owner == _humanCivilization && w.Id.Value == _selectedLocalWorkerId);
        _selectedUnitIdentity.Text = worker is null ? "No unit selected" :
            $"Worker #{worker.Id.Value} | Node {worker.TacticalTileId?.FineStrategicCellId.Value}\n" +
            (worker.Activity == PhysicalWorkerActivity.Traveling ? "Traveling" : worker.Occupation == PhysicalWorkerOccupation.Unassigned ? "Idle / assigned" : worker.Occupation?.ToString() ?? worker.Activity.ToString());
    }
    private Label? _compactTurnPulse;
    private GaFloatingWindow? _atlasFloatingWindow;
    private Label? _metricTurn;
    private Label? _metricHumanWorkers;
    private Label? _metricAiCivilizations;
    private GaOperationalRoster? _operationalRoster;

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
    private PhysicalTacticalTileId? _initialTacticalHomeAnchor;

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
        if (_tacticalHost is not null)
        {
            RefreshFineTacticalWorkerControls();
            return;
        }
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
            _compactTurnPulse.Text = $"TURN {_localFoundation.NextTurn.Value} | REV {_localFoundation.Snapshot.Revision} | Human + AI";
        if (_metricTurn is not null)
            _metricTurn.Text = _localFoundation.NextTurn.Value.ToString();
        if (_metricHumanWorkers is not null)
            _metricHumanWorkers.Text = _localFoundation.ControllableWorkers.Count.ToString();
        if (_metricAiCivilizations is not null)
            _metricAiCivilizations.Text = _localFoundation.AiCivilizations.Count.ToString();
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

        // These are the actual, individually identified v9 census workers,
        // not approximate markers and not pseudo-positions on G(18,0).
        _operationalRoster?.Refresh(workers.Select(worker =>
            new GaWorkerRosterRow(
                worker.Id.Value, worker.CellId.Value, worker.Activity.ToString(),
                worker.Commodity.Value, worker.OutputMicroPerTurn,
                worker.DemandMultiplierMicro)).ToArray(),
            _selectedLocalWorkerId, _localFoundation.Snapshot.Revision);
    }

    internal static string FormatTemporalMoney(BigInteger scaled) => GaDisplayNumbers.MoneyScaled(scaled);

    private void RefreshLastTemporalEconomyReadout()
    {
        var label = _lastTemporalEconomyLabel;
        if (label is null) return;
        // Never expose staged orders, historical receipts after another
        // closure, or the financial lines of AI civilizations.
        var settlement = _tacticalHost?.GetLastClosedTemporalEconomics(
            _humanCivilization);
        label.Visible = settlement is not null;
        if (settlement is null)
        {
            label.Text = "";
            return;
        }
        label.Text =
            $"LAST ECONOMIC CLOSE | CYCLE {settlement.EconomicCycleOrdinal}\n" +
            $"Treasury credit: {FormatTemporalMoney(settlement.TotalCashCreditScaled)}\n" +
            $"Net sales: {FormatTemporalMoney(settlement.SellerNetScaled)}\n" +
            $"Tax receipts: {FormatTemporalMoney(settlement.TaxReceiptsScaled)}\n" +
            $"Toll receipts: {FormatTemporalMoney(settlement.OwnerTollIncomeScaled)}\n" +
            $"Global settled output: {GaDisplayNumbers.PhysicalMicro(settlement.PhysicalGoodsProducedMicro)} units";
    }

    private void RefreshFineTacticalWorkerControls()
    {
        var host = _tacticalHost;
        if (host is null || _localWorkerSelector is null ||
            _localMoveButton is null || _localProduceButton is null ||
            _localEndTurnButton is null || _localCancelQueuedButton is null ||
            _localPendingLabel is null || _localTurnLabel is null ||
            _localAiStatus is null) return;

        var workers = host.Snapshot.Workers.Workers
            .Where(worker => worker.Owner == _humanCivilization)
            .OrderBy(worker => worker.Id.Value).ToArray();
        if (!workers.Any(worker => worker.Id.Value == _selectedLocalWorkerId))
            _selectedLocalWorkerId = workers.FirstOrDefault()?.Id.Value;
        _localWorkerSelector.Clear();
        for (var i = 0; i < workers.Length; i++)
        {
            var worker = workers[i];
            var tile = worker.TacticalTileId!.Value.FineStrategicCellId.Value;
            var pending = host.GetHumanOrders(_humanCivilization)
                .Any(order => order.WorkerId == worker.Id);
            _localWorkerSelector.AddItem(
                CompactOperationalDock ? $"Worker #{worker.Id.Value} | Node {tile}" + (pending ? " *" : "") :
                $"#{worker.Id.Value} | G(18,0)/{tile} | {worker.Activity}" +
                (worker.Occupation.HasValue ? $" / {worker.Occupation}" : "") +
                (pending ? " | QUEUED" : ""));
            if (_selectedLocalWorkerId == worker.Id.Value)
                _localWorkerSelector.Select(i);
        }
        _localWorkerSelector.Disabled = workers.Length == 0;
        var chosen = workers.FirstOrDefault(
            worker => worker.Id.Value == _selectedLocalWorkerId);
        var ready = host.ReadyCivilizations.Contains(_humanCivilization);
        if (_tacticalPreview is not null)
        {
            _tacticalPreview.ExplicitMapMoveEnabled = CompactOperationalDock && host.PersistentOrdersEnabled && chosen is not null && !ready;
            _tacticalPreview.MapConnectionEnabled = CompactOperationalDock && host.PersistentOrdersEnabled;
        }
        var pendingOrders = host.GetHumanOrders(_humanCivilization);
        var selectedTile = _tacticalPreview?.SelectedCanonicalPhysicalTileId;
        PhysicalTacticalTileId? destination = selectedTile.HasValue
            ? new PhysicalTacticalTileId(
                host.Incidence.Refinement.FineParameters,
                new StrategicCellId(selectedTile.Value))
            : null;
        bool adjacent = chosen is not null && destination.HasValue &&
            host.Incidence.FineTopology.Cells[
                checked((int)chosen.TacticalTileId!.Value
                    .FineStrategicCellId.Value - 1)]
                .AdjacentCellIds.Contains(destination.Value.FineStrategicCellId);
        _localMoveButton.Disabled = ready || !adjacent;
        RefreshFineMovementDecision(selectedTile, adjacent, ready);
        _localProduceButton.Disabled = ready || chosen is null ||
            chosen.Activity == PhysicalWorkerActivity.Producing ||
            chosen.OutputMicroPerTurn == 0UL ||
            chosen.Occupation is (PhysicalWorkerOccupation.LandTransport or
                PhysicalWorkerOccupation.PortOperations);
        _localCancelQueuedButton.Disabled = ready || chosen is null ||
            !pendingOrders.Any(order => order.WorkerId == chosen.Id);
        if (_r149LandWorkButton is not null && _r149PortWorkButton is not null)
        {
            bool canAssign = !ready && chosen is not null &&
                host.Snapshot.Workers.HasAuthoritativeOccupations &&
                chosen.Activity is (PhysicalWorkerActivity.Producing or
                    PhysicalWorkerActivity.Other);
            _r149LandWorkButton.Visible = host.Snapshot.Workers.HasAuthoritativeOccupations;
            _r149PortWorkButton.Visible = host.Snapshot.Workers.HasAuthoritativeOccupations;
            _r149LandWorkButton.Disabled = !canAssign ||
                chosen!.Occupation == PhysicalWorkerOccupation.LandTransport;
            _r149PortWorkButton.Disabled = !canAssign ||
                chosen!.Occupation == PhysicalWorkerOccupation.PortOperations;
        }
        var hasOccupations = host.Snapshot.Workers.HasAuthoritativeOccupations;
        foreach (var (button, role) in new[] {
            (_localAgricultureButton, PhysicalWorkerOccupation.Agriculture),
            (_localForestryButton, PhysicalWorkerOccupation.Forestry),
            (_localMiningButton, PhysicalWorkerOccupation.Mining) })
            if (button is not null)
            {
                button.Visible = hasOccupations;
                button.Disabled = ready || chosen is null ||
                    !hasOccupations || chosen.Occupation == role ||
                    chosen.Activity is (PhysicalWorkerActivity.Traveling or
                        PhysicalWorkerActivity.Military);
            }
        _localEndTurnButton.Disabled = false;
        _localTurnLabel.Text =
            $"TACTICAL HUMAN + AI v{host.StateHash.FormatVersion} | turn {host.NextTurn.Value} | " +
            $"rev {host.Snapshot.Revision} | Human Civ {_humanCivilization.Value}";
        if (_compactTurnPulse is not null)
            _compactTurnPulse.Text =
                $"TURN {host.NextTurn.Value} | REV {host.Snapshot.Revision} | Human + AI v{host.StateHash.FormatVersion}";
        if (_metricTurn is not null)
            _metricTurn.Text = host.NextTurn.Value.ToString();
        if (_metricHumanWorkers is not null)
            _metricHumanWorkers.Text = workers.Length.ToString();
        if (_metricAiCivilizations is not null)
            _metricAiCivilizations.Text = host.AiCivilizations.Count.ToString();
        _localAiStatus.Text =
            $"AI CIVILIZATIONS | {host.AiCivilizations.Count} opponent | " +
            $"{host.AiCivilizations.Count(civ => host.ReadyCivilizations.Contains(civ))} ready";
        _localPendingLabel.Text = pendingOrders.Count == 0
            ? "No human orders queued; E closes the collective turn"
            : string.Join(", ", pendingOrders.Select(order =>
                $"#{order.WorkerId.Value} " +
                (order.NextOccupation.HasValue
                    ? $"{order.Activity} / {order.NextOccupation.Value} at tile {order.Source.FineStrategicCellId.Value}"
                    : order.IsMovement
                        ? $"G(18,0)/{order.Source.FineStrategicCellId.Value}→{order.Destination.FineStrategicCellId.Value}"
                        : $"{order.Activity} at {order.Source.FineStrategicCellId.Value}")));
        _operationalRoster?.Refresh(workers.Select(worker =>
            new GaWorkerRosterRow(
                worker.Id.Value, worker.CellId.Value,
                worker.Activity.ToString(), worker.Commodity.Value,
                worker.OutputMicroPerTurn, worker.DemandMultiplierMicro,
                worker.TacticalTileId!.Value.FineStrategicCellId.Value,
                worker.Occupation switch
                {
                    PhysicalWorkerOccupation.Agriculture => "Agricultura",
                    PhysicalWorkerOccupation.Forestry => "Extração vegetal",
                    PhysicalWorkerOccupation.Mining => "Mineração",
                    PhysicalWorkerOccupation.LandTransport => "Transporte terrestre",
                    PhysicalWorkerOccupation.PortOperations => "Operação portuária",
                    PhysicalWorkerOccupation.Unassigned => "Não atribuída",
                    null => null,
                    _ => throw new System.InvalidOperationException(
                        "Invalid productive worker occupation")
                })).ToArray(),
            _selectedLocalWorkerId, host.Snapshot.Revision);
        _tacticalPreview?.PresentAuthoritativeWorkers(
            host.Snapshot.Workers, _selectedLocalWorkerId);
        // Strictly participant-filtered last CLOSED market. The default
        // worker-only preview has no settled sale and draws nothing.
        var authorizedSettledTrade =
            host.GetLastClosedEconomicTradeView(_humanCivilization);
        _tacticalPreview?.PresentSettledTradeRoutes(authorizedSettledTrade);
        _strategicGlobe?.PresentSphericalSettledTradeRoutes(authorizedSettledTrade);
        _tacticalPreview?.PresentPendingWorkerBoardOrders(
            pendingOrders.Where(order => order.NextOccupation.HasValue)
                .Select(order => (order.WorkerId.Value, order.NextOccupation!.Value)),
            !ready);
        RefreshPortInvestmentQaControls();
        RefreshOpenConnectionInvestment();
        RefreshLocalSiteSelectionQa();
        RefreshOperationalContext();
        RefreshLastTemporalEconomyReadout();
        RefreshCivilizationWindow();
        RefreshCityProduction();
        RefreshCityWorkers();
        RefreshCityConnectionTrade();
        RefreshPersistentWorkerControls();
    }

    // R140: read directly from the same authoritative worker and pending-order
    // snapshots as the movement command. No UI-owned copies of game state.
    private void RefreshFineMovementDecision(ulong? selectedTile, bool adjacent, bool ready)
    {
        if (_fineMovementDecision is null || _tacticalHost is null) return;
        var worker = _tacticalHost.Snapshot.Workers.Workers.FirstOrDefault(w =>
            w.Id.Value == _selectedLocalWorkerId && w.Owner == _humanCivilization);
        if (worker is null)
        {
            _fineMovementDecision.Text = "MOVEMENT | Select one of your workers";
            return;
        }
        var source = worker.TacticalTileId!.Value.FineStrategicCellId.Value;
        var queued = _tacticalHost.GetHumanOrders(_humanCivilization)
            .FirstOrDefault(o => o.WorkerId == worker.Id);
        var queuedText = queued is null
            ? worker.Activity == PhysicalWorkerActivity.Traveling &&
              worker.OutputMicroPerTurn > 0UL
                ? "production resumes automatically on next E unless reassigned"
                : "no pending order"
            : queued.IsMovement
                ? $"move queued to tile {queued.Destination.FineStrategicCellId.Value}"
                : queued.NextOccupation.HasValue
                    ? $"occupation queued: {queued.NextOccupation.Value}"
                    : $"activity queued: {queued.Activity}";
        string decision = !selectedTile.HasValue
            ? "select a destination tile on the map"
            : selectedTile.Value == source
                ? "destination equals current tile"
                : adjacent
                    ? ready ? "turn already committed" : "adjacent — G can queue movement"
                    : "not adjacent — one physical edge per move";
        var target = selectedTile.HasValue ? selectedTile.Value.ToString() : "none";
        _fineMovementDecision.Text =
            $"MOVEMENT | worker #{worker.Id.Value} | from {source} to {target}\n" +
            $"{decision} | {queuedText}";
    }

    internal string FineMovementDecisionText => _fineMovementDecision?.Text ?? "";

    private void QueueFineWorkerMove()
    {
        if (_tacticalHost is null || !_selectedLocalWorkerId.HasValue ||
            !_tacticalPreview!.SelectedCanonicalPhysicalTileId.HasValue ||
            _localOrderStatus is null) return;
        try
        {
            var target = new PhysicalTacticalTileId(
                _tacticalHost.Incidence.Refinement.FineParameters,
                new StrategicCellId(
                    _tacticalPreview.SelectedCanonicalPhysicalTileId.Value));
            if (_tacticalHost.PersistentOrdersEnabled)
                AppendPersistentOrder(new PhysicalWorkerId(_selectedLocalWorkerId.Value), FoundationWorkerOrderKind.Move, destination: target);
            else _tacticalHost.QueueHumanMove(
                _humanCivilization,
                new PhysicalWorkerId(_selectedLocalWorkerId.Value), target);
            _localOrderStatus.Text =
                $"Worker #{_selectedLocalWorkerId.Value}: {(_tacticalHost.PersistentOrdersEnabled ? "journey" : "physical step")} queued " +
                $"toward G(18,0)/{target.FineStrategicCellId.Value}. E to commit.";
            GD.Print($"GA_NEXT_FINE_MOVE_QUEUED worker={_selectedLocalWorkerId.Value} " +
                $"destination={target.FineStrategicCellId.Value} " +
                $"pending={_tacticalHost.PendingOrderCount} mutation=false");
        }
        catch (System.Exception e)
        {
            _localOrderStatus.Text = $"Fine move rejected: {e.Message}";
            GD.Print($"GA_NEXT_FINE_MOVE_REJECTED {e.Message}");
        }
        RefreshFineTacticalWorkerControls();
    }

    private void QueueFineWorkerProduction()
    {
        if (_tacticalHost is null || !_selectedLocalWorkerId.HasValue ||
            _localOrderStatus is null) return;
        try
        {
            if (_tacticalHost.PersistentOrdersEnabled)
            {
                var worker = _tacticalHost.Snapshot.Workers.Workers.Single(w => w.Id.Value == _selectedLocalWorkerId.Value);
                AppendPersistentOrder(worker.Id, FoundationWorkerOrderKind.Occupation,
                    occupation: worker.Occupation is PhysicalWorkerOccupation.Agriculture or PhysicalWorkerOccupation.Forestry or PhysicalWorkerOccupation.Mining ? worker.Occupation : PhysicalWorkerOccupation.Agriculture);
            }
            else _tacticalHost.QueueHumanActivity(
                _humanCivilization, new PhysicalWorkerId(_selectedLocalWorkerId.Value),
                PhysicalWorkerActivity.Producing);
            _localOrderStatus.Text =
                $"Worker #{_selectedLocalWorkerId.Value}: production restart queued.";
            GD.Print($"GA_NEXT_FINE_PRODUCTION_QUEUED worker={_selectedLocalWorkerId.Value} " +
                "mutation=false");
        }
        catch (System.Exception e)
        {
            _localOrderStatus.Text = $"Activity rejected: {e.Message}";
        }
        RefreshFineTacticalWorkerControls();
    }

    private void OnWorkerBoardOccupationRequested(
        ulong workerId, PhysicalWorkerOccupation occupation)
    {
        if (_tacticalHost is null ||
            !_tacticalHost.Snapshot.Workers.Workers.Any(worker =>
                worker.Id.Value == workerId && worker.Owner == _humanCivilization))
        {
            GD.PrintErr($"GA_NEXT_WORKER_BOARD_REJECT_NOT_OWNER worker={workerId}");
            return;
        }
        OnCanonicalWorkerBadgeClicked(workerId);
        QueueFineWorkerOccupation(occupation);
    }

    private void OnWorkerBoardCancelSelectedOrderRequested(ulong workerId)
    {
        if (_tacticalHost is null || _localOrderStatus is null ||
            !_tacticalHost.Snapshot.Workers.Workers.Any(worker =>
                worker.Id.Value == workerId &&
                worker.Owner == _humanCivilization))
        {
            GD.PrintErr($"GA_NEXT_R110_CANCEL_REJECT_NOT_OWNER worker={workerId}");
            return;
        }
        try
        {
            bool cancelled = _tacticalHost.PersistentOrdersEnabled ? RemovePersistentHead(new PhysicalWorkerId(workerId)) : _tacticalHost.CancelHumanOrder(
                _humanCivilization, new PhysicalWorkerId(workerId));
            _localOrderStatus.Text = cancelled
                ? $"Worker #{workerId}: pending order cancelled; no turn committed."
                : $"Worker #{workerId}: no pending order to cancel.";
            GD.Print($"GA_NEXT_R110_BOARD_CANCEL worker={workerId} " +
                $"cancelled={cancelled.ToString().ToLowerInvariant()} " +
                "source=authorized_host before_turn=true");
        }
        catch (System.Exception error)
        {
            _localOrderStatus.Text =
                $"Worker #{workerId}: cancellation rejected: {error.Message}";
            GD.PrintErr($"GA_NEXT_R110_BOARD_CANCEL_REJECT {error.Message}");
        }
        RefreshFineTacticalWorkerControls();
    }

    private void QueueFineWorkerOccupation(PhysicalWorkerOccupation occupation)
    {
        if (_tacticalHost is null || !_selectedLocalWorkerId.HasValue ||
            _localOrderStatus is null) return;
        try
        {
            var worker = _tacticalHost.Snapshot.Workers.Workers.Single(w =>
                w.Id.Value == _selectedLocalWorkerId.Value);
            if (_tacticalHost.PersistentOrdersEnabled)
                AppendPersistentOrder(worker.Id, FoundationWorkerOrderKind.Occupation, occupation: occupation);
            else if (worker.Occupation is (PhysicalWorkerOccupation.LandTransport or
                    PhysicalWorkerOccupation.PortOperations))
                _tacticalHost.QueueHumanProductiveRole(_humanCivilization,
                    worker.Id, occupation);
            else
                _tacticalHost.QueueHumanOccupation(_humanCivilization,
                    worker.Id, occupation);
            _localOrderStatus.Text =
                $"Worker #{_selectedLocalWorkerId.Value}: {occupation} " +
                "queued at the current fine tile. Press E to apply.";
            GD.Print($"GA_NEXT_OCCUPATION_QUEUED worker={_selectedLocalWorkerId.Value} " +
                $"occupation={occupation} pending={_tacticalHost.PendingOrderCount} " +
                "world_mutation=false");
        }
        catch (System.Exception e)
        {
            _localOrderStatus.Text = $"Occupation reassignment rejected: {e.Message}";
            GD.Print($"GA_NEXT_OCCUPATION_REJECTED {e.Message}");
        }
        RefreshFineTacticalWorkerControls();
    }

    private void QueueFineWorkerOperationalRole(
        FoundationOperationalWorkKind kind)
    {
        if (_tacticalHost is null || !_selectedLocalWorkerId.HasValue ||
            _localOrderStatus is null) return;
        try
        {
            if (_tacticalHost.PersistentOrdersEnabled)
                AppendPersistentOrder(new PhysicalWorkerId(_selectedLocalWorkerId.Value), FoundationWorkerOrderKind.Occupation,
                    occupation: kind == FoundationOperationalWorkKind.Land ? PhysicalWorkerOccupation.LandTransport : PhysicalWorkerOccupation.PortOperations);
            else _tacticalHost.QueueHumanOperationalRole(_humanCivilization,
                new PhysicalWorkerId(_selectedLocalWorkerId.Value), kind);
            _localOrderStatus.Text =
                $"Worker #{_selectedLocalWorkerId.Value}: {kind} queued at the " +
                "current physical tile. Press E to commit; X cancels.";
            GD.Print($"GA_NEXT_R149_LOGISTICS_ORDER worker={_selectedLocalWorkerId.Value} " +
                $"kind={kind} pending={_tacticalHost.PendingOrderCount} " +
                "world_mutation=false");
        }
        catch (System.Exception error)
        {
            _localOrderStatus.Text =
                $"Logistics assignment rejected: {error.Message}";
            GD.PrintErr($"GA_NEXT_R149_LOGISTICS_REJECT {error.Message}");
        }
        RefreshFineTacticalWorkerControls();
    }

    private void CancelFineWorkerOrder()
    {
        if (_tacticalHost is null || !_selectedLocalWorkerId.HasValue ||
            _localOrderStatus is null) return;
        try
        {
            if (_tacticalHost.PersistentOrdersEnabled ? RemovePersistentHead(new PhysicalWorkerId(_selectedLocalWorkerId.Value)) : _tacticalHost.CancelHumanOrder(
                    _humanCivilization,
                    new PhysicalWorkerId(_selectedLocalWorkerId.Value)))
                _localOrderStatus.Text =
                    $"Worker #{_selectedLocalWorkerId.Value}: pending order cancelled.";
        }
        catch (System.Exception e)
        {
            _localOrderStatus.Text = $"Cancellation rejected: {e.Message}";
        }
        RefreshFineTacticalWorkerControls();
    }

    private void EndFineCollectiveTurn()
    {
        if (_tacticalHost is null || _localOrderStatus is null) return;
        try
        {
            var host = _tacticalHost;
            if (!host.ReadyCivilizations.Contains(_humanCivilization))
                host.SetHumanReady(_humanCivilization);
            var receipt = host.TryCloseTurn() ??
                throw new System.InvalidOperationException(
                    "Collective turn not closed: all participants not ready.");
            AttachRuntimeState(host.Snapshot);
            AuditPortInvestmentQa(receipt);
            var humanCount = receipt.Events.Count(x => x.Issuer == _humanCivilization);
            var aiCount = receipt.Events.Count(x =>
                x.Issuer != _humanCivilization);
            _localOrderStatus.Text =
                $"Turn {receipt.Turn.Value}: Human {humanCount} + AI {aiCount} " +
                $"actions | journal replay OK | hash v{receipt.Hash.FormatVersion}";
            if (receipt.PersistentTimeline is { } timeline)
            {
                humanCount = receipt.PersistentEdits.Count(x => x.Edit.Issuer == _humanCivilization) + receipt.PersistentFunding.Count(x => x.Investor == _humanCivilization);
                _localOrderStatus.Text = $"Turn {receipt.Turn.Value}: {humanCount} submitted commands | {timeline.Slices.Select(x => x.Worker).Distinct().Count()} workers scheduled | journal replay OK";
            }
            if (receipt.EconomicSettlement is { } settlement)
            {
                var view = host.GetLastClosedEconomicTradeView(
                    _humanCivilization);
                _localOrderStatus.Text =
                    $"Turn {receipt.Turn.Value}: economic cycle " +
                    $"{settlement.EconomicCycleOrdinal} closed | " +
                    $"{settlement.SoldAndConsumedGoodsMicro}/" +
                    $"{settlement.ProducedGoodsMicro} goods sold/consumed | " +
                    $"routes visible {view.UsedEdges.Count} | " +
                    $"journal replay OK | hash v{receipt.Hash.FormatVersion}";
                GD.Print($"GA_NEXT_V15_SETTLED_TRADE_READY " +
                    $"cycle={settlement.EconomicCycleOrdinal} " +
                    $"visible_edge_count={view.UsedEdges.Count} " +
                    $"sold_micro={settlement.SoldAndConsumedGoodsMicro} " +
                    $"produced_micro={settlement.ProducedGoodsMicro} " +
                    "source=authoritative_journal privacy=participant");
            }
            GD.Print($"GA_NEXT_FINE_HUMAN_AI_COMMIT turn={receipt.Turn.Value} " +
                $"human={humanCount} ai={aiCount} " +
                $"rev={receipt.Revision} replay={receipt.ReplayMatched} " +
                $"hash_v={receipt.Hash.FormatVersion}");
        }
        catch (System.Exception e)
        {
            _localOrderStatus.Text = $"Collective turn rejected: {e.Message}";
            GD.PrintErr($"GA_NEXT_FINE_HUMAN_AI_REJECTED {e}");
        }
        RefreshFineTacticalWorkerControls();
        RefreshInteractiveEconomyPreviewBanner();
    }

    private void OnLocalWorkerSelected(long optionIndex)
    {
        if (_tacticalHost is not null)
        {
            var fineWorkers = _tacticalHost.Snapshot.Workers.Workers
                .Where(w => w.Owner == _humanCivilization).OrderBy(w => w.Id.Value).ToArray();
            var chosenIndex = checked((int)optionIndex);
            if (chosenIndex >= 0 && chosenIndex < fineWorkers.Length)
            {
                _selectedLocalWorkerId = fineWorkers[chosenIndex].Id.Value;
                _operationalCitySelected = false;
                _operationalSelectedTile = null;
                RefreshFineTacticalWorkerControls();
            }
            return;
        }
        if (_localFoundation is null)
            return;
        var workers = _localFoundation.ControllableWorkers;
        var index = checked((int)optionIndex);
        _selectedLocalWorkerId = index >= 0 && index < workers.Count
            ? workers[index].Id.Value : null;
        RefreshLocalWorkerOrderControls();
    }

    private void OnOperationalWorkerSelected(ulong id)
    {
        if (_tacticalHost is not null)
        {
            if (!_tacticalHost.Snapshot.Workers.Workers.Any(w =>
                w.Id.Value == id && w.Owner == _humanCivilization))
                return;
            _selectedLocalWorkerId = id;
            _operationalCitySelected = false;
            _operationalSelectedTile = null;
            RefreshOperationalContext();
            RefreshFineTacticalWorkerControls();
            GD.Print($"GA_NEXT_FINE_WORKER_SELECTED worker={id} source=canonical_v10 mutation=false");
            return;
        }
        if (_localFoundation is null ||
            !_localFoundation.ControllableWorkers.Any(worker => worker.Id.Value == id))
            return;
        // Selection only; this cannot queue a strategic destination or
        // impersonate a canonical physical tactical tile.
        _selectedLocalWorkerId = id;
        RefreshLocalWorkerOrderControls();
        GD.Print($"GA_NEXT_ROSTER_SELECTED worker={id} " +
            "source=canonical_v9 fine_location=false mutation=false");
    }

    private void OnLocalWorkerMoveRequested()
    {
        if (_tacticalHost is not null)
        {
            QueueFineWorkerMove();
            return;
        }
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
        if (_tacticalHost is not null)
        {
            QueueFineWorkerProduction();
            return;
        }
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
        if (_tacticalHost is not null)
        {
            CancelFineWorkerOrder();
            return;
        }
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
        if (_tacticalHost is not null)
        {
            EndFineCollectiveTurn();
            return;
        }
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

    // UI-wide shortcuts MUST run before Godot's Control keyboard focus
    // navigation: Tab is ui_focus_next and Escape is ui_cancel. Both can be
    // consumed before _UnhandledKeyInput. Keep this handler presentation-
    // only; only E delegates to the existing authoritative turn host.
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo ||
            key.AltPressed || key.CtrlPressed || key.MetaPressed ||
            key.ShiftPressed || _workspaceWindow is null ||
            _planetWindow is null)
            return;

        if (key.Keycode == Key.M)
        {
            TogglePlanetWindow();
        }
        else if (key.Keycode == Key.Tab)
        {
            ToggleWorkspaceWindow();
        }
        else if (key.Keycode == Key.Y)
        {
            OpenSelectedCityWindow();
        }
        else if (key.Keycode == Key.C)
        {
            OpenCivilizationWindow();
        }
        else if (key.Keycode == Key.Escape && _cityWorkerOrigin.HasValue && _r224Window?.Visible == true)
        {
            CloseR224WorkerWindow();
        }
        else if (key.Keycode == Key.Escape &&
            _civilizationWindowLayer?.Visible == true)
        {
            _civilizationWindowLayer.Visible = false;
        }
        else if (key.Keycode == Key.Escape && _cityWindowLayer?.Visible == true)
        {
            _cityWindowLayer.Visible = false;
        }
        else if (key.Keycode == Key.Escape &&
            _tacticalPreview?.DismissTileClickMenu() == true)
        {
            // Experimental hierarchy closes before any permanent dock.
        }
        else if (key.Keycode == Key.Escape &&
            (_planetWindow.Visible || _workspaceWindow.Visible))
        {
            // Escape dismisses the upper floating atlas first, leaving
            // the operational dock stable underneath.
            if (_planetWindow.Visible) ClosePlanetWindow();
            else SetOperationalDockVisible(false);
        }
        else if (!_planetWindow.Visible &&
                 key.Keycode is Key.G or Key.P or Key.X &&
                 _tacticalHost is not null)
        {
            if (key.Keycode == Key.G) OnLocalWorkerMoveRequested();
            else if (key.Keycode == Key.P) OnLocalWorkerProduceRequested();
            else OnLocalCancelQueuedRequested();
        }
        else if (!_planetWindow.Visible &&
                 _tacticalHost?.Snapshot.Workers.HasAuthoritativeOccupations == true &&
                 key.Keycode is Key.A or Key.V or Key.N)
        {
            QueueFineWorkerOccupation(key.Keycode switch
            {
                Key.A => PhysicalWorkerOccupation.Agriculture,
                Key.V => PhysicalWorkerOccupation.Forestry,
                Key.N => PhysicalWorkerOccupation.Mining,
                _ => throw new System.InvalidOperationException("Unknown occupation shortcut")
            });
        }
        else if (key.Keycode == Key.E &&
            !_planetWindow.Visible && _localEndTurnButton?.Disabled == false)
        {
            OnLocalTurnEndRequested();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    // Historical foundation G/P/X/E fixture remains in _UnhandledKeyInput
    // for the PR #361 regression scene, which has no tactical windows.
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;
        // Normal tactical play routes these through _Input, never twice.
        if (_workspaceWindow is not null) return;
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

    // Hit-test on the badge itself takes precedence over tile-level first-ID
    // selection when several independent workers share the same physical tile.
    private void OnCanonicalWorkerBadgeClicked(ulong id)
    {
        if (_tacticalHost is null ||
            !_tacticalHost.Snapshot.Workers.Workers.Any(w =>
                w.Id.Value == id && w.Owner == _humanCivilization))
            return;
        _selectedLocalWorkerId = id;
        _operationalCitySelected = false;
        _operationalSelectedTile = null;
        RefreshOperationalContext();
        RefreshFineTacticalWorkerControls();
        GD.Print($"GA_NEXT_WORKER_BADGE_SELECTED worker={id} " +
            "source=canonical_v10 no_order=true state_unchanged=true");
    }

    private void OnR226MapMoveRequested(PhysicalTacticalTileId tile)
    {
        var host = _tacticalHost;
        if (host?.PersistentOrdersEnabled != true || !CompactOperationalDock ||
            _selectedLocalWorkerId is not { } id || host.ReadyCivilizations.Contains(_humanCivilization)) return;
        var worker = host.Snapshot.Workers.Workers.FirstOrDefault(w => w.Id.Value == id && w.Owner == _humanCivilization);
        if (worker is null || worker.TacticalTileId == tile) return;
        try
        {
            AppendPersistentOrder(worker.Id, FoundationWorkerOrderKind.Move, destination: tile);
            if (_localOrderStatus is not null) _localOrderStatus.Text = $"Worker #{id}: move to node {tile.FineStrategicCellId.Value} queued. E to commit.";
        }
        catch (Exception ex)
        {
            if (_localOrderStatus is not null) _localOrderStatus.Text = "Map move rejected: " + ex.Message;
        }
        RefreshFineTacticalWorkerControls();
    }

    private void OnTacticalPhysicalTileClicked(PhysicalTacticalTileId tile)
    {
        if (_tacticalHost is null) return;
        // A player may click their own physical worker to select it.
        // Clicking an empty/AI-occupied tile instead chooses a destination,
        // never transfers ownership or changes authoritative state.
        var own = _tacticalHost.Snapshot.Workers.Workers
            .Where(w => w.Owner == _humanCivilization &&
                w.TacticalTileId == tile)
            .OrderBy(w => w.Id.Value).FirstOrDefault();
        // Cartographic click selects a PLACE, not an individual worker.
        // Workforce assignment is retained for the future local admin UI.
        if (CompactOperationalDock && _tacticalPreview?.IsCartographicSiteMode == true)
        {
            _operationalSelectedTile = tile.FineStrategicCellId.Value;
            _operationalCitySelected = false;
            if (_tacticalPreview.LocalSiteMarkerTileIds.Contains(tile.FineStrategicCellId.Value))
                OnCartographicLocalSiteSelected(tile.FineStrategicCellId.Value);
            else
            {
                _selectedCartographicSite = null;
                RefreshOperationalContext();
            }
        }
        else if (own is not null && _tacticalPreview?.IsCartographicSiteMode != true)
            _selectedLocalWorkerId = own.Id.Value;
        RefreshFineTacticalWorkerControls();
        GD.Print($"GA_NEXT_FINE_TILE_CLICK tile={tile.FineStrategicCellId.Value} " +
            $"worker={(_selectedLocalWorkerId?.ToString() ?? "none")} " +
            "authoritative_mutation=false");
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
            _tacticalPreview.Open(_bridgeSelection.Value,
                _initialTacticalHomeAnchor);
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
                if (_tacticalSettledTradeRoutes is not null)
                    _tacticalSettledTradeRoutes.Visible = true;
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
        if (_tacticalSettledTradeRoutes is not null)
            _tacticalSettledTradeRoutes.Visible = false;
        _tacticalBridgeStatus.Text = _bridgeSelection.HasValue
            ? $"Territory {_bridgeSelection.Value} selected | tactical context available"
            : "Select a canonical territory to enable tactical context";
        GD.Print("GA_UI_TACTICAL_TRANSITION_RETURN globe_state_preserved=true");
    }

    // A local Control-based window, not a second OS window or a game-state owner.
    // The same GlobePreview instance is reparented; it never regenerates a world.
    private void CreateTacticalWorkspaceWindows(GlobePreview globe,
        PanelContainer rightPanel, HBoxContainer header, HBoxContainer content)
    {
        var openPlanet = new Button { Name = "GANextPlanetButton", Text = "M | PLANET" };
        openPlanet.Pressed += TogglePlanetWindow;
        header.AddChild(openPlanet);
        var city = new Button { Name = "GANextCityButton", Text = "Y | CITY" };
        city.Pressed += OpenSelectedCityWindow;
        header.AddChild(city);
        var civilization = new Button
        {
            Name = "GANextCivilizationButton",
            Text = "C | CIVILIZATION"
        };
        civilization.Pressed += OpenCivilizationWindow;
        header.AddChild(civilization);
        var openWorkspace = new Button { Name = "GANextPanelsButton", Text = "TAB | PANELS" };
        openWorkspace.Pressed += ToggleWorkspaceWindow;
        header.AddChild(openWorkspace);
        _compactTurnPulse = new Label
        {
            Text = "TURN 1 | REV 0 | Human + AI",
            VerticalAlignment = VerticalAlignment.Center
        };
        header.AddChild(_compactTurnPulse);
        var quickEnd = new Button { Name = "GANextEndTurnButton", Text = "E | END TURN" };
        GaVisualFoundation.StylePrimaryAction(quickEnd);
        quickEnd.Pressed += OnLocalTurnEndRequested;
        header.AddChild(quickEnd);

        // TAB is a real HBox sibling of the map, not an overlay. Hiding
        // this container returns the entire width to the tactical viewport.
        var operations = new MarginContainer
        {
            Name = "GANextOperationsWindow",
            Visible = CompactOperationalDock,
            CustomMinimumSize = new Vector2(CompactOperationalDock ? R2212DockWidth : 394f, 0f),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        content.AddChild(operations);
        var operationsPanel = new PanelContainer
        {
            Name = "GANextOperationsPanel",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop
        };
        operations.AddChild(operationsPanel);
        operationsPanel.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        var operationsColumn = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        operationsPanel.AddChild(operationsColumn);
        Button? cityNavigation = null, civilizationNavigation = null;
        if (CompactOperationalDock)
        {
            var compactHeader = new HBoxContainer();
            operationsColumn.AddChild(compactHeader);
            compactHeader.AddChild(new Label { Text = LayeredWorldDock ? "WORLD CONTEXT" : "COMMANDS", SizeFlagsHorizontal = SizeFlags.ExpandFill });
            var hide = new Button { Text = "X" };
            hide.Pressed += () => SetOperationalDockVisible(false);
            compactHeader.AddChild(hide);
            var selectedPanel = new PanelContainer { Name = "GANextSelectedUnitSlot" };
            selectedPanel.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
            operationsColumn.AddChild(selectedPanel);
            _selectedUnitSlot = new VBoxContainer();
            selectedPanel.AddChild(_selectedUnitSlot);
            _selectedUnitSlot.AddChild(new Label { Text = LayeredWorldDock ? "UNIT" : "SELECTED UNIT" });
            var unitRow = new HBoxContainer();
            _selectedUnitSlot.AddChild(unitRow);
            unitRow.AddChild(new R218WorkerSilhouette { CustomMinimumSize = new Vector2(28, 32), MouseFilter = MouseFilterEnum.Ignore });
            _selectedUnitIdentity = new Label { Name = "GANextSelectedUnitIdentity", AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            unitRow.AddChild(_selectedUnitIdentity);
            _operationalContext = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            operationsColumn.AddChild(_operationalContext);
            var cityAction = cityNavigation = new Button { Text = "Y | OPEN CITY" };
            cityAction.Pressed += OpenSelectedCityWindow;
            operationsColumn.AddChild(cityAction);
            var civAction = civilizationNavigation = new Button { Text = "C | CIVILIZATION" };
            civAction.Pressed += OpenCivilizationWindow;
            operationsColumn.AddChild(civAction);
            RefreshOperationalContext();
        }
        else GaVisualFoundation.AddWindowHeader(
            operationsColumn, "GA / COMMAND DESK", "Operational workspace",
            "People · orders · administrative intelligence",
            () => SetOperationalDockVisible(false));
        var metrics = new HBoxContainer();
        metrics.AddThemeConstantOverride("separation", 6);
        operationsColumn.AddChild(metrics);
        metrics.Visible = !CompactOperationalDock;
        metrics.AddChild(GaVisualFoundation.CreateMetric(
            "TURN", _localFoundation!.NextTurn.Value.ToString(), out _metricTurn));
        metrics.AddChild(GaVisualFoundation.CreateMetric(
            "HUMAN", _localFoundation.ControllableWorkers.Count.ToString(),
            out _metricHumanWorkers));
        metrics.AddChild(GaVisualFoundation.CreateMetric(
            "AI", _localFoundation.AiCivilizations.Count.ToString(),
            out _metricAiCivilizations));
        // Keep compact command header and turn metrics accessible while the
        // ENTIRE body scrolls. Previously only an inner sub-section of the
        // reparented sidebar could scroll, clipping lower worker commands.
        // ScrollContainer supplies a bounded viewport rather than asking the
        // VBox parent for the full height of every detail and order control.
        var operationsScroll = new ScrollContainer
        {
            Name = "GANextOperationsBodyScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            MouseFilter = MouseFilterEnum.Stop,
            ClipContents = true
        };
        operationsColumn.AddChild(operationsScroll);
        var operationsBody = new VBoxContainer
        {
            Name = "GANextOperationsBody",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass
        };
        operationsBody.AddThemeConstantOverride("separation", 8);
        operationsScroll.AddChild(operationsBody);
        var roster = new GaOperationalRoster
        {
            Name = "GANextOperationalRoster",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        roster.WorkerSelected += OnOperationalWorkerSelected;
        operationsBody.AddChild(roster);
        _operationalRoster = roster;
        roster.Visible = !CompactOperationalDock;
        // R132: a selected local center belongs at the TOP of the
        // administration pane, not buried under the worker roster and
        // every experimental order widget. Reparent existing controls:
        // no second model, window, or authoritative state.
        if (_localSiteSelectionQa is not null &&
            _localCenterQaPanel is not null)
        {
            _localSiteSelectionQa.Reparent(operationsBody);
            _localCenterQaPanel.Reparent(operationsBody);
            operationsBody.MoveChild(_localSiteSelectionQa, 0);
            operationsBody.MoveChild(_localCenterQaPanel, 1);
        }
        // The existing region/worker/order controls keep their original
        // event callbacks and remain below the roster in one scroll flow.
        rightPanel.Reparent(operationsBody);
        rightPanel.CustomMinimumSize = Vector2.Zero;
        rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        rightPanel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _workspaceWindow = operations;
        if (CompactOperationalDock)
        {
            // Keep existing detailed commands reachable on demand, not as default clutter.
            // Keep diagnostic controls instantiated for existing callbacks, but
            // outside the visible command layout. Reuse the real order widgets.
            rightPanel.Visible = false;
            if (_localSiteSelectionQa is not null) _localSiteSelectionQa.Visible = false;
            var orderPanel = _localWorkerSelector!.GetParent() as VBoxContainer
                ?? throw new InvalidOperationException("Worker command layout unavailable.");
            orderPanel.Reparent(operationsBody);
            orderPanel.CustomMinimumSize = Vector2.Zero;
            _localTurnLabel!.Visible = false;
            _localAiStatus!.Visible = false;
            _localEndTurnButton!.Visible = false;
            _lastTemporalEconomyLabel!.Visible = false;
            _localMoveButton!.Text = "G | MOVE TO NODE";
            _localProduceButton!.Text = "P | PRODUCE";
            _localCancelQueuedButton!.Text = "X | CANCEL ORDER";
            _localAgricultureButton!.Text = "A | AGRICULTURE";
            _localForestryButton!.Text = "V | FORESTRY";
            _localMiningButton!.Text = "N | MINING";
            _r149LandWorkButton!.Text = "LAND TRANSPORT";
            _r149PortWorkButton!.Text = "PORT OPERATIONS";
            foreach (var button in new[] { _localAgricultureButton, _localForestryButton,
                _localMiningButton, _r149LandWorkButton, _r149PortWorkButton })
            {
                var row = button!.GetParent();
                button.Reparent(orderPanel);
                orderPanel.MoveChild(button, orderPanel.GetChildCount() - 1);
                if (row.GetChildCount() == 0 && row is Control emptyRow) emptyRow.Visible = false;
            }
            _localWorkerSelector.FitToLongestItem = false;
            // One selected-worker slot, one set of real command controls.
            // Keep identity and roster selector fixed; actions/details scroll.
            _localWorkerSelector.Reparent(_selectedUnitSlot!);
            _localWorkerSelector.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            operationsBody.Visible = true;
            // Existing controls stay accessible without a second click.
            // The body scrolls independently and the fixed header leaves
            // the lower dock available for the future auxiliary planet.
            if (_persistentQueuePanel is not null)
            {
                // Put frequently used move/production/crop controls before
                // the detailed queue; retain all existing widgets/callbacks.
                if (_cropSelector is not null)
                {
                    _cropSelector.Reparent(orderPanel);
                    orderPanel.MoveChild(_cropSelector, 0);
                }
                var actions = new VBoxContainer { Name = "GANextSelectedUnitActions" };
                orderPanel.AddChild(actions);
                orderPanel.MoveChild(actions, 1);
                foreach (var button in new[] { _localMoveButton, _localProduceButton,
                    _localAgricultureButton, _localForestryButton, _localMiningButton,
                    _r149LandWorkButton, _r149PortWorkButton, _localCancelQueuedButton })
                    if (button is not null) button.Reparent(actions);
            }
            ConfigureR225WorkerDock(orderPanel);
            RefreshSelectedUnitSlot();
            if (LayeredWorldDock)
            {
                CreateLayeredWorldDock(operationsColumn, operationsScroll, operationsBody,
                    orderPanel, cityNavigation!, civilizationNavigation!, globe);
            }
            else if (globe.ReadSignedPhysicalSurface() is { } surface)
            {
                operationsColumn.AddChild(new HSeparator());
                operationsColumn.AddChild(new Label { Text = "PLANET | MAP FOCUS" });
                _auxiliaryPlanet = new GaAuxiliaryPlanet(surface.Mesh, surface.Material);
                operationsColumn.AddChild(_auxiliaryPlanet);
                RefreshAuxiliaryPlanet();
            }
        }

        // Secondary planetary atlas is a bounded, movable and resizable
        // internal window. Outside the atlas, the tactical map still gets
        // pointer events (unlike the old screen-sized modal veil).
        var planet = new Control
        {
            Name = "GANextPlanetWindow",
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(planet);
        planet.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var planetPanel = new GaFloatingWindow
        {
            Name = "GANextPlanetPanel",
            Position = new Vector2(190f, 75f),
            Size = new Vector2(740f, 550f),
            MinimumFloatingSize = new Vector2(440f, 360f),
            MouseFilter = MouseFilterEnum.Stop
        };
        planet.AddChild(planetPanel);
        planetPanel.ClampIntoParent();
        _atlasFloatingWindow = planetPanel;
        planetPanel.AddThemeStyleboxOverride("panel", GaVisualFoundation.WindowSurface());
        var planetColumn = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        planetPanel.AddChild(planetColumn);
        var planetTitle = GaVisualFoundation.AddWindowHeader(
            planetColumn, "GA / PLANETARY ATLAS", "Strategic navigation",
            "Drag title to move · resize from lower-right corner",
            ClosePlanetWindow);
        planetPanel.AttachTitleBar(planetTitle);
        var planetSlot = new AspectRatioContainer
        {
            Ratio = 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        planetColumn.AddChild(planetSlot);
        var gripRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        planetColumn.AddChild(gripRow);
        gripRow.AddChild(GaVisualFoundation.Caption("M / ATLAS  •  navigation only"));
        var gripSpacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        gripRow.AddChild(gripSpacer);
        var grip = new Button
        {
            Name = "GANextPlanetResizeGrip",
            Text = "◢",
            TooltipText = "Drag to resize planetary window",
            CustomMinimumSize = new Vector2(30f, 25f)
        };
        gripRow.AddChild(grip);
        planetPanel.AttachResizeGrip(grip);
        globe.Reparent(planetSlot);
        globe.Visible = false;
        _planetWindow = planet;
        GD.Print("GA_NEXT_TACTICAL_WORKSPACE_WINDOWS_READY " +
            "planet=M movable_resizable=true dock=TAB close=ESC " +
            "native_windows=false simulation_mutation=false");
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
        // R139: M is a two-way exact geographic handoff, not a camera reset.
        // Preserve each viewer's independent zoom and never touch simulation.
        if (open)
        {
            _atlasFloatingWindow?.ClampIntoParent();
            if (_tacticalPreview is not null && _tacticalPreview.IsCartographicSiteMode)
                _strategicGlobe.CenterOnGeography(_tacticalPreview.CenterGeography);
        }
        else if (_tacticalPreview is not null && _tacticalPreview.IsCartographicSiteMode)
            _tacticalPreview.CenterOnGeography(_strategicGlobe.CenterGeography);
        _planetWindow.Visible = open;
        _strategicGlobe.Visible = open;
        GD.Print($"GA_NEXT_PLANET_WINDOW visible={open.ToString().ToLowerInvariant()} " +
            "navigation_only=true worker_move=false");
    }

    // R139 regression: exercise the SAME M-window handoff used by the player.
    // No authoritative turn, selection or workforce mutation is permitted.
    internal void ExerciseGeographicHandoffSmoke()
    {
        if (_planetWindow is null || _strategicGlobe is null ||
            _tacticalPreview is null || !_tacticalPreview.IsCartographicSiteMode)
            throw new System.InvalidOperationException("Cartographic handoff scene unavailable.");
        if (_planetWindow.Visible) TogglePlanetWindow();
        var before = _tacticalPreview.CenterGeography;
        var tacticalZoom = _tacticalPreview.TacticalCameraState.Zoom;
        TogglePlanetWindow();
        if (!_planetWindow.Visible ||
            before.Dot(_strategicGlobe.CenterGeography) < 0.99999f)
            throw new System.InvalidOperationException("M did not open globe at tactical geography.");
        // Simulate planetary navigation to an arbitrary non-cell-center position.
        var target = (before + new Vector3(0.03f, 0.045f, -0.017f)).Normalized();
        _strategicGlobe.CenterOnGeography(target);
        var globeCenter = _strategicGlobe.CenterGeography;
        TogglePlanetWindow();
        if (_planetWindow.Visible ||
            globeCenter.Dot(_tacticalPreview.CenterGeography) < 0.99999f ||
            Math.Abs(_tacticalPreview.TacticalCameraState.Zoom - tacticalZoom) > 0.00001f)
            throw new System.InvalidOperationException("M return lost geography or tactical zoom.");
        GD.Print("GA_NEXT_R139_M_ROUNDTRIP_PASS " +
            "tactical_to_globe=true globe_to_tactical=true exact_geography=true " +
            "tactical_zoom_preserved=true simulation_unchanged=true");
    }

    private void SetOperationalDockVisible(bool visible)
    {
        if (_workspaceWindow is null) return;
        _workspaceWindow.Visible = visible;
        // Godot's HBoxContainer will reclaim/release width without modifying
        // the tactical camera target, yaw or orthographic zoom.
        GD.Print($"GA_NEXT_OPERATIONS_DOCK visible={visible.ToString().ToLowerInvariant()} " +
            "map_reflow=true camera_state_preserved=true");
    }

    private void ToggleWorkspaceWindow()
    {
        if (_workspaceWindow is null) return;
        SetOperationalDockVisible(!_workspaceWindow.Visible);
    }

    public override void _Ready()
    {
        // Explicit, isolated lab. Ordinary games never import QA balance.
        // Unlike the CI smoke this mode remains open for manual E turns.
        _interactiveEconomicPreviewActive =
            System.Environment.GetEnvironmentVariable(
                "GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW") == "1";
        // R104: one immutable pre-scene source of the world request and
        // coarse-to-fine scale. Neither renderer chooses an independent grid.
        _resolutionBootstrapSmoke =
            System.Environment.GetEnvironmentVariable(
                "GA_NEXT_GOLDBERG_RESOLUTION_SMOKE") == "1";
        _goldbergStartup = GaGoldbergStartupConfiguration.FromEnvironment(
            _resolutionBootstrapSmoke,
            System.Environment.GetEnvironmentVariable(
                "GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW") == "1");
        GD.Print($"GA_NEXT_R102_BOOTSTRAP_CONFIGURATION " +
            $"coarse=G({_goldbergStartup.Coarse.M},0) " +
            $"fine=G({_goldbergStartup.Fine.M},0) " +
            "source=pre_scene strict_profiles=true");
        GD.Print("GA_NEXT_R104_SINGLE_BOOTSTRAP_READY " +
            $"coarse=G({_goldbergStartup.Coarse.M},0) " +
            $"fine=G({_goldbergStartup.Fine.M},0) " +
            $"scale={_goldbergStartup.Refinement.Scale} " +
            "source=immutable_request shared=true");
        GD.Print("GA_PROD_MAIN_READY_R6");
        // Centralized production-oriented styling; Godot Theme is the owner
        // of interactive states rather than per-screen debug colors.
        Theme = GaVisualFoundation.BuildTheme();
        // Prioritize a maximized production window; preserve windowed resizing and avoid headless changes.
        if (DisplayServer.GetName() != "headless")
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);
        var backdrop = new ColorRect { Color = GaVisualFoundation.Background, MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeConstantOverride("margin_left", 12);
        frame.AddThemeConstantOverride("margin_right", 12);
        frame.AddThemeConstantOverride("margin_top", 8);
        frame.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(frame);

        _layoutDiagnosticFrame = frame;
        var root = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _layoutDiagnosticRoot = root;
        root.AddThemeConstantOverride("separation", 7);
        frame.AddChild(root);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        header.AddChild(new ColorRect
        {
            Color = GaVisualFoundation.Accent,
            CustomMinimumSize = new Vector2(3, 32),
            MouseFilter = MouseFilterEnum.Ignore
        });
        root.AddChild(header);
        var title = new Label { Text = "GLOBAL ARENA" };
        title.AddThemeFontSizeOverride("font_size", GaVisualFoundation.ApplicationFontSize);
        title.AddThemeColorOverride("font_color", GaVisualFoundation.Text);
        header.AddChild(title);
        var subtitle = new Label { Text = "Planetary strategy  |  Production foundation", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        subtitle.AddThemeColorOverride("font_color", new Color("#7eabc4"));
        header.AddChild(subtitle);
        var button = new Button { Text = "SYSTEM STATUS" };
        button.Pressed += ShowStatus;
        header.AddChild(button);

        // Disclose provisional balancing while observing the actual market.
        CreateInteractiveEconomyPreviewBanner(root);

        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        root.AddChild(content);

        // Production-ready shell: the TACTICAL viewport owns the full
        // available landscape rectangle, never an AspectRatioContainer 1:1.
        // The spherical planetary globe can keep a square framing inside
        // its separate modal; that does not constrain the main gameplay.
        var mapRegion = new PanelContainer
        {
            Name = "GANextWideTacticalCanvas",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mapRegion.AddThemeStyleboxOverride("panel", GaVisualFoundation.MapFrame());
        content.AddChild(mapRegion);
        var globe = new GlobePreview
        {
            StartupWorldRequest = _goldbergStartup.WorldRequest,
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
        CreateR218SiteHover();
        _tacticalPreview.PhysicalTileInspected += OnPhysicalTileInspected;
        _tacticalPreview.CanonicalPhysicalTileClicked += OnTacticalPhysicalTileClicked;
        _tacticalPreview.ExplicitMapMoveRequested += OnR226MapMoveRequested;
        _tacticalPreview.MapConnectionRequested += OnR227MapConnectionRequested;
        _tacticalPreview.CartographicLocalSiteSelected += OnCartographicLocalSiteSelected;
        _tacticalPreview.CanonicalWorkerBadgeClicked += OnCanonicalWorkerBadgeClicked;
        _tacticalPreview.WorkerBoardOccupationRequested += OnWorkerBoardOccupationRequested;
        _tacticalPreview.WorkerBoardCancelSelectedOrderRequested +=
            OnWorkerBoardCancelSelectedOrderRequested;
        // The Foundation gameplay host materializes a *labeled local preview*
        // scenario. Main/Godot never creates, moves or settles workers directly.
        // This is NOT a claim of final world-population generation.
        _localFoundation = FoundationHumanAiLocalPreview.CreateLocalPreview(
            _goldbergStartup.WorldRequest);
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
        var panelTitle = new Label { Text = "TILE & TERRITORY" };
        panelTitle.AddThemeFontSizeOverride("font_size", 14);
        rightLayout.AddChild(panelTitle);
        var tacticalBridgeTitle = new Label { Text = "SELECTED TILE" };
        tacticalBridgeTitle.AddThemeFontSizeOverride("font_size", 13);
        rightLayout.AddChild(tacticalBridgeTitle);
        _tacticalBridgeStatus = new Label { Text = "Select a canonical territory to enable tactical context" };
        _tacticalBridgeStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rightLayout.AddChild(_tacticalBridgeStatus);
        CreateLocalSiteSelectionQaControls(rightLayout);
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
        _tacticalSettledTradeRoutes = new CheckBox
        {
            Name = "GANextSettledTradeRoutesToggle",
            Text = "LAST SETTLED TRADE ROUTES",
            ButtonPressed = true,
            Visible = false
        };
        _tacticalSettledTradeRoutes.Toggled += enabled =>
        {
            _tacticalPreview?.SetSettledTradeRoutesVisible(enabled);
            _strategicGlobe?.SetSphericalSettledTradeRoutesVisible(enabled);
        };
        rightLayout.AddChild(_tacticalSettledTradeRoutes);
        // All sidebar details now belong to the single bounded outer dock
        // scroll region; this VBox must NOT create a nested vertical viewport.
        var detailSections = new VBoxContainer
        {
            Name = "GANextOperationsDetailSections",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        rightLayout.AddChild(detailSections);
        // Tactical/strategic readouts still switch by context, not by hover.
        var hud = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        hud.AddThemeConstantOverride("separation", 8);
        detailSections.AddChild(hud);
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
        var orderTitle = new Label { Text = "WORKER ORDERS" };
        orderTitle.AddThemeFontSizeOverride("font_size", 13);
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
        CreatePersistentWorkerControls(orderSection);
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
        var occupationRow = new HBoxContainer();
        occupationRow.AddThemeConstantOverride("separation", 4);
        orderSection.AddChild(occupationRow);
        _localAgricultureButton = new Button {
            Text = "A | AGRI", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false, Disabled = true
        };
        _localAgricultureButton.Pressed += () =>
            QueueFineWorkerOccupation(PhysicalWorkerOccupation.Agriculture);
        occupationRow.AddChild(_localAgricultureButton);
        _localForestryButton = new Button {
            Text = "V | VEGETAL", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false, Disabled = true
        };
        _localForestryButton.Pressed += () =>
            QueueFineWorkerOccupation(PhysicalWorkerOccupation.Forestry);
        occupationRow.AddChild(_localForestryButton);
        _localMiningButton = new Button {
            Text = "N | MINERAL", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false, Disabled = true
        };
        _localMiningButton.Pressed += () =>
            QueueFineWorkerOccupation(PhysicalWorkerOccupation.Mining);
        occupationRow.AddChild(_localMiningButton);
        var logisticsRow = new HBoxContainer();
        logisticsRow.AddThemeConstantOverride("separation", 4);
        orderSection.AddChild(logisticsRow);
        _r149LandWorkButton = new Button
        {
            Text = "LAND | ASSIGN TRANSPORT",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false, Disabled = true
        };
        _r149LandWorkButton.Pressed += () =>
            QueueFineWorkerOperationalRole(FoundationOperationalWorkKind.Land);
        logisticsRow.AddChild(_r149LandWorkButton);
        _r149PortWorkButton = new Button
        {
            Text = "PORT | ASSIGN OPERATIONS",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false, Disabled = true
        };
        _r149PortWorkButton.Pressed += () =>
            QueueFineWorkerOperationalRole(FoundationOperationalWorkKind.Port);
        logisticsRow.AddChild(_r149PortWorkButton);
        _localPendingLabel = new Label
        {
            Text = "No orders queued",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_localPendingLabel);
        _fineMovementDecision = new Label
        {
            Name = "GANextFineMovementDecision",
            Text = "MOVEMENT | Select a worker and a destination tile",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_fineMovementDecision);
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
            Name = "GANextOrderFeedback",
            Text = "Select a worker and tile, queue orders, then press E to end turn",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_localOrderStatus);
        _lastTemporalEconomyLabel = new Label
        {
            Name = "GANextTemporalEconomicClose",
            Text = "",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        orderSection.AddChild(_lastTemporalEconomyLabel);
        CreatePortInvestmentQaControls(orderSection);
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
        _status.AddThemeColorOverride("font_color", GaVisualFoundation.Muted);
        _status.AddThemeFontSizeOverride("font_size", 11);
        root.AddChild(_status);
        GD.Print("GA_UI_FOUNDATION_READY_01 layout=map,sidebar,header,footer");
        GD.Print("GA_UI_FOUNDATION_RESIZE_READY_02 responsive=true");

        // Preserve PR #361 historical strategic-order E2E as an independent
        // regression fixture. Ordinary play opens the tactical viewport first.
        bool legacySmoke = System.Environment.GetEnvironmentVariable(
            "GA_FOUNDATION_GODOT_SMOKE") == "1";
        if (!legacySmoke)
        {
            // Preserve old v9 fullscreen/shortcut smoke without changing
            // its journal or assertions. Ordinary play uses the explicit
            // fine v10 demo with a real collective human+AI host.
            bool legacyWorkspaceSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_WORKSPACE_SMOKE") == "1";
            // Keep the v10 graphical E2E as a standalone compatibility
            // fixture; ordinary play and the new v11 E2E opt into an
            // explicit, six-worker occupation migration.
            bool legacyV10Smoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_TACTICAL_V10_SMOKE") == "1";
            bool legacyV11Smoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_TACTICAL_V11_SMOKE") == "1";
            bool concentratedSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_CONCENTRATED_WORKFORCE_SMOKE") == "1";
            bool tileClickMenuSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_TILE_CLICK_MENU_SMOKE") == "1";
            bool workerBoardSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_WORKER_BOARD_SMOKE") == "1";
            bool settledTradeQaSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_SETTLED_TRADE_VISUAL_SMOKE") == "1";
            bool fineFirstVisualSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_FINE_FIRST_VISUAL_SMOKE") == "1";
            bool temporalEconomyVisualSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_R179_TEMPORAL_ECONOMY_SMOKE") == "1";
            bool cartographicSiteSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_CARTOGRAPHIC_SITE_SMOKE") == "1";
            bool fineFirstInteractive = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_FINE_FIRST_INTERACTIVE_PREVIEW") == "1";
            bool constructionPreview = System.Environment.GetEnvironmentVariable("GA_NEXT_CONSTRUCTION_PREVIEW") == "1";
            bool localEconomyPreview = System.Environment.GetEnvironmentVariable("GA_NEXT_LOCAL_ECONOMY_PREVIEW") == "1";
            if (localEconomyPreview && (!fineFirstInteractive || !_interactiveEconomicPreviewActive || !constructionPreview ||
                System.Environment.GetEnvironmentVariable("GA_NEXT_PERSISTENT_ORDERS") != "1" ||
                System.Environment.GetEnvironmentVariable("GA_NEXT_CROP_PREVIEW") != "1"))
                throw new InvalidOperationException("Local economy preview requires explicit signed interactive economy, construction, orders and crops.");
            if (constructionPreview && (!fineFirstInteractive || !_interactiveEconomicPreviewActive))
                throw new InvalidOperationException("Construction preview requires the signed interactive economy lab.");
            bool fineFirstLab = fineFirstVisualSmoke || fineFirstInteractive ||
                temporalEconomyVisualSmoke || _resolutionBootstrapSmoke;
            bool continuousNavigationSmoke = System.Environment.GetEnvironmentVariable(
                "GA_NEXT_CONTINUOUS_NAVIGATION_SMOKE") == "1";
            if (cartographicSiteSmoke &&
                (!fineFirstInteractive ||
                 System.Environment.GetEnvironmentVariable(
                     "GA_NEXT_QA_LOCAL_SITE_MARKERS") != "1"))
                throw new InvalidOperationException(
                    "Cartographic QA smoke requires signed G18 and explicit site markers.");
            bool useExplicitEconomicLab = settledTradeQaSmoke ||
                _interactiveEconomicPreviewActive || fineFirstLab;
            if ((useExplicitEconomicLab &&
                 (legacyV10Smoke || legacyV11Smoke || concentratedSmoke ||
                  tileClickMenuSmoke || workerBoardSmoke || legacyWorkspaceSmoke ||
                  legacySmoke || _configuredEconomicGameplay is not null)) ||
                (settledTradeQaSmoke && _interactiveEconomicPreviewActive) ||
                // The explicit interactive EconomyLab may share the
                // fine-first G18 QA world: both read the same signed physical
                // geography and settled treasury, with no parallel worldgen.
                // Keep automated smoke, resolution probes and configured
                // production gameplay mutually exclusive.
                (fineFirstLab &&
                 (settledTradeQaSmoke ||
                  (_interactiveEconomicPreviewActive &&
                   !fineFirstInteractive))) ||
                (fineFirstVisualSmoke && fineFirstInteractive) ||
                (temporalEconomyVisualSmoke &&
                 (fineFirstVisualSmoke || fineFirstInteractive ||
                  _resolutionBootstrapSmoke || cartographicSiteSmoke)) ||
                (_resolutionBootstrapSmoke &&
                    (fineFirstVisualSmoke || fineFirstInteractive)))
                throw new InvalidOperationException(
                    "Experimental market must not replace normal gameplay, " +
                    "another test, or a trusted economic configuration.");
            if (!legacyWorkspaceSmoke)
            {
                var request = _goldbergStartup.WorldRequest;
                _tacticalHost = legacyV10Smoke
                    ? FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(request)
                    : legacyV11Smoke
                        ? FoundationTacticalDemoScenario
                            .CreateLocalHumanAiOccupationPreview(request)
                        : FoundationConcentratedWorkforceScenario.CreatePreview(
                            request, useExplicitEconomicLab ? 3 :
                                FoundationConcentratedWorkforceScenario
                                    .DefaultWorkersPerCivilization,
                            _goldbergStartup.Refinement);
                if (fineFirstLab)
                {
                    _goldbergStartup.VerifyWorldAndIncidence(
                        _tacticalHost.Session.GeneratedWorld,
                        _tacticalHost.Incidence);
                    var g18 = GaExplicitEconomicQaConfiguration
                        .PrepareFineFirst(_tacticalHost,
                            enableTemporalFinancialVisualSmoke:
                                temporalEconomyVisualSmoke, enableConstructionPreview: constructionPreview,
                            enablePersistentOrders: constructionPreview && System.Environment.GetEnvironmentVariable("GA_NEXT_PERSISTENT_ORDERS") == "1",
                            enableCropPreview: System.Environment.GetEnvironmentVariable("GA_NEXT_CROP_PREVIEW") == "1",
                            enableLocalEconomy: localEconomyPreview);
                    _tacticalHost = g18.Host;
                    _r123FineGeography = g18.Geography;
                    _goldbergStartup.VerifyWorldAndIncidence(
                        _tacticalHost.Session.GeneratedWorld,
                        _tacticalHost.Incidence);
                    // R99: the strategic navigation sphere displays the
                    // exact same signed G18 geography, without flattening.
                    // Keep G3 identity only as an invisible selection index.
                    globe.ConfigureSignedFineFirstSphere(
                        _tacticalHost.Session.GeneratedWorld,
                        _tacticalHost.Incidence, g18.Geography,
                        _tacticalHost.Snapshot.WorldBinding ??
                            throw new System.InvalidOperationException(
                                "G18 globe requires a signed world binding."));
                    _tacticalPreview!.ConfigureFineFirstEconomicTerrain(
                        _tacticalHost.Session.GeneratedWorld,
                        _tacticalHost.Incidence, g18.Geography, g18.Identity,
                        _tacticalHost.Snapshot);
                    if (globe.PresentedCanonicalWorld?.Request.Seed !=
                            _goldbergStartup.WorldRequest.Seed ||
                        globe.PresentedCanonicalWorld?.Request.StrategicParameters !=
                            _goldbergStartup.Coarse ||
                        g18.Geography.FineParameters != _goldbergStartup.Fine)
                        throw new InvalidOperationException(
                            "Signed physical sphere and economic world must share startup request.");
                    GD.Print("GA_NEXT_R104_SIGNED_WORLD_CONTRACT_PASS " +
                        $"coarse=G({_goldbergStartup.Coarse.M},0) " +
                        $"fine=G({_goldbergStartup.Fine.M},0) " +
                        $"scale={_goldbergStartup.Refinement.Scale} " +
                        "sphere=true tactical=true economy=true");
                }
                else
                {
                    if (useExplicitEconomicLab)
                        _tacticalHost = GaExplicitEconomicQaConfiguration
                            .Prepare(_tacticalHost);
                    else if (_configuredEconomicGameplay is not null)
                        _tacticalHost = FoundationGameplayEconomicBootstrap.Enable(
                            _tacticalHost, _configuredEconomicGameplay);
                    _tacticalPreview!.ConfigureAuthoritativeEconomyTerrain(
                        useExplicitEconomicLab || _configuredEconomicGameplay is not null
                            ? _tacticalHost.Session.GeneratedWorld : null);
                }
                _goldbergStartup.VerifyWorldAndIncidence(
                    _tacticalHost.Session.GeneratedWorld,
                    _tacticalHost.Incidence);
                _canonicalSession = _tacticalHost.Session;
                AttachRuntimeState(_tacticalHost.Snapshot);
            }
            // The old v9 record locates workers on G(3,0). This is a
            // PRESENTATION REGION ANCHOR, not a fabricated G(18,0) worker tile.
            // Tactical movement remains locked until the typed-v10 migration.
            var first = _tacticalHost is null
                ? _localFoundation!.ControllableWorkers.FirstOrDefault()
                : _tacticalHost.Snapshot.Workers.Workers.FirstOrDefault(
                    w => w.Owner == _humanCivilization);
            if (first is null)
                throw new System.InvalidOperationException(
                    "No local physical worker available for tactical entry.");
            // Prefer the physical home tile's actual coarse incidence as
            // the initial chart, rather than assuming the worker's legacy
            // strategic CellId is the center of its fine location.
            PhysicalTacticalTileId? openingHome = null;
            if (_tacticalHost?.Snapshot.Workers.HasAuthoritativeTacticalLocations == true &&
                first.TacticalTileId.HasValue)
            {
                openingHome = first.TacticalTileId.Value;
                var fineTile = openingHome.Value.FineStrategicCellId.Value;
                _bridgeSelection = _tacticalHost.Incidence.TileIncidences[
                    checked((int)fineTile - 1)].IncidentCoarseCellIds
                    .OrderBy(id => id.Value).First().Value;
            }
            else _bridgeSelection = first.CellId.Value;
            _openTacticalContext!.Disabled = false;
            // The city-centric chart is the cartographic UX pilot.
            // Legacy navigation/E2E retains its established coarse chart
            // until the new handoff policy is validated separately.
            _initialTacticalHomeAnchor =
                _tacticalPreview?.IsCartographicSiteMode == true
                    ? openingHome : null;
            OpenTacticalContext();
            _initialTacticalHomeAnchor = null;
            // City-centered first frame is opt-in to the cartographic map.
            // Legacy worker/navigation demos retain their proven tactical
            // region opening until that interface adopts named localities.
            if (openingHome.HasValue &&
                _tacticalPreview?.IsCartographicSiteMode == true)
                _tacticalPreview.FocusInitialPhysicalCity(openingHome.Value);
            CreateTacticalWorkspaceWindows(globe, rightPanel, header, content);
            CreateCityWindow();
            CreateCivilizationWindow();
            // Keep the 1280x720 header usable: the long subtitle belongs in
            // secondary information, not beside all keyboard-first controls.
            subtitle.Visible = false;
            _returnStrategicContext!.Text = "M | OPEN PLANETARY MAP";
            if (_tacticalHost is null)
            {
                // Legacy v9-only workspace E2E, intentionally locked.
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
            }
            else
            {
                _tacticalPreview!.PresentAuthoritativeWorkers(
                    _tacticalHost.Snapshot.Workers, first.Id.Value);
                if (!legacyV10Smoke && !legacyV11Smoke && !concentratedSmoke)
                    _tacticalPreview.EnableTileClickMenuHypothesis(_humanCivilization);
                _localMoveButton!.Text = "G | MOVE ONE FINE EDGE";
                _localProduceButton!.Text = "P | RESUME PRODUCTION";
                _localCancelQueuedButton!.Text = "X | CANCEL ORDER";
                _localOrderStatus!.Text =
                    _tacticalHost.PersistentOrdersEnabled ? "Select a worker and a destination. G adds movement; A/V/N adds occupation; X removes or interrupts the head; E submits changes." :
                    $"Click your worker, select an adjacent G({_goldbergStartup.Fine.M},{_goldbergStartup.Fine.N}) tile, then G. " +
                    "LAND/PORT assigns logistic work; A/V/N selects or resumes " +
                    "production; X cancels; E commits the collective turn.";
                GD.Print("GA_NEXT_FINE_VISUAL_BRIDGE_READY " +
                    $"world=v{_tacticalHost.StateHash.FormatVersion} worker_ids={_tacticalHost.Snapshot.Workers.Workers.Count} physical_tiles=G({_goldbergStartup.Fine.M},{_goldbergStartup.Fine.N}) " +
                    $"inputs=G/P/X/E gameplay_economy={(_tacticalHost.StateHash.FormatVersion >= 13U ? "explicit_v15" : "not_active")}");
            }
            _status!.Text = _interactiveEconomicPreviewActive
                ? (System.Environment.GetEnvironmentVariable(
                    "GA_NEXT_QA_SHALLOW_PORTS") == "1"
                    ? "GA-NEXT / SHALLOW PORT A/B • QA ONLY • E turn • M map"
                    : "GA-NEXT / ECONOMY BASELINE • QA ONLY • E turn • M map")
                : _tacticalHost is null
                    ? "GA-NEXT / WORKFORCE 01  •  TAB personnel  •  M planetary atlas  •  E collective turn"
                    : $"GA-NEXT / TACTICAL V{_tacticalHost.StateHash.FormatVersion}  •  G step  •  P produce  •  X cancel  •  E collective";
            GD.Print("GA_VISUAL_FOUNDATION_01_READY tactical_rectangular=true theme_tokens=true " +
                "windows=chrome native_windows=false game_authority=untouched");
            RefreshLocalWorkerOrderControls();
            RefreshInteractiveEconomyPreviewBanner();
            if (fineFirstInteractive)
            {
                var polarGeometry = _tacticalHost!.Session.GeneratedWorld
                    .StrategicSphericalGeometry;
                ulong northId = polarGeometry.Cells
                    .OrderByDescending(cell => cell.Center.Z)
                    .First().CellId.Value;
                ulong southId = polarGeometry.Cells
                    .OrderBy(cell => cell.Center.Z)
                    .First().CellId.Value;
                void AddPolarButton(string label, ulong focusId)
                {
                    var polarButton = new Button { Text = label };
                    polarButton.Pressed += () =>
                    {
                        _bridgeSelection = focusId;
                        OpenTacticalContext();
                        GD.Print($"GA_NEXT_R95_MANUAL_POLAR_FOCUS " +
                            $"focus={focusId} economic_hash_unchanged=true");
                    };
                    header.AddChild(polarButton);
                }
                AddPolarButton("NORTH POLE", northId);
                AddPolarButton("SOUTH POLE", southId);
                GD.Print("GA_NEXT_R95_MANUAL_POLAR_CONTROLS_READY " +
                    $"north={northId} south={southId} " +
                    "manual_navigation_only=true no_autoplay=true");
                GD.Print("GA_NEXT_R87_FINE_FIRST_INTERACTIVE_READY " +
                    "source=signed_g18 physical_freight_same_field=true " +
                    "mode=manual_e_turns auto_quit=false qa_only=true");
            }
            if (_interactiveEconomicPreviewActive)
                GD.Print("GA_NEXT_R120_EXPERIMENTAL_COASTAL_PROFILE " +
                    $"shallow={(System.Environment.GetEnvironmentVariable("GA_NEXT_QA_SHALLOW_PORTS") == "1").ToString().ToLowerInvariant()} " +
                    "physical_source=signed_fine_first_g18");
            if (_interactiveEconomicPreviewActive)
                GD.Print("GA_NEXT_INTERACTIVE_ECONOMY_PREVIEW_READY " +
                    "mode=manual_e_turns auto_quit=false " +
                    "configuration=qa_only_not_approved opening_cash=zero " +
                    "source=real_v15_worker_market");
            GD.Print($"GA_NEXT_TACTICAL_ENTRY_PASS local_civ={_humanCivilization.Value} " +
                $"worker={first.Id.Value} coarse_preview_focus={first.CellId.Value} " +
                $"physical_worker_location_authoritative={(_tacticalHost is not null).ToString().ToLowerInvariant()} " +
                "tactical_visible=true");
            if (System.Environment.GetEnvironmentVariable("GA_NEXT_R213_UI_SMOKE") == "1")
                _ = ExerciseR213PersistentInterfaceSmoke();
            else if (legacyWorkspaceSmoke)
                _ = new FoundationGodotWorkspaceSmoke(this, _localFoundation!).RunAsync();
            if (legacyV10Smoke && _tacticalHost is not null)
                _ = new FoundationGodotTacticalV10Smoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (legacyV11Smoke && _tacticalHost is not null)
                _ = new FoundationGodotTacticalV10Smoke(
                    this, _tacticalHost, _tacticalPreview!,
                    expectedFormatVersion: 11U).RunAsync();
            else if (concentratedSmoke && _tacticalHost is not null)
                _ = new FoundationGodotConcentratedWorkforceSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (tileClickMenuSmoke && _tacticalHost is not null)
                _ = new FoundationGodotTileClickMenuSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (workerBoardSmoke && _tacticalHost is not null)
                _ = new FoundationGodotWorkerBoardSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (_resolutionBootstrapSmoke && _tacticalHost is not null)
                _ = new FoundationGodotGoldbergResolutionBootstrapSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (cartographicSiteSmoke && _tacticalHost is not null)
                _ = new FoundationGodotCartographicSiteSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (fineFirstVisualSmoke && _tacticalHost is not null)
                _ = new FoundationGodotFineFirstTerrainSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (temporalEconomyVisualSmoke && _tacticalHost is not null)
                _ = new FoundationGodotTemporalEconomySmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (settledTradeQaSmoke && _tacticalHost is not null)
                _ = new FoundationGodotSettledTradeSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
            else if (continuousNavigationSmoke && _tacticalHost is not null)
                _ = new FoundationGodotContinuousNavigationSmoke(
                    this, _tacticalHost, _tacticalPreview!).RunAsync();
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
        UpdateR218SiteHover(delta);
        RefreshAuxiliaryPlanet();
        if (_layoutDiagnosticFrame is null || _layoutDiagnosticRoot is null) return;
        var view = GetViewport();
        if (view is null) return;
        var current = view.GetVisibleRect().Size;
        var signature = $"{current}/{Size}/{_layoutDiagnosticFrame.Size}/{_layoutDiagnosticRoot.Size}/{DisplayServer.WindowGetSize()}";
        if (signature == _lastLoggedLayoutSignature) return;
        _lastLoggedLayoutSignature = signature;
        GD.Print($"GA_LAYOUT_DIAGNOSTIC_02 viewport={current} main={Size} frame={_layoutDiagnosticFrame.Size} root={_layoutDiagnosticRoot.Size} window={DisplayServer.WindowGetSize()} screen={DisplayServer.ScreenGetSize()}");
    }
    private void RefreshAuxiliaryPlanet()
    {
        if (_auxiliaryPlanet is null || _tacticalPreview?.IsCartographicSiteMode != true) return;
        _auxiliaryPlanet.Synchronize(_tacticalPreview.CenterGeography, _tacticalPreview.AuxiliaryNorthGeography);
    }
    private void ShowStatus()
    {
        if (_status is null) return;
        _status.Text = $"Godot {Engine.GetVersionInfo()["string"]} | .NET production shell | World assembly referenced";
    }
}