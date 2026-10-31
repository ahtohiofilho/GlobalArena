using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class CanonicalTransportSelection
{
    private readonly CanonicalTransportProof _proof;
    public IReadOnlyList<ExperimentalAbstractPhysicalFreightDelivery> Deliveries { get; }
    public ExperimentalBuiltFreightRightsProjection Audit { get; }
    public int EvaluatedConfigurations { get; }
    internal CanonicalTransportSelection(IReadOnlyList<ExperimentalAbstractPhysicalFreightDelivery> deliveries,
        ExperimentalBuiltFreightRightsProjection audit, int evaluated, CanonicalTransportProof proof)
    { Deliveries = deliveries; Audit = audit; EvaluatedConfigurations = evaluated; _proof = proof; }
    public void RequireCurrent(CanonicalPhysicalTacticalSession session) => session.TransportNetwork.Validate(_proof);
    public bool CanAuthorizeRealSettlement => false;
}

/// <summary>
/// Bounded exhaustive reference. Maximizes the sum of fulfilled demand fractions,
/// then minimizes abstract delivered cost. This explicit laboratory objective
/// is not utility maximization or market clearing. No private cash snapshot.
/// </summary>
public sealed class CanonicalSimultaneousTransportSelector
{
    public CanonicalTransportSelection Select(
        CanonicalPhysicalTacticalSession session,
        ExperimentalAbstractPurchasingProjection purchasing,
        EconomicProductionDemandResult flows, FoodCommodityCatalog catalog,
        IEnumerable<ExperimentalAbstractNominalDeliveredQuote> nominalQuotes,
        ExperimentalAbstractPriceFreightCalibrationPolicy calibrationPolicy,
        string physicalFreightNominalUnitId,
        IEnumerable<ExperimentalSimultaneousFreightLane> offeredLanes,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        CanonicalTransportProof proof,
        IReadOnlyDictionary<EconomicPointId, PhysicalTacticalTileId> anchors,
        ulong rawQuantum, int maximumConfigurations = 4096)
    {
        if (rawQuantum == 0 || maximumConfigurations < 1 || maximumConfigurations > 100_000)
            throw new ArgumentOutOfRangeException(nameof(rawQuantum));
        session.TransportNetwork.Validate(proof);
        var quotes = nominalQuotes.ToArray();
        var lanes = offeredLanes.OrderBy(x => x.Supply.EconomicPointId.Value)
            .ThenBy(x => x.Supply.CommodityId.Value).ThenBy(x => x.Supply.Activity)
            .ThenBy(x => x.Demand.EconomicPointId.Value).ToArray();
        var fixedAnchors = anchors.ToDictionary(x => x.Key, x => x.Value);
        if (lanes.Length == 0 || lanes.Length > 128 || lanes.Select(x => (x.Supply.EconomicPointId,
                x.Supply.CommodityId, x.Supply.Activity, x.Demand.EconomicPointId)).Distinct().Count() != lanes.Length)
            throw new ArgumentException("Nonempty unique lanes required.");
        var supply = flows.SupplyFlows.ToDictionary(x => (x.EconomicPointId, x.CommodityId, x.Activity), x => x.RawQuantity);
        var demand = flows.DemandFlows.ToDictionary(x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        var variants = new List<ExperimentalAbstractPhysicalFreightDelivery?>[lanes.Length];
        BigInteger configurations = 1;
        for (var i = 0; i < lanes.Length; i++)
        {
            var lane = lanes[i];
            var available = supply[(lane.Supply.EconomicPointId, lane.Supply.CommodityId, lane.Supply.Activity)];
            var requested = demand[(lane.Demand.EconomicPointId, lane.Demand.CommodityId)];
            var quanta = Math.Min(available, requested) / rawQuantum;
            configurations *= 1 + (BigInteger)quanta * lane.Services.Count;
            if (configurations > maximumConfigurations)
                throw new ArgumentException("Exhaustive configuration limit exceeded; no partial optimum returned.");
            variants[i] = new() { null };
            for (ulong q = 1; q <= quanta; q++)
                foreach (var service in lane.Services.OrderBy(x => x.ServiceId))
                    variants[i].Add(new((ulong)i + 1, lane.Supply.EconomicPointId,
                        lane.Supply.Activity, lane.Demand.EconomicPointId, lane.Demand.CommodityId,
                        checked(q * rawQuantum), service));
        }
        ExperimentalBuiltFreightRightsProjection Audit(ExperimentalAbstractPhysicalFreightDelivery[] lots) =>
            new CanonicalTransportFreightProjector().Project(session, purchasing, flows, catalog,
                quotes, calibrationPolicy, physicalFreightNominalUnitId, lanes, lots,
                congestionPolicy, proof, fixedAnchors);
        var bestLots = Array.Empty<ExperimentalAbstractPhysicalFreightDelivery>();
        var bestAudit = Audit(bestLots); // validates ALL offered services, even unselected ones
        var bestScore = new FoodTransportCostFraction(0, 1);
        var selected = new ExperimentalAbstractPhysicalFreightDelivery?[lanes.Length];
        var evaluated = 0;
        void Visit(int index)
        {
            if (index < lanes.Length)
            {
                foreach (var variant in variants[index]) { selected[index] = variant; Visit(index + 1); }
                return;
            }
            evaluated++;
            var lots = selected.Where(x => x is not null).Select(x => x!).ToArray();
            if (lots.GroupBy(x => (x.Source, x.Commodity, x.Activity)).Any(g =>
                    g.Aggregate(BigInteger.Zero, (s, x) => s + x.RawQuantity) > supply[g.Key]) ||
                lots.GroupBy(x => (x.Buyer, x.Commodity)).Any(g =>
                    g.Aggregate(BigInteger.Zero, (s, x) => s + x.RawQuantity) > demand[g.Key])) return;
            var audit = Audit(lots);
            var reconciliation = audit.InstitutionalProjection.PhysicalReconciliation;
            if (!reconciliation.AllBuyersCanCoverHypotheticalCharges) return;
            var score = new FoodTransportCostFraction(0, 1);
            foreach (var lot in lots)
            {
                var denominator = demand[(lot.Buyer, lot.Commodity)];
                score = new(score.Numerator * denominator + (BigInteger)lot.RawQuantity * score.Denominator,
                    score.Denominator * denominator);
            }
            if (score.CompareTo(bestScore) > 0 || (score.CompareTo(bestScore) == 0 &&
                reconciliation.AbstractDeliveredCharges.CompareTo(
                    bestAudit.InstitutionalProjection.PhysicalReconciliation.AbstractDeliveredCharges) < 0))
            { bestLots = lots; bestAudit = audit; bestScore = score; }
        }
        Visit(0);
        // Independently reconstruct the chosen batch; never return a cached search receipt.
        return new(Array.AsReadOnly(bestLots), Audit(bestLots), evaluated, proof);
    }
}
