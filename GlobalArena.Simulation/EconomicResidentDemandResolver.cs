using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Pure recurring consumer-demand resolution against authoritative residents.
/// Requires an initialized v8 census and explicit per-resident rates.
/// Output retains the existing economic point/commodity demand-flow contract.
/// It does not persist goods, mutate the state or combine with worker demand.
/// </summary>
public sealed class EconomicResidentDemandResolver
{
    public IReadOnlyList<EconomicDemandFlow> Resolve(
        WorldState worldState,
        IEnumerable<EconomicResidentDemandProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(worldState);
        ArgumentNullException.ThrowIfNull(profiles);

        if (!worldState.Population.IsInitialized)
            throw new InvalidOperationException(
                "Resident consumption requires an explicitly initialized population census.");

        var points = worldState.Population.PointPopulations
            .ToDictionary(point => point.EconomicPointId);

        var canonical = profiles.ToArray();
        if (canonical.Any(profile => profile is null))
            throw new ArgumentException(
                "Resident demand profiles cannot contain null entries.",
                nameof(profiles));

        canonical = canonical
            .OrderBy(profile => profile.EconomicPointId.Value)
            .ThenBy(profile => profile.CommodityId.Value)
            .ToArray();

        for (var index = 1; index < canonical.Length; index++)
        {
            if (canonical[index - 1].EconomicPointId == canonical[index].EconomicPointId
                && canonical[index - 1].CommodityId == canonical[index].CommodityId)
                throw new ArgumentException(
                    "Resident demand profiles cannot contain duplicate point/commodity keys.",
                    nameof(profiles));
        }

        var demands = new List<EconomicDemandFlow>(canonical.Length);
        foreach (var profile in canonical)
        {
            if (!points.TryGetValue(profile.EconomicPointId, out var population))
                throw new InvalidOperationException(
                    $"Resident demand references unknown economic point {profile.EconomicPointId.Value}.");

            var rawProduct = (UInt128)population.Residents
                * checked((ulong)profile.RawDemandRatePerResident);

            if (rawProduct > ulong.MaxValue)
                throw new OverflowException(
                    "Resident demand exceeds the supported UInt64 fixed-point range.");

            var quantity = checked((ulong)rawProduct);
            if (quantity != 0UL)
                demands.Add(new EconomicDemandFlow(
                    profile.EconomicPointId, profile.CommodityId, quantity));
        }

        return Array.AsReadOnly(demands.ToArray());
    }
}
