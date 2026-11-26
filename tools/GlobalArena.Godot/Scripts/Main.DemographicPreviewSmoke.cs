using Godot;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    // Typed, replay-verified receipt from the real E handler. UI label text
    // is presentation, not the authority for the demographic assertion.
    private FoundationTacticalCollectiveReceipt? _lastDemographicPreviewReceipt;

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
            _lastDemographicPreviewReceipt = null;
            var viewport = GetViewport();
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = true, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = false, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var current = host.Snapshot.Workers.Workers;
            var previousIds = before.Select(w => w.Id).ToHashSet();
            var newborns = current.Where(w => !previousIds.Contains(w.Id)).ToArray();
            void Require(bool ok, string condition)
            {
                if (!ok) throw new InvalidOperationException($"Demographic close cycle={cycle}: {condition}");
            }
            var receipt = _lastDemographicPreviewReceipt ??
                throw new InvalidOperationException($"Missing real E-close receipt at cycle {cycle}.");
            Require(receipt.ReplayMatched, "journal replay did not match");
            Require(receipt.Turn.Value == cycle, $"receipt turn={receipt.Turn.Value}");
            Require(host.NextTurn.Value == cycle + 1UL, $"next turn={host.NextTurn.Value}");
            Require(host.StateHash != hash && receipt.Hash == host.StateHash,
                "committed hash did not change or receipt differs");
            Require(host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal == cycle,
                $"settled cycle={host.Snapshot.Treasury.LastSettledEconomicCycleOrdinal}");
            Require(receipt.TemporalEconomicSettlement is not null, "missing temporal sale");
            Require(current.Count == before.Length + owners.Length,
                $"census opening={before.Length} current={current.Count} owners={owners.Length}");
            Require(newborns.Length == owners.Length, $"newborn count={newborns.Length}");
            Require(owners.All(owner => newborns.Count(w => w.Owner == owner) == 1),
                "births not distributed one per surviving owner");
            Require(receipt.BirthBatch is not null &&
                receipt.BirthBatch.Newborns.OrderBy(w => w.Id.Value).SequenceEqual(newborns.OrderBy(w => w.Id.Value)),
                "birth event differs from actual committed newborns");
            Require(receipt.PersistentTimeline is not null &&
                receipt.PersistentTimeline.Slices.All(s => previousIds.Contains(s.Worker)),
                "newborn worked before birth");
            foreach (var child in newborns)
            {
                Require(child.NatalNode is not null && child.NatalNode == child.TacticalTileId,
                    $"worker {child.Id.Value}: natal node differs");
                var queue = host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == child.Id);
                Require(queue.Orders.Count == 0 && queue.Owner == child.Owner,
                    $"worker {child.Id.Value}: nonempty or wrong-owner newborn queue");
            }
            Require(host.Snapshot.WorkerOrders.Queues.Count == current.Count,
                $"queues={host.Snapshot.WorkerOrders.Queues.Count} census={current.Count}");
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
