using GlobalArena.Runtime;
using GlobalArena.Foundation.Agriculture;
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
    uint EffortMicro, ulong ProducedMicro,
    PhysicalWorkerOccupation? SliceOccupation = null);

public sealed class FoundationTemporalEconomicTurnInputs
{
    public FoundationPhysicalTemporalPresenceReport Presence { get; }
    public IReadOnlyList<FoundationWorkerTemporalNodeSlice> Slices { get; }
    public IReadOnlyList<FoundationTemporalPhysicalProduction> Production { get; }
    public ulong TotalProductionMicro { get; }
    public ulong TotalPresenceMicro { get; }
    public ulong TotalProductiveEffortMicro { get; }
    public IReadOnlyDictionary<PhysicalWorkerId, PhysicalWorkerOccupation> EffectiveOccupations { get; }
    public bool IsCommitted => false;

    internal FoundationTemporalEconomicTurnInputs(
        FoundationPhysicalTemporalPresenceReport presence,
        FoundationWorkerTemporalNodeSlice[] slices,
        FoundationTemporalPhysicalProduction[] production,
        IReadOnlyDictionary<PhysicalWorkerId, PhysicalWorkerOccupation>? effectiveOccupations = null)
    {
        Presence = presence;
        Slices = Array.AsReadOnly(slices);
        Production = Array.AsReadOnly(production);
        EffectiveOccupations = new System.Collections.ObjectModel.ReadOnlyDictionary<
            PhysicalWorkerId, PhysicalWorkerOccupation>(
            new Dictionary<PhysicalWorkerId, PhysicalWorkerOccupation>(
                effectiveOccupations ?? new Dictionary<PhysicalWorkerId, PhysicalWorkerOccupation>()));
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
    // One opening-state output quote, shared by actual production and local
    // candidate ranking. Capital never changes natural suitability or effort.
    internal static (CommodityId Commodity, ulong WholeTurnOutput) ResolveOpeningOutput(
        WorldState opening, PhysicalWorkerRuntimeRecord worker, PhysicalTacticalTileId node,
        PhysicalWorkerOccupation occupation, byte? crop, FoundationAgriculturalProductionPolicy? agriculture)
    {
        var natural = agriculture is not null
            ? agriculture.ResolveActivity(worker, node, occupation, crop)
            : (Commodity: worker.Commodity, WholeTurnOutput: worker.OutputMicroPerTurn);
        if (!opening.ProductiveInfrastructure.IsInitialized || natural.WholeTurnOutput == 0UL)
            return natural;
        FoundationProductiveActivity? activity = occupation switch
        {
            PhysicalWorkerOccupation.Agriculture => FoundationProductiveActivity.Agriculture,
            PhysicalWorkerOccupation.Forestry => FoundationProductiveActivity.Forestry,
            PhysicalWorkerOccupation.Mining => FoundationProductiveActivity.Mining,
            _ => null
        };
        if (activity is not { } productive) return natural;
        var capital = opening.ProductiveInfrastructure.Read(new FoundationProductiveTarget(node, productive));
        // Exclusive capital benefit; physical node labor admission remains a
        // separate scheduler/host contract, not a productive tariff.
        if (capital.ReservedOwner != worker.Owner || capital.ActivatedCapitalMoneyMicro == 0UL)
            return natural;
        return (natural.Commodity, FoundationInfrastructureCapitalResponse.ProjectProductiveOutput(
            natural.WholeTurnOutput, capital.ActivatedCapitalMoneyMicro, capital.ReferenceMoneyMicro));
    }

    internal static FoundationTemporalEconomicTurnInputs ProjectPersistent(
        WorldState start, IReadOnlyList<FoundationPersistentWorkerSlice> timeline,
        FoundationActivityCommodityDemandPolicy demandPolicy, FoundationAgriculturalProductionPolicy? agriculture = null)
    {
        if (agriculture is null && timeline.Any(s => s.Crop.HasValue))
            throw new InvalidOperationException("Explicit crop production requires configured agricultural policy.");
        if (agriculture is not null && agriculture.Commodities.Any(c => !demandPolicy.Commodities.Contains(c)))
            throw new InvalidOperationException("Every crop requires an explicit market demand entry.");
        if (agriculture is not null && agriculture.BasicResourceCommodities.Any(c => !demandPolicy.Commodities.Contains(c)))
            throw new InvalidOperationException("Every basic resource requires explicit market demand.");
        var people = start.Workers.Workers.ToDictionary(w => w.Id);
        foreach (var group in timeline.GroupBy(s => s.Worker))
        {
            uint cursor = 0;
            foreach (var slice in group.OrderBy(s => s.StartMicro))
            {
                if (slice.StartMicro != cursor || slice.DurationMicro == 0 ||
                    (slice.ProjectId.HasValue && slice.Activity != PhysicalWorkerActivity.Other) ||
                    (slice.ProductiveProjectId.HasValue && (slice.ProjectId.HasValue || slice.Crop.HasValue ||
                        slice.Activity != PhysicalWorkerActivity.Other || slice.Occupation != PhysicalWorkerOccupation.Unassigned)))
                    throw new InvalidOperationException("Invalid persistent worker timeline.");
                cursor = checked(cursor + slice.DurationMicro);
            }
            if (cursor != FoundationPhysicalPresenceDemographicProjector.WholeTurnMicro)
                throw new InvalidOperationException("Incomplete persistent worker timeline.");
        }
        var schedule = timeline.Select(s => new FoundationWorkerTemporalNodeSlice(
            s.Worker, s.Node, s.DurationMicro,
            s.Activity == PhysicalWorkerActivity.Producing ? s.DurationMicro : 0U,
            s.Activity, s.Occupation)).ToArray();
        var presence = FoundationPhysicalPresenceDemographicProjector.Project(start, schedule, demandPolicy);
        const ulong whole = FoundationPhysicalPresenceDemographicProjector.WholeTurnMicro;
        var production = timeline.Where(s => s.Activity == PhysicalWorkerActivity.Producing)
            .GroupBy(s => (s.Worker, s.Node, SliceOccupation: s.Occupation, s.Crop))
            .OrderBy(g => g.Key.Worker.Value).ThenBy(g => g.Key.Node.FineStrategicCellId.Value)
            .ThenBy(g => g.Key.SliceOccupation).ThenBy(g => g.Key.Crop)
            .Select(g =>
            {
                var worker = people[g.Key.Worker];
                ulong effort = g.Aggregate(0UL, (sum, s) => checked(sum + s.DurationMicro));
                var output = ResolveOpeningOutput(start, worker, g.Key.Node, g.Key.SliceOccupation, g.Key.Crop, agriculture);
                return new FoundationTemporalPhysicalProduction(worker.Id, worker.Owner, g.Key.Node,
                    output.Commodity, checked((uint)effort),
                    checked((ulong)((UInt128)output.WholeTurnOutput * effort / whole)),
                    g.Key.SliceOccupation);
            }).ToArray();
        return new FoundationTemporalEconomicTurnInputs(presence, schedule, production);
    }

    public static FoundationTemporalEconomicTurnInputs Project(
        WorldState start, WorldState end,
        IReadOnlyList<FoundationEdgeTravelTurnEvent> travelEvents,
        FoundationActivityCommodityDemandPolicy demandPolicy) =>
        Project(start, end, travelEvents, Array.Empty<PhysicalTacticalWorkerMovedEvent>(),
            demandPolicy);

    /// <summary>
    /// Local activity/occupation commands are executed at the start of the
    /// collective turn: the worker's resulting state applies to its ONE
    /// complete worker-turn. They cannot share a turn with travel for
    /// the same worker or change physical location.
    /// </summary>
    public static FoundationTemporalEconomicTurnInputs Project(
        WorldState start, WorldState end,
        IReadOnlyList<FoundationEdgeTravelTurnEvent> travelEvents,
        IReadOnlyList<PhysicalTacticalWorkerMovedEvent> localEvents,
        FoundationActivityCommodityDemandPolicy demandPolicy)
    {
        ArgumentNullException.ThrowIfNull(localEvents);
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
        var local = new Dictionary<PhysicalWorkerId, PhysicalTacticalWorkerMovedEvent>();
        foreach (var ev in localEvents)
        {
            if (ev is null || ev.Source != ev.Destination ||
                ev.TraversedEdge.HasValue || !local.TryAdd(ev.WorkerId, ev) ||
                !beginning.TryGetValue(ev.WorkerId, out var person) ||
                !ending.TryGetValue(ev.WorkerId, out var after) ||
                person.Owner != ev.Issuer || after.Owner != ev.Issuer ||
                person.TacticalTileId != ev.Source ||
                after.TacticalTileId != ev.Destination ||
                after.Activity != ev.NextActivity ||
                after.Occupation != (ev.NextOccupation ?? person.Occupation))
                throw new InvalidOperationException(
                    "Temporal activity or occupation must match one canonical local journal event.");
        }
        if (beginning.Any(entry =>
                !ending.TryGetValue(entry.Key, out var after) ||
                entry.Value.Owner != after.Owner ||
                (entry.Value.Occupation != after.Occupation &&
                    !local.ContainsKey(entry.Key)) ||
                (entry.Value.Activity != after.Activity &&
                    !local.ContainsKey(entry.Key) &&
                    !travelEvents.Any(ev => ev.WorkerId == entry.Key)) ||
                entry.Value.Commodity != after.Commodity ||
                entry.Value.OutputMicroPerTurn != after.OutputMicroPerTurn ||
                entry.Value.DemandMultiplierMicro != after.DemandMultiplierMicro))
            throw new InvalidOperationException(
                "Unjournaled occupation, activity, or worker-economic change in temporal cycle.");
        if (!beginning.Keys.ToHashSet().SetEquals(ending.Keys))
            throw new InvalidOperationException(
                "Movement cannot create or remove an economic worker.");

        var traveled = new HashSet<PhysicalWorkerId>();
        var schedule = new List<FoundationWorkerTemporalNodeSlice>();
        foreach (var ev in travelEvents)
        {
            if (ev is null || !beginning.TryGetValue(ev.WorkerId, out var person) ||
                person.Owner != ev.Issuer || local.ContainsKey(ev.WorkerId) ||
                !traveled.Add(ev.WorkerId) ||
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
                (local.TryGetValue(person.Id, out var instruction) &&
                    (instruction.Source != person.TacticalTileId ||
                     instruction.NextActivity != after.Activity)) ||
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

        var effectiveOccupations = local.Values
            .Where(ev => ev.NextOccupation.HasValue)
            .ToDictionary(ev => ev.WorkerId, ev => ev.NextOccupation!.Value);
        var report = FoundationPhysicalPresenceDemographicProjector.Project(
            start, schedule, demandPolicy, effectiveOccupations);
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
            report, schedule.ToArray(), production, effectiveOccupations);
    }
}
