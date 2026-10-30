using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs15TimedWorkerTransitTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent);

    [Fact]
    public void DepartureStopsProductionAndTravelersStayProvisionalConsumersUntilArrival()
    {
        var game = Start();
        var source = game.InitialTile;
        var destination = game.AdjacentWorkable(source)[0];
        var initialMoney = game.State.FoodCash!.InitialMoneySupplyMicrocredits;
        var previousHash = game.CurrentHash;

        var transit = game.OrderWorkerTransit(source, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Agriculture, 3UL);
        Assert.Equal(13U, game.CurrentHash.FormatVersion);
        Assert.NotEqual(previousHash, game.CurrentHash);
        Assert.Equal(2UL, transit.ArrivalTurn);
        Assert.Equal(0UL, transit.DepartureTurn);
        Assert.Equal(3UL, game.State.WorkersInTransit);
        Assert.Equal(5UL, game.WorkersAt(source));
        Assert.Equal(0UL, game.WorkersAt(destination));
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(4_000_000UL, game.FoodDemandAt(source));
        Assert.Empty(game.State.FoodCash.Receipts);

        var first = Assert.IsType<PhysicalEconomicCycle>(game.AdvanceMovementTurn());
        Assert.Equal(1UL, game.State.MovementTurn);
        Assert.Equal(3UL, game.State.WorkersInTransit);
        Assert.Equal(0UL, game.WorkersAt(destination));
        Assert.Equal(4_000_000UL,
            first.FoodConsumedRaw + first.FoodUnmetRaw);
        Assert.True(first.FoodConsumedRaw < 3_546_000UL);
        Assert.Equal(initialMoney, TotalMoney(game));

        var second = Assert.IsType<PhysicalEconomicCycle>(game.AdvanceMovementTurn());
        Assert.Equal(2UL, game.State.MovementTurn);
        Assert.Equal(0UL, game.State.WorkersInTransit);
        Assert.Empty(game.State.Transits);
        Assert.Equal(5UL, game.WorkersAt(source));
        Assert.Equal(3UL, game.WorkersAt(destination));
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(2_500_000UL, game.FoodDemandAt(source));
        Assert.Equal(1_500_000UL, game.FoodDemandAt(destination));
        Assert.Equal(4_000_000UL,
            second.FoodConsumedRaw + second.FoodUnmetRaw);
        Assert.True(game.State.Allocations.Any(x =>
            x.TileId == destination && x.Activity == EconomicActivityKind.Agriculture &&
            x.Workers == 3UL));
        Assert.Equal(initialMoney, TotalMoney(game));
        Assert.Equal(13U, game.CurrentHash.FormatVersion);
    }

    [Fact]
    public void TimedDepartureAndArrivalReplayAcrossSaveV13WithoutDuplication()
    {
        var game = Start();
        var destination = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        game.AdvanceMovementTurn();
        var before = game.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(before)!;
        Assert.Equal(13, envelope.Version);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Equal(before, restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(2UL, Assert.Single(restored.State.Transits).ArrivalTurn);
        for (var i = 0; i < 6; i++)
        {
            Assert.Equal(game.AdvanceMovementTurn(), restored.AdvanceMovementTurn());
            Assert.Equal(game.CurrentHash, restored.CurrentHash);
        }
        Assert.Equal(game.ExportSaveJson(), restored.ExportSaveJson());
        Assert.Equal(8UL, restored.State.TotalWorkers);
        Assert.Empty(restored.State.Transits);
        Assert.Equal(2UL, restored.WorkersAt(destination));
        Assert.Equal(320_000_000UL, TotalMoney(restored));
    }

    [Fact]
    public void RejectsIllegalOrInstantOrdersWithoutMutatingTheWorld()
    {
        var game = Start();
        var source = game.InitialTile;
        var neighbor = game.AdjacentWorkable(source)[0];
        var hash = game.CurrentHash;
        var save = game.ExportSaveJson();
        Assert.Throws<InvalidOperationException>(() =>
            game.MoveWorkers(source, neighbor,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2));
        Assert.Throws<InvalidOperationException>(() =>
            game.OrderWorkerTransit(source, source,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Agriculture, 2));
        Assert.Throws<InvalidOperationException>(() =>
            game.OrderWorkerTransit(source, neighbor,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Agriculture, 5));
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.Events);
        Assert.Equal(8UL, game.State.TotalWorkers);
    }

    [Fact]
    public void ExistingVs14CashSaveKeepsInstantMovementAndVersion12()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement);
        var neighbor = legacy.AdjacentWorkable(legacy.InitialTile)[0];
        legacy.MoveWorkers(legacy.InitialTile, neighbor,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 1);
        legacy.AdvanceMovementTurn();
        var json = legacy.ExportSaveJson();
        var reloaded = CanonicalPhysicalTacticalSession.RestoreSaveJson(json);
        Assert.Equal(12U, legacy.CurrentHash.FormatVersion);
        Assert.Equal(PhysicalWorkerMovementMode.LegacyInstant, reloaded.MovementMode);
        Assert.Equal(json, reloaded.ExportSaveJson());
        Assert.Equal(legacy.CurrentHash, reloaded.CurrentHash);
    }

    private static ulong TotalMoney(CanonicalPhysicalTacticalSession game) =>
        game.State.FoodCash!.Accounts.Aggregate(0UL, (sum, account) =>
            checked(sum + account.HouseholdMicrocredits +
                account.ProducerMicrocredits));
}
