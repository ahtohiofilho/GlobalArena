using GlobalArena.Foundation.Agriculture;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>One local ranking per stationary automatic suffix. No financial
/// evaluation, demand projection, route search, or hypothetical market.
/// The old experimental allocator remains available for historical replay.</summary>
internal static class FoundationLocalBaseCalendarPlanner
{
    internal static FoundationPersistentWorkerSlice[] Plan(WorldState opening,
        PhysicalTacticalIncidenceMap incidence, FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        IReadOnlyList<FoundationPersistentWorkerSlice> calendar,
        IReadOnlyList<FoundationBaseMarginalInterval> intervals,
        FoundationAgriculturalProductionPolicy agriculture)
    {
        var ordered = calendar.OrderBy(s => s.StartMicro).ThenBy(s => s.Worker.Value).ToArray();
        var indices = ordered.Select((s, i) => (s, i))
            .ToDictionary(x => (x.s.Worker, x.s.StartMicro), x => x.i);
        var workers = opening.Workers.Workers.ToDictionary(w => w.Id);
        var prices = (opening.Treasury.LocalPriceHistory ?? Array.Empty<CivilizationObservedMarketPrice>())
            .ToDictionary(p => (p.Node, p.Commodity), p => p.PriceMicro);
        var reservations = new Dictionary<(CivilizationId, PhysicalTacticalTileId),
            Dictionary<(PhysicalWorkerOccupation, byte?), uint>>();
        foreach (var interval in intervals.OrderBy(i => i.Worker.Value).ThenBy(i => i.StartMicro))
        {
            var index = indices[(interval.Worker, interval.StartMicro)];
            var idle = ordered[index];
            var worker = workers[idle.Worker];
            if (idle.Activity != PhysicalWorkerActivity.Other || idle.Occupation != PhysicalWorkerOccupation.Unassigned ||
                idle.ProjectId.HasValue || idle.Crop.HasValue || idle.DurationMicro == 0U)
                throw new InvalidOperationException("Only available stationary effort can be allocated.");
            if (geography.SurfaceAtIndex(checked((int)idle.Node.FineStrategicCellId.Value - 1)) != FoundationFineSurfaceKind.Land)
                continue;
            var key = (worker.Owner, idle.Node);
            if (!reservations.TryGetValue(key, out var counts))
            {
                counts = new();
                reservations.Add(key, counts);
            }
            var candidates = FoundationBaseMarginalRevenueEvaluator.EligibleActivities(
                worker, idle, incidence, geography, policy, agriculture, null).Select(choice =>
            {
                var output = choice.Crop is { } crop
                    ? agriculture.Resolve(idle.Node, (CropId)crop)
                    : (Commodity: worker.Commodity, WholeTurnOutput: worker.OutputMicroPerTurn);
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
