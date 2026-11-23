using System.Numerics;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R177 research-only stopping candidate. Reads actual observed net values;
/// it does not stop, reprice, or settle the canonical solver.
/// The threshold is an explicit gameplay hypothesis measured in basis points,
/// not a mathematical optimum or a fixed-point certificate.
/// </summary>
internal sealed record FoundationMarginalStopComparison(
    int CandidatePass,
    int CertifiedPass,
    BigInteger CandidateNetScaled,
    BigInteger CertifiedNetScaled,
    BigInteger MissedNetScaled,
    BigInteger FirstPassIncrementScaled,
    BigInteger ThresholdScaled);

internal static class FoundationMarginalEconomicStopCandidate
{
    /// <summary>
    /// Candidate rule: always complete at least one global adjustment;
    /// afterwards, if the gain of the last complete adjustment is no
    /// greater than thresholdBasisPoints of the positive initial net
    /// economic value, retain that allocation. A certified final-pass
    /// zero-movement marker is always a natural stop.
    ///
    /// Only an offline comparison; a live version would also need a
    /// defensible response to temporarily negative marginal passes and
    /// proof that conservation/journal validation work with non-fixed
    /// allocations. These are deliberately NOT claimed here.
    /// </summary>
    internal static FoundationMarginalStopComparison Compare(
        IReadOnlyList<BigInteger> netAfterPass,
        IReadOnlyList<ulong> movedGoodsByPass,
        uint thresholdBasisPoints)
    {
        ArgumentNullException.ThrowIfNull(netAfterPass);
        ArgumentNullException.ThrowIfNull(movedGoodsByPass);
        if (thresholdBasisPoints > 10_000U)
            throw new ArgumentOutOfRangeException(nameof(thresholdBasisPoints));
        if (netAfterPass.Count != movedGoodsByPass.Count + 1 ||
            movedGoodsByPass.Count == 0 ||
            netAfterPass[0] <= BigInteger.Zero ||
            movedGoodsByPass[^1] != 0UL)
            throw new ArgumentException(
                "Compare only actual, positive-initial-net, certified market traces.");
        BigInteger tolerance = netAfterPass[0] *
            thresholdBasisPoints / 10_000U;
        int selected = movedGoodsByPass.Count;
        for (int pass = 1; pass <= movedGoodsByPass.Count; pass++)
        {
            BigInteger gain = netAfterPass[pass] -
                netAfterPass[pass - 1];
            if (movedGoodsByPass[pass - 1] == 0UL ||
                gain <= tolerance)
            {
                selected = pass;
                break;
            }
        }
        return new FoundationMarginalStopComparison(
            selected, movedGoodsByPass.Count,
            netAfterPass[selected], netAfterPass[^1],
            netAfterPass[^1] - netAfterPass[selected],
            netAfterPass[1] - netAfterPass[0], tolerance);
    }
}
