using System;
using System.Linq;
using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Godot;

/// <summary>
/// GA-UI-ECON-CYCLE-01: minimal tactical-right-rail physical flow. This is a
/// real canonical stock and worker-custody laboratory, not a cash market.
/// Default layout is read-only and compact; actions are opt-in and collapsed.
/// The map viewport is never a child of the laboratory.
/// </summary>
public partial class Main
{
    private Label? _physicalFlowSummary;
    private Label? _physicalFlowActionResult;
    private VBoxContainer? _physicalFlowActions;
    private Button? _physicalFlowToggle;
    private OptionButton? _physicalFlowCommodity;
    private OptionButton? _physicalFlowBeneficiary;
    private OptionButton? _physicalFlowCommitment;
    private SpinBox? _physicalFlowAmount;
    private Button? _physicalFlowOfferButton;
    private Button? _physicalFlowRetainButton;
    private Button? _physicalFlowUseButton;
    private Button? _physicalFlowReleaseButton;
    private ulong? _physicalFlowStatusTile;

    private static string FlowQuantity(System.Numerics.BigInteger raw) =>
        ((double)raw / 1_000_000d).ToString("F3");

    private static string CommodityTitle(uint commodity) => commodity switch
    {
        1U => "Alimento",
        2U => "Pedra",
        3U => "Madeira",
        _ => $"Item {commodity}"
    };

    private static string ActivityTitle(EconomicActivityKind activity) => activity switch
    {
        EconomicActivityKind.Agriculture => "Agricultura",
        EconomicActivityKind.Forestry => "Madeira",
        EconomicActivityKind.Mining => "Pedra",
        _ => activity.ToString()
    };

    private void InitializePhysicalFlowHud(VBoxContainer laboratory)
    {
        var root = new VBoxContainer { Name = "GAPhysicalFlowRoot" };
        root.AddThemeConstantOverride("separation", 2);
        laboratory.AddChild(root);

        var title = new Label
        {
            Name = "GAPhysicalFlowTitle",
            Text = "MATERIAIS · FLUXO FÍSICO DO TILE"
        };
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", new Color("#d4e9f3"));
        root.AddChild(title);

        _physicalFlowSummary = new Label
        {
            Name = "GAPhysicalFlowSummary",
            Text = "Selecione um tile para observar os estoques reais.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        root.AddChild(_physicalFlowSummary);

        _physicalFlowToggle = new Button
        {
            Name = "GAPhysicalFlowToggle",
            Text = "ABRIR OPERAÇÕES FÍSICAS (LAB)",
            ToggleMode = true,
            ButtonPressed = false
        };
        root.AddChild(_physicalFlowToggle);
        _physicalFlowActions = new VBoxContainer
        {
            Name = "GAPhysicalFlowActions",
            Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowActions.AddThemeConstantOverride("separation", 3);
        root.AddChild(_physicalFlowActions);
        var editor = _physicalFlowActions;
        _physicalFlowToggle.Toggled += expanded =>
        {
            editor.Visible = expanded;
            _physicalFlowToggle.Text = expanded
                ? "FECHAR OPERAÇÕES FÍSICAS (LAB)"
                : "ABRIR OPERAÇÕES FÍSICAS (LAB)";
            UpdatePhysicalFlowHud();
        };

        editor.AddChild(new Label
        {
            Text = "Oferta/retencão utiliza estoque real. Uso material é local, não venda.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });

        var commodityRow = new HBoxContainer();
        commodityRow.AddThemeConstantOverride("separation", 4);
        editor.AddChild(commodityRow);
        commodityRow.AddChild(new Label { Text = "Material:" });
        _physicalFlowCommodity = new OptionButton
        {
            Name = "GAPhysicalFlowCommodity",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowCommodity.AddItem("Madeira", 3);
        _physicalFlowCommodity.AddItem("Pedra", 2);
        _physicalFlowCommodity.Select(0);
        _physicalFlowCommodity.ItemSelected += _ => UpdatePhysicalFlowHud();
        commodityRow.AddChild(_physicalFlowCommodity);

        var amountRow = new HBoxContainer();
        amountRow.AddThemeConstantOverride("separation", 4);
        editor.AddChild(amountRow);
        amountRow.AddChild(new Label { Text = "Quantidade:" });
        _physicalFlowAmount = new SpinBox
        {
            Name = "GAPhysicalFlowAmount",
            MinValue = 0.001,
            MaxValue = 1_000_000,
            Step = 0.001,
            Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowAmount.ValueChanged += _ => UpdatePhysicalFlowButtons();
        amountRow.AddChild(_physicalFlowAmount);

        var commitRow = new HBoxContainer();
        commitRow.AddThemeConstantOverride("separation", 4);
        editor.AddChild(commitRow);
        _physicalFlowOfferButton = new Button
        {
            Name = "GAPhysicalFlowOffer",
            Text = "RESERVAR OFERTA",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowOfferButton.Pressed += () =>
            CommitPhysicalFlow(CanonicalPhysicalCommitmentKind.MarketOffer);
        commitRow.AddChild(_physicalFlowOfferButton);
        _physicalFlowRetainButton = new Button
        {
            Name = "GAPhysicalFlowRetain",
            Text = "RETER MATERIAL",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowRetainButton.Pressed += () =>
            CommitPhysicalFlow(CanonicalPhysicalCommitmentKind.DeliberateRetention);
        commitRow.AddChild(_physicalFlowRetainButton);

        editor.AddChild(new Label { Text = "Compromisso ativo no tile:" });
        _physicalFlowCommitment = new OptionButton
        {
            Name = "GAPhysicalFlowSelectedCommitment",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowCommitment.ItemSelected += _ => UpdatePhysicalFlowButtons();
        editor.AddChild(_physicalFlowCommitment);

        var workRow = new HBoxContainer();
        workRow.AddThemeConstantOverride("separation", 4);
        editor.AddChild(workRow);
        workRow.AddChild(new Label { Text = "Grupo:" });
        _physicalFlowBeneficiary = new OptionButton
        {
            Name = "GAPhysicalFlowBeneficiary",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        foreach (var (label, id) in new[]
        {
            ("Madeira", EconomicActivityKind.Forestry),
            ("Pedra", EconomicActivityKind.Mining),
            ("Agricultura", EconomicActivityKind.Agriculture)
        })
            _physicalFlowBeneficiary.AddItem(label, (int)id);
        _physicalFlowBeneficiary.Select(0);
        _physicalFlowBeneficiary.ItemSelected += _ => UpdatePhysicalFlowButtons();
        workRow.AddChild(_physicalFlowBeneficiary);

        var destinationRow = new HBoxContainer();
        destinationRow.AddThemeConstantOverride("separation", 4);
        editor.AddChild(destinationRow);
        _physicalFlowUseButton = new Button
        {
            Name = "GAPhysicalFlowUse",
            Text = "UTILIZAR NO GRUPO",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowUseButton.Pressed += ExecutePhysicalFlowUse;
        destinationRow.AddChild(_physicalFlowUseButton);
        _physicalFlowReleaseButton = new Button
        {
            Name = "GAPhysicalFlowRelease",
            Text = "LIBERAR RESERVA",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _physicalFlowReleaseButton.Pressed += ReleasePhysicalFlow;
        destinationRow.AddChild(_physicalFlowReleaseButton);

        _physicalFlowActionResult = new Label
        {
            Name = "GAPhysicalFlowActionResult",
            Text = "Sem operação nesta sessão.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _physicalFlowActionResult.AddThemeColorOverride(
            "font_color", new Color("#b9d9ca"));
        editor.AddChild(_physicalFlowActionResult);
    }

    private void UpdatePhysicalFlowHud()
    {
        if (_physicalFlowSummary is null || _canonicalPhysicalLab is null ||
            !_workerOrigin.HasValue)
            return;
        var game = _canonicalPhysicalLab;
        var tile = _workerOrigin.Value;
        CanonicalTacticalPhysicalFlowReport report;
        try
        {
            report = CanonicalTacticalPhysicalFlowProjection.Read(game, tile);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                          ArgumentException or OverflowException)
        {
            _physicalFlowSummary.Text = "Fluxo físico indisponível: " + exception.Message;
            UpdatePhysicalFlowButtons();
            return;
        }

        if (_physicalFlowStatusTile != report.FineTile)
        {
            _physicalFlowStatusTile = report.FineTile;
            if (_physicalFlowActionResult is not null)
                _physicalFlowActionResult.Text = "Sem operação neste tile nesta sessão de inspeção.";
        }

        var cycle = report.ClosedEconomicCycles == 0
            ? "CICLO: ainda não houve fechamento econômico."
            : $"CICLO GLOBAL {report.ClosedEconomicCycles} (turno {report.LastClosedCycleTurn}) " +
              $"· produzido {report.LastGlobalProducedRaw / 1_000_000.0:F2} (itens somados) " +
              $"· alimento consumido {report.LastGlobalFoodConsumedRaw / 1_000_000.0:F2} " +
              $"· falta {report.LastGlobalFoodUnmetRaw / 1_000_000.0:F2}";
        var lastUse = report.LastExecutedLocalUse;
        var destination = lastUse is null ? "USO: nenhum material comprometido aplicado."
            : $"USO LOCAL: {CommodityTitle(lastUse.Commodity)} " +
              $"{lastUse.UsedRaw / 1_000_000.0:F3} · " +
              (lastUse.ProvesLocalWorkforceGroup
                  ? $"{ActivityTitle(lastUse.BeneficiaryActivity!.Value)}, " +
                    $"{lastUse.BeneficiaryWorkers} pessoas"
                  : "grupo não identificado (modo histórico)") +
              " · sem venda.";
        _physicalFlowSummary.Text =
            $"TILE {report.FineTile} · {report.ResidentPeople} pessoas\n" +
            $"LIVRE: alim. {FlowQuantity(report.FreeFoodRaw)} · " +
            $"mad. {FlowQuantity(report.FreeWoodRaw)} · " +
            $"ped. {FlowQuantity(report.FreeStoneRaw)}\n" +
            $"VINCULADO: ofertas {report.ActiveMarketOffers} " +
            $"({FlowQuantity(report.CommittedMarketRaw)}) · " +
            $"retenções {report.ActiveRetentions} " +
            $"({FlowQuantity(report.DeliberatelyRetainedRaw)})\n" +
            destination + "\n" + cycle;

        if (_physicalFlowCommitment is not null)
        {
            var selection = _physicalFlowCommitment.GetSelectedId();
            _physicalFlowCommitment.Clear();
            foreach (var row in report.ActiveLocalCommitments)
            {
                var label = row.Kind == CanonicalPhysicalCommitmentKind.MarketOffer
                    ? "OFERTA" : "RETENÇÃO";
                _physicalFlowCommitment.AddItem(
                    $"#{row.ActionOrdinal} · {label} · " +
                    $"{CommodityTitle(row.Commodity)} " +
                    $"{row.RawQuantity / 1_000_000.0:F3}",
                    row.ActionOrdinal);
            }
            var selected = _physicalFlowCommitment.GetItemIndex(selection);
            if (selected < 0 && _physicalFlowCommitment.ItemCount > 0)
                selected = 0;
            if (selected >= 0)
                _physicalFlowCommitment.Select(selected);
        }

        if (_physicalFlowCommodity is not null && _physicalFlowAmount is not null)
        {
            var commodity = (uint)_physicalFlowCommodity.GetSelectedId();
            var freeRaw = commodity == 3U
                ? report.FreeWoodRaw : report.FreeStoneRaw;
            var upper = Math.Max(0.001, (double)freeRaw / 1_000_000d);
            _physicalFlowAmount.MaxValue = upper;
            if (_physicalFlowAmount.Value > upper)
                _physicalFlowAmount.Value = upper;
        }
        UpdatePhysicalFlowButtons();
    }

    private void UpdatePhysicalFlowButtons()
    {
        if (_physicalFlowOfferButton is null || _physicalFlowRetainButton is null ||
            _physicalFlowUseButton is null || _physicalFlowReleaseButton is null ||
            _physicalFlowAmount is null || _physicalFlowCommitment is null ||
            _physicalFlowCommodity is null || _physicalFlowBeneficiary is null ||
            _canonicalPhysicalLab is null || !_workerOrigin.HasValue)
            return;
        var game = _canonicalPhysicalLab;
        var tile = _workerOrigin.Value;
        var good = new CommodityId((uint)_physicalFlowCommodity.GetSelectedId());
        var raw = checked((ulong)Math.Round(
            _physicalFlowAmount.Value * 1_000_000d, MidpointRounding.ToEven));
        var canReserve = game.WorkersAt(tile) > 0UL &&
            raw > 0UL && game.StockAt(tile, good) >= raw;
        _physicalFlowOfferButton.Disabled = !canReserve;
        _physicalFlowRetainButton.Disabled = !canReserve;

        var selected = _physicalFlowCommitment.GetSelectedId();
        var commitment = game.PhysicalStockCommitments.FirstOrDefault(x =>
            x.ActionOrdinal == selected &&
            x.FineTile == tile.FineStrategicCellId.Value && x.IsLive);
        _physicalFlowReleaseButton.Disabled = commitment is null;
        var activity = (EconomicActivityKind)_physicalFlowBeneficiary.GetSelectedId();
        _physicalFlowUseButton.Disabled = commitment is null ||
            commitment.Kind != CanonicalPhysicalCommitmentKind.MarketOffer ||
            game.State.Allocations.All(x =>
                x.TileId != tile || x.Activity != activity || x.Workers == 0UL);
    }

    private void CommitPhysicalFlow(CanonicalPhysicalCommitmentKind kind)
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            !_workerOrigin.HasValue || _physicalFlowCommodity is null ||
            _physicalFlowAmount is null || _physicalFlowActionResult is null)
            return;
        try
        {
            var commodity = new CommodityId((uint)_physicalFlowCommodity.GetSelectedId());
            var raw = checked((ulong)Math.Round(
                _physicalFlowAmount.Value * 1_000_000d, MidpointRounding.ToEven));
            var receipt = _canonicalPhysicalLab.CommitPhysicalStock(
                _workerOrigin.Value, commodity, kind, raw);
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            if (_physicalFlowCommitment is not null)
            {
                var index = _physicalFlowCommitment.GetItemIndex(receipt.ActionOrdinal);
                if (index >= 0) _physicalFlowCommitment.Select(index);
            }
            UpdatePhysicalFlowButtons();
            _physicalFlowActionResult.Text =
                $"COMPROMISSO #{receipt.ActionOrdinal} executado · " +
                $"{CommodityTitle(receipt.Commodity)} " +
                $"{receipt.RawQuantity / 1_000_000.0:F3} retirado do estoque livre.";
            GD.Print($"GA_UI_PHYSICAL_FLOW_COMMITTED ordinal={receipt.ActionOrdinal} " +
                     $"kind={kind} tile={receipt.FineTile} raw={receipt.RawQuantity} " +
                     $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                          ArgumentException or OverflowException)
        {
            _physicalFlowActionResult.Text = "Compromisso recusado: " + exception.Message;
        }
    }

    private void ExecutePhysicalFlowUse()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _physicalFlowCommitment is null || _physicalFlowBeneficiary is null ||
            _physicalFlowActionResult is null)
            return;
        try
        {
            var activity = (EconomicActivityKind)_physicalFlowBeneficiary.GetSelectedId();
            var use = _canonicalPhysicalLab.UseCommittedLocalMaterialForWorkforce(
                _physicalFlowCommitment.GetSelectedId(), activity,
                CanonicalMaterialDispositionKind.FinalPhysicalUse);
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _physicalFlowActionResult.Text =
                $"USO FÍSICO REGISTRADO #{use.ActionOrdinal}: " +
                $"{CommodityTitle(use.Commodity)} " +
                $"{use.UsedRaw / 1_000_000.0:F3} · " +
                $"{ActivityTitle(activity)} ({use.BeneficiaryWorkers} pessoas). " +
                "Não houve venda nem nova construção.";
            GD.Print($"GA_UI_PHYSICAL_FLOW_USED action={use.ActionOrdinal} " +
                     $"source_commitment={use.SourceCommitmentActionOrdinal} " +
                     $"workers={use.BeneficiaryWorkers} tile={use.FineTile} " +
                     $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                          ArgumentException or OverflowException)
        {
            _physicalFlowActionResult.Text = "Uso recusado: " + exception.Message;
        }
    }

    private void ReleasePhysicalFlow()
    {
        if (_canonicalPhysicalLab is null || _canonicalSession is null ||
            _physicalFlowCommitment is null || _physicalFlowActionResult is null)
            return;
        try
        {
            var row = _canonicalPhysicalLab.ReleasePhysicalStockCommitment(
                _physicalFlowCommitment.GetSelectedId());
            AttachRuntimeState(_canonicalSession.Snapshot);
            UpdateWorkerLabHud();
            _physicalFlowActionResult.Text =
                $"COMPROMISSO #{row.ActionOrdinal} liberado: " +
                $"{CommodityTitle(row.Commodity)} " +
                $"{row.RawQuantity / 1_000_000.0:F3} devolvido ao estoque livre.";
            GD.Print($"GA_UI_PHYSICAL_FLOW_RELEASED ordinal={row.ActionOrdinal} " +
                     $"raw={row.RawQuantity} tile={row.FineTile} " +
                     $"hash={_canonicalPhysicalLab.CurrentHash.HexDigest}");
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                          ArgumentException or OverflowException)
        {
            _physicalFlowActionResult.Text = "Liberação recusada: " + exception.Message;
        }
    }
}
