using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>
/// Real Godot startup/keyboard/turn smoke for the new tactical-first shell.
/// It deliberately does NOT assert physical worker movement on G(18,0),
/// which awaits a separately versioned authoritative worker-location model.
/// The PR #361 strategic-order smoke remains a distinct regression contract.
/// </summary>
internal sealed class FoundationGodotWorkspaceSmoke
{
    private readonly Main _main;
    private readonly FoundationHumanAiLocalPreview _game;

    public FoundationGodotWorkspaceSmoke(
        Main main, FoundationHumanAiLocalPreview game)
    {
        _main = main;
        _game = game;
    }

    public async Task RunAsync()
    {
        try
        {
            for (var frame = 0; frame < 6; frame++)
                await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
            var tactical = _main.FindChild(
                "GANextTacticalViewport", recursive: true, owned: false)
                as TacticalContextPreview;
            var globe = _main.FindChild(
                "GANextPlanetGlobe", recursive: true, owned: false)
                as GlobePreview;
            var planet = _main.GetNode<Control>("GANextPlanetWindow");
            var operations = _main.GetNode<Control>("GANextOperationsWindow");
            var canvas = _main.FindChild(
                "GANextWideTacticalCanvas", recursive: true, owned: false)
                as PanelContainer;
            var atlasPanel = _main.GetNode<Control>(
                "GANextPlanetWindow/GANextPlanetPanel");
            var operationsPanel = _main.GetNode<Control>(
                "GANextOperationsWindow/GANextOperationsPanel");
            Must(_main.Theme is not null, "GA design system Theme missing");
            Must(canvas is not null && canvas.Size.X > canvas.Size.Y * 1.18f,
                "tactical viewport is constrained to an obsolete 1:1 square");
            Must(tactical is not null && tactical.Size.X > tactical.Size.Y * 1.18f,
                "tactical surface did not inherit landscape viewport size");
            Must(canvas!.GetThemeStylebox("panel") is StyleBoxFlat,
                "map framing must use centralized visual foundation style");
            Must(((PanelContainer)atlasPanel).GetThemeStylebox("panel") is StyleBoxFlat,
                "planet window missing shared design-system frame");
            Must(((PanelContainer)operationsPanel).GetThemeStylebox("panel") is StyleBoxFlat,
                "operations window missing shared design-system frame");
            GD.Print($"GA_VISUAL_FOUNDATION_01_GATE rectangle={canvas.Size.X:F0}x{canvas.Size.Y:F0} " +
                "theme=true windows=2 production_style=true");
            Must(tactical is not null && tactical.Visible,
                "the local player did not enter the tactical viewport");
            Must(tactical!.CanonicalPhysicalIncidenceMap is not null,
                "no canonical G(18,0) physical incidence was opened");
            Must(globe is not null && !globe.Visible,
                "planet was shown permanently instead of as a window");
            Must(!planet.Visible && !operations.Visible,
                "temporary windows should start closed");
            Must(_game.PendingOrderCount == 0 &&
                 _game.Snapshot.Revision == 0UL,
                "tactical startup changed gameplay state");
            var hash = _game.StateHash;
            await CaptureRenderedViewIfRequested("01_tactical_start");

            Press(Key.M);
            Must(planet.Visible && globe!.Visible && tactical.Visible,
                "M did not open a floating planetary navigation window");
            Must(_game.StateHash == hash, "M mutated the authoritative hash");
            await CaptureRenderedViewIfRequested("02_planet_map");
            Press(Key.M);
            Must(!planet.Visible && !globe!.Visible && tactical.Visible,
                "M did not restore the uninterrupted tactical viewport");

            Press(Key.Tab);
            Must(operations.Visible && !planet.Visible,
                "TAB did not reveal the operational panel as a window");
            await CaptureRenderedViewIfRequested("03_operations_panel");
            Press(Key.Escape);
            Must(!operations.Visible && tactical.Visible,
                "ESC did not close the contextual window");

            // G cannot submit an old strategic destination while in tactical
            // mode. No claim of authoritative physical-tile movement is made.
            Press(Key.G);
            Must(_game.PendingOrderCount == 0 &&
                 _game.Snapshot.Revision == 0UL && _game.StateHash == hash,
                "tactical G illegally queued a coarse Goldberg worker order");

            Press(Key.E);
            Must(_game.NextTurn.Value == 2UL &&
                 _game.Snapshot.Revision == 1UL &&
                 _game.LastAiEventCount == 1 &&
                 _game.Snapshot.Workers.Workers.Count == 6 &&
                 _game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
                     .Activity == PhysicalWorkerActivity.Producing,
                "E did not close the existing authoritative human/AI turn");
            Must(_game.LastTurnReceipt is { ReplayMatched: true },
                "tactical-first turn did not replay identically");
            await CaptureRenderedViewIfRequested("04_after_ai_turn");
            GD.Print("GA_NEXT_TACTICAL_WORKSPACE_PASS " +
                "entry=tactical planet=M panels=TAB close=ESC " +
                "g_tactical_locked=true human_ai_turn=true " +
                "ai_events=1 replay=true hash=v9");
            _main.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PrintErr("GA_NEXT_TACTICAL_WORKSPACE_FAIL " + error);
            _main.GetTree().Quit(83);
        }
    }

    // Linux Xvfb/GL graphical gate: images come from the actual Godot
    // framebuffer, not a synthetic mock. Headless test skips this explicitly.
    private async Task CaptureRenderedViewIfRequested(string stage)
    {
        var directory = System.Environment.GetEnvironmentVariable("GA_NEXT_WORKSPACE_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        for (var i = 0; i < 4; i++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
        var frame = _main.GetViewport().GetTexture().GetImage();
        Must(frame is not null && !frame.IsEmpty(),
            "no Godot framebuffer for tactical workspace");
        Must(frame!.GetWidth() >= 640 && frame.GetHeight() >= 480,
            "unexpected tactical frame size");
        var palette = new System.Collections.Generic.HashSet<string>();
        for (var y = 0; y < frame.GetHeight(); y += Math.Max(1, frame.GetHeight() / 16))
            for (var x = 0; x < frame.GetWidth(); x += Math.Max(1, frame.GetWidth() / 16))
            {
                var sample = frame.GetPixel(x, y);
                palette.Add($"{(int)(sample.R * 255)}:{(int)(sample.G * 255)}:" +
                    $"{(int)(sample.B * 255)}");
            }
        Must(palette.Count >= 5, "tactical frame is blank or nearly uniform");
        var filename = Path.Combine(directory, stage + ".png");
        Must(frame.SavePng(filename) == Error.Ok, "Godot PNG write failed");
        GD.Print($"GA_NEXT_RENDER_FRAME_PASS stage={stage} " +
            $"dimensions={frame.GetWidth()}x{frame.GetHeight()} palette={palette.Count}");
    }

    private void Press(Key code)
    {
        _main._UnhandledKeyInput(new InputEventKey
        {
            Keycode = code, Pressed = true, Echo = false
        });
    }

    private static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
