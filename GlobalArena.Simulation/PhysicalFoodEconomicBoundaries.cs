namespace GlobalArena.Simulation;

/// <summary>
/// A market PRICE PRESSURE reference for food. RawPressure is a quantity
/// scale used to shape a price function, never a maximum physical intake.
/// A numeric reference may be above or below realized food consumption.
/// </summary>
public readonly record struct PhysicalFoodPricePressureReference(ulong RawPressure)
{
    // Historical calibration, independent from the V9-V20 food-intake quota
    // even though both were accidentally equal in those save versions.
    public const ulong LegacyRawPerResident = 500_000UL;

    public static PhysicalFoodPricePressureReference FromResidents(
        ulong residents, ulong rawReferencePerResident)
    {
        if (rawReferencePerResident == 0UL)
            throw new ArgumentOutOfRangeException(nameof(rawReferencePerResident),
                "Price pressure needs a positive explicit optimum reference.");
        return new PhysicalFoodPricePressureReference(
            checked(residents * rawReferencePerResident));
    }
}

/// <summary>
/// V9-V20 COMPATIBILITY RULE ONLY. This is the old physical-consumption
/// quota, not an economic demand signal or a biological nutritional target.
/// Keep its exact arithmetic and 500k calibration for deterministic replay;
/// replacement with endogenous voluntary intake needs a separately versioned
/// economy/save migration, not a silent reinterpretation of this struct.
/// </summary>
public readonly record struct LegacyPhysicalFoodIntakeTarget(ulong RawQuota)
{
    public const ulong RawPerResident = 500_000UL;

    public static LegacyPhysicalFoodIntakeTarget FromResidents(ulong residents) =>
        new(checked(residents * RawPerResident));

    public ulong DeliverDirect(ulong available) => Math.Min(available, RawQuota);

    public ulong LimitLegacyQuotedPurchase(ulong quoted, ulong available) =>
        Math.Min(Math.Min(quoted, available), RawQuota);

    public ulong LegacyResidual(ulong actuallyConsumed)
    {
        if (actuallyConsumed > RawQuota)
            throw new ArgumentOutOfRangeException(nameof(actuallyConsumed),
                "Legacy settlement cannot consume above its historical quota.");
        return RawQuota - actuallyConsumed;
    }
}
