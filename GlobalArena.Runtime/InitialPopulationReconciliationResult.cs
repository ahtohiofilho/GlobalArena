namespace GlobalArena.Runtime;

/// <summary>
/// Immutable-view validated initial census snapshot. This is an explicit
/// external bootstrap result: the resident census is NOT part of the
/// authoritative WorldState until a separate demographic state contract exists.
/// </summary>
public sealed class InitialPopulationReconciliationResult
{
    public EconomyRuntimeState Economy { get; }

    public IReadOnlyList<InitialCivilizationPopulationBudget> CivilizationPopulations { get; }

    public IReadOnlyList<InitialEconomicPointPopulation> PointPopulations { get; }

    internal InitialPopulationReconciliationResult(
        EconomyRuntimeState economy,
        IEnumerable<InitialCivilizationPopulationBudget> civilizationPopulations,
        IEnumerable<InitialEconomicPointPopulation> pointPopulations)
    {
        Economy = economy ?? throw new ArgumentNullException(nameof(economy));
        ArgumentNullException.ThrowIfNull(civilizationPopulations);
        ArgumentNullException.ThrowIfNull(pointPopulations);

        CivilizationPopulations = Array.AsReadOnly(
            civilizationPopulations
                .OrderBy(population => population.CivilizationId.Value)
                .ToArray());

        PointPopulations = Array.AsReadOnly(
            pointPopulations
                .OrderBy(population => population.EconomicPointId.Value)
                .ToArray());
    }
}
