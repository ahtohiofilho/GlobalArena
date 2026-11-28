using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationPurchasedDemographicCommand(
    CommandId Id, CivilizationId Issuer, ulong Quantity) : ISimulationCommand;

public sealed record FoundationPurchasedDemographicReservation(
    CivilizationId Owner, ulong NextHistoricalIndex, ulong MoneyMicro,
    IReadOnlyList<FoundationPurchasedBirthCandidate> Parents);

/// <summary>Pure pre-sale admission. Both inputs must originate from the
/// same authoritative turn: the opening and the verified calendar-end state
/// after competing construction debits, before any market credits.
/// Nothing is debited or published here. The collective executor must
/// independently derive these reservations, debit and publish them atomically.
/// </summary>
public static class FoundationPurchasedDemographicAdmission
{
    public static IReadOnlyList<FoundationPurchasedDemographicReservation> Plan(
        WorldState opening, WorldState staged,
        FoundationFineFirstPhysicalGeography fine,
        IEnumerable<FoundationPurchasedDemographicCommand> commands,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(opening);
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentNullException.ThrowIfNull(fine);
        ArgumentNullException.ThrowIfNull(commands);
        if (!opening.PurchasedDemography.IsInitialized ||
            !ReferenceEquals(opening.PurchasedDemography, staged.PurchasedDemography) ||
            !ReferenceEquals(opening.WorldBinding, staged.WorldBinding) ||
            context.Turn.Value == 0UL ||
            opening.Treasury.LastSettledEconomicCycleOrdinal != staged.Treasury.LastSettledEconomicCycleOrdinal ||
            context.Turn.Value != checked(opening.Treasury.LastSettledEconomicCycleOrdinal + 1UL))
            throw new InvalidOperationException("Demographic admission requires one unchanged pre-sale policy and economic cycle.");

        var initial = opening.Workers.Workers.ToDictionary(w => w.Id);
        if (initial.Count != staged.Workers.Workers.Count ||
            staged.Workers.Workers.Any(w => !initial.TryGetValue(w.Id, out var old) ||
                old.Owner != w.Owner || old.BirthWaitTurns != w.BirthWaitTurns))
            throw new InvalidOperationException("Population and opening weights must remain unchanged until demographic close.");

        var requests = commands.ToArray();
        if (requests.Any(c => c is null || !c.Issuer.IsValid || c.Quantity == 0UL ||
                c.Id.Turn != context.Turn || c.Id.Sequence == 0UL) ||
            requests.Select(c => c.Id).Distinct().Count() != requests.Length)
            throw new InvalidOperationException("Demographic requests must be positive, unique and current.");
        var ledger = opening.PurchasedDemography;
        var pricing = new FoundationPurchasedDemographicPricePolicy(
            ledger.InitialMoneyMicro, ledger.ReferenceAcquisitions);
        var results = new List<FoundationPurchasedDemographicReservation>();
        foreach (var group in requests.GroupBy(c => c.Issuer).OrderBy(g => g.Key.Value))
        {
            var owner = group.Key;
            ulong count = group.Aggregate(0UL, (sum, c) => checked(sum + c.Quantity));
            var account = ledger.Get(owner);
            var candidates = FoundationPurchasedBirthLottery.ReadCandidates(staged, fine, owner);
            if (count > (ulong)candidates.Count)
                throw new InvalidOperationException("Insufficient eligible positive-weight parents for the full purchase batch.");
            ulong money = pricing.QuoteBatchMoneyMicro(account.NextHistoricalIndex, count);
            // Check the next historical index also fits after the entire batch.
            _ = ledger.RecordAcquisitions(owner, count);
            var scaled = GlobalArenaMoneyScale.FromMoneyMicro(money);
            if (opening.Treasury.Get(owner).CashScaled < scaled ||
                staged.Treasury.Get(owner).CashScaled < scaled)
                throw new InvalidOperationException("Insufficient pre-sale treasury cash for demographic purchase.");
            var parents = FoundationPurchasedBirthLottery.Select(candidates, owner, context, count);
            results.Add(new(owner, account.NextHistoricalIndex, money, parents));
        }
        return Array.AsReadOnly(results.ToArray());
    }
}
