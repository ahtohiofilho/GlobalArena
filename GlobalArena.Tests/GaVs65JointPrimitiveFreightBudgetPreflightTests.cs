using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs65JointPrimitiveFreightBudgetPreflightTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);
    private static readonly CommodityId Stone = new(9);

    private static FoodCommodityCatalog Catalog() => new(new[] {
        new FoodCommodityDefinition(Food, "food", 1),
        new FoodCommodityDefinition(Wood, "wood", 1),
        new FoodCommodityDefinition(Stone, "stone", 1)
    });

    private static CanonicalPrimitiveSiteDiscoveryPolicy SitePolicy() => new(
        new EconomicPrimitiveCommodityMapping(Food, Wood, Stone),
        new[] {
            new CanonicalPrimitiveDemandRate(Food,
                checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
            new CanonicalPrimitiveDemandRate(Wood, 125_000),
            new CanonicalPrimitiveDemandRate(Stone, 250_000)
        }, new FoodTransportCostFraction(1, 1));

    private static CanonicalEligibleFreightDiscoveryPolicy FreightPolicy(
        CommodityId commodity, EconomicActivityKind activity) => new(
        commodity, activity, new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
        ExperimentalLandInfrastructureKind.StoneRoad, Catalog());

    private static ExperimentalAbstractPurchasingPolicy Purchasing(
        long grossPerResident = 5) => new(
        "VS56-PURCHASING", 1,
        new FoodTransportCostFraction(grossPerResident, 1),
        new FoodTransportCostFraction(5, 8));

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalPrimitiveFreightPortfolio Portfolio) Setup(
            bool secondRight = false)
    {
        var f = GaVs57SessionTransportAuthorityTests.Create();
        var src = CanonicalPrimitiveSiteDiscovery.SiteId(
            f.Anchors[f.Flows.SupplyFlows.Single().EconomicPointId]);
        var dst = CanonicalPrimitiveSiteDiscovery.SiteId(
            f.Anchors[f.Flows.DemandFlows.Single().EconomicPointId]);
        var right = f.Registry().Entitlements.Single() with {
            Source = src, Destination = dst, ServiceId = 77
        };
        f.Session.TransportNetwork.SetEntitlement(right);
        if (secondRight)
            f.Session.TransportNetwork.SetEntitlement(right with { ServiceId = 78 });
        f.Session.ReassignWorkers(f.Session.InitialTile,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1);
        var sites = f.Session.DiscoverPrimitiveEconomicSites(SitePolicy());
        var portfolio = f.Session.DiscoverPrimitiveFreightPortfolio(
            sites, new[] {
                FreightPolicy(Food, EconomicActivityKind.Agriculture),
                FreightPolicy(Wood, EconomicActivityKind.Forestry),
                FreightPolicy(Stone, EconomicActivityKind.Mining)
            }, "VS56-PURCHASING", 1,
            "VS56-CALIBRATION", 1, "GA-VS56-NOMINAL-LAB");
        return (f, sites, portfolio);
    }

    private static ExperimentalBuyerCommodityPrice[] Prices(
        CanonicalPrimitiveSiteDiscovery sites, long wholePrice = 1) =>
        sites.Flows.DemandFlows.Select(x =>
            new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId,
                new FoodTransportCostFraction(wholePrice, 1))).ToArray();

    private static CanonicalPrimitiveJointFreightLot[] ThreeLots(
        CanonicalPrimitiveFreightPortfolio portfolio, ulong rawQuantity = 100_000) =>
        portfolio.CandidateUses
            .Where(x => x.ServiceId == 77)
            .Select(x => new CanonicalPrimitiveJointFreightLot(
                x, Math.Min(x.MaximumRawQuantity, rawQuantity)))
            .ToArray();

    [Fact]
    public void OneBuyerBudgetAndOneCongestionLoadCoverAllThreeProductsOnOneRealRoad()
    {
        var (f, sites, portfolio) = Setup();
        var save = f.Session.ExportSaveJson();
        var raw = ThreeLots(portfolio);
        Assert.Equal(3, raw.Length);
        var forecast = portfolio.PreviewJointBudget(
            raw.Reverse(), Prices(sites), Purchasing(5000), Congestion());
        Assert.Equal(new[] { Food, Wood, Stone },
            forecast.Lots.Select(x => x.Candidate.Commodity));
        Assert.True(forecast.IsBudgetFeasible);
        Assert.True(forecast.Buyers.CanSettleAtSight);
        Assert.False(forecast.CanAuthorizeRealSettlement);
        Assert.False(forecast.IsCommitted);
        Assert.Single(forecast.Freight.CorridorLoads);
        Assert.Equal(
            raw.Aggregate(BigInteger.Zero, (sum, x) => sum + x.RawQuantity),
            Assert.Single(forecast.Freight.CorridorLoads).Value);
        Assert.Equal(0, forecast.Freight.TotalCost.CompareTo(
            forecast.Buyers.TotalBuyerFreight));
        var paidBuyer = Assert.Single(forecast.Buyers.Buyers, x =>
            x.TotalCommitment.Numerator > 0);
        Assert.True(paidBuyer.ProtectedResidual.Numerator > 0);
        Assert.True(paidBuyer.RemainingSpendableCash.Numerator >= 0);
        Assert.Equal(sites.BuyerAnchors.Count, forecast.Purchasing.Buyers.Count);
        Assert.Equal(save, f.Session.ExportSaveJson());
        forecast.RequireCurrent(f.Session);
        Assert.Empty(typeof(CanonicalPrimitiveJointFreightBudgetPreview).GetConstructors());
    }

    [Fact]
    public void DisproportionateMultiProductCostsFailClosedAgainstOneAbstractBudget()
    {
        var (f, sites, portfolio) = Setup();
        var save = f.Session.ExportSaveJson();
        var forecast = portfolio.PreviewJointBudget(
            ThreeLots(portfolio), Prices(sites, 1_000_000), Purchasing(), Congestion());
        Assert.False(forecast.IsBudgetFeasible);
        Assert.False(forecast.Buyers.CanSettleAtSight);
        Assert.Contains(forecast.Buyers.Buyers, x =>
            x.TotalCommitment.CompareTo(x.SpendableCash) > 0);
        Assert.All(forecast.Buyers.Buyers, x =>
            Assert.True(x.RemainingSpendableCash.Numerator >= 0));
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.False(forecast.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void MultipleRealRightsCannotSellTheSameSourceOutputTwice()
    {
        var (f, sites, portfolio) = Setup(secondRight: true);
        var food = portfolio.CandidateUses
            .Where(x => x.Commodity == Food).OrderBy(x => x.ServiceId).ToArray();
        Assert.Equal(new ulong[] { 77, 78 }, food.Select(x => x.ServiceId));
        var proposals = food.Select(x =>
            new CanonicalPrimitiveJointFreightLot(x, x.MaximumRawQuantity)).ToArray();
        Assert.Throws<ArgumentException>(() =>
            portfolio.PreviewJointBudget(proposals, Prices(sites),
                Purchasing(), Congestion()));
        Assert.True(portfolio.SharedAssets.Count > 0);
        Assert.False(portfolio.CanAuthorizeRealSettlement);
        portfolio.RequireCurrent(f.Session);
    }

    [Fact]
    public void MissingPricesForeignCandidatesAndDuplicateLotsAreRejected()
    {
        var (f, sites, portfolio) = Setup();
        var first = ThreeLots(portfolio)[0];
        var prices = Prices(sites);
        Assert.Throws<ArgumentException>(() =>
            portfolio.PreviewJointBudget([first], prices.Skip(1),
                Purchasing(), Congestion()));
        Assert.Throws<ArgumentException>(() =>
            portfolio.PreviewJointBudget([first, first], prices,
                Purchasing(), Congestion()));
        Assert.Throws<ArgumentException>(() =>
            portfolio.PreviewJointBudget([
                first with { RawQuantity = first.Candidate.MaximumRawQuantity + 1 }
            ], prices, Purchasing(), Congestion()));
        Assert.Throws<ArgumentException>(() =>
            portfolio.PreviewJointBudget([
                first with { Candidate = first.Candidate with { } }
            ], prices, Purchasing(), Congestion()));
        Assert.Throws<ArgumentException>(() =>
            portfolio.PreviewJointBudget([first], prices,
                new ExperimentalAbstractPurchasingPolicy("FOREIGN", 1,
                    new FoodTransportCostFraction(5, 1),
                    new FoodTransportCostFraction(5, 8)), Congestion()));
        Assert.Throws<ArgumentException>(() =>
            portfolio.PreviewJointBudget(
                Array.Empty<CanonicalPrimitiveJointFreightLot>(),
                prices, Purchasing(), Congestion()));
        Assert.Equal(f.Session.CurrentHash, sites.WorldHash);
    }

    [Fact]
    public void SaveRestorePhysicalRevisionAndNetworkClosureExpireWholeJointPreview()
    {
        var (f, sites, portfolio) = Setup();
        var lots = ThreeLots(portfolio);
        var old = portfolio.PreviewJointBudget(
            lots, Prices(sites), Purchasing(), Congestion());
        var saved = f.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Throws<InvalidOperationException>(() => old.RequireCurrent(restored));
        var newSites = restored.DiscoverPrimitiveEconomicSites(SitePolicy());
        var newPortfolio = restored.DiscoverPrimitiveFreightPortfolio(
            newSites, portfolio.Slices.Select(x => x.Policy),
            "VS56-PURCHASING", 1, "VS56-CALIBRATION", 1,
            "GA-VS56-NOMINAL-LAB");
        var renewed = newPortfolio.PreviewJointBudget(
            ThreeLots(newPortfolio), Prices(newSites), Purchasing(), Congestion());
        Assert.Equal(old.Buyers.Buyers.Select(x =>
            (x.Buyer, x.TotalCommitment.Numerator, x.TotalCommitment.Denominator)),
            renewed.Buyers.Buyers.Select(x =>
            (x.Buyer, x.TotalCommitment.Numerator, x.TotalCommitment.Denominator)));
        Assert.Equal(saved, restored.ExportSaveJson());
        f.Session.TransportNetwork.SetAccess(f.Asset.GeographicEdgeId, false);
        Assert.Throws<InvalidOperationException>(() => old.RequireCurrent(f.Session));
        Assert.Throws<InvalidOperationException>(() =>
            portfolio.PreviewJointBudget(lots, Prices(sites), Purchasing(), Congestion()));
    }

    [Fact]
    public void SameProposalInAnyInputOrderHasIdenticalJointCashAndCongestion()
    {
        var (f, sites, portfolio) = Setup();
        var proposals = ThreeLots(portfolio);
        var first = portfolio.PreviewJointBudget(
            proposals, Prices(sites), Purchasing(), Congestion());
        var second = portfolio.PreviewJointBudget(
            proposals.Reverse(), Prices(sites).Reverse(),
            Purchasing(), Congestion());
        Assert.Equal(first.Lots, second.Lots);
        Assert.Equal(first.Buyers.Buyers.Select(x =>
            (x.Buyer, x.TotalCommitment.Numerator, x.TotalCommitment.Denominator,
                x.RemainingSpendableCash.Numerator, x.RemainingSpendableCash.Denominator)),
            second.Buyers.Buyers.Select(x =>
            (x.Buyer, x.TotalCommitment.Numerator, x.TotalCommitment.Denominator,
                x.RemainingSpendableCash.Numerator, x.RemainingSpendableCash.Denominator)));
        Assert.Equal(0, first.Freight.TotalCost.CompareTo(second.Freight.TotalCost));
        Assert.Equal(first.Freight.CorridorLoads.OrderBy(x => x.Key.ToString()),
            second.Freight.CorridorLoads.OrderBy(x => x.Key.ToString()));
        first.RequireCurrent(f.Session);
    }
}
