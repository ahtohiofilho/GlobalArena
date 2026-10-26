using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Checks that each materialized civilization's explicitly supplied initial
/// workforce is accounted for exactly once in its localized economic points.
/// Relies on the existing initial economic-point materializer for geographical
/// and ownership validation. It never invents workers, points or activities.
///
/// This is an opt-in *complete allocation* entry boundary; it is not a
/// population model, a demographics rule or a workforce growth algorithm.
/// </summary>
public static class RuntimeInitialWorkforceBudgetReconciler
{
    public static EconomyRuntimeState Materialize(
        WorldGenerationResult generatedWorld,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        IEnumerable<EconomicPointRuntimeState> initialPoints,
        IEnumerable<InitialCivilizationWorkforceBudget> workforceBudgets)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(civilizations);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(initialPoints);
        ArgumentNullException.ThrowIfNull(workforceBudgets);

        var budgets = workforceBudgets.ToArray();
        if (budgets.Any(budget => budget is null))
            throw new ArgumentException(
                "Initial workforce budgets cannot contain null entries.",
                nameof(workforceBudgets));

        var materializedCivilizations = civilizations.Records
            .Where(record => record.IsMaterialized)
            .Select(record => record.Id)
            .ToHashSet();

        var totals = new Dictionary<CivilizationId, ulong>(
            materializedCivilizations.Count);

        foreach (var budget in budgets)
        {
            if (!materializedCivilizations.Contains(budget.CivilizationId))
                throw new InvalidOperationException(
                    $"Workforce budget references unmaterialized or unknown civilization {budget.CivilizationId.Value}.");

            if (!totals.TryAdd(budget.CivilizationId, budget.Workforce))
                throw new ArgumentException(
                    $"Duplicate initial workforce budget for civilization {budget.CivilizationId.Value}.",
                    nameof(workforceBudgets));
        }

        if (totals.Count != materializedCivilizations.Count)
            throw new InvalidOperationException(
                "Initial workforce accounting requires exactly one budget per materialized civilization.");

        // Reuse PR #2 ownership, territorial control and world land validation.
        var economy = RuntimeInitialEconomicPointMaterializer.Materialize(
            generatedWorld, civilizations, territory, initialPoints);

        var allocatedTotals = new Dictionary<CivilizationId, ulong>();
        foreach (var point in economy.EconomicPoints)
        {
            var prior = allocatedTotals.TryGetValue(point.Owner, out var value)
                ? value
                : 0UL;

            allocatedTotals[point.Owner] = checked(prior + point.Workforce);
        }

        foreach (var budget in budgets)
        {
            var actual = allocatedTotals.TryGetValue(
                budget.CivilizationId, out var allocated)
                ? allocated
                : 0UL;

            if (actual != budget.Workforce)
                throw new InvalidOperationException(
                    $"Initial workforce for civilization {budget.CivilizationId.Value}: " +
                    $"expected {budget.Workforce} workers, allocated {actual}.");
        }

        return economy;
    }
}
