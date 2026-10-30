using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs19PoliticalFoundingTests
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
    public void FoundingIsExplicitAtomicAndDifferentFromWorkersStandingOnLand()
    {
        var game = Start();
        var before = game.Game.Snapshot;
        Assert.Empty(before.Civilizations.Records);
        Assert.Empty(before.Territory.Controls);
        Assert.Equal(13U, game.CurrentHash.FormatVersion);
        var initial = game.CurrentHash;
        var origin = game.InitialTile;
        var territory = game.DisplayBucket(origin);
        game.FoundFirstCivilization(origin, territory);

        Assert.True(game.HasFoundedCivilization);
        Assert.Equal(14U, game.CurrentHash.FormatVersion);
        Assert.NotEqual(initial, game.CurrentHash);
        Assert.Equal(before.Revision + 1UL, game.Game.Snapshot.Revision);
        var civ = Assert.Single(game.Game.Snapshot.Civilizations.Records);
        Assert.Equal(1UL, civ.Id.Value);
        Assert.Equal(territory, civ.StartCellId);
        var control = Assert.Single(game.Game.Snapshot.Territory.Controls);
        Assert.Equal(territory, control.StrategicCellId);
        Assert.Equal(civ.Id, control.Controller);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(0UL, game.State.MovementTurn);
        Assert.Equal(0UL, game.State.WorkerRevision);
        Assert.Equal("found", Assert.Single(game.Events).Kind);
        Assert.Equal(1UL, game.Game.Snapshot.Revision - before.Revision);
    }

    [Fact]
    public void FoundingSaveV14ReplaysExactlyWithMovementCashAndCameraSidecar()
    {
        var game = Start();
        var origin = game.InitialTile;
        var neighbor = game.AdjacentWorkable(origin)[0];
        game.OrderWorkerTransit(origin, neighbor,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        game.FoundFirstCivilization(origin, game.DisplayBucket(origin));
        game.AdvanceMovementTurn();

        var saved = game.ExportSaveJson();
        var envelope = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(saved)!;
        Assert.Equal(14, envelope.Version);
        var payload = JsonSerializer.Deserialize<CanonicalPhysicalSavePayload>(envelope.PayloadJson)!;
        Assert.NotNull(payload.Founding);
        Assert.Equal(1UL, payload.Founding.Civilization);
        Assert.Equal(game.DisplayBucket(origin).Value, payload.Founding.Territory);
        var camera = new TacticalCameraBookmark(
            game.DisplayBucket(origin).Value, 2, true, true,
            0.041, -0.056, 0.5, 15);
        var sidecar = TacticalViewSidecarCodec.Create(saved, camera);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(saved);
        Assert.Equal(game.CurrentHash, restored.CurrentHash);
        Assert.Equal(saved, restored.ExportSaveJson());
        Assert.True(TacticalViewSidecarCodec.TryRestore(saved, sidecar, out var retrieved));
        Assert.Equal(camera, retrieved);
        Assert.Equal(2UL, Assert.Single(restored.State.Transits).Workers);
        Assert.Equal(8UL, restored.State.TotalWorkers);

        for (var i = 0; i < 3; i++)
        {
            game.AdvanceMovementTurn();
            restored.AdvanceMovementTurn();
            Assert.Equal(game.CurrentHash, restored.CurrentHash);
        }
        Assert.Empty(restored.State.Transits);
        Assert.True(restored.Game.Snapshot.Territory.TryGetController(
            game.DisplayBucket(origin), out var owner));
        Assert.Equal(1UL, owner.Value);
        Assert.Equal(game.ExportSaveJson(), restored.ExportSaveJson());
    }

    [Fact]
    public void RefusesDuplicateFoundingAndUnoccupiedTileWithoutWorldMutation()
    {
        var game = Start();
        var origin = game.InitialTile;
        var neighbor = game.AdjacentWorkable(origin)[0];
        var territory = game.DisplayBucket(origin);
        var initial = game.CurrentHash;
        Assert.Throws<InvalidOperationException>(() =>
            game.FoundFirstCivilization(neighbor, game.DisplayBucket(neighbor)));
        Assert.Throws<ArgumentException>(() =>
            game.FoundFirstCivilization(origin, new StrategicCellId(
                checked((ulong)game.Game.GeneratedWorld.StrategicTopology.Cells.Count + 1UL))));
        Assert.Equal(initial, game.CurrentHash);
        Assert.Empty(game.Events);
        game.FoundFirstCivilization(origin, territory);
        var foundedHash = game.CurrentHash;
        var journal = game.Events;
        Assert.Throws<InvalidOperationException>(() =>
            game.FoundFirstCivilization(origin, territory));
        Assert.Equal(foundedHash, game.CurrentHash);
        Assert.Equal(journal, game.Events);
        Assert.Single(game.Game.Snapshot.Civilizations.Records);
        Assert.Single(game.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void V13ExistingWorkerSaveRemainsByteForByteCompatibleUntilFounding()
    {
        var game = Start();
        var target = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 2UL);
        game.AdvanceMovementTurn();
        var save = game.ExportSaveJson();
        var before = game.CurrentHash;
        var version = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version;
        Assert.Equal(13, version);
        var loaded = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, loaded.ExportSaveJson());
        Assert.Equal(before, loaded.CurrentHash);
        Assert.Empty(loaded.Game.Snapshot.Civilizations.Records);
        Assert.Empty(loaded.Game.Snapshot.Territory.Controls);
        loaded.FoundFirstCivilization(game.InitialTile, game.DisplayBucket(game.InitialTile));
        Assert.Equal(14, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(
            loaded.ExportSaveJson())!.Version);
    }
}
