using System.Numerics;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Exact, non-monetized deltas for the three precommit worlds in R154.
/// All money uses FoundationPhysicalEconomicAccountingProposal.MoneyScale.
/// This report is neither a recommendation for adding a new person nor
/// proof of a beneficial individual allocation for one civilization.
/// </summary>
public sealed record FoundationOperationalEconomicScenarioDelta(
    BigInteger GoodsMicro,
    int Deliveries,
    int UsedCrossings,
    BigInteger CrossingCargoMicro,
    BigInteger GrossSalesScaled,
    BigInteger PhysicalFreightScaled,
    BigInteger ExternalTollsScaled,
    BigInteger RevenueTaxScaled,
    BigInteger NetSellersScaled,
    BigInteger TotalCivilizationCashEquivalentScaled);

public enum FoundationOperationalEconomicRelativeOutcome : uint
{
    Worse = 1,
    Equivalent = 2,
    Better = 3
}

public sealed class FoundationOperationalEconomicAssessment
{
    public FoundationOperationalEconomicScenarioDelta ProductionOpportunityDelta { get; }
    public FoundationOperationalEconomicScenarioDelta LogisticsServiceDelta { get; }
    public FoundationOperationalEconomicScenarioDelta CombinedDelta { get; }
    public FoundationOperationalEconomicRelativeOutcome CombinedCashOutcome { get; }
    public BigInteger RequiredServiceGainToBreakEvenScaled { get; }
    public BigInteger ActualServiceCashGainScaled { get; }
    public BigInteger RemainingBreakEvenGapScaled { get; }
    public bool IsCommitted => false;

    internal FoundationOperationalEconomicAssessment(
        FoundationOperationalEconomicScenarioDelta production,
        FoundationOperationalEconomicScenarioDelta logistics,
        FoundationOperationalEconomicScenarioDelta combined)
    {
        ProductionOpportunityDelta = production;
        LogisticsServiceDelta = logistics;
        CombinedDelta = combined;
        BigInteger net = combined.TotalCivilizationCashEquivalentScaled;
        CombinedCashOutcome = net.Sign switch
        {
            < 0 => FoundationOperationalEconomicRelativeOutcome.Worse,
            0 => FoundationOperationalEconomicRelativeOutcome.Equivalent,
            _ => FoundationOperationalEconomicRelativeOutcome.Better
        };
        // This includes route/revenue responses to the lost production in B.
        // It is NOT the net value of one new birth/population unit.
        RequiredServiceGainToBreakEvenScaled = BigInteger.Max(
            BigInteger.Zero, -production.TotalCivilizationCashEquivalentScaled);
        ActualServiceCashGainScaled =
            logistics.TotalCivilizationCashEquivalentScaled;
        RemainingBreakEvenGapScaled = BigInteger.Max(BigInteger.Zero,
            RequiredServiceGainToBreakEvenScaled - ActualServiceCashGainScaled);
    }
}

/// <summary>
/// R155 exact numeric diagnosis, NOT a self-adjusting worker optimizer.
/// Delta(A,B) = B-A; the treatment is a TRANSFER of an existing worker.
/// Growing the workforce has different supply AND demand; its own scenario
/// boundary must be implemented later, never inferred from these deltas.
/// </summary>
public static class FoundationOperationalEconomicAssessmentProjector
{
    public static FoundationOperationalEconomicAssessment Evaluate(
        FoundationOperationalEconomicCounterfactual comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        if (comparison.IsCommitted)
            throw new InvalidOperationException(
                "Diagnostics may only inspect uncommitted market comparisons.");
        var a = comparison.AllProducingBaseline;
        var b = comparison.ReassignedWithoutServiceDiscount;
        var c = comparison.ReassignedWithServiceDiscount;
        if (a is null || b is null || c is null ||
            a.ProducedAndSoldGoodsMicro < b.ProducedAndSoldGoodsMicro ||
            b.ProducedAndSoldGoodsMicro != c.ProducedAndSoldGoodsMicro ||
            comparison.LostProductionGoodsMicro !=
                (BigInteger)a.ProducedAndSoldGoodsMicro -
                b.ProducedAndSoldGoodsMicro)
            throw new InvalidOperationException(
                "Economic assessment requires consistent producer-transfer scenarios.");

        static BigInteger CashEquivalent(
            FoundationOperationalEconomicScenarioReading s) =>
            s.GrossSalesScaled - s.PhysicalFreightScaled;

        static FoundationOperationalEconomicScenarioDelta Difference(
            FoundationOperationalEconomicScenarioReading from,
            FoundationOperationalEconomicScenarioReading to) =>
            new(
                (BigInteger)to.ProducedAndSoldGoodsMicro -
                    from.ProducedAndSoldGoodsMicro,
                checked(to.DeliveryCount - from.DeliveryCount),
                checked(to.UsedPhysicalCrossings - from.UsedPhysicalCrossings),
                (BigInteger)to.TotalEdgeTraversalCargoMicro -
                    from.TotalEdgeTraversalCargoMicro,
                to.GrossSalesScaled - from.GrossSalesScaled,
                to.PhysicalFreightScaled - from.PhysicalFreightScaled,
                to.ExternalTollsScaled - from.ExternalTollsScaled,
                to.RevenueTaxScaled - from.RevenueTaxScaled,
                to.NetSellerReceiptsScaled - from.NetSellerReceiptsScaled,
                CashEquivalent(to) - CashEquivalent(from));

        var production = Difference(a, b);
        var logistics = Difference(b, c);
        var combined = Difference(a, c);
        if (production.GoodsMicro + logistics.GoodsMicro !=
                combined.GoodsMicro ||
            production.TotalCivilizationCashEquivalentScaled +
                logistics.TotalCivilizationCashEquivalentScaled !=
                combined.TotalCivilizationCashEquivalentScaled ||
            logistics.GoodsMicro != BigInteger.Zero ||
            comparison.OperationalGrossSalesDeltaScaled !=
                logistics.GrossSalesScaled ||
            comparison.OperationalSellerNetDeltaScaled !=
                logistics.NetSellersScaled ||
            comparison.OverallSellerNetDeltaScaled !=
                combined.NetSellersScaled)
            throw new InvalidOperationException(
                "Transfer and service effects must exactly reconcile with the combined delta.");
        return new FoundationOperationalEconomicAssessment(
            production, logistics, combined);
    }
}
