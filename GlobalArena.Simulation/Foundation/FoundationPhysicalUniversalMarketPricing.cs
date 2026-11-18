using GlobalArena.Runtime;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Approved Global Arena commodity valuation policy. EVERY real worker-backed
/// item and market has the SAME dimensionless marginal price curve:
///
///     p_i(Q_i) = sqrt(D_i / Q_i), for Q_i > 0.
///
/// Normalized p_i(D_i) = 1.0 money unit per whole commodity unit.
/// D_i is real, weighted demand from the activity policy and Q_i is the
/// quantity delivered. Ownership, civilization, tile, geography, historical
/// era and transport cost must NEVER change this universal coefficient.
/// Freight and seller-specific third-party tolls are subtracted from the
/// seller's proceeds, not added to the consumer's starting price.
///
/// Q=0 is singular in the continuous derivative. The existing discrete
/// water-filling computes the first physically representable increment,
/// rather than inventing an infinite price or a fictional starting stock.
/// </summary>
public static class FoundationPhysicalUniversalMarketPricing
{
    // There are one million micro-money units per whole money unit.
    // This is the NORMALIZATION of p=x^(-1/2), not a discretionary quote.
    public const ulong UnitQuoteMicroPerWholeCommodityUnit = 1_000_000UL;

    /// <summary>
    /// Produce one quote per actual market demand line. The caller supplies
    /// the real census and positive worker activity coefficients, never
    /// per-market price multipliers. Does not invent buyers or inventory.
    /// </summary>
    public static IReadOnlyList<FoundationCommodityMarketPlanningQuote>
        QuotesFromRealWorkers(
            PhysicalWorkerRuntimeState census,
            FoundationActivityCommodityDemandPolicy demandPolicy)
    {
        ArgumentNullException.ThrowIfNull(census);
        ArgumentNullException.ThrowIfNull(demandPolicy);
        var demand = FoundationPhysicalCommodityDemandProjector
            .Project(census, demandPolicy);
        var quotes = demand.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                UnitQuoteMicroPerWholeCommodityUnit)).ToArray();
        if (quotes.Length == 0)
            throw new InvalidOperationException(
                "Universal pricing requires real worker-backed demand; " +
                "no artificial zero-worker markets can be created.");
        return Array.AsReadOnly(quotes);
    }

    /// <summary>
    /// Compatibility validation for the older per-market quote interface.
    /// Call this BEFORE starting planning, and never silently normalize
    /// an inconsistent vector: doing so could hide a caller-side bug.
    /// </summary>
    public static void RequireUniversal(
        IEnumerable<FoundationCommodityMarketPlanningQuote> marketQuotes)
    {
        ArgumentNullException.ThrowIfNull(marketQuotes);
        bool any = false;
        foreach (var quote in marketQuotes)
        {
            any = true;
            if (quote is null ||
                quote.QuoteMicroPerWholeCommodityUnit !=
                    UnitQuoteMicroPerWholeCommodityUnit)
                throw new ArgumentException(
                    "Market-specific reference prices or a globally " +
                    "arbitrary multiplier violate the universal p=x^(-1/2) " +
                    "curve. All items and markets must have a baseline " +
                    "of exactly one whole money unit per whole good unit.",
                    nameof(marketQuotes));
        }
        if (!any)
            throw new ArgumentException(
                "At least one real worker-backed demand line is required.",
                nameof(marketQuotes));
    }
}
