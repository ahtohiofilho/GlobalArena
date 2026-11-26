using Godot;
using GlobalArena.Runtime;
using GlobalArena.Runtime.PersistentOrders;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Godot;

public partial class Main
{
    // Typed, replay-verified receipt from the real E handler. UI label text
    // is presentation, not the authority for the demographic assertion.
    private FoundationTacticalCollectiveReceipt? _lastDemographicPreviewReceipt;

    // Real E-close and actual orders: move every own worker across a
    // natural 1.4-turn edge, then verify skip and resumption on arrival.
    private async Task ExerciseUniformCityTransit(FoundationTacticalHumanAiTurnHost host)
    {
        var own = host.Snapshot.Workers.Workers.Where(w => w.Owner == _humanCivilization).ToArray();
        var source = own[0].TacticalTileId!.Value;
        if (own.Length != 10 || own.Any(w => w.TacticalTileId != source))
            throw new InvalidOperationException("Expected stationary own city before travel acceptance.");
        var fine = _r123FineGeography ?? throw new InvalidOperationException("Missing signed geography.");
        var destination = host.Incidence.FineTopology.Cells[(int)source.FineStrategicCellId.Value - 1]
            .AdjacentCellIds.First(c => fine.CrossingAtIndices((int)source.FineStrategicCellId.Value - 1,
                (int)c.Value - 1) == FoundationFineTransportCrossingKind.Ground);
        var target = new PhysicalTacticalTileId(host.Incidence.Refinement.FineParameters, destination);
        foreach (var worker in own)
            host.QueueHumanPersistentEdit(_humanCivilization, new FoundationPersistentOrderEdit(
                _humanCivilization, worker.Id, FoundationPersistentOrderEditKind.Insert,
                Index: 0, OrderKind: FoundationWorkerOrderKind.Move, Destination: target));
        async Task<FoundationTacticalCollectiveReceipt> Close()
        {
            _lastDemographicPreviewReceipt = null;
            var viewport = GetViewport();
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = true, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = false, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var result = _lastDemographicPreviewReceipt ?? throw new InvalidOperationException("Missing real travel close.");
            if (!result.ReplayMatched || result.Hash != host.StateHash || result.BirthBatch is null)
                throw new InvalidOperationException("Travel close did not replay.");
            return result;
        }
        var crossing = await Close();
        if (crossing.BirthBatch!.Newborns.Count != 1 ||
            crossing.BirthBatch.Newborns.Any(w => w.Owner == _humanCivilization) ||
            own.Any(w => !host.Snapshot.EdgeTravels.Pending.Any(p => p.Worker == w.Id)))
            throw new InvalidOperationException("All-own-transit turn incorrectly allowed birth.");
        var arrived = await Close();
        var child = arrived.BirthBatch!.Newborns.Single(w => w.Owner == _humanCivilization);
        if (arrived.BirthBatch.Newborns.Count != 2 || child.NatalNode != target ||
            own.Any(w => host.Snapshot.Workers.Workers.Single(p => p.Id == w.Id).TacticalTileId != target) ||
            host.Snapshot.Workers.Workers.Count(w => w.Owner == _humanCivilization) != 11)
            throw new InvalidOperationException("Arrival birth was not resumed at destination or skipped birth was accumulated.");
        GD.Print("GA_NEXT_R23112_TRANSIT_PASS actual_crossing=true birth_skipped=true resumed_at_destination=true no_carry=true replay=true");
    }

    // Optional scalability continuation after the existing nine stationary
    // and two real crossing/arrival closes. No synthetic worker injection.
    private async Task ExerciseProfileScale(FoundationTacticalHumanAiTurnHost host)
    {
        string? configured = System.Environment.GetEnvironmentVariable("GA_NEXT_R232_PROFILE_THROUGH_CYCLE");
        if (string.IsNullOrWhiteSpace(configured)) return;
        if (System.Environment.GetEnvironmentVariable("GA_NEXT_R232_TURN_PROFILE") != "1" ||
            !ulong.TryParse(configured, out ulong last) || last < 12UL || last > 1000UL)
            throw new InvalidOperationException("Scale continuation requires explicit profiling and 12–1000 closes.");
        var owners = host.Snapshot.Workers.Workers.Select(w => w.Owner).Distinct().ToArray();
        int starting = host.Snapshot.Workers.Workers.Count;
        ulong first = host.NextTurn.Value;
        if (last < first) throw new InvalidOperationException("Invalid scale horizon.");
        var viewport = GetViewport();
        for (ulong cycle = first; cycle <= last; cycle++)
        {
            _lastDemographicPreviewReceipt = null;
            int before = host.Snapshot.Workers.Workers.Count;
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = true, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            viewport.PushInput(new InputEventKey { Keycode = Key.E, Pressed = false, Echo = false }, true);
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var receipt = _lastDemographicPreviewReceipt ?? throw new InvalidOperationException("Missing scale close.");
            if (receipt.Turn.Value != cycle || !receipt.ReplayMatched || receipt.Hash != host.StateHash ||
                host.Snapshot.Workers.Workers.Count != before + owners.Length ||
                receipt.BirthBatch is null || !receipt.BirthBatch.Newborns.Select(w => w.Owner).OrderBy(o => o.Value)
                    .SequenceEqual(owners.OrderBy(o => o.Value)) ||
                host.GetLastClosedEconomicTradeView(_humanCivilization).PublishedHash != receipt.Hash)
                throw new InvalidOperationException($"Scale close {cycle} violated existing contracts.");
        }
        int expected = checked(starting + (int)(last - first + 1UL) * owners.Length);
        if (host.Snapshot.Workers.Workers.Count != expected)
            throw new InvalidOperationException("Scale census mismatch.");
        GD.Print($"GA_NEXT_R232_SCALE_PASS through={last} population={expected} actual_closes=true replay=true");
    }

    // Real normal preview: no worker relocations, no manual orders, no
    // additional commodities, no special market policy or invented sites.
    private async Task ExerciseDemographicPreview(FoundationTacticalHumanAiTurnHost host)
    {
        var map = _tacticalPreview ?? throw new InvalidOperationException("Missing demographic map.");
        var initial = host.Snapshot.Workers.Workers.ToArray();
        var owners = initial.Select(w => w.Owner).Distinct().ToArray();
        bool unitOpening = GaExplicitEconomicQaConfiguration.UnitOpeningPreviewEnabled;
        bool uniformCity = GaExplicitEconomicQaConfiguration.UniformCityDemographyPreviewEnabled;
        bool individual = GaExplicitEconomicQaConfiguration.IndividualDemographyPreviewEnabled;
        if (uniformCity && initial.Any(w => !w.UniformCityDemography || w.BirthWaitTurns.HasValue))
            throw new InvalidOperationException("Uniform city founders must not carry individual lottery weights.");
        if (individual && initial.Any(w => w.BirthWaitTurns != 0UL))
            throw new InvalidOperationException("All founders must start with weight one.");
        if (unitOpening && (owners.Length != 2 || initial.Length != owners.Length ||
            initial.Any(w => w.DemandMultiplierMicro != 1_000_000UL)))
            throw new InvalidOperationException("Unit opening must contain exactly one standard-demand worker per civilization.");
        var home = initial.First(w => w.Owner == _humanCivilization).TacticalTileId!.Value;
        var homeId = home.FineStrategicCellId.Value;
        // Birth slots are global (R164), not one guaranteed child per owner.
        // Inspect both starting sites with the real camera, including sites
        // outside the opening chart. Restore the player's opening focus.
        Dictionary<ulong, float> ReadPopulationMarkers()
        {
            var hash = host.StateHash;
            var focus = map.CenterGeography;
            var result = new Dictionary<ulong, float>();
            try
            {
                foreach (var site in host.Snapshot.Workers.Workers
                    .GroupBy(w => w.TacticalTileId!.Value.FineStrategicCellId.Value)
                    .OrderBy(g => g.Key))
                {
                    var diameter = map.InspectPopulationMarkerForSmoke(site.Key);
                    if (diameter != GaCartographicStyle.SiteDiameter(site.Count()))
                        throw new InvalidOperationException($"Site {site.Key}: population={site.Count()} actual diameter={diameter}");
                    result.Add(site.Key, diameter);
                }
            }
            finally
            {
                if (map.CenterGeography.DistanceSquaredTo(focus) > 1e-10f)
                    map.CenterOnGeography(focus);
            }
            if (host.StateHash != hash)
                throw new InvalidOperationException("Population camera inspection mutated world state.");
            return result;
        }
        var openingDiameters = ReadPopulationMarkers();
        if (uniformCity)
        {
            GaCivilizationVisualIdentity.ExerciseSiteColorContract();
            var focus = map.CenterGeography;
            var hash = host.StateHash;
            foreach (var founder in initial)
            {
                ulong tile = founder.TacticalTileId!.Value.FineStrategicCellId.Value;
                map.InspectPopulationMarkerForSmoke(tile);
                var dot = map.FindChild("GANextR134SiteDot" + tile, true, false) as Panel;
                var expected = founder.Owner.Value == 1UL ? new Color("#ed5252") : new Color("#398bf2");
                if (dot?.IsVisibleInTree() != true ||
                    dot.GetThemeStylebox("panel") is not StyleBoxFlat disc ||
                    disc.BgColor != expected || disc.BorderColor != new Color("#f5f5f5"))
                    throw new InvalidOperationException("Founder's visible site must show the canonical civilization color.");
            }
            map.CenterOnGeography(focus);
            if (host.StateHash != hash)
                throw new InvalidOperationException("Site color inspection changed the canonical world.");
            GD.Print("GA_NEXT_R23111_SITE_COLORS_PASS red=true blue=true mixed_policy=true white_border=true hash_unchanged=true");
        }

        var previousDiameters = openingDiameters;
        AssertCartographicPublication(host, 0UL);
        bool bandChanged = false;
        bool bounded = GaExplicitEconomicQaConfiguration.BoundedTradePreviewEnabled;
        bool positiveTrade = false;
        string? atlasEvidence = System.Environment.GetEnvironmentVariable("GA_NEXT_R23110_ATLAS_SCREENSHOT_DIR");
        bool atlasChecked = false;
        int cropRows = 0;
        // The canonical treasury retains the last observation per market,
        // even across closes without sales. Reconstruct that history from
        // consecutive authorized own-node reads, never replace it wholesale.
        var allocationPriceHistory = new Dictionary<(PhysicalTacticalTileId, CommodityId), ulong>();
        for (ulong cycle = 1; cycle <= 9; cycle++)
        {
            if (bounded)
                foreach (var observation in ReadOpeningAllocationPrices(host))
                    allocationPriceHistory[observation.Key] = observation.Value;
            var opening = host.Snapshot;
            var before = opening.Workers.Workers.ToArray();
            var hash = host.StateHash;
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
            Require(newborns.All(w => owners.Contains(w.Owner)),
                "birth assigned to a non-surviving owner");
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
                // The eligible lottery bucket is owner + physical worker-time,
                // not a residence or the final position of a traveling worker.
                Require(receipt.PersistentTimeline!.Slices.Any(s =>
                    s.Node == child.NatalNode && s.DurationMicro > 0 &&
                    before.Any(w => w.Id == s.Worker && w.Owner == child.Owner)),
                    $"worker {child.Id.Value}: no eligible owner/node presence");
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
            if (bounded)
            {
                var sale = receipt.TemporalEconomicSettlement!;
                Require(sale.UsedBoundedSafeProgressiveRoutes, "missing bounded policy evidence");
                Require(sale.SellerNetScaled.Sign >= 0 && sale.Credits.All(x => x.SellerNetScaled.Sign >= 0),
                    "negative seller credit");
                positiveTrade |= view.UsedEdges.Count > 0 && sale.PhysicalFreightScaled.Sign > 0;
                GD.Print($"GA_NEXT_R2317_BOUNDED_CYCLE cycle={cycle} edges={view.UsedEdges.Count} freight={sale.PhysicalFreightScaled} net={sale.SellerNetScaled} replay=true");
            }
            else AuditNormalTradeInputs(host, opening, receipt);
            if (unitOpening)
            {
                var people = before.Where(w => w.Owner == _humanCivilization).Select(w => w.Id).ToHashSet();
                var durations = receipt.PersistentTimeline!.Slices.Where(s => people.Contains(s.Worker))
                    .GroupBy(s => s.Node);
                foreach (var node in durations)
                {
                    ulong expectedDemand = node.Aggregate(0UL, (sum, slice) => checked(sum + (ulong)slice.DurationMicro));
                    var local = host.GetLastClosedLocalEconomics(_humanCivilization, node.Key) ??
                        throw new InvalidOperationException("Unit opening has no own closed demand evidence.");
                    Require(local.Commodities.Count() == 8 && local.Commodities.All(row => row.DemandMicro == expectedDemand),
                        "individual commodity demand differs from physical worker-time");
                }
                if (cycle == 1)
                    GD.Print($"GA_NEXT_R2318_OPENING_REFERENCE workers={before.Length} aggregate_seller_net_scaled={receipt.TemporalEconomicSettlement!.SellerNetScaled} diagnostic_only=true");
            }
            if (bounded) AuditLocalAutomaticAllocation(host, opening, receipt, allocationPriceHistory);
            if (uniformCity) AuditUniformCityDemography(host, opening, receipt);
            else if (individual) AuditIndividualDemography(host, opening, receipt);
            else if (unitOpening) AuditUnitOpeningDemography(host, opening, receipt);
            if (bounded && !unitOpening)
                cropRows += ExerciseCityWorkerClosedCropRows(host, receipt);
            var diameters = ReadPopulationMarkers();
            AssertCartographicPublication(host, cycle);
            if (uniformCity && !atlasChecked && !string.IsNullOrWhiteSpace(atlasEvidence) && view.UsedEdges.Count > 0)
            {
                await AssertRenderedStrategicTrade(host, atlasEvidence);
                atlasChecked = true;
            }
            bandChanged |= diameters.Any(p => previousDiameters.TryGetValue(p.Key, out var prior) && p.Value > prior);
            previousDiameters = diameters;
            var distribution = string.Join(",", owners.OrderBy(o => o.Value)
                .Select(o => $"{o.Value}:{newborns.Count(w => w.Owner == o)}"));
            var sites = string.Join(",", diameters.OrderBy(p => p.Key).Select(p => $"{p.Key}:{p.Value}"));
            GD.Print($"GA_NEXT_R2315_DEMOGRAPHIC_CYCLE cycle={cycle} own={current.Count(w => w.Owner == _humanCivilization)} home_population={current.Count(w => w.TacticalTileId == home)} dot_px={diameters[homeId]} authorized_trade_edges={view.UsedEdges.Count} replay=true births_by_owner={distribution} sites_px={sites}");
            if (cycle == 3)
            {
                if (!bandChanged || !previousDiameters.Any(p =>
                    openingDiameters.TryGetValue(p.Key, out var original) && p.Value > original))
                    throw new InvalidOperationException("Growing normal opening did not cross any actual visible population band.");
                GD.Print("GA_NEXT_R2315_DEMOGRAPHIC_PASS cycles=3 normal_opening=true actual_births=true atomic_queues=true dot_growth=true final_hash=true");
            }
        }
        if (bounded)
        {
            if (!positiveTrade) throw new InvalidOperationException("Bounded preview did not settle any positive normal-opening remote trade.");
            if (!unitOpening)
            {
                if (cropRows == 0) throw new InvalidOperationException("No actual own agricultural rows tested.");
                GD.Print($"GA_NEXT_R2318C_CROP_ROWS_PASS actual_rows={cropRows} committed_calendar=true");
            }
            GD.Print("GA_NEXT_R2317_BOUNDED_TRADE_PASS cycles=9 positive_trade=true replay=true");
            if (unitOpening) GD.Print("GA_NEXT_R2318_UNIT_OPENING_PASS cycles=9 individual_demand=true initial_per_owner=1 replay=true");
            if (uniformCity)
            {
                if (owners.Any(o => host.Snapshot.Workers.Workers.Count(w => w.Owner == o) != 10))
                    throw new InvalidOperationException("Nine stationary closes must yield ten workers per civilization.");
                GD.Print("GA_NEXT_R23112_UNIFORM_PASS cycles=9 population_per_owner=10 replay=true");
                if (!string.IsNullOrWhiteSpace(atlasEvidence) && !atlasChecked)
                    throw new InvalidOperationException("No positive trade frame tested in atlas.");
                await ExerciseUniformCityTransit(host);
                await ExerciseProfileScale(host);
            }
            if (individual)
            {
                if (owners.Any(o => host.Snapshot.Workers.Workers.Count(w => w.Owner == o) != 10))
                    throw new InvalidOperationException("Nine closes must yield ten workers per surviving civilization.");
                GD.Print("GA_NEXT_R2319_INDIVIDUAL_PASS cycles=9 population_per_owner=10 replay=true");
            }
        }
        else GD.Print("GA_NEXT_R2316_NORMAL_TRADE_AUDIT_PASS cycles=9 no_market_resolve=true");
    }
}
