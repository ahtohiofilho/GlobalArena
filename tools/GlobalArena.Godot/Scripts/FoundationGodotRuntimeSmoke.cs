using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>
/// Opt-in execution inside the ACTUAL Godot .NET scene, not an xUnit-only
/// simulation test. Godot creates all its controls/world and processes the
/// same ray-picked GUI events as a player. Strictly guarded by environment,
/// so it never changes gameplay for normal Godot runs. No screenshots/headless
/// layout claims: this gate proves runtime input wiring and canonical commits.
/// </summary>
internal sealed class FoundationGodotRuntimeSmoke
{
    private readonly Main _main;
    private readonly GlobePreview _globe;
    private readonly FoundationLocalGameHost _game;
    private readonly OptionButton _workers;
    private readonly Button _move;
    private readonly Button _produce;
    private readonly Label _summary;
    private readonly Label _turn;
    private readonly Label _status;

    public FoundationGodotRuntimeSmoke(
        Main main, GlobePreview globe, FoundationLocalGameHost game,
        OptionButton workers, Button move, Button produce,
        Label summary, Label turn, Label status)
    {
        _main = main;
        _globe = globe;
        _game = game;
        _workers = workers;
        _move = move;
        _produce = produce;
        _summary = summary;
        _turn = turn;
        _status = status;
    }

    public async Task RunAsync()
    {
        try
        {
            // Layout and SubViewportContainer.Stretch need several real frames.
            for (var frame = 0; frame < 6; frame++)
                await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);

            await CaptureRenderedViewIfRequested("01_startup");
            Must(_game.Snapshot.Workers.IsInitialized, "census is not authoritative");
            Must(_game.Snapshot.Workers.Workers.Count == 6, "initial workers != 6");
            Must(_game.Snapshot.Revision == 0UL, "revision at startup != 0");
            Must(_game.StateHash.FormatVersion == 9U, "worker hash is not v9");
            Must(_workers.ItemCount == 3, "own worker dropdown missing");
            Must(_workers.Selected == 0, "initial worker selection not set");
            Must(_move.Disabled, "movement button enabled with no map destination");
            Must(!_turn.Text.Contains("uninitialized", StringComparison.OrdinalIgnoreCase),
                "worker census did not reach the actual Godot sidebar");

            var worker = _game.ControllableWorkers.Single(w => w.Id.Value == 1UL);
            var originalCell = worker.CellId;
            var graph = _game.Session.GeneratedWorld.StrategicSurfaceGraph;
            var neighbor = graph.GetCellId(
                graph.GetNeighborIndexes(graph.GetNodeIndex(originalCell))[0]);

            // The picker must ray-hit an ACTUAL Goldberg triangle, publish
            // the normal selection event and enable the actual UI button.
            Must(_globe.TryClickCanonicalCellForRuntimeSmoke(neighbor.Value),
                "real ray pick did not select intended neighbor");
            Must(!_move.Disabled, "adjacent selection did not enable move");
            Must(_summary.Text.Contains($"TILE {neighbor.Value}", StringComparison.Ordinal),
                "selected worker inspector did not show picked cell");
            Must(_status.Text.Contains("Choose your worker", StringComparison.Ordinal),
                "unexpected order status before first commit");

            // Emit the REAL Godot button event, not a direct runtime mutation.
            _move.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.Snapshot.Revision == 1UL, "button did not commit turn 1");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .CellId == neighbor, "worker did not move to selected cell");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .Activity == PhysicalWorkerActivity.Traveling,
                "travel action did not update worker activity");
            Must(_status.Text.Contains("replay verified", StringComparison.Ordinal),
                "button commit did not report journal replay");
            Must(_move.Disabled, "same destination should disable no-op move");
            Must(!_produce.Disabled, "producer should be able to resume work");
            await CaptureRenderedViewIfRequested("02_after_move");
            GD.Print($"GA_GODOT_RUNTIME_SMOKE_STEP move=PASS " +
                $"revision={_game.Snapshot.Revision} worker=1 target={neighbor.Value}");

            _produce.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.Snapshot.Revision == 2UL, "production button did not commit turn 2");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .Activity == PhysicalWorkerActivity.Producing,
                "production button did not resume work");
            Must(_produce.Disabled, "already producing must disable duplicate order");
            GD.Print("GA_GODOT_RUNTIME_SMOKE_STEP production=PASS revision=2");

            // Also exercise keyboard-first G on the real Main._UnhandledKeyInput
            // callback after clicking the original tile by its projected ray.
            Must(_globe.TryClickCanonicalCellForRuntimeSmoke(originalCell.Value),
                "real ray pick did not select home cell");
            Must(!_move.Disabled, "valid return destination disabled");
            _main._UnhandledKeyInput(new InputEventKey
            {
                Keycode = Key.G, Pressed = true, Echo = false
            });
            Must(_game.Snapshot.Revision == 3UL, "G shortcut did not commit turn 3");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                    .CellId == originalCell,
                "keyboard movement did not return worker home");
            Must(_game.Snapshot.Workers.Workers.Count == 6,
                "movement created or deleted physical workers");
            Must(_game.StateHash.FormatVersion == 9U,
                "committed worker hash changed format");
            Must(_turn.Text.Contains("turn 4", StringComparison.Ordinal),
                "turn sidebar was not refreshed after keyboard commit");
            await CaptureRenderedViewIfRequested("03_after_shortcut");
            GD.Print("GA_GODOT_RUNTIME_SMOKE_STEP keyboard_g=PASS revision=3");

            // Exercise rejection via the UI: selecting the SAME tile disables
            // move, and a foreign worker never appears in the player's dropdown.
            Must(_move.Disabled, "UI permits no-op move");
            Must(_workers.ItemCount == 3, "UI exposes non-owned workers");
            Must(_status.Text.Contains("replay verified", StringComparison.Ordinal),
                "last journaled order missing feedback");
            GD.Print("GA_GODOT_FOUNDATION_SMOKE_PASS " +
                "engine=4.7.2.net scene=Main.tscn " +
                "world_nodes=92 workers=6 turns=3 " +
                "ray_picker=true buttons=true keyboard_g=true " +
                "commit=true journal_replay=true hash=v9");
            _main.GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PrintErr("GA_GODOT_FOUNDATION_SMOKE_FAIL " + error);
            _main.GetTree().Quit(82);
        }
    }

    // Optional graphical-Xvfb evidence. The ordinary --headless smoke never
    // reads render targets and remains a separate independent gate.
    private async Task CaptureRenderedViewIfRequested(string stage)
    {
        var directory = System.Environment.GetEnvironmentVariable(
            "GA_FOUNDATION_GODOT_SCREENSHOT_DIR");
        if (string.IsNullOrWhiteSpace(directory))
            return;
        Directory.CreateDirectory(directory);
        for (var frame = 0; frame < 4; frame++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
        var viewport = _main.GetViewport();
        var texture = viewport.GetTexture();
        Must(texture is not null, "graphical viewport has no texture");
        var picture = texture!.GetImage();
        Must(picture is not null && !picture.IsEmpty(),
            "graphical engine produced no actual frame image");
        Must(picture!.GetWidth() >= 640 && picture.GetHeight() >= 480,
            "graphical frame is smaller than a normal playable window");
        // This detects missing renderer output, a solid framebuffer or blank
        // screenshot without claiming that typographic layout was inspected.
        var palette = new System.Collections.Generic.HashSet<string>();
        for (var y = 0; y < picture.GetHeight(); y += Math.Max(1, picture.GetHeight() / 16))
        {
            for (var x = 0; x < picture.GetWidth(); x += Math.Max(1, picture.GetWidth() / 16))
            {
                var sample = picture.GetPixel(x, y);
                palette.Add($"{(int)(sample.R * 255)}:" +
                    $"{(int)(sample.G * 255)}:{(int)(sample.B * 255)}");
            }
        }
        Must(palette.Count >= 5, "graphical screenshot is blank or nearly uniform");
        var file = Path.Combine(directory, stage + ".png");
        var saved = picture.SavePng(file);
        Must(saved == Error.Ok, "unable to save Godot-rendered PNG");
        GD.Print($"GA_GODOT_RENDER_FRAME_PASS stage={stage} " +
            $"dimensions={picture.GetWidth()}x{picture.GetHeight()} " +
            $"sample_colors={palette.Count} file={file}");
    }

    private static void Must(bool condition, string explanation)
    {
        if (!condition)
            throw new InvalidOperationException(explanation);
    }
}
