using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs26AdjacentWorkSurveyTests
{
    private static CanonicalPhysicalTacticalSession Start(bool born = true) =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            born ? PhysicalCivilizationIdentityMode.BornWithPopulation
                : PhysicalCivilizationIdentityMode.LegacyUnattributed);

    [Theory]
    [InlineData(EconomicActivityKind.Agriculture)]
    [InlineData(EconomicActivityKind.Forestry)]
    [InlineData(EconomicActivityKind.Mining)]
    public void ScoutingIsDeterministicAndUsesOnlyCanonicalGeographicYield(
        EconomicActivityKind targetJob)
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var journal = game.Events.ToArray();

        var result = PhysicalAdjacentWorkSurveyProjection.Survey(
            game, game.InitialTile, targetJob);

        Assert.Equal(game.InitialTile, result.Origin);
        Assert.Equal(targetJob, result.ProposedJob);
        Assert.Equal(8UL, result.CurrentTileResidents);
        Assert.Equal(game.PrimitiveYieldAt(game.InitialTile, targetJob),
            result.CurrentTileYieldRaw);
        Assert.NotEmpty(result.Neighbours);
        Assert.Equal(game.AdjacentWorkable(game.InitialTile).Count, result.Neighbours.Count);
        var expectedOrder = result.Neighbours
            .OrderByDescending(x => x.YieldFor(targetJob))
            .ThenBy(x => x.Tile.FineStrategicCellId.Value)
            .ToArray();
        Assert.Equal(expectedOrder, result.Neighbours);
        Assert.All(result.Neighbours, item =>
        {
            Assert.True(game.IsPhysicalWorkableLand(item.Tile.FineStrategicCellId));
            Assert.Equal(game.PrimitiveYieldAt(item.Tile, EconomicActivityKind.Agriculture),
                item.FoodYieldPerWorkerRaw);
            Assert.Equal(game.PrimitiveYieldAt(item.Tile, EconomicActivityKind.Forestry),
                item.WoodYieldPerWorkerRaw);
            Assert.Equal(game.PrimitiveYieldAt(item.Tile, EconomicActivityKind.Mining),
                item.StoneYieldPerWorkerRaw);
            Assert.Equal(0UL, item.ResidentWorkers);
            Assert.Equal(0UL, item.WorkersArriving);
        });
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(journal, game.Events);
        Assert.Equal(0UL, game.State.MovementTurn);
        Assert.Empty(game.State.Cycles);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Equal(result.Neighbours,
            PhysicalAdjacentWorkSurveyProjection.Survey(
                reopened, reopened.InitialTile, targetJob).Neighbours);
    }

    [Fact]
    public void ExistingSiteAndTravellersAreVisibleButSurveyNeverMovesAnyone()
    {
        var game = Start();
        var origin = game.InitialTile;
        var target = game.AdjacentWorkable(origin)[0];
        game.OrderWorkerTransit(origin, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        var before = game.ExportSaveJson();
        var stateHash = game.CurrentHash;
        var whileTravelling = PhysicalAdjacentWorkSurveyProjection.Survey(
            game, origin, EconomicActivityKind.Forestry);
        Assert.Equal(2UL, Assert.Single(whileTravelling.Neighbours,
            site => site.Tile == target).WorkersArriving);
        Assert.Equal(0UL, Assert.Single(whileTravelling.Neighbours,
            site => site.Tile == target).ResidentWorkers);

        game.AdvanceMovementTurn();
        game.AdvanceMovementTurn();
        var afterArrival = game.ExportSaveJson();
        var afterArrivalHash = game.CurrentHash;
        var after = PhysicalAdjacentWorkSurveyProjection.Survey(
            game, origin, EconomicActivityKind.Forestry);
        Assert.Equal(0UL, Assert.Single(after.Neighbours,
            site => site.Tile == target).WorkersArriving);
        Assert.Equal(2UL, Assert.Single(after.Neighbours,
            site => site.Tile == target).ResidentWorkers);
        Assert.Equal(afterArrival, game.ExportSaveJson());
        Assert.Equal(afterArrivalHash, game.CurrentHash);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Equal(stateHash, reopened.CurrentHash);
        Assert.Equal(2UL, Assert.Single(
            PhysicalAdjacentWorkSurveyProjection.Survey(
                reopened, origin, EconomicActivityKind.Forestry).Neighbours,
                site => site.Tile == target).WorkersArriving);
    }

    [Fact]
    public void LegacyAndBornWithPopulationReadSamePhysicalGeography()
    {
        var born = Start();
        var legacy = Start(born: false);
        var a = PhysicalAdjacentWorkSurveyProjection.Survey(
            born, born.InitialTile, EconomicActivityKind.Agriculture);
        var b = PhysicalAdjacentWorkSurveyProjection.Survey(
            legacy, legacy.InitialTile, EconomicActivityKind.Agriculture);
        Assert.Equal(a.Neighbours, b.Neighbours);
        Assert.Equal(14U, born.CurrentHash.FormatVersion);
        Assert.Equal(13U, legacy.CurrentHash.FormatVersion);
        Assert.Empty(born.Events);
        Assert.Empty(legacy.Events);
    }

    [Fact]
    public void InvalidTileAndProfessionAreRejectedWithoutChangingTheWorld()
    {
        var game = Start();
        var save = game.ExportSaveJson();
        var invalid = (EconomicActivityKind)255;
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalAdjacentWorkSurveyProjection.Survey(game, game.InitialTile, invalid));
        var empty = game.AdjacentWorkable(game.InitialTile)[0];
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalAdjacentWorkSurveyProjection.Survey(game, empty,
                EconomicActivityKind.Agriculture));
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }
}
