using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// One explicit tactical identity binding for demand. Productivity is a
/// nonnegative scenario FACTOR, not a balance, private salary or bank wallet.
/// Every buyer maps to a unique currently inhabited physical tile.
/// </summary>
public sealed record ExperimentalAbstractBuyerAnchor(
    EconomicPointId Buyer,
    PhysicalTacticalTileId Tile,
    FoodTransportCostFraction PurchasingProductivityFactor);

public sealed record ExperimentalAbstractProducerAnchor(
    EconomicPointId Producer, PhysicalTacticalTileId Tile);

/// <summary>
/// Deliberately unresolved: a declared physical transport service does NOT
/// define a legal owner or a recipient wallet for its freight proceeds.
/// </summary>
public sealed record ExperimentalUnresolvedTransportOperator(
    EconomicPointId Source,
    EconomicPointId Destination,
    ulong ServiceId);

public sealed class ExperimentalAbstractPurchasingPolicy
{
    public string PolicyId { get; }
    public ulong Version { get; }
    public FoodTransportCostFraction BasePurchasingUnitsPerResident { get; }
    public FoodTransportCostFraction ProtectedInvisibleUnitsPerResident { get; }

    public ExperimentalAbstractPurchasingPolicy(
        string policyId, ulong version,
        FoodTransportCostFraction basePurchasingUnitsPerResident,
        FoodTransportCostFraction protectedInvisibleUnitsPerResident)
    {
        if (string.IsNullOrWhiteSpace(policyId) || version == 0)
            throw new ArgumentException("Abstract purchasing policy needs a stable ID/version.");
        BasePurchasingUnitsPerResident =
            basePurchasingUnitsPerResident ??
            throw new ArgumentNullException(nameof(basePurchasingUnitsPerResident));
        ProtectedInvisibleUnitsPerResident =
            protectedInvisibleUnitsPerResident ??
            throw new ArgumentNullException(nameof(protectedInvisibleUnitsPerResident));
        if (BasePurchasingUnitsPerResident.CompareTo(
                ProtectedInvisibleUnitsPerResident) < 0)
            throw new ArgumentException("The protected invisible portion cannot exceed basic purchasing power.");
        PolicyId = policyId;
        Version = version;
    }
}

public sealed record ExperimentalAbstractBuyerProjection(
    EconomicPointId Buyer,
    PhysicalTacticalTileId Tile,
    ulong ResidentWorkers,
    FoodTransportCostFraction ProductivityFactor,
    ExperimentalBuyerCashBudget TemporaryCycleBudget);

public sealed record ExperimentalAbstractProducerProjection(
    EconomicPointId Producer,
    PhysicalTacticalTileId Tile,
    ulong ResidentWorkers);

public sealed record ExperimentalAbstractPurchasingProjection(
    ExperimentalBuyerCashSnapshot BudgetSnapshot,
    IReadOnlyList<ExperimentalAbstractBuyerProjection> Buyers,
    IReadOnlyList<ExperimentalAbstractProducerProjection> Producers,
    IReadOnlyList<ExperimentalUnresolvedTransportOperator> UnresolvedCarriers,
    ulong ResidentWorkersInWorld,
    ulong ResidentWorkersInProjectedBuyers,
    ulong UnprojectedResidentWorkers,
    ulong TravellingWorkers,
    FoodTransportCostFraction TotalTemporaryPurchasingUnits,
    string PolicyId,
    ulong PolicyVersion);

/// <summary>
/// READ ONLY bridge from actual productive physical settlement locations to
/// a per-economic-cycle NUMERIC FORECAST of abstract purchasing capacity.
///
/// Never reads V14 FoodCash balances, persists wallets, or silently turns a
/// transport service into a legal carrier identity. The only monetary
/// numeraire is explicitly chosen by the supplied scenario policy; prices
/// and freight must be calibrated in those same units before actual clearing.
/// No on-world market execution, save mutation or credit is permitted.
/// </summary>
public sealed class ExperimentalAbstractPurchasingPowerProjector
{
    public ExperimentalAbstractPurchasingProjection Project(
        CanonicalPhysicalTacticalSession session,
        EconomicProductionDemandResult flows,
        IEnumerable<ExperimentalAbstractBuyerAnchor> buyerAnchors,
        IEnumerable<ExperimentalAbstractProducerAnchor> producerAnchors,
        IEnumerable<ExperimentalSimultaneousFreightLane> offeredLanes,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(flows);
        ArgumentNullException.ThrowIfNull(buyerAnchors);
        ArgumentNullException.ThrowIfNull(producerAnchors);
        ArgumentNullException.ThrowIfNull(offeredLanes);
        ArgumentNullException.ThrowIfNull(postedPrices);
        ArgumentNullException.ThrowIfNull(policy);

        var settlements = EmergentSettlementProjection.FromSession(session);
        var places = settlements.Sites.ToDictionary(x => x.Tile);
        var fine = session.FineWorld.StrategicTopology.Parameters;
        var demandIds = flows.DemandFlows.Select(x => x.EconomicPointId).ToHashSet();
        var sourceIds = flows.SupplyFlows.Select(x => x.EconomicPointId).ToHashSet();

        var buyers = buyerAnchors.ToArray();
        var producers = producerAnchors.ToArray();
        if (buyers.Any(x => x is null || !x.Buyer.IsValid ||
                !x.Tile.IsValid || x.Tile.FineGoldbergParameters != fine ||
                x.PurchasingProductivityFactor is null ||
                !places.ContainsKey(x.Tile)) ||
            buyers.Select(x => x.Buyer).Distinct().Count() != buyers.Length ||
            buyers.Select(x => x.Tile).Distinct().Count() != buyers.Length ||
            !buyers.Select(x => x.Buyer).ToHashSet().SetEquals(demandIds))
            throw new ArgumentException(
                "Every buyer needs one unique explicitly inhabited physical site; no shared resident base may be counted twice.",
                nameof(buyerAnchors));

        if (producers.Any(x => x is null || !x.Producer.IsValid ||
                !x.Tile.IsValid || x.Tile.FineGoldbergParameters != fine ||
                !places.ContainsKey(x.Tile)) ||
            producers.Select(x => x.Producer).Distinct().Count() != producers.Length ||
            !producers.Select(x => x.Producer).ToHashSet().SetEquals(sourceIds))
            throw new ArgumentException(
                "Every offered supplier needs an explicit, inhabited physical production-site anchor.",
                nameof(producerAnchors));

        var buyerById = buyers.ToDictionary(x => x.Buyer);
        var supplierById = producers.ToDictionary(x => x.Producer);
        foreach (var producer in producers)
            if (buyerById.TryGetValue(producer.Producer, out var alsoBuyer) &&
                alsoBuyer.Tile != producer.Tile)
                throw new ArgumentException(
                    "A dual producer/consumer economic identity must occupy the same physical site.");

        var carriers = new HashSet<(EconomicPointId Source,
            EconomicPointId Destination, ulong Service)>();
        foreach (var lane in offeredLanes)
        {
            if (lane is null ||
                !supplierById.ContainsKey(lane.Supply.EconomicPointId) ||
                !buyerById.ContainsKey(lane.Demand.EconomicPointId) ||
                !flows.SupplyFlows.Any(x =>
                    x.EconomicPointId == lane.Supply.EconomicPointId &&
                    x.CommodityId == lane.Supply.CommodityId &&
                    x.Activity == lane.Supply.Activity) ||
                !flows.DemandFlows.Any(x =>
                    x.EconomicPointId == lane.Demand.EconomicPointId &&
                    x.CommodityId == lane.Demand.CommodityId))
                throw new ArgumentException(
                    "Every service must connect existing explicitly anchored supply and demand.",
                    nameof(offeredLanes));
            foreach (var service in lane.Services)
            {
                if (service is null || service.ServiceId == 0 ||
                    !carriers.Add((lane.Supply.EconomicPointId,
                        lane.Demand.EconomicPointId, service.ServiceId)))
                    throw new ArgumentException(
                        "The projected physical service identity must be nonzero and unambiguous.",
                        nameof(offeredLanes));
            }
        }

        var budgetRows = new List<ExperimentalAbstractBuyerProjection>();
        var budgets = new List<ExperimentalBuyerCashBudget>();
        var total = ExperimentalCashMoney.Zero;
        ulong covered = 0;
        foreach (var anchor in buyers.OrderBy(x => x.Buyer.Value))
        {
            var workers = places[anchor.Tile].Workers;
            covered = checked(covered + workers);
            var baseUnits = ExperimentalCashMoney.Multiply(
                policy.BasePurchasingUnitsPerResident, new BigInteger(workers), 1);
            var gross = ExperimentalCashMoney.Multiply(baseUnits,
                anchor.PurchasingProductivityFactor.Numerator,
                anchor.PurchasingProductivityFactor.Denominator);
            var protectedH = ExperimentalCashMoney.Multiply(
                policy.ProtectedInvisibleUnitsPerResident,
                new BigInteger(workers), 1);
            var temporary = new ExperimentalBuyerCashBudget(
                anchor.Buyer, gross, protectedH);
            budgets.Add(temporary);
            budgetRows.Add(new ExperimentalAbstractBuyerProjection(
                anchor.Buyer, anchor.Tile, workers,
                anchor.PurchasingProductivityFactor, temporary));
            total = ExperimentalCashMoney.Add(total, gross);
        }

        if (covered > settlements.TotalResidentWorkers)
            throw new InvalidOperationException(
                "Projected buyer residency exceeds the canonical physical population.");

        var producerRows = producers.OrderBy(x => x.Producer.Value)
            .Select(x => new ExperimentalAbstractProducerProjection(
                x.Producer, x.Tile, places[x.Tile].Workers)).ToArray();

        var cashSnapshot = new ExperimentalBuyerCashSnapshot(
            budgets, postedPrices);
        cashSnapshot.ValidateCoverage(flows);
        return new ExperimentalAbstractPurchasingProjection(
            cashSnapshot,
            Array.AsReadOnly(budgetRows.ToArray()),
            Array.AsReadOnly(producerRows),
            Array.AsReadOnly(carriers.OrderBy(x => x.Source.Value)
                .ThenBy(x => x.Destination.Value).ThenBy(x => x.Service)
                .Select(x => new ExperimentalUnresolvedTransportOperator(
                    x.Source, x.Destination, x.Service)).ToArray()),
            settlements.TotalResidentWorkers, covered,
            settlements.TotalResidentWorkers - covered,
            settlements.TotalTravellers, total,
            policy.PolicyId, policy.Version);
    }
}
