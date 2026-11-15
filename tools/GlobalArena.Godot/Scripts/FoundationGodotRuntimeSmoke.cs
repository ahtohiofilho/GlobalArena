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
    private readonly Button _endTurn;
    private readonly Button _cancel;
    private readonly Label _pending;
    private readonly Label _summary;
    private readonly Label _turn;
    private readonly Label _status;

    public FoundationGodotRuntimeSmoke(
        Main main, GlobePreview globe, FoundationLocalGameHost game,
        OptionButton workers, Button move, Button produce,
        Button endTurn, Button cancel, Label pending,
        Label summary, Label turn, Label status)
    {
        _main = main;
        _globe = globe;
        _game = game;
        _workers = workers;
        _move = move;
        _produce = produce;
        _endTurn = endTurn;
        _cancel = cancel;
        _pending = pending;
        _summary = summary;
        _turn = turn;
        _status = status;
    }

    public async Task RunAsync()
    {
        try
        {
            for (var frame = 0; frame < 6; frame++)
                await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);

            await CaptureRenderedViewIfRequested("01_startup");
            Must(_game.Snapshot.Workers.IsInitialized, "census not authoritative");
            Must(_game.Snapshot.Workers.Workers.Count == 6, "initial workers != 6");
            Must(_game.Snapshot.Revision == 0UL, "startup revision != 0");
            Must(_game.StateHash.FormatVersion == 9U, "worker hash is not v9");
            Must(_workers.ItemCount == 3, "own worker menu missing");
            Must(_workers.Selected == 0, "initial worker not selected");
            Must(_move.Disabled, "move button enabled without destination");
            Must(!_endTurn.Disabled, "empty turn cannot be closed");
            Must(_game.PendingOrderCount == 0, "game started with pending commands");
            Must(_pending.Text.Contains("No orders queued", StringComparison.Ordinal),
                "initial pending label incorrect");

            var initial = _game.Snapshot;
            var initialHash = _game.StateHash;
            var graph = _game.Session.GeneratedWorld.StrategicSurfaceGraph;
            var w1 = _game.ControllableWorkers.Single(w => w.Id.Value == 1UL);
            var w2 = _game.ControllableWorkers.Single(w => w.Id.Value == 2UL);
            Must(w1.CellId == w2.CellId, "fixture workers must share initial tile");
            var origin = w1.CellId;
            var neighbor = graph.GetCellId(
                graph.GetNeighborIndexes(graph.GetNodeIndex(origin))[0]);

            // Map-selected tile from a *real ray/triangle hit*; no fake
            // territory event or injected destination ID.
            Must(_globe.TryClickCanonicalCellForRuntimeSmoke(neighbor.Value),
                "ray picking did not select the intended neighboring tile");
            Must(!_move.Disabled, "neighbor selection did not enable queue move");
            Must(_summary.Text.Contains($"TILE {neighbor.Value}", StringComparison.Ordinal),
                "strategic inspector did not follow real selection");

            // Worker #1 queues move. Absolutely no authoritative state may
            // change until the user presses the explicit END TURN button.
            _move.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.PendingOrderCount == 1, "worker #1 was not queued");
            Must(_game.Snapshot.Revision == 0UL &&
                ReferenceEquals(initial, _game.Snapshot) &&
                _game.StateHash == initialHash, "queue mutated WorldState");
            Must(_pending.Text.Contains("#1", StringComparison.Ordinal),
                "UI did not show first pending order");

            // Choose second physical worker through actual OptionButton signal,
            // then queue an independent move to the same valid neighbor.
            _workers.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
            Must(_workers.Selected == 1, "worker dropdown failed to select #2");
            Must(!_move.Disabled, "second valid worker move disabled");
            _move.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.PendingOrderCount == 2, "second worker order was lost");
            Must(_game.Snapshot.Revision == 0UL &&
                _game.StateHash == initialHash, "two queued orders mutated authority");
            Must(_pending.Text.Contains("#1", StringComparison.Ordinal) &&
                _pending.Text.Contains("#2", StringComparison.Ordinal),
                "pending plan is not visible for both workers");

            // Cancel and requeue via *real* UI buttons; only the pending intent
            // changes, never an already-published snapshot.
            Must(!_cancel.Disabled, "selected worker cannot cancel queued intent");
            _cancel.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.PendingOrderCount == 1, "UI cancel did not remove worker #2");
            Must(_game.Snapshot.Revision == 0UL, "cancel changed authority");
            _move.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.PendingOrderCount == 2, "requeue after cancel failed");
            GD.Print("GA_GODOT_RUNTIME_SMOKE_STEP batch_queued=PASS " +
                "pending=2 revision=0 journal_events=0");
            await CaptureRenderedViewIfRequested("02_queued");

            // ONE Godot button press resolves both commands in ONE turn.
            _endTurn.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.PendingOrderCount == 0, "end turn left stale intentions");
            Must(_game.NextTurn.Value == 2UL, "two commands consumed two turns");
            Must(_game.Snapshot.Revision == 2UL,
                "two executed worker events did not produce two revisions");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                .CellId == neighbor, "worker #1 not moved");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 2UL)
                .CellId == neighbor, "worker #2 not moved");
            Must(_game.Snapshot.Workers.Workers.Where(w =>
                w.Id.Value is 1UL or 2UL).All(w =>
                w.Activity == PhysicalWorkerActivity.Traveling),
                "move did not stop production for traveling workers");
            Must(_status.Text.Contains("2 worker orders", StringComparison.Ordinal)
                && _status.Text.Contains("journal replay verified", StringComparison.Ordinal),
                "one-turn batch/journal confirmation absent from GUI");
            Must(_move.Disabled, "same-tile movement should be disabled after commit");
            Must(_workers.ItemCount == 3, "foreign workers leaked into controls");
            await CaptureRenderedViewIfRequested("03_after_batch_move");
            GD.Print("GA_GODOT_RUNTIME_SMOKE_STEP move=PASS " +
                "turn=1 workers=2 revision=2 replay=true");

            // Separate turn: actual Godot production button queues work for
            // worker #1; closing the turn applies it.
            _workers.EmitSignal(OptionButton.SignalName.ItemSelected, 0L);
            Must(_workers.Selected == 0, "cannot reselect worker #1");
            Must(!_produce.Disabled, "traveling producer cannot queue production");
            _produce.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.PendingOrderCount == 1 &&
                _game.Snapshot.Revision == 2UL, "production queue mutated snapshot");
            _endTurn.EmitSignal(BaseButton.SignalName.Pressed);
            Must(_game.Snapshot.Revision == 3UL &&
                _game.NextTurn.Value == 3UL, "production turn did not close correctly");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                .Activity == PhysicalWorkerActivity.Producing,
                "Godot P button did not resume production");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 2UL)
                .Activity == PhysicalWorkerActivity.Traveling,
                "other worker activity was corrupted");
            Must(_produce.Disabled, "already producing should disable duplicate order");
            GD.Print("GA_GODOT_RUNTIME_SMOKE_STEP production=PASS " +
                "turn=2 revision=3 replay=true");

            // Keyboard-first G queues the last move, E closes this third turn.
            Must(_globe.TryClickCanonicalCellForRuntimeSmoke(origin.Value),
                "ray click failed to reselect home tile");
            Must(!_move.Disabled, "valid return move disabled");
            _main._UnhandledKeyInput(new InputEventKey
            {
                Keycode = Key.G, Pressed = true, Echo = false
            });
            Must(_game.PendingOrderCount == 1 && _game.Snapshot.Revision == 3UL,
                "G key bypassed pending-intent boundary");
            _main._UnhandledKeyInput(new InputEventKey
            {
                Keycode = Key.E, Pressed = true, Echo = false
            });
            Must(_game.PendingOrderCount == 0, "E did not close pending turn");
            Must(_game.NextTurn.Value == 4UL && _game.Snapshot.Revision == 4UL,
                "G/E consumed the wrong turn or event revisions");
            Must(_game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
                .CellId == origin, "keyboard move did not return worker home");
            Must(_game.Snapshot.Workers.Workers.Count == 6,
                "movement invented or destroyed workers");
            Must(_game.StateHash.FormatVersion == 9U, "hash is not v9");
            Must(_turn.Text.Contains("turn 4", StringComparison.Ordinal),
                "turn sidebar did not update");
            Must(_move.Disabled && _workers.ItemCount == 3,
                "final UI controls do not reflect canonical state");
            Must(_status.Text.Contains("journal replay verified", StringComparison.Ordinal),
                "keyboard turn replay confirmation missing");
            await CaptureRenderedViewIfRequested("04_after_shortcut");
            GD.Print("GA_GODOT_RUNTIME_SMOKE_STEP keyboard_g=PASS " +
                "turn=3 revision=4 replay=true");

            GD.Print("GA_GODOT_FOUNDATION_SMOKE_PASS " +
                "engine=4.7.2.net scene=Main.tscn " +
                "world_nodes=92 workers=6 turns=3 events=4 " +
                "queue=true cancel=true batch_workers=2 " +
                "ray_picker=true buttons=true keyboard_g=true keyboard_e=true " +
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
