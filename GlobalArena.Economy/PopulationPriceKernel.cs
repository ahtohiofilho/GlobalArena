namespace GlobalArena.Economy;

/// <summary>
/// Physical goods available to one market. Deliberate retention is excluded
/// from the offer. Nothing here represents an unfilled demand or a wallet.
/// </summary>
public readonly record struct PhysicalOffer
{
    public decimal Available { get; }
    public decimal Retained { get; }
    public decimal Offered => Available - Retained;

    private PhysicalOffer(decimal available, decimal retained)
    {
        Available = available;
        Retained = retained;
    }

    public static PhysicalOffer FromAvailable(decimal available, decimal retained = 0m)
    {
        if (available < 0m || retained < 0m || retained > available)
            throw new ArgumentOutOfRangeException(nameof(retained),
                "Retained goods must lie between zero and physical availability.");
        return new PhysicalOffer(available, retained);
    }
}

/// <summary>
/// Abstract relative buyer capacity. Neutral = 1 for every initial
/// civilization; future updates must be grounded in verified PRIOR-cycle
/// economic evidence. This index is neither currency nor a food quantity.
/// </summary>
public readonly record struct AbstractPurchasingPowerIndex
{
    public decimal RelativeCapacity { get; }

    private AbstractPurchasingPowerIndex(decimal relativeCapacity) =>
        RelativeCapacity = relativeCapacity;

    public static AbstractPurchasingPowerIndex Neutral => new(1m);

    public static AbstractPurchasingPowerIndex FromRelativeCapacity(decimal relativeCapacity)
    {
        if (relativeCapacity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(relativeCapacity),
                "Buyer capacity must be positive. Zero is not a valid priced market.");
        return new(relativeCapacity);
    }
}

/// <summary>
/// A market price function of ONE relative scarcity argument: x = S / D.
/// The ruleset supplies and calibrates the function; this kernel never
/// introduces an implicit demand-per-person coefficient or a second M input.
/// </summary>
public delegate decimal PopulationPriceFunction(decimal supplyDemandRatio);

/// <summary>
/// Read-only market calculation, NOT a sale, a payment or a food-intake plan.
/// D = P*N, x = offered/D, price = f(x), R_reference = price*D.
/// The reference revenue does not certify shipped physical quantities.
/// </summary>
public readonly record struct PopulationPriceQuote(
    ulong Population,
    decimal RelativePurchasingPower,
    decimal EffectiveDemand,
    decimal Offered,
    decimal SupplyDemandRatio,
    decimal Price,
    decimal ReferenceRevenue,
    bool HasOffer)
{
    public bool IsExecutedSettlement => false;
}

/// <summary>
/// O(1) scalar price kernel. No legacy simulation, bank/cash ledger,
/// inventory policy or consumer-by-consumer loop is imported.
/// </summary>
public static class PopulationPriceKernel
{
    public static PopulationPriceQuote Quote(
        ulong population,
        PhysicalOffer physical,
        PopulationPriceFunction priceFunction) =>
        Quote(population, physical, AbstractPurchasingPowerIndex.Neutral, priceFunction);

    public static PopulationPriceQuote Quote(
        ulong population,
        PhysicalOffer physical,
        AbstractPurchasingPowerIndex purchasingPower,
        PopulationPriceFunction priceFunction)
    {
        ArgumentNullException.ThrowIfNull(priceFunction);
        if (population == 0UL)
            throw new ArgumentOutOfRangeException(nameof(population),
                "A priced population market needs at least one real resident.");
        if (purchasingPower.RelativeCapacity <= 0m)
            throw new ArgumentOutOfRangeException(nameof(purchasingPower),
                "A priced market requires a positive relative purchasing capacity.");

        var offered = physical.Offered;
        if (offered < 0m)
            throw new ArgumentOutOfRangeException(nameof(physical),
                "Physical supply cannot be negative.");

        // D is effective economic pressure, never a physical-intake quota.
        var effectiveDemand = checked(population * purchasingPower.RelativeCapacity);

        // No fictitious transactions or prices when nothing is on offer.
        if (offered == 0m)
            return new(population, purchasingPower.RelativeCapacity,
                effectiveDemand, 0m, 0m, 0m, 0m, false);

        var x = offered / effectiveDemand;
        var price = priceFunction(x);
        if (price < 0m)
            throw new ArgumentOutOfRangeException(nameof(priceFunction),
                "A price curve cannot return a negative price.");

        // R_reference = p*D is a valuation, not a cash transfer, an
        // inventory movement or a quantity physically consumed.
        var revenueReference = checked(price * effectiveDemand);
        return new(population, purchasingPower.RelativeCapacity,
            effectiveDemand, offered, x, price, revenueReference, true);
    }
}
