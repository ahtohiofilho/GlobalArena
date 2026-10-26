using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Converts caller-supplied food requirements in grams per resident/cycle to
/// the accepted resident-demand fixed-point rate. Demands can be passed to
/// the opt-in resident market phase. Inexact conversions, unavailable food
/// identities and rates exceeding the accepted rate range fail closed.
/// No consumption, nutritional satisfaction or physical delivery is asserted.
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

            if (!catalog.TryGet(row.CommodityId, out var food))
                throw new InvalidOperationException(
                    $"Food requirement references an unregistered food commodity {row.CommodityId.Value}.");

            if (!knownPoints.Contains(row.EconomicPointId))
                throw new InvalidOperationException(
                    $"Food requirement references an unknown economic point {row.EconomicPointId.Value}.");

            // One whole flow unit equals food.GramsPerFlowUnit grams. The
            // existing resolver uses fixed-point whole flow units.
            var numerator = (UInt128)row.GramsPerResidentPerCycle
                * (ulong)StrategicScalarField.Denominator;
            var denominator = food!.GramsPerFlowUnit;
            if (numerator % denominator != 0U)
                throw new InvalidOperationException(
                    "Food grams-per-resident cannot be represented exactly in this commodity's flow-unit scale.");

            var raw = numerator / denominator;
            if (raw == 0U ||
                raw > (ulong)StrategicScalarField.Denominator)
                throw new InvalidOperationException(
                    "Food requirement exceeds the supported normalized per-resident demand rate.");

            profiles[index] = new EconomicResidentDemandProfile(
                row.EconomicPointId,
                row.CommodityId,
                checked((long)raw));
        }

        return Array.AsReadOnly(profiles);
    }
}
