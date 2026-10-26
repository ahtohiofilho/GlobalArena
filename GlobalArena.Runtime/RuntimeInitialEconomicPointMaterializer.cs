using GlobalArena.World;

namespace GlobalArena.Runtime;

/// <summary>
/// Validates explicitly supplied initial economic points against the actual
/// generated planet and its materialized civilization/territory snapshots.
/// It does not invent settlements, workers, activities, or production rates.
/// Multiple economic points per civilization or controlled strategic cell
/// are supported. Existing EconomyRuntimeState canonicalizes point ordering.
/// </summary>
public static class RuntimeInitialEconomicPointMaterializer
{
    public static EconomyRuntimeState Materialize(
        WorldGenerationResult generatedWorld,
        CivilizationRuntimeState civilizations,
        StrategicTerritoryRuntimeState territory,
        IEnumerable<EconomicPointRuntimeState> initialPoints)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(civilizations);
        ArgumentNullException.ThrowIfNull(territory);
        ArgumentNullException.ThrowIfNull(initialPoints);

        var points = initialPoints.ToArray();
        if (points.Any(point => point is null))
            throw new ArgumentException(
                "Initial economic points cannot contain null entries.",
                nameof(initialPoints));

        var materializedOwners = civilizations.Records
            .Where(record => record.IsMaterialized)
            .Select(record => record.Id)
            .ToHashSet();

        var graph = generatedWorld.StrategicSurfaceGraph;
        foreach (var point in points)
        {
            if (!materializedOwners.Contains(point.Owner))
                throw new InvalidOperationException(
                    $"Economic point {point.Id.Value} must belong to a materialized civilization.");

            if (point.StrategicCellId.Value > (ulong)graph.NodeCount)
                throw new InvalidOperationException(
                    $"Economic point {point.Id.Value} lies outside the generated strategic world.");

            if (generatedWorld.StrategicPhysicalFields.LandWater.GetKind(point.StrategicCellId)
                != StrategicLandWaterKind.Land)
                throw new InvalidOperationException(
                    $"Economic point {point.Id.Value} must be placed on strategic land.");

            if (!territory.TryGetController(point.StrategicCellId, out var controller)
                || controller != point.Owner)
                throw new InvalidOperationException(
                    $"Economic point {point.Id.Value} requires control of its strategic cell by its owning civilization.");
        }

        return new EconomyRuntimeState(points);
    }
}
