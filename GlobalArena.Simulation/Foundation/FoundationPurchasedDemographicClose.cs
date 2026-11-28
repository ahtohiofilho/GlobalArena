using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationPurchasedDemographicFundingEvent(
    EventId Id, WorldStateHash Before, WorldStateHash After,
    IReadOnlyList<FoundationPurchasedDemographicCommand> Commands,
    IReadOnlyList<FoundationPurchasedDemographicReservation> Reservations) : ISimulationEvent;

public sealed record FoundationPurchasedDemographicBirthEvent(
    EventId Id, WorldStateHash Before, WorldStateHash After,
    IReadOnlyList<PhysicalWorkerId> SelectedParents,
    IReadOnlyList<PhysicalWorkerRuntimeRecord> Newborns) : ISimulationEvent;

internal static class FoundationPurchasedDemographicClose
{
    private static readonly CanonicalWorldStateHasher Hasher = new();

    internal static (WorldState Next, FoundationPurchasedDemographicFundingEvent Event) Fund(
        WorldState opening, WorldState staged, FoundationFineFirstPhysicalGeography fine,
        IEnumerable<FoundationPurchasedDemographicCommand> commands, SimulationContext context)
    {
        var ordered = commands.OrderBy(c => c.Id.Sequence).ToArray();
        var reservations = FoundationPurchasedDemographicAdmission.Plan(opening, staged, fine, ordered, context);
        var next = staged.DebitAuditedDemographicPurchases(reservations.ToDictionary(r => r.Owner, r => r.MoneyMicro));
        return (next, new FoundationPurchasedDemographicFundingEvent(
            EventId.ForSystemPhase(context.Turn, FoundationTemporalEconomicJournalGate.StablePhaseId, 5UL),
            Hasher.Compute(staged), Hasher.Compute(next), Array.AsReadOnly(ordered), reservations));
    }

    internal static (WorldState Next, FoundationPurchasedDemographicFundingEvent Verified) ReplayFunding(
        WorldState opening, WorldState staged, FoundationFineFirstPhysicalGeography fine,
        SimulationContext context, FoundationPurchasedDemographicFundingEvent received)
    {
        var (next, expected) = Fund(opening, staged, fine, received.Commands, context);
        if (received.Id != expected.Id || received.Before != expected.Before || received.After != expected.After ||
            !received.Commands.SequenceEqual(expected.Commands) ||
            received.Reservations.Count != expected.Reservations.Count ||
            received.Reservations.Where((r, i) =>
                r.Owner != expected.Reservations[i].Owner ||
                r.NextHistoricalIndex != expected.Reservations[i].NextHistoricalIndex ||
                r.MoneyMicro != expected.Reservations[i].MoneyMicro ||
                !r.Parents.SequenceEqual(expected.Reservations[i].Parents)).Any())
            throw new InvalidOperationException("Invalid demographic funding evidence.");
        return (next, expected);
    }

    internal static (WorldState Next, FoundationPurchasedDemographicBirthEvent Event) Birth(
        WorldState opening, WorldState settled, FoundationPurchasedDemographicFundingEvent funding,
        PhysicalTacticalWorkerMoveEventExecutor movement, SimulationContext context)
    {
        if (settled.Treasury.LastSettledEconomicCycleOrdinal != checked(opening.Treasury.LastSettledEconomicCycleOrdinal + 1UL) ||
            !ReferenceEquals(opening.PurchasedDemography, settled.PurchasedDemography))
            throw new InvalidOperationException("Purchased births require one verified settled economic cycle.");
        var workers = settled.Workers.Workers.ToDictionary(w => w.Id);
        var selected = new List<PhysicalWorkerId>();
        var children = new List<PhysicalWorkerRuntimeRecord>();
        ulong nextId = workers.Count == 0 ? 0UL : workers.Keys.Max(w => w.Value);
        var openingWeights = opening.Workers.Workers.ToDictionary(w => w.Id, w => w.BirthWaitTurns);
        if (workers.Count != openingWeights.Count || workers.Values.Any(w =>
            !openingWeights.TryGetValue(w.Id, out var weight) || weight != w.BirthWaitTurns))
            throw new InvalidOperationException("Census or wait changed before purchased births.");
        foreach (var reservation in funding.Reservations)
        {
            var debit = settled.Treasury.Get(reservation.Owner).TotalDemographicPurchaseDebitsScaled -
                opening.Treasury.Get(reservation.Owner).TotalDemographicPurchaseDebitsScaled;
            if (debit != GlobalArenaMoneyScale.FromMoneyMicro(reservation.MoneyMicro))
                throw new InvalidOperationException("Purchased births require the exact verified debit.");
            foreach (var parent in reservation.Parents)
            {
                var worker = workers[parent.Worker];
                if (worker.Owner != reservation.Owner || worker.TacticalTileId != parent.Node ||
                    worker.BirthWaitTurns != parent.WaitTurns)
                    throw new InvalidOperationException("Reserved parent changed before publication.");
                selected.Add(worker.Id);
                children.Add(new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(checked(++nextId)),
                    worker.Owner, movement.PresentationRegion(parent.Node, worker.CellId),
                    worker.Commodity, worker.OutputMicroPerTurn, PhysicalWorkerActivity.Other,
                    1_000_000U, parent.Node, PhysicalWorkerOccupation.Unassigned, parent.Node,
                    birthWaitTurns: 0UL));
            }
        }
        var aged = FoundationPurchasedBirthLottery.AdvanceWaitingCounters(settled.Workers.Workers, selected);
        var next = settled.PublishAuditedPurchasedBirths(aged, children).AdvanceRevision();
        return (next, new FoundationPurchasedDemographicBirthEvent(
            EventId.ForSystemPhase(context.Turn, FoundationTemporalEconomicJournalGate.StablePhaseId, 6UL),
            Hasher.Compute(settled), Hasher.Compute(next), Array.AsReadOnly(selected.ToArray()),
            Array.AsReadOnly(children.ToArray())));
    }

    internal static WorldState ReplayBirth(WorldState opening, WorldState settled,
        FoundationPurchasedDemographicFundingEvent funding, PhysicalTacticalWorkerMoveEventExecutor movement,
        SimulationContext context, FoundationPurchasedDemographicBirthEvent received)
    {
        var (next, expected) = Birth(opening, settled, funding, movement, context);
        if (received.Id != expected.Id || received.Before != expected.Before || received.After != expected.After ||
            !received.SelectedParents.SequenceEqual(expected.SelectedParents) ||
            !received.Newborns.SequenceEqual(expected.Newborns))
            throw new InvalidOperationException("Invalid purchased birth evidence.");
        return next;
    }
}
