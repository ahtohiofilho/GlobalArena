using System.Diagnostics;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// VS99 audit over a *real* closed cycle and authenticated freight, but
/// conversion rates are explicitly LAB policy, NOT ratified currency.
/// 8/32/128 are replay counts, not a world-size scalability claim.
/// </summary>
public sealed class GaVs99AbstractCurrencyCalibrationBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs99AbstractCurrencyCalibrationBenchmarkTests(
        ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void ReviewedNonpostedStateProposalBenchmark(int iterations)
    {
        var d = GaVs99CalibratedTreasuryFixture.Build();
        var session = d.Source.Physical.Fixture.Session;
        var save = session.ExportSaveJson();
        var worldHash = session.CurrentHash;
        var accounts = session.FoodCash!.Accounts.ToArray();
        var measures = new double[3];

        for (var pass = -1; pass < 3; pass++)
        {
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++)
            {
                var report = GaVs99CalibratedTreasuryFixture.Preview(d);
                report.VerifyIndependent();
                Assert.Equal(3, report.Commodities.Count);
                Assert.Equal(d.Abstract.Institutions.Count,
                    report.Institutions.Count);
                Assert.False(report.DebitsPopulationOrBuyerCash);
                Assert.False(report.PostsToStateTreasury);
                Assert.False(report.CanAuthorizeRealSettlement);
                var gross = report.Commodities.Aggregate(
                    CanonicalCalibratedMonetaryAmount.Zero,
                    (sum, c) => CanonicalCalibratedMonetaryAmount.Add(
                        sum, c.GrossNonpostedAmount));
                var publicTotal = report.Commodities.Aggregate(
                    CanonicalCalibratedMonetaryAmount.Zero,
                    (sum, c) => CanonicalCalibratedMonetaryAmount.Add(
                        sum, c.PublicNonpostedAmount));
                Assert.Equal(gross,
                    report.ProposedAllInstitutionsNonpostedTotal);
                Assert.Equal(publicTotal,
                    report.ProposedPublicNonpostedTotal);
            }
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (pass >= 0) measures[pass] = elapsed;
        }

        Array.Sort(measures);
        _output.WriteLine(
            $"CASE=vs99-reviewed-abstract-currency;" +
            $"iterations={iterations};institutions={d.Abstract.Institutions.Count};" +
            $"median_ms={measures[1]:F3};" +
            "RESULT=PASS_GA_VS99_ABSTRACT_CURRENCY");
        Assert.Equal(save, session.ExportSaveJson());
        Assert.Equal(worldHash, session.CurrentHash);
        Assert.Equal(accounts, session.FoodCash.Accounts);
    }
}
