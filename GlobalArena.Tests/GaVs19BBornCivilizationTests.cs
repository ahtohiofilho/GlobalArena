using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs19BBornCivilizationTests
{
    private static CanonicalPhysicalTacticalSession Start(bool born) =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            born ? PhysicalCivilizationIdentityMode.BornWithPopulation
                 : PhysicalCivilizationIdentityMode.LegacyUnattributed);

    [Fact]
    public void CivilizationAndWorkersAreBornTogetherOnInitialWorldRevision()
    {
        var game = Start(born: true);
        var world = game.Game.Snapshot;
        Assert.Equal(1UL, world.Revision);
        Assert.Equal(0UL, game.State.MovementTurn);
        Assert.Empty(game.Events);
        Assert.Single(world.Civilizations.Records);
        Assert.Single(world.Civilizations.Civilizations);
        Assert.Equal(1UL, world.Civilizations.Records[0].Id.Value);
        Assert.True(world.Civilizations.Records[0].IsMaterialized);
        Assert.Equal(game.DisplayBucket(game.InitialTile),
            world.Civilizations.Records[0].StartCellId);
        Assert.Empty(world.Territory.Controls);
        Assert.Empty(world.Warfare.Units);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
        Assert.Equal(320_000_000UL,
            game.State.FoodCash!.InitialMoneySupplyMicrocredits);

        var economic = CivilizationalEconomicAttributionProjection.FromSession(game);
        Assert.Equal(1UL, economic.Civilization.Value);
        Assert.Equal(8UL, economic.TotalWorkers);
        Assert.Equal(0UL, economic.WorkersInTransit);
        Assert.Equal(3, economic.Workforce.Count);
        Assert.Single(economic.Accounts);
        Assert.Equal(320_000_000UL, economic.HouseholdMoney);
        Assert.Equal(0UL, economic.ProducerMoney);
        Assert.Equal(320_000_000UL, economic.TotalMoney);
        Assert.Empty(economic.Stocks);
        Assert.All(economic.Workforce,
            group => Assert.Equal(economic.Civilization, group.Civilization));
        Assert.All(economic.Accounts,
            account => Assert.Equal(economic.Civilization, account.Civilization));
    }

    [Fact]
    public void MovementAndEconomicSettlementRemainInsideOriginalCommunity()
    {
        var game = Start(born: true);
        var target = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        var enRoute = CivilizationalEconomicAttributionProjection.FromSession(game);
        Assert.Equal(2UL, enRoute.WorkersInTransit);
        Assert.Equal(8UL, enRoute.TotalWorkers);
        Assert.Contains(enRoute.Workforce,
            group => group.InTransit && group.Workers == 2UL &&
                group.Tile == game.InitialTile);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        game.AdvanceMovementTurn();
        var settled = CivilizationalEconomicAttributionProjection.FromSession(game);
        Assert.Equal(8UL, settled.TotalWorkers);
        Assert.Equal(2UL, settled.WorkersInTransit);
        Assert.NotEmpty(settled.Stocks);
        Assert.Equal(320_000_000UL, settled.TotalMoney);
        Assert.True(settled.ProducerMoney > 0UL);
        Assert.All(settled.Stocks,
            stock => Assert.Equal(settled.Civilization, stock.Civilization));
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        game.AdvanceMovementTurn();
        var landed = CivilizationalEconomicAttributionProjection.FromSession(game);
        Assert.Equal(0UL, landed.WorkersInTransit);
        Assert.Equal(8UL, landed.TotalWorkers);
        Assert.Equal(320_000_000UL, landed.TotalMoney);
        Assert.Equal(1UL, game.Game.Snapshot.Civilizations.Civilizations.Single().Value);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        Assert.Contains(landed.Workforce,
            group => !group.InTransit && group.Tile == target &&
                group.Activity == EconomicActivityKind.Forestry &&
                group.Workers == 2UL);
    }

    [Fact]
    public void BornIdentityV14ReplaysWithUnchangedCameraSidecarAndNoFoundingEvent()
    {
        var game = Start(born: true);
        var target = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 2UL);
        game.AdvanceMovementTurn();
        var save = game.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!;
        var payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            envelope.PayloadJson)!;
        Assert.Equal(14, envelope.Version);
        Assert.NotNull(payload.InitialCivilization);
        Assert.Equal(1UL, payload.InitialCivilization.Identity);
        Assert.Equal(game.DisplayBucket(game.InitialTile).Value,
            payload.InitialCivilization.PopulationOrigin);
        Assert.DoesNotContain(game.Events, action => action.Kind == "found");
        var camera = new TacticalCameraBookmark(
            game.DisplayBucket(game.InitialTile).Value, 2, true, true,
            0.10, -0.20, 0.42, 30.0);
        var companion = TacticalViewSidecarCodec.Create(save, camera);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.True(TacticalViewSidecarCodec.TryRestore(
            restored.ExportSaveJson(), companion, out var cameraAgain));
        Assert.Equal(camera, cameraAgain);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(game.AdvanceMovementTurn(), restored.AdvanceMovementTurn());
            Assert.Equal(game.CurrentHash, restored.CurrentHash);
            Assert.Equal(320_000_000UL,
                CivilizationalEconomicAttributionProjection
                    .FromSession(restored).TotalMoney);
        }
        Assert.Equal(game.ExportSaveJson(), restored.ExportSaveJson());
        Assert.Empty(restored.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void OriginalV13SaveAndHashRemainByteCompatibleWithoutSilentCivilizationCreation()
    {
        var legacy = Start(born: false);
        var target = legacy.AdjacentWorkable(legacy.InitialTile)[0];
        legacy.OrderWorkerTransit(legacy.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 2UL);
        legacy.AdvanceMovementTurn();
        var save = legacy.ExportSaveJson();
        var hash = legacy.CurrentHash;
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!;
        Assert.Equal(13, envelope.Version);
        Assert.Equal(13U, hash.FormatVersion);
        Assert.Null(JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(
            envelope.PayloadJson)!.InitialCivilization);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, restored.ExportSaveJson());
        Assert.Equal(hash, restored.CurrentHash);
        Assert.Empty(restored.Game.Snapshot.Civilizations.Civilizations);
        Assert.Throws<InvalidOperationException>(() =>
            CivilizationalEconomicAttributionProjection.FromSession(restored));
        Assert.Throws<ArgumentException>(() =>
            CanonicalPhysicalTacticalSession.Start(
                20261012UL, new GoldbergParameters(3, 0),
                new GoldbergParameters(18, 0),
                PhysicalEconomicCadenceMode.AdaptiveThirty,
                PhysicalTacticalGeographyMode.Canonical01C,
                PhysicalFoodMarketMode.LocalCashSettlement,
                PhysicalWorkerMovementMode.LegacyInstant,
                PhysicalCivilizationIdentityMode.BornWithPopulation));
    }
}
