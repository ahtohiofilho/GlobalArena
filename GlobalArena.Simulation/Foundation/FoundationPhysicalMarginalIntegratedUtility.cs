using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Deterministic candidate-planning potential for one REAL worker-backed
/// commodity market:
///
///   integrated utility U_j(Q) = 2 * quote_j * sqrt(demand_j * Q)
///   total transport C_ij(q) = q * cargoPerWhole_i *
///                                  (physicalRate_ij + sellerToll_ij)
///
/// All terms are compared in a single positive fixed-point integer scale,
/// with 64 binary fractional bits for sqrt and the original micro-unit
/// conversion scale for freight. This is NOT money received or settled.
/// The concave U has derivative quote * sqrt(demand / Q), the same marginal
/// signal used by the canonical #390/#391 water-filling policy.
/// </summary>
internal static class FoundationPhysicalMarginalIntegratedUtility
{
    private const int FractionalBits = 64;
    private const ulong Micro = FoundationPhysicalMarginalMarketDistributor.Micro;
    private static readonly BigInteger SqrtScale = BigInteger.One <<
        FractionalBits;

    /// <summary>
    /// Exact integer floor of a nonnegative BigInteger square root. The
    /// Newton steps start from a strict power-of-two upper bound, never
    /// depend on platform floating-point or random approximation.
    /// </summary>
    internal static BigInteger IntegerSqrt(BigInteger value)
    {
        if (value.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(value));
        if (value <= BigInteger.One)
            return value;
        int upperBits = checked(value.GetByteCount(isUnsigned: true) * 8);
        BigInteger upper = BigInteger.One << ((upperBits + 1) / 2);
        while (true)
        {
            var next = (upper + value / upper) >> 1;
            if (next >= upper)
                return upper;
            upper = next;
        }
    }

    /// <summary>
    /// U(Q) is in a common 64-bit binary fractional comparison domain,
    /// multiplied by the standard cargo-commodity Micro scale. All callers
    /// compare DIFFERENCES, never charge this potential to a wallet.
    /// </summary>
    internal static BigInteger MarketBenefitScaled(
        ulong quoteMicro, ulong demandMicro, ulong allocatedGoodsMicro)
    {
        if (quoteMicro == 0UL || demandMicro == 0UL)
            throw new ArgumentOutOfRangeException(nameof(quoteMicro),
                "Real market quote and worker demand must be positive.");
        if (allocatedGoodsMicro == 0UL)
            return BigInteger.Zero;
        BigInteger inside = new BigInteger(demandMicro) *
            allocatedGoodsMicro;
        BigInteger root = IntegerSqrt(
            inside << (2 * FractionalBits));
        return 2 * new BigInteger(quoteMicro) * Micro * root;
    }

    /// <summary>
    /// Exact, deterministic fixed-point comparison of a full proposed
    /// seller response against the current allocation. Q aggregates ALL
    /// civilization demand at each physical (tile, commodity), independently
    /// of the bookkeeping buyer to whom physical goods are attributed.
    /// Q of all OTHER
    /// sellers is unchanged; the seller's own positive production is
    /// conserved by the water-filling solver. A strictly positive score
    /// increases the global concave-market potential after transport.
    /// </summary>
    internal static BigInteger ProposalGain(
        IReadOnlyList<FoundationPhysicalMarginalMarketDistributor.Buyer> buyers,
        IReadOnlyList<ulong> oldQuanta,
        IReadOnlyList<ulong> newQuanta,
        ulong goodsQuantum,
        IReadOnlyDictionary<(PhysicalTacticalTileId Tile,
            CommodityId Commodity), ulong> marketTotals)
    {
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(oldQuanta);
        ArgumentNullException.ThrowIfNull(newQuanta);
        ArgumentNullException.ThrowIfNull(marketTotals);
        if (goodsQuantum == 0UL || buyers.Count != oldQuanta.Count ||
            buyers.Count != newQuanta.Count)
            throw new ArgumentException(
                "One exact goods quantum and one pair of quotas per real buyer are required.");

        BigInteger soldBefore = BigInteger.Zero;
        BigInteger soldAfter = BigInteger.Zero;
        BigInteger gain = BigInteger.Zero;
        for (int i = 0; i < buyers.Count; i++)
        {
            ulong oldGoods = checked(oldQuanta[i] * goodsQuantum);
            ulong newGoods = checked(newQuanta[i] * goodsQuantum);
            soldBefore += oldGoods;
            soldAfter += newGoods;
            if (oldGoods == newGoods)
                continue;

            var market = buyers[i].Demand;
            var key = (market.Tile, market.Commodity);
            if (!marketTotals.TryGetValue(key, out ulong oldMarket) ||
                oldMarket < oldGoods)
                throw new InvalidOperationException(
                    "A proposed source allocation exceeds the real market total.");
            BigInteger newMarket = new BigInteger(oldMarket) -
                oldGoods + newGoods;
            if (newMarket < BigInteger.Zero || newMarket > ulong.MaxValue)
                throw new OverflowException(
                    "Proposed real market allocation is outside micro-units.");

            gain += MarketBenefitScaled(
                buyers[i].Quote.QuoteMicroPerWholeCommodityUnit,
                market.DemandMicro, checked((ulong)newMarket));
            gain -= MarketBenefitScaled(
                buyers[i].Quote.QuoteMicroPerWholeCommodityUnit,
                market.DemandMicro, oldMarket);
            gain -= buyers[i].CostScaled *
                (new BigInteger(newGoods) - oldGoods) * SqrtScale;
        }
        if (soldBefore != soldAfter)
            throw new InvalidOperationException(
                "Proposed market response creates or destroys physical output.");
        return gain;
    }
}
