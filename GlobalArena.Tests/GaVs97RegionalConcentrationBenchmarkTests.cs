using System.Diagnostics;
using System.Numerics;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Stable, bounded benchmark of deterministic regional concentration on real
/// VS96 physical market data. 8/32/128 means iterations, NOT world size.
/// </summary>
public sealed class GaVs97RegionalConcentrationBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs97RegionalConcentrationBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void AuthenticatedRegionalSharesBenchmark(int iterations)
    {
        var fixture = GaVs96CertifiedFixture.Build();
        var absorption = GaVs96CertifiedFixture.Observe(fixture);
        var save = fixture.Fixture.Session.ExportSaveJson();
        var hash = fixture.Fixture.Session.CurrentHash;
        var measurements = new double[3];

        for (var sample = -1; sample < 3; sample++)
        {
            var began = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++)
            {
                var audit = CanonicalRegionalEconomicConcentrationAudit
                    .FromCertifiedAbsorption(
                        fixture.Fixture.Session, fixture.Sites, absorption);
                audit.VerifyIndependent();
                Assert.Equal(3, audit.Commodities.Count);
                Assert.Equal(absorption.Markets.Count, audit.Markets.Count);
                Assert.All(audit.Commodities, c =>
                {
                    Assert.Equal(c.TotalAbsorbedRaw, audit.Markets
                        .Where(x => x.Commodity == c.Commodity)
                        .Aggregate(BigInteger.Zero,
                            (sum, x) => sum + x.HypotheticallyAbsorbedRaw));
                    Assert.True(c.PhysicalHerfindahlIndex.CompareTo(
                        CanonicalRegionalShare.One) <= 0);
                });
                Assert.False(audit.DebitsHouseholdOrPopulationCash);
                Assert.False(audit.ConvertsIndicesToMoney);
                Assert.False(audit.PostsStateTreasuryRevenue);
                Assert.False(audit.CanAuthorizeSettlement);
            }
            var duration = Stopwatch.GetElapsedTime(began).TotalMilliseconds;
            if (sample >= 0) measurements[sample] = duration;
        }

        Array.Sort(measurements);
        _output.WriteLine(
            $"CASE=vs97-regional-concentration;iterations={iterations};" +
            $"markets={absorption.Markets.Count};" +
            $"median_ms={measurements[1]:F3};RESULT=PASS_GA_VS97_CONCENTRATION");
        Assert.Equal(save, fixture.Fixture.Session.ExportSaveJson());
        Assert.Equal(hash, fixture.Fixture.Session.CurrentHash);
    }
}
