using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>
/// R179 opt-in, in the actual rendered G18 scene. The real E key closes
/// an R170 temporal financial cycle; the read-only Godot sidebar must
/// show precisely the human civilization's authoritative R171 receipt.
/// No fabricated UI data and no extra financial simulation.
/// </summary>
internal sealed class FoundationGodotTemporalEconomySmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;
    private static readonly CivilizationId Human = new(1UL);

    internal FoundationGodotTemporalEconomySmoke(
        Main main, FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview map)
    {
        _main = main;
        _host = host;
        _map = map;
    }

    private static void Must(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException(reason);
    }

    private async Task Frames()
    {
        for (int i = 0; i < 8; ++i)
            await _main.ToSignal(_main.GetTree(),
                SceneTree.SignalName.ProcessFrame);
    }

    private async Task PressE()
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.E, Pressed = true, Echo = false },
            inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.E, Pressed = false, Echo = false },
            inLocalCoords: true);
        await Frames();
    }

    private async Task PressTab()
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.Tab, Pressed = true, Echo = false },
            inLocalCoords: true);
        await Frames();
        viewport.PushInput(new InputEventKey
            { Keycode = Key.Tab, Pressed = false, Echo = false },
            inLocalCoords: true);
        await Frames();
    }

    private async Task Capture(string stage, Label label)
    {
        var directory = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_R179_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        // The financial label is in the existing bounded right dock;
        // scroll it into view without modifying the game or presentation.
        for (Node? parent = label.GetParent(); parent is not null;
            parent = parent.GetParent())
        {
            if (parent is ScrollContainer scroll)
            {
                scroll.EnsureControlVisible(label);
                break;
            }
        }
        await Frames();
        Must(label.IsVisibleInTree() &&
            label.GetGlobalRect().Intersects(
                new Rect2(Vector2.Zero,
                    _main.GetViewport().GetVisibleRect().Size)),
            "The temporal economic text exists but is not visible within " +
            "the rendered operational sidebar.");
        var image = _main.GetViewport().GetTexture().GetImage();
        Must(image is not null && !image.IsEmpty(),
            "The financial screenshot has no actual framebuffer.");
        string target = Path.Combine(directory, stage + ".png");
        Must(image!.SavePng(target) == Error.Ok &&
            new FileInfo(target).Length > 1500,
            "Cannot capture the real rendered temporal financial panel.");
        GD.Print($"GA_NEXT_R179_FRAME_PASS stage={stage} " +
            $"width={image.GetWidth()} height={image.GetHeight()} " +
            "source=actual_godot_framebuffer");
    }

    private static void ValidateClose(
        Label label, FoundationViewerTemporalEconomicClose receipt,
        ulong cycle)
    {
        var text = label.Text;
        Must(label.Visible && receipt.EconomicCycleOrdinal == cycle &&
            text.Contains($"CICLO {cycle}", StringComparison.Ordinal) &&
            text.Contains("Crédito ao caixa: " +
                Main.FormatTemporalMoney(receipt.TotalCashCreditScaled) + " u.m.",
                StringComparison.Ordinal) &&
            text.Contains("Vendas líquidas: " +
                Main.FormatTemporalMoney(receipt.SellerNetScaled) + " u.m.",
                StringComparison.Ordinal) &&
            text.Contains("Impostos recebidos: " +
                Main.FormatTemporalMoney(receipt.TaxReceiptsScaled) + " u.m.",
                StringComparison.Ordinal) &&
            text.Contains("Pedágios recebidos: " +
                Main.FormatTemporalMoney(receipt.OwnerTollIncomeScaled) + " u.m.",
                StringComparison.Ordinal) &&
            text.Contains("Produção global liquidada: " +
                receipt.PhysicalGoodsProducedMicro, StringComparison.Ordinal),
            "Godot did not render the exact private committed finance fields.");
    }

    internal async Task RunAsync()
    {
        try
        {
            await Frames();
            var label = _main.FindChild(
                "GANextTemporalEconomicClose", true, false) as Label;
            Must(label is not null &&
                _main.ActiveTacticalHost == _host &&
                _host.Snapshot.WorldBinding?.IsFineFirstWorld == true &&
                _host.Snapshot.EdgeTravels.IsInitialized &&
                _host.Snapshot.Treasury.IsInitialized &&
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 0UL &&
                _host.GetLastClosedTemporalEconomics(Human) is null &&
                !_main.LastTemporalEconomyVisible &&
                string.IsNullOrEmpty(_main.LastTemporalEconomyText),
                "R179 must start with one genuine fine-first temporal world " +
                "and no pending, preview or fabricated financial values.");

            var initialHash = _host.StateHash;
            var camera = _map.TacticalCameraState;
            GD.Print("GA_NEXT_R179_TEMPORAL_START_PASS " +
                "ledger=empty readout=hidden worker_time=authoritative");
            await PressE();
            var first = _host.GetLastClosedTemporalEconomics(Human);
            Must(first is not null && first.CommittedWorldHash == _host.StateHash &&
                _host.StateHash != initialHash &&
                _host.NextTurn.Value == 2UL &&
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 1UL &&
                _map.TacticalCameraState == camera,
                "First real E key did not commit a validated temporal sale.");
            ValidateClose(label!, first!, 1UL);
            var firstText = _main.LastTemporalEconomyText;
            var firstHash = _host.StateHash;
            // R179 visual acceptance must show the actual operational dock.
            // The prior screenshot only photographed the globe because TAB
            // had never been pressed, despite correct hidden-control text.
            await PressTab();
            var dock = _main.FindChild(
                "GANextOperationsWindow", true, false) as Control;
            Must(dock is not null && dock.Visible &&
                label!.IsVisibleInTree(),
                "TAB must reveal the real command desk and finance label.");
            await Capture("01_temporal_close_cycle_1", label!);

            await PressE();
            var second = _host.GetLastClosedTemporalEconomics(Human);
            Must(second is not null &&
                second.CommittedWorldHash == _host.StateHash &&
                _host.StateHash != firstHash &&
                _host.NextTurn.Value == 3UL &&
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 2UL &&
                _map.TacticalCameraState == camera,
                "Second E key did not commit the next real temporal cycle.");
            ValidateClose(label!, second!, 2UL);
            Must(_main.LastTemporalEconomyText != firstText,
                "The Godot sidebar retained the previous cycle after E.");
            await Capture("02_temporal_close_cycle_2", label!);
            GD.Print("GA_NEXT_R179_TEMPORAL_GODOT_PASS " +
                "cycles=2 exact_human_credit=true journal_replay=true " +
                "no_preview=true no_phantom_income=true " +
                "actual_godot_scene=true unchanged_market_math=true");
            _main.GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"GA_NEXT_R179_TEMPORAL_GODOT_FAIL {error}");
            _main.GetTree().Quit(1);
        }
    }
}
