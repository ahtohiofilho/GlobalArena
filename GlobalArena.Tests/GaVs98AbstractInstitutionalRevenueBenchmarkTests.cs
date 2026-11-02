using System.Diagnostics;
using System.Numerics;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Bounded real-cycle, real-road repeatability benchmark (8/32/128
/// independent reconstruction iterations). No cash, performance promises,
/// or false claim of whole-world economic settlement.
/// </summary>
public sealed class GaVs98AbstractInstitutionalRevenueBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs98AbstractInstitutionalRevenueBenchmarkTests(
        ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void AbstractSourceInstitutionalRevenueBenchmark(int iterations)
    {
        var d = GaVs98InstitutionalRevenueFixture.Build();
        var session = d.Physical.Fixture.Session;
        var save = session.ExportSaveJson();
        var hash = session.CurrentHash;
        var accountBefore = session.FoodCash!.Accounts.ToArray();
        var times = new double[3];
        for (var sample = -1; sample < 3; sample++)
        {
            var begin = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++)
            {
                var a = GaVs98InstitutionalRevenueFixture.Preview(d);
                a.VerifyIndependent();
                Assert.Equal(3, a.Commodities.Count);
                Assert.False(a.HasBuyerBudgetOrDebit);
                Assert.False(a.PostsSpendableStateTreasuryCredits);
                Assert.False(a.CanAuthorizeSettlement);
                foreach (var c in a.Commodities)
                {
                    var sources = a.SourceMarkets.Where(x =>
                        x.Commodity == c.Commodity);
                    Assert.Equal(c.HypotheticallyAbsorbedPhysicalRaw,
                        sources.Aggregate(BigInteger.Zero,
                            (sum, x) => sum + x.HypotheticalPhysicalRaw));
                    var institutions = a.Institutions.Where(x =>
                        x.Commodity == c.Commodity);
                    var sum = institutions.Aggregate(
                        CanonicalEndogenousPriceIndex.Zero,
                        (total, x) => CanonicalEndogenousPriceIndex.Add(
                            total, x.GentleRelativeGrossIndex));
                    Assert.Equal(0, sum.CompareTo(
                        c.TotalGentleRelativeGrossIndex));
                }
            }
            var elapsed = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
            if (sample >= 0) times[sample] = elapsed;
        }
        Array.Sort(times);
        _output.WriteLine(
            $"CASE=vs98-abstract-institutional-revenue;" +
            $"iterations={iterations};sources={d.Physical.Disposition.Sources.Count};" +
            $"median_ms={times[1]:F3};" +
            "RESULT=PASS_GA_VS98_ABSTRACT_REVENUE");
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(accountBefore, session.FoodCash.Accounts);
    }
}
