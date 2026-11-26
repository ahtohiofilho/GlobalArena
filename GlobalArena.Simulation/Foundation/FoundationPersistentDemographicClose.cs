using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationPhysicalBirthBatchEvent(
    EventId Id, WorldStateHash Before, WorldStateHash After, string ScheduleDigest,
    IReadOnlyList<PhysicalWorkerRuntimeRecord> Newborns) : ISimulationEvent
{
    // Null on historical R164 events. In v25, parents align with newborns.
    public IReadOnlyList<PhysicalWorkerId>? SelectedParents { get; init; }
}

public static class FoundationPersistentDemographicClose
{
    private static readonly CanonicalWorldStateHasher Hasher = new();

    internal static (WorldState Next, FoundationPhysicalBirthBatchEvent Event) Record(
        WorldState opening, WorldState settled, FoundationPersistentWorkerTurnEvent timeline,
        FoundationActivityCommodityDemandPolicy demand, PhysicalTacticalWorkerMoveEventExecutor movement,
        SimulationContext context)
    {
        if (!opening.WorkerOrders.IsInitialized || opening.CityResidences.IsInitialized ||
            settled.Treasury.LastSettledEconomicCycleOrdinal != checked(opening.Treasury.LastSettledEconomicCycleOrdinal + 1UL) ||
            opening.Workers.Workers.Count != settled.Workers.Workers.Count)
            throw new InvalidOperationException("Births require one settled persistent turn.");
        var slices = timeline.Slices.Select(s => new FoundationWorkerTemporalNodeSlice(s.Worker, s.Node, s.DurationMicro, s.Activity == PhysicalWorkerActivity.Producing ? s.DurationMicro : 0U, s.Activity, s.Occupation)).ToArray();
        bool individual = opening.Workers.Workers.Any(w => w.BirthWaitTurns.HasValue);
        FoundationPhysicalTemporalPresenceReport presence;
        IReadOnlyList<FoundationPhysicalPresenceBirth> births;
        PhysicalWorkerId[]? parents = null;
        if (individual)
        {
            var plan = FoundationPhysicalPresenceDemographicProjector.PlanIndividualBirths(opening, slices, demand, context.Turn.Value);
            presence = plan.Presence;
            births = plan.Births.Select(b => b.Birth).ToArray();
            parents = plan.Births.Select(b => b.Parent).ToArray();
        }
        else
        {
            presence = FoundationPhysicalPresenceDemographicProjector.Project(opening, slices, demand);
            var surviving = opening.Workers.Workers.Select(w => w.Owner).Distinct().OrderBy(c => c.Value).ToArray();
            births = FoundationPhysicalPresenceDemographicProjector.PlanBirths(opening, presence, surviving, context.Turn.Value).Births;
        }
        ulong next = settled.Workers.Workers.Max(w => w.Id.Value);
        var children = births.Select(b =>
        {
            // Temporary neutral worker profile; no inherited order or automatic work.
            var template = opening.Workers.Workers.First(w => w.Owner == b.Owner);
            return new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(checked(++next)), b.Owner,
                movement.PresentationRegion(b.NatalNode, template.CellId), template.Commodity,
                template.OutputMicroPerTurn, PhysicalWorkerActivity.Other, 1_000_000U,
                b.NatalNode, PhysicalWorkerOccupation.Unassigned, b.NatalNode, b.NatalCity,
                birthWaitTurns: individual ? 0UL : null);
        }).ToArray();
        var weighted = settled;
        if (parents is not null)
        {
            var selected = parents.ToHashSet();
            var openingWait = opening.Workers.Workers.ToDictionary(w => w.Id, w => w.BirthWaitTurns!.Value);
            if (settled.Workers.Workers.Any(w => w.BirthWaitTurns != openingWait[w.Id]))
                throw new InvalidOperationException("Worker waiting counters changed before birth phase.");
            weighted = settled.WithWorkers(new PhysicalWorkerRuntimeState(settled.Workers.Workers.Select(w =>
                w.WithBirthWaitTurns(selected.Contains(w.Id) ? 0UL : checked(openingWait[w.Id] + 1UL))), true, true));
        }
        var result = weighted.AppendPhysicalBirths(children).AdvanceRevision();
        return (result, new FoundationPhysicalBirthBatchEvent(
            EventId.ForSystemPhase(context.Turn, FoundationTemporalEconomicJournalGate.StablePhaseId, 3UL),
            Hasher.Compute(settled), Hasher.Compute(result), presence.ScheduleDigest, Array.AsReadOnly(children))
            { SelectedParents = parents is null ? null : Array.AsReadOnly(parents) });
    }

    internal static WorldState Replay(WorldState opening, WorldState settled,
        FoundationPersistentWorkerTurnEvent timeline, FoundationActivityCommodityDemandPolicy demand,
        PhysicalTacticalWorkerMoveEventExecutor movement, SimulationContext context, FoundationPhysicalBirthBatchEvent received)
    {
        var (next, expected) = Record(opening, settled, timeline, demand, movement, context);
        if (received.Id != expected.Id || received.Before != expected.Before || received.After != expected.After ||
            received.ScheduleDigest != expected.ScheduleDigest || !received.Newborns.SequenceEqual(expected.Newborns) ||
            (received.SelectedParents is null) != (expected.SelectedParents is null) ||
            (expected.SelectedParents is not null && !expected.SelectedParents.SequenceEqual(received.SelectedParents!)))
            throw new InvalidOperationException("Invalid or forged physical birth batch.");
        return next;
    }
}
