using System.Diagnostics;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Bounded replay benchmark of a real VS92..99 certified economic cycle.
/// 8/32/128 are iterations, not world size. No nominal performance gate.
/// </summary>
public sealed class GaVs100TreasuryGovernanceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs100TreasuryGovernanceBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void IdempotentNonpostedTreasuryRehearsal(int iterations)
    {
        var data = GaVs100TreasuryDryRunFixture.Build();
        var session = data.Source.Source.Physical.Fixture.Session;
        var before = session.ExportSaveJson();
        var hash = session.CurrentHash;
        var accounts = session.FoodCash!.Accounts.ToArray();
        var measures = new double[3];

        for (var sample = -1; sample < 3; sample++)
        {
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++)
            {
                var audit = GaVs100TreasuryDryRunFixture.Preview(data);
                audit.VerifyIndependent();
                var first = audit.StageInLaboratory();
                Assert.Same(first, audit.StageInLaboratory(first));
                Assert.Equal(
                    data.Proposed99.ProposedPublicNonpostedTotal,
                    first.ProposedNonpostedTotal);
                Assert.False(audit.DebitsPopulationMoney);
                Assert.False(audit.PostsRealStateTreasuryRevenue);
                Assert.False(audit.CanAuthorizeSettlement);
            }
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (sample >= 0) measures[sample] = elapsed;
        }
        Array.Sort(measures);
        _output.WriteLine(
            $"CASE=vs100-idempotent-treasury-dryrun;" +
            $"iterations={iterations};" +
            $"institutions={data.Proposed99.Institutions.Count};" +
            $"median_ms={measures[1]:F3};" +
            "RESULT=PASS_GA_VS100_TREASURY_REPLAY");
        Assert.Equal(before, session.ExportSaveJson());
        Assert.Equal(hash, session.CurrentHash);
        Assert.Equal(accounts, session.FoodCash.Accounts);
    }
}
