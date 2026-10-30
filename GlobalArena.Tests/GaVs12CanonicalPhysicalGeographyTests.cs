using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs12CanonicalPhysicalGeographyTests
{
    private static CanonicalPhysicalTacticalSession StartGeographic() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C);

    [Fact]
    public void EveryFineTileMatchesExactlyTheBiomeDisplayedByThe01CRenderer()
    {
        var game = StartGeographic();
        var physical = game.CanonicalTerrain;
        Assert.NotNull(physical);
        Assert.Equal(11U, game.CurrentHash.FormatVersion);
        Assert.Equal(35, physical.LandTargetPercent);
        Assert.Equal(game.FineWorld.StrategicTopology.Cells.Count, physical.Biomes.Count);

        var terra = 0;
        var oceano = 0;
        foreach (var cell in game.FineWorld.StrategicTopology.Cells)
        {
            var displayed = physical.Biomes[checked((int)cell.Id.Value - 1)];
            var eligible = !displayed.IsWater &&
                displayed.VisibleBiome != ExperimentalProceduralBiomeClassifier.Biome.Ice;
            Assert.Equal(eligible, game.IsPhysicalWorkableLand(cell.Id));
            if (displayed.IsWater) oceano++;
            else terra++;
        }
        Assert.True(terra > 0);
        Assert.True(oceano > 0);
        Assert.False(physical.Biomes[checked((int)game.InitialTile.FineStrategicCellId.Value - 1)].IsWater);
        Assert.True(game.IsPhysicalWorkableLand(game.InitialTile.FineStrategicCellId));
    }

    [Fact]
    public void ProductionAndLegalNeighborsReferenceOnlyTheDisplayed01CTerrain()
    {
        var game = StartGeographic();
        var model = new CanonicalTacticalPhysicalGeography(game.FineWorld, 20261012UL);
        Assert.Equal(model.InitialWorkableTile, game.InitialTile.FineStrategicCellId);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(PhysicalTacticalGeographyMode.Canonical01C, game.State.GeographyMode);
        Assert.All(game.AdjacentWorkable(game.InitialTile), tile =>
        {
            Assert.True(game.IsPhysicalWorkableLand(tile.FineStrategicCellId));
            Assert.False(game.CanonicalTerrain!.Biomes[
                checked((int)tile.FineStrategicCellId.Value - 1)].IsWater);
        });
        foreach (var cell in game.FineWorld.StrategicTopology.Cells)
        {
            var farmland = model.Productivity(cell.Id, EconomicActivityKind.Agriculture);
            var timber = model.Productivity(cell.Id, EconomicActivityKind.Forestry);
            var stone = model.Productivity(cell.Id, EconomicActivityKind.Mining);
            if (!game.IsPhysicalWorkableLand(cell.Id))
                Assert.Equal((0L, 0L, 0L), (farmland, timber, stone));
            else
            {
                Assert.True(farmland > 0L);
                Assert.True(stone > 0L);
            }
        }
    }

    [Fact]
    public void EconomicCycleUsesCanonical01CRatesAndConservesWorkers()
    {
        var game = StartGeographic();
        var location = game.InitialTile;
        var model = new CanonicalTacticalPhysicalGeography(game.FineWorld, 20261012UL);
        var expectedProduced = checked(
            4UL * (ulong)model.Productivity(location.FineStrategicCellId, EconomicActivityKind.Agriculture) +
            2UL * (ulong)model.Productivity(location.FineStrategicCellId, EconomicActivityKind.Forestry) +
            2UL * (ulong)model.Productivity(location.FineStrategicCellId, EconomicActivityKind.Mining));

        var first = game.AdvanceMovementTurn();
        Assert.NotNull(first);
        Assert.Equal(expectedProduced, first.ProducedRaw);
        Assert.Equal(1UL, game.State.MovementTurn);
        Assert.Single(game.State.Cycles);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(11U, game.CurrentHash.FormatVersion);
        Assert.Equal(4UL, game.State.Allocations.First(
            x => x.Activity == EconomicActivityKind.Agriculture).Workers);
    }

    [Fact]
    public void SaveV11ReopensWithoutReinterpretingHistoricalV9OrV10()
    {
        var modern = StartGeographic();
        var second = modern.AdjacentWorkable(modern.InitialTile).First();
        modern.MoveWorkers(modern.InitialTile, second,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        for (var i = 0; i < 34; i++) modern.AdvanceMovementTurn();
        var raw = modern.ExportSaveJson();
        Assert.Equal(11, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(raw)!.Version);
        var restored = CanonicalPhysicalTacticalSession.RestoreSaveJson(raw);
        Assert.Equal(PhysicalTacticalGeographyMode.Canonical01C, restored.GeographyMode);
        Assert.Equal(11U, restored.CurrentHash.FormatVersion);
        Assert.Equal(raw, restored.ExportSaveJson());
        Assert.Equal(modern.CurrentHash, restored.CurrentHash);
        Assert.Equal(modern.InitialTile, restored.InitialTile);
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(modern.AdvanceMovementTurn(), restored.AdvanceMovementTurn());
        }
        Assert.Equal(modern.CurrentHash, restored.CurrentHash);

        var old = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty);
        for (var i = 0; i < 4; i++) old.AdvanceMovementTurn();
        var v10 = old.ExportSaveJson();
        Assert.Equal(10, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(v10)!.Version);
        var legacyReload = CanonicalPhysicalTacticalSession.RestoreSaveJson(v10);
        Assert.Equal(PhysicalTacticalGeographyMode.LegacyV1, legacyReload.GeographyMode);
        Assert.Null(legacyReload.CanonicalTerrain);
        Assert.Equal(old.InitialTile, legacyReload.InitialTile);
        Assert.Equal(10U, legacyReload.CurrentHash.FormatVersion);
        Assert.Equal(v10, legacyReload.ExportSaveJson());
    }

    [Fact]
    public void NewTerrainModeNeverChangesV9LegacySaveFormat()
    {
        var legacy = CanonicalPhysicalTacticalSession.Start();
        Assert.Equal(PhysicalTacticalGeographyMode.LegacyV1, legacy.GeographyMode);
        legacy.AdvanceMovementTurn();
        var encoded = legacy.ExportSaveJson();
        Assert.Equal(9, JsonSerializer.Deserialize<CanonicalPhysicalSaveEnvelope>(encoded)!.Version);
        Assert.Equal(9U, CanonicalPhysicalTacticalSession.RestoreSaveJson(encoded)
            .CurrentHash.FormatVersion);
    }
}
