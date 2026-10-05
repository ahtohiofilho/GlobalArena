namespace GlobalArena.Simulation;

public sealed class EconomicProductionDemandResult
{
    private readonly EconomicSupplyFlow[] _supplyFlows;
    private readonly IReadOnlyList<EconomicSupplyFlow> _readOnlySupplyFlows;
    private readonly EconomicDemandFlow[] _demandFlows;
    private readonly IReadOnlyList<EconomicDemandFlow> _readOnlyDemandFlows;

    public IReadOnlyList<EconomicSupplyFlow> SupplyFlows =>
        _readOnlySupplyFlows;

    public IReadOnlyList<EconomicDemandFlow> DemandFlows =>
        _readOnlyDemandFlows;

    public EconomicProductionDemandResult(
        IEnumerable<EconomicSupplyFlow> supplyFlows,
        IEnumerable<EconomicDemandFlow> demandFlows)
    {
        ArgumentNullException.ThrowIfNull(
            supplyFlows);

        ArgumentNullException.ThrowIfNull(
            demandFlows);

        var canonicalSupply =
            supplyFlows.ToArray();

        if (canonicalSupply.Any(
            flow =>
                flow is null))
        {
            throw new ArgumentException(
                "Supply flow result cannot contain null entries.",
                nameof(supplyFlows));
        }

        canonicalSupply =
            canonicalSupply
                .OrderBy(
                    flow =>
                        flow.EconomicPointId.Value)
                .ThenBy(
                    flow =>
                        flow.CommodityId.Value)
                .ThenBy(
                    flow =>
                        (byte)flow.Activity)
                .ToArray();

        for (var index = 1;
             index < canonicalSupply.Length;
             index++)
        {
            var previous =
                canonicalSupply[index - 1];

            var current =
                canonicalSupply[index];

            if (previous.EconomicPointId == current.EconomicPointId
                && previous.CommodityId == current.CommodityId
                && previous.Activity == current.Activity)
            {
                throw new ArgumentException(
                    "Supply flow result cannot contain duplicate point/commodity/activity keys.",
                    nameof(supplyFlows));
            }
        }

        var canonicalDemand =
            demandFlows.ToArray();

        if (canonicalDemand.Any(
            flow =>
                flow is null))
        {
            throw new ArgumentException(
                "Demand flow result cannot contain null entries.",
                nameof(demandFlows));
        }

        canonicalDemand =
            canonicalDemand
                .OrderBy(
                    flow =>
                        flow.EconomicPointId.Value)
                .ThenBy(
                    flow =>
                        flow.CommodityId.Value)
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
                    "Demand flow result cannot contain duplicate point/commodity keys.",
                    nameof(demandFlows));
            }
        }

        _supplyFlows =
            canonicalSupply;

        _readOnlySupplyFlows =
            Array.AsReadOnly(
                _supplyFlows);

        _demandFlows =
            canonicalDemand;

        _readOnlyDemandFlows =
            Array.AsReadOnly(
                _demandFlows);
    }
}
