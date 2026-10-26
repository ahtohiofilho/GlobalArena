namespace GlobalArena.Runtime;

/// <summary>
/// Turns a previously validated initial census into an authoritative
/// population snapshot, without changing resident totals or manufacturing
/// demographics. The WorldState cross-domain gate remains authoritative.
/// </summary>
public static class RuntimePopulationMaterializer
{
    public static PopulationRuntimeState Materialize(
        InitialPopulationReconciliationResult initialCensus)
    {
        ArgumentNullException.ThrowIfNull(initialCensus);

        return new PopulationRuntimeState(
            initialCensus.PointPopulations.Select(
                record => new PopulationPointRuntimeState(
                    record.EconomicPointId,
                    record.Residents)));
    }
}
