using System.Diagnostics;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// 8/32/128 are repeat counts of dormant reservation/hash/codec only, not
/// universe size or a claim that currency issuance exists.
/// </summary>
public sealed class GaVs101DormantTreasuryPersistenceBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs101DormantTreasuryPersistenceBenchmarkTests(
        ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void ZeroTreasuryHashAndRoundTripBenchmark(int iterations)
    {
        var session = GaVs101DormantTreasuryFixture.StartSession();
        var baseline = session.Game.Snapshot;
        var canonicalSave = session.ExportSaveJson();
        var canonicalHash = session.CurrentHash;
        var cash = session.FoodCash!.Accounts.ToArray();
        var hasher = new CanonicalWorldStateHasher();
        var measured = new double[3];

        for (var pass = -1; pass < 3; pass++)
        {
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < iterations; i++)
            {
                var reserved = CanonicalDormantTreasuryPersistenceCodec.Reserve(
                    baseline);
                var encoded = CanonicalDormantTreasuryPersistenceCodec.Export(
                    baseline);
                var restored = CanonicalDormantTreasuryPersistenceCodec.Restore(
                    baseline, encoded);
                Assert.Equal(hasher.Compute(reserved), hasher.Compute(restored));
                Assert.Equal(0UL, restored.TreasuryReservation!
                    .SpendableBalanceMinorUnits);
                Assert.Equal(0, restored.TreasuryReservation.MonetaryReceiptCount);
                Assert.Equal(canonicalHash, hasher.Compute(baseline));
            }
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            if (pass >= 0) measured[pass] = elapsed;
        }
        Array.Sort(measured);
        _output.WriteLine(
            $"CASE=vs101-dormant-treasury-persistence;" +
            $"iterations={iterations};" +
            $"median_ms={measured[1]:F3};" +
            "RESULT=PASS_GA_VS101_DORMANT_TREASURY_HASH_AND_CODEC");
        Assert.Equal(canonicalSave, session.ExportSaveJson());
        Assert.Equal(canonicalHash, session.CurrentHash);
        Assert.Equal(cash, session.FoodCash.Accounts);
    }
}
