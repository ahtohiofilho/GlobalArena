using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    // Real normal preview: no worker relocations, no manual orders, no
    // additional commodities, no special market policy or invented sites.
    private async Task ExerciseDemographicPreview(FoundationTacticalHumanAiTurnHost host)
    {
        var map = _tacticalPreview ?? throw new InvalidOperationException("Missing demographic map.");
        var initial = host.Snapshot.Workers.Workers.ToArray();
        var owners = initial.Select(w => w.Owner).Distinct().ToArray();
        var home = initial.First(w => w.Owner == _humanCivilization).TacticalTileId!.Value;
        var homeId = home.FineStrategicCellId.Value;
        int Population() => host.Snapshot.Workers.Workers.Count(w => w.TacticalTileId == home);
        int openingPopulation = Population();
        var openingDiameter = map.PresentedSiteDiameter(homeId);
        if (openingDiameter != GaCartographicStyle.SiteDiameter(openingPopulation))
            throw new InvalidOperationException("Opening population dot is missing or incorrectly sized.");
        AssertCartographicPublication(host, 0UL);
        bool bandChanged = false;
        for (ulong cycle = 1; cycle <= 3; cycle++)
        {
            var before = host.Snapshot.Workers.Workers.ToArray();
            var hash = host.StateHash;
            var previousDiameter = map.PresentedSiteDiameter(homeId);
            var viewport = GetViewport();
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = true, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = false, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var current = host.Snapshot.Workers.Workers;
            var previousIds = before.Select(w => w.Id).ToHashSet();
            var newborns = current.Where(w => !previousIds.Contains(w.Id)).ToArray();
            if (host.StateHash == hash || host.NextTurn.Value != cycle + 1UL ||
                host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal != cycle ||
                current.Count != before.Length + owners.Length ||
                newborns.Length != owners.Length ||
                owners.Any(owner => newborns.Count(w => w.Owner == owner) != 1) ||
                newborns.Any(w => w.NatalNode is null || w.NatalNode != w.TacticalTileId ||
                    host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == w.Id).Orders.Count != 0) ||
                host.Snapshot.WorkerOrders.Queues.Count != current.Count ||
                _localOrderStatus?.Text.Contains("journal replay OK") != true)
                throw new InvalidOperationException($"Demographic close failed at cycle {cycle}.");
            // Births are after settlement; the preceding cycle's children
            // must have entered real automatic production by this close.
            foreach (var child in before.Where(w => !initial.Any(x => x.Id == w.Id)))
                if (current.Single(w => w.Id == child.Id).Activity != PhysicalWorkerActivity.Producing)
                    throw new InvalidOperationException("Newborn did not enter automatic local production.");
            var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
            if (view.PublishedHash != host.StateHash)
                throw new InvalidOperationException("Trade publication was not bound to final post-birth state.");
            AssertCartographicPublication(host, cycle);
            int population = Population();
            var diameter = map.PresentedSiteDiameter(homeId);
            if (diameter != GaCartographicStyle.SiteDiameter(population))
                throw new InvalidOperationException("Population marker did not update from committed census.");
            bandChanged |= diameter != previousDiameter;
            GD.Print($"GA_NEXT_R2315_DEMOGRAPHIC_CYCLE cycle={cycle} own={current.Count(w => w.Owner == _humanCivilization)} home_population={population} dot_px={diameter} authorized_trade_edges={view.UsedEdges.Count} replay=true");
        }
        if (!bandChanged || map.PresentedSiteDiameter(homeId) <= openingDiameter)
            throw new InvalidOperationException("Growing normal opening did not cross a visible population band.");
        GD.Print("GA_NEXT_R2315_DEMOGRAPHIC_PASS cycles=3 normal_opening=true actual_births=true atomic_queues=true dot_growth=true final_hash=true");
    }
}
