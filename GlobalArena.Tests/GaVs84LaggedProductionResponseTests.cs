using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs84LaggedProductionResponseTests
{
    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalSimultaneousFreightReference Freight) Build()
    {
        var (fixture, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var freight = CanonicalSimultaneousFreightReference.FromSites(
            fixture.Session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        return (fixture, sites, freight);
    }

    private static CanonicalWorkforceActivityRealizationPolicy[] Rates(
        CanonicalPrimitiveSiteDiscovery sites, int utilization, int retention = 0) =>
        sites.Flows.SupplyFlows.Where(x => x.RawQuantity > 0)
            .Select(x => x.Activity).Distinct()
            .Select(x => new CanonicalWorkforceActivityRealizationPolicy(
                x, utilization, retention)).ToArray();

    private static CanonicalWorkforceRealizationProjection Previous(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture fixture,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freight,
        int utilization = 500, int retention = 0) =>
        CanonicalWorkforceRealizationProjection.FromSites(
            fixture.Session, sites, freight,
            Rates(sites, utilization, retention),
            Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion());

    private static CanonicalLaggedProductionResponse Respond(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture fixture,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freight,
        CanonicalWorkforceRealizationProjection previous,
        int step = 100,
        params CanonicalSimultaneousFreightLot[] nextLots) =>
        CanonicalLaggedProductionResponse.FromFrozenProjection(
            fixture.Session, sites, freight, previous,
            step, nextLots, Congestion());

    [Fact]
    public void ExactPureCalibrationIsBoundedMonotonicAndNeutralAtOneHalf()
    {
        var zero = new CanonicalEndogenousPriceIndex(0, 1);
        var low = new CanonicalEndogenousPriceIndex(1, 4);
        var neutral = new CanonicalEndogenousPriceIndex(1, 2);
        var high = new CanonicalEndogenousPriceIndex(3, 4);
        var max = new CanonicalEndogenousPriceIndex(1, 1);
        Assert.Equal(-100, CanonicalLaggedProductionResponse.CalculateBoundedResponse(
            zero, 100));
        Assert.Equal(-50, CanonicalLaggedProductionResponse.CalculateBoundedResponse(
            low, 100));
        Assert.Equal(0, CanonicalLaggedProductionResponse.CalculateBoundedResponse(
            neutral, 100));
        Assert.Equal(50, CanonicalLaggedProductionResponse.CalculateBoundedResponse(
            high, 100));
        Assert.Equal(100, CanonicalLaggedProductionResponse.CalculateBoundedResponse(
            max, 100));
        Assert.Equal(0, CanonicalLaggedProductionResponse.CalculateBoundedResponse(
            new(11, 22), 250));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                new(-1, 1), 100));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                new(2, 1), 100));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                new(1, 0), 100));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                neutral, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                neutral, 251));
    }

    [Fact]
    public void FrozenRealLocalMarketDrivesOneBoundedActivityResponseWithoutCircularPrices()
    {
        var (fixture, sites, freight) = Build();
        var save = fixture.Session.ExportSaveJson();
        var previous = Previous(fixture, sites, freight, 500, 100);
        var result = Respond(fixture, sites, freight, previous);
        Assert.NotEmpty(result.Signals);
        Assert.Equal(previous.Policies.Count, result.Signals.Count);
        Assert.All(result.Signals, signal => {
            Assert.True(signal.PreviouslyLocallyAbsorbedRaw > 0);
            Assert.True(signal.FrozenWeightedLocalPriceIndex.Denominator > 0);
            Assert.InRange(signal.BoundedUtilizationDeltaPermille, -100, 100);
            Assert.InRange(signal.NextUtilizationPermille, 0, 1000);
            Assert.Equal(500, signal.PriorUtilizationPermille);
            Assert.Equal(100, signal.UnchangedRetentionPermille);
            Assert.Equal(500 + signal.BoundedUtilizationDeltaPermille,
                signal.NextUtilizationPermille);
            Assert.Equal(CanonicalLaggedProductionResponse.CalculateBoundedResponse(
                signal.FrozenWeightedLocalPriceIndex, 100),
                signal.BoundedUtilizationDeltaPermille);
            Assert.Equal(signal.NextUtilizationPermille,
                result.NextProjection.Policies.Single(x =>
                    x.Activity == signal.Activity).UtilizationPermille);
        });
        Assert.True(result.UsesFrozenPriorLocalPrices);
        Assert.False(result.IsRealHistoricalCycleAdvance);
        Assert.False(result.UsesFuturePricesToDecideCurrentProduction);
        Assert.False(result.ComputesNominalNetProfit);
        Assert.False(result.InventsRemoteBuyersOrBuyerBudget);
        Assert.False(result.CapsProductionByResidentDemand);
        Assert.False(result.CreatesInvoluntaryStockOrCredit);
        Assert.False(result.ChangesRealWorkerAssignments);
        Assert.False(result.CanAuthorizeRealSettlement);
        result.VerifyIndependent();
        Assert.Equal(save, fixture.Session.ExportSaveJson());
    }

    [Fact]
    public void OldNoOutputOrAllRetainedCannotSupplyFictionalLaggedQuote()
    {
        var (fixture, sites, freight) = Build();
        var zero = Previous(fixture, sites, freight, 0);
        Assert.Null(zero.Allocation);
        Assert.Throws<InvalidOperationException>(() =>
            Respond(fixture, sites, freight, zero));
        var allRetained = Previous(fixture, sites, freight, 500, 1000);
        Assert.NotNull(allRetained.Allocation);
        Assert.All(allRetained.Allocation!.LocalMarkets, x =>
            Assert.Equal(BigInteger.Zero, x.TotalLocallyAbsorbedRaw));
        Assert.Throws<InvalidOperationException>(() =>
            Respond(fixture, sites, freight, allRetained));
    }

    [Fact]
    public void UpperAndLowerUtilizationBoundsRemainPhysicalUnderExtremePricePressure()
    {
        var (fixture, sites, freight) = Build();
        var almostFull = Previous(fixture, sites, freight, 1000);
        var high = Respond(fixture, sites, freight, almostFull, 250);
        Assert.All(high.Signals, x => {
            Assert.Equal(1000, x.NextUtilizationPermille);
            Assert.True(x.BoundedUtilizationDeltaPermille >= 0);
        });
        Assert.All(high.NextProjection.Sources, x =>
            Assert.Equal(x.PotentialRaw, x.DerivedProducedRaw));
        high.VerifyIndependent();
    }

    [Fact]
    public void NextRemoteShipmentExceedingDerivedProductionFailsEvenIfPotentialWouldAllowIt()
    {
        var (fixture, sites, freight) = Build();
        var previous = Previous(fixture, sites, freight, 500);
        var first = Respond(fixture, sites, freight, previous);
        var offer = freight.Offers.Single(x => x.Commodity == new CommodityId(7));
        var derived = first.NextProjection.Sources.Single(x =>
            x.Source == offer.Source && x.Commodity == offer.Commodity &&
            x.Activity == offer.Activity).DerivedProducedRaw;
        Assert.True(offer.SourcePotentialRaw > derived);
        Assert.Throws<ArgumentException>(() =>
            Respond(fixture, sites, freight, previous, 100,
                new CanonicalSimultaneousFreightLot(offer, derived + 1UL)));
        var accepted = Respond(fixture, sites, freight, previous, 100,
            new CanonicalSimultaneousFreightLot(offer, derived));
        Assert.Equal(new BigInteger(derived), Assert.Single(
            accepted.NextProjection.Allocation!.RemoteMarkets)
            .TotalHypotheticalDeliveredRaw);
        accepted.VerifyIndependent();
    }

    [Fact]
    public void PermutationsAndCrossSessionRestorationCannotAlterOrReuseFrozenProof()
    {
        var (fixture, sites, freight) = Build();
        var save = fixture.Session.ExportSaveJson();
        var previous = Previous(fixture, sites, freight, 500, 50);
        var food = freight.Offers.Single(x => x.Commodity == new CommodityId(7));
        var wood = freight.Offers.Single(x => x.Commodity == new CommodityId(8));
        var q1 = Math.Min(100UL, food.SourcePotentialRaw / 4UL);
        var q2 = Math.Min(100UL, wood.SourcePotentialRaw / 4UL);
        var a = Respond(fixture, sites, freight, previous, 100,
            new CanonicalSimultaneousFreightLot(food, q1),
            new CanonicalSimultaneousFreightLot(wood, q2));
        var b = Respond(fixture, sites, freight, previous, 100,
            new CanonicalSimultaneousFreightLot(wood, q2),
            new CanonicalSimultaneousFreightLot(food, q1));
        Assert.Equal(a.Signals, b.Signals);
        Assert.Equal(a.NextProjection.Sources, b.NextProjection.Sources);
        Assert.Equal(a.NextProjection.Allocation!.Sources,
            b.NextProjection.Allocation!.Sources);
        Assert.Equal(0, a.NextProjection.Allocation.AuthenticatedNominalFreightCost.CompareTo(
            b.NextProjection.Allocation.AuthenticatedNominalFreightCost));
        Assert.Equal(save, fixture.Session.ExportSaveJson());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() => a.RequireCurrent(restored));
        a.VerifyIndependent();
        b.VerifyIndependent();
    }

    [Fact]
    public void ForgedServiceAndAnotherSessionPreviousProjectionFailClosed()
    {
        var (fixture, sites, freight) = Build();
        var previous = Previous(fixture, sites, freight, 500);
        var real = freight.Offers.Single(x => x.Commodity == new CommodityId(7));
        Assert.Throws<ArgumentException>(() =>
            Respond(fixture, sites, freight, previous, 100,
                new CanonicalSimultaneousFreightLot(real with { }, 1UL)));
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            fixture.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalLaggedProductionResponse.FromFrozenProjection(
                restored, sites, freight, previous, 100,
                Array.Empty<CanonicalSimultaneousFreightLot>(), Congestion()));
    }
}
