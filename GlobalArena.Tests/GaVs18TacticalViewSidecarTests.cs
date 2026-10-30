using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs18TacticalViewSidecarTests
{
    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent);

    private static TacticalCameraBookmark View(ulong focus = 1UL) =>
        new(focus, 2, true, true, 0.125, -0.075, 0.42, 30.0);

    [Fact]
    public void ViewBookmarkDoesNotModifyGameHashOrV13SaveAndReturnsExactly()
    {
        var game = Start();
        var destination = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        game.AdvanceMovementTurn();
        var save = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var version = JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(save)!.Version;
        Assert.Equal(13, version);

        var bookmark = View();
        var sidecar = TacticalViewSidecarCodec.Create(save, bookmark);
        Assert.True(TacticalViewSidecarCodec.TryRestore(save, sidecar, out var restored));
        Assert.Equal(bookmark, restored);
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(hash, reopened.CurrentHash);
        Assert.True(TacticalViewSidecarCodec.TryRestore(
            reopened.ExportSaveJson(), sidecar, out var cameraAgain));
        Assert.Equal(bookmark, cameraAgain);

        reopened.AdvanceMovementTurn();
        Assert.Empty(reopened.State.Transits);
        Assert.Equal(8UL, reopened.State.TotalWorkers);
        Assert.NotEqual(hash, reopened.CurrentHash);
    }

    [Fact]
    public void MissingMalformedAndWrongSaveCompanionNeverOverrideNewerGame()
    {
        var game = Start();
        var original = game.ExportSaveJson();
        var view = View();
        var companion = TacticalViewSidecarCodec.Create(original, view);
        Assert.False(TacticalViewSidecarCodec.TryRestore(original, null, out _));
        Assert.False(TacticalViewSidecarCodec.TryRestore(original, "{invalid", out _));
        Assert.False(TacticalViewSidecarCodec.TryRestore(
            original, new string('x', 16_385), out _));
        var anotherView = companion.Replace("\"Version\":1", "\"Version\":99");
        Assert.False(TacticalViewSidecarCodec.TryRestore(original, anotherView, out _));

        game.AdvanceMovementTurn();
        var newSave = game.ExportSaveJson();
        Assert.NotEqual(original, newSave);
        Assert.False(TacticalViewSidecarCodec.TryRestore(newSave, companion, out _));
        Assert.True(TacticalViewSidecarCodec.TryRestore(original, companion, out var restored));
        Assert.Equal(view, restored);
    }

    [Fact]
    public void RejectsNaNImpossibleZoomAndUnboundedCameraCoordinates()
    {
        var game = Start();
        var canonical = game.ExportSaveJson();
        Assert.Throws<ArgumentException>(() =>
            TacticalViewSidecarCodec.Create(canonical, View() with { Zoom = double.NaN }));
        Assert.Throws<ArgumentException>(() =>
            TacticalViewSidecarCodec.Create(canonical, View() with { Zoom = 0 }));
        Assert.Throws<ArgumentException>(() =>
            TacticalViewSidecarCodec.Create(canonical, View() with { CenterX = 150.0 }));
        Assert.Throws<ArgumentException>(() =>
            TacticalViewSidecarCodec.Create(canonical, View() with { FrameRings = 3 }));
        Assert.Throws<ArgumentException>(() =>
            TacticalViewSidecarCodec.Create(canonical, View() with { YawDegrees = 360.0 }));
        Assert.Throws<ArgumentException>(() =>
            TacticalViewSidecarCodec.Create(canonical, View() with { StrategicFocus = 0UL }));

        var sidecar = TacticalViewSidecarCodec.Create(canonical, View());
        var modified = sidecar.Replace("\"Zoom\":0.42", "\"Zoom\":9999");
        Assert.False(TacticalViewSidecarCodec.TryRestore(canonical, modified, out _));
        Assert.Equal(canonical, game.ExportSaveJson());
    }

    [Fact]
    public void CameraParametersAreExactlyReproducedIndependentlyOfV13Gameplay()
    {
        var save = Start().ExportSaveJson();
        var original = new TacticalCameraBookmark(
            15, 1, false, false, -0.256, 0.314, 1.22, 345.0);
        var serialized = TacticalViewSidecarCodec.Create(save, original);
        Assert.True(TacticalViewSidecarCodec.TryRestore(save, serialized, out var replay));
        Assert.Equal(original, replay);
        Assert.Equal(serialized, TacticalViewSidecarCodec.Create(save, replay!));
    }
}
