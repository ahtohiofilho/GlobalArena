using Godot;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

/// <summary>
/// R45 CI only: execute actual Godot pointer handlers over consecutive
/// strategic focus crossings with one held mouse gesture. No turns,
/// workers, prices, or game orders are modified by navigation.
/// </summary>
internal sealed class FoundationGodotContinuousNavigationSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;

    internal FoundationGodotContinuousNavigationSmoke(
        Main main, FoundationTacticalHumanAiTurnHost host, TacticalContextPreview map)
    {
        _main = main;
        _host = host;
        _map = map;
    }

    internal async Task RunAsync()
    {
        try
        {
            for (int frame = 0; frame < 8; frame++)
                await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);

            var priorHash = _host.StateHash;
            var priorTurn = _host.NextTurn;
            _map.ExerciseDualOrientationSmoke();
            _map.ExerciseContinuousNavigationSmoke();
            if (_host.StateHash != priorHash || _host.NextTurn != priorTurn)
                throw new InvalidOperationException(
                    "Navigation changed authoritative simulation hash or turn.");
            GD.Print("GA_NEXT_CONTINUOUS_NAVIGATION_PASS " +
                "multi_focus=true held_drag=true camera_stable=true " +
                "terrain_covered=true simulation_unchanged=true");
            _main.GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"GA_NEXT_CONTINUOUS_NAVIGATION_FAIL {exception}");
            _main.GetTree().Quit(1);
        }
    }
}
