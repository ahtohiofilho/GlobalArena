using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs79AuthenticatedFreightEndogenousShadowTests
{
    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Build() =>
        GaVs67IndependentFreightComponentsTests.SharedBuyer();

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (CanonicalPrimitiveFreightCandidateUse Candidate, ulong Quantity)
        FirstShipment(CanonicalPrimitiveSiteDiscovery sites,
            CanonicalPrimitiveFreightPortfolio portfolio)
    {
        var candidate = portfolio.CandidateUses.First(x =>
            sites.At(x.Source)!.ResidentWorkers > 0 &&
            sites.At(x.Buyer)!.ResidentWorkers > 0 &&
            x.Source != x.Buyer &&
            sites.Flows.SupplyFlows.Any(s => s.EconomicPointId == x.Source &&
                s.CommodityId == x.Commodity &&
                s.Activity == x.Activity && s.RawQuantity > 0) &&
            sites.Flows.DemandFlows.Any(d => d.EconomicPointId == x.Source &&
                d.CommodityId == x.Commodity && d.RawQuantity > 0) &&
            sites.Flows.DemandFlows.Any(d => d.EconomicPointId == x.Buyer &&
                d.CommodityId == x.Commodity && d.RawQuantity > 0));
        var supply = sites.Flows.SupplyFlows.Single(x =>
            x.EconomicPointId == candidate.Source &&
            x.CommodityId == candidate.Commodity &&
            x.Activity == candidate.Activity);
        return (candidate, Math.Min(100_000UL,
            Math.Min(supply.RawQuantity, candidate.MaximumRawQuantity)));
    }

    private static CanonicalEndogenousFreightPriceComparison Compare(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture fixture,
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveFreightCandidateUse candidate, ulong raw) =>
        CanonicalEndogenousFreightPriceComparison.FromPortfolio(
            portfolio, fixture.Session, candidate, raw, Congestion());

    [Fact]
    public void OneRealRemoteServiceProducesPriceIndicesAndAuditedFreightSeparately()
    {
        var (fixture, sites, portfolio) = Build();
        var before = fixture.Session.ExportSaveJson();
        var (candidate, quantity) = FirstShipment(sites, portfolio);
        var observation = Compare(fixture, portfolio, candidate, quantity);
        foreach (var row in new[] { observation.Gentle, observation.Steep })
        {
            Assert.Equal(candidate.Source, row.Source);
            Assert.Equal(candidate.Buyer, row.Buyer);
            Assert.Equal(candidate.ServiceId, row.ServiceId);
            Assert.Equal(quantity, row.RawQuantity);
            Assert.True(row.SourceDemandPressure > 0);
            Assert.True(row.BuyerDemandPressure > 0);
            Assert.True(row.AuthenticatedNominalFreightCost.Numerator >= 0);
            Assert.True(row.BuyerGrossRevenueIndex.Numerator > 0);
        }
        Assert.Equal(0, observation.Gentle.AuthenticatedNominalFreightCost
            .CompareTo(observation.Steep.AuthenticatedNominalFreightCost));
        Assert.False(observation.UsesExogenousBuyerBudget);
        Assert.False(observation.LimitsConsumptionToPhysicalDemand);
        Assert.False(observation.CanCalculateMonetaryNetRevenue);
        Assert.False(observation.IsNominalFreightConvertedIntoPriceIndex);
        Assert.False(observation.CanAuthorizeRealSettlement);
        Assert.False(observation.IsDispatchOrSettlement);
        Assert.False(observation.HasCommittedPhysicalInventory);
        observation.VerifyIndependent();
        Assert.Equal(before, fixture.Session.ExportSaveJson());
    }

    [Fact]
    public void RemoteQuantityIsNotCappedByBasePhysicalDemand()
    {
        var (fixture, sites, portfolio) = Build();
        var (candidate, _) = FirstShipment(sites, portfolio);
        var actualPhysicalSupply = sites.Flows.SupplyFlows.Single(x =>
            x.EconomicPointId == candidate.Source &&
            x.CommodityId == candidate.Commodity &&
            x.Activity == candidate.Activity).RawQuantity;
        var currentDemand = sites.Flows.DemandFlows.Single(x =>
            x.EconomicPointId == candidate.Buyer &&
            x.CommodityId == candidate.Commodity).RawQuantity;
        var quantity = Math.Min(candidate.MaximumRawQuantity, actualPhysicalSupply);
        var result = Compare(fixture, portfolio, candidate, quantity);
        Assert.Equal(quantity, result.Gentle.RawQuantity);
        Assert.Equal(new BigInteger(currentDemand),
            result.Gentle.BuyerDemandPressure);
        var derived = CanonicalEndogenousLocalPriceComparison.Calculate(
            quantity, currentDemand, 1);
        Assert.Equal(0, result.Gentle.BuyerGrossRevenueIndex.CompareTo(
            derived.Revenue));
        result.VerifyIndependent();
    }

    [Fact]
    public void LargerOfferReducesMarginalPriceAndCostIsNotClaimedAsRevenue()
    {
        var (fixture, sites, portfolio) = Build();
        var (candidate, quantity) = FirstShipment(sites, portfolio);
        Assert.True(quantity > 1);
        var one = Compare(fixture, portfolio, candidate, 1);
        var many = Compare(fixture, portfolio, candidate, quantity);
        Assert.True(many.Gentle.BuyerLastMarginalPriceIndex.CompareTo(
            one.Gentle.BuyerLastMarginalPriceIndex) <= 0);
        Assert.True(many.Gentle.BuyerGrossRevenueIndex.CompareTo(
            one.Gentle.BuyerGrossRevenueIndex) > 0);
        Assert.False(many.CanCalculateMonetaryNetRevenue);
        one.VerifyIndependent();
        many.VerifyIndependent();
    }

    [Fact]
    public void ForgedAndOversizedShipmentsFailClosed()
    {
        var (fixture, sites, portfolio) = Build();
        var (candidate, quantity) = FirstShipment(sites, portfolio);
        Assert.Throws<ArgumentException>(() => Compare(fixture,
            portfolio, candidate with {
                MaximumRawQuantity = candidate.MaximumRawQuantity
            }, quantity));
        Assert.Throws<ArgumentException>(() =>
            Compare(fixture, portfolio, candidate, 0UL));
        Assert.Throws<ArgumentException>(() =>
            Compare(fixture, portfolio, candidate,
                checked(candidate.MaximumRawQuantity + 1UL)));
        Assert.Throws<ArgumentNullException>(() =>
            CanonicalEndogenousFreightPriceComparison.FromPortfolio(
                portfolio, fixture.Session, candidate, quantity, null!));
    }

    [Fact]
    public void SameInputReplaysDeterministicallyAndRestoreInvalidatesReceipt()
    {
        var (fixture, sites, portfolio) = Build();
        var (candidate, quantity) = FirstShipment(sites, portfolio);
        var a = Compare(fixture, portfolio, candidate, quantity);
        var b = Compare(fixture, portfolio, candidate, quantity);
        Assert.Equal(a.Gentle, b.Gentle);
        Assert.Equal(a.Steep, b.Steep);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            fixture.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() => a.RequireCurrent(restored));
        a.VerifyIndependent();
        b.VerifyIndependent();
    }

    [Fact]
    public void BuyerDemandChangesPriceNotVolumeAsAFunctionProperty()
    {
        var (fixture, sites, portfolio) = Build();
        var (candidate, quantity) = FirstShipment(sites, portfolio);
        var snapshot = Compare(fixture, portfolio, candidate, quantity);
        var low = CanonicalEndogenousLocalPriceComparison.Calculate(
            quantity, 1, 1);
        var high = CanonicalEndogenousLocalPriceComparison.Calculate(
            quantity, new BigInteger(quantity) * 10, 1);
        Assert.True(high.Average.CompareTo(low.Average) > 0);
        Assert.True(high.Revenue.CompareTo(low.Revenue) > 0);
        Assert.Equal(quantity, snapshot.Gentle.RawQuantity);
        snapshot.VerifyIndependent();
    }
}
