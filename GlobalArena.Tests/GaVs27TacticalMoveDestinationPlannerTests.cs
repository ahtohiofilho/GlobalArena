using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs27TacticalMoveDestinationPlannerTests
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
    public void SelectedHighestYieldIsDeterministicAndDoesNotCommitTravel(
        EconomicActivityKind destinationJob)
    {
        var game = Start();
        var origin = game.InitialTile;
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var journal = game.Events.ToArray();

        var plan = PhysicalWorkerDestinationPlanner.PreviewHighestYield(
            game, origin, EconomicActivityKind.Agriculture, destinationJob, 2UL);
        var survey = PhysicalAdjacentWorkSurveyProjection.Survey(
            game, origin, destinationJob);
        Assert.Equal(origin, plan.Origin);
        Assert.Equal(survey.Neighbours[0].Tile, plan.Destination);
        Assert.Equal(survey.CurrentTileYieldRaw, plan.OriginYieldPerWorkerRaw);
        Assert.Equal(survey.Neighbours[0].YieldFor(destinationJob),
            plan.DestinationYieldPerWorkerRaw);
        Assert.Equal(2UL, plan.Workers);
        Assert.Equal(0UL, plan.DestinationResidents);
        Assert.Equal(0UL, plan.DestinationTravellers);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(journal, game.Events);
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Equal(0UL, game.State.MovementTurn);
        Assert.Empty(game.State.Transits);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(before);
        Assert.Equal(plan, PhysicalWorkerDestinationPlanner.PreviewHighestYield(
            reopened, origin, EconomicActivityKind.Agriculture, destinationJob, 2UL));
    }

    [Fact]
    public void SeparateCanonicalMoveOrderActuallyTravelsTwoTicksAndArrives()
    {
        var game = Start();
        var origin = game.InitialTile;
        var plan = PhysicalWorkerDestinationPlanner.PreviewHighestYield(
            game, origin, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2UL);
        var hashBeforeSelection = game.CurrentHash;
        var beforeActions = game.Events.Count;
        Assert.Empty(game.State.Transits);
        Assert.Equal(hashBeforeSelection, game.CurrentHash);

        game.OrderWorkerTransit(plan.Origin, plan.Destination,
            plan.FromJob, plan.DestinationJob, plan.Workers);
        Assert.Equal(beforeActions + 1, game.Events.Count);
        Assert.Equal(2UL, game.State.WorkersInTransit);
        Assert.Equal(0UL, game.WorkersAt(plan.Destination));
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        game.AdvanceMovementTurn();
        Assert.Equal(1UL, game.State.MovementTurn);
        Assert.Equal(2UL, game.State.WorkersInTransit);
        game.AdvanceMovementTurn();
        Assert.Equal(2UL, game.State.MovementTurn);
        Assert.Equal(0UL, game.State.WorkersInTransit);
        Assert.Equal(2UL, game.WorkersAt(plan.Destination));
        Assert.Equal(8UL, game.State.TotalWorkers);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(
            game.ExportSaveJson());
        Assert.Equal(game.CurrentHash, reopened.CurrentHash);
        Assert.Equal(2UL, reopened.WorkersAt(plan.Destination));
    }

    [Fact]
    public void PlannerExposesExistingDestinationsAndIncomingWorkersWithoutMutating()
    {
        var game = Start(born: false);
        var origin = game.InitialTile;
        var original = PhysicalWorkerDestinationPlanner.PreviewHighestYield(
            game, origin, EconomicActivityKind.Forestry,
            EconomicActivityKind.Mining, 1UL);
        game.OrderWorkerTransit(origin, original.Destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Mining, 1UL);
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var withTransit = PhysicalWorkerDestinationPlanner.PreviewHighestYield(
            game, origin, EconomicActivityKind.Forestry,
            EconomicActivityKind.Mining, 1UL);
        Assert.Equal(original.Destination, withTransit.Destination);
        Assert.Equal(1UL, withTransit.DestinationTravellers);
        Assert.Equal(0UL, withTransit.DestinationResidents);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(13U, game.CurrentHash.FormatVersion);
    }

    [Fact]
    public void IllegalSourceActivityOrAmountCannotSelectAValidDestination()
    {
        var game = Start();
        var before = game.ExportSaveJson();
        var origin = game.InitialTile;
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalWorkerDestinationPlanner.PreviewHighestYield(game, origin,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 0UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalWorkerDestinationPlanner.PreviewHighestYield(game, origin,
                EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 5UL));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalWorkerDestinationPlanner.PreviewHighestYield(game, origin,
                EconomicActivityKind.Mining, EconomicActivityKind.Forestry, 3UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalWorkerDestinationPlanner.PreviewHighestYield(game, origin,
                EconomicActivityKind.Agriculture, (EconomicActivityKind)255, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PhysicalWorkerDestinationPlanner.PreviewHighestYield(game, origin,
                (EconomicActivityKind)255, EconomicActivityKind.Mining, 1UL));
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }
}
