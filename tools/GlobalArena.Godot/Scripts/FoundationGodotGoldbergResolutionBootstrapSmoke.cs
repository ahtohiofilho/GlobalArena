using System;
using Godot;
using GlobalArena.Simulation.Foundation;
using GlobalArena.Runtime;
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

    private async Task PressEconomicTurnKey()
    {
        var viewport = _main.GetViewport();
        viewport.PushInput(new InputEventKey
        {
            Keycode = Key.E, Pressed = true, Echo = false
        }, inLocalCoords: true);
        for (int frame = 0; frame < 8; frame++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
        viewport.PushInput(new InputEventKey
        {
            Keycode = Key.E, Pressed = false, Echo = false
        }, inLocalCoords: true);
        for (int frame = 0; frame < 8; frame++)
            await _main.ToSignal(_main.GetTree(), SceneTree.SignalName.ProcessFrame);
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

            // R103: the alternative resolution must settle REAL economic
            // cycles, not merely paint an alternative spherical mesh.
            // Drive the same player E key as the G18 regression, and query
            // the actual committed host and per-participant read boundary.
            var human = new CivilizationId(1UL);
            var initialCycle = _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal;
            if (initialCycle != 0UL)
                throw new InvalidOperationException(
                    "Alternative G12 opening must start before economic cycle 1.");
            await PressEconomicTurnKey();
            var first = _host.GetLastClosedEconomicTradeView(human);
            var hashOne = _host.StateHash;
            if (_host.NextTurn.Value != 2UL ||
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != 1UL ||
                hashOne == beforeHash ||
                _host.Snapshot.Revision <= beforeRevision ||
                first.Availability !=
                    FoundationEconomicTradeOverlayAvailability.AuthorizedSubset ||
                first.EconomicCycleOrdinal != 1UL ||
                first.PublishedHash != hashOne ||
                _map.PresentedEconomicCycleOrdinal != 1UL ||
                _main.VisibleSphericalTradeCrossings != first.UsedEdges.Count ||
                _host.Snapshot.Treasury.Get(human).CashScaled.Sign <= 0)
                throw new InvalidOperationException(
                    "First G12 cycle must settle actual goods, cash, private " +
                    "audited freight and authoritative replay.");

            await PressEconomicTurnKey();
            var second = _host.GetLastClosedEconomicTradeView(human);
            if (_host.NextTurn.Value != 3UL ||
                _host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != 2UL ||
                _host.StateHash == hashOne ||
                _host.Snapshot.Revision <= beforeRevision ||
                second.Availability !=
                    FoundationEconomicTradeOverlayAvailability.AuthorizedSubset ||
                second.EconomicCycleOrdinal != 2UL ||
                second.PublishedHash != _host.StateHash ||
                _map.PresentedEconomicCycleOrdinal != 2UL ||
                _main.VisibleSphericalTradeCrossings != second.UsedEdges.Count ||
                second.FineTopology != fine ||
                _host.Incidence.Refinement.FineParameters != fine)
                throw new InvalidOperationException(
                    "Second G12 economic cycle must preserve signed fine " +
                    "topology, real participant freight, replay and chronology.");

            var postSettlementHash = _host.StateHash;
            _map.Open(focus.Value);
            if (_host.StateHash != postSettlementHash)
                throw new InvalidOperationException(
                    "G12 camera focus must never modify settled world hash.");
            GD.Print("GA_NEXT_R103_G12_ECONOMIC_TWO_CYCLES_PASS " +
                $"cycles=2 fine=G({fine.M},{fine.N}) " +
                $"physical_tiles={physical.FineElevationRaw.Count} " +
                $"first_edges={first.UsedEdges.Count} " +
                $"second_edges={second.UsedEdges.Count} " +
                "real_market=true private_routes=true signed_hashes=true " +
                "journal_replay=true economic_state_independent_of_camera=true");
            _main.GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"GA_NEXT_R102_ALTERNATE_GOLDBERG_BOOTSTRAP_FAIL {error}");
            _main.GetTree().Quit(1);
        }
    }
}
