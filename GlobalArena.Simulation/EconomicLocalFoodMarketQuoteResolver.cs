using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Quote-only, local one-commodity market; quantity and money are expressed
/// in whole reference units, never as canonical financial balances.
/// This is an economically meaningful reference for a future cash-only
/// settlement gate, not an invented purchase, wage or income stream.
/// </summary>
public sealed record EconomicLocalFoodMarketQuote(
    PhysicalTacticalTileId Tile,
    ulong MovementTurn,
    ulong Workers,
    ulong RawAvailableStock,
    ulong RawProjectedCropOutput,
    ulong RawFoodNeed,
    double ReferenceBudgetPerWorker,
    double PricePerWholeFoodUnit,
    double ReferenceSold,
    double ReferenceUnsold,
    double ReferenceBuyerSpending,
    double ReferenceSellerReceipts,
    double ReferenceBudgetResidual)
{
    public double ReferenceSupply => (RawAvailableStock + (double)RawProjectedCropOutput) / 1_000_000.0;
}

/// <summary>
/// Reuses the audited GA-PROP-001 single-variety reference solver on the
/// exact supply physically available at a tile plus the next cycle's crops.
/// There is no remote delivery, commodity conversion, monetary issuance,
/// physical sale, price commitment, or mutation of the WorldState.
/// </summary>
public sealed class EconomicLocalFoodMarketQuoteResolver
{
    public const ulong RawScale = 1_000_000UL;

    private static readonly EconomicFoodPreferenceParameters SingleVarietyPreferences =
        new(sigma: 1.0, nu: 0.0, lambda: 0.0);

    private readonly EconomicSingleVarietyOriginClearing _clearing = new();

    public EconomicLocalFoodMarketQuote Resolve(
        PhysicalTacticalTileId tile, ulong turn,
        ulong workers, ulong rawStock, ulong rawExpectedCrop,
        ulong rawFoodNeed, double referenceBudgetPerWorker)
    {
        if (!tile.IsValid)
            throw new ArgumentException("Local quote requires a physical tile.", nameof(tile));
        if (workers == 0UL)
            throw new ArgumentOutOfRangeException(nameof(workers),
                "Local quote requires buyers resident at this tile.");
        if (!double.IsFinite(referenceBudgetPerWorker) ||
            referenceBudgetPerWorker <= 0.0 ||
            referenceBudgetPerWorker > 1_000_000.0)
            throw new ArgumentOutOfRangeException(nameof(referenceBudgetPerWorker));

        var supply = rawStock / (double)RawScale +
            rawExpectedCrop / (double)RawScale;
        var budget = workers * referenceBudgetPerWorker;
        if (!double.IsFinite(budget) || budget <= 0)
            throw new ArgumentOutOfRangeException(nameof(referenceBudgetPerWorker));

        if (supply <= 0)
            return new EconomicLocalFoodMarketQuote(tile, turn, workers,
                rawStock, rawExpectedCrop, rawFoodNeed, referenceBudgetPerWorker,
                0, 0, 0, 0, 0, budget);

        // The ID is a temporary quote-local buyer key derived from the tile;
        // it is not a strategic EconomicPoint or a new persistent city.
        var buyers = new[] { new EconomicSingleVarietyBuyer(
            new EconomicPointId(tile.FineStrategicCellId.Value),
            workers, budget, freightPerUnit: 0.0) };
        var result = _clearing.Resolve(supply, buyers, SingleVarietyPreferences);
        var buyer = result.Buyers[0];
        var tolerance = 1e-8 * Math.Max(1.0, supply);
        if (!double.IsFinite(result.OriginPrice) ||
            result.OriginPrice < 0 ||
            result.Sold < 0 || result.Sold > supply + tolerance ||
            result.Unsold < 0 ||
            result.TotalBuyerExpenditure > budget + tolerance ||
            Math.Abs(result.TotalBuyerExpenditure - result.SellerReceipts) > tolerance ||
            Math.Abs(buyer.MonetaryResidual + buyer.CommodityExpenditure - budget) > tolerance)
            throw new InvalidOperationException(
                "Local food market quote violated supply or budget invariants.");

        return new EconomicLocalFoodMarketQuote(tile, turn, workers,
            rawStock, rawExpectedCrop, rawFoodNeed, referenceBudgetPerWorker,
            result.OriginPrice, result.Sold, result.Unsold,
            result.TotalBuyerExpenditure, result.SellerReceipts,
            buyer.MonetaryResidual);
    }
}
