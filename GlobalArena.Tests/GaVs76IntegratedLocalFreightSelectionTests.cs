using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs76IntegratedLocalFreightSelectionTests
{
    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Build() =>
        GaVs67IndependentFreightComponentsTests.SharedBuyer();

    private static ExperimentalAbstractPurchasingPolicy Purchasing(
        long gross = 5000) => new("VS56-PURCHASING", 1,
        new FoodTransportCostFraction(gross, 1),
        new FoodTransportCostFraction(5, 8));

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites,
        long localPrice = 1, long externalPrice = 1,
        EconomicPointId? source = null)
    {
        return sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(x.EconomicPointId,
                x.CommodityId, new FoodTransportCostFraction(
                    source is not null && x.EconomicPointId == source.Value
                        ? localPrice : externalPrice, 1))).ToArray();
    }

    private static (CanonicalPrimitiveLocalUseProposal Local,
        CanonicalPrimitiveFreightCandidateUse Freight,
        ulong Quantum) Pair(
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalPrimitiveFreightPortfolio portfolio)
    {
        var freight = portfolio.CandidateUses.First(x =>
            sites.At(x.Source)!.Supply.Any(s =>
                s.CommodityId == x.Commodity && s.Activity == x.Activity &&
                s.RawQuantity > 0) &&
            sites.At(x.Source)!.Demand.Any(d =>
                d.CommodityId == x.Commodity && d.RawQuantity > 0));
        var src = sites.At(freight.Source)!;
        var supply = src.Supply.Single(x =>
            x.CommodityId == freight.Commodity && x.Activity == freight.Activity);
        var demand = src.Demand.Single(x => x.CommodityId == freight.Commodity);
        var cap = Math.Min(Math.Min(supply.RawQuantity, demand.RawQuantity),
            freight.MaximumRawQuantity);
        var quantum = Math.Min(100_000UL, cap);
        Assert.True(quantum > 0);
        return (new(freight.Source, freight.Commodity, freight.Activity,
            Math.Min(supply.RawQuantity, demand.RawQuantity)), freight, quantum);
    }

    private static CanonicalPrimitiveIntegratedSelection Select(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPrimitiveSiteDiscovery sites,
        IEnumerable<CanonicalPrimitiveLocalUseProposal> local,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> freight,
        ulong quantum, long localPrice = 1, long externalPrice = 1,
        long gross = 5000, int maximumQuanta = 1,
        int maximumConfigurations = 4096, EconomicPointId? source = null) =>
        portfolio.SelectJointLocalFreight(local, freight,
            Prices(sites, localPrice, externalPrice, source),
            Purchasing(gross), Congestion(), quantum,
            maximumQuanta, maximumConfigurations);

    [Fact]
    public void SimultaneousLocalAndFreightConserveSameSourceAndDemand()
    {
        var (f, sites, portfolio) = Build();
        var (local, freight, quantum) = Pair(sites, portfolio);
        var before = f.Session.ExportSaveJson();
        var result = Select(portfolio, sites, [local], [freight], quantum,
            source: freight.Source);
        Assert.Equal(4, result.PossibleConfigurations);
        Assert.Equal(4, result.EvaluatedConfigurations);
        Assert.True(result.FeasibleConfigurations >= 2);
        Assert.True(result.Lots.Count >= 1);
        Assert.All(result.Lots, x => Assert.Equal(quantum, x.RawQuantity));
        var source = sites.At(local.Point)!.Supply.Single(x =>
            x.CommodityId == local.Commodity && x.Activity == local.Activity);
        var selected = result.Lots.Where(x =>
            x.Option.Source == local.Point &&
            x.Option.Commodity == local.Commodity &&
            x.Option.Activity == local.Activity)
            .Sum(x => (long)x.RawQuantity);
        Assert.True(selected <= (long)source.RawQuantity);
        Assert.True(result.ProjectedNetValue.Numerator > 0);
        Assert.False(result.IsActualConsumption);
        Assert.False(result.IsCommitted);
        Assert.False(result.CanAuthorizeRealSettlement);
        result.VerifyIndependent();
        Assert.Equal(before, f.Session.ExportSaveJson());
    }

    [Fact]
    public void LocalOnlyWorksWithoutInventingFreightService()
    {
        var (f, sites, portfolio) = Build();
        var (local, _, quantum) = Pair(sites, portfolio);
        var result = Select(portfolio, sites, [local],
            Array.Empty<CanonicalPrimitiveFreightCandidateUse>(),
            quantum, source: local.Point);
        Assert.Single(result.Lots);
        Assert.True(result.Lots[0].Option.IsLocal);
        Assert.Null(result.FreightPreview);
        Assert.Equal(local.Point, result.Lots[0].Option.Buyer);
        result.VerifyIndependent();
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void AHigherExternalPriceCanMakeFreightCommerciallyAttractive()
    {
        var (_, sites, portfolio) = Build();
        var (local, freight, quantum) = Pair(sites, portfolio);
        var result = Select(portfolio, sites, [local], [freight],
            quantum, localPrice: 1, externalPrice: 100,
            source: freight.Source);
        Assert.Contains(result.Lots, x => !x.Option.IsLocal);
        Assert.True(result.ProjectedNetValue.Numerator > 0);
        result.VerifyIndependent();
    }

    [Fact]
    public void LocalAndFreightCandidateReorderingPreservesExactOptimum()
    {
        var (_, sites, portfolio) = Build();
        var (local, freight, quantum) = Pair(sites, portfolio);
        var secondLocal = sites.Sites.SelectMany(site => site.Supply
            .Where(s => site.Demand.Any(d => d.CommodityId == s.CommodityId) &&
                !(site.Point == local.Point && s.CommodityId == local.Commodity &&
                  s.Activity == local.Activity))
            .Select(s => new CanonicalPrimitiveLocalUseProposal(
                site.Point, s.CommodityId, s.Activity, s.RawQuantity)))
            .FirstOrDefault();
        Assert.NotNull(secondLocal);
        var forward = Select(portfolio, sites,
            [local, secondLocal], [freight], quantum, source: freight.Source);
        var reverse = Select(portfolio, sites,
            [secondLocal, local], [freight], quantum, source: freight.Source);
        Assert.Equal(forward.Lots.Select(x => (
            x.Option.Source, x.Option.Commodity, x.Option.IsLocal, x.RawQuantity)),
            reverse.Lots.Select(x => (
            x.Option.Source, x.Option.Commodity, x.Option.IsLocal, x.RawQuantity)));
        Assert.Equal(0, forward.ProjectedNetValue.CompareTo(
            reverse.ProjectedNetValue));
        forward.VerifyIndependent();
        reverse.VerifyIndependent();
    }

    [Fact]
    public void ZeroLotWinsWhenNeitherLocalNorFreightFitsProtectedH()
    {
        var (_, sites, portfolio) = Build();
        var (local, freight, quantum) = Pair(sites, portfolio);
        var result = Select(portfolio, sites, [local], [freight],
            quantum, localPrice: 1_000_000, externalPrice: 1_000_000,
            gross: 1, source: freight.Source);
        Assert.Empty(result.Lots);
        Assert.Equal(0, result.ProjectedNetValue.Numerator);
        Assert.Equal(1, result.FeasibleConfigurations);
        result.VerifyIndependent();
    }

    [Fact]
    public void ForgedFreightAndInauthenticLocalBoundsAreRejected()
    {
        var (_, sites, portfolio) = Build();
        var (local, freight, quantum) = Pair(sites, portfolio);
        Assert.Throws<ArgumentException>(() => Select(portfolio, sites,
            [local], [freight with { MaximumRawQuantity =
                freight.MaximumRawQuantity }], quantum));
        Assert.Throws<ArgumentException>(() => Select(portfolio, sites,
            [local, local], [freight], quantum));
        Assert.Throws<ArgumentException>(() => Select(portfolio, sites,
            [local with { RawQuantity = ulong.MaxValue }], [freight], quantum));
        Assert.Throws<ArgumentException>(() => Select(portfolio, sites,
            [local], [freight], quantum, maximumConfigurations: 2));
    }

    [Fact]
    public void RestoredSessionCannotReuseSelectedReceipt()
    {
        var (fixture, sites, portfolio) = Build();
        var (local, freight, quantum) = Pair(sites, portfolio);
        var selected = Select(portfolio, sites, [local], [freight],
            quantum, source: freight.Source);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            fixture.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            selected.RequireCurrent(restored));
        selected.VerifyIndependent();
    }
}
