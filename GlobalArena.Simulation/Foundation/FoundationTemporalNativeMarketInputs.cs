using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R169: construct the EXACT canonical market input type that the established
/// physical water-filling engine already consumes. No fractional worker
/// records, alternative prices, cargo conversion or parallel solver.
/// </summary>
public static class FoundationTemporalNativeMarketInputs
{
    public static FoundationPhysicalCommodityMarketInputs FromTemporal(
        WorldState baseline, FoundationTemporalEconomicTurnInputs temporal,
        FoundationActivityCommodityDemandPolicy demandPolicy)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(temporal);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        if (temporal.IsCommitted || temporal.Presence.IsCommitted ||
            temporal.Presence.BaselineIdentity !=
                new CanonicalWorldStateHasher().Compute(baseline).ToString() ||
            temporal.TotalPresenceMicro != checked(
                (ulong)baseline.Workers.Workers.Count *
                FoundationPhysicalPresenceDemographicProjector.WholeTurnMicro))
            throw new InvalidOperationException(
                "Native temporal market inputs require exact source-world worker-time evidence.");

        var workers = baseline.Workers.Workers.ToDictionary(w => w.Id);
        var ownerCounts = temporal.Slices
            .Where(s => s.PresenceMicro > 0U)
            .GroupBy(s => (s.Node, Owner: workers[s.Worker].Owner))
            .ToDictionary(g => g.Key,
                g => g.Select(s => s.Worker).Distinct().Count());
        var demandRows = temporal.Presence.Demand
            .Where(d => d.DemandMicro > 0UL)
            .OrderBy(d => d.Node.FineStrategicCellId.Value)
            .ThenBy(d => d.Owner.Value)
            .ThenBy(d => d.Commodity.Value)
            .Select(d => new FoundationPhysicalCommodityDemandLine(
                d.Node, d.Owner, d.Commodity,
                ownerCounts[(d.Node, d.Owner)], d.DemandMicro))
            .ToArray();
        var markets = demandRows
            .GroupBy(d => (d.Tile, d.Commodity))
            .OrderBy(g => g.Key.Tile.FineStrategicCellId.Value)
            .ThenBy(g => g.Key.Commodity.Value)
            .Select(g => new FoundationPhysicalCommodityMarketDemand(
                g.Key.Tile, g.Key.Commodity,
                g.Sum(d => d.WorkerCount),
                g.Aggregate(0UL, (sum, d) => checked(sum + d.DemandMicro))))
            .ToArray();

        var production = temporal.Production
            .Where(p => p.ProducedMicro > 0UL)
            .Select(p => new
            {
                Item = p,
                Worker = workers.TryGetValue(p.Worker, out var w)
                    ? w : throw new InvalidOperationException(
                        "Temporal supply uses an unknown real worker."),
                Occupation = p.SliceOccupation ?? (temporal.EffectiveOccupations.TryGetValue(
                    p.Worker, out var updatedOccupation)
                    ? updatedOccupation : workers[p.Worker].Occupation!.Value)
            }).ToArray();
        var lots = production
            .GroupBy(x => (x.Item.Node, x.Item.Owner,
                Occupation: x.Occupation, x.Item.Commodity))
            .OrderBy(g => g.Key.Node.FineStrategicCellId.Value)
            .ThenBy(g => g.Key.Owner.Value)
            .ThenBy(g => g.Key.Occupation)
            .ThenBy(g => g.Key.Commodity.Value)
            .Select(g => new FoundationPhysicalSupplyLot(
                g.Key.Node, g.Key.Owner, g.Key.Occupation,
                g.Key.Commodity, g.Select(x => x.Item.Worker).Distinct().Count(),
                g.Aggregate(0UL, (sum, x) => checked(sum + x.Item.ProducedMicro))))
            .ToArray();
        ulong demand = demandRows.Aggregate(0UL,
            (sum, line) => checked(sum + line.DemandMicro));
        ulong supply = lots.Aggregate(0UL,
            (sum, line) => checked(sum + line.CapacityMicroPerTurn));
        if (demand != temporal.Presence.Demand.Aggregate(0UL,
                (sum, d) => checked(sum + d.DemandMicro)) ||
            supply != temporal.TotalProductionMicro ||
            demandRows.Any(d => !demandPolicy.Commodities.Contains(d.Commodity)) ||
            lots.Any(l => !demandPolicy.Commodities.Contains(l.Commodity)))
            throw new InvalidOperationException(
                "Temporal native market input cannot omit any positive demand or supply.");
        return new FoundationPhysicalCommodityMarketInputs(
            demandRows, markets, lots, demandPolicy.Commodities,
            workers.Count, demand, supply);
    }
}
