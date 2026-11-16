using System;
using System.Linq;
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

            Press(Key.M);
            Must(planet.Visible && globe!.Visible && tactical.Visible,
                "M did not open a floating planetary navigation window");
            Must(_game.StateHash == hash, "M mutated the authoritative hash");
            Press(Key.M);
            Must(!planet.Visible && !globe!.Visible && tactical.Visible,
                "M did not restore the uninterrupted tactical viewport");

            Press(Key.Tab);
            Must(operations.Visible && !planet.Visible,
                "TAB did not reveal the operational panel as a window");
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
