using System;
using Godot;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

/// <summary>
/// R102 actual Godot startup probe: verifies that alternate class-I
/// resolutions are chosen BEFORE the world, globe and tactical preview are
/// created. Never substitutes a visual-only map for economic authority.
/// </summary>
internal sealed class FoundationGodotGoldbergResolutionBootstrapSmoke
{
    private readonly Main _main;
    private readonly FoundationTacticalHumanAiTurnHost _host;
    private readonly TacticalContextPreview _map;

    internal FoundationGodotGoldbergResolutionBootstrapSmoke(
        Main main, FoundationTacticalHumanAiTurnHost host,
        TacticalContextPreview map)
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
                await _main.ToSignal(_main.GetTree(),
                    SceneTree.SignalName.ProcessFrame);

            var coarse = _main.StartupCoarseParameters;
            var fine = new GoldbergParameters(checked(coarse.M * 6), 0);
            var geometry = _host.Session.GeneratedWorld;
            var physical = _map.AuthoritativeFineEconomy ??
                throw new InvalidOperationException(
                    "Signed fine-first physical field not supplied to Godot.");
            var beforeHash = _host.StateHash;
            var beforeRevision = _host.Snapshot.Revision;

            if (coarse != new GoldbergParameters(2, 0) ||
                fine != new GoldbergParameters(12, 0) ||
                geometry.Request.StrategicParameters != coarse ||
                _main.GlobeCoarseParameters != coarse ||
                _main.ActiveFineParameters != fine ||
                _host.Incidence.Refinement.CoarseParameters != coarse ||
                _host.Incidence.Refinement.FineParameters != fine ||
                physical.CoarseParameters != coarse ||
                physical.FineParameters != fine ||
                physical.FineElevationRaw.Count != 1442 ||
                physical.FineTemperatureRaw.Count != 1442)
                throw new InvalidOperationException(
                    "G2/G12 must be the SAME physical geography in host, " +
                    "globe, renderer and signed economy.");

            var focus = geometry.StrategicSphericalGeometry.Cells[0].CellId;
            _map.Open(focus.Value);
            if (_map.ActiveStrategicFocus != focus.Value ||
                _host.StateHash != beforeHash ||
                _host.Snapshot.Revision != beforeRevision)
                throw new InvalidOperationException(
                    "Opening an alternate resolution changed world authority.");
            GD.Print("GA_NEXT_R102_ALTERNATE_GOLDBERG_BOOTSTRAP_PASS " +
                "coarse=G(2,0) fine=G(12,0) tiles=1442 " +
                "signed_geography=true spherical_globe=true " +
                "tactical_chart=true simulation_unchanged=true");
            _main.GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"GA_NEXT_R102_ALTERNATE_GOLDBERG_BOOTSTRAP_FAIL {error}");
            _main.GetTree().Quit(1);
        }
    }
}
