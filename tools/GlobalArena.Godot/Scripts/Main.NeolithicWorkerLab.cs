using System;
using System.IO;
using System.Linq;
using System.Text;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;
using Godot;

namespace GlobalArena.Godot;

/// <summary>
/// Neolithic control surface for the canonical physical economy in the real Godot shell.
/// All live actions commit immutable CanonicalGameSession.WorldState snapshots.
/// The renderer receives only physical tile IDs for visual highlights.
/// </summary>
public partial class Main
{
    // One authoritative source: CanonicalGameSession.WorldState.
    private CanonicalPhysicalTacticalSession? _canonicalPhysicalLab;
    private PhysicalTacticalTileId? _workerOrigin;
    private PhysicalTacticalTileId? _workerDestination;
    private Label? _workerLabStatus;
    private Label? _workerLabHover;
    private Label? _workerLabDetails;
    private Label? _settlementSummary;
    private Label? _economicCycleSummary;
    private Label? _workerDecisionPreview;
    private Label? _foodAvailabilityPreview;
    private Label? _adjacentSurveyPreview;
    private SpinBox? _remoteDestinationTileId;
    private Label? _remoteRoutePreview;
    private VBoxContainer? _workerLabAdvancedPanel;
    private Button? _workerMoveButton;
    private Button? _workerReassignButton;
    private SpinBox? _workerAmount;
    private OptionButton? _workerFromJob;
    private OptionButton? _workerToJob;
    private SpinBox? _referenceFoodBudget;
    private Label? _foodMarketQuoteLabel;
    private ulong? _foodMarketQuoteRevision;
    private ulong? _foodMarketQuoteTile;

    private void InitializeWorkerLabControls(VBoxContainer tacticalHud)
    {
        var lab = new VBoxContainer();
        lab.AddThemeConstantOverride("separation", 4);
        tacticalHud.AddChild(lab);

        var title = new Label
        {
            Text = "PLANEJAMENTO TÁTICO | VS29",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color("#d4e9f3"));
        lab.AddChild(title);

        var warning = new Label
        {
            Text = "O movimento primitivo atual usa trechos adjacentes; a colonização futura não terá limite artificial de distância.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        warning.AddThemeColorOverride("font_color", new Color("#e1c883"));
        lab.AddChild(warning);

        var start = new Button { Name = "GAQAInspect", Text = "INSPECIONAR POVOAMENTO INICIAL (JÁ EXISTENTE)" };
        start.Pressed += StartWorkerLab;
        lab.AddChild(start);

        _workerLabStatus = new Label
        {
            Name = "GAQAWorkerStatus",
            Text = "Povoamento já existente: Civilização 1 e oito trabalhadores desde o turno zero.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        lab.AddChild(_workerLabStatus);
        _settlementSummary = new Label
        {
            Name = "GAQASettlementSummary",
            Text = "Núcleos habitados: leitura automática do trabalho efetivo.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _settlementSummary.AddThemeColorOverride("font_color", new Color("#d9c27d"));
        lab.AddChild(_settlementSummary);

        var jobs = new HBoxContainer();
        lab.AddChild(jobs);
        _workerFromJob = new OptionButton { Name = "GAQAFromJob", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _workerToJob = new OptionButton { Name = "GAQAToJob", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var (name, id) in new[]
        {
            ("Agricultura", EconomicActivityKind.Agriculture),
            ("Pedra", EconomicActivityKind.Mining),
            ("Madeira", EconomicActivityKind.Forestry)
        })
        {
            _workerFromJob.AddItem(name, (int)id);
            _workerToJob.AddItem(name, (int)id);
        }
        _workerFromJob.Select(0);
        _workerToJob.Select(0);
        _workerFromJob.ItemSelected += _ => UpdateWorkerLabHud();
        _workerToJob.ItemSelected += _ => UpdateWorkerLabHud();
        jobs.AddChild(_workerFromJob);
        jobs.AddChild(_workerToJob);

        var quantity = new HBoxContainer();
        quantity.AddChild(new Label { Text = "Transferir:" });
        _workerAmount = new SpinBox { Name = "GAQAWorkerAmount", MinValue = 1, MaxValue = 8, Step = 1, Value = 3 };
        _workerAmount.ValueChanged += _ =>
        {
            if (_workerDecisionPreview is not null)
                _workerDecisionPreview.Visible = false;
        };
        quantity.AddChild(_workerAmount);
        lab.AddChild(quantity);

        _workerMoveButton = new Button
        {
            Name = "GAQAMoveWorkers",
            Text = "ORDENAR DESLOCAMENTO (2 TURNOS)",
            Disabled = true
        };
        _workerMoveButton.Pressed += MoveWorkerLabWorkers;
        lab.AddChild(_workerMoveButton);
        var travelPlanning = new HBoxContainer();
        travelPlanning.AddThemeConstantOverride("separation", 4);
        lab.AddChild(travelPlanning);
        var surveyNeighbours = new Button
        {
            Name = "GAQAAdjacentSurvey",
            Text = "EXPLORAR VIZINHOS",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        surveyNeighbours.Pressed += SurveyAdjacentPhysicalWork;
        travelPlanning.AddChild(surveyNeighbours);
        var selectHigherYield = new Button
        {
            Name = "GAQAPlanDestination",
            Text = "SELECIONAR MAIOR POTENCIAL",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        selectHigherYield.Pressed += SelectHighestYieldAdjacentDestination;
        travelPlanning.AddChild(selectHigherYield);
        _adjacentSurveyPreview = new Label
        {
            Name = "GAQAAdjacentSurveyResult",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _adjacentSurveyPreview.AddThemeColorOverride("font_color", new Color("#b5ddb5"));
        lab.AddChild(_adjacentSurveyPreview);
        _workerReassignButton = new Button
        {
            Name = "GAQAReassign",
            Text = "MUDAR ATIVIDADE NO TILE (SEM VIAGEM)",
            Disabled = true
        };
        _workerReassignButton.Pressed += ReassignWorkerLabWorkers;
        lab.AddChild(_workerReassignButton);
        // Two read-only decisions share a row, rather than crowding the HUD.
        var analysisActions = new HBoxContainer();
        analysisActions.AddThemeConstantOverride("separation", 4);
        lab.AddChild(analysisActions);
        var compareDecision = new Button
        {
            Name = "GAQACompare",
            Text = "COMPARAR (5 CICLOS)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        compareDecision.Pressed += CompareWorkerDecisionWithoutCommitting;
        analysisActions.AddChild(compareDecision);
        var foodDiagnosis = new Button
        {
            Name = "GAQAFoodDiagnosis",
            Text = "DIAGNÓSTICO ALIMENTAR",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        foodDiagnosis.Pressed += DiagnoseNextFoodCycleWithoutCommitting;
        analysisActions.AddChild(foodDiagnosis);
        _workerDecisionPreview = new Label
        {
            Name = "GAQAComparisonResult",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _workerDecisionPreview.AddThemeColorOverride("font_color", new Color("#e7d8ad"));
        lab.AddChild(_workerDecisionPreview);
        _foodAvailabilityPreview = new Label
        {
            Name = "GAQAFoodResult",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _foodAvailabilityPreview.AddThemeColorOverride("font_color", new Color("#e7d8ad"));
        lab.AddChild(_foodAvailabilityPreview);

        var nextTurn = new Button { Text = "AVANÇAR TURNO DE MOVIMENTO (CICLOS ADAPTATIVOS)" };
        nextTurn.Pressed += AdvanceOfflineLabTurn;
        lab.AddChild(nextTurn);
        var economicCycleActions = new HBoxContainer();
        economicCycleActions.AddThemeConstantOverride("separation", 4);
        lab.AddChild(economicCycleActions);
        var toNextCycle = new Button
        {
            Name = "GAQAAdvanceOneCycle",
            Text = "AVANÇAR 1 CICLO",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        toNextCycle.Pressed += AdvanceOfflineLabToNextEconomicCycle;
        economicCycleActions.AddChild(toNextCycle);
        var toFiveCycles = new Button
        {
            Name = "GAQAAdvanceFiveCycles",
            Text = "AVANÇAR 5 CICLOS",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        toFiveCycles.Pressed += AdvanceOfflineLabFiveEconomicCycles;
        economicCycleActions.AddChild(toFiveCycles);
        _economicCycleSummary = new Label
        {
            Name = "GAQAEconomicSummary",
            Text = "CICLO ECONÔMICO | aguardando inspeção do povoamento.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _economicCycleSummary.AddThemeColorOverride("font_color", new Color("#d4e9f3"));
        lab.AddChild(_economicCycleSummary);

        var persistence = new HBoxContainer();
        lab.AddChild(persistence);
        var save = new Button { Name = "GAQASave", Text = "SALVAR PARTIDA CANÔNICA", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        save.Pressed += SaveOfflineLab;
        persistence.AddChild(save);
        var load = new Button { Name = "GAQALoad", Text = "REABRIR SAVE", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        load.Pressed += LoadOfflineLab;
        persistence.AddChild(load);

        // VS19C: gameplay controls and save/reopen always precede telemetry.
        // Optional diagnostics and historical market quotation remain available
        // by explicit expansion, and start collapsed to maximize useful space.
        var diagnosticsToggle = new Button
        {
            Name = "GAQAAdvancedToggle",
            Text = "MOSTRAR DETALHES E FERRAMENTAS DE LABORATÓRIO",
            ToggleMode = true,
            ButtonPressed = false
        };
        lab.AddChild(diagnosticsToggle);
        _workerLabAdvancedPanel = new VBoxContainer
        {
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _workerLabAdvancedPanel.AddThemeConstantOverride("separation", 4);
        lab.AddChild(_workerLabAdvancedPanel);
        diagnosticsToggle.Toggled += expanded =>
        {
            if (_workerLabAdvancedPanel is not null)
                _workerLabAdvancedPanel.Visible = expanded;
            diagnosticsToggle.Text = expanded
                ? "OCULTAR DETALHES E FERRAMENTAS DE LABORATÓRIO"
                : "MOSTRAR DETALHES E FERRAMENTAS DE LABORATÓRIO";
        };
        // Optional long-distance itinerary (any physical tile ID). It never
        // changes the existing adjacent move button or issues a move order.
        var remoteRow = new HBoxContainer();
        remoteRow.AddThemeConstantOverride("separation", 4);
        _workerLabAdvancedPanel.AddChild(remoteRow);
        remoteRow.AddChild(new Label { Text = "Destino global (tile):" });
        _remoteDestinationTileId = new SpinBox
        {
            Name = "GAQARemoteDestination",
            MinValue = 1, MaxValue = 100000, Step = 1, Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        remoteRow.AddChild(_remoteDestinationTileId);
        var previewRemote = new Button
        {
            Name = "GAQAPreviewRemoteRoute",
            Text = "CALCULAR ROTA A QUALQUER TILE (SEM VIAJAR)"
        };
        previewRemote.Pressed += PreviewRemotePhysicalRoute;
        _workerLabAdvancedPanel.AddChild(previewRemote);
        var orderRemote = new Button
        {
            Name = "GAQAOrderRemoteJourney",
            Text = "ORDENAR VIAGEM LONGA ATÉ DESTINO FINAL"
        };
        orderRemote.Pressed += OrderRemotePhysicalJourney;
        _workerLabAdvancedPanel.AddChild(orderRemote);
        _remoteRoutePreview = new Label
        {
            Name = "GAQARemoteRouteResult",
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _remoteRoutePreview.AddThemeColorOverride("font_color", new Color("#b5ddb5"));
        _workerLabAdvancedPanel.AddChild(_remoteRoutePreview);

        _workerLabDetails = new Label
        {
            Text = "Detalhes técnicos disponíveis após a inspeção.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _workerLabAdvancedPanel.AddChild(_workerLabDetails);
        var legend = new Label
        {
            Text = "Mapa: ciano origem | laranja destino | verde vizinhos | marcadores âmbar em viagem.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _workerLabAdvancedPanel.AddChild(legend);
        _workerLabHover = new Label { Text = "Sob o cursor: —" };
        _workerLabAdvancedPanel.AddChild(_workerLabHover);

        var budgetRow = new HBoxContainer();
        budgetRow.AddChild(new Label { Text = "Orçamento hipotético / trabalhador:" });
        _referenceFoodBudget = new SpinBox
        {
            MinValue = 1, MaxValue = 100000, Step = 1, Value = 40,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        budgetRow.AddChild(_referenceFoodBudget);
        _workerLabAdvancedPanel.AddChild(budgetRow);
        var quoteButton = new Button { Text = "COTAR MERCADO ALIMENTAR LOCAL (SEM LIQUIDAR)" };
        quoteButton.Pressed += QuoteLocalFoodMarket;
        _workerLabAdvancedPanel.AddChild(quoteButton);
        _foodMarketQuoteLabel = new Label
        {
            Text = "Cotação hipotética: não movimenta caixa nem altera o mundo.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _workerLabAdvancedPanel.AddChild(_foodMarketQuoteLabel);

        if (_tacticalPreview is null)
            throw new InvalidOperationException("Tactical renderer must exist before lab HUD setup.");
        _tacticalPreview.WorkerTileHovered += id =>
        {
            if (_workerLabHover is not null)
                _workerLabHover.Text = _canonicalPhysicalLab is null || !id.HasValue
                    ? "Sob o cursor: —"
                    : $"Sob o cursor: tile físico {id.Value}";
        };
        _tacticalPreview.WorkerGroupPicked += (tile, activity) =>
        {
            if (_canonicalPhysicalLab is null || _workerFromJob is null) return;
            var index = _workerFromJob.GetItemIndex((int)activity);
            if (index < 0) return;
            _workerFromJob.Select(index);
            // Tile selection is already dispatched by the canonical tile picker.
            // Do not issue an order or mutate the world from the presentation.
            UpdateWorkerLabHud();
        };
    }

    private void StartWorkerLab()
    {
        if (_tacticalPreview is null || _workerLabStatus is null || _canonicalSession is null)
            return;
        // Re-entering the laboratory must never discard its canonical
        // journal, otherwise the next V9 save could not be replayed.
        if (_canonicalPhysicalLab is not null)
        {
            _workerOrigin ??= _canonicalPhysicalLab.State.Allocations.FirstOrDefault()?.TileId
                ?? _canonicalPhysicalLab.State.Transits.FirstOrDefault()?.Source
                ?? _canonicalPhysicalLab.InitialTile;
            // "Inspect" is a camera navigation action, never population creation.
            // Recenter only if the inhabited tile lies outside the current
            // tactical chart: preserve user pan and zoom when it is already visible.
            if (!_tacticalPreview.HasVisiblePhysicalTile(
                    _workerOrigin.Value.FineStrategicCellId.Value))
            {
                var focus = _canonicalPhysicalLab.DisplayBucket(_workerOrigin.Value);
                _tacticalPreview.BindEconomicTacticalGeography(
                    _canonicalPhysicalLab.FineWorld, _canonicalPhysicalLab.CanonicalTerrain);
                _tacticalPreview.Open(focus.Value);
            }
            UpdateWorkerLabHud();
            return;
        }
        try
        {
            // The game already owns G(3,0); its physical topology is G(18,0).
            // No offline worker session survives beyond canonical seeding.
            var next = new CanonicalPhysicalTacticalSession(
                _canonicalSession, new GoldbergParameters(18, 0),
                PhysicalEconomicCadenceMode.AdaptiveThirty,
                PhysicalTacticalGeographyMode.Canonical01C,
                PhysicalFoodMarketMode.LocalCashSettlement,
                PhysicalWorkerMovementMode.TimedAdjacent,
                PhysicalCivilizationIdentityMode.BornWithPopulation);
            var origin = next.State.Allocations.First().TileId;
            if (!next.IsPhysicalWorkableLand(origin.FineStrategicCellId))
                throw new InvalidOperationException("Origin is not on canonical 01C workable land.");
            var focus = next.DisplayBucket(origin);
            _tacticalPreview.BindEconomicTacticalGeography(next.FineWorld, next.CanonicalTerrain);
            _tacticalPreview.Open(focus.Value);
            if (!_tacticalPreview.HasVisiblePhysicalTile(origin.FineStrategicCellId.Value))
                throw new InvalidOperationException("Canonical worker tile is not visible.");
            _canonicalPhysicalLab = next;
            _workerOrigin = origin;
            _workerDestination = null;
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            GD.Print($"GA-VS10-CANONICAL-READY revision={_canonicalSession.Snapshot.Revision} " +
                $"source={origin.FineStrategicCellId.Value} workers={next.State.TotalWorkers} " +
                $"hash={next.CurrentHash.HexDigest} canonical_worldstate=true");
        }
        catch (Exception ex)
        {
            _canonicalPhysicalLab = null;
            _workerOrigin = null;
            _workerDestination = null;
            _tacticalPreview.ClearWorkerRings();
            _workerLabStatus.Text = "SESSÃO CANÔNICA NÃO INICIADA: " + ex.Message;
            GD.PushError("GA-VS10-CANONICAL-START-FAIL: " + ex);
        }
    }

    private void HandleWorkerLabSelection(ulong? clickedTile)
    {
        if (_canonicalPhysicalLab is null || _workerOrigin is null || _tacticalPreview is null)
            return;
        if (!clickedTile.HasValue)
        {
            _workerDestination = null;
            UpdateWorkerLabHud();
            return;
        }
        var id = new PhysicalTacticalTileId(
            _canonicalPhysicalLab.State.FineParameters,
            new StrategicCellId(clickedTile.Value));
        var occupied = _canonicalPhysicalLab.WorkersAt(id);
        if (occupied > 0)
        {
            _workerOrigin = id;
            _workerDestination = null;
            if (_workerFromJob is not null)
            {
                var activeJob = _canonicalPhysicalLab.State.Allocations
                    .Where(x => x.TileId == id)
                    .OrderBy(x => (byte)x.Activity)
                    .FirstOrDefault();
                if (activeJob is not null)
                {
                    var selectedJob = (EconomicActivityKind)_workerFromJob.GetSelectedId();
                    if (!_canonicalPhysicalLab.State.Allocations.Any(x =>
                        x.TileId == id && x.Activity == selectedJob))
                    {
                        var index = _workerFromJob.GetItemIndex((int)activeJob.Activity);
                        if (index >= 0) _workerFromJob.Select(index);
                    }
                }
            }
        }
        else if (_canonicalPhysicalLab.AdjacentWorkable(_workerOrigin.Value).Contains(id))
            _workerDestination = id;
        else
            _workerDestination = null;
        UpdateWorkerLabHud();
    }

    private void MoveWorkerLabWorkers()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _workerOrigin is null || _workerDestination is null ||
            _workerAmount is null || _workerFromJob is null || _workerToJob is null)
            return;
        try
        {
            if (_canonicalPhysicalLab.MovementMode == PhysicalWorkerMovementMode.TimedAdjacent)
                _canonicalPhysicalLab.OrderWorkerTransit(
                    _workerOrigin.Value, _workerDestination.Value,
                    (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                    (EconomicActivityKind)_workerToJob.GetSelectedId(),
                    checked((ulong)_workerAmount.Value));
            else
            {
                _canonicalPhysicalLab.MoveWorkers(
                    _workerOrigin.Value, _workerDestination.Value,
                    (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                    (EconomicActivityKind)_workerToJob.GetSelectedId(),
                    checked((ulong)_workerAmount.Value));
                _workerOrigin = _workerDestination;
            }
            _workerDestination = null;
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            GD.Print($"GA-VS10-CANONICAL-WORKERS-MOVED revision={_canonicalSession.Snapshot.Revision} " +
                $"workers={_canonicalPhysicalLab.State.TotalWorkers} " +
                $"occupied={_canonicalPhysicalLab.State.Allocations.Select(x => x.TileId).Distinct().Count()} " +
                $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            if (_workerLabStatus is not null)
                _workerLabStatus.Text = "TRANSFERÊNCIA RECUSADA (estado preservado): " + ex.Message;
        }
    }

    /// <summary>
    /// Set the ordinary destination selection from canonical geographic
    /// reconnaissance. Selecting does not create a journal action, move a
    /// worker or advance a turn; the existing move button remains the only
    /// way to commit a timed transit.
    /// </summary>
    private void SelectHighestYieldAdjacentDestination()
    {
        if (_canonicalPhysicalLab is null || _workerOrigin is null ||
            _workerFromJob is null || _workerToJob is null ||
            _workerAmount is null || _adjacentSurveyPreview is null)
            return;
        try
        {
            var beforeHash = _canonicalPhysicalLab.CurrentHash;
            var plan = PhysicalWorkerDestinationPlanner.PreviewHighestYield(
                _canonicalPhysicalLab, _workerOrigin.Value,
                (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                (EconomicActivityKind)_workerToJob.GetSelectedId(),
                checked((ulong)_workerAmount.Value));
            // The same destination field and rings are updated by ordinary
            // mouse selection; no alternative fast-path to OrderWorkerTransit.
            _workerDestination = plan.Destination;
            UpdateWorkerLabHud();
            if (!_canonicalPhysicalLab.CurrentHash.Equals(beforeHash))
                throw new InvalidOperationException(
                    "Destination selection unexpectedly changed the real game.");
            var delta = (plan.DestinationYieldPerWorkerRaw -
                plan.OriginYieldPerWorkerRaw) / 1_000_000.0;
            _adjacentSurveyPreview.Text =
                $"DESTINO PREPARADO: tile {plan.Destination.FineStrategicCellId.Value} " +
                $"({plan.DestinationJob}) | potencial de origem " +
                $"{plan.OriginYieldPerWorkerRaw / 1_000_000.0:F2} → " +
                $"{plan.DestinationYieldPerWorkerRaw / 1_000_000.0:F2} " +
                $"por trabalhador/ciclo (diferença {delta:+0.00;-0.00;0.00})\n" +
                $"Trabalhadores planejados {plan.Workers}; " +
                $"residentes no destino {plan.DestinationResidents}; " +
                $"em viagem ao destino {plan.DestinationTravellers}. " +
                "Maior potencial não garante maior lucro ou melhor estratégia. " +
                "NENHUMA VIAGEM INICIADA. Para confirmar use ORDENAR DESLOCAMENTO.";
            _adjacentSurveyPreview.Visible = true;
            GD.Print($"GA_VS27_DESTINATION_SELECTED origin=" +
                $"{plan.Origin.FineStrategicCellId.Value} " +
                $"destination={plan.Destination.FineStrategicCellId.Value} " +
                $"workers={plan.Workers} hash={beforeHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or OverflowException)
        {
            _adjacentSurveyPreview.Text =
                "Seleção de destino recusada (partida preservada): " + ex.Message;
            _adjacentSurveyPreview.Visible = true;
        }
    }

    private void OrderRemotePhysicalJourney()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _workerOrigin is null || _workerFromJob is null ||
            _workerToJob is null || _workerAmount is null ||
            _remoteDestinationTileId is null || _workerLabStatus is null)
            return;
        try
        {
            var target = new PhysicalTacticalTileId(
                _canonicalPhysicalLab.State.FineParameters,
                new StrategicCellId(checked((ulong)_remoteDestinationTileId.Value)));
            var trip = _canonicalPhysicalLab.OrderLongDistanceTransit(
                _workerOrigin.Value, target,
                (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                (EconomicActivityKind)_workerToJob.GetSelectedId(),
                checked((ulong)_workerAmount.Value));
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _workerLabStatus.Text +=
                $"\nORDEM GLOBAL ÚNICA {trip.OrderId}: " +
                $"{trip.TotalLegs} trechos, {trip.Workers} trabalhadores; " +
                $"chegada prevista turno {trip.ArrivalTurn}. " +
                "Intermediários são passagem, não assentamentos.";
            GD.Print($"GA_VS29_REMOTE_ORDER origin={trip.Source.FineStrategicCellId.Value} " +
                $"destination={trip.Destination.FineStrategicCellId.Value} " +
                $"legs={trip.TotalLegs} arrival={trip.ArrivalTurn} " +
                $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or OverflowException)
        {
            _workerLabStatus.Text +=
                "\nORDEM GLOBAL RECUSADA (partida preservada): " + ex.Message;
        }
    }

    private void PreviewRemotePhysicalRoute()
    {
        if (_canonicalPhysicalLab is null || _workerOrigin is null ||
            _workerFromJob is null || _workerToJob is null ||
            _workerAmount is null || _remoteDestinationTileId is null ||
            _remoteRoutePreview is null) return;
        try
        {
            var sourceHash = _canonicalPhysicalLab.CurrentHash;
            var targetId = checked((ulong)_remoteDestinationTileId.Value);
            var target = new PhysicalTacticalTileId(
                _canonicalPhysicalLab.State.FineParameters,
                new StrategicCellId(targetId));
            var route = PhysicalRemoteDestinationRouteProjection.Preview(
                _canonicalPhysicalLab, _workerOrigin.Value, target,
                (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                (EconomicActivityKind)_workerToJob.GetSelectedId(),
                checked((ulong)_workerAmount.Value));

            var message = route.Availability switch
            {
                PhysicalPrimitiveRouteAvailability.Walkable =>
                    $"ROTA TERRESTRE: {route.MovementLegs} trechos adjacentes | " +
                    $"mínimo estimado {route.PrimitiveTravelTurns} turnos " +
                    $"| chegada não antes do turno {route.EarliestArrivalTurn}.\n" +
                    "Caminho: " +
                    string.Join(" → ", route.Tiles.Take(8)
                        .Select(t => t.FineStrategicCellId.Value)) +
                    (route.Tiles.Count > 8 ?
                        (route.Tiles.Count > 9 ? " → ... → " : " → ") +
                        route.Destination.FineStrategicCellId.Value : ""),
                PhysicalPrimitiveRouteAvailability.DestinationNeedsOtherTechnology =>
                    "O tile não comporta trabalho primitivo terrestre neste momento. " +
                    "Outras tecnologias de assentamento/transporte poderão abrir possibilidades.",
                PhysicalPrimitiveRouteAvailability.NoContinuousLandFootpath =>
                    "Não há conexão terrestre contínua para caminhada primitiva. " +
                    "A colonização não está proibida: requer outro modal ou infraestrutura.",
                _ => "Modal ainda não reconhecido."
            };
            _remoteRoutePreview.Text =
                $"PLANEJAMENTO GLOBAL | origem " +
                $"{route.Origin.FineStrategicCellId.Value} → destino " +
                $"{route.Destination.FineStrategicCellId.Value} " +
                $"| {route.Workers} trabalhadores\n" +
                message +
                "\nConsulta sem viagem, sem custo monetário inventado e sem " +
                "limite fixo de distância. O custo material/logístico futuro " +
                "será avaliado pelo modal e percurso. NÃO ORDENA DESLOCAMENTO.";
            _remoteRoutePreview.Visible = true;
            if (!_canonicalPhysicalLab.CurrentHash.Equals(sourceHash))
                throw new InvalidOperationException(
                    "Remote route preview unexpectedly changed the world.");
            GD.Print($"GA_VS28_GLOBAL_ROUTE_PREVIEW origin=" +
                $"{route.Origin.FineStrategicCellId.Value} " +
                $"destination={route.Destination.FineStrategicCellId.Value} " +
                $"legs={route.MovementLegs} status={route.Availability} " +
                $"hash={sourceHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or OverflowException)
        {
            _remoteRoutePreview.Text =
                "Planejamento indisponível (partida preservada): " + ex.Message;
            _remoteRoutePreview.Visible = true;
        }
    }

    private void SurveyAdjacentPhysicalWork()
    {
        if (_canonicalPhysicalLab is null || _workerOrigin is null ||
            _workerToJob is null || _adjacentSurveyPreview is null)
            return;
        if (_foodAvailabilityPreview is not null)
            _foodAvailabilityPreview.Visible = false;
        if (_workerDecisionPreview is not null)
            _workerDecisionPreview.Visible = false;
        try
        {
            var sourceHash = _canonicalPhysicalLab.CurrentHash;
            var targetJob = (EconomicActivityKind)_workerToJob.GetSelectedId();
            var report = PhysicalAdjacentWorkSurveyProjection.Survey(
                _canonicalPhysicalLab, _workerOrigin.Value, targetJob);

            static string Amount(long raw) =>
                (raw / 1_000_000.0).ToString("F2");
            var text = new StringBuilder();
            text.Append($"TILES VIZINHOS VIÁVEIS | origem " +
                $"{report.Origin.FineStrategicCellId.Value} | " +
                $"destino produtivo: {targetJob}\n");
            text.Append($"Potencial por trabalhador NA ORIGEM: " +
                $"{Amount(report.CurrentTileYieldRaw)} " +
                "(unidades físicas/ciclo econômico)\n");
            foreach (var item in report.Neighbours)
            {
                text.Append($"Tile {item.Tile.FineStrategicCellId.Value}: " +
                    $"agricultura {Amount(item.FoodYieldPerWorkerRaw)}; " +
                    $"madeira {Amount(item.WoodYieldPerWorkerRaw)}; " +
                    $"pedra {Amount(item.StoneYieldPerWorkerRaw)}");
                if (item.ResidentWorkers != 0UL || item.WorkersArriving != 0UL)
                    text.Append($" | residentes {item.ResidentWorkers}; " +
                        $"em viagem {item.WorkersArriving}");
                text.Append('\n');
            }
            text.Append("Ordenado pela atividade de destino. Potencial não é " +
                "produção realizada, receita nem propriedade. " +
                "Para deslocar, selecione o tile no mapa e use ORDENAR DESLOCAMENTO.");
            _adjacentSurveyPreview.Text = text.ToString();
            _adjacentSurveyPreview.Visible = true;
            if (!_canonicalPhysicalLab.CurrentHash.Equals(sourceHash))
                throw new InvalidOperationException(
                    "Physical reconnaissance unexpectedly changed the world.");
            GD.Print($"GA_VS26_ADJACENT_SURVEY origin=" +
                $"{report.Origin.FineStrategicCellId.Value} " +
                $"job={targetJob} neighbours={report.Neighbours.Count} " +
                $"hash={sourceHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or
                                   ArgumentException or OverflowException)
        {
            _adjacentSurveyPreview.Text =
                "Exploração indisponível (partida preservada): " + ex.Message;
            _adjacentSurveyPreview.Visible = true;
        }
    }

    private void DiagnoseNextFoodCycleWithoutCommitting()
    {
        if (_canonicalPhysicalLab is null || _workerOrigin is null ||
            _foodAvailabilityPreview is null) return;
        if (_workerDecisionPreview is not null)
            _workerDecisionPreview.Visible = false;
        try
        {
            var sourceHash = _canonicalPhysicalLab.CurrentHash;
            var report = PhysicalFoodAvailabilityProjection.PreviewNextCycle(
                _canonicalPhysicalLab, _workerOrigin.Value);
            static string Quant(ulong raw) => (raw / 1_000_000.0).ToString("F2");
            var physicalLine = report.PhysicalShortageRaw == 0UL
                ? "nenhuma falta física"
                : $"falta física {Quant(report.PhysicalShortageRaw)}";
            var marketLine = report.UnconsumedDespiteSupplyRaw == 0UL
                ? "nenhuma demanda adicional não atendida"
                : $"demanda não atendida apesar de oferta {Quant(report.UnconsumedDespiteSupplyRaw)}";
            _foodAvailabilityPreview.Text =
                $"OFERTA ALIMENTAR | tile {report.Tile.FineStrategicCellId.Value} " +
                $"| próximo ciclo no turno {report.SettlementMovementTurn}\n" +
                $"Estoque inicial {Quant(report.InitialFoodStockRaw)} + " +
                $"produção {Quant(report.ProducedFoodRaw)} = " +
                $"disponível {Quant(report.TotalAvailableFoodRaw)}\n" +
                $"Demanda {Quant(report.FoodDemandRaw)} | " +
                $"consumo efetivo {Quant(report.ActuallyConsumedRaw)}\n" +
                $"Não atendido: {physicalLine}; {marketLine}\n" +
                $"Estoque final {Quant(report.FinalFoodStockRaw)}. " +
                "A segunda parcela não prova falta de dinheiro; " +
                "é uma diferença observada na liquidação do mercado. " +
                "Prévia sem avançar a partida.";
            _foodAvailabilityPreview.Visible = true;
            if (!_canonicalPhysicalLab.CurrentHash.Equals(sourceHash))
                throw new InvalidOperationException(
                    "Food diagnosis unexpectedly modified the live game.");
            GD.Print($"GA_VS25_FOOD_AVAILABILITY_PREVIEW tile={report.Tile.FineStrategicCellId.Value} " +
                $"turn={report.StartingMovementTurn} settled={report.SettlementMovementTurn} " +
                $"demand={report.FoodDemandRaw} consumed={report.ActuallyConsumedRaw} " +
                $"shortage={report.PhysicalShortageRaw} " +
                $"unmatched_supply={report.UnconsumedDespiteSupplyRaw} " +
                $"hash={sourceHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or
                                   ArgumentException or OverflowException)
        {
            _foodAvailabilityPreview.Text =
                "Diagnóstico não disponível (partida preservada): " + ex.Message;
            _foodAvailabilityPreview.Visible = true;
        }
    }

    private void CompareWorkerDecisionWithoutCommitting()
    {
        if (_canonicalPhysicalLab is null || _workerOrigin is null ||
            _workerFromJob is null || _workerToJob is null ||
            _workerAmount is null || _workerDecisionPreview is null)
            return;
        if (_foodAvailabilityPreview is not null)
            _foodAvailabilityPreview.Visible = false;
        try
        {
            var sourceHash = _canonicalPhysicalLab.CurrentHash;
            var from = (EconomicActivityKind)_workerFromJob.GetSelectedId();
            var to = (EconomicActivityKind)_workerToJob.GetSelectedId();
            var amount = checked((ulong)_workerAmount.Value);
            var forecast = PhysicalWorkerDecisionProjection.Compare(
                _canonicalPhysicalLab, _workerOrigin.Value, from, to, amount);

            static string Quant(ulong raw) => (raw / 1_000_000.0).ToString("F2");
            static string Service(RecentPhysicalEconomicCycles sample)
            {
                var demand = (double)sample.FoodConsumedRaw + sample.FoodUnmetRaw;
                return demand == 0.0 ? "sem demanda" :
                    $"{sample.FoodConsumedRaw * 100.0 / demand:F1}% atendido";
            }
            var baseLine = forecast.Unchanged;
            var modified = forecast.Reassigned;
            _workerDecisionPreview.Text =
                $"PRÉVIA HIPOTÉTICA | tile {forecast.Tile.FineStrategicCellId.Value} " +
                $"| turnos {forecast.StartingMovementTurn} a {forecast.ProjectedFinalMovementTurn}\n" +
                $"Manter: alimento {Quant(baseLine.FoodService.FoodConsumedRaw)}; " +
                $"falta {Quant(baseLine.FoodService.FoodUnmetRaw)}; " +
                $"{Service(baseLine.FoodService)}\n" +
                $"Mudar {amount} trabalhador(es), {from} → {to}: " +
                $"alimento {Quant(modified.FoodService.FoodConsumedRaw)}; " +
                $"falta {Quant(modified.FoodService.FoodUnmetRaw)}; " +
                $"{Service(modified.FoodService)}\n" +
                $"ESTOQUE LOCAL NO FINAL (alim./madeira/pedra): " +
                $"manter {Quant(baseLine.LocalFoodStockRaw)}/" +
                $"{Quant(baseLine.LocalWoodStockRaw)}/" +
                $"{Quant(baseLine.LocalStoneStockRaw)} | mudar " +
                $"{Quant(modified.LocalFoodStockRaw)}/" +
                $"{Quant(modified.LocalWoodStockRaw)}/" +
                $"{Quant(modified.LocalStoneStockRaw)}\n" +
                "Previsão condicional: nenhuma ação aplicada. Para confirmar, " +
                "use MUDAR ATIVIDADE NO TILE. Não é uma estimativa de PIB.";
            _workerDecisionPreview.Visible = true;
            if (!_canonicalPhysicalLab.CurrentHash.Equals(sourceHash))
                throw new InvalidOperationException(
                    "Hypothetical preview unexpectedly modified the live game.");
            GD.Print($"GA_VS24_LABOR_DECISION_PREVIEW tile={forecast.Tile.FineStrategicCellId.Value} " +
                $"turn={forecast.StartingMovementTurn} horizon={forecast.ProjectedFinalMovementTurn} " +
                $"workers={forecast.Workers} hash={sourceHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            _workerDecisionPreview.Text =
                "Prévia não disponível (partida preservada): " + ex.Message;
            _workerDecisionPreview.Visible = true;
        }
    }

    private void ReassignWorkerLabWorkers()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _workerOrigin is null || _workerAmount is null ||
            _workerFromJob is null || _workerToJob is null)
            return;
        try
        {
            _canonicalPhysicalLab.ReassignWorkers(_workerOrigin.Value,
                (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                (EconomicActivityKind)_workerToJob.GetSelectedId(),
                checked((ulong)_workerAmount.Value));
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            GD.Print($"GA_VS21_LOCAL_REASSIGNMENT tile={_workerOrigin.Value.FineStrategicCellId.Value} " +
                $"total={_canonicalPhysicalLab.State.TotalWorkers} " +
                $"turn={_canonicalPhysicalLab.State.MovementTurn} " +
                $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            if (_workerLabStatus is not null)
                _workerLabStatus.Text = "REALOCAÇÃO RECUSADA (estado preservado): " + ex.Message;
        }
    }

    private void AdvanceOfflineLabTurn()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _workerLabStatus is null) return;
        try
        {
            var cycle = _canonicalPhysicalLab.AdvanceMovementTurn();
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            GD.Print(cycle is null
                ? $"GA-VS10-CANONICAL-TICK movement_turn={_canonicalPhysicalLab.State.MovementTurn} " +
                  $"revision={_canonicalSession.Snapshot.Revision} settled=false"
                : $"GA-VS10-CANONICAL-SETTLED movement_turn={cycle.MovementTurn} " +
                  $"produced_raw={cycle.ProducedRaw} food_consumed_raw={cycle.FoodConsumedRaw} " +
                  $"food_unmet_raw={cycle.FoodUnmetRaw} " +
                  $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException)
        {
            _workerLabStatus.Text = "TURNO RECUSADO: " + ex.Message;
        }
    }

    private void AdvanceOfflineLabToNextEconomicCycle()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _workerLabStatus is null) return;
        try
        {
            var plan = PhysicalEconomicCycleProgression.Preview(_canonicalPhysicalLab);
            var settled = PhysicalEconomicCycleProgression.AdvanceToNext(_canonicalPhysicalLab);
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            GD.Print($"GA_VS22_ECONOMIC_CYCLE_ADVANCED from={plan.CurrentMovementTurn} " +
                $"to={settled.MovementTurn} steps={plan.MovementTurnsToAdvance} " +
                $"cycles={_canonicalPhysicalLab.State.Cycles.Count} " +
                $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException)
        {
            // An underlying single-turn failure may have committed earlier
            // ticks. Always refresh from the authoritative state, never
            // imply a rollback of already journaled movement turns.
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _workerLabStatus.Text += "\nAVANÇO INTERROMPIDO: " + ex.Message +
                " (verifique o turno efetivamente alcançado).";
        }
    }

    private void AdvanceOfflineLabFiveEconomicCycles()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _workerLabStatus is null) return;
        try
        {
            var completed = PhysicalEconomicCycleProgression.AdvanceBatch(_canonicalPhysicalLab);
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            GD.Print($"GA_VS23_FIVE_CYCLES_ADVANCED from={completed.StartingMovementTurn} " +
                $"to={completed.FinalSettlementTurn} steps={completed.MovementTurnsToAdvance} " +
                $"cycles={completed.EconomicCycles} " +
                $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException)
        {
            // A runtime error does not roll back already committed canonical ticks.
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _workerLabStatus.Text += "\nAVANÇO DE CICLOS INTERROMPIDO: " + ex.Message +
                " (confira o estado efetivamente alcançado).";
        }
    }

    private static string RemoteJourneyLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs29-remote-journey.json");
    private static string BornCivilizationLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs19b-born-civilization.json");
    private static string TimedLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs15-worker-transit.json");
    private static string CashFoodLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs14-cash-food.json");
    private static string GeographyLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs12-geography-physical.json");
    private static string AdaptiveLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs11-adaptive-physical.json");
    private static string CanonicalLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs10-canonical-physical.json");
    private static string LegacyLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs07-neolithic-offline.json");

    private void SaveOfflineLab()
    {
        if (_canonicalPhysicalLab is null || _workerLabStatus is null) return;
        try
        {
            var content = _canonicalPhysicalLab.ExportSaveJson();
            var isRemote = _canonicalPhysicalLab.State.RemoteRoutingEnabled;
            var isBorn = _canonicalPhysicalLab.HasInitialCivilization;
            var isTimed = _canonicalPhysicalLab.MovementMode == PhysicalWorkerMovementMode.TimedAdjacent;
            var isCash = _canonicalPhysicalLab.MarketMode == PhysicalFoodMarketMode.LocalCashSettlement;
            var isGeographic = _canonicalPhysicalLab.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C;
            var isAdaptive = _canonicalPhysicalLab.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty;
            var path = isRemote ? RemoteJourneyLabSavePath :
                isBorn ? BornCivilizationLabSavePath :
                isTimed ? TimedLabSavePath : isCash ? CashFoodLabSavePath : isGeographic ? GeographyLabSavePath :
                isAdaptive ? AdaptiveLabSavePath : CanonicalLabSavePath;
            var temp = path + ".tmp";
            File.WriteAllText(temp, content, Encoding.UTF8);
            File.Move(temp, path, overwrite: true);
            // Save only viewport/presentation alongside the canonical file.
            // It is bound to this exact save by SHA-256 and can be discarded
            // without affecting the V13 replay or physical world hash.
            var cameraSaved = false;
            var viewPath = path + ".view.json";
            try
            {
                if (_tacticalPreview is null)
                    throw new InvalidOperationException("A câmera tática não está inicializada.");
                var bookmark = _tacticalPreview.CaptureCameraBookmark();
                var viewContent = TacticalViewSidecarCodec.Create(content, bookmark);
                File.WriteAllText(viewPath + ".tmp", viewContent, Encoding.UTF8);
                File.Move(viewPath + ".tmp", viewPath, overwrite: true);
                cameraSaved = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or InvalidOperationException or ArgumentException)
            {
                // A stale camera file must never override this newly saved
                // game, even when the physical state happens not to change.
                try { if (File.Exists(viewPath)) File.Delete(viewPath); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    GD.PushWarning("GA-VS18-VIEW-STALE-CLEANUP: " + cleanup.Message);
                }
                GD.PushWarning("GA-VS18-VIEW-SAVE-FAILED: " + ex.Message);
            }
            UpdateWorkerLabHud();
            _workerLabStatus.Text += cameraSaved
                ? "\nCÂMERA TÁTICA SALVA: posição, zoom e orientação."
                : "\nATENÇÃO: partida salva, mas a visão tática não pôde ser salva.";
            _workerLabStatus.Text += isRemote
                ? "\nSAVE MULTITRECHO V15 GRAVADO: ga-vs29-remote-journey.json"
                : isBorn
                ? "\nSAVE CIVILIZACIONAL V14 GRAVADO: ga-vs19b-born-civilization.json"
                : isTimed
                ? "\nSAVE TEMPORAL V13 GRAVADO: ga-vs15-worker-transit.json"
                : isCash
                ? "\nSAVE DE CAIXA V12 GRAVADO: ga-vs14-cash-food.json"
                : isGeographic
                ? "\nSAVE GEOGRÁFICO V11 GRAVADO: ga-vs12-geography-physical.json"
                : isAdaptive
                    ? "\nSAVE ADAPTATIVO V10 GRAVADO: ga-vs11-adaptive-physical.json"
                    : "\nSAVE LEGADO V9 GRAVADO: ga-vs10-canonical-physical.json";
            GD.Print($"GA-VS10-CANONICAL-SAVED revision={_canonicalPhysicalLab.Game.Snapshot.Revision} " +
                $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _workerLabStatus.Text = "ERRO AO SALVAR: " + ex.Message;
        }
    }

    private void LoadOfflineLab()
    {
        if (_tacticalPreview is null || _workerLabStatus is null) return;
        try
        {
            // Never silently downgrade on a damaged newer save.
            var hasRemote = File.Exists(RemoteJourneyLabSavePath);
            var hasBorn = !hasRemote && File.Exists(BornCivilizationLabSavePath);
            var hasTimed = !hasBorn && File.Exists(TimedLabSavePath);
            var hasCash = !hasBorn && !hasTimed && File.Exists(CashFoodLabSavePath);
            var hasGeography = !hasTimed && !hasCash && File.Exists(GeographyLabSavePath);
            var hasAdaptive = !hasCash && !hasGeography && File.Exists(AdaptiveLabSavePath);
            var hasCanonical = !hasCash && !hasGeography && !hasAdaptive && File.Exists(CanonicalLabSavePath);
            var path = hasRemote ? RemoteJourneyLabSavePath :
                hasBorn ? BornCivilizationLabSavePath :
                hasTimed ? TimedLabSavePath : hasCash ? CashFoodLabSavePath : hasGeography ? GeographyLabSavePath :
                hasAdaptive ? AdaptiveLabSavePath :
                hasCanonical ? CanonicalLabSavePath : LegacyLabSavePath;
            var content = File.ReadAllText(path, Encoding.UTF8);
            var restored = hasRemote || hasBorn || hasTimed || hasCash || hasGeography || hasAdaptive || hasCanonical
                ? CanonicalPhysicalTacticalSession.RestoreSaveJson(content)
                : CanonicalPhysicalTacticalSession.MigrateVs07SaveJson(content);
            if (restored.State.CoarseParameters != new GoldbergParameters(3, 0) ||
                restored.State.FineParameters != new GoldbergParameters(18, 0) ||
                _canonicalSession is null ||
                restored.Game.Snapshot.WorldBinding != _canonicalSession.Snapshot.WorldBinding)
                throw new InvalidOperationException("Save não pertence ao mundo G3/G18 ativo.");
            // Optional companion is read-only and not part of the canonical
            // payload. Missing, invalid, or wrong-save views default safely.
            TacticalCameraBookmark? view = null;
            var viewPath = path + ".view.json";
            if (File.Exists(viewPath))
            {
                try
                {
                    var viewContent = File.ReadAllText(viewPath, Encoding.UTF8);
                    if (!TacticalViewSidecarCodec.TryRestore(content, viewContent, out view))
                        GD.PushWarning("GA-VS18-VIEW-MISMATCH: ignored invalid or stale camera companion.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    GD.PushWarning("GA-VS18-VIEW-READ-FAILED: " + ex.Message);
                }
            }
            var origin = restored.State.Allocations.FirstOrDefault()?.TileId
                ?? restored.State.Transits.FirstOrDefault()?.Source
                ?? restored.State.LongTrips.FirstOrDefault()?.Source
                ?? restored.InitialTile;
            var focus = restored.DisplayBucket(origin);
            // Visual and economic data must share the same fine-tile reference.
            // Legacy saves display V1 land instead of mislabeling V1 tiles as 01C.
            _tacticalPreview.BindEconomicTacticalGeography(
                restored.FineWorld, restored.CanonicalTerrain);
            _tacticalPreview.Open(focus.Value);
            var cameraRestored = view is not null &&
                _tacticalPreview.TryRestoreCameraBookmark(view);
            if (!cameraRestored && view is not null)
                _tacticalPreview.Open(focus.Value);
            // A saved view may legitimately be centered away from the
            // workforce origin. Only the default frame requires the
            // origin tile to appear in the visible physical scope.
            if (!cameraRestored &&
                !_tacticalPreview.HasVisiblePhysicalTile(origin.FineStrategicCellId.Value))
                throw new InvalidOperationException("Tile restaurado não aparece na malha.");
            _canonicalSession = restored.Game;
            _canonicalPhysicalLab = restored;
            _workerOrigin = origin;
            _workerDestination = null;
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _workerLabStatus.Text += cameraRestored
                ? "\nCÂMERA TÁTICA RESTAURADA: mesma posição e zoom do save."
                : "\nVisão tática: enquadramento inicial (save antigo ou visão indisponível).";
            _workerLabStatus.Text += hasRemote
                ? "\nSAVE DE VIAGEM MULTITRECHO V15 REABERTO E VALIDADO."
                : hasBorn
                ? "\nSAVE CIVILIZACIONAL V14 REABERTO E VALIDADO."
                : hasTimed
                ? "\nSAVE TEMPORAL V13 REABERTO E VALIDADO."
                : hasCash
                ? "\nSAVE DE CAIXA V12 REABERTO E VALIDADO."
                : hasGeography
                ? "\nSAVE GEOGRÁFICO V11 REABERTO E VALIDADO."
                : hasAdaptive
                    ? "\nSAVE ADAPTATIVO V10 REABERTO: TERRENO V1 LEGADO."
                    : hasCanonical
                        ? "\nSAVE LEGADO V9 REABERTO: TERRENO V1 LEGADO."
                        : "\nSAVE VS07 MIGRADO EM MEMÓRIA: TERRENO V1 LEGADO.";
            GD.Print($"GA-VS10-CANONICAL-LOADED revision={_canonicalSession.Snapshot.Revision} " +
                $"hash={restored.CurrentHash.HexDigest} migrated_legacy={!hasCanonical}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or OverflowException)
        {
            _workerLabStatus.Text = "SAVE NÃO CARREGADO (estado anterior preservado): " + ex.Message;
        }
    }

    private void QuoteLocalFoodMarket()
    {
        if (_canonicalPhysicalLab is null || _workerOrigin is null ||
            _referenceFoodBudget is null || _foodMarketQuoteLabel is null)
            return;
        try
        {
            var quote = _canonicalPhysicalLab.QuoteNextLocalFoodMarket(
                _workerOrigin.Value, _referenceFoodBudget.Value);
            _foodMarketQuoteRevision = _canonicalPhysicalLab.Game.Snapshot.Revision;
            _foodMarketQuoteTile = _workerOrigin.Value.FineStrategicCellId.Value;
            _foodMarketQuoteLabel.Text =
                $"COTAÇÃO HIPOTÉTICA LOCAL | tile {_foodMarketQuoteTile} | " +
                $"estoque + próxima colheita: {quote.ReferenceSupply:F3} | " +
                $"necessidade alimentar: {quote.RawFoodNeed / 1_000_000.0:F3}\n" +
                $"preço endógeno: {quote.PricePerWholeFoodUnit:F4} | " +
                $"venda possível: {quote.ReferenceSold:F3} | " +
                $"não vendido: {quote.ReferenceUnsold:F3}\n" +
                $"gasto potencial: {quote.ReferenceBuyerSpending:F3} | " +
                $"H residual: {quote.ReferenceBudgetResidual:F3}\n" +
                "APENAS REFERÊNCIA: sem compra efetiva, sem transporte, " +
                "sem lançamento de receita e sem alteração do WorldState.";
        }
        catch (Exception ex) when (ex is ArgumentException or
            InvalidOperationException or OverflowException)
        {
            _foodMarketQuoteRevision = null;
            _foodMarketQuoteTile = null;
            _foodMarketQuoteLabel.Text = "COTAÇÃO NÃO DISPONÍVEL: " + ex.Message;
        }
    }

    private void UpdateWorkerLabHud()
    {
        if (_workerDecisionPreview is not null)
            _workerDecisionPreview.Visible = false; // clear any stale hypothetical
        if (_foodAvailabilityPreview is not null)
            _foodAvailabilityPreview.Visible = false;
        if (_adjacentSurveyPreview is not null)
            _adjacentSurveyPreview.Visible = false;
        if (_remoteRoutePreview is not null)
            _remoteRoutePreview.Visible = false;
        if (_canonicalPhysicalLab is null || _workerOrigin is null || _tacticalPreview is null ||
            _workerLabStatus is null) return;
        if (_remoteDestinationTileId is not null)
            _remoteDestinationTileId.MaxValue =
                (double)_canonicalPhysicalLab.State.FineParameters.StrategicCellCount;
        var state = _canonicalPhysicalLab.State;
        if (_foodMarketQuoteLabel is not null &&
            (_foodMarketQuoteRevision != _canonicalPhysicalLab.Game.Snapshot.Revision ||
             _foodMarketQuoteTile != _workerOrigin.Value.FineStrategicCellId.Value))
        {
            _foodMarketQuoteRevision = null;
            _foodMarketQuoteTile = null;
            _foodMarketQuoteLabel.Text = "Cotação opcional: refaça após mudanças de estado/tile.";
        }
        var available = _canonicalPhysicalLab.AdjacentWorkable(_workerOrigin.Value);
        var settlements = EmergentSettlementProjection.FromSession(_canonicalPhysicalLab);
        _tacticalPreview.SetSettlementSites(settlements);
        _tacticalPreview.SetWorkerMarkers(
            TacticalWorkerMarkerProjection.FromPhysicalState(state),
            _workerOrigin.Value.FineStrategicCellId.Value,
            _workerFromJob is null ? null :
                (EconomicActivityKind)_workerFromJob.GetSelectedId());
        _tacticalPreview.SetWorkerRings(
            _workerOrigin.Value.FineStrategicCellId.Value,
            _workerDestination?.FineStrategicCellId.Value,
            available);

        var origin = _workerOrigin.Value;
        var inhabited = settlements.At(origin);
        if (_settlementSummary is not null)
        {
            var originSite = inhabited is null
                ? "origem sem residentes ativos"
                : $"tile {origin.FineStrategicCellId.Value}: {inhabited.Workers} pessoas " +
                  $"(agricultura {inhabited.AgriculturalWorkers}, madeira {inhabited.ForestryWorkers}, " +
                  $"pedra {inhabited.MiningWorkers})";
            _settlementSummary.Text =
                $"NÚCLEOS HABITADOS {settlements.Sites.Count} | residentes " +
                $"{settlements.TotalResidentWorkers} | viajando {settlements.TotalTravellers}\n" +
                originSite;
        }
        var local = state.Allocations.Where(x => x.TileId == origin).ToArray();
        var selectedActivity = _workerFromJob is null
            ? EconomicActivityKind.Agriculture
            : (EconomicActivityKind)_workerFromJob.GetSelectedId();
        var selectedGroupWorkers = local.Where(x => x.Activity == selectedActivity)
            .Aggregate(0UL, (sum, x) => checked(sum + x.Workers));
        if (_workerAmount is not null)
        {
            _workerAmount.MaxValue = Math.Max(1, (double)selectedGroupWorkers);
            if (_workerAmount.Value > _workerAmount.MaxValue)
                _workerAmount.Value = _workerAmount.MaxValue;
        }
        var demand = _canonicalPhysicalLab.FoodDemandAt(origin);
        var jobs = string.Join(" | ", local.Select(x => $"{x.Activity}: {x.Workers}"));
        var destination = _workerDestination.HasValue
            ? $"Tile {_workerDestination.Value.FineStrategicCellId.Value} selecionado"
            : "Clique num vizinho contornado em verde-lima";
        var stockFood = _canonicalPhysicalLab.StockAt(origin, new CommodityId(1U));
        var stockStone = _canonicalPhysicalLab.StockAt(origin, new CommodityId(2U));
        var stockWood = _canonicalPhysicalLab.StockAt(origin, new CommodityId(3U));
        var lastCycle = state.Cycles.LastOrDefault();
        var nextEconomicCycle = PhysicalEconomicCycleProgression.Preview(_canonicalPhysicalLab);
        var recentCycles = PhysicalEconomicCycleProgression.SummarizeRecent(_canonicalPhysicalLab);
        if (_economicCycleSummary is not null)
        {
            var cyclePulse = lastCycle is null
                ? "Nenhum ciclo liquidado."
                : $"Último ciclo, turno {lastCycle.MovementTurn}: " +
                  $"produção física bruta {lastCycle.ProducedRaw / 1_000_000.0:F2} " +
                  $"(soma de itens diferentes) | " +
                  $"alimento consumido {lastCycle.FoodConsumedRaw / 1_000_000.0:F2} | " +
                  $"falta de alimento {lastCycle.FoodUnmetRaw / 1_000_000.0:F2}";
            var demanded = (double)recentCycles.FoodConsumedRaw + recentCycles.FoodUnmetRaw;
            var service = demanded <= 0.0 ? "sem demanda alimentar" :
                $"{100.0 * recentCycles.FoodConsumedRaw / demanded:F1}% atendido";
            var recentPulse = recentCycles.CycleCount == 0
                ? "Histórico: nenhum ciclo realizado."
                : $"ÚLTIMOS {recentCycles.CycleCount} CICLOS (turnos " +
                  $"{recentCycles.FirstSettlementTurn} a {recentCycles.LastSettlementTurn}): " +
                  $"alimento consumido {recentCycles.FoodConsumedRaw / 1_000_000.0:F2} | " +
                  $"falta de alimento {recentCycles.FoodUnmetRaw / 1_000_000.0:F2} | " + service;
            _economicCycleSummary.Text =
                $"CICLOS ECONÔMICOS {state.Cycles.Count} | próximo no turno " +
                $"{nextEconomicCycle.SettlementTurn} " +
                $"({nextEconomicCycle.MovementTurnsToAdvance} movimentos)\n" +
                cyclePulse + "\n" + recentPulse;
        }
        var interval = _canonicalPhysicalLab.NextEconomicInterval;
        var nextTurn = checked(state.MovementTurn + 1UL);
        var cadenceInfo = _canonicalPhysicalLab.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty
            ? $"adaptativo 1/{interval} | faixa {((nextTurn - 1UL) / PhysicalEconomicCadence.AdaptiveWindowTurns) + 1UL}"
            : "legado fixo 1/6";
        var tickInfo =
            $"Turno de movimento: {state.MovementTurn} | ciclos econômicos: {state.Cycles.Count} | ritmo: {cadenceInfo}";
        var transitInfo = state.MovementMode == PhysicalWorkerMovementMode.TimedAdjacent
            ? "EM TRÂNSITO: " + state.WorkersInTransit + " trabalhadores | " +
              (state.Transits.Count == 0 && state.LongTrips.Count == 0
                  ? "nenhuma viagem ativa\n"
                  : string.Join(" | ", state.Transits.Select(x =>
                      $"ordem {x.OrderId}: tile {x.Source.FineStrategicCellId.Value} → " +
                      $"{x.Destination.FineStrategicCellId.Value}, {x.Workers} pessoas, " +
                      $"chegada turno {x.ArrivalTurn} ({x.ArrivalTurn - state.MovementTurn} restantes)")
                      .Concat(state.LongTrips.Select(x =>
                      $"rota {x.OrderId}: tile {x.Source.FineStrategicCellId.Value} → " +
                      $"{x.Destination.FineStrategicCellId.Value}, trecho {x.CompletedLegs + 1}/" +
                      $"{x.TotalLegs}, {x.Workers} pessoas, " +
                      $"chegada turno {x.ArrivalTurn} ({x.ArrivalTurn - state.MovementTurn} restantes)"))) + "\n")
            : "";
        var localLedger = state.FoodCash;
        var localWallet = localLedger?.At(origin);
        var lastPurchase = localLedger?.Receipts.LastOrDefault(x => x.Tile == origin);
        var cashInfo = localLedger is null ? "" :
            $"CAIXA LOCAL V12 | famílias: {localWallet!.HouseholdMicrocredits / 1_000_000.0:F3} | " +
            $"produtores: {localWallet.ProducerMicrocredits / 1_000_000.0:F3} | " +
            $"emissão inicial total: {localLedger.InitialMoneySupplyMicrocredits / 1_000_000.0:F3}\n" +
            (lastPurchase is null ? "Nenhuma compra liquidada neste tile.\n" :
                $"ÚLTIMA COMPRA REAL turno {lastPurchase.MovementTurn}: alimento {lastPurchase.FoodRaw / 1_000_000.0:F3}; " +
                $"pagamento {lastPurchase.PaidMicrocredits / 1_000_000.0:F3}; " +
                $"preço {lastPurchase.PriceMicrocreditsPerUnit / 1_000_000.0:F4}\n");
        var civilizationalInfo = _canonicalPhysicalLab.HasInitialCivilization
            ? CivilizationalEconomicAttributionProjection.FromSession(_canonicalPhysicalLab)
            : null;
        var ownershipInfo = civilizationalInfo is null
            ? "MODO LEGADO V13: associação civilizacional não materializada neste save.\n"
            : $"CIVILIZAÇÃO {civilizationalInfo.Civilization.Value} | " +
              $"comunidade: {civilizationalInfo.TotalWorkers} trabalhadores " +
              $"({civilizationalInfo.WorkersInTransit} em trânsito) | " +
              $"contas locais: {civilizationalInfo.Accounts.Count} | " +
              $"estoques de recursos: {civilizationalInfo.Stocks.Count}\n" +
              $"TITULARES: famílias {civilizationalInfo.HouseholdMoney / 1_000_000.0:F3} " +
              $"e produtores {civilizationalInfo.ProducerMoney / 1_000_000.0:F3}; " +
              "ambos da comunidade, NÃO tesouro estatal.\n" +
              "INSTITUIÇÕES: implícitas. CONTROLE POLÍTICO: não decretado.\n";
        _workerLabStatus.Text =
            (civilizationalInfo is null
                ? "LEGADO V13 | população original sem identidade retroativa"
                : $"CIVILIZAÇÃO {civilizationalInfo.Civilization.Value} | {state.TotalWorkers} trabalhadores") +
            $" | turno {state.MovementTurn} | ciclos {state.Cycles.Count}\n" +
            $"Origem {origin.FineStrategicCellId.Value} | {selectedActivity}: {selectedGroupWorkers} disponíveis\n" +
            $"Destino: {destination}\n" +
            (state.Transits.Count == 0 && state.LongTrips.Count == 0
                ? "Viagens: nenhuma em andamento."
                : string.Join(" | ", state.Transits.Select(x =>
                    $"Em viagem: {x.Workers} pessoas, chegada no turno {x.ArrivalTurn} " +
                    $"({x.ArrivalTurn - state.MovementTurn} restantes)")
                    .Concat(state.LongTrips.Select(x =>
                    $"Em viagem longa: {x.Workers} pessoas, trecho {x.CompletedLegs + 1}/" +
                    $"{x.TotalLegs} rumo ao tile {x.Destination.FineStrategicCellId.Value}, " +
                    $"chegada no turno {x.ArrivalTurn} " +
                    $"({x.ArrivalTurn - state.MovementTurn} restantes)")))) +
            "";
        if (_workerLabDetails is not null)
            _workerLabDetails.Text =
            $"POVOAMENTOS DERIVADOS: {settlements.Sites.Count} núcleos; " +
            $"contas em tiles desabitados {settlements.UninhabitedAccountMoney / 1_000_000.0:F3}; " +
            $"estoques em tiles desabitados {settlements.UninhabitedStockRecords} registros. " +
            "Esses bens não desaparecem nem criam cidades.\n" +
            ownershipInfo +
            $"Origem tile {origin.FineStrategicCellId.Value} | {jobs}\n" +
            $"Grupo selecionado: {selectedActivity} | disponíveis: {selectedGroupWorkers} trabalhadores\n" +
            $"Destino: {destination}\n" +
            $"Trabalhadores globais: {state.TotalWorkers} | " +
            $"tiles ocupados: {state.Allocations.Select(x => x.TileId).Distinct().Count()} | " +
            $"revisão canônica: {_canonicalPhysicalLab.Game.Snapshot.Revision}\n" +
            $"Consumo alimentar na origem (prévia): {demand / 1_000_000.0:F3}\n" +
            tickInfo + "\n" + transitInfo + cashInfo +
            $"Estoque LOCAL: alimento {stockFood / 1_000_000.0:F3} | pedra {stockStone / 1_000_000.0:F3} | madeira {stockWood / 1_000_000.0:F3}\n" +
            (lastCycle is null ? "Nenhum ciclo econômico liquidado ainda.\n" :
                $"Último ciclo: produção {lastCycle.ProducedRaw / 1_000_000.0:F3}; " +
                $"consumo alimentar {lastCycle.FoodConsumedRaw / 1_000_000.0:F3}; " +
                $"FALTA {lastCycle.FoodUnmetRaw / 1_000_000.0:F3}\n") +
            $"WORLDSTATE CANÔNICO V{(state.RemoteRoutingEnabled ? "15" : _canonicalPhysicalLab.HasInitialCivilization ? "14" : _canonicalPhysicalLab.MovementMode == PhysicalWorkerMovementMode.TimedAdjacent ? "13" : _canonicalPhysicalLab.MarketMode == PhysicalFoodMarketMode.LocalCashSettlement ? "12" : _canonicalPhysicalLab.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C ? "11" : _canonicalPhysicalLab.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty ? "10" : "9")}: " +
            $"terreno {(_canonicalPhysicalLab.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C ? "01C" : "V1 legado")}; " +
            $"mercado alimentar {(_canonicalPhysicalLab.MarketMode == PhysicalFoodMarketMode.LocalCashSettlement ? "LIQUIDADO À VISTA" : "LEGADO DIRETO")}; " +
            $"deslocamento {(_canonicalPhysicalLab.MovementMode == PhysicalWorkerMovementMode.TimedAdjacent ? "TEMPORAL" : "LEGADO")}; SEM carga comercial.";
        if (_workerMoveButton is not null)
            _workerMoveButton.Disabled = !_workerDestination.HasValue ||
                selectedGroupWorkers == 0UL;
        if (_workerReassignButton is not null && _workerToJob is not null)
            _workerReassignButton.Disabled = selectedGroupWorkers == 0UL ||
                selectedActivity == (EconomicActivityKind)_workerToJob.GetSelectedId();
    }

}
