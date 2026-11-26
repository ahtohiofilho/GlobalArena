using Godot;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

public partial class Main
{
    // QA continuation only: preserve the first 50 reference hashes, then
    // use the ordinary human command interface to expand on real ground.
    private async Task ExerciseDispersedProfileScale(FoundationTacticalHumanAiTurnHost host)
    {
        if (System.Environment.GetEnvironmentVariable("GA_NEXT_R232D_DISPERSED_PROFILE") != "1")
            return;
        if (System.Environment.GetEnvironmentVariable("GA_NEXT_R232_TURN_PROFILE") != "1" ||
            host.NextTurn.Value != 51UL || host.Snapshot.Workers.Workers.Count != 101)
            throw new InvalidOperationException("Dispersed profiling requires the complete 50-cycle baseline.");
        const int targetSites = 8;
        const ulong finalCycle = 100UL;
        var fine = _r123FineGeography ?? throw new InvalidOperationException("Missing signed geography.");
        var viewport = GetViewport();
        var owners = host.Snapshot.Workers.Workers.Select(w => w.Owner).Distinct().OrderBy(o => o.Value).ToArray();
        int OwnSites() => host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization)
            .Select(w => w.TacticalTileId!.Value).Distinct().Count();
        int moveOrders = 0, travelCloses = 0, stableCloses = 0, maxEdges = 0;
        bool positiveDispersedTrade = false;
        async Task Close(bool expansion)
        {
            var opening = host.Snapshot;
            var cycle = host.NextTurn.Value;
            var existing = opening.Workers.Workers.Select(w => w.Id).ToHashSet();
            _lastDemographicPreviewReceipt = null;
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = true, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = false, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var receipt = _lastDemographicPreviewReceipt ?? throw new InvalidOperationException("Missing real dispersed close.");
            var batch = receipt.BirthBatch ?? throw new InvalidOperationException("Missing dispersed births.");
            var timeline = receipt.PersistentTimeline ?? throw new InvalidOperationException("Missing dispersed calendar.");
            var sale = receipt.TemporalEconomicSettlement ?? throw new InvalidOperationException("Missing dispersed settlement.");
            var snapshot = host.Snapshot;
            var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
            if (receipt.Turn.Value != cycle || !receipt.ReplayMatched || receipt.Hash != host.StateHash ||
                view.PublishedHash != receipt.Hash ||
                snapshot.Workers.Workers.Count != opening.Workers.Workers.Count + owners.Length ||
                snapshot.WorkerOrders.Queues.Count != snapshot.Workers.Workers.Count ||
                !batch.Newborns.Select(w => w.Owner).OrderBy(o => o.Value).SequenceEqual(owners) ||
                timeline.Slices.Any(s => !existing.Contains(s.Worker)) ||
                !sale.UsedBoundedSafeProgressiveRoutes || sale.SellerNetScaled.Sign < 0 ||
                sale.Credits.Any(c => c.SellerNetScaled.Sign < 0))
                throw new InvalidOperationException($"Dispersed close {cycle} violated the economic or demographic contract.");
            foreach (var child in batch.Newborns)
                if (!snapshot.Workers.Workers.Any(w => existing.Contains(w.Id) && w.Owner == child.Owner &&
                    w.TacticalTileId == child.NatalNode && w.Activity != PhysicalWorkerActivity.Traveling &&
                    !snapshot.EdgeTravels.Pending.Any(p => p.Worker == w.Id)) ||
                    snapshot.WorkerOrders.Queues.Single(q => q.Worker == child.Id).Orders.Count != 0)
                    throw new InvalidOperationException("Invalid dispersed birth node or newborn queue.");
            var ownIds = opening.Workers.Workers.Where(w => w.Owner == _humanCivilization).Select(w => w.Id).ToHashSet();
            foreach (var node in timeline.Slices.Where(s => ownIds.Contains(s.Worker)).GroupBy(s => s.Node))
            {
                ulong expected = node.Aggregate(0UL, (sum, s) => checked(sum + (ulong)s.DurationMicro));
                var local = host.GetLastClosedLocalEconomics(_humanCivilization, node.Key);
                if (local is null || local.Commodities.Count != 8 || local.Commodities.Any(r => r.DemandMicro != expected))
                    throw new InvalidOperationException("Dispersed commodity demand differs from physical worker-time.");
            }
            AssertCartographicPublication(host, cycle);
            bool traveled = timeline.Slices.Any(s => s.Activity == PhysicalWorkerActivity.Traveling);
            if (traveled) travelCloses++;
            if (!expansion)
            {
                if (traveled || OwnSites() < targetSites)
                    throw new InvalidOperationException("Stationary dispersed phase lost its sites or retained travel.");
                stableCloses++;
                positiveDispersedTrade |= view.UsedEdges.Count > 0 && sale.PhysicalFreightScaled.Sign > 0;
            }
            maxEdges = Math.Max(maxEdges, view.UsedEdges.Count);
            GD.Print($"GA_NEXT_R232D_CYCLE cycle={cycle} stage={(expansion ? "expansion" : "stationary")} own_sites={OwnSites()} workers={snapshot.Workers.Workers.Count} edges={view.UsedEdges.Count} traveled={traveled} replay=true");
        }

        while (OwnSites() < targetSites)
        {
            if (host.NextTurn.Value > 80UL) throw new InvalidOperationException("Expansion exceeded diagnostic horizon.");
            var opening = host.Snapshot;
            var occupied = opening.Workers.Workers.Select(w => w.TacticalTileId!.Value).ToHashSet();
            var orders = new List<(PhysicalWorkerId Worker, PhysicalTacticalTileId Target)>();
            int limit = Math.Min(3, targetSites - OwnSites());
            foreach (var site in opening.Workers.Workers.Where(w => w.Owner == _humanCivilization)
                .GroupBy(w => w.TacticalTileId!.Value).OrderBy(g => g.Key.FineStrategicCellId.Value))
            {
                var available = site.OrderBy(w => w.Id.Value).Skip(1).ToArray(); // keep one stationary
                int from = checked((int)site.Key.FineStrategicCellId.Value - 1);
                foreach (var adjacent in host.Incidence.FineTopology.Cells[from].AdjacentCellIds.OrderBy(c => c.Value))
                {
                    if (orders.Count == limit || available.Length == 0) break;
                    if (fine.CrossingAtIndices(from, checked((int)adjacent.Value - 1)) != FoundationFineTransportCrossingKind.Ground)
                        continue;
                    var target = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, adjacent);
                    if (!occupied.Add(target)) continue;
                    orders.Add((available[0].Id, target));
                    available = available.Skip(1).ToArray();
                }
                if (orders.Count == limit) break;
            }
            if (orders.Count == 0)
                throw new InvalidOperationException("No reachable unoccupied ground node for the next expansion wave.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var move in orders)
                host.QueueHumanPersistentEdit(_humanCivilization, new FoundationPersistentOrderEdit(
                    _humanCivilization, move.Worker, FoundationPersistentOrderEditKind.Insert,
                    Index: 0, OrderKind: FoundationWorkerOrderKind.Move, Destination: move.Target));
            watch.Stop();
            moveOrders += orders.Count;
            GD.Print($"GA_NEXT_R232D_WAVE cycle={host.NextTurn.Value} orders={orders.Count} queue_us={watch.ElapsedTicks * 1_000_000L / System.Diagnostics.Stopwatch.Frequency}");
            bool Arrived() => orders.All(o => host.Snapshot.Workers.Workers.Single(w => w.Id == o.Worker).TacticalTileId == o.Target &&
                !host.Snapshot.EdgeTravels.Pending.Any(p => p.Worker == o.Worker) &&
                host.Snapshot.WorkerOrders.Queues.Single(q => q.Worker == o.Worker).Orders.Count == 0);
            for (int step = 0; step < 8 && !Arrived(); step++) await Close(true);
            if (!Arrived()) throw new InvalidOperationException("Expansion order did not arrive within eight closes.");
        }
        while (host.NextTurn.Value <= finalCycle) await Close(false);
        if (host.Snapshot.Workers.Workers.Count != 201 || OwnSites() < targetSites ||
            moveOrders != targetSites - 1 || travelCloses == 0 || stableCloses < 10 || !positiveDispersedTrade)
            throw new InvalidOperationException("Incomplete dispersed profiling evidence.");
        GD.Print($"GA_NEXT_R232D_DISPERSED_PASS through={finalCycle} population=201 own_sites={OwnSites()} moves={moveOrders} travel_closes={travelCloses} stationary_closes={stableCloses} max_edges={maxEdges} positive_trade=true replay=true");
    }
}
