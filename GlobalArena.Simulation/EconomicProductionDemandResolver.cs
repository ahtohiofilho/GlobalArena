using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

public sealed class EconomicProductionDemandResolver
{
    public EconomicProductionDemandResult Resolve(
        EconomyRuntimeState economy,
        IEnumerable<EconomicProductionPotential> productionPotentials,
        IEnumerable<EconomicDemandProfile> demandProfiles)
    {
        ArgumentNullException.ThrowIfNull(
            economy);

        ArgumentNullException.ThrowIfNull(
            productionPotentials);

        ArgumentNullException.ThrowIfNull(
            demandProfiles);

        var points =
            economy
                .EconomicPoints
                .ToDictionary(
                    point =>
                        point.Id);

        var canonicalProduction =
            productionPotentials.ToArray();

        if (canonicalProduction.Any(
            potential =>
                potential is null))
        {
            throw new ArgumentException(
                "Production potentials cannot contain null entries.",
                nameof(productionPotentials));
        }

        canonicalProduction =
            canonicalProduction
                .OrderBy(
                    potential =>
                        potential.EconomicPointId.Value)
                .ThenBy(
                    potential =>
                        potential.CommodityId.Value)
                .ThenBy(
                    potential =>
                        (byte)potential.Activity)
                .ToArray();

        for (var index = 1;
             index < canonicalProduction.Length;
             index++)
        {
            var previous =
                canonicalProduction[index - 1];

            var current =
                canonicalProduction[index];

            if (previous.EconomicPointId == current.EconomicPointId
                && previous.CommodityId == current.CommodityId
                && previous.Activity == current.Activity)
            {
                throw new ArgumentException(
                    "Production potentials cannot contain duplicate point/commodity/activity keys.",
                    nameof(productionPotentials));
            }
        }

        var canonicalDemand =
            demandProfiles.ToArray();

        if (canonicalDemand.Any(
            profile =>
                profile is null))
        {
            throw new ArgumentException(
                "Demand profiles cannot contain null entries.",
                nameof(demandProfiles));
        }

        canonicalDemand =
            canonicalDemand
                .OrderBy(
                    profile =>
                        profile.EconomicPointId.Value)
                .ThenBy(
                    profile =>
                        profile.CommodityId.Value)
                .ToArray();

        for (var index = 1;
             index < canonicalDemand.Length;
             index++)
        {
            var previous =
                canonicalDemand[index - 1];

            var current =
                canonicalDemand[index];

            if (previous.EconomicPointId == current.EconomicPointId
                && previous.CommodityId == current.CommodityId)
            {
                throw new ArgumentException(
                    "Demand profiles cannot contain duplicate point/commodity keys.",
                    nameof(demandProfiles));
            }
        }

        var supplyFlows =
            new List<EconomicSupplyFlow>(
                canonicalProduction.Length);

        foreach (var potential in
            canonicalProduction)
        {
            if (!points.TryGetValue(
                potential.EconomicPointId,
                out var point))
            {
                throw new InvalidOperationException(
                    $"Production potential references unknown economic point {potential.EconomicPointId.Value}.");
            }

            var allocation =
                point
                    .ActivityAllocations
                    .FirstOrDefault(
                        candidate =>
                            candidate.Kind
                            == potential.Activity);

            if (allocation is null)
            {
                throw new InvalidOperationException(
                    $"Economic point {point.Id.Value} has no workforce allocated to {potential.Activity}.");
            }

            var rawQuantity =
                MultiplyRate(
                    allocation.Workforce,
                    potential.RawSupplyRatePerWorker);

            supplyFlows.Add(
                new EconomicSupplyFlow(
                    point.Id,
                    potential.CommodityId,
                    potential.Activity,
                    rawQuantity));
        }

        var demandFlows =
            new List<EconomicDemandFlow>(
                canonicalDemand.Length);

        foreach (var profile in
            canonicalDemand)
        {
            if (!points.TryGetValue(
                profile.EconomicPointId,
                out var point))
            {
                throw new InvalidOperationException(
                    $"Demand profile references unknown economic point {profile.EconomicPointId.Value}.");
            }

            var rawQuantity =
                MultiplyRate(
                    point.Workforce,
                    profile.RawDemandRatePerWorker);

            if (rawQuantity > 0UL)
            {
                demandFlows.Add(
                    new EconomicDemandFlow(
                        point.Id,
                        profile.CommodityId,
                        rawQuantity));
            }
        }

        return new EconomicProductionDemandResult(
            supplyFlows,
            demandFlows);
    }

    private static ulong MultiplyRate(
        ulong workforce,
        long rawRatePerWorker)
    {
        var product =
            (UInt128)workforce
            * checked(
                (ulong)rawRatePerWorker);

        if (product > ulong.MaxValue)
        {
            throw new OverflowException(
                "Economic flow quantity exceeds the supported UInt64 fixed-point range.");
        }

        return checked(
            (ulong)product);
    }
}
