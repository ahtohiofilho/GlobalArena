using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Immutable read-only R170 financial reconciliation of a real worker-time
/// market. Not an authoritative receipt, not treasury credit or sales journal.
/// </summary>
public sealed class FoundationTemporalFinancialReconciliation
{
    public FoundationPhysicalGlobalMarginalCoordinationPlan Distribution { get; }
    public FoundationPhysicalEconomicAccountingProposal Accounting { get; }
    public ulong GoodsProducedMicro { get; }
    public IReadOnlyList<FoundationOperationalFreightShadowEdge>? OperationalQuotes { get; }
    public BigInteger CashCreditsScaled { get; }
    public bool IsCommitted => false;

    internal FoundationTemporalFinancialReconciliation(
        FoundationPhysicalGlobalMarginalCoordinationPlan distribution,
        FoundationPhysicalEconomicAccountingProposal accounting,
        ulong goodsProducedMicro, IReadOnlyList<FoundationOperationalFreightShadowEdge>? operationalQuotes = null)
    {
        Distribution = distribution;
        Accounting = accounting;
        GoodsProducedMicro = goodsProducedMicro;
        OperationalQuotes = operationalQuotes;
        CashCreditsScaled = accounting.Accounts.Aggregate(BigInteger.Zero,
            (sum, row) => sum + row.SellerNetScaled +
                row.RevenueTaxScaled + row.TollIncomeScaled);
    }
}

/// <summary>
/// Uses the same universal water-filling, physical freight and monetary
/// reconciler as existing settlement, but with R168 actual worker-time.
/// No worker copies, extra markets or alternative tax formula are introduced.
/// </summary>
public static class FoundationTemporalFinancialReconciler
{
    public static FoundationTemporalFinancialReconciliation Evaluate(
        WorldState baseline,
        PhysicalTacticalIncidenceMap incidence,
        FoundationTemporalEconomicTurnInputs temporal,
        FoundationAuthoritativeEconomicSettlementPolicy policy,
        FoundationFineFirstPhysicalGeography? signedFine = null,
        WorldState? freightSnapshot = null,
        FoundationOperationalFreightShadowPolicy? operationalPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(temporal);
        ArgumentNullException.ThrowIfNull(policy);
        var freightWorld = freightSnapshot ?? baseline;
        if (freightWorld.WorldBinding != baseline.WorldBinding ||
            freightWorld.Workers.Workers.Count != baseline.Workers.Workers.Count)
            throw new InvalidOperationException(
                "Transport-stage market cannot replace the temporal worker census.");
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(
            baseline, temporal, policy.Demand);
        var roads = FoundationPhysicalAuthoritativeTransportProjector.FromWorld(freightWorld, incidence).Roads;
        IReadOnlyList<FoundationOperationalFreightShadowEdge>? operationalQuotes = null;
        if (operationalPolicy is not null)
        {
            if (signedFine is null || !baseline.WorkerOrders.IsInitialized || !ReferenceEquals(baseline, freightWorld))
                throw new InvalidOperationException("Operational freight requires signed persistent opening infrastructure.");
            var projection = ProjectOperationalQuotes(baseline, incidence, signedFine, temporal, roads, operationalPolicy);
            roads = projection.CounterfactualRoads;
            operationalQuotes = projection.EdgeReadings.Where(e => e.OperationalServiceMicro > 0).ToArray();
        }
        var distribution = FoundationTemporalNativeMarginalMarketPlanner.CoordinateVerified(
            baseline, incidence, temporal, policy.Demand, policy.Cargo,
            policy.MaxProducerPasses, signedFine, freightWorld, roads,
            policy.UseTemporalProgressiveDistribution);
        // Matching R169's signed topology, only real permitted freight edges
        // are considered in the financial cost calculation.
        if (signedFine is not null)
        {
            if (signedFine.FineParameters != incidence.Refinement.FineParameters)
                throw new InvalidOperationException("Signed world mismatch.");
            var forbidden = incidence.FineTopology.Edges
                .Where(edge => signedFine.CrossingAtIndices(
                    checked((int)edge.IncidentCellIds[0].Value - 1),
                    checked((int)edge.IncidentCellIds[1].Value - 1)) ==
                    FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                .Select(edge => new PhysicalTacticalEdgeId(
                    incidence.Refinement.FineParameters, edge.Id)).ToHashSet();
            roads = roads.Where(road => !forbidden.Contains(road.Edge)).ToArray();
        }
        var accounts = FoundationPhysicalEconomicAccountingReconciler
            .EvaluateCandidate(market, incidence, policy.Cargo, distribution,
                roads, baseline.Civilizations.Civilizations, policy.TaxRates);
        var outcome = new FoundationTemporalFinancialReconciliation(
            distribution, accounts, temporal.TotalProductionMicro, operationalQuotes);
        if (distribution.AllocationAudit.TotalAvailableGoodsMicro !=
                temporal.TotalProductionMicro ||
            distribution.AllocationAudit.TotalAllocatedGoodsMicro !=
                temporal.TotalProductionMicro ||
            accounts.Accounts.Count != baseline.Civilizations.Civilizations.Count ||
            outcome.CashCreditsScaled + accounts.PhysicalFreightScaled !=
                accounts.GrossSalesScaled ||
            accounts.GrossSalesScaled != accounts.PhysicalFreightScaled +
                accounts.ExternalTollsScaled + accounts.RevenueTaxScaled +
                accounts.NetSellerReceiptsScaled ||
            accounts.ExternalTollsScaled != accounts.Accounts.Aggregate(
                BigInteger.Zero, (sum, account) => sum + account.TollIncomeScaled))
            throw new InvalidOperationException(
                "Temporal physical sales and exact monetary credits do not reconcile.");
        return outcome;
    }
    // Identical fixed-cost/service projection for authoritative reconciliation
    // and diagnostic frozen-flow counterfactuals. Never clears a market.
    internal static FoundationOperationalFreightShadowResult ProjectOperationalQuotes(
        WorldState baseline, PhysicalTacticalIncidenceMap incidence, FoundationFineFirstPhysicalGeography signedFine,
        FoundationTemporalEconomicTurnInputs temporal, IReadOnlyList<FoundationPhysicalRoutingEdgeCost> roads,
        FoundationOperationalFreightShadowPolicy operationalPolicy)
    {
        var nodes = FoundationPhysicalOperationalWorkProjector.ProjectTemporal(baseline, incidence, signedFine, temporal);
        var factors = nodes.SelectMany(n => n.Allocation.Shares).Where(s => s.EffortMicro > 0)
            .GroupBy(s => s.Edge).Select(g => new FoundationOperationalEdgeProductivity(g.Key, g.First().Kind, 1_000_000UL)).ToArray();
        var service = FoundationOperationalServiceProjector.Project(nodes, factors);
        return FoundationOperationalFreightShadowProjector.Project(roads, service, operationalPolicy);
    }

}
