using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>One local ranking per stationary automatic suffix. No financial
/// evaluation, demand projection, route search, or hypothetical market.
/// The old experimental allocator remains available for historical replay.</summary>
internal static class FoundationLocalBaseCalendarPlanner
{
    internal static IReadOnlyList<CivilizationObservedMarketPrice>? ReadHistory(WorldState opening) =>
        opening.Treasury.LocalPriceHistory;

    internal static FoundationPersistentWorkerSlice[] Plan(WorldState opening,
        PhysicalTacticalIncidenceMap incidence, FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        IReadOnlyList<FoundationPersistentWorkerSlice> calendar,
        IReadOnlyList<FoundationBaseMarginalInterval> intervals,
        FoundationAgriculturalProductionPolicy agriculture)
    {
        if (agriculture.BasicResourceCommodities.Any(c => !policy.Demand.Commodities.Contains(c)))
            throw new InvalidOperationException("Every basic resource requires explicit market demand.");
        var ordered = calendar.OrderBy(s => s.StartMicro).ThenBy(s => s.Worker.Value).ToArray();
        var indices = ordered.Select((s, i) => (s, i))
            .ToDictionary(x => (x.s.Worker, x.s.StartMicro), x => x.i);
        var workers = opening.Workers.Workers.ToDictionary(w => w.Id);
        var prices = (ReadHistory(opening) ?? Array.Empty<CivilizationObservedMarketPrice>())
            .ToDictionary(p => (p.Node, p.Commodity), p => p.PriceMicro);
        var reservations = new Dictionary<(CivilizationId, PhysicalTacticalTileId),
            Dictionary<(PhysicalWorkerOccupation, byte?), uint>>();
        foreach (var interval in intervals.OrderBy(i => i.Worker.Value).ThenBy(i => i.StartMicro))
        {
            var index = indices[(interval.Worker, interval.StartMicro)];
            var idle = ordered[index];
            var worker = workers[idle.Worker];
            if (idle.Activity != PhysicalWorkerActivity.Other || idle.Occupation != PhysicalWorkerOccupation.Unassigned ||
                idle.ProjectId.HasValue || idle.ProductiveProjectId.HasValue || idle.Crop.HasValue || idle.DurationMicro == 0U)
                throw new InvalidOperationException("Only available stationary effort can be allocated.");
            if (geography.SurfaceAtIndex(checked((int)idle.Node.FineStrategicCellId.Value - 1)) != FoundationFineSurfaceKind.Land)
                continue;
            var key = (worker.Owner, idle.Node);
            if (!reservations.TryGetValue(key, out var counts))
            {
                counts = new();
                reservations.Add(key, counts);
            }
            var eligible = FoundationBaseMarginalRevenueEvaluator.EligibleActivities(
                worker, idle, incidence, geography, policy, agriculture, null);
            if (agriculture.BasicResources is not null)
            {
                eligible.RemoveAll(c => c.Role is PhysicalWorkerOccupation.Forestry or PhysicalWorkerOccupation.Mining);
                eligible.Add((PhysicalWorkerOccupation.Forestry, null, PhysicalWorkerActivity.Producing));
                eligible.Add((PhysicalWorkerOccupation.Mining, null, PhysicalWorkerActivity.Producing));
            }
            var candidates = eligible.Select(choice =>
            {
                var output = FoundationTemporalEconomicTurnProjector.ResolveOpeningOutput(opening, worker,
                    idle.Node, choice.Role, choice.Crop, agriculture);
                ulong goods = checked((ulong)((UInt128)output.WholeTurnOutput * idle.DurationMicro / 1_000_000UL));
                ulong? observed = prices.TryGetValue((idle.Node, output.Commodity), out var price) ? price : null;
                return new FoundationLocalBaseCandidate(choice.Role, choice.Crop, goods, observed,
                    FoundationSettledLocalPriceSnapshot.InitialReferencePriceMicro);
            });
            var selected = FoundationLocalBaseRevenueRanker.Select(candidates, counts);
            if (selected is null) continue;
            ordered[index] = idle with { Activity = PhysicalWorkerActivity.Producing,
                Occupation = selected.Occupation, Crop = selected.Crop };
            var activity = (selected.Occupation, selected.Crop);
            counts[activity] = checked(counts.GetValueOrDefault(activity) + 1U);
        }
        return ordered;
    }
}
