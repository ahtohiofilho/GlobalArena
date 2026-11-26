using Godot;
using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;
using GlobalArena.Foundation.Agriculture;
using GlobalArena.Simulation.Foundation;

namespace GlobalArena.Godot;

public partial class Main
{
    // Diagnostic ONLY, reached by the explicit demographic CI smoke. No
    // client read-model expansion, no new market solve, no change to prices.
    // Reconstruct the input totals from the verified timeline and compare
    // against the actual receipt before interpreting the freight bound.
    private void AuditNormalTradeInputs(FoundationTacticalHumanAiTurnHost host,
        WorldState opening, FoundationTacticalCollectiveReceipt receipt)
    {
        const ulong unit = 1_000_000UL;
        var hash = host.StateHash;
        var sale = receipt.TemporalEconomicSettlement ??
            throw new InvalidOperationException("Trade audit requires actual temporal settlement.");
        var timeline = receipt.PersistentTimeline ??
            throw new InvalidOperationException("Trade audit requires actual timeline.");
        var fine = _tacticalPreview?.AuthoritativeFineEconomy ??
            throw new InvalidOperationException("Trade audit requires signed geography.");
        var agriculture = host.AgriculturalProduction ??
            throw new InvalidOperationException("Trade audit requires eight-commodity policy.");
        var basic = agriculture.BasicResources ??
            throw new InvalidOperationException("Trade audit requires actual basic resource policy.");
        var policy = GaExplicitEconomicQaConfiguration.Profile(true, true).Settlement;
        if (!sale.UsedSafeProgressiveRoutes || sale.OperationalQuotes is not null)
            throw new InvalidOperationException("Audit is restricted to the normal fixed-freight safe progressive QA profile.");
        var people = opening.Workers.Workers.ToDictionary(w => w.Id);
        var presence = FoundationPhysicalPresenceDemographicProjector.Project(opening,
            timeline.Slices.Select(s => new FoundationWorkerTemporalNodeSlice(s.Worker, s.Node,
                s.DurationMicro, s.Activity == PhysicalWorkerActivity.Producing ? s.DurationMicro : 0U,
                s.Activity, s.Occupation)).ToArray(), policy.Demand);
        if (presence.ScheduleDigest != sale.ScheduleDigest)
            throw new InvalidOperationException("Audit schedule differs from committed sale.");

        var supply = timeline.Slices.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
            .GroupBy(s => (s.Worker, s.Node, s.Occupation, s.Crop))
            .Select(g =>
            {
                var worker = people[g.Key.Worker];
                var land = fine.SurfaceAtIndex(checked((int)g.Key.Node.FineStrategicCellId.Value - 1)) == FoundationFineSurfaceKind.Land;
                var output = g.Key.Crop.HasValue ? agriculture.Resolve(g.Key.Node, (CropId)g.Key.Crop.Value)
                    : g.Key.Occupation == PhysicalWorkerOccupation.Forestry ? (basic.Wood, land ? basic.WoodWholeTurnOutput : 0UL)
                    : g.Key.Occupation == PhysicalWorkerOccupation.Mining ? (basic.Stone, land ? basic.StoneWholeTurnOutput : 0UL)
                    : (worker.Commodity, worker.OutputMicroPerTurn);
                ulong effort = g.Aggregate(0UL, (sum, s) => checked(sum + s.DurationMicro));
                return (Node: g.Key.Node, worker.Owner, Commodity: output.Item1,
                    Goods: checked((ulong)((UInt128)output.Item2 * effort / unit)));
            }).Where(x => x.Goods > 0).ToArray();
        if (supply.Aggregate(0UL, (sum, p) => checked(sum + p.Goods)) != sale.ProducedGoodsMicro)
            throw new InvalidOperationException("Audit production differs from settled total.");
        var demand = presence.Demand.GroupBy(d => (d.Node, d.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL, (sum, d) => checked(sum + d.DemandMicro)));
        var totalSupply = supply.GroupBy(p => p.Commodity)
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL, (sum, p) => checked(sum + p.Goods)));
        // Cross-check own actual commodity evidence, not just the global sum.
        foreach (var node in opening.Workers.Workers.Where(w => w.Owner == _humanCivilization)
            .Select(w => w.TacticalTileId!.Value).Distinct())
        {
            var local = host.GetLastClosedLocalEconomics(_humanCivilization, node) ??
                throw new InvalidOperationException("Missing own closed local evidence.");
            foreach (var row in local.Commodities)
                if (row.ProducedMicro != supply.Where(p => p.Owner == _humanCivilization && p.Node == node && p.Commodity == row.Commodity)
                    .Aggregate(0UL, (sum, p) => checked(sum + p.Goods)) ||
                    row.DemandMicro != presence.Demand.Where(d => d.Owner == _humanCivilization && d.Node == node && d.Commodity == row.Commodity)
                    .Aggregate(0UL, (sum, d) => checked(sum + d.DemandMicro)))
                    throw new InvalidOperationException("Audit own local inputs differ from closed evidence.");
        }

        // Read-only shortest path cross-check. The production allocator is
        // never invoked here. Match signed blocking, physical costs and
        // seller-specific toll exemption in the existing opening snapshot.
        var incidence = host.Incidence;
        var roads = FoundationPhysicalAuthoritativeTransportProjector.FromWorld(opening, incidence).Roads
            .ToDictionary(r => r.Edge);
        var graph = new Dictionary<ulong, List<(ulong To, FoundationPhysicalRoutingEdgeCost Road)>>();
        foreach (var edge in incidence.FineTopology.Edges)
        {
            ulong a = edge.IncidentCellIds[0].Value, b = edge.IncidentCellIds[1].Value;
            if (fine.CrossingAtIndices(checked((int)a - 1), checked((int)b - 1)) ==
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary) continue;
            var road = roads[new PhysicalTacticalEdgeId(incidence.Refinement.FineParameters, edge.Id)];
            if (!graph.ContainsKey(a)) graph.Add(a, new());
            if (!graph.ContainsKey(b)) graph.Add(b, new());
            graph[a].Add((b, road)); graph[b].Add((a, road));
        }
        int candidates = 0, unreachable = 0, rejected = 0, admitted = 0;
        foreach (var group in supply.GroupBy(p => (p.Node, p.Owner)))
        {
            ulong source = group.Key.Node.FineStrategicCellId.Value;
            var rates = new Dictionary<ulong, ulong> { [source] = 0 };
            var queue = new PriorityQueue<ulong, ulong>();
            queue.Enqueue(source, 0);
            while (queue.TryDequeue(out var at, out var cost))
            {
                if (rates[at] != cost || !graph.TryGetValue(at, out var links)) continue;
                foreach (var link in links)
                {
                    ulong toll = link.Road.InfrastructureOwner.HasValue && link.Road.InfrastructureOwner.Value != group.Key.Owner
                        ? link.Road.ExternalTariffMicroPerWholeCargoUnit : 0;
                    ulong next = checked(cost + link.Road.PhysicalCostMicroPerWholeCargoUnit + toll);
                    if (rates.TryGetValue(link.To, out var prior) && prior <= next) continue;
                    rates[link.To] = next;
                    queue.Enqueue(link.To, next);
                }
            }
            uint tax = policy.TaxRates.Single(t => t.Civilization == group.Key.Owner).GrossRevenueBasisPoints;
            foreach (var item in group.Select(p => p.Commodity).Distinct().OrderBy(c => c.Value))
            foreach (var target in demand.Where(p => p.Key.Commodity == item && p.Key.Node != group.Key.Node && p.Value > 0)
                .OrderBy(p => p.Key.Node.FineStrategicCellId.Value))
            {
                candidates++;
                ulong dest = target.Key.Node.FineStrategicCellId.Value;
                if (!rates.TryGetValue(dest, out var rate))
                {
                    unreachable++;
                    GD.Print($"GA_NEXT_R2316_ROUTE cycle={receipt.Turn.Value} from={source} to={dest} item={item.Value} status=unreachable");
                    continue;
                }
                // Equivalent exact comparison to the floor sqrt admission
                // rule: floor(sqrt(N)) >= ceil(required), iff N >= required^2.
                BigInteger costScaled = (BigInteger)policy.Cargo.CargoMicro(item, unit) * rate;
                BigInteger denominator = (BigInteger)unit * (10_000U - tax);
                BigInteger minimumRequired = (costScaled * 10_000U + denominator - 1) / denominator;
                bool safe = (BigInteger)target.Value * unit * unit >= minimumRequired * minimumRequired * totalSupply[item];
                if (safe) admitted++; else rejected++;
                GD.Print($"GA_NEXT_R2316_ROUTE cycle={receipt.Turn.Value} from={source} to={dest} owner={group.Key.Owner.Value} item={item.Value} freight_micro_per_cargo={rate} destination_demand_micro={target.Value} world_supply_micro={totalSupply[item]} tax_bp={tax} required_gross_price_micro={minimumRequired} status={(safe ? "admitted" : "bound_rejected")}");
            }
        }
        var view = host.GetLastClosedEconomicTradeView(_humanCivilization);
        if (candidates == 0 || candidates != unreachable + rejected + admitted || hash != host.StateHash ||
            (admitted == 0 && (view.UsedEdges.Count != 0 || sale.PhysicalFreightScaled != BigInteger.Zero)))
            throw new InvalidOperationException("Trade input diagnostic contradicts actual close.");
        GD.Print($"GA_NEXT_R2316_TRADE_AUDIT cycle={receipt.Turn.Value} remote_candidates={candidates} unreachable={unreachable} bound_rejected={rejected} admitted={admitted} authorized_edges={view.UsedEdges.Count} world_freight_scaled={sale.PhysicalFreightScaled} input_totals_verified=true");
    }
}
