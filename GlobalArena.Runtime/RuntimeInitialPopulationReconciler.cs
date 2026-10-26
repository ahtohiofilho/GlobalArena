using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Validates an opt-in, completely localized initial resident census against
/// real generated geography, civilizational ownership and explicit workforce
/// budgets. No demographic rates, participation ratio or settlement population
/// are inferred. Resident population is not persisted in WorldState here.
///
/// Each economic point is given exactly one resident headcount; each
/// materialized civilization is given exactly one total resident budget.
/// A future demographics contract may support residents outside economic
/// points without changing the existing workforce accounting.
/// </summary>
public static class RuntimeInitialPopulationReconciler
{
    public static InitialPopulationReconciliationResult Reconcile(
        WorldGenerationResult generatedWorld,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        IEnumerable<EconomicPointRuntimeState> initialPoints,
        IEnumerable<InitialCivilizationWorkforceBudget> workforceBudgets,
        IEnumerable<InitialCivilizationPopulationBudget> populationBudgets,
        IEnumerable<InitialEconomicPointPopulation> pointPopulations)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(civilizations);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(initialPoints);
        ArgumentNullException.ThrowIfNull(workforceBudgets);
        ArgumentNullException.ThrowIfNull(populationBudgets);
        ArgumentNullException.ThrowIfNull(pointPopulations);

        // Reuse the prior accepted contracts; workforce is already fully
        // accounted for and every point has valid land/territorial ownership.
        var economy = RuntimeInitialWorkforceBudgetReconciler.Materialize(
            generatedWorld, civilizations, territory,
            initialPoints, workforceBudgets);

        var populationBudgetSnapshot = populationBudgets.ToArray();
        if (populationBudgetSnapshot.Any(budget => budget is null))
            throw new ArgumentException(
                "Population budgets cannot contain null entries.",
                nameof(populationBudgets));

        var materializedCivilizations = civilizations.Records
            .Where(record => record.IsMaterialized)
            .Select(record => record.Id)
            .ToHashSet();

        var expectedResidents = new Dictionary<CivilizationId, ulong>(
            materializedCivilizations.Count);

        foreach (var budget in populationBudgetSnapshot)
        {
            if (!materializedCivilizations.Contains(budget.CivilizationId))
                throw new InvalidOperationException(
                    $"Population budget references unknown or unmaterialized civilization {budget.CivilizationId.Value}.");

            if (!expectedResidents.TryAdd(budget.CivilizationId, budget.Residents))
                throw new ArgumentException(
                    $"Duplicate population budget for civilization {budget.CivilizationId.Value}.",
                    nameof(populationBudgets));
        }

        if (expectedResidents.Count != materializedCivilizations.Count)
            throw new InvalidOperationException(
                "Initial census requires exactly one population budget per materialized civilization.");

        var censusSnapshot = pointPopulations.ToArray();
        if (censusSnapshot.Any(record => record is null))
            throw new ArgumentException(
                "Local population records cannot contain null entries.",
                nameof(pointPopulations));

        var indexedPoints = economy.EconomicPoints
            .ToDictionary(point => point.Id);

        var observedPoints = new HashSet<EconomicPointId>();
        var observedResidents = new Dictionary<CivilizationId, ulong>();

        foreach (var record in censusSnapshot)
        {
            if (!indexedPoints.TryGetValue(record.EconomicPointId, out var point))
                throw new InvalidOperationException(
                    $"Population references nonexistent economic point {record.EconomicPointId.Value}.");

            if (!observedPoints.Add(record.EconomicPointId))
                throw new ArgumentException(
                    $"Duplicate census for economic point {record.EconomicPointId.Value}.",
                    nameof(pointPopulations));

            if (record.Residents < point.Workforce)
                throw new InvalidOperationException(
                    $"Economic point {point.Id.Value} cannot have more workers than residents.");

            var prior = observedResidents.TryGetValue(point.Owner, out var value)
                ? value
                : 0UL;

            observedResidents[point.Owner] = checked(prior + record.Residents);
        }

        if (observedPoints.Count != indexedPoints.Count)
            throw new InvalidOperationException(
                "Initial census requires one population record for every economic point.");

        foreach (var budget in populationBudgetSnapshot)
        {
            var actual = observedResidents.TryGetValue(
                budget.CivilizationId, out var residents)
                ? residents
                : 0UL;

            if (actual != budget.Residents)
                throw new InvalidOperationException(
                    $"Initial population for civilization {budget.CivilizationId.Value}: " +
                    $"expected {budget.Residents} residents, localized {actual}.");
        }

        return new InitialPopulationReconciliationResult(
            economy, populationBudgetSnapshot, censusSnapshot);
    }
}
