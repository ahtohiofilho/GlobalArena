using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R77 research-only double-entry reconciliation of prospective tolls against
/// already audited gross SELLER sales credit. This does NOT authorize invoice
/// inputs, publish credits or transfer cash. No buyer purse exists.
/// The actual economic close must independently certify sold quantity, seller
/// receipt, taxable base and route traversal before admitting such entries.
/// </summary>
public static class FoundationPhysicalCandidateTariffTransferReconciliation
{
    public sealed record CandidateDebit(
        PhysicalTacticalEdgeId Edge,
        CivilizationId Seller,
        CivilizationId Owner,
        ulong CargoMicro,
        ulong TariffMicroPerWholeCargoUnit);

    public sealed record CivilizationAllocation(
        CivilizationId Civilization,
        BigInteger GrossSalesCreditScaled,
        BigInteger TollOutflowScaled,
        BigInteger TollInflowScaled,
        BigInteger NetCandidateCreditScaled);

    public sealed record Evidence(
        IReadOnlyList<CivilizationAllocation> Accounts,
        BigInteger TotalGrossSalesScaled,
        BigInteger TotalTollTransferredScaled,
        BigInteger TotalNetCandidateCreditsScaled);

    public static Evidence Reconcile(
        CivilizationTreasuryRuntimeState treasury,
        IReadOnlyDictionary<CivilizationId, BigInteger> auditedGrossSalesCreditsScaled,
        IEnumerable<CandidateDebit> proposedTolls)
    {
        ArgumentNullException.ThrowIfNull(treasury);
        ArgumentNullException.ThrowIfNull(auditedGrossSalesCreditsScaled);
        ArgumentNullException.ThrowIfNull(proposedTolls);
        if (!treasury.IsInitialized ||
            auditedGrossSalesCreditsScaled.Count != treasury.Accounts.Count ||
            treasury.Accounts.Any(account =>
                !auditedGrossSalesCreditsScaled.TryGetValue(
                    account.Civilization, out var credit) || credit.Sign < 0))
            throw new InvalidOperationException(
                "A candidate attribution needs exactly one nonnegative " +
                "gross seller credit for every authorized civilization.");

        var accountIds = treasury.Accounts.Select(a => a.Civilization)
            .ToHashSet();
        var debit = accountIds.ToDictionary(id => id, _ => BigInteger.Zero);
        var income = accountIds.ToDictionary(id => id, _ => BigInteger.Zero);
        var seen = new HashSet<(PhysicalTacticalEdgeId Edge,
            CivilizationId Seller, CivilizationId Owner)>();
        foreach (var toll in proposedTolls)
        {
            if (toll is null || !toll.Edge.IsValid ||
                !accountIds.Contains(toll.Seller) ||
                !accountIds.Contains(toll.Owner) ||
                toll.Seller == toll.Owner ||
                toll.CargoMicro == 0UL ||
                toll.TariffMicroPerWholeCargoUnit == 0UL ||
                !seen.Add((toll.Edge, toll.Seller, toll.Owner)))
                throw new InvalidOperationException(
                    "Candidate tolls must be positive, unique per seller/" +
                    "owner/edge, between distinct real civilizations.");

            // A unit of cargo is exactly 1_000_000 micro-cargo. Apply the
            // tariff to the aggregate cargo by exact BigInteger arithmetic,
            // with ONE deterministic rounding to monetary micro-units per
            // declared candidate edge entry. No arithmetic overflow.
            BigInteger moneyMicro =
                new BigInteger(toll.CargoMicro) *
                toll.TariffMicroPerWholeCargoUnit /
                GlobalArenaMoneyScale.MoneyMicroPerUnit;
            BigInteger scaled =
                moneyMicro * GlobalArenaMoneyScale.ExactScaledPerMoneyMicro;
            debit[toll.Seller] += scaled;
            income[toll.Owner] += scaled;
        }

        var allocations = treasury.Accounts.Select(account =>
        {
            var id = account.Civilization;
            BigInteger gross = auditedGrossSalesCreditsScaled[id];
            if (debit[id] > gross)
                throw new InvalidOperationException(
                    "A freight toll cannot finance itself using nonexistent " +
                    "seller sales or leave a negative seller sales credit.");
            return new CivilizationAllocation(id, gross,
                debit[id], income[id], gross - debit[id] + income[id]);
        }).ToArray();

        var totalGross = allocations.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.GrossSalesCreditScaled);
        var outflow = allocations.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.TollOutflowScaled);
        var inflow = allocations.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.TollInflowScaled);
        var net = allocations.Aggregate(BigInteger.Zero,
            (sum, x) => sum + x.NetCandidateCreditScaled);
        if (outflow != inflow || net != totalGross)
            throw new InvalidOperationException(
                "Candidate toll allocation must be double-entry conservative.");

        return new Evidence(Array.AsReadOnly(allocations),
            totalGross, outflow, net);
    }
}
