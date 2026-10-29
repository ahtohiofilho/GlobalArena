using System;
using System.Linq;
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
    private PhysicalTacticalTileId? _workerOrigin;
    private PhysicalTacticalTileId? _workerDestination;
    private Label? _workerLabStatus;
    private Label? _workerLabHover;
    private Button? _workerMoveButton;
    private Button? _workerInstallSiteButton;
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
            Text = "Lab isolado: implante primeiro um roçado/ponto de coleta GRÁTIS e depois aloque trabalhadores. Sem cidades, sem save, sem turnos canônicos.",
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
        _workerToJob.ItemSelected += _ =>
        {
            if (_workerLab is not null) UpdateWorkerLabHud();
        };

        _workerInstallSiteButton = new Button
        {
            Text = "1. ATIVAR INSTALAÇÃO PRIMITIVA (GRÁTIS)",
            Disabled = true
        };
        _workerInstallSiteButton.Pressed += InstallWorkerLabSite;
        lab.AddChild(_workerInstallSiteButton);

        var quantity = new HBoxContainer();
        quantity.AddChild(new Label { Text = "Transferir:" });
        _workerAmount = new SpinBox { MinValue = 1, MaxValue = 8, Step = 1, Value = 3 };
        quantity.AddChild(_workerAmount);
        lab.AddChild(quantity);

        _workerMoveButton = new Button
        {
            Text = "2. ALOCAR TRABALHADORES NA INSTALAÇÃO",
            Disabled = true
        };
        _workerMoveButton.Pressed += MoveWorkerLabWorkers;
        lab.AddChild(_workerMoveButton);

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

    private void StartWorkerLab()
    {
        if (_tacticalPreview is null || _workerLabStatus is null)
            return;
        try
        {
            // The existing production renderer is G(3,0)->G(18,0), not the
            // G(2,0)->G(12,0) used by the isolated VS03/VS04 demos.
            // Explicit instance parameters prevent invalid cross-world IDs.
            _workerLab = new EconomicPhysicalTacticalWorkerSession(
                20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0),
                requirePrimitiveSites: true);
            _workerOrigin = _workerLab.InitialTile;
            _workerDestination = null;
            var focus = _workerLab.DisplayBucket(_workerLab.InitialTile);
            _tacticalPreview.Open(focus.Value);
            if (!_tacticalPreview.HasVisiblePhysicalTile(_workerLab.InitialTile.FineStrategicCellId.Value))
                throw new InvalidOperationException("Worker origin is not visible in its physical map.");
            UpdateWorkerLabHud();
            GD.Print($"GA-VS06-SITES-LAB-READY fine=18 coarse=3 source={_workerOrigin.Value.FineStrategicCellId.Value} primitive_sites={_workerLab.PrimitiveSites.Count} workers={_workerLab.TotalWorkers} canonical_worldstate_untouched=true");
        }
        catch (Exception ex)
        {
            _workerLab = null;
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

    private void InstallWorkerLabSite()
    {
        if (_workerLab is null || !_workerDestination.HasValue ||
            _workerToJob is null || _workerLabStatus is null) return;
        try
        {
            var activity = (EconomicActivityKind)_workerToJob.GetSelectedId();
            _workerLab.InstallPrimitiveSite(_workerDestination.Value, activity);
            UpdateWorkerLabHud();
            GD.Print($"GA-VS06-PRIMITIVE-SITE-ACTIVATED tile={_workerDestination.Value.FineStrategicCellId.Value} job={activity} revision={_workerLab.Revision} workers={_workerLab.TotalWorkers} expense=unmodeled");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            _workerLabStatus.Text = "INSTALAÇÃO RECUSADA: " + ex.Message;
        }
    }

    private void MoveWorkerLabWorkers()
    {
        if (_workerLab is null || _workerOrigin is null || _workerDestination is null ||
            _workerAmount is null || _workerFromJob is null || _workerToJob is null)
            return;
        try
        {
            _workerLab.MoveWorkers(_workerOrigin.Value, _workerDestination.Value,
                (EconomicActivityKind)_workerFromJob.GetSelectedId(),
                (EconomicActivityKind)_workerToJob.GetSelectedId(),
                checked((ulong)_workerAmount.Value));
            _workerOrigin = _workerDestination;
            _workerDestination = null;
            UpdateWorkerLabHud();
            GD.Print($"GA-VS06-WORKERS-ASSIGNED revision={_workerLab.Revision} workers={_workerLab.TotalWorkers} primitive_sites={_workerLab.PrimitiveSites.Count} occupied={_workerLab.Assignments.Select(x => x.TileId).Distinct().Count()} city_entity=false canonical_worldstate_untouched=true");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or OverflowException)
        {
            if (_workerLabStatus is not null)
                _workerLabStatus.Text = "TRANSFERÊNCIA RECUSADA (sem alterar o estado): " + ex.Message;
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
        var targetJob = _workerToJob is null
            ? EconomicActivityKind.Agriculture
            : (EconomicActivityKind)_workerToJob.GetSelectedId();
        var siteAlreadyInstalled = _workerDestination.HasValue &&
            _workerLab.HasPrimitiveSite(_workerDestination.Value, targetJob);
        var canInstall = _workerDestination.HasValue &&
            _workerLab.CanInstallPrimitiveSite(_workerDestination.Value, targetJob);
        var destination = _workerDestination.HasValue
            ? $"Tile {_workerDestination.Value.FineStrategicCellId.Value}: " +
                (siteAlreadyInstalled ? "instalação pronta, pode alocar" :
                 canInstall ? "sem instalação: ATIVAR primeiro (grátis)" :
                 "terreno sem potencial para esta atividade")
            : "Clique num vizinho contornado em verde-lima";
        _workerLabStatus.Text =
            $"Origem tile {origin.FineStrategicCellId.Value} | {jobs}\n" +
            $"Destino: {destination}\n" +
            $"Trabalhadores globais: {_workerLab.TotalWorkers} | " +
            $"tiles ocupados: {_workerLab.Assignments.Select(x => x.TileId).Distinct().Count()} | " +
            $"revisão: {_workerLab.Revision} | instalações primitivas: {_workerLab.PrimitiveSites.Count}\n" +
            $"Consumo alimentar na origem (prévia): {demand / 1_000_000.0:F3}\n" +
            "Vagas/capacidade e curvas de investimento ainda NÃO modeladas. Sem estoque, mercado ou save.";
        if (_workerInstallSiteButton is not null)
            _workerInstallSiteButton.Disabled = !_workerDestination.HasValue ||
                !canInstall || siteAlreadyInstalled;
        if (_workerMoveButton is not null)
            _workerMoveButton.Disabled = !_workerDestination.HasValue ||
                !siteAlreadyInstalled;
    }
}
