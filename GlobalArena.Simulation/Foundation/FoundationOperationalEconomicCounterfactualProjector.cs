using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// ONE uncommitted market solution and its independently reconciled monetary
/// accounts. Monetary values are exact scaled values, NOT treasury balances.
/// </summary>
public sealed record FoundationOperationalEconomicScenarioReading(
    ulong ProducedAndSoldGoodsMicro,
    int DeliveryCount,
    int UsedPhysicalCrossings,
    ulong TotalEdgeTraversalCargoMicro,
    BigInteger GrossSalesScaled,
    BigInteger PhysicalFreightScaled,
    BigInteger ExternalTollsScaled,
    BigInteger RevenueTaxScaled,
    BigInteger NetSellerReceiptsScaled)
{
    /// <summary>
    /// Real traded crossings that intersect a WORKED physical crossing,
    /// independent of whether the experimental discount changed routing.
    /// </summary>
    public int UsedOperationalCrossings { get; init; }
}

/// <summary>
/// Three-way comparison keeps the opportunity cost of labor separate from the
/// benefit of cheaper operating freight. Every run is an uncommitted candidate.
/// </summary>
public sealed class FoundationOperationalEconomicCounterfactual
{
    public FoundationOperationalEconomicScenarioReading AllProducingBaseline { get; }
    public FoundationOperationalEconomicScenarioReading ReassignedWithoutServiceDiscount { get; }
    public FoundationOperationalEconomicScenarioReading ReassignedWithServiceDiscount { get; }
    public FoundationOperationalServiceResult CommittedOperationalService { get; }
    public FoundationOperationalFreightShadowResult FreightShadow { get; }
    public BigInteger LostProductionGoodsMicro { get; }
    public BigInteger OperationalGrossSalesDeltaScaled { get; }
    public BigInteger OperationalSellerNetDeltaScaled { get; }
    public BigInteger OverallSellerNetDeltaScaled { get; }
    public bool IsCommitted => false;

    internal FoundationOperationalEconomicCounterfactual(
        FoundationOperationalEconomicScenarioReading allProducing,
        FoundationOperationalEconomicScenarioReading reassigned,
        FoundationOperationalEconomicScenarioReading shadow,
        FoundationOperationalServiceResult service,
        FoundationOperationalFreightShadowResult freight)
    {
        AllProducingBaseline = allProducing;
        ReassignedWithoutServiceDiscount = reassigned;
        ReassignedWithServiceDiscount = shadow;
        CommittedOperationalService = service;
        FreightShadow = freight;
        LostProductionGoodsMicro = (BigInteger)allProducing.ProducedAndSoldGoodsMicro -
            reassigned.ProducedAndSoldGoodsMicro;
        OperationalGrossSalesDeltaScaled =
            shadow.GrossSalesScaled - reassigned.GrossSalesScaled;
        OperationalSellerNetDeltaScaled =
            shadow.NetSellerReceiptsScaled - reassigned.NetSellerReceiptsScaled;
        OverallSellerNetDeltaScaled =
            shadow.NetSellerReceiptsScaled - allProducing.NetSellerReceiptsScaled;
    }
}

/// <summary>
/// R154: controlled, read-only, THREE separate fixed-price market solutions:
///
/// A) all productive workers, authoritative physical freight;
/// B) logistics-reassigned workers, SAME authoritative freight;
/// C) the SAME reassigned workers, R153 shadow freight.
///
/// A vs B isolates the opportunity cost of losing physical production.
/// B vs C isolates the possible market response to logistics service.
/// A vs C combines both, but does not assert that it is economically positive.
/// No state, journal, cash, investment, freight or trade publication is changed.
/// </summary>
public static class FoundationOperationalEconomicCounterfactualProjector
{
    public static FoundationOperationalEconomicCounterfactual Compare(
        WorldState beforeReassignment,
        WorldState afterReassignment,
        PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography geography,
        FoundationAuthoritativeEconomicSettlementPolicy marketPolicy,
        FoundationOperationalProductivityPolicy productivity,
        FoundationOperationalFreightShadowPolicy shadowPolicy)
    {
        ArgumentNullException.ThrowIfNull(beforeReassignment);
        ArgumentNullException.ThrowIfNull(afterReassignment);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(geography);
        ArgumentNullException.ThrowIfNull(marketPolicy);
        ArgumentNullException.ThrowIfNull(productivity);
        ArgumentNullException.ThrowIfNull(shadowPolicy);
        shadowPolicy.RequireValid();

        if (beforeReassignment.WorldBinding is null ||
            beforeReassignment.WorldBinding != afterReassignment.WorldBinding ||
            !beforeReassignment.Workers.HasAuthoritativeOccupations ||
            !afterReassignment.Workers.HasAuthoritativeOccupations ||
            !beforeReassignment.Workers.HasAuthoritativeTacticalLocations ||
            !afterReassignment.Workers.HasAuthoritativeTacticalLocations ||
            !beforeReassignment.Transport.IsInitialized ||
            !beforeReassignment.Transport.Edges.SequenceEqual(
                afterReassignment.Transport.Edges) ||
            geography.FineParameters != incidence.Refinement.FineParameters ||
            geography.FineElevationRaw.Count !=
                incidence.FineTopology.Cells.Count)
            throw new ArgumentException(
                "The two scenarios must share the exact same bound physical " +
                "world and unchanged authoritative transport inventory.");

        var original = beforeReassignment.Workers.Workers
            .ToDictionary(w => w.Id);
        var changed = afterReassignment.Workers.Workers
            .ToDictionary(w => w.Id);
        if (original.Count == 0 || original.Count != changed.Count ||
            original.Keys.Any(id => !changed.ContainsKey(id)))
            throw new ArgumentException(
                "Counterfactual scenarios must contain the same real workers.");

        int reassigned = 0;
        foreach (var pair in original)
        {
            var a = pair.Value;
            var b = changed[pair.Key];
            if (a.Owner != b.Owner || a.CellId != b.CellId ||
                a.TacticalTileId != b.TacticalTileId ||
                a.Commodity != b.Commodity ||
                a.OutputMicroPerTurn != b.OutputMicroPerTurn ||
                a.DemandMultiplierMicro != b.DemandMultiplierMicro)
                throw new ArgumentException(
                    "Labor reallocation cannot change physical identity, " +
                    "location, owner, productive capacity or consumption.");
            if (a.Activity == b.Activity && a.Occupation == b.Occupation)
                continue;
            if (a.Activity != PhysicalWorkerActivity.Producing ||
                b.Activity != PhysicalWorkerActivity.Other ||
                b.Occupation is not (PhysicalWorkerOccupation.LandTransport or
                    PhysicalWorkerOccupation.PortOperations))
                throw new ArgumentException(
                    "Only explicit producer-to-logistics transfers are supported " +
                    "by this controlled comparison.");
            reassigned++;
        }
        if (reassigned == 0)
            throw new ArgumentException(
                "A comparison requires at least one real producer reallocation.");

        var (authoritativeRoads, carrying) =
            FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
                beforeReassignment, incidence);
        var transportByEdge = afterReassignment.Transport.Edges
            .ToDictionary(e => e.Edge);
        var operationalNodes = FoundationPhysicalOperationalWorkProjector
            .ProjectCommitted(afterReassignment, incidence, geography);
        var rates = operationalNodes
            .SelectMany(n => n.Allocation.Shares)
            .Where(s => s.EffortMicro > 0UL)
            .GroupBy(s => s.Edge)
            .Select(g =>
            {
                var item = g.First();
                if (g.Any(s => s.Kind != item.Kind))
                    throw new InvalidOperationException(
                        "A crossing cannot mix land and port work.");
                return FoundationDiminishingOperationalProductivityProjector
                    .Project(transportByEdge[g.Key], item.Kind, productivity);
            })
            .OrderBy(rate => rate.Edge.FineStrategicEdgeId.Value)
            .ToArray();
        var service = FoundationOperationalServiceProjector.Project(
            operationalNodes, rates);
        var shadow = FoundationOperationalFreightShadowProjector.Project(
            authoritativeRoads, service, shadowPolicy);

        // Exactly the same real ice boundaries are excluded in all solutions.
        var blocked = new HashSet<PhysicalTacticalEdgeId>();
        foreach (var edge in incidence.FineTopology.Edges)
        {
            int a = checked((int)edge.IncidentCellIds[0].Value - 1);
            int b = checked((int)edge.IncidentCellIds[1].Value - 1);
            if (geography.CrossingAtIndices(a, b) ==
                FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                blocked.Add(new PhysicalTacticalEdgeId(
                    incidence.Refinement.FineParameters, edge.Id));
        }

        FoundationOperationalEconomicScenarioReading Solve(
            PhysicalWorkerRuntimeState workforce,
            IReadOnlyList<FoundationPhysicalRoutingEdgeCost> roads)
        {
            var quotes = FoundationPhysicalUniversalMarketPricing
                .QuotesFromRealWorkers(workforce, marketPolicy.Demand);
            var plan = FoundationPhysicalLaggedFreightCyclePlanner
                .CoordinateCandidate(workforce, incidence,
                    marketPolicy.Demand, marketPolicy.Cargo, quotes,
                    roads, carrying,
                    maxProducerPasses: marketPolicy.MaxProducerPasses,
                    blockedEdges: blocked, fixedUnitFreight: true);
            var audit = plan.Distribution.AllocationAudit;
            if (plan.RepricedWithinCycle ||
                audit.TotalAllocatedGoodsMicro !=
                    audit.TotalAvailableGoodsMicro)
                throw new InvalidOperationException(
                    "The comparison requires complete fixed-freight allocations.");
            var accounts = FoundationPhysicalEconomicAccountingReconciler
                .EvaluateCandidate(workforce, incidence,
                    marketPolicy.Demand, marketPolicy.Cargo, plan,
                    marketPolicy.TaxRates);
            var operatedEdges = service.Edges
                .Where(item => item.ServiceMicro > 0UL)
                .Select(item => item.Edge).ToHashSet();
            return new FoundationOperationalEconomicScenarioReading(
                audit.TotalAllocatedGoodsMicro, audit.DeliveryCount,
                audit.Freight.UsedEdges.Count,
                audit.Freight.TotalEdgeTraversalCargoMicro,
                accounts.GrossSalesScaled, accounts.PhysicalFreightScaled,
                accounts.ExternalTollsScaled, accounts.RevenueTaxScaled,
                accounts.NetSellerReceiptsScaled)
            {
                UsedOperationalCrossings = audit.Freight.UsedEdges.Count(
                    item => operatedEdges.Contains(item.Edge))
            };
        }

        var allProducing = Solve(beforeReassignment.Workers, authoritativeRoads);
        var reassignedWithout = Solve(afterReassignment.Workers, authoritativeRoads);
        var reassignedWith = Solve(afterReassignment.Workers,
            shadow.CounterfactualRoads);
        if (allProducing.ProducedAndSoldGoodsMicro <
                reassignedWithout.ProducedAndSoldGoodsMicro ||
            reassignedWithout.ProducedAndSoldGoodsMicro !=
                reassignedWith.ProducedAndSoldGoodsMicro)
            throw new InvalidOperationException(
                "The comparison must preserve the physical production lost " +
                "to logistics, independent of the freight scenario.");

        return new FoundationOperationalEconomicCounterfactual(
            allProducing, reassignedWithout, reassignedWith, service, shadow);
    }
}
