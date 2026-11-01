using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs78EndogenousLocalPriceComparisonTests
{
    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Build() =>
        GaVs67IndependentFreightComponentsTests.SharedBuyer();

    private static (EconomicSupplyFlow Source, EconomicDemandFlow Demand)
        ActiveProduct(CanonicalPrimitiveSiteDiscovery sites)
    {
        foreach (var source in sites.Flows.SupplyFlows.Where(x => x.RawQuantity >= 1000))
        {
            var demand = sites.Flows.DemandFlows.FirstOrDefault(x =>
                x.EconomicPointId == source.EconomicPointId &&
                x.CommodityId == source.CommodityId);
            if (demand is not null && demand.RawQuantity > 0)
                return (source, demand);
        }
        throw new InvalidOperationException(
            "Fixture has no suitable inhabited source/commodity demand.");
    }

    private static CanonicalEndogenousProductionDeclaration Declare(
        EconomicSupplyFlow source, ulong produced, ulong retained = 0) =>
        new(source.EconomicPointId, source.CommodityId, source.Activity,
            produced, retained);

    private static CanonicalEndogenousLocalPriceComparison Preview(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture fixture,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalEndogenousProductionDeclaration[] productions,
        CanonicalEndogenousDemandSignalAddition[]? additions = null) =>
        CanonicalEndogenousLocalPriceComparison.FromSites(
            fixture.Session, sites, productions, additions);

    [Fact]
    public void HigherPhysicalOfferLowersMarginalPriceWithoutDemandOrCashCutoff()
    {
        var (fixture, sites, _) = Build();
        var (source, demand) = ActiveProduct(sites);
        var lower = Preview(fixture, sites, [Declare(source, 100)]);
        var higher = Preview(fixture, sites, [Declare(source, 1000)]);
        Assert.Equal(100UL, Assert.Single(lower.Sources).OfferedAndLocallyAbsorbedRaw);
        Assert.Equal(1000UL, Assert.Single(higher.Sources).OfferedAndLocallyAbsorbedRaw);
        foreach (var (a, b) in new[] { (lower.Gentle, higher.Gentle),
                 (lower.Steep, higher.Steep) })
        {
            var small = Assert.Single(a.Commodities);
            var large = Assert.Single(b.Commodities);
            Assert.Equal(new BigInteger(1000), large.EffectiveOfferRaw);
            Assert.Equal(new BigInteger(demand.RawQuantity),
                large.BaseDemandPressureRaw);
            Assert.True(large.LastMarginalPriceIndex.CompareTo(
                small.LastMarginalPriceIndex) < 0);
            Assert.True(large.AveragePriceIndex.CompareTo(
                small.AveragePriceIndex) < 0);
            Assert.True(large.ResultingGrossRevenueIndex.CompareTo(
                small.ResultingGrossRevenueIndex) > 0);
        }
        higher.VerifyIndependent();
        Assert.False(higher.UsesAnExogenousMonetaryBudget);
        Assert.False(higher.AppliesPhysicalDemandAsConsumptionCap);
        Assert.False(higher.CanAuthorizeRealSettlement);
        Assert.False(higher.IsActualConsumption);
    }

    [Fact]
    public void DemandPressureRaisesPricesButDoesNotSpecifyMoneyOrStockVolume()
    {
        var (fixture, sites, _) = Build();
        var (source, demand) = ActiveProduct(sites);
        var declaration = Declare(source, 1000);
        var baseline = Preview(fixture, sites, [declaration]);
        var extra = Preview(fixture, sites, [declaration],
            [new(source.EconomicPointId, source.CommodityId, demand.RawQuantity)]);
        foreach (var (a, b) in new[] { (baseline.Gentle, extra.Gentle),
                 (baseline.Steep, extra.Steep) })
        {
            var old = Assert.Single(a.Commodities);
            var updated = Assert.Single(b.Commodities);
            Assert.Equal(old.EffectiveOfferRaw, updated.EffectiveOfferRaw);
            Assert.True(updated.AveragePriceIndex.CompareTo(
                old.AveragePriceIndex) > 0);
            Assert.True(updated.ResultingGrossRevenueIndex.CompareTo(
                old.ResultingGrossRevenueIndex) > 0);
        }
        Assert.False(extra.CreatesActualInventory);
        Assert.Single(extra.DemandSignalAdditions);
        extra.VerifyIndependent();
    }

    [Fact]
    public void DirectRetentionDeductsExactlyChosenPhysicalAmountWithoutSale()
    {
        var (fixture, sites, _) = Build();
        var (source, _) = ActiveProduct(sites);
        var result = Preview(fixture, sites,
            [Declare(source, produced: 1000, retained: 375)]);
        var row = Assert.Single(result.Sources);
        Assert.Equal(1000UL, row.HypotheticallyProducedRaw);
        Assert.Equal(375UL, row.ExplicitRetentionRaw);
        Assert.Equal(625UL, row.OfferedAndLocallyAbsorbedRaw);
        Assert.Equal(new BigInteger(625),
            Assert.Single(result.Gentle.Commodities).EffectiveOfferRaw);
        Assert.False(result.CreatesActualInventory);
        Assert.False(result.IsActualConsumption);
        result.VerifyIndependent();
    }

    [Fact]
    public void CommodityDemandIsNotClonedForTwoActivitiesSharingOneGood()
    {
        var (fixture, sites, _) = Build();
        var (source, _) = ActiveProduct(sites);
        var other = sites.Flows.SupplyFlows.FirstOrDefault(x =>
            x.EconomicPointId == source.EconomicPointId &&
            x.CommodityId == source.CommodityId &&
            x.Activity != source.Activity &&
            x.RawQuantity > 0);
        if (other is null) return; // No second producing activity in this fixture.
        var result = Preview(fixture, sites,
            [Declare(source, 1), Declare(other, 1)]);
        Assert.Equal(new BigInteger(2),
            Assert.Single(result.Gentle.Commodities).EffectiveOfferRaw);
        result.VerifyIndependent();
    }

    [Fact]
    public void InvalidSourcesRetentionAndDemandSignalsFailClosed()
    {
        var (fixture, sites, _) = Build();
        var (source, demand) = ActiveProduct(sites);
        Assert.Throws<ArgumentException>(() => Preview(fixture, sites,
            [Declare(source, source.RawQuantity + 1UL)]));
        Assert.Throws<ArgumentException>(() => Preview(fixture, sites,
            [Declare(source, 100, retained: 101)]));
        Assert.Throws<ArgumentException>(() => Preview(fixture, sites,
            [Declare(source, 100), Declare(source, 100)]));
        Assert.Throws<ArgumentException>(() => Preview(fixture, sites,
            [Declare(source, 100)],
            [new(source.EconomicPointId, source.CommodityId, 1UL),
             new(source.EconomicPointId, source.CommodityId, 2UL)]));
        Assert.Throws<ArgumentException>(() => Preview(fixture, sites,
            [Declare(source, 100)],
            [new(source.EconomicPointId, new CommodityId(999999), 1UL)]));
        Assert.True(demand.RawQuantity > 0);
    }

    [Fact]
    public void ReorderingPhysicalSourcesDoesNotChangeCommodityRevenueOrSnapshot()
    {
        var (fixture, sites, _) = Build();
        var sources = sites.Flows.SupplyFlows
            .Where(x => x.RawQuantity >= 1000 &&
                sites.Flows.DemandFlows.Any(d =>
                    d.EconomicPointId == x.EconomicPointId &&
                    d.CommodityId == x.CommodityId))
            .Take(2).ToArray();
        Assert.Equal(2, sources.Length);
        var declared = sources.Select(x => Declare(x, 1000)).ToArray();
        var forward = Preview(fixture, sites, declared);
        var backwards = Preview(fixture, sites, declared.Reverse().ToArray());
        Assert.Equal(forward.Sources, backwards.Sources);
        Assert.Equal(forward.Gentle.Commodities, backwards.Gentle.Commodities);
        Assert.Equal(0, forward.Gentle.AggregateGrossRevenueIndex.CompareTo(
            backwards.Gentle.AggregateGrossRevenueIndex));
        forward.VerifyIndependent();
        backwards.VerifyIndependent();
    }

    [Fact]
    public void SaveAndWorldHashRemainUnchangedAndRestoredSessionRejected()
    {
        var (fixture, sites, _) = Build();
        var (source, _) = ActiveProduct(sites);
        var save = fixture.Session.ExportSaveJson();
        var preview = Preview(fixture, sites, [Declare(source, 100)]);
        Assert.Equal(save, fixture.Session.ExportSaveJson());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Throws<InvalidOperationException>(() =>
            preview.RequireCurrent(restored));
        preview.VerifyIndependent();
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void NoDemandSignalStillAllowsLocalAbsorptionAtZeroPriceIndex()
    {
        var (fixture, sites, _) = Build();
        var (source, _) = ActiveProduct(sites);
        // A separate exact mathematical boundary check does not invent a buyer.
        var zero = CanonicalEndogenousLocalPriceComparison.Calculate(
            offered: 1000000, demandPressure: 0, sensitivity: 1);
        Assert.Equal(0, zero.Revenue.Numerator);
        Assert.Equal(0, zero.Last.Numerator);
        var result = Preview(fixture, sites, [Declare(source, 100)]);
        Assert.Equal(100UL, Assert.Single(result.Sources).OfferedAndLocallyAbsorbedRaw);
    }

    [Fact]
    public void RevenuesGrowBeyondAnyFixedSpendEnvelopeWhenPhysicalOfferGrows()
    {
        var p = new BigInteger(2);
        var a = CanonicalEndogenousLocalPriceComparison.Calculate(
            offered: 10, demandPressure: p, sensitivity: 1);
        var b = CanonicalEndogenousLocalPriceComparison.Calculate(
            offered: 1000, demandPressure: p, sensitivity: 1);
        var c = CanonicalEndogenousLocalPriceComparison.Calculate(
            offered: 1000000, demandPressure: p, sensitivity: 1);
        Assert.True(b.Revenue.CompareTo(a.Revenue) > 0);
        Assert.True(c.Revenue.CompareTo(b.Revenue) > 0);
        Assert.True(c.Last.CompareTo(b.Last) < 0);
        Assert.True(c.Revenue.CompareTo(
            new CanonicalEndogenousPriceIndex(p, 1)) > 0);
    }
}
