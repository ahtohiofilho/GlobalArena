using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationPhysicalBirthBatchEvent(
    EventId Id, WorldStateHash Before, WorldStateHash After, string ScheduleDigest,
    IReadOnlyList<PhysicalWorkerRuntimeRecord> Newborns) : ISimulationEvent;

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
        var presence = FoundationTemporalEconomicTurnProjector.ProjectPersistent(opening, timeline.Slices, demand).Presence;
        var surviving = opening.Workers.Workers.Select(w => w.Owner).Distinct().OrderBy(c => c.Value).ToArray();
        var plan = FoundationPhysicalPresenceDemographicProjector.PlanBirths(opening, presence, surviving, context.Turn.Value);
        ulong next = settled.Workers.Workers.Max(w => w.Id.Value);
        var children = plan.Births.Select(b =>
        {
            // Temporary neutral worker profile; no inherited order or automatic work.
            var template = opening.Workers.Workers.First(w => w.Owner == b.Owner);
            return new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(checked(++next)), b.Owner,
                movement.PresentationRegion(b.NatalNode, template.CellId), template.Commodity,
                template.OutputMicroPerTurn, PhysicalWorkerActivity.Other, 1_000_000U,
                b.NatalNode, PhysicalWorkerOccupation.Unassigned, b.NatalNode, b.NatalCity);
        }).ToArray();
        var result = settled.AppendPhysicalBirths(children).AdvanceRevision();
        return (result, new FoundationPhysicalBirthBatchEvent(
            EventId.ForSystemPhase(context.Turn, FoundationTemporalEconomicJournalGate.StablePhaseId, 3UL),
            Hasher.Compute(settled), Hasher.Compute(result), presence.ScheduleDigest, Array.AsReadOnly(children)));
    }

    internal static WorldState Replay(WorldState opening, WorldState settled,
        FoundationPersistentWorkerTurnEvent timeline, FoundationActivityCommodityDemandPolicy demand,
        PhysicalTacticalWorkerMoveEventExecutor movement, SimulationContext context, FoundationPhysicalBirthBatchEvent received)
    {
        var (next, expected) = Record(opening, settled, timeline, demand, movement, context);
        if (received.Id != expected.Id || received.Before != expected.Before || received.After != expected.After ||
            received.ScheduleDigest != expected.ScheduleDigest || !received.Newborns.SequenceEqual(expected.Newborns))
            throw new InvalidOperationException("Invalid or forged physical birth batch.");
        return next;
    }
}
