using System.Diagnostics;
using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Real world/cycle fixture, warmed once; 8/32/128 deterministic audits
/// count allocation and invariant verification, not geographic scale.
/// No hard performance cutoff because CI runners have variable speed.
/// </summary>
public sealed class GaVs95PhysicalDispositionBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs95PhysicalDispositionBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void AuthenticatedLocalPhysicalBatchBenchmark(int iterations)
    {
        var (f, _, _, audit92) = GaVs92ExecutedFoodSampleAuditTests.Build();
        var food = new CommodityId(7);
        var wood = new CommodityId(8);
        var stone = new CommodityId(9);
        var policy = new CanonicalPrimitiveSiteDiscoveryPolicy(
            new EconomicPrimitiveCommodityMapping(food, wood, stone),
            new[] {
                new CanonicalPrimitiveDemandRate(food,
                    checked((long)CanonicalPhysicalTacticalSession.FoodDemandRawPerWorker)),
                new CanonicalPrimitiveDemandRate(wood, 125_000),
                new CanonicalPrimitiveDemandRate(stone, 250_000)
            }, new FoodTransportCostFraction(1, 1));
        var sites = f.Session.DiscoverPrimitiveEconomicSites(policy);
        var evidence = CanonicalPrimitiveCommodityExecutionBoundary
            .FromCertifiedEvidence(f.Session, sites, audit92);
        var physical = CanonicalPrimitivePhysicalMulticommodityBatch
            .FromCertifiedEvidence(f.Session, sites, evidence,
                new[] {
                    new CanonicalPrimitivePhysicalUtilization(
                        EconomicActivityKind.Agriculture, 800),
                    new CanonicalPrimitivePhysicalUtilization(
                        EconomicActivityKind.Forestry, 650),
                    new CanonicalPrimitivePhysicalUtilization(
                        EconomicActivityKind.Mining, 1000)
                });
        var pricing = new ExperimentalFreightTechnologyPricing(
            101, 7, 56, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 1_000, 1_000_000);
        var catalog = new FoodCommodityCatalog(new[] {
            new FoodCommodityDefinition(food, "food", 1),
            new FoodCommodityDefinition(wood, "wood", 1),
            new FoodCommodityDefinition(stone, "stone", 1)
        });
        var freight = CanonicalSimultaneousFreightReference.FromSites(
            f.Session, sites, new[] {
                new CanonicalEligibleFreightDiscoveryPolicy(
                    food, EconomicActivityKind.Agriculture, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad, catalog),
                new CanonicalEligibleFreightDiscoveryPolicy(
                    wood, EconomicActivityKind.Forestry, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad, catalog),
                new CanonicalEligibleFreightDiscoveryPolicy(
                    stone, EconomicActivityKind.Mining, pricing,
                    ExperimentalLandInfrastructureKind.StoneRoad, catalog)
            }, "VS56-PURCHASING", 1,
            "VS56-CALIBRATION", 1, "GA-VS56-NOMINAL-LAB");
        var policies = new[] {
            new CanonicalPrimitiveLocalPhysicalUsePolicy(
                EconomicActivityKind.Agriculture, 250),
            new CanonicalPrimitiveLocalPhysicalUsePolicy(
                EconomicActivityKind.Forestry, 300),
            new CanonicalPrimitiveLocalPhysicalUsePolicy(
                EconomicActivityKind.Mining, 400)
        };
        var candidates = freight.Offers.Where(x =>
            x.Commodity == food || x.Commodity == wood).ToArray();
        Assert.Equal(2, candidates.Length);
        var lots = candidates.Select(o => {
            var source = physical.Sources.Single(x =>
                x.Source == o.Source && x.Commodity == o.Commodity &&
                x.Activity == o.Activity);
            var q = Math.Min(1_000UL, source.ExperimentallyFeasibleRaw / 4UL);
            Assert.True(q > 0);
            return new CanonicalSimultaneousFreightLot(o, q);
        }).ToArray();
        var save = f.Session.ExportSaveJson();
        var hash = f.Session.CurrentHash;
        var times = new double[3];
        for (var sample = -1; sample < 3; sample++)
        {
            var watch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                var trial = CanonicalPrimitivePhysicalDispositionTrial
                    .FromCertifiedPlans(f.Session, sites, physical, freight,
                        policies, lots, f.Congestion);
                trial.VerifyIndependent();
                Assert.Equal(2, trial.Buyers.Count);
                Assert.Equal(3, trial.Commodities.Count);
                Assert.All(trial.Sources, x =>
                    Assert.Equal(new BigInteger(x.PotentialRaw),
                        (BigInteger)x.ProposedLocalUseRaw +
                            x.ProposedRemoteDeliveryRaw + x.TotalNotExtractedRaw));
                Assert.False(trial.CanAuthorizeSettlement);
            }
            watch.Stop();
            if (sample >= 0) times[sample] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(times);
        _output.WriteLine(
            $"CASE=vs95-local-remote;iterations={iterations};sources={physical.Sources.Count};" +
            $"median_ms={times[1]:F3};RESULT=PASS_GA_VS95_PHYSICAL_DISPOSITION");
        Assert.Equal(save, f.Session.ExportSaveJson());
        Assert.Equal(hash, f.Session.CurrentHash);
    }
}
