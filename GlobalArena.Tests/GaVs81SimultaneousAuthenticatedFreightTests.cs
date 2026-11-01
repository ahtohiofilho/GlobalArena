using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs81SimultaneousAuthenticatedFreightTests
{
    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Shared() =>
        GaVs67IndependentFreightComponentsTests.SharedBuyer();

    private static CanonicalSimultaneousFreightReference Reference(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture fixture,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveFreightPortfolio portfolio,
        bool foodOnly = false) =>
        CanonicalSimultaneousFreightReference.FromSites(
            fixture.Session, sites,
            portfolio.Slices.Where(x =>
                !foodOnly || x.Policy.Commodity == new CommodityId(7))
                .Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);

    [Fact]
    public void TwoRealCommoditiesShareARealCongestedCorridorWithoutDuplicateDemandWallet()
    {
        var (f, sites, portfolio) = Shared();
        var before = f.Session.ExportSaveJson();
        var proof = Reference(f, sites, portfolio);
        var food = proof.Offers.Single(x =>
            x.Commodity == new CommodityId(7));
        var wood = proof.Offers.Single(x =>
            x.Commodity == new CommodityId(8));
        Assert.Equal(food.ServiceId, wood.ServiceId);
        var q = Math.Min(10_000UL, Math.Min(
            food.SourcePotentialRaw, wood.SourcePotentialRaw));
        Assert.True(q > 0UL);
        var lots = new[] {
            new CanonicalSimultaneousFreightLot(food, q),
            new CanonicalSimultaneousFreightLot(wood, q)
        };
        var combined = proof.Audit(lots, Congestion());
        var aloneFood = proof.Audit([lots[0]], Congestion());
        var aloneWood = proof.Audit([lots[1]], Congestion());
        Assert.Equal(2, combined.Sources.Count);
        Assert.Equal(2, combined.Markets.Count);
        Assert.Single(combined.CorridorLoads);
        Assert.Equal(new BigInteger(q * 2), combined.CorridorLoads.Single().Value);
        var standalone = ExperimentalSharedFreightBatchCost.Add(
            aloneFood.TotalAuthenticatedNominalFreightCost,
            aloneWood.TotalAuthenticatedNominalFreightCost);
        Assert.True(combined.TotalAuthenticatedNominalFreightCost.CompareTo(
            standalone) > 0);
        Assert.False(combined.UsesExogenousMonetaryBudget);
        Assert.False(combined.CapsShipmentsByConsumerDemand);
        Assert.False(combined.ProvesHardThroughput);
        Assert.False(combined.ComputesNetMonetaryRevenue);
        Assert.False(combined.ProducesActualDeliveryOrInventory);
        Assert.False(combined.CanAuthorizeRealSettlement);
        combined.VerifyIndependent();
        Assert.Equal(before, f.Session.ExportSaveJson());
    }

    [Fact]
    public void TwoAuthenticatedServicesCannotDoubleSpendSameSourcePhysicalPool()
    {
        var (f, sites, portfolio, _, _) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var proof = Reference(f, sites, portfolio, foodOnly: true);
        var sameSource = proof.Offers.Where(x =>
            x.Commodity == new CommodityId(7))
            .GroupBy(x => (x.Source, x.Commodity, x.Activity))
            .First(x => x.Select(o => o.ServiceId).Distinct().Count() >= 2)
            .OrderBy(x => x.ServiceId).Take(2).ToArray();
        var supply = sameSource[0].SourcePotentialRaw;
        Assert.Equal(supply, sameSource[1].SourcePotentialRaw);
        Assert.True(supply > 1);
        Assert.Throws<ArgumentException>(() => proof.Audit(
            [new(sameSource[0], supply), new(sameSource[1], 1UL)],
            Congestion()));
        var half = supply / 2UL;
        var accepted = proof.Audit(
            [new(sameSource[0], half), new(sameSource[1], supply - half)],
            Congestion());
        var balance = Assert.Single(accepted.Sources);
        Assert.Equal(new BigInteger(supply), balance.TotalDeclaredShipmentsRaw);
        Assert.Equal(BigInteger.Zero, balance.RemainingUncommittedPotentialRaw);
        Assert.Equal(2, accepted.Markets.Count);
        accepted.VerifyIndependent();
    }

    [Fact]
    public void ExactSourceRetentionReducesOnlyTheDeclaredProducerPool()
    {
        var (f, sites, portfolio) = Shared();
        var proof = Reference(f, sites, portfolio);
        var food = proof.Offers.Single(x => x.Commodity == new CommodityId(7));
        var wood = proof.Offers.Single(x => x.Commodity == new CommodityId(8));
        var supply = food.SourcePotentialRaw;
        var keep = supply - 1;
        Assert.True(keep > 0);
        var withheld = new CanonicalSimultaneousFreightSourceRetention(
            food.Source, food.Commodity, food.Activity, keep);
        Assert.Throws<ArgumentException>(() =>
            proof.Audit([new(food, 2UL)], Congestion(), [withheld]));
        var accepted = proof.Audit([new(food, 1UL), new(wood, 1UL)],
            Congestion(), [withheld]);
        var source = accepted.Sources.Single(x => x.Commodity == food.Commodity);
        Assert.Equal(keep, source.DeliberateRetentionRaw);
        Assert.Equal(new BigInteger(1), source.TotalDeclaredShipmentsRaw);
        Assert.Equal(BigInteger.Zero, source.RemainingUncommittedPotentialRaw);
        var woodBalance = accepted.Sources.Single(x => x.Commodity == wood.Commodity);
        Assert.Equal(0UL, woodBalance.DeliberateRetentionRaw);
        Assert.Equal(wood.SourcePotentialRaw - 1UL,
            (ulong)woodBalance.RemainingUncommittedPotentialRaw);
        Assert.False(accepted.ProducesActualDeliveryOrInventory);
        accepted.VerifyIndependent();
    }

    [Fact]
    public void BuyerPressureIsAPriceSignalNotPhysicalShipmentLimit()
    {
        var (f, _, old) = Shared();
        var food = new CommodityId(7);
        var wood = new CommodityId(8);
        var stone = new CommodityId(9);
        var adjusted = new CanonicalPrimitiveSiteDiscoveryPolicy(
            new EconomicPrimitiveCommodityMapping(food, wood, stone),
            [new(food, 1L), new(wood, 125_000L), new(stone, 250_000L)],
            new FoodTransportCostFraction(1, 1));
        var sites = f.Session.DiscoverPrimitiveEconomicSites(adjusted);
        var proof = Reference(f, sites, old, foodOnly: true);
        var candidate = Assert.Single(proof.Offers);
        var q = candidate.SourcePotentialRaw;
        Assert.True(q > candidate.BuyerPriceDemandSignalRaw);
        var result = proof.Audit([new(candidate, q)], Congestion());
        var price = Assert.Single(result.Markets);
        Assert.Equal(new BigInteger(q), price.TotalHypotheticalDeliveredRaw);
        Assert.Equal(new BigInteger(candidate.BuyerPriceDemandSignalRaw),
            price.BuyerDemandPressureRaw);
        Assert.True(price.GentleGrossRevenueIndex.Numerator > 0);
        result.VerifyIndependent();
    }

    [Fact]
    public void ForgedDuplicateMissingAndOversizedOffersFailClosed()
    {
        var (f, sites, portfolio) = Shared();
        var proof = Reference(f, sites, portfolio);
        var candidate = proof.Offers.Single(x => x.Commodity == new CommodityId(7));
        Assert.Throws<ArgumentException>(() =>
            proof.Audit([new(candidate with { }, 1)], Congestion()));
        Assert.Throws<ArgumentException>(() =>
            proof.Audit([new(candidate, 1), new(candidate, 1)], Congestion()));
        Assert.Throws<ArgumentException>(() =>
            proof.Audit([new(candidate, 0)], Congestion()));
        Assert.Throws<ArgumentException>(() =>
            proof.Audit([new(candidate, checked(candidate.SourcePotentialRaw + 1))],
                Congestion()));
        Assert.Throws<ArgumentException>(() => proof.Audit([new(candidate, 1)],
            Congestion(),
            [new(candidate.Source, candidate.Commodity, candidate.Activity, 1),
             new(candidate.Source, candidate.Commodity, candidate.Activity, 1)]));
        Assert.Throws<ArgumentException>(() => proof.Audit([new(candidate, 1)],
            Congestion(),
            [new(new EconomicPointId(999999UL), candidate.Commodity,
                candidate.Activity, 1)]));
        Assert.Throws<ArgumentNullException>(() =>
            proof.Audit([new(candidate, 1)], null!));
    }

    [Fact]
    public void PermutationHasExactSamePhysicalBalancesMarketValuesAndFreightLoads()
    {
        var (f, sites, portfolio) = Shared();
        var proof = Reference(f, sites, portfolio);
        var food = proof.Offers.Single(x => x.Commodity == new CommodityId(7));
        var wood = proof.Offers.Single(x => x.Commodity == new CommodityId(8));
        var a = proof.Audit([new(wood, 123), new(food, 456)], Congestion());
        var b = proof.Audit([new(food, 456), new(wood, 123)], Congestion());
        Assert.Equal(a.Sources, b.Sources);
        Assert.Equal(a.Lots, b.Lots);
        Assert.Equal(a.Markets, b.Markets);
        Assert.Equal(0, a.TotalAuthenticatedNominalFreightCost.CompareTo(
            b.TotalAuthenticatedNominalFreightCost));
        Assert.Equal(a.CorridorLoads.Count, b.CorridorLoads.Count);
        Assert.All(a.CorridorLoads, x => {
            Assert.True(b.CorridorLoads.TryGetValue(x.Key, out var other));
            Assert.Equal(x.Value, other);
        });
        a.VerifyIndependent();
        b.VerifyIndependent();
    }

    [Fact]
    public void ARestoredSessionCannotReuseAnyAuthenticationOrReadOnlyAudit()
    {
        var (f, sites, portfolio) = Shared();
        var original = f.Session.ExportSaveJson();
        var proof = Reference(f, sites, portfolio);
        var candidate = proof.Offers.Single(x => x.Commodity == new CommodityId(7));
        var audit = proof.Audit([new(candidate, 100UL)], Congestion());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(original);
        Assert.Throws<InvalidOperationException>(() => proof.RequireCurrent(restored));
        Assert.Throws<InvalidOperationException>(() => audit.RequireCurrent(restored));
        Assert.Equal(original, f.Session.ExportSaveJson());
        audit.VerifyIndependent();
    }
}
