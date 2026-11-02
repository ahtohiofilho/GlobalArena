using System.Diagnostics;
using System.Numerics;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Bounded repeatability benchmark on a certified live cycle and real freight
/// routes. It is NOT a world-scale throughput claim or performance promise.
/// </summary>
public sealed class GaVs96SimultaneousMarketAbsorptionBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs96SimultaneousMarketAbsorptionBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void CombinedMarketAbsorptionBenchmark(int iterations)
    {
        var data = GaVs96CertifiedFixture.Build();
        var save = data.Fixture.Session.ExportSaveJson();
        var hash = data.Fixture.Session.CurrentHash;
        var times = new double[3];

        for (var sample = -1; sample < 3; sample++)
        {
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++)
            {
                var a = GaVs96CertifiedFixture.Observe(data);
                a.VerifyIndependent();
                Assert.Equal(data.Sites.Flows.DemandFlows.Count, a.Markets.Count);
                Assert.Equal(3, a.Commodities.Count);
                Assert.False(a.CanAuthorizeSettlement);
                Assert.All(a.Markets, m =>
                {
                    Assert.Equal(m.TotalHypotheticallyAbsorbedRaw,
                        m.ProposedLocalAbsorptionRaw +
                            m.ProposedRemoteAbsorptionRaw);
                    Assert.True(m.GentleGrossRevenueIndex.CompareTo(
                        m.SteepGrossRevenueIndex) >= 0);
                });
                Assert.All(a.Commodities, c =>
                    Assert.Equal(c.PotentialRaw,
                        c.LocallyAbsorbedRaw +
                            c.RemotelyAbsorbedRaw + c.NeverExtractedRaw));
            }
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (sample >= 0) times[sample] = elapsed;
        }

        Array.Sort(times);
        _output.WriteLine(
            $"CASE=vs96-simultaneous-market-absorption;" +
            $"iterations={iterations};markets={data.Sites.Flows.DemandFlows.Count};" +
            $"median_ms={times[1]:F3};" +
            "RESULT=PASS_GA_VS96_SIMULTANEOUS_MARKET_ABSORPTION");
        Assert.Equal(save, data.Fixture.Session.ExportSaveJson());
        Assert.Equal(hash, data.Fixture.Session.CurrentHash);
    }
}
