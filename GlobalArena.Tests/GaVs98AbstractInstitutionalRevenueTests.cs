using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

internal static class GaVs98InstitutionalRevenueFixture
{
    internal sealed record Data(
        GaVs96CertifiedFixture.Data Physical,
        CanonicalPrimitiveSimultaneousAbsorptionTrial Absorption,
        CanonicalRegionalEconomicConcentrationAudit Regional,
        CanonicalAbstractRevenueEntitlement[] Entitlements);

    internal static Data Build(bool zero = false)
    {
        var physical = zero ? GaVs96CertifiedFixture.Build(
            foodUtilization: 0, woodUtilization: 0,
            stoneUtilization: 0, withRemote: false) :
            GaVs96CertifiedFixture.Build();
        var absorption = GaVs96CertifiedFixture.Observe(physical);
        var regional = CanonicalRegionalEconomicConcentrationAudit
            .FromCertifiedAbsorption(physical.Fixture.Session,
                physical.Sites, absorption);
        var publicAuthority = new ExperimentalTransportInstitution(
            "GA-VS98-PUBLIC-LAB",
            ExperimentalTransportInstitutionKind.PublicAuthority);
        var community = new ExperimentalTransportInstitution(
            "GA-VS98-COMMUNITY-LAB",
            ExperimentalTransportInstitutionKind.Community);
        var entitlements = physical.Disposition.Sources.Where(x =>
            x.ProposedLocalUseRaw + x.ProposedRemoteDeliveryRaw > 0)
            .SelectMany(x => new[] {
                new CanonicalAbstractRevenueEntitlement(
                    x.Source, x.Commodity, x.Activity, publicAuthority, 600),
                new CanonicalAbstractRevenueEntitlement(
                    x.Source, x.Commodity, x.Activity, community, 400)
            }).ToArray();
        return new(physical, absorption, regional, entitlements);
    }

    internal static CanonicalAbstractInstitutionalRevenuePreflight Preview(
        Data d, IEnumerable<CanonicalAbstractRevenueEntitlement>? rights = null,
        CanonicalPrimitiveSimultaneousAbsorptionTrial? absorption = null,
        CanonicalRegionalEconomicConcentrationAudit? regional = null,
        CanonicalPrimitivePhysicalDispositionTrial? disposition = null,
        CanonicalPrimitiveSiteDiscovery? sites = null) =>
        CanonicalAbstractInstitutionalRevenuePreflight.FromCertifiedMarkets(
            d.Physical.Fixture.Session, sites ?? d.Physical.Sites,
            disposition ?? d.Physical.Disposition,
            absorption ?? d.Absorption, regional ?? d.Regional,
            rights ?? d.Entitlements);
}

public sealed class GaVs98AbstractInstitutionalRevenueTests
{
    [Fact]
    public void SourceMarketToInstitutionConservesEveryCommodityInExactFractions()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        var save = d.Physical.Fixture.Session.ExportSaveJson();
        var hash = d.Physical.Fixture.Session.CurrentHash;
        var report = GaVs98InstitutionalRevenueFixture.Preview(d);
        Assert.True(report.OriginatesRevenueFromPhysicalOfferAndDemandFunctions);
        Assert.True(report.UsesExplicitInstitutionalBeneficiaries);
        Assert.False(report.UsesBuyerCashBalances);
        Assert.False(report.HasBuyerBudgetOrDebit);
        Assert.False(report.IssuesLoansOrCreatesCredit);
        Assert.False(report.RequiresAnExistingPopulationMoneySupply);
        Assert.False(report.MakesAnyNumericalMonetaryConversion);
        Assert.False(report.AuthenticatesLegalOwnershipOrTaxRights);
        Assert.False(report.PostsSpendableStateTreasuryCredits);
        Assert.False(report.ChangesLegacyFoodCashAccounts);
        Assert.False(report.CreatesPhysicalStockOrDelivery);
        Assert.False(report.MutatesCanonicalWorldOrSave);
        Assert.False(report.CanAuthorizeSettlement);
        Assert.Equal(3, report.Commodities.Count);

        foreach (var c in report.Commodities)
        {
            var existing = d.Absorption.Commodities.Single(x =>
                x.Commodity == c.Commodity);
            Assert.Equal(existing.LocallyAbsorbedRaw + existing.RemotelyAbsorbedRaw,
                c.HypotheticallyAbsorbedPhysicalRaw);
            Assert.Equal(0, c.TotalGentleRelativeGrossIndex.CompareTo(
                existing.GentleGrossRevenueIndex));
            Assert.Equal(0, c.TotalSteepRelativeGrossIndex.CompareTo(
                existing.SteepGrossRevenueIndex));
            var sourceRows = report.SourceMarkets.Where(x =>
                x.Commodity == c.Commodity).ToArray();
            var institutionalRows = report.Institutions.Where(x =>
                x.Commodity == c.Commodity).ToArray();
            Assert.Equal(c.HypotheticallyAbsorbedPhysicalRaw,
                sourceRows.Aggregate(BigInteger.Zero,
                    (sum, x) => sum + x.HypotheticalPhysicalRaw));
            var institutionalGentle = institutionalRows.Aggregate(
                CanonicalEndogenousPriceIndex.Zero,
                (sum, x) => CanonicalEndogenousPriceIndex.Add(
                    sum, x.GentleRelativeGrossIndex));
            Assert.Equal(0, institutionalGentle.CompareTo(
                c.TotalGentleRelativeGrossIndex));
            var publicRow = Assert.Single(institutionalRows, x =>
                x.Beneficiary.Kind ==
                    ExperimentalTransportInstitutionKind.PublicAuthority);
            var nonpublicRow = Assert.Single(institutionalRows, x =>
                x.Beneficiary.Kind ==
                    ExperimentalTransportInstitutionKind.Community);
            Assert.Equal(0, c.PublicGentleRelativeGrossIndex.CompareTo(
                publicRow.GentleRelativeGrossIndex));
            Assert.Equal(0, c.PublicSteepRelativeGrossIndex.CompareTo(
                publicRow.SteepRelativeGrossIndex));
            Assert.Equal(0, publicRow.GentleRelativeGrossIndex.CompareTo(
                c.TotalGentleRelativeGrossIndex.Times(600).DividedBy(1000)));
            Assert.Equal(0, nonpublicRow.SteepRelativeGrossIndex.CompareTo(
                c.TotalSteepRelativeGrossIndex.Times(400).DividedBy(1000)));
        }
        report.VerifyIndependent();
        Assert.Equal(save, d.Physical.Fixture.Session.ExportSaveJson());
        Assert.Equal(hash, d.Physical.Fixture.Session.CurrentHash);
    }

    [Fact]
    public void RemoteReceiptsAreAttributedToProducerSourceNotToBuyerProperty()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        var report = GaVs98InstitutionalRevenueFixture.Preview(d);
        var remote = d.Physical.Disposition.ProposedRemoteLots.Single(x =>
            x.Offer.Commodity == new CommodityId(8));
        var attribution = Assert.Single(report.SourceMarkets, x =>
            x.Source == remote.Offer.Source &&
            x.Market == remote.Offer.Buyer &&
            x.Commodity == remote.Offer.Commodity);
        Assert.Equal(new BigInteger(remote.RawQuantity),
            attribution.HypotheticalPhysicalRaw);
        var market = d.Absorption.Markets.Single(x =>
            x.Market == attribution.Market &&
            x.Commodity == attribution.Commodity);
        Assert.Equal(0, attribution.GentleRelativeGrossIndex.CompareTo(
            market.GentleGrossRevenueIndex
                .Times(remote.RawQuantity)
                .DividedBy(market.TotalHypotheticallyAbsorbedRaw)));
        report.VerifyIndependent();
    }

    [Fact]
    public void NoProductionAndNoRevenueRequireNoInventedBeneficiaries()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build(zero: true);
        Assert.Empty(d.Entitlements);
        var report = GaVs98InstitutionalRevenueFixture.Preview(d);
        Assert.Empty(report.SourceMarkets);
        Assert.Empty(report.Institutions);
        Assert.All(report.Commodities, x =>
        {
            Assert.Equal(BigInteger.Zero, x.HypotheticallyAbsorbedPhysicalRaw);
            Assert.Equal(0, x.TotalGentleRelativeGrossIndex.CompareTo(
                CanonicalEndogenousPriceIndex.Zero));
            Assert.Equal(0, x.PublicSteepRelativeGrossIndex.CompareTo(
                CanonicalEndogenousPriceIndex.Zero));
        });
        report.VerifyIndependent();
    }

    [Fact]
    public void InstitutionalSharesMustBeExactAndCompletelyCoverRealActiveSources()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        var first = d.Entitlements[0];
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Skip(1)));
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Concat([first])));
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Select((x, i) =>
                    i == 0 ? x with { SharePermille = 601 } : x)));
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Select((x, i) =>
                    i == 0 ? x with { SharePermille = 0 } : x)));
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Select((x, i) =>
                    i == 0 ? x with { SharePermille = 1001 } : x)));
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Concat([
                    first with { Source = new EconomicPointId(999_999) }])));
    }

    [Fact]
    public void InvalidAndContradictoryInstitutionalIdentitiesAreRejected()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Select((x, i) =>
                    i == 0 ? x with {
                        Beneficiary = new ExperimentalTransportInstitution(
                            "", ExperimentalTransportInstitutionKind.PublicAuthority)
                    } : x)));
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Select((x, i) =>
                    i == 0 ? x with {
                        Beneficiary = new ExperimentalTransportInstitution(
                            "INVALID", ExperimentalTransportInstitutionKind.Unspecified)
                    } : x)));
        Assert.Throws<ArgumentException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                d.Entitlements.Select((x, i) =>
                    i == 0 ? x with {
                        Beneficiary = new ExperimentalTransportInstitution(
                            "GA-VS98-COMMUNITY-LAB",
                            ExperimentalTransportInstitutionKind.PublicAuthority)
                    } : x)));
    }

    [Fact]
    public void PolicyPermutationDoesNotChangeRevenueOrBeneficiaryIdentity()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        var a = GaVs98InstitutionalRevenueFixture.Preview(d);
        var b = GaVs98InstitutionalRevenueFixture.Preview(d,
            d.Entitlements.Reverse());
        Assert.Equal(a.Entitlements, b.Entitlements);
        Assert.Equal(a.SourceMarkets, b.SourceMarkets);
        Assert.Equal(a.Institutions, b.Institutions);
        Assert.Equal(a.Commodities, b.Commodities);
        a.VerifyIndependent();
        b.VerifyIndependent();
    }

    [Fact]
    public void CrossEvidenceCrossSessionAndNetworkOrTurnMutationFailClosed()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        var report = GaVs98InstitutionalRevenueFixture.Preview(d);
        var alternative = GaVs96CertifiedFixture.Observe(d.Physical);
        Assert.Throws<InvalidOperationException>(() =>
            GaVs98InstitutionalRevenueFixture.Preview(d,
                absorption: alternative));

        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            d.Physical.Fixture.Session.ExportSaveJson());
        Assert.Throws<InvalidOperationException>(() =>
            report.RequireCurrent(restored));
        d.Physical.Fixture.Session.TransportNetwork.SetAccess(
            d.Physical.Fixture.Asset.GeographicEdgeId, true);
        Assert.Throws<InvalidOperationException>(() =>
            report.VerifyIndependent());

        var another = GaVs98InstitutionalRevenueFixture.Build();
        var other = GaVs98InstitutionalRevenueFixture.Preview(another);
        another.Physical.Fixture.Session.AdvanceMovementTurn();
        Assert.Throws<InvalidOperationException>(() =>
            other.RequireCurrent(another.Physical.Fixture.Session));
    }

    [Fact]
    public void NoCashTransferAcrossManyReplaysIncludingLegacyFoodAccounts()
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        var save = d.Physical.Fixture.Session.ExportSaveJson();
        var money = d.Physical.Fixture.Session.FoodCash!
            .InitialMoneySupplyMicrocredits;
        var cash = d.Physical.Fixture.Session.FoodCash.Accounts.ToArray();
        var hash = d.Physical.Fixture.Session.CurrentHash;
        for (var i = 0; i < 3; i++)
        {
            var a = GaVs98InstitutionalRevenueFixture.Preview(d);
            a.VerifyIndependent();
            Assert.Equal(save, d.Physical.Fixture.Session.ExportSaveJson());
            Assert.Equal(hash, d.Physical.Fixture.Session.CurrentHash);
            Assert.Equal(money, d.Physical.Fixture.Session.FoodCash
                .InitialMoneySupplyMicrocredits);
            Assert.Equal(cash, d.Physical.Fixture.Session.FoodCash.Accounts);
        }
    }
}
