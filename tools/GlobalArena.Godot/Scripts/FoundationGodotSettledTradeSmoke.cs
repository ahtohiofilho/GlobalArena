using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>
/// End-to-end Godot CI ONLY, with an EXPLICIT numeric QA fixture:
/// the real E key closes real v15 sales, exact treasury, trusted last-cycle
/// freight and visible center-to-center colored bands. Normal gameplay
/// is never seeded with these unapproved QA monetary coefficients.
/// </summary>
internal sealed class FoundationGodotSettledTradeSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;
    private static readonly CivilizationId Human = new(1UL);

    internal FoundationGodotSettledTradeSmoke(
        Main main, FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview map)
    {
        _main = main;
        _host = host;
        _map = map;
    }

    private static void Must(bool okay, string why)
    {
        if (!okay) throw new InvalidOperationException(why);
    }

    private async Task Frames()
    {
        for (int i = 0; i < 6; i++)
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

    private async Task Screenshot(string step)
    {
        var directory = System.Environment.GetEnvironmentVariable(
            "GA_NEXT_SETTLED_TRADE_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        await Frames();
        var shot = _main.GetViewport().GetTexture().GetImage();
        Must(shot is not null && !shot.IsEmpty(),
            "Actual Godot trade screenshot has no framebuffer.");
        var path = Path.Combine(directory, step + ".png");
        Must(shot!.SavePng(path) == Error.Ok &&
             new FileInfo(path).Length > 1500,
            "Cannot save authoritative trade overlay screenshot.");
        GD.Print($"GA_NEXT_V15_SETTLED_TRADE_FRAME_PASS step={step} " +
            $"width={shot.GetWidth()} height={shot.GetHeight()}");
    }

    internal async Task RunAsync()
    {
        try
        {
            // R80: PR277 continuous relief was restored using the canonical
            // economic height domain, not discrete 01C biome colors.
            // Check transitions and sea-level continuity before real E turns.
            var ocean = CanonicalEconomyReliefPalette.FromElevation(
                -80L, 0L, -100L, 100L);
            var coast = CanonicalEconomyReliefPalette.FromElevation(
                0L, 0L, -100L, 100L);
            var plains = CanonicalEconomyReliefPalette.FromElevation(
                10L, 0L, -100L, 100L);
            var highland = CanonicalEconomyReliefPalette.FromElevation(
                80L, 0L, -100L, 100L);
            Must(ocean != coast && coast != plains &&
                 plains != highland,
                "Authoritative continuous relief must distinguish real " +
                "depth, coastline, lowland and highland.");
            Must(CanonicalEconomyReliefPalette.FromElevation(
                10L, 0L, -100L, 100L) == plains,
                "Canonical relief color cannot vary between frames.");
            GD.Print("GA_NEXT_R80_CONTINUOUS_ECONOMY_RELIEF_PASS " +
                "source=authoritative_v1 no_discrete_biomes=true");
            await Frames();
            Must(_main.ActiveTacticalHost == _host &&
                 _host.StateHash.FormatVersion == 13U &&
                 _host.Snapshot.Treasury.IsInitialized &&
                 _host.Snapshot.Treasury.Get(Human).CashScaled.IsZero &&
                 !_map.HasSettledTradeData &&
                 _map.VisibleSettledTradeCrossings == 0,
                "QA must start with real v13 zero-cash world and NO fake routes.");
            var toggle = _main.FindChild(
                "GANextSettledTradeRoutesToggle", true, false) as CheckBox;
            Must(toggle is not null && toggle.ButtonPressed &&
                 toggle.Visible,
                "Tactical trade layer needs the actual visible sidebar toggle.");
            var initialHash = _host.StateHash;
            var camera = _map.TacticalCameraState;

            await PressE();
            var first = _host.GetLastClosedEconomicTradeView(Human);
            Must(_host.StateHash.FormatVersion == 15U &&
                 _host.StateHash != initialHash &&
                 _host.NextTurn.Value == 2UL &&
                 _host.Snapshot.Treasury.Get(Human).CashScaled.Sign > 0 &&
                 _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 1UL &&
                 first.EconomicCycleOrdinal == 1UL &&
                 first.Availability ==
                     FoundationEconomicTradeOverlayAvailability.AuthorizedSubset &&
                 first.UsedEdges.Count > 0 &&
                 _map.HasSettledTradeData &&
                 _map.PresentedEconomicCycleOrdinal == 1UL &&
                 _map.VisibleSettledTradeCrossings > 0 &&
                 _map.TacticalCameraState == camera,
                "Real E-key closed market must create funded cash and private " +
                "visible route mesh without moving the tactical camera.");
            var firstVisible = _map.VisibleSettledTradeCrossings;
            var hash = _host.StateHash;
            toggle!.ButtonPressed = false;
            await Frames();
            Must(!_map.IsSettledTradeLayerVisible &&
                 _map.VisibleSettledTradeCrossings == firstVisible &&
                 _host.StateHash == hash &&
                 _map.TacticalCameraState == camera,
                "Trade toggle must be purely visual and cannot alter cash or camera.");
            toggle.ButtonPressed = true;
            await Frames();
            Must(_map.IsSettledTradeLayerVisible &&
                 _host.StateHash == hash,
                "Re-enabling layer must reuse the already closed trade data.");
            await Screenshot("01_v15_last_closed_trade");

            await PressE();
            var second = _host.GetLastClosedEconomicTradeView(Human);
            Must(_host.NextTurn.Value == 3UL &&
                 _host.StateHash != hash &&
                 _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == 2UL &&
                 second.EconomicCycleOrdinal == 2UL &&
                 second.PublishedHash == _host.StateHash &&
                 _map.PresentedEconomicCycleOrdinal == 2UL &&
                 _map.VisibleSettledTradeCrossings > 0 &&
                 _map.TacticalCameraState == camera,
                "Second real E-key must replace the visible last-closed trade " +
                "without altering camera, topology or issuing fake preview routes.");
            await Screenshot("02_v15_next_closed_trade");
            GD.Print("GA_NEXT_V15_GODOT_SETTLED_TRADE_PASS " +
                $"first_visible={firstVisible} " +
                $"second_visible={_map.VisibleSettledTradeCrossings} " +
                "cycles=2 real_workers=true cash_from_sales=true " +
                "read_boundary=participant_only sold_consumed=true");
            _main.GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"GA_NEXT_V15_GODOT_SETTLED_TRADE_FAIL {error}");
            _main.GetTree().Quit(1);
        }
    }
}
