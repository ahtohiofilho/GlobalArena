using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class GaVs82RealizedProductionAllocationTests
{
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Wood = new(8);

    private static ExperimentalSharedFreightCongestionPolicy Congestion() =>
        new(1, 1_000_000);

    private static (
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture Fixture,
        CanonicalPrimitiveSiteDiscovery Sites,
        CanonicalSimultaneousFreightReference Freight) Build()
    {
        var (f, sites, portfolio) =
            GaVs67IndependentFreightComponentsTests.SharedBuyer();
        var reference = CanonicalSimultaneousFreightReference.FromSites(
            f.Session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        return (f, sites, reference);
    }

    private static CanonicalRealizedProductionDeclaration Produce(
        CanonicalSimultaneousFreightOffer offer, ulong q, ulong keep = 0) =>
        new(offer.Source, offer.Commodity, offer.Activity, q, keep);

    private static CanonicalRealizedProductionAllocation Allocate(
        GaVs56BuiltNetworkInstitutionalRightsTests.Fixture f,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference reference,
        IEnumerable<CanonicalRealizedProductionDeclaration> production,
        IEnumerable<CanonicalSimultaneousFreightLot> lots) =>
        CanonicalRealizedProductionAllocation.FromSites(
            f.Session, sites, reference, production, lots, Congestion());

    [Fact]
    public void CapacityNotExtractedCannotBeSoldOrCountedAsInventoriedOutput()
    {
        var (f, sites, reference) = Build();
        var before = f.Session.ExportSaveJson();
        var food = reference.Offers.Single(x => x.Commodity == Food);
        var produced = Math.Min(100_000UL, food.SourcePotentialRaw - 1UL);
        Assert.True(produced > 2000UL);
        var result = Allocate(f, sites, reference,
            [Produce(food, produced, 1000UL)],
            [new(food, 500UL)]);
        var source = Assert.Single(result.Sources);
        Assert.Equal(food.SourcePotentialRaw, source.PotentialRaw);
        Assert.Equal(produced, source.HypotheticallyProducedRaw);
        Assert.Equal(food.SourcePotentialRaw - produced,
            source.PotentialNotExtractedRaw);
        Assert.Equal(1000UL, source.DeliberatelyRetainedRaw);
        Assert.Equal(new BigInteger(500), source.HypotheticallyRemotelyShippedRaw);
        Assert.Equal(new BigInteger(produced - 1500UL),
            source.OfferedAndLocallyAbsorbedRaw);
        var local = Assert.Single(result.LocalMarkets);
        Assert.Equal(source.OfferedAndLocallyAbsorbedRaw,
            local.TotalLocallyAbsorbedRaw);
        Assert.True(local.GentleGrossRevenueIndex.Numerator > 0);
        Assert.Equal(new BigInteger(500), Assert.Single(result.RemoteMarkets)
            .TotalHypotheticalDeliveredRaw);
        Assert.True(result.AuthenticatedNominalFreightCost.Numerator >= 0);
        Assert.False(result.TreatsUnextractedPotentialAsInventory);
        Assert.False(result.CreatesInvoluntaryStock);
        Assert.False(result.CreatesActualConsumption);
        Assert.False(result.UsesExogenousBuyerMoney);
        Assert.False(result.CapsConsumptionByDemandRaw);
        Assert.False(result.ProvesHardFreightThroughput);
        Assert.False(result.ComputesMonetaryNetRevenue);
        Assert.False(result.CanAuthorizeRealSettlement);
        result.VerifyIndependent();
        Assert.Equal(before, f.Session.ExportSaveJson());
    }

    [Fact]
    public void LocalOnlyPrimitiveEconomyRequiresNoCarriageOrArtificialBuyer()
    {
        var (f, sites, reference) = Build();
        var food = reference.Offers.Single(x => x.Commodity == Food);
        var result = Allocate(f, sites, reference,
            [Produce(food, 2500UL, 400UL)],
            Array.Empty<CanonicalSimultaneousFreightLot>());
        Assert.Null(result.FreightAudit);
        Assert.Empty(result.RemoteMarkets);
        Assert.Equal(BigInteger.Zero, result.AuthenticatedNominalFreightCost.Numerator);
        Assert.Equal(new BigInteger(2100), Assert.Single(result.LocalMarkets)
            .TotalLocallyAbsorbedRaw);
        Assert.Equal(new BigInteger(2100), Assert.Single(result.Sources)
            .OfferedAndLocallyAbsorbedRaw);
        result.VerifyIndependent();
    }

    [Fact]
    public void ActualProductionRatherThanPotentialIsTheFreightVolumeCap()
    {
        var (f, sites, reference) = Build();
        var food = reference.Offers.Single(x => x.Commodity == Food);
        Assert.True(food.SourcePotentialRaw > 100UL);
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [Produce(food, 100UL, 10UL)], [new(food, 91UL)]));
        var valid = Allocate(f, sites, reference,
            [Produce(food, 100UL, 10UL)], [new(food, 90UL)]);
        Assert.Equal(BigInteger.Zero,
            Assert.Single(valid.Sources).OfferedAndLocallyAbsorbedRaw);
        Assert.Equal(new BigInteger(90),
            Assert.Single(valid.Sources).HypotheticallyRemotelyShippedRaw);
        valid.VerifyIndependent();
    }

    [Fact]
    public void SimultaneousCarriersCannotDoubleAllocateRealizedProduction()
    {
        var (f, sites, portfolio, _, _) =
            GaVs67IndependentFreightComponentsTests.TwoDisjointRoads();
        var refFreight = CanonicalSimultaneousFreightReference.FromSites(
            f.Session, sites, portfolio.Slices.Select(x => x.Policy),
            portfolio.PurchasingPolicyId, portfolio.PurchasingPolicyVersion,
            portfolio.CalibrationId, portfolio.CalibrationVersion,
            portfolio.NominalUnitId);
        var offers = refFreight.Offers.Where(x => x.Commodity == Food)
            .OrderBy(x => x.ServiceId).Take(2).ToArray();
        Assert.Equal(2, offers.Length);
        Assert.Equal(offers[0].Source, offers[1].Source);
        var production = Produce(offers[0], 1000UL, 100UL);
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, refFreight,
            [production], [new(offers[0], 600UL), new(offers[1], 301UL)]));
        var report = Allocate(f, sites, refFreight,
            [production], [new(offers[0], 600UL), new(offers[1], 300UL)]);
        Assert.Equal(new BigInteger(900),
            Assert.Single(report.Sources).HypotheticallyRemotelyShippedRaw);
        Assert.Equal(2, report.RemoteMarkets.Count);
        Assert.Equal(BigInteger.Zero, Assert.Single(report.Sources)
            .OfferedAndLocallyAbsorbedRaw);
        report.VerifyIndependent();
    }

    [Fact]
    public void InvalidProductionDeclarationsAndUnrealizedSourcesFailClosed()
    {
        var (f, sites, reference) = Build();
        var food = reference.Offers.Single(x => x.Commodity == Food);
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [Produce(food, 0UL)], []));
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [Produce(food, checked(food.SourcePotentialRaw + 1UL))], []));
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [Produce(food, 40UL, 41UL)], []));
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [Produce(food, 40UL), Produce(food, 40UL)], []));
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [new(new EconomicPointId(999999UL), Food,
                EconomicActivityKind.Agriculture, 10UL, 0UL)], []));
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [Produce(food, 100UL)],
            [new(food with { }, 1UL)]));
        var wood = reference.Offers.Single(x => x.Commodity == Wood);
        Assert.Throws<ArgumentException>(() => Allocate(f, sites, reference,
            [Produce(food, 100UL)], [new(wood, 1UL)]));
    }

    [Fact]
    public void DifferentCommoditiesUseDistinctRealizationPoolsAndSameSiteDemand()
    {
        var (f, sites, reference) = Build();
        var food = reference.Offers.Single(x => x.Commodity == Food);
        var wood = reference.Offers.Single(x => x.Commodity == Wood);
        var result = Allocate(f, sites, reference,
            [Produce(food, 500UL, 10UL), Produce(wood, 400UL, 20UL)],
            [new(food, 300UL), new(wood, 350UL)]);
        Assert.Equal(2, result.Sources.Count);
        Assert.Equal(new BigInteger(190), result.Sources.Single(x =>
            x.Commodity == Food).OfferedAndLocallyAbsorbedRaw);
        Assert.Equal(new BigInteger(30), result.Sources.Single(x =>
            x.Commodity == Wood).OfferedAndLocallyAbsorbedRaw);
        Assert.Equal(2, result.LocalMarkets.Count);
        Assert.Equal(2, result.RemoteMarkets.Count);
        Assert.True(result.AuthenticatedNominalFreightCost.Numerator > 0);
        result.VerifyIndependent();
    }

    [Fact]
    public void PermutationReplayAndRestoreAreStrictlyDeterministicWithoutWorldChanges()
    {
        var (f, sites, reference) = Build();
        var before = f.Session.ExportSaveJson();
        var food = reference.Offers.Single(x => x.Commodity == Food);
        var wood = reference.Offers.Single(x => x.Commodity == Wood);
        var a = Allocate(f, sites, reference,
            [Produce(food, 500UL, 10UL), Produce(wood, 400UL, 20UL)],
            [new(wood, 350UL), new(food, 300UL)]);
        var b = Allocate(f, sites, reference,
            [Produce(wood, 400UL, 20UL), Produce(food, 500UL, 10UL)],
            [new(food, 300UL), new(wood, 350UL)]);
        Assert.Equal(a.Sources, b.Sources);
        Assert.Equal(a.LocalMarkets, b.LocalMarkets);
        Assert.Equal(a.RemoteMarkets, b.RemoteMarkets);
        Assert.Equal(0, a.AuthenticatedNominalFreightCost.CompareTo(
            b.AuthenticatedNominalFreightCost));
        Assert.Equal(before, f.Session.ExportSaveJson());
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Throws<InvalidOperationException>(() => a.RequireCurrent(restored));
        a.VerifyIndependent();
        b.VerifyIndependent();
    }
}
