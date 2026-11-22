using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Physical production actually available during one whole worker-turn.
/// Demand is computed separately by the existing R164 activity policy.
/// Values are micro-goods with deterministic floor rounding per worker/node.
/// </summary>
public sealed record FoundationTemporalPhysicalProduction(
    PhysicalWorkerId Worker, CivilizationId Owner,
    PhysicalTacticalTileId Node, CommodityId Commodity,
    uint EffortMicro, ulong ProducedMicro);

public sealed class FoundationTemporalEconomicTurnInputs
{
    public FoundationPhysicalTemporalPresenceReport Presence { get; }
    public IReadOnlyList<FoundationWorkerTemporalNodeSlice> Slices { get; }
    public IReadOnlyList<FoundationTemporalPhysicalProduction> Production { get; }
    public ulong TotalProductionMicro { get; }
    public ulong TotalPresenceMicro { get; }
    public ulong TotalProductiveEffortMicro { get; }
    public bool IsCommitted => false;

    internal FoundationTemporalEconomicTurnInputs(
        FoundationPhysicalTemporalPresenceReport presence,
        FoundationWorkerTemporalNodeSlice[] slices,
        FoundationTemporalPhysicalProduction[] production)
    {
        Presence = presence;
        Slices = Array.AsReadOnly(slices);
        Production = Array.AsReadOnly(production);
        TotalProductionMicro = production.Aggregate(0UL,
            (sum, row) => checked(sum + row.ProducedMicro));
        TotalPresenceMicro = presence.WorkerTurnMicro;
        TotalProductiveEffortMicro = production.Aggregate(0UL,
            (sum, row) => checked(sum + row.EffortMicro));
    }
}

/// <summary>
/// R168 input bridge: derives an entire turn's demand and producible supply
/// from actual R167 journal receipts, rather than the final worker snapshot.
/// Deliberately does not mint money, alter prices, or close a market cycle:
/// the existing allocator still needs an authoritative fractional input API.
/// </summary>
public static class FoundationTemporalEconomicTurnProjector
{
    public static FoundationTemporalEconomicTurnInputs Project(
        WorldState start, WorldState end,
        IReadOnlyList<FoundationEdgeTravelTurnEvent> travelEvents,
        FoundationActivityCommodityDemandPolicy demandPolicy)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(travelEvents);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        if (!start.EdgeTravels.IsInitialized ||
            !end.EdgeTravels.IsInitialized ||
            !start.Workers.HasAuthoritativeOccupations ||
            !end.Workers.HasAuthoritativeOccupations ||
            start.WorldBinding != end.WorldBinding ||
            start.Workers.Workers.Count != end.Workers.Workers.Count)
            throw new InvalidOperationException(
                "Temporal economy requires the same authoritative opt-in physical census.");

        var beginning = start.Workers.Workers.ToDictionary(w => w.Id);
        var ending = end.Workers.Workers.ToDictionary(w => w.Id);
        if (!beginning.Keys.ToHashSet().SetEquals(ending.Keys))
            throw new InvalidOperationException(
                "Movement cannot create or remove an economic worker.");

        var traveled = new HashSet<PhysicalWorkerId>();
        var schedule = new List<FoundationWorkerTemporalNodeSlice>();
        foreach (var ev in travelEvents)
        {
            if (ev is null || !beginning.TryGetValue(ev.WorkerId, out var person) ||
                person.Owner != ev.Issuer || !traveled.Add(ev.WorkerId) ||
                ev.Receipt.TurnResult is not { } result ||
                result.Worker != ev.WorkerId ||
                result.TimeSlices.Count == 0 ||
                ev.Receipt.Worker != ev.WorkerId ||
                !ending.TryGetValue(ev.WorkerId, out var after) ||
                after.TacticalTileId != result.PhysicalTileAfterTurn)
                throw new InvalidOperationException(
                    "Temporal economic input requires one complete actual travel receipt per traveler.");
            if (result.TimeSlices[0].Node != person.TacticalTileId ||
                result.TimeSlices[0].DemandActivity != PhysicalWorkerActivity.Traveling ||
                result.TimeSlices[0].ProductiveEffortMicro != 0U ||
                result.TimeSlices.Any(s => s.Worker != person.Id) ||
                result.Arrived == end.EdgeTravels.Pending.Any(p => p.Worker == person.Id))
                throw new InvalidOperationException(
                    "Physical travel receipt contradicts starting position or persisted journey.");
            schedule.AddRange(result.TimeSlices);
        }

        foreach (var person in beginning.Values.OrderBy(w => w.Id.Value))
        {
            if (traveled.Contains(person.Id)) continue;
            var after = ending[person.Id];
            if (after.Owner != person.Owner ||
                after.TacticalTileId != person.TacticalTileId ||
                start.EdgeTravels.Pending.Any(p => p.Worker == person.Id) ||
                end.EdgeTravels.Pending.Any(p => p.Worker == person.Id))
                throw new InvalidOperationException(
                    "Unjournaled crossing, stale traveler, or owner change in temporal cycle.");
            var productive = after.Activity == PhysicalWorkerActivity.Producing;
            schedule.Add(new FoundationWorkerTemporalNodeSlice(
                person.Id, person.TacticalTileId ??
                    throw new InvalidOperationException("Missing physical worker tile."),
                FoundationPhysicalPresenceDemographicProjector.WholeTurnMicro,
                productive ? FoundationPhysicalPresenceDemographicProjector.WholeTurnMicro : 0U,
                after.Activity));
        }

        var report = FoundationPhysicalPresenceDemographicProjector.Project(
            start, schedule, demandPolicy);
        // Accumulate effort at the worker/node level before one integer rounding.
        // This avoids a supply-side rounding tax from splitting identical slices.
        const ulong whole = FoundationPhysicalPresenceDemographicProjector.WholeTurnMicro;
        var production = schedule
            .Where(s => s.ProductiveEffortMicro > 0U)
            .GroupBy(s => (s.Worker, s.Node))
            .OrderBy(g => g.Key.Worker.Value)
            .ThenBy(g => g.Key.Node.FineStrategicCellId.Value)
            .Select(g =>
            {
                var worker = beginning[g.Key.Worker];
                ulong effort = g.Aggregate(0UL,
                    (sum, s) => checked(sum + s.ProductiveEffortMicro));
                ulong amount = checked((ulong)(
                    (UInt128)worker.OutputMicroPerTurn * effort / whole));
                return new FoundationTemporalPhysicalProduction(
                    worker.Id, worker.Owner, g.Key.Node, worker.Commodity,
                    checked((uint)effort), amount);
            }).ToArray();
        return new FoundationTemporalEconomicTurnInputs(
            report, schedule.ToArray(), production);
    }
}
