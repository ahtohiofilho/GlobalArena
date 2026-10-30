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
    private Button? _workerMoveButton;
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
        lab.AddThemeConstantOverride("separation", 6);
        tacticalHud.AddChild(lab);

        var title = new Label
        {
            Text = "ATIVIDADES TÁTICAS | LABORATÓRIO",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color("#d4e9f3"));
        lab.AddChild(title);

        var warning = new Label
        {
            Text = "Geografia física CANÔNICA 01C: terrenos, trabalhadores e produção na mesma malha. Ciclos econômicos adaptativos 1/1, 1/2, 1/3... Save V11; V9/V10 preservados.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        warning.AddThemeColorOverride("font_color", new Color("#e1c883"));
        lab.AddChild(warning);

        var start = new Button { Text = "INICIAR TRABALHO NEOLÍTICO (LAB)" };
        start.Pressed += StartWorkerLab;
        lab.AddChild(start);

        var legend = new Label
        {
            Text = "Ciano: origem | Laranja: destino | Verde: vizinhos válidos",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        lab.AddChild(legend);

        _workerLabHover = new Label { Text = "Sob o cursor: —" };
        lab.AddChild(_workerLabHover);

        _workerLabStatus = new Label
        {
            Text = "Inicie o laboratório para habilitar os trabalhadores na malha física.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        lab.AddChild(_workerLabStatus);

        var jobs = new HBoxContainer();
        lab.AddChild(jobs);
        _workerFromJob = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _workerToJob = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
        jobs.AddChild(_workerFromJob);
        jobs.AddChild(_workerToJob);

        var quantity = new HBoxContainer();
        quantity.AddChild(new Label { Text = "Transferir:" });
        _workerAmount = new SpinBox { MinValue = 1, MaxValue = 8, Step = 1, Value = 3 };
        quantity.AddChild(_workerAmount);
        lab.AddChild(quantity);

        _workerMoveButton = new Button
        {
            Text = "ALOCAR NO TILE FÍSICO",
            Disabled = true
        };
        _workerMoveButton.Pressed += MoveWorkerLabWorkers;
        lab.AddChild(_workerMoveButton);

        var nextTurn = new Button { Text = "AVANÇAR TURNO DE MOVIMENTO (CICLOS ADAPTATIVOS)" };
        nextTurn.Pressed += AdvanceOfflineLabTurn;
        lab.AddChild(nextTurn);

        // First market gate: observed physical food supply and an explicitly
        // hypothetical per-worker spending budget. No cash ledger yet.
        var budgetRow = new HBoxContainer();
        budgetRow.AddChild(new Label { Text = "Orçamento hipotético / trabalhador:" });
        _referenceFoodBudget = new SpinBox
        {
            MinValue = 1, MaxValue = 100000, Step = 1, Value = 40,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        budgetRow.AddChild(_referenceFoodBudget);
        lab.AddChild(budgetRow);
        var quoteButton = new Button { Text = "COTAR MERCADO ALIMENTAR LOCAL (SEM LIQUIDAR)" };
        quoteButton.Pressed += QuoteLocalFoodMarket;
        lab.AddChild(quoteButton);
        _foodMarketQuoteLabel = new Label
        {
            Text = "Cotação opcional: não altera preços oficiais, estoques ou dinheiro.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        lab.AddChild(_foodMarketQuoteLabel);

        var persistence = new HBoxContainer();
        lab.AddChild(persistence);
        var save = new Button { Text = "SALVAR PARTIDA CANÔNICA", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        save.Pressed += SaveOfflineLab;
        persistence.AddChild(save);
        var load = new Button { Text = "REABRIR SAVE", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        load.Pressed += LoadOfflineLab;
        persistence.AddChild(load);

        if (_tacticalPreview is null)
            throw new InvalidOperationException("Tactical renderer must exist before lab HUD setup.");
        _tacticalPreview.WorkerTileHovered += id =>
        {
            if (_workerLabHover is not null)
                _workerLabHover.Text = _canonicalPhysicalLab is null || !id.HasValue
                    ? "Sob o cursor: —"
                    : $"Sob o cursor: tile físico {id.Value}";
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
            _workerOrigin ??= _canonicalPhysicalLab.State.Allocations.First().TileId;
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
                PhysicalTacticalGeographyMode.Canonical01C);
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
            _canonicalPhysicalLab.MoveWorkers(_workerOrigin.Value, _workerDestination.Value,
                (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                (EconomicActivityKind)_workerToJob.GetSelectedId(),
                checked((ulong)_workerAmount.Value));
            _workerOrigin = _workerDestination;
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
            var isGeographic = _canonicalPhysicalLab.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C;
            var isAdaptive = _canonicalPhysicalLab.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty;
            var path = isGeographic ? GeographyLabSavePath :
                isAdaptive ? AdaptiveLabSavePath : CanonicalLabSavePath;
            var temp = path + ".tmp";
            File.WriteAllText(temp, content, Encoding.UTF8);
            File.Move(temp, path, overwrite: true);
            UpdateWorkerLabHud();
            _workerLabStatus.Text += isGeographic
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
            var hasGeography = File.Exists(GeographyLabSavePath);
            var hasAdaptive = !hasGeography && File.Exists(AdaptiveLabSavePath);
            var hasCanonical = !hasGeography && !hasAdaptive && File.Exists(CanonicalLabSavePath);
            var path = hasGeography ? GeographyLabSavePath :
                hasAdaptive ? AdaptiveLabSavePath :
                hasCanonical ? CanonicalLabSavePath : LegacyLabSavePath;
            var content = File.ReadAllText(path, Encoding.UTF8);
            var restored = hasGeography || hasAdaptive || hasCanonical
                ? CanonicalPhysicalTacticalSession.RestoreSaveJson(content)
                : CanonicalPhysicalTacticalSession.MigrateVs07SaveJson(content);
            if (restored.State.CoarseParameters != new GoldbergParameters(3, 0) ||
                restored.State.FineParameters != new GoldbergParameters(18, 0) ||
                _canonicalSession is null ||
                restored.Game.Snapshot.WorldBinding != _canonicalSession.Snapshot.WorldBinding)
                throw new InvalidOperationException("Save não pertence ao mundo G3/G18 ativo.");
            var origin = restored.State.Allocations.First().TileId;
            var focus = restored.DisplayBucket(origin);
            // Visual and economic data must share the same fine-tile reference.
            // Legacy saves display V1 land instead of mislabeling V1 tiles as 01C.
            _tacticalPreview.BindEconomicTacticalGeography(
                restored.FineWorld, restored.CanonicalTerrain);
            _tacticalPreview.Open(focus.Value);
            if (!_tacticalPreview.HasVisiblePhysicalTile(origin.FineStrategicCellId.Value))
                throw new InvalidOperationException("Tile restaurado não aparece na malha.");
            _canonicalSession = restored.Game;
            _canonicalPhysicalLab = restored;
            _workerOrigin = origin;
            _workerDestination = null;
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _workerLabStatus.Text += hasGeography
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
        if (_canonicalPhysicalLab is null || _workerOrigin is null || _tacticalPreview is null ||
            _workerLabStatus is null) return;
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
        _tacticalPreview.SetWorkerRings(
            _workerOrigin.Value.FineStrategicCellId.Value,
            _workerDestination?.FineStrategicCellId.Value,
            available);

        var origin = _workerOrigin.Value;
        var local = state.Allocations.Where(x => x.TileId == origin).ToArray();
        var demand = _canonicalPhysicalLab.FoodDemandAt(origin);
        var jobs = string.Join(" | ", local.Select(x => $"{x.Activity}: {x.Workers}"));
        var destination = _workerDestination.HasValue
            ? $"Tile {_workerDestination.Value.FineStrategicCellId.Value} selecionado"
            : "Clique num vizinho contornado em verde-lima";
        var stockFood = _canonicalPhysicalLab.StockAt(origin, new CommodityId(1U));
        var stockStone = _canonicalPhysicalLab.StockAt(origin, new CommodityId(2U));
        var stockWood = _canonicalPhysicalLab.StockAt(origin, new CommodityId(3U));
        var lastCycle = state.Cycles.LastOrDefault();
        var interval = _canonicalPhysicalLab.NextEconomicInterval;
        var nextTurn = checked(state.MovementTurn + 1UL);
        var cadenceInfo = _canonicalPhysicalLab.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty
            ? $"adaptativo 1/{interval} | faixa {((nextTurn - 1UL) / PhysicalEconomicCadence.AdaptiveWindowTurns) + 1UL}"
            : "legado fixo 1/6";
        var tickInfo =
            $"Turno de movimento: {state.MovementTurn} | ciclos econômicos: {state.Cycles.Count} | ritmo: {cadenceInfo}";
        _workerLabStatus.Text =
            $"Origem tile {origin.FineStrategicCellId.Value} | {jobs}\n" +
            $"Destino: {destination}\n" +
            $"Trabalhadores globais: {state.TotalWorkers} | " +
            $"tiles ocupados: {state.Allocations.Select(x => x.TileId).Distinct().Count()} | " +
            $"revisão canônica: {_canonicalPhysicalLab.Game.Snapshot.Revision}\n" +
            $"Consumo alimentar na origem (prévia): {demand / 1_000_000.0:F3}\n" +
            tickInfo + "\n" +
            $"Estoque LOCAL: alimento {stockFood / 1_000_000.0:F3} | pedra {stockStone / 1_000_000.0:F3} | madeira {stockWood / 1_000_000.0:F3}\n" +
            (lastCycle is null ? "Nenhum ciclo econômico liquidado ainda.\n" :
                $"Último ciclo: produção {lastCycle.ProducedRaw / 1_000_000.0:F3}; " +
                $"consumo alimentar {lastCycle.FoodConsumedRaw / 1_000_000.0:F3}; " +
                $"FALTA {lastCycle.FoodUnmetRaw / 1_000_000.0:F3}\n") +
            $"WORLDSTATE CANÔNICO V{(_canonicalPhysicalLab.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C ? "11" : _canonicalPhysicalLab.CadenceMode == PhysicalEconomicCadenceMode.AdaptiveThirty ? "10" : "9")}: " +
            $"terreno {(_canonicalPhysicalLab.GeographyMode == PhysicalTacticalGeographyMode.Canonical01C ? "01C" : "V1 legado")}; produção/consumo locais; SEM mercado ou transporte.";
        if (_workerMoveButton is not null)
            _workerMoveButton.Disabled = !_workerDestination.HasValue;
    }

}
