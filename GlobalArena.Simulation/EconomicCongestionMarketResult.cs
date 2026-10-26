namespace GlobalArena.Simulation;

/// <summary>
/// Transient, canonical decision-order market outcome with exact economic
/// values and final shared-edge congestion. It does not implement the flat
/// legacy market settlement or mutate WorldState; downstream consumers must
/// explicitly opt into this new contract.
/// </summary>
public sealed class EconomicCongestionMarketResult
{
    public IReadOnlyList<EconomicCongestionMarketSelection> Selections { get; }
    public FoodStrategicEdgeCongestionResult FinalCongestion { get; }

    public EconomicCongestionMarketResult(
        IEnumerable<EconomicCongestionMarketSelection> selections,
        FoodStrategicEdgeCongestionResult finalCongestion)
    {
        ArgumentNullException.ThrowIfNull(selections);
        ArgumentNullException.ThrowIfNull(finalCongestion);
        var rows = selections.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException("Market selections cannot contain null rows.", nameof(selections));

        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].DecisionOrdinal != (ulong)index + 1UL)
                throw new ArgumentException(
                    "Selections must have contiguous one-based decision ordinals.",
                    nameof(selections));
        }

        Selections = Array.AsReadOnly(rows);
        FinalCongestion = finalCongestion;
    }
}
