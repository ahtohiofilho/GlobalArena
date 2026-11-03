using System.Diagnostics;
using GlobalArena.Simulation;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Real V20 whole-session save and restore, with the original physical
/// action journal. 8/32/128 are replay counts, not a world-size benchmark.
/// The opt-in treasury is strictly dormant and posts no monetary receipts.
/// </summary>
public sealed class GaVs102IntegratedTreasurySaveBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public GaVs102IntegratedTreasurySaveBenchmarkTests(
        ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(8)]
    [InlineData(32)]
    [InlineData(128)]
    public void FullSaveV20ReplaysHistoricalActionsAndHashBoundary(int iterations)
    {
        var source = GaVs102IntegratedTreasurySaveFixture.OpenLegacy();
        var legacy = source.ExportSaveJson();
        var previousActions = source.Events.ToArray();
        source.EnableDormantTreasuryReservation();
        source.AdvanceMovementTurn();
        var complete = source.ExportSaveJson();
        // Movement legitimately settles the historical food market. Compare
        // restored accounts with the exact V20 snapshot, not the pre-turn state.
        var savedFoodCashAccounts = source.FoodCash!.Accounts.ToArray();
        var currentHash = source.CurrentHash;
        var measured = new double[3];

        for (var sample = -1; sample < 3; sample++)
        {
            var started = Stopwatch.GetTimestamp();
            for(var i=0;i<iterations;i++)
            {
                var restored =
                    CanonicalPhysicalTacticalSession.RestoreSaveJson(complete);
                Assert.Equal(currentHash, restored.CurrentHash);
                Assert.Equal(complete, restored.ExportSaveJson());
                Assert.Equal(source.Events, restored.Events);
                Assert.Equal(0UL, restored.Game.Snapshot
                    .TreasuryReservation!.SpendableBalanceMinorUnits);
                Assert.Equal(0, restored.Game.Snapshot.TreasuryReservation
                    .MonetaryReceiptCount);
                Assert.Equal(savedFoodCashAccounts, restored.FoodCash!.Accounts);
            }
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (sample >= 0) measured[sample] = elapsed;
        }
        Array.Sort(measured);
        _output.WriteLine(
            $"CASE=vs102-full-v20-replay;iterations={iterations};" +
            $"historical_actions={previousActions.Length};" +
            $"median_ms={measured[1]:F3};" +
            "RESULT=PASS_GA_VS102_INTEGRATED_TREASURY_SAVE");
        Assert.Equal(savedFoodCashAccounts, source.FoodCash.Accounts);
        Assert.Equal(currentHash, source.CurrentHash);
        Assert.Equal(legacy, CanonicalPhysicalTacticalSession
            .RestoreSaveJson(legacy).ExportSaveJson());
    }
}
