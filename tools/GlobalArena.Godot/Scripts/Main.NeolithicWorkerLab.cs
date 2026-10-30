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
/// Explicit, opt-in in-memory Neolithic activity lab in the REAL Godot shell.
/// Does not modify or impersonate the CanonicalGameSession WorldState.
/// The renderer receives only physical tile IDs for visual highlights.
/// </summary>
public partial class Main
{
    private EconomicPhysicalTacticalWorkerSession? _workerLab;
    private EconomicPhysicalTacticalOfflineSession? _offlineLab;
    private PhysicalEconomyCanonicalCommandGate? _physicalGate;
    private PhysicalTacticalTileId? _workerOrigin;
    private PhysicalTacticalTileId? _workerDestination;
    private Label? _workerLabStatus;
    private Label? _workerLabHover;
    private Button? _workerMoveButton;
    private SpinBox? _workerAmount;
    private OptionButton? _workerFromJob;
    private OptionButton? _workerToJob;

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
            Text = "Ordens físicas validadas e publicadas atomicamente no WorldState (hash v9); ciclo de 6 turnos ainda provisório e save offline separado. SEM mercado, transporte ou cidades.",
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

        var nextTurn = new Button { Text = "AVANÇAR TURNO DE MOVIMENTO (ECONOMIA A CADA 6)" };
        nextTurn.Pressed += AdvanceOfflineLabTurn;
        lab.AddChild(nextTurn);

        var persistence = new HBoxContainer();
        lab.AddChild(persistence);
        var save = new Button { Text = "SALVAR PARTIDA OFFLINE", SizeFlagsHorizontal = SizeFlags.ExpandFill };
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
                _workerLabHover.Text = _workerLab is null || !id.HasValue
                    ? "Sob o cursor: —"
                    : $"Sob o cursor: tile físico {id.Value}";
        };
    }

    // The command gate is the sole writer. The local offline model is a
    // defensive HUD copy, never a second authority.
    private void RefreshPhysicalLabFromGate(string commandKind)
    {
        if (_canonicalSession is null || _physicalGate is null)
            throw new InvalidOperationException("The canonical physical command gate is unavailable.");
        _offlineLab = _physicalGate.CopyForPresentation();
        _workerLab = _offlineLab.Workers;
        AttachRuntimeState(_canonicalSession.Snapshot);
        var state = _canonicalSession.Snapshot;
        var hash = new CanonicalWorldStateHasher().Compute(state);
        GD.Print($"GA-VS10-CANONICAL-PHYSICAL-COMMITTED kind={commandKind} " +
                 $"revision={state.Revision} hash_format={hash.FormatVersion} " +
                 $"movement_turn={_offlineLab.MovementTurn} economic_cycles={_offlineLab.EconomicCycles} " +
                 $"workers={_workerLab.TotalWorkers} stock_entries={_offlineLab.Stocks.Count} " +
                 $"next_sequence={_physicalGate.NextSequence} digest={hash.HexDigest} " +
                 "offline_model_is_defensive_copy=true");
    }

    private void StartWorkerLab()
    {
        if (_tacticalPreview is null || _workerLabStatus is null)
            return;
        try
        {
            // The existing production renderer is G(3,0)->G(18,0), not the
            // G(2,0)->G(12,0) used by the isolated VS03/VS04 demos.
            // Explicit instance parameters prevent invalid cross-world IDs.
            if (_canonicalSession is null)
                throw new InvalidOperationException("Canonical game session is unavailable.");
            var candidate = new EconomicPhysicalTacticalOfflineSession(
                20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
            var origin = candidate.Workers.InitialTile;
            var focus = candidate.Workers.DisplayBucket(origin);
            _tacticalPreview.Open(focus.Value);
            if (!_tacticalPreview.HasVisiblePhysicalTile(origin.FineStrategicCellId.Value))
                throw new InvalidOperationException("Worker origin is not visible in its physical map.");
            var gate = new PhysicalEconomyCanonicalCommandGate(_canonicalSession, candidate);
            _physicalGate = gate;
            _workerOrigin = origin;
            _workerDestination = null;
            RefreshPhysicalLabFromGate("start");
            UpdateWorkerLabHud();
            GD.Print($"GA-VS05-ORIGINAL-UI-LAB-READY fine=18 coarse=3 source={_workerOrigin.Value.FineStrategicCellId.Value} workers={_workerLab.TotalWorkers} physical_worldstate_published=true");
        }
        catch (Exception ex)
        {
            _workerLab = null;
            _offlineLab = null;
            _physicalGate = null;
            _workerOrigin = null;
            _workerDestination = null;
            _tacticalPreview.ClearWorkerRings();
            _workerLabStatus.Text = "LAB NÃO INICIADO: " + ex.Message;
            GD.PushError("GA-VS05-LAB-START-FAIL: " + ex);
        }
    }

    private void HandleWorkerLabSelection(ulong? clickedTile)
    {
        if (_workerLab is null || _workerOrigin is null || _tacticalPreview is null)
            return;
        if (!clickedTile.HasValue)
        {
            _workerDestination = null;
            UpdateWorkerLabHud();
            return;
        }

        var id = new PhysicalTacticalTileId(
            _workerLab.FineWorldParameters, new StrategicCellId(clickedTile.Value));
        var occupied = _workerLab.WorkersAt(id);
        if (occupied > 0)
        {
            _workerOrigin = id;
            _workerDestination = null;
        }
        else if (_workerLab.AdjacentWorkable(_workerOrigin.Value).Contains(id))
        {
            _workerDestination = id;
        }
        else _workerDestination = null;

        UpdateWorkerLabHud();
    }

    private void MoveWorkerLabWorkers()
    {
        if (_workerLab is null || _offlineLab is null || _physicalGate is null ||
            _canonicalSession is null || _workerOrigin is null || _workerDestination is null ||
            _workerAmount is null || _workerFromJob is null || _workerToJob is null)
            return;
        try
        {
            _physicalGate.MoveWorkers(_physicalGate.NextSequence,
                _canonicalSession.Snapshot.Revision,
                _workerOrigin.Value, _workerDestination.Value,
                (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                (EconomicActivityKind)_workerToJob.GetSelectedId(),
                checked((ulong)_workerAmount.Value));
            _workerOrigin = _workerDestination;
            _workerDestination = null;
            RefreshPhysicalLabFromGate("move");
            UpdateWorkerLabHud();
            GD.Print($"GA-VS05-ORIGINAL-UI-WORKERS-MOVED revision={_workerLab.Revision} workers={_workerLab.TotalWorkers} occupied={_workerLab.Assignments.Select(x => x.TileId).Distinct().Count()} city_entity=false physical_worldstate_published=true");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            if (_workerLabStatus is not null)
                _workerLabStatus.Text = "TRANSFERÊNCIA RECUSADA (sem alterar o estado): " + ex.Message;
        }
    }

    private void AdvanceOfflineLabTurn()
    {
        if (_physicalGate is null || _canonicalSession is null || _workerLabStatus is null) return;
        try
        {
            var receipt = _physicalGate.AdvanceMovementTurn(
                _physicalGate.NextSequence, _canonicalSession.Snapshot.Revision);
            var cycle = receipt.Settlement;
            RefreshPhysicalLabFromGate("tick");
            UpdateWorkerLabHud();
            GD.Print(cycle is null
                ? $"GA-VS07-TICK movement_turn={_offlineLab.MovementTurn} settlement=false"
                : $"GA-VS07-SETTLED movement_turn={cycle.MovementTurn} produced_raw={cycle.ProducedRaw} food_consumed_raw={cycle.FoodConsumedRaw} food_unmet_raw={cycle.FoodUnmetRaw} stock_locations={_offlineLab.Stocks.Count}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or OverflowException)
        {
            _workerLabStatus.Text = "TURNO RECUSADO: " + ex.Message;
        }
    }

    private static string OfflineLabSavePath =>
        ProjectSettings.GlobalizePath("user://ga-vs07-neolithic-offline.json");

    private void SaveOfflineLab()
    {
        if (_offlineLab is null || _workerLabStatus is null) return;
        try
        {
            var content = _physicalGate!.ExportOfflineSaveJson();
            // Create the file alongside its destination, then atomically
            // replace. No canonical WorldState or network operations involved.
            var path = OfflineLabSavePath;
            var temp = path + ".tmp";
            File.WriteAllText(temp, content, Encoding.UTF8);
            File.Move(temp, path, overwrite: true);
            UpdateWorkerLabHud();
            _workerLabStatus.Text += "\nSAVE OFFLINE GRAVADO: ga-vs07-neolithic-offline.json";
            GD.Print($"GA-VS07-SAVED movement_turn={_offlineLab.MovementTurn} cycles={_offlineLab.EconomicCycles} workers={_offlineLab.Workers.TotalWorkers}");
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
            var content = File.ReadAllText(OfflineLabSavePath, Encoding.UTF8);
            var next = EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(content);
            if (next.Workers.CoarseWorldParameters != new GoldbergParameters(3, 0) ||
                next.Workers.FineWorldParameters != new GoldbergParameters(18, 0))
                throw new InvalidOperationException("O save não pertence à malha física G3/G18 do visualizador.");
            var origin = next.Workers.Assignments.First().TileId;
            var focus = next.Workers.DisplayBucket(origin);
            // Validate a fully reconstructed state before replacing the active
            // lab. Never publish partial data from a corrupt save.
            _tacticalPreview.Open(focus.Value);
            if (!_tacticalPreview.HasVisiblePhysicalTile(origin.FineStrategicCellId.Value))
                throw new InvalidOperationException("O tile de trabalho restaurado não aparece na malha.");
            if (_canonicalSession is null)
                throw new InvalidOperationException("Canonical game session is unavailable.");
            if (_physicalGate is null)
                _physicalGate = new PhysicalEconomyCanonicalCommandGate(_canonicalSession, next);
            else
                _physicalGate.RestoreOfflineSave(_physicalGate.NextSequence,
                    _canonicalSession.Snapshot.Revision, content);
            _workerOrigin = origin;
            _workerDestination = null;
            RefreshPhysicalLabFromGate("restore");
            UpdateWorkerLabHud();
            _workerLabStatus.Text += "\nSAVE OFFLINE REABERTO E VALIDADO.";
            GD.Print($"GA-VS07-LOADED movement_turn={_offlineLab.MovementTurn} cycles={_offlineLab.EconomicCycles} workers={_workerLab.TotalWorkers}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException)
        {
            _workerLabStatus.Text = "SAVE NÃO CARREGADO (estado anterior preservado): " + ex.Message;
        }
    }

    private void UpdateWorkerLabHud()
    {
        if (_workerLab is null || _workerOrigin is null || _tacticalPreview is null ||
            _workerLabStatus is null) return;
        var available = _workerLab.AdjacentWorkable(_workerOrigin.Value);
        _tacticalPreview.SetWorkerRings(
            _workerOrigin.Value.FineStrategicCellId.Value,
            _workerDestination?.FineStrategicCellId.Value,
            available);

        var origin = _workerOrigin.Value;
        var local = _workerLab.Assignments.Where(x => x.TileId == origin).ToArray();
        var flows = _workerLab.Preview();
        var demand = flows.Demand.Where(x => x.TileId == origin)
            .Aggregate(0UL, (sum, x) => checked(sum + x.RawQuantity));
        var jobs = string.Join(" | ", local.Select(x => $"{x.Activity}: {x.Workers}"));
        var destination = _workerDestination.HasValue
            ? $"Tile {_workerDestination.Value.FineStrategicCellId.Value} selecionado"
            : "Clique num vizinho contornado em verde-lima";
        var stockFood = _offlineLab?.StockAt(origin, new CommodityId(1U)) ?? 0UL;
        var stockStone = _offlineLab?.StockAt(origin, new CommodityId(2U)) ?? 0UL;
        var stockWood = _offlineLab?.StockAt(origin, new CommodityId(3U)) ?? 0UL;
        var lastCycle = _offlineLab?.Cycles.LastOrDefault();
        var tickInfo = _offlineLab is null
            ? "Sessão de turnos não inicializada"
            : $"Turno de movimento: {_offlineLab.MovementTurn} | ciclos econômicos: {_offlineLab.EconomicCycles} (a cada 6)";
        var physicalCanonical = _canonicalSession?.Snapshot.PhysicalEconomy;
        var canonicalStatus = physicalCanonical is null
            ? "WorldState físico ainda não publicado"
            : $"WorldState físico CANÔNICO: revisão {_canonicalSession!.Snapshot.Revision} | " +
              $"trabalhadores {physicalCanonical.TotalWorkers} | estoque em {physicalCanonical.Stocks.Count} entradas";
        _workerLabStatus.Text =
            $"Origem tile {origin.FineStrategicCellId.Value} | {jobs}\n" +
            $"Destino: {destination}\n" +
            $"Trabalhadores globais: {_workerLab.TotalWorkers} | " +
            $"tiles ocupados: {_workerLab.Assignments.Select(x => x.TileId).Distinct().Count()} | " +
            $"revisão: {_workerLab.Revision}\n" +
            $"Consumo alimentar na origem (prévia): {demand / 1_000_000.0:F3}\n" +
            tickInfo + "\n" +
            canonicalStatus + "\n" +
            $"Estoque LOCAL: alimento {stockFood / 1_000_000.0:F3} | pedra {stockStone / 1_000_000.0:F3} | madeira {stockWood / 1_000_000.0:F3}\n" +
            (lastCycle is null ? "Nenhum ciclo econômico liquidado ainda.\n" :
                $"Último ciclo: produção {lastCycle.ProducedRaw / 1_000_000.0:F3}; consumo alimentar {lastCycle.FoodConsumedRaw / 1_000_000.0:F3}; FALTA {lastCycle.FoodUnmetRaw / 1_000_000.0:F3}\n") +
            "Economia física no WorldState e hash v9; save offline próprio ainda separado. SEM mercado, transporte ou fome.";
        if (_workerMoveButton is not null)
            _workerMoveButton.Disabled = !_workerDestination.HasValue;
    }
}
