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
    public BigInteger CashCreditsScaled { get; }
    public bool IsCommitted => false;

    internal FoundationTemporalFinancialReconciliation(
        FoundationPhysicalGlobalMarginalCoordinationPlan distribution,
        FoundationPhysicalEconomicAccountingProposal accounting,
        ulong goodsProducedMicro)
    {
        Distribution = distribution;
        Accounting = accounting;
        GoodsProducedMicro = goodsProducedMicro;
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
        FoundationFineFirstPhysicalGeography? signedFine = null)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(incidence);
        ArgumentNullException.ThrowIfNull(temporal);
        ArgumentNullException.ThrowIfNull(policy);
        var market = FoundationTemporalNativeMarketInputs.FromTemporal(
            baseline, temporal, policy.Demand);
        var distribution = FoundationTemporalNativeMarginalMarketPlanner.Coordinate(
            baseline, incidence, temporal, policy.Demand, policy.Cargo,
            policy.MaxProducerPasses, signedFine);
        var roads = FoundationPhysicalAuthoritativeTransportProjector.FromWorld(
            baseline, incidence).Roads;
        // Matching R169's signed topology, only real permitted freight edges
        // are considered in the financial cost calculation.
        if (signedFine is not null)
            roads = roads.Where(road =>
            {
                var edge = incidence.FineTopology.Edges.Single(e =>
                    e.Id == road.Edge.FineStrategicEdgeId);
                int a = checked((int)edge.IncidentCellIds[0].Value - 1);
                int b = checked((int)edge.IncidentCellIds[1].Value - 1);
                return signedFine.CrossingAtIndices(a, b) !=
                    FoundationFineTransportCrossingKind.BlockedIceSeaBoundary;
            }).ToArray();
        var accounts = FoundationPhysicalEconomicAccountingReconciler
            .EvaluateCandidate(market, incidence, policy.Cargo, distribution,
                roads, baseline.Civilizations.Civilizations, policy.TaxRates);
        var outcome = new FoundationTemporalFinancialReconciliation(
            distribution, accounts, temporal.TotalProductionMicro);
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
}
