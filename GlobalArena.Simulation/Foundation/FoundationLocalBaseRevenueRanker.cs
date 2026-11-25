using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation.Foundation;

/// <summary>Local potential output for the actually available effort, with a
/// price from a previously committed local observation or an explicit initial
/// reference. Null means no observation; zero is a valid observed price.</summary>
public sealed record FoundationLocalBaseCandidate(
    PhysicalWorkerOccupation Occupation, byte? Crop, ulong PotentialGoodsMicro,
    ulong? LastLocalPriceMicro, ulong ReferencePriceMicro)
{
    public ulong EffectivePriceMicro => LastLocalPriceMicro ?? ReferencePriceMicro;
    public BigInteger EstimatedRevenue => new BigInteger(PotentialGoodsMicro) * EffectivePriceMicro;
}

/// <summary>Pure local allocation heuristic. The caller supplies candidates
/// and reservations for ONE node/civilization only. No market clearing,
/// routes, mutable world state, or settlement. Reservations affect ranking,
/// not physical production or realized prices.</summary>
public static class FoundationLocalBaseRevenueRanker
{
    public static FoundationLocalBaseCandidate? Select(
        IEnumerable<FoundationLocalBaseCandidate> candidates,
        IReadOnlyDictionary<(PhysicalWorkerOccupation Occupation, byte? Crop), uint> assigned)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(assigned);
        var ordered = candidates.OrderBy(x => x.Occupation).ThenBy(x => x.Crop).ToArray();
        if (ordered.Select(x => (x.Occupation, x.Crop)).Distinct().Count() != ordered.Length)
            throw new ArgumentException("Duplicate local activity.", nameof(candidates));
        FoundationLocalBaseCandidate? best = null;
        BigInteger bestRevenue = BigInteger.Zero;
        BigInteger bestDivisor = BigInteger.One;
        foreach (var candidate in ordered)
        {
            if (candidate.Occupation is not (PhysicalWorkerOccupation.Agriculture or
                PhysicalWorkerOccupation.Forestry or PhysicalWorkerOccupation.Mining))
                throw new ArgumentException("Only productive local activities are eligible.", nameof(candidates));
            if (candidate.ReferencePriceMicro == 0UL)
                throw new ArgumentException("A positive initial reference price is required.", nameof(candidates));
            var revenue = candidate.EstimatedRevenue;
            if (revenue <= BigInteger.Zero) continue;
            var divisor = BigInteger.One + assigned.GetValueOrDefault((candidate.Occupation, candidate.Crop));
            // Compare the rational scores exactly; canonical order wins ties.
            if (best is null || revenue * bestDivisor > bestRevenue * divisor)
            {
                best = candidate;
                bestRevenue = revenue;
                bestDivisor = divisor;
            }
        }
        return best;
    }
}
