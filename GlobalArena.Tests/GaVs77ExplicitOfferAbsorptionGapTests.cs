using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs77ExplicitOfferAbsorptionGapTests
{
    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Build() =>
        GaVs67IndependentFreightComponentsTests.SharedBuyer();

    private static CanonicalPrimitiveIntegratedSelection Select(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        bool prohibitPurchases = false)
    {
        var freight = portfolio.CandidateUses.First(x =>
            sites.At(x.Source)!.Supply.Any(s =>
                s.CommodityId == x.Commodity && s.Activity == x.Activity &&
                s.RawQuantity > 0) &&
            sites.At(x.Source)!.Demand.Any(d =>
                d.CommodityId == x.Commodity && d.RawQuantity > 0));
        var site = sites.At(freight.Source)!;
        var physical = site.Supply.Single(x =>
            x.CommodityId == freight.Commodity && x.Activity == freight.Activity);
        var demand = site.Demand.Single(x => x.CommodityId == freight.Commodity);
        var quantum = Math.Min(100_000UL,
            Math.Min(freight.MaximumRawQuantity,
                Math.Min(physical.RawQuantity, demand.RawQuantity)));
        var local = new CanonicalPrimitiveLocalUseProposal(
            freight.Source, freight.Commodity, freight.Activity,
            Math.Min(physical.RawQuantity, demand.RawQuantity));
        var prices = sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(x.EconomicPointId,
                x.CommodityId, new FoodTransportCostFraction(
                    prohibitPurchases ? 1_000_000 : 1, 1)));
        return portfolio.SelectJointLocalFreight([local], [freight],
            prices, new ExperimentalAbstractPurchasingPolicy(
                "VS56-PURCHASING", 1,
                new FoodTransportCostFraction(prohibitPurchases ? 1 : 5000, 1),
                new FoodTransportCostFraction(5, 8)),
            new ExperimentalSharedFreightCongestionPolicy(1, 1_000_000),
            quantum);
    }

    private static CanonicalPrimitiveExplicitSourceOffer Offer(
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveIntegratedSelection selection,
        bool fullPotential)
    {
        var lot = Assert.Single(selection.Lots, x => x.Option.IsLocal);
        var source = sites.At(lot.Option.Source)!.Supply.Single(x =>
            x.CommodityId == lot.Option.Commodity &&
            x.Activity == lot.Option.Activity);
        var selectedFromSource = selection.Lots.Where(x =>
            x.Option.Source == lot.Option.Source &&
            x.Option.Commodity == lot.Option.Commodity &&
            x.Option.Activity == lot.Option.Activity)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        Assert.InRange(selectedFromSource, BigInteger.One,
            new BigInteger(source.RawQuantity));
        return new(lot.Option.Source, lot.Option.Commodity, lot.Option.Activity,
            fullPotential ? source.RawQuantity : checked((ulong)selectedFromSource));
    }

    [Fact]
    public void OfferedEqualsAllocatedDoesNotImplyActualSalesOrInventory()
    {
        var (f, sites, portfolio) = Build();
        var save = f.Session.ExportSaveJson();
        var selection = Select(portfolio, sites);
        var sourceKey = selection.Lots.First().Option;
        var offered = new CanonicalPrimitiveExplicitSourceOffer(
            sourceKey.Source, sourceKey.Commodity, sourceKey.Activity,
            checked((ulong)selection.Lots.Where(x =>
                x.Option.Source == sourceKey.Source &&
                x.Option.Commodity == sourceKey.Commodity &&
                x.Option.Activity == sourceKey.Activity)
                .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity)));
        var diagnostic = selection.DiagnoseExplicitOfferAbsorption([offered]);
        var row = Assert.Single(diagnostic.Snapshot.Sources, x =>
            x.Source == offered.Source && x.Commodity == offered.Commodity &&
            x.Activity == offered.Activity);
        Assert.Equal(row.HypotheticallyOfferedRaw, row.Vs76AllocatedRaw);
        Assert.Equal(0UL, row.OfferedButUnallocatedRaw);
        Assert.True(diagnostic.AllDeclaredOffersHypotheticallyAllocated);
        Assert.False(diagnostic.IsActualSale);
        Assert.False(diagnostic.IsActualInventory);
        Assert.False(diagnostic.IsMarginalPriceCurveCalibrated);
        Assert.False(diagnostic.CanAuthorizeRealSettlement);
        Assert.False(diagnostic.IsCommitted);
        diagnostic.VerifyIndependent();
        Assert.Equal(save, f.Session.ExportSaveJson());
    }

    [Fact]
    public void FullPotentialOfferRevealsUndeliveredOutputWithoutFakeStock()
    {
        var (_, sites, portfolio) = Build();
        var selection = Select(portfolio, sites);
        var key = selection.Lots.First().Option;
        var potential = sites.At(key.Source)!.Supply.Single(x =>
            x.CommodityId == key.Commodity && x.Activity == key.Activity);
        var offer = new CanonicalPrimitiveExplicitSourceOffer(key.Source,
            key.Commodity, key.Activity, potential.RawQuantity);
        var audit = selection.DiagnoseExplicitOfferAbsorption([offer]);
        var row = Assert.Single(audit.Snapshot.Sources, x =>
            x.Source == key.Source && x.Commodity == key.Commodity &&
            x.Activity == key.Activity);
        Assert.Equal(row.PhysicalPotentialRaw, row.HypotheticallyOfferedRaw);
        Assert.Equal(0UL, row.PotentialNotOfferedRaw);
        Assert.Equal(row.HypotheticallyOfferedRaw - row.Vs76AllocatedRaw,
            row.OfferedButUnallocatedRaw);
        Assert.All(audit.Snapshot.Commodities, c =>
        {
            Assert.Equal(BigInteger.Max(BigInteger.Zero,
                c.OfferedButUnallocatedRaw - c.UnmetPhysicalDemandRaw),
                c.NecessaryAdditionalPhysicalDemandRaw);
        });
        audit.VerifyIndependent();
    }

    [Fact]
    public void ZeroLotAndNoOffersCannotManufacturePurchasers()
    {
        var (fixture, sites, portfolio) = Build();
        var selection = Select(portfolio, sites, prohibitPurchases: true);
        Assert.Empty(selection.Lots);
        var audit = selection.DiagnoseExplicitOfferAbsorption(
            Array.Empty<CanonicalPrimitiveExplicitSourceOffer>());
        Assert.True(audit.AllDeclaredOffersHypotheticallyAllocated);
        Assert.False(audit.HasNecessaryPhysicalDemandShortfall);
        Assert.All(audit.Snapshot.Sources, x =>
        {
            Assert.Equal(0UL, x.HypotheticallyOfferedRaw);
            Assert.Equal(0UL, x.Vs76AllocatedRaw);
            Assert.Equal(x.PhysicalPotentialRaw, x.PotentialNotOfferedRaw);
        });
        Assert.Equal(fixture.Session.CurrentHash, sites.WorldHash);
        audit.VerifyIndependent();
    }

    [Fact]
    public void OffersBelowSelectedOrAbovePotentialFailClosed()
    {
        var (_, sites, portfolio) = Build();
        var selected = Select(portfolio, sites);
        var option = selected.Lots.First().Option;
        var chosen = selected.Lots.Where(x =>
            x.Option.Source == option.Source &&
            x.Option.Commodity == option.Commodity &&
            x.Option.Activity == option.Activity)
            .Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity);
        var source = sites.At(option.Source)!.Supply.Single(x =>
            x.CommodityId == option.Commodity && x.Activity == option.Activity);
        Assert.True(chosen > 0);
        Assert.Throws<ArgumentException>(() =>
            selected.DiagnoseExplicitOfferAbsorption([
                new(option.Source, option.Commodity, option.Activity,
                    checked((ulong)(chosen - 1)))]));
        Assert.Throws<ArgumentException>(() =>
            selected.DiagnoseExplicitOfferAbsorption([
                new(option.Source, option.Commodity, option.Activity,
                    checked(source.RawQuantity + 1UL))]));
        Assert.Throws<ArgumentException>(() =>
            selected.DiagnoseExplicitOfferAbsorption([
                new(option.Source, option.Commodity, option.Activity,
                    source.RawQuantity),
                new(option.Source, option.Commodity, option.Activity,
                    source.RawQuantity)]));
    }

    [Fact]
    public void ReorderedIndependentOffersProduceIdenticalReconciliation()
    {
        var (_, sites, portfolio) = Build();
        var selection = Select(portfolio, sites, prohibitPurchases: true);
        var sources = sites.Flows.SupplyFlows
            .Where(x => x.RawQuantity > 0)
            .Take(2).Select(x => new CanonicalPrimitiveExplicitSourceOffer(
                x.EconomicPointId, x.CommodityId, x.Activity, x.RawQuantity))
            .ToArray();
        Assert.Equal(2, sources.Length);
        var a = selection.DiagnoseExplicitOfferAbsorption(sources);
        var b = selection.DiagnoseExplicitOfferAbsorption(sources.Reverse());
        Assert.Equal(a.Snapshot.Sources, b.Snapshot.Sources);
        Assert.Equal(a.Snapshot.Buyers, b.Snapshot.Buyers);
        Assert.Equal(a.Snapshot.Commodities, b.Snapshot.Commodities);
        a.VerifyIndependent();
        b.VerifyIndependent();
    }

    [Fact]
    public void RestoredSessionInvalidatesOfferAuditProvenance()
    {
        var (fixture, sites, portfolio) = Build();
        var selection = Select(portfolio, sites, prohibitPurchases: true);
        var audit = selection.DiagnoseExplicitOfferAbsorption(
            Array.Empty<CanonicalPrimitiveExplicitSourceOffer>());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            fixture.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            audit.RequireCurrent(restored));
        audit.VerifyIndependent();
    }
}
