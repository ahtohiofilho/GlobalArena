using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs97RegionalConcentrationTests
{
    private static (
        GaVs96CertifiedFixture.Data Fixture,
        CanonicalPrimitiveSimultaneousAbsorptionTrial Absorption,
        CanonicalRegionalEconomicConcentrationAudit Audit) Build(
        long woodDemand = 125_000,
        int foodUtilization = 800,
        int woodUtilization = 650,
        int stoneUtilization = 1000,
        int localFood = 250,
        int localWood = 300,
        int localStone = 400,
        bool withRemote = true)
    {
        var fixture = GaVs96CertifiedFixture.Build(woodDemand,
            foodUtilization, woodUtilization, stoneUtilization,
            localFood, localWood, localStone, withRemote);
        var absorption = GaVs96CertifiedFixture.Observe(fixture);
        var audit = CanonicalRegionalEconomicConcentrationAudit
            .FromCertifiedAbsorption(fixture.Fixture.Session,
                fixture.Sites, absorption);
        return (fixture, absorption, audit);
    }

    [Fact]
    public void EachCommodityHasExactIndependentPhysicalAndIndexConcentration()
    {
        var (f, source, audit) = Build();
        var save = f.Fixture.Session.ExportSaveJson();
        var hash = f.Fixture.Session.CurrentHash;

        Assert.True(audit.UsesOnlyCertifiedVs96PhysicalMarkets);
        Assert.True(audit.ComparesWithinEachCommodityOnly);
        Assert.True(audit.UsesSpatialMarketProxiesNotPoliticalRegions);
        Assert.True(audit.PreservesAbstractFunctionRegulatedMoneySource);
        Assert.False(audit.DebitsHouseholdOrPopulationCash);
        Assert.False(audit.CreatesBuyerBudgetsOrCredits);
        Assert.False(audit.ConvertsIndicesToMoney);
        Assert.False(audit.PostsStateTreasuryRevenue);
        Assert.False(audit.ClaimsExecutedWoodStoneSales);
        Assert.False(audit.MutatesWorldOrSave);
        Assert.False(audit.CanAuthorizeSettlement);
        Assert.Equal(source.Markets.Count, audit.Markets.Count);
        Assert.Equal(3, audit.Commodities.Count);

        foreach (var c in audit.Commodities)
        {
            var old = source.Commodities.Single(x => x.Commodity == c.Commodity);
            var regional = audit.Markets.Where(x => x.Commodity == c.Commodity)
                .ToArray();
            Assert.Equal(f.Sites.Sites.Count, c.InhabitedMarketCount);
            Assert.Equal(c.InhabitedMarketCount, regional.Length);
            Assert.Equal(old.LocallyAbsorbedRaw + old.RemotelyAbsorbedRaw,
                c.TotalAbsorbedRaw);
            Assert.Equal(old.ProjectedDemandSignalRaw, c.BasalDemandSignalRaw);
            Assert.Equal(0, c.GentleTotalGrossRevenueIndex.CompareTo(
                old.GentleGrossRevenueIndex));
            Assert.Equal(0, c.SteepTotalGrossRevenueIndex.CompareTo(
                old.SteepGrossRevenueIndex));
            var expectedTotal = regional.Aggregate(BigInteger.Zero,
                (sum, x) => sum + x.HypotheticallyAbsorbedRaw);
            Assert.Equal(c.TotalAbsorbedRaw, expectedTotal);
            var physicalSum = regional.Aggregate(CanonicalRegionalShare.Zero,
                (sum, x) => CanonicalRegionalShare.Add(sum, x.PhysicalShare));
            var gentleSum = regional.Aggregate(CanonicalRegionalShare.Zero,
                (sum, x) => CanonicalRegionalShare.Add(
                    sum, x.GentleGrossIndexShare));
            var steepSum = regional.Aggregate(CanonicalRegionalShare.Zero,
                (sum, x) => CanonicalRegionalShare.Add(
                    sum, x.SteepGrossIndexShare));
            Assert.Equal(0, physicalSum.CompareTo(
                c.TotalAbsorbedRaw.IsZero
                    ? CanonicalRegionalShare.Zero : CanonicalRegionalShare.One));
            Assert.Equal(0, gentleSum.CompareTo(
                c.GentleTotalGrossRevenueIndex.Numerator.IsZero
                    ? CanonicalRegionalShare.Zero : CanonicalRegionalShare.One));
            Assert.Equal(0, steepSum.CompareTo(
                c.SteepTotalGrossRevenueIndex.Numerator.IsZero
                    ? CanonicalRegionalShare.Zero : CanonicalRegionalShare.One));

            var physicalHhi = regional.Aggregate(CanonicalRegionalShare.Zero,
                (sum, x) => CanonicalRegionalShare.Add(
                    sum, x.PhysicalShare.Squared()));
            var gentleHhi = regional.Aggregate(CanonicalRegionalShare.Zero,
                (sum, x) => CanonicalRegionalShare.Add(
                    sum, x.GentleGrossIndexShare.Squared()));
            var steepHhi = regional.Aggregate(CanonicalRegionalShare.Zero,
                (sum, x) => CanonicalRegionalShare.Add(
                    sum, x.SteepGrossIndexShare.Squared()));
            Assert.Equal(c.PhysicalHerfindahlIndex, physicalHhi);
            Assert.Equal(c.GentleGrossHerfindahlIndex, gentleHhi);
            Assert.Equal(c.SteepGrossHerfindahlIndex, steepHhi);
            if (c.TotalAbsorbedRaw > 0)
            {
                var theoreticalMin = CanonicalRegionalShare.Of(
                    1, c.InhabitedMarketCount);
                Assert.True(c.PhysicalHerfindahlIndex.CompareTo(
                    theoreticalMin) >= 0);
                Assert.True(c.PhysicalHerfindahlIndex.CompareTo(
                    CanonicalRegionalShare.One) <= 0);
                var leader = regional.OrderByDescending(x =>
                    x.PhysicalShare, Comparer<CanonicalRegionalShare>.Default)
                    .ThenBy(x => x.Market.Value).First();
                Assert.Equal(leader.Market, c.LargestPhysicalMarket);
            }
        }
        audit.VerifyIndependent();
        Assert.Equal(save, f.Fixture.Session.ExportSaveJson());
        Assert.Equal(hash, f.Fixture.Session.CurrentHash);
    }

    [Fact]
    public void ZeroPhysicalSupplyGivesZeroSharesAndNoInventedRegionalLeader()
    {
        var (_, _, audit) = Build(foodUtilization: 0,
            woodUtilization: 0, stoneUtilization: 0, withRemote: false);
        Assert.All(audit.Markets, x =>
        {
            Assert.Equal(BigInteger.Zero, x.HypotheticallyAbsorbedRaw);
            Assert.Equal(CanonicalRegionalShare.Zero, x.PhysicalShare);
            Assert.Equal(CanonicalRegionalShare.Zero, x.GentleGrossIndexShare);
            Assert.Equal(CanonicalRegionalShare.Zero, x.SteepGrossIndexShare);
        });
        Assert.All(audit.Commodities, x =>
        {
            Assert.Equal(BigInteger.Zero, x.TotalAbsorbedRaw);
            Assert.Equal(CanonicalRegionalShare.Zero, x.PhysicalHerfindahlIndex);
            Assert.Equal(CanonicalRegionalShare.Zero, x.GentleGrossHerfindahlIndex);
            Assert.Equal(CanonicalRegionalShare.Zero, x.SteepGrossHerfindahlIndex);
            Assert.Null(x.LargestPhysicalMarket);
            Assert.Null(x.LargestGentleGrossIndexMarket);
            Assert.Null(x.LargestSteepGrossIndexMarket);
        });
        audit.VerifyIndependent();
    }

    [Fact]
    public void LargeWoodOfferOverBasalDemandIsNotRationedByWalletOrQuantity()
    {
        var (_, source, audit) = Build(woodDemand: 1,
            localWood: 1000, withRemote: false);
        var wood = audit.Commodities.Single(x => x.Commodity == new CommodityId(8));
        Assert.True(wood.TotalAbsorbedRaw > wood.BasalDemandSignalRaw);
        Assert.True(wood.TotalAbsorbedRaw > 0);
        Assert.All(audit.Markets.Where(x => x.Commodity == wood.Commodity),
            x => Assert.Equal(x.HypotheticallyAbsorbedRaw,
                source.Markets.Single(m =>
                    m.Market == x.Market && m.Commodity == x.Commodity)
                    .TotalHypotheticallyAbsorbedRaw));
        Assert.True(wood.PhysicalHerfindahlIndex.Numerator > 0);
        Assert.False(audit.DebitsHouseholdOrPopulationCash);
        audit.VerifyIndependent();
    }

    [Fact]
    public void FreightMayAlterRegionalSharesWithoutMintingReceipts()
    {
        var (f, source, audit) = Build();
        var food = new CommodityId(7);
        var wood = new CommodityId(8);
        foreach (var c in new[] { food, wood })
        {
            Assert.Contains(source.Markets, m =>
                m.Commodity == c && m.ProposedRemoteAbsorptionRaw > 0);
            Assert.Equal(source.Commodities.Single(x => x.Commodity == c)
                    .LocallyAbsorbedRaw +
                source.Commodities.Single(x => x.Commodity == c)
                    .RemotelyAbsorbedRaw,
                audit.Commodities.Single(x => x.Commodity == c).TotalAbsorbedRaw);
        }
        Assert.Null(f.Disposition.Commodities.Single(x =>
                x.Commodity == wood).IndependentlyExecutedFoodPaymentMicrocredits);
        Assert.False(audit.PostsStateTreasuryRevenue);
        Assert.False(audit.CanAuthorizeSettlement);
        audit.VerifyIndependent();
    }

    [Fact]
    public void InvalidRationalFractionsFailClosedAndNormalizePrecisely()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalRegionalShare.Of(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CanonicalRegionalShare.Of(-1, 1));
        Assert.Equal(new CanonicalRegionalShare(1, 2),
            CanonicalRegionalShare.Of(500, 1000));
        Assert.Equal(CanonicalRegionalShare.One,
            CanonicalRegionalShare.Add(
                CanonicalRegionalShare.Of(1, 3),
                CanonicalRegionalShare.Of(2, 3)));
        Assert.Equal(CanonicalRegionalShare.Of(1, 4),
            CanonicalRegionalShare.Of(1, 2).Squared());
        Assert.Equal(0, CanonicalRegionalShare.Of(
            BigInteger.Pow(2, 100), BigInteger.Pow(2, 101))
            .CompareTo(CanonicalRegionalShare.Of(1, 2)));
        Assert.Throws<InvalidOperationException>(() =>
            new CanonicalRegionalShare(1, 0).CompareTo(
                CanonicalRegionalShare.Zero));
    }

    [Fact]
    public void DifferentButIdenticalLookingDiscoveryCannotBorrowVs96Evidence()
    {
        var (f, source, _) = Build();
        var alternative = f.Fixture.Session.DiscoverPrimitiveEconomicSites(
            new CanonicalPrimitiveSiteDiscoveryPolicy(
                new EconomicPrimitiveCommodityMapping(
                    new CommodityId(7), new CommodityId(8), new CommodityId(9)),
                new[] {
                    new CanonicalPrimitiveDemandRate(new CommodityId(7),
                        checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
                    new CanonicalPrimitiveDemandRate(new CommodityId(8), 125_000),
                    new CanonicalPrimitiveDemandRate(new CommodityId(9), 250_000)
                }, new FoodTransportCostFraction(1, 1)));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalRegionalEconomicConcentrationAudit.FromCertifiedAbsorption(
                f.Fixture.Session, alternative, source));
    }

    [Fact]
    public void ForeignSaveMovementTurnAndNetworkChangeInvalidateReport()
    {
        var (f, _, audit) = Build();
        var original = f.Fixture.Session.ExportSaveJson();
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(original);
        Assert.Throws<InvalidOperationException>(() =>
            audit.RequireCurrent(restored));
        f.Fixture.Session.TransportNetwork.SetAccess(
            f.Fixture.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            audit.VerifyIndependent());
        var (other, _, second) = Build();
        other.Fixture.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            second.RequireCurrent(other.Fixture.Session));
    }

    [Fact]
    public void ReplayLeavesSaveAndSourceWitnessIntact()
    {
        var (f, absorption, audit) = Build();
        var save = f.Fixture.Session.ExportSaveJson();
        var state = f.Fixture.Session.CurrentHash;
        for(var i=0;i<3;i++)
        {
            var repeated = CanonicalRegionalEconomicConcentrationAudit
                .FromCertifiedAbsorption(f.Fixture.Session, f.Sites, absorption);
            Assert.Equal(audit.Markets, repeated.Markets);
            Assert.Equal(audit.Commodities, repeated.Commodities);
            repeated.VerifyIndependent();
            Assert.Equal(save, f.Fixture.Session.ExportSaveJson());
            Assert.Equal(state, f.Fixture.Session.CurrentHash);
        }
    }
}
