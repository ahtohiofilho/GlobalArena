using GlobalArena.Foundation.Circulation;

namespace GlobalArena.Foundation.Circulation.Tests;

public sealed class EdgeTariffReferenceLaboratoryTests
{
    private const long S = EdgeTariffReferenceLaboratory.Scale;

    private static NaturalEdgeEvaluation NeutralLand() =>
        NaturalEdgeEvaluator.Evaluate(
            new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Land, 0, 0, 0),
            NaturalEdgeReferenceProfiles.CandidateA);

    private static NaturalEdgeEvaluation Coast() =>
        NaturalEdgeEvaluator.Evaluate(
            new NaturalLogicalEdge(NodeMedium.Land, NodeMedium.Water, 0, -800_000, 0),
            NaturalEdgeReferenceProfiles.CandidateA);

    private static EdgeTariffReference Reference(long investedCapital, int p = 4)
    {
        var natural = NeutralLand();
        var improved = EdgeInvestmentLaboratory.Evaluate(natural, investedCapital, S);
        return EdgeTariffReferenceLaboratory.Evaluate(natural, improved, p);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void NaturalInfrastructureWithZeroCapitalHasZeroTariff(int exponent)
    {
        var natural = NeutralLand();
        var improved = EdgeInvestmentLaboratory.Evaluate(natural, 0, S);
        var tariff = EdgeTariffReferenceLaboratory.Evaluate(natural, improved, exponent);

        Assert.Equal(S, tariff.NaturalReferenceLoadRateRaw);
        Assert.Equal(2 * S, tariff.NaturalReferenceImpedanceRaw);
        Assert.Equal(tariff.NaturalReferenceImpedanceRaw, tariff.InvestedReferenceImpedanceRaw);
        Assert.Equal(0, tariff.PhysicalGainRaw);
        Assert.Equal(0, tariff.WeightRaw);
        Assert.Equal(0, tariff.IncorporatedCapitalRaw);
        Assert.Equal(0, EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            tariff, Array.Empty<long>()));
    }

    [Theory]
    [InlineData(3, 836_363L)]
    [InlineData(4, 846_846L)]
    [InlineData(5, 852_017L)]
    public void TwofoldImprovementMatchesFixedReferenceLoadSensitivity(
        int globalExponent, long expectedWeightRaw)
    {
        var natural = NeutralLand();
        var improved = EdgeInvestmentLaboratory.Evaluate(natural, S, S);
        var result = EdgeTariffReferenceLaboratory.Evaluate(natural, improved, globalExponent);

        Assert.Equal(2 * S, result.NaturalReferenceImpedanceRaw);
        Assert.Equal(S, result.NaturalReferenceLoadRateRaw);
        Assert.Equal(500_000, improved.FreeFlowImpedanceRaw);
        Assert.Equal(2 * S, improved.CapacityRateRaw);
        Assert.Equal(expectedWeightRaw, result.WeightRaw);
        Assert.True(result.PhysicalGainRaw >= result.WeightRaw);
        Assert.True(result.WeightRaw <= result.NaturalReferenceImpedanceRaw / 2);
    }

    [Fact]
    public void FixedReferenceTariffRemainsIndependentOfCurrentCommercialLoad()
    {
        var natural = Coast();
        var improved = EdgeInvestmentLaboratory.Evaluate(natural, 3 * S, S);
        var reference = EdgeTariffReferenceLaboratory.Evaluate(natural, improved, 4);

        var noTraffic = EdgeCongestionLaboratory.Evaluate(improved, 0, 4);
        var severeTraffic = EdgeCongestionLaboratory.Evaluate(improved, 4 * S, 4);
        Assert.NotEqual(noTraffic.EffectiveImpedanceRaw, severeTraffic.EffectiveImpedanceRaw);
        Assert.Equal(natural.NaturalCapacityRateRaw, reference.NaturalReferenceLoadRateRaw);
        Assert.Equal(reference,
            EdgeTariffReferenceLaboratory.Evaluate(natural, improved, 4));
        Assert.True(reference.WeightRaw > 0);
    }

    [Fact]
    public void CapacityOnlyInvestmentGeneratesReferenceGainDespiteUnchangedFreeFlow()
    {
        var natural = NeutralLand();
        var capacityOnly = new FreeFlowInvestedEdge(
            TotalInvestedCapitalRaw: S, ReferenceCapitalRaw: S,
            ImprovementFactorRaw: S, FreeFlowImpedanceRaw: S,
            CapacityRateRaw: 2 * S);
        var tariff = EdgeTariffReferenceLaboratory.Evaluate(natural, capacityOnly, 4);

        Assert.Equal(S, capacityOnly.FreeFlowImpedanceRaw);
        Assert.Equal(2 * S, tariff.NaturalReferenceImpedanceRaw);
        Assert.Equal(1_062_500, tariff.InvestedReferenceImpedanceRaw);
        Assert.Equal(937_500, tariff.PhysicalGainRaw);
        Assert.Equal(638_297, tariff.WeightRaw);
    }

    [Fact]
    public void FreeFlowOnlyImprovementAlsoGeneratesReferenceGain()
    {
        var natural = NeutralLand();
        var efficiencyOnly = new FreeFlowInvestedEdge(
            TotalInvestedCapitalRaw: S, ReferenceCapitalRaw: S,
            ImprovementFactorRaw: S, FreeFlowImpedanceRaw: 500_000,
            CapacityRateRaw: S);
        var tariff = EdgeTariffReferenceLaboratory.Evaluate(natural, efficiencyOnly, 4);

        Assert.Equal(S, tariff.InvestedReferenceImpedanceRaw);
        Assert.Equal(S, tariff.PhysicalGainRaw);
        Assert.Equal(666_666, tariff.WeightRaw);
    }

    [Fact]
    public void CoownershipExemptsOnlySellersOwnCapitalFraction()
    {
        var reference = Reference(S);
        long[] holdings = { 250_000, 750_000 }; // A 25%, B 75%
        long weightForA = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            reference, holdings, sellerSlot: 0);
        long weightForB = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            reference, holdings, sellerSlot: 1);
        long weightForOutside = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            reference, holdings, sellerSlot: -1);

        Assert.Equal(846_846, reference.WeightRaw);
        Assert.Equal(635_134, weightForA);
        Assert.Equal(211_711, weightForB);
        Assert.Equal(reference.WeightRaw, weightForOutside);

        Assert.Equal(0, EdgeTariffOwnershipLaboratory.OwnerExternalComponentRaw(
            reference, holdings, sellerSlot: 0, ownerSlot: 0));
        Assert.Equal(weightForA,
            EdgeTariffOwnershipLaboratory.OwnerExternalComponentRaw(
                reference, holdings, sellerSlot: 0, ownerSlot: 1));
        Assert.Equal(weightForB,
            EdgeTariffOwnershipLaboratory.OwnerExternalComponentRaw(
                reference, holdings, sellerSlot: 1, ownerSlot: 0));
    }

    [Fact]
    public void SoleOwnerUsesOwnEdgeWithoutTariffAndOutsideSellerPaysWeight()
    {
        var reference = Reference(S);
        long[] holdings = { S };
        Assert.Equal(0, EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            reference, holdings, 0));
        Assert.Equal(reference.WeightRaw, EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            reference, holdings, -1));
        Assert.Equal(0, EdgeTariffOwnershipLaboratory.OwnerExternalComponentRaw(
            reference, holdings, 0, 0));
    }

    [Fact]
    public void ExternalWeightAndOwnershipDoNotAffectPhysicalImpedance()
    {
        var natural = NeutralLand();
        var improved = EdgeInvestmentLaboratory.Evaluate(natural, S, S);
        var reference = EdgeTariffReferenceLaboratory.Evaluate(natural, improved, 4);
        long[] ownershipA = { 250_000, 750_000 };
        long[] ownershipB = { 500_000, 500_000 };
        Assert.NotEqual(
            EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, ownershipA, 0),
            EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, ownershipB, 0));
        Assert.Equal(reference, EdgeTariffReferenceLaboratory.Evaluate(natural, improved, 4));
        Assert.Equal(improved, EdgeInvestmentLaboratory.Evaluate(natural, S, S));
    }

    [Fact]
    public void WeightedRouteSumIsOrderIndependentAndDoesNotCreateRouteEntity()
    {
        var reference = Reference(S);
        long[] edgeOne = { 250_000, 750_000 };
        long[] edgeTwo = { 500_000, 500_000 };

        long w1 = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, edgeOne, 0);
        long w2 = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, edgeTwo, 0);
        long forward = EdgeTariffOwnershipLaboratory.SumExternalWeights(new[] { w1, w2 });
        long reversed = EdgeTariffOwnershipLaboratory.SumExternalWeights(new[] { w2, w1 });

        Assert.Equal(forward, reversed);
        Assert.Equal(1_058_557L, forward);
        Assert.Equal(0, EdgeTariffOwnershipLaboratory.SumExternalWeights(Array.Empty<long>()));
    }

    [Fact]
    public void IndicativeRevenueFractionsConserveOneHundredPercentWithoutMoney()
    {
        var reference = Reference(S);
        long external = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            reference, new long[] { S }, sellerSlot: -1);
        var parts = EdgeTariffOwnershipLaboratory.IndicativeRevenueFractions(external);
        Assert.Equal(S, parts.SellerRetainedRaw + parts.ExternalOwnersCombinedRaw);
        Assert.True(parts.SellerRetainedRaw > 0);
        Assert.InRange(parts.ExternalOwnersCombinedRaw, 450_000, 460_000);

        var owner = EdgeTariffOwnershipLaboratory.IndicativeRevenueFractions(0);
        Assert.Equal(S, owner.SellerRetainedRaw);
        Assert.Equal(0, owner.ExternalOwnersCombinedRaw);
    }

    [Fact]
    public void DifferentProductPricesCannotChangeDimensionlessTariffWeight()
    {
        // This API takes no product identity, price or current sales
        // quantity. F-060 nominal settlement belongs to another gate.
        var reference = Reference(S);
        long[] holdings = { 250_000, 750_000 };
        var forSeller = EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, holdings, 0);
        var costA = EdgeCongestionLaboratory.Evaluate(
            EdgeInvestmentLaboratory.Evaluate(NeutralLand(), S, S), 0, 4);
        var costB = EdgeCongestionLaboratory.Evaluate(
            EdgeInvestmentLaboratory.Evaluate(NeutralLand(), S, S), 2 * S, 4);
        Assert.NotEqual(costA.EffectiveImpedanceRaw, costB.EffectiveImpedanceRaw);
        Assert.Equal(forSeller, EdgeTariffOwnershipLaboratory.ExternalWeightRaw(
            reference, holdings, 0));
    }

    [Fact]
    public void TariffReferenceIsHomogeneousUnderEquivalentMathematicalSplit()
    {
        // Analytic equivalence only, NOT a requirement to split Goldberg
        // LOGICAL edges, which would create a different world under F-081.
        var natural = NeutralLand();
        var full = EdgeInvestmentLaboratory.Evaluate(natural, S, S);
        var fullReference = EdgeTariffReferenceLaboratory.Evaluate(natural, full, 4);

        var halfNatural = new NaturalEdgeEvaluation(
            EdgeMedium.Land, natural.ImpedanceRaw / 2, natural.NaturalCapacityRateRaw);
        var halfImproved = new FreeFlowInvestedEdge(
            full.TotalInvestedCapitalRaw, full.ReferenceCapitalRaw,
            full.ImprovementFactorRaw, full.FreeFlowImpedanceRaw / 2,
            full.CapacityRateRaw);

        var half = EdgeTariffReferenceLaboratory.Evaluate(halfNatural, halfImproved, 4);
        Assert.Equal(fullReference.WeightRaw, 2 * half.WeightRaw);
        Assert.Equal(fullReference.PhysicalGainRaw, 2 * half.PhysicalGainRaw);
    }

    [Fact]
    public void GrowingInvestmentRaisesWeightWithoutArbitraryTariffCap()
    {
        long previous = -1;
        for (long investment = 0; investment <= 31 * S; investment += S)
        {
            var reference = Reference(investment);
            Assert.True(reference.WeightRaw >= previous);
            Assert.True(reference.WeightRaw >= 0);
            Assert.True(reference.WeightRaw <= reference.PhysicalGainRaw);
            Assert.True(2 * reference.WeightRaw <= reference.NaturalReferenceImpedanceRaw);
            previous = reference.WeightRaw;
        }
    }

    [Fact]
    public void InvalidHoldingsZeroCapitalAndDegradingInvestmentFailExplicitly()
    {
        var reference = Reference(S);
        var natural = NeutralLand();
        Assert.Throws<ArgumentException>(() =>
            EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, new long[] { 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, new long[] { -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, new long[] { S }, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeTariffOwnershipLaboratory.ExternalWeightRaw(reference, new long[] { S }, -2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeTariffOwnershipLaboratory.OwnerExternalComponentRaw(reference, new long[] { S }, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeTariffOwnershipLaboratory.IndicativeRevenueFractions(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeTariffOwnershipLaboratory.SumExternalWeights(new long[] { -1 }));
        Assert.Throws<OverflowException>(() =>
            EdgeTariffOwnershipLaboratory.SumExternalWeights(new long[] { long.MaxValue, 1 }));

        var zero = EdgeInvestmentLaboratory.Evaluate(natural, 0, S);
        Assert.Throws<ArgumentException>(() =>
            EdgeTariffReferenceLaboratory.Evaluate(natural,
                zero with { CapacityRateRaw = 2 * S }, 4));
        Assert.Throws<ArgumentException>(() =>
            EdgeTariffReferenceLaboratory.Evaluate(natural,
                EdgeInvestmentLaboratory.Evaluate(natural, S, S)
                    with { FreeFlowImpedanceRaw = natural.ImpedanceRaw + 1 }, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EdgeTariffReferenceLaboratory.Evaluate(natural,
                EdgeInvestmentLaboratory.Evaluate(natural, S, S), 2));
    }

    [Fact]
    public void FrozenCrossPlatformReferenceWeightChecksum()
    {
        long sum = 0;
        var natural = NeutralLand();
        for (int i = 0; i < 4_096; i++)
        {
            var improved = EdgeInvestmentLaboratory.Evaluate(
                natural, i * 13_781L, S);
            var value = EdgeTariffReferenceLaboratory.Evaluate(
                natural, improved, 3 + i % 3);
            sum = checked(sum + value.WeightRaw * 7 +
                value.PhysicalGainRaw * 3 + value.InvestedReferenceImpedanceRaw);
        }
        Assert.Equal(49_949_188_417L, sum);
    }
}
