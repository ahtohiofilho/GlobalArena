using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs14PhysicalFoodCashSettlementTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement);

    [Fact]
    public void FirstCycleMakesARealAtomicFoodPurchaseWithNoMonetaryCreation()
    {
        var game = Start();
        var before = game.Game.Snapshot;
        Assert.Equal(12U, game.CurrentHash.FormatVersion);
        Assert.Equal(1UL, before.Revision);
        var initial = Assert.IsType<PhysicalFoodCashMarketState>(game.State.FoodCash);
        Assert.Equal(320_000_000UL, initial.InitialMoneySupplyMicrocredits);
        var account = Assert.Single(initial.Accounts);
        Assert.Equal(320_000_000UL, account.HouseholdMicrocredits);
        Assert.Equal(0UL, account.ProducerMicrocredits);
        Assert.Empty(initial.Receipts);
        Assert.Empty(game.State.Stocks);
        Assert.Empty(game.State.Cycles);

        var cycle = Assert.IsType<PhysicalEconomicCycle>(game.AdvanceMovementTurn());
        var ledger = Assert.IsType<PhysicalFoodCashMarketState>(game.State.FoodCash);
        var paid = Assert.Single(ledger.Receipts);
        var after = Assert.Single(ledger.Accounts);
        Assert.Equal(1UL, paid.MovementTurn);
        Assert.Equal(game.InitialTile, paid.Tile);
        Assert.True(paid.FoodRaw > 0UL);
        Assert.True(paid.PriceMicrocreditsPerUnit > 0UL);
        Assert.True(paid.PaidMicrocredits > 0UL);
        Assert.Equal(paid.FoodRaw, cycle.FoodConsumedRaw);
        Assert.Equal(4_000_000UL - paid.FoodRaw, cycle.FoodUnmetRaw);
        Assert.Equal(320_000_000UL - paid.PaidMicrocredits,
            after.HouseholdMicrocredits);
        Assert.Equal(paid.PaidMicrocredits, after.ProducerMicrocredits);
        Assert.Equal(320_000_000UL,
            after.HouseholdMicrocredits + after.ProducerMicrocredits);
        Assert.Equal(2UL, game.Game.Snapshot.Revision);
        Assert.NotEqual(before, game.Game.Snapshot);
        Assert.Equal(12U, game.CurrentHash.FormatVersion);
    }

    [Fact]
    public void RelocatingWorkersTransfersOnlyExistingHouseholdCash()
    {
        var game = Start();
        var origin = game.InitialTile;
        var destination = game.AdjacentWorkable(origin)[0];
        game.MoveWorkers(origin, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);
        var state = game.State;
        Assert.Equal(8UL, state.TotalWorkers);
        var accounts = Assert.IsType<PhysicalFoodCashMarketState>(state.FoodCash);
        Assert.Equal(200_000_000UL, accounts.At(origin).HouseholdMicrocredits);
        Assert.Equal(120_000_000UL, accounts.At(destination).HouseholdMicrocredits);
        Assert.Equal(320_000_000UL, accounts.InitialMoneySupplyMicrocredits);
        var cycle = Assert.IsType<PhysicalEconomicCycle>(game.AdvanceMovementTurn());
        Assert.Equal(4_000_000UL, cycle.FoodConsumedRaw + cycle.FoodUnmetRaw);
        var log = Assert.IsType<PhysicalFoodCashMarketState>(game.State.FoodCash);
        Assert.DoesNotContain(log.Receipts, r => r.Tile == destination);
        Assert.True(log.At(destination).HouseholdMicrocredits == 120_000_000UL);
        Assert.Equal(320_000_000UL,
            log.Accounts.Aggregate(0UL, (sum, a) =>
                checked(sum + a.HouseholdMicrocredits + a.ProducerMicrocredits)));
    }

    [Fact]
    public void MultipleCyclesAndSaveV12ReplayKeepFoodAndCashBitExact()
    {
        var game = Start();
        var origin = game.InitialTile;
        var target = game.AdjacentWorkable(origin)[0];
        for (var i = 0; i < 3; i++) game.AdvanceMovementTurn();
        game.MoveWorkers(origin, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        for (var i = 0; i < 5; i++) game.AdvanceMovementTurn();
        var save = game.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!;
        Assert.Equal(12, envelope.Version);
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(PhysicalFoodMarketMode.LocalCashSettlement, reopened.MarketMode);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(game.State.FoodCash!.Receipts,
            reopened.State.FoodCash!.Receipts);
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(game.AdvanceMovementTurn(), reopened.AdvanceMovementTurn());
        }
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(game.ExportSaveJson(), reopened.ExportSaveJson());
        Assert.Equal(320_000_000UL,
            reopened.State.FoodCash!.Accounts.Aggregate(0UL, (sum, a) =>
                checked(sum + a.HouseholdMicrocredits + a.ProducerMicrocredits)));
    }

    [Fact]
    public void ForgedBalancedFinancialSummaryIsRejectedEvenWithNewChecksum()
    {
        var game = Start();
        game.AdvanceMovementTurn();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            game.ExportSaveJson())!;
        var payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            envelope.PayloadJson)!;
        var market = payload.FoodCash!;
        var changed = market.Accounts.Select(x => x with
        {
            Household = x.Household + 1UL,
            Producer = x.Producer - 1UL
        }).ToArray();
        var forgedPayload = JsonSerializer.Serialize(payload with
        {
            FoodCash = market with { Accounts = changed }
        });
        var forgedEnvelope = JsonSerializer.Serialize(new CanonicalPhysicalSaveEnvelope(
            envelope.Version, forgedPayload,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(forgedPayload)))));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalPhysicalTacticalSession.RestoreSaveJson(forgedEnvelope));
    }

    [Fact]
    public void ExistingV11GeographySessionRetainsDirectConsumptionAndExactReplay()
    {
        var old = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C);
        Assert.Null(old.State.FoodCash);
        Assert.Equal(11U, old.CurrentHash.FormatVersion);
        for (var i = 0; i < 7; i++) old.AdvanceMovementTurn();
        var save = old.ExportSaveJson();
        Assert.Equal(11, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version);
        Assert.Equal(save, CanonicalPhysicalTacticalSession.RestoreSaveJson(save).ExportSaveJson());
    }
}
