namespace GlobalArena.Economy;

/// <summary>
/// A physical commodity offer. Only explicitly retained goods stay out of
/// market supply. No involuntary inventory, population wallet or food quota.
/// Values are in the commodity's declared physical unit (not cash or a
/// hard-coded per-person demand quantity).
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
                "Retained goods must be between zero and available physical supply.");
        return new PhysicalOffer(available, retained);
    }
}

/// <summary>
/// The price curve is a policy: price = f(physical offer / actual population).
/// No universal curve, coefficient, budget or resident intake target is supplied.
/// The passed function MUST be pure and deterministic to qualify for promotion
/// into a canonical game ruleset.
/// </summary>
public delegate decimal PopulationPriceFunction(decimal offerPerResident);

/// <summary>
/// Dimensionless relative purchasing capacity. All initial civilizations
/// share neutral 1. The future derived index depends on real previous-cycle
/// productivity, nutrition and development, NOT a money balance or demand
/// quantity. No calibration or persistence is chosen by this value type.
/// </summary>
public readonly record struct AbstractPurchasingPowerIndex
{
    public decimal RelativeCapacity { get; }
    private AbstractPurchasingPowerIndex(decimal relativeCapacity) =>
        RelativeCapacity = relativeCapacity;

    public static AbstractPurchasingPowerIndex Neutral => new(1m);

    public static AbstractPurchasingPowerIndex FromRelativeCapacity(decimal relativeCapacity)
    {
        if (relativeCapacity < 0m)
            throw new ArgumentOutOfRangeException(nameof(relativeCapacity),
                "Abstract purchasing power cannot be negative.");
        return new(relativeCapacity);
    }
}

/// <summary>
/// A deliberately UNCALIBRATED two-input price function. The purchasing
/// power index changes the price, never the supply/population ratio.
/// A game's chosen policy must remain deterministic and normalize its neutral
/// buying-power path to the original f(x). No function is prescribed here.
/// </summary>
public delegate decimal PurchasingPowerPriceFunction(
    decimal offerPerResident, decimal relativePurchasingPower);


/// <summary>
/// The user's market-price algebra, not a receipt or a cash ledger:
/// D = Population, x = Offered/D, price = f(x), reference revenue = price*D.
/// Physical sale, consumption, nutrition and transport are different stages.
/// If there is no offered supply, no price or revenue has been established.
/// </summary>
public readonly record struct PopulationPriceQuote(
    ulong Population,
    decimal Offered,
    decimal OfferPerResident,
    decimal Price,
    decimal ReferenceRevenue,
    bool HasOffer)
{
    public bool IsExecutedSettlement => false;
}

/// <summary>
/// Standalone, O(1), allocation-free (apart from caller-supplied curve) market
/// calculation. Does not import the simulation, legacy demand flows, cash
/// accounts, saves, Godot, transport or hypothetical inventory.
/// </summary>
public static class PopulationPriceKernel
{
    public static PopulationPriceQuote Quote(
        ulong population,
        PhysicalOffer physical,
        PopulationPriceFunction priceFunction)
    {
        ArgumentNullException.ThrowIfNull(priceFunction);
        if (population == 0UL)
            throw new ArgumentOutOfRangeException(nameof(population),
                "No resident market exists without people.");

        var offered = physical.Offered;
        if (offered < 0m)
            throw new ArgumentOutOfRangeException(nameof(physical),
                "Physical market offer cannot be negative.");

        // A market with no goods is not a fictitious sale at f(0).
        if (offered == 0m)
            return new(population, 0m, 0m, 0m, 0m, false);

        var x = offered / population; // D IS population. No demand rate.
        var price = priceFunction(x);
        if (price < 0m)
            throw new ArgumentOutOfRangeException(nameof(priceFunction),
                "A price curve cannot publish negative prices.");

        return CreateQuote(population, offered, x, price);
    }

    /// <summary>
    /// Same strict D = P and x = S/P; purchasing power enters ONLY as a
    /// second input to a caller-defined price function. This is a read-only
    /// market quote, not a cash debit, physical intake or transfer.
    /// </summary>
    public static PopulationPriceQuote Quote(
        ulong population,
        PhysicalOffer physical,
        AbstractPurchasingPowerIndex purchasingPower,
        PurchasingPowerPriceFunction priceFunction)
    {
        ArgumentNullException.ThrowIfNull(priceFunction);
        if (population == 0UL)
            throw new ArgumentOutOfRangeException(nameof(population),
                "No resident market exists without people.");

        var offered = physical.Offered;
        if (offered < 0m)
            throw new ArgumentOutOfRangeException(nameof(physical),
                "Physical market offer cannot be negative.");

        if (offered == 0m)
            return new(population, 0m, 0m, 0m, 0m, false);

        var x = offered / population;
        var price = priceFunction(x, purchasingPower.RelativeCapacity);
        return CreateQuote(population, offered, x, price);
    }

    private static PopulationPriceQuote CreateQuote(
        ulong population, decimal offered, decimal x, decimal price)
    {
        if (price < 0m)
            throw new ArgumentOutOfRangeException(nameof(price),
                "A price curve cannot publish negative prices.");
        var revenueReference = checked(price * population);
        return new(population, offered, x, price, revenueReference, true);
    }
}
