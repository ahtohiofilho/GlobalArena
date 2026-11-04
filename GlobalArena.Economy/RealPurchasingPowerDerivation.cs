namespace GlobalArena.Economy;

/// <summary>
/// Physically completed FINAL output attributable to a given population and
/// economic cycle. This quantity must eventually come from authenticated
/// conservation/provenance of real goods. No prices or money balances here.
/// Intermediate resale/transport must not be counted a second time.
/// </summary>
public readonly record struct RealFinalOutputRow(uint Commodity, decimal Quantity);

/// <summary>
/// An immutable input snapshot, not an authorization to settle a market.
/// Only a later adapter may prove that all final-output quantities were
/// produced, delivered and attributed in the canonical physical ledger.
/// </summary>
public sealed class RealCompletedCycleObservation
{
    public ulong EconomicCycle { get; }
    public ulong Population { get; }
    public IReadOnlyList<RealFinalOutputRow> FinalOutput { get; }

    private RealCompletedCycleObservation(
        ulong economicCycle, ulong population,
        IReadOnlyList<RealFinalOutputRow> finalOutput)
    {
        EconomicCycle = economicCycle;
        Population = population;
        FinalOutput = finalOutput;
    }

    public static RealCompletedCycleObservation FromPhysicalObservations(
        ulong economicCycle, ulong population,
        IEnumerable<RealFinalOutputRow> finalOutput)
    {
        ArgumentNullException.ThrowIfNull(finalOutput);
        if (economicCycle == 0UL)
            throw new ArgumentOutOfRangeException(nameof(economicCycle),
                "A real-activity observation must be a completed economic cycle.");
        if (population == 0UL)
            throw new ArgumentOutOfRangeException(nameof(population));
        var rows = finalOutput.OrderBy(x => x.Commodity).ToArray();
        if (rows.Length == 0)
            throw new ArgumentException("Real output requires an explicit commodity universe.",
                nameof(finalOutput));
        for (var i = 0; i < rows.Length; i++)
        {
            if (rows[i].Commodity == 0U || rows[i].Quantity < 0m)
                throw new ArgumentOutOfRangeException(nameof(finalOutput),
                    "Commodity keys must be positive and observed physical output nonnegative.");
            if (i != 0 && rows[i - 1].Commodity == rows[i].Commodity)
                throw new ArgumentException("Duplicate physical output commodity.",
                    nameof(finalOutput));
        }
        return new(economicCycle, population, Array.AsReadOnly(rows));
    }
}

/// <summary>
/// A FROZEN base-period UNIT valuation for each distinct commodity.
/// This is a unit-conversion valuation, not a per-resident consumption
/// rate, monetary budget, or arbitrary constant embedded in the kernel.
/// The future canonical adapter must authenticate the baseline price source.
/// </summary>
public readonly record struct FrozenBaseUnitValuation(
    uint Commodity, decimal ReferenceValuePerPhysicalUnit);

/// <summary>
/// Base-period physical-output basket and its frozen comparative valuations.
/// Reusing the same values across cycles prevents nominal repricing alone
/// from manufacturing changes in the real purchasing-power index.
/// </summary>
public sealed class FrozenRealPurchasingPowerBasis
{
    public ulong BaseCycle { get; }
    public ulong BasePopulation { get; }
    public decimal BaseRealOutputValue { get; }
    public IReadOnlyList<FrozenBaseUnitValuation> UnitValuations { get; }

    internal FrozenRealPurchasingPowerBasis(
        RealCompletedCycleObservation baseline,
        IReadOnlyList<FrozenBaseUnitValuation> valuations,
        decimal baseValue)
    {
        BaseCycle = baseline.EconomicCycle;
        BasePopulation = baseline.Population;
        BaseRealOutputValue = baseValue;
        UnitValuations = valuations;
    }
}

/// <summary>
/// Relative real final-output capacity per person compared with a frozen
/// base-cycle basket. The result may be applied to the NEXT economic cycle
/// only, never the same cycle that generated the evidence.
/// This is one explicitly named capacity channel, not an all-factor welfare
/// index and not a financial receipt.
/// </summary>
public readonly record struct LaggedRealPurchasingPowerObservation(
    ulong EvidenceCycle,
    ulong PricingCycle,
    decimal CurrentRealOutputValue,
    decimal BaseRealOutputValue,
    AbstractPurchasingPowerIndex PurchasingPower);

public static class RealPurchasingPowerDerivation
{
    /// <summary>
    /// Fix the commodity universe and base prices before evaluating later
    /// cycles. No hard-coded commodity weights or money injection.
    /// </summary>
    public static FrozenRealPurchasingPowerBasis FreezeBase(
        RealCompletedCycleObservation baseline,
        IEnumerable<FrozenBaseUnitValuation> baseUnitValuations)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(baseUnitValuations);
        var values = baseUnitValuations.OrderBy(x => x.Commodity).ToArray();
        if (values.Length == 0)
            throw new ArgumentException("A base valuation requires commodities.",
                nameof(baseUnitValuations));
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i].Commodity == 0U ||
                values[i].ReferenceValuePerPhysicalUnit <= 0m)
                throw new ArgumentOutOfRangeException(nameof(baseUnitValuations),
                    "Every frozen unit valuation must be strictly positive.");
            if (i > 0 && values[i - 1].Commodity == values[i].Commodity)
                throw new ArgumentException("Duplicate base commodity valuation.",
                    nameof(baseUnitValuations));
        }
        var immutable = Array.AsReadOnly(values);
        var realValue = CalculateBaseValuedOutput(baseline, immutable);
        if (realValue == 0m)
            throw new InvalidOperationException(
                "Cannot normalize an initial civilization from no observed real final output.");
        return new(baseline, immutable, realValue);
    }

    /// <summary>
    /// A candidate N computed from real value per capita, evaluated only
    /// from the COMPLETED preceding cycle. It is intentionally not a
    /// population wallet and has no feedback from this cycle's quoted price.
    /// </summary>
    public static LaggedRealPurchasingPowerObservation FromPriorCycle(
        FrozenRealPurchasingPowerBasis basis,
        RealCompletedCycleObservation previousCompletedCycle,
        ulong pricingCycle)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(previousCompletedCycle);
        if (previousCompletedCycle.EconomicCycle < basis.BaseCycle)
            throw new ArgumentOutOfRangeException(nameof(previousCompletedCycle),
                "Cannot evaluate before the frozen base period.");
        if (checked(previousCompletedCycle.EconomicCycle + 1UL) != pricingCycle)
            throw new ArgumentOutOfRangeException(nameof(pricingCycle),
                "Purchasing power must use the immediately preceding completed cycle.");

        var realValue = CalculateBaseValuedOutput(
            previousCompletedCycle, basis.UnitValuations);
        if (realValue == 0m)
            throw new InvalidOperationException(
                "Zero observed final output does not establish zero purchasing capacity. Additional economic evidence is required.");

        // Compare base-valued REAL output per person, never nominal
        // receipts. This is the fixed-basket quantity-index identity:
        // N = (V_previous / P_previous) / (V_base / P_base).
        // A single algebraic fraction avoids intermediate rounded ratios.
        var numerator = checked(realValue * basis.BasePopulation);
        var denominator = checked(basis.BaseRealOutputValue *
            previousCompletedCycle.Population);
        var relative = numerator / denominator;
        if (relative == 0m)
            throw new OverflowException(
                "Purchasing capacity underflow cannot be replaced by a fabricated floor.");
        var purchasingPower = AbstractPurchasingPowerIndex.FromRelativeCapacity(
            relative);
        return new(previousCompletedCycle.EconomicCycle, pricingCycle,
            realValue, basis.BaseRealOutputValue, purchasingPower);
    }

    private static decimal CalculateBaseValuedOutput(
        RealCompletedCycleObservation snapshot,
        IReadOnlyList<FrozenBaseUnitValuation> valuations)
    {
        if (snapshot.FinalOutput.Count != valuations.Count)
            throw new InvalidOperationException(
                "Real capacity snapshots must cover the identical frozen commodity universe.");
        var realValue = 0m;
        for (var i = 0; i < valuations.Count; i++)
        {
            if (snapshot.FinalOutput[i].Commodity != valuations[i].Commodity)
                throw new InvalidOperationException(
                    "Missing or foreign commodity in a comparative real-output snapshot.");
            realValue = checked(realValue + checked(
                snapshot.FinalOutput[i].Quantity *
                valuations[i].ReferenceValuePerPhysicalUnit));
        }
        return realValue;
    }
}
