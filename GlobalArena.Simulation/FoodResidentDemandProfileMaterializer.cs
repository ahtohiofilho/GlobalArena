using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Converts caller-supplied food identity and per-resident mass requirements
/// into the already accepted resident-demand input contract. No rate or
/// population is synthesized. Result may feed the optional resident-market
/// turn phase unchanged.
/// </summary>
public static class FoodResidentDemandProfileMaterializer
{
    public static IReadOnlyList<EconomicResidentDemandProfile> Materialize(
        WorldState state,
        FoodCommodityCatalog catalog,
        IEnumerable<FoodResidentConsumptionRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(requirements);

        if (!state.Population.IsInitialized)
            throw new InvalidOperationException(
                "Food demand requires an initialized authoritative resident census.");

        var knownPoints = state.Population.PointPopulations
            .Select(point => point.EconomicPointId)
            .ToHashSet();

        var rows = requirements.ToArray();
        if (rows.Any(row => row is null))
            throw new ArgumentException(
                "Food requirements cannot contain null records.",
                nameof(requirements));

        rows = rows.OrderBy(row => row.EconomicPointId.Value)
            .ThenBy(row => row.CommodityId.Value)
            .ToArray();

        var profiles = new EconomicResidentDemandProfile[rows.Length];
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            if (index != 0 &&
                rows[index - 1].EconomicPointId == row.EconomicPointId &&
                rows[index - 1].CommodityId == row.CommodityId)
                throw new ArgumentException(
                    "Duplicate food requirement point/commodity key.",
                    nameof(requirements));

            if (!catalog.Contains(row.CommodityId))
                throw new InvalidOperationException(
                    $"Food requirement references an unregistered food commodity {row.CommodityId.Value}.");

            if (!knownPoints.Contains(row.EconomicPointId))
                throw new InvalidOperationException(
                    $"Food requirement references an unknown economic point {row.EconomicPointId.Value}.");

            profiles[index] = new EconomicResidentDemandProfile(
                row.EconomicPointId,
                row.CommodityId,
                row.RawKilogramsPerResidentPerCycle);
        }

        return Array.AsReadOnly(profiles);
    }
}
