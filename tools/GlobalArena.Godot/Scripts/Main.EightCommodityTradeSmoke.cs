using Godot;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    private async Task ExerciseEightCommodityTradeFixture(FoundationTacticalHumanAiTurnHost host, string mode)
    {
        bool remote = mode == "remote";
        var own = host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization).ToArray();
        var foreign = host.Snapshot.Workers.Workers.Where(w => w.Owner != _humanCivilization).ToArray();
        var source = own.First().TacticalTileId!.Value;
        var destination = foreign.First().TacticalTileId!.Value;
        if (own.Length != 3 || foreign.Length != 3 ||
            own.Any(w => w.TacticalTileId != source) || foreign.Any(w => w.TacticalTileId != destination) ||
            (source != destination) != remote || !host.DefaultAutomaticBaseEnabled ||
            host.AgriculturalProduction?.BasicResources is null)
            throw new InvalidOperationException("Eight-commodity fixture census or configuration differs.");
        AssertCartographicPublication(host, 0UL);
        var opening = host.StateHash;
        // Real private planning: specialize existing human workers in wood
        // using the public queue API. Foreign workers retain automatic base.
        // Wood has sufficient real demand and safe freight on a ground edge.
        foreach (var worker in own)
            host.QueueHumanBaseActivity(worker.Owner, worker.Id,
                new FoundationWorkerBaseActivity(FoundationWorkerBaseMode.Manual, PhysicalWorkerOccupation.Forestry));
        if (host.StateHash != opening) throw new InvalidOperationException("Private base orders changed committed state.");

        for (ulong cycle = 1; cycle <= 2; cycle++)
        {
            var before = host.StateHash;
            var viewport = GetViewport();
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = true, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = false, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
            var local = host.GetLastClosedLocalEconomics(_humanCivilization, source);
            if (host.StateHash == before || host.NextTurn.Value != cycle + 1UL ||
                host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != cycle ||
                view.PublishedHash != host.StateHash || local is null ||
                local.Commodities.Count != 8 || !local.Commodities.Any(row => row.ProducedMicro > 0UL && row.SoldMicro > 0UL) ||
                _localOrderStatus?.Text.Contains("journal replay OK") != true)
                throw new InvalidOperationException($"Fixture did not close real eight-commodity sales: {mode} cycle={cycle}");
            AssertCartographicPublication(host, cycle);
            if (remote)
            {
                if (!view.UsedEdges.Any(e => e.TotalCargoMicro > 0UL &&
                    ((e.FirstTile == source && e.SecondTile == destination) ||
                     (e.FirstTile == destination && e.SecondTile == source))) ||
                    !local.Commodities.Any(row => row.ExportedMicro > 0UL) ||
                    _tacticalPreview!.VisibleSettledTradeCrossings <= 0 ||
                    _strategicGlobe!.VisibleSphericalSettledTradeCrossings <= 0)
                    throw new InvalidOperationException($"Remote fixture must publish real positive exports and visible adjacent freight: cycle={cycle} edges={view.UsedEdges.Count}");
            }
            else if (view.UsedEdges.Count != 0 || local.Commodities.Any(row => row.ExportedMicro != 0UL || row.ImportedMicro != 0UL))
                throw new InvalidOperationException("Co-located economic activity must not create trade edges.");
            FocusCityProductionSmokeSite(host, source);
            var expectedStatus = remote ? $"{view.UsedEdges.Count} own trade crossings | Cargo" : "No own trade crossings in last closed cycle";
            if (_cityTradeStatus?.Text.Contains(expectedStatus, StringComparison.Ordinal) != true)
                throw new InvalidOperationException("City trade status does not distinguish actual published crossings from zero crossings.");
            ExerciseCityConnectionTradeReadout(host, source, cycle);
            GD.Print($"GA_NEXT_R235F21_TRADE_STATUS_PASS mode={mode} cycle={cycle} edges={view.UsedEdges.Count}");
            GD.Print($"GA_NEXT_R2313_TRADE_CYCLE mode={mode} cycle={cycle} edges={view.UsedEdges.Count} replay=true");
        }
        GD.Print($"GA_NEXT_R2313_TRADE_FIXTURE_PASS mode={mode} cycles=2 catalog=8 real_e=true");
    }
}
