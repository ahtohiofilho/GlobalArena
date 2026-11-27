using System.Numerics;
using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationNavalFareDebit(FoundationNavalLanding Landing, BigInteger AmountScaled);

public sealed record FoundationNavalSettlementEvent(
    EventId Id, WorldStateHash Before, WorldStateHash After, ulong EconomicCycleOrdinal,
    IReadOnlyList<FoundationNavalFareDebit> Debits) : ISimulationEvent;

internal static class FoundationNavalSettlement
{
    private static readonly CanonicalWorldStateHasher Hasher = new();

    internal static (WorldState Next, FoundationNavalSettlementEvent Event) Record(
        WorldState opening, WorldState settled, FoundationPersistentWorkerTurnEvent timeline,
        SimulationContext context)
    {
        if (opening.EdgeTravels.NavalCargoMicroPerWorker is not { } cargo ||
            settled.EdgeTravels.NavalCargoMicroPerWorker != cargo ||
            settled.Treasury.LastSettledEconomicCycleOrdinal !=
                checked(opening.Treasury.LastSettledEconomicCycleOrdinal + 1UL) ||
            timeline.Id.Turn != context.Turn)
            throw new InvalidOperationException("Naval settlement requires priced, verified current calendar and closed economy.");
        var amounts = settled.Treasury.Accounts.ToDictionary(a => a.Civilization, _ => BigInteger.Zero);
        var debits = timeline.NavalLandings.Select(landing =>
        {
            var amount = FoundationNavalFareQuote.CalculateScaled(landing.Voyage, cargo);
            amounts[landing.Voyage.Owner] += amount;
            return new FoundationNavalFareDebit(landing, amount);
        }).ToArray();
        var ordinal = settled.Treasury.LastSettledEconomicCycleOrdinal;
        var next = settled.RecordAuditedNavalSettlement(ordinal, amounts).AdvanceRevision();
        return (next, new FoundationNavalSettlementEvent(
            EventId.ForSystemPhase(context.Turn, FoundationTemporalEconomicJournalGate.StablePhaseId, 4UL),
            Hasher.Compute(settled), Hasher.Compute(next), ordinal, Array.AsReadOnly(debits)));
    }

    internal static WorldState Replay(WorldState opening, WorldState settled,
        FoundationPersistentWorkerTurnEvent timeline, SimulationContext context,
        FoundationNavalSettlementEvent received)
    {
        var (next, expected) = Record(opening, settled, timeline, context);
        if (received.Id != expected.Id || received.Before != expected.Before ||
            received.After != expected.After || received.EconomicCycleOrdinal != expected.EconomicCycleOrdinal ||
            !received.Debits.SequenceEqual(expected.Debits))
            throw new InvalidOperationException("Invalid naval settlement journal evidence.");
        return next;
    }
}
