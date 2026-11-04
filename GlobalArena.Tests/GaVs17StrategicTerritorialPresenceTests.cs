using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs17StrategicTerritorialPresenceTests
{
    private static readonly GoldbergParameters Coarse = new(3, 0);
    private static readonly GoldbergParameters Fine = new(18, 0);

    private static PhysicalTacticalIncidenceMap Incidence() =>
        PhysicalTacticalIncidenceMapper.Materialize(new GoldbergScaledRefinement(Coarse, Fine));

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, Coarse, Fine,
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent);

    [Fact]
    public void InitialPresenceUsesRealCanonicalIncidenceWithoutClaimingPoliticalOwnership()
    {
        var game = Start();
        var map = Incidence();
        var beforeHash = game.CurrentHash;
        var beforeSave = game.ExportSaveJson();

        var projection = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, map);
        Assert.Equal(8UL, projection.TotalWorkers);
        Assert.Equal(0UL, projection.MovementTurn);
        Assert.Equal(48UL, projection.Territories.Aggregate(0UL,
            (sum, t) => checked(sum + t.TotalSixths)));
        Assert.Equal(0UL, projection.Territories.Aggregate(0UL,
            (sum, t) => checked(sum + t.InTransitSixths)));
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
        Assert.Empty(game.Game.Snapshot.Civilizations.Civilizations);
        Assert.Equal(beforeHash, game.CurrentHash);
        Assert.Equal(beforeSave, game.ExportSaveJson());
        var absent = map.CoarseTopology.Cells.Select(x => x.Id)
            .First(id => projection.At(id).TotalSixths == 0UL);
        Assert.Equal(0.0, projection.At(absent).TotalEquivalent);
        Assert.Equal(0, projection.At(absent).OccupiedPhysicalTiles);
    }

    [Fact]
    public void BorderTilesSplitWeightedPresenceWithoutEverDuplicatingPopulation()
    {
        var game = Start();
        var map = Incidence();
        foreach (var incidence in map.TileIncidences
                     .Where(x => x.IncidentCoarseCellIds.Count is 2 or 3)
                     .GroupBy(x => x.IncidentCoarseCellIds.Count)
                     .Select(x => x.First()))
        {
            const ulong number = 6UL;
            var physical = game.State;
            var synthetic = new PhysicalTacticalEconomyRuntimeState(
                physical.CoarseWorldBinding, physical.CoarseParameters,
                physical.FineParameters, 0UL, 0UL,
                new[]
                {
                    new PhysicalWorkerAllocation(incidence.PhysicalTacticalTileId,
                        EconomicActivityKind.Agriculture, number)
                },
                Array.Empty<PhysicalCommodityStock>(),
                Array.Empty<PhysicalEconomicCycle>(),
                PhysicalEconomicCadenceMode.AdaptiveThirty,
                PhysicalTacticalGeographyMode.Canonical01C);
            var view = StrategicTerritorialPresenceProjection.FromPhysicalState(synthetic, map);
            Assert.Equal(6UL, view.TotalWorkers);
            Assert.Equal((ulong)(6 * number),
                view.Territories.Aggregate(0UL,
                    (sum, x) => checked(sum + x.TotalSixths)));
            Assert.Equal(incidence.IncidentCoarseCellIds.Count, view.Territories.Count);
            var perTerritory = number * (6UL / (ulong)incidence.IncidentCoarseCellIds.Count);
            foreach (var owner in incidence.IncidentCoarseCellIds)
            {
                var item = view.At(owner);
                Assert.Equal(perTerritory, item.WorkingSixths);
                Assert.Equal(0UL, item.InTransitSixths);
                Assert.Equal(1, item.OccupiedPhysicalTiles);
            }
        }
    }

    [Fact]
    public void DepartureKeepsPopulationAnchoredToOriginUntilPhysicalArrival()
    {
        var game = Start();
        var map = Incidence();
        var before = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, map);
        var target = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);

        var travelling = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, map);
        Assert.Equal(48UL, travelling.Territories.Aggregate(0UL,
            (sum, x) => checked(sum + x.TotalSixths)));
        Assert.Equal(12UL, travelling.Territories.Aggregate(0UL,
            (sum, x) => checked(sum + x.InTransitSixths)));
        Assert.Equal(before.Territories.Select(x => (x.Territory, x.TotalSixths)),
            travelling.Territories.Select(x => (x.Territory, x.TotalSixths)));
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        game.AdvanceMovementTurn();
        var mid = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, map);
        Assert.Equal(12UL, mid.Territories.Aggregate(0UL,
            (sum, x) => checked(sum + x.InTransitSixths)));
        var save = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var reloaded = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
        Assert.Equal(hash, reloaded.CurrentHash);
        Assert.Equal(save, reloaded.ExportSaveJson());
        Assert.Equal(mid.Territories,
            StrategicTerritorialPresenceProjection.FromPhysicalState(reloaded.State, map).Territories);

        game.AdvanceMovementTurn();
        reloaded.AdvanceMovementTurn();
        var arrived = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, map);
        Assert.Equal(48UL, arrived.Territories.Aggregate(0UL,
            (sum, x) => checked(sum + x.TotalSixths)));
        Assert.Equal(0UL, arrived.Territories.Aggregate(0UL,
            (sum, x) => checked(sum + x.InTransitSixths)));
        Assert.Empty(reloaded.State.Transits);
        Assert.Equal(game.CurrentHash, reloaded.CurrentHash);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
    }


    [Fact]
    public void LongDistanceAndAdjacentTravellersPreserveTerritorialSixthsAcrossReplayAndArrival()
    {
        var game = Start();
        var incidence = Incidence();
        var source = game.InitialTile;
        var adjacent = game.AdjacentWorkable(source);
        var neighbours = adjacent.ToHashSet();
        var destination = adjacent.SelectMany(game.AdjacentWorkable)
            .Where(x => x != source && !neighbours.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value)
            .First();
        var original = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, incidence);

        var longTrip = game.OrderLongDistanceTransit(source, destination,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 2UL);
        Assert.True(longTrip.TotalLegs >= 2);
        game.OrderWorkerTransit(source, adjacent[0],
            EconomicActivityKind.Mining, EconomicActivityKind.Mining, 1UL);

        var inTransit = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, incidence);
        Assert.Equal(8UL, inTransit.TotalWorkers);
        Assert.Equal(48UL, inTransit.Territories.Sum(x => x.TotalSixths));
        Assert.Equal(18UL, inTransit.Territories.Sum(x => x.InTransitSixths));
        Assert.Equal(original.Territories.Select(x => (x.Territory, x.TotalSixths)),
            inTransit.Territories.Select(x => (x.Territory, x.TotalSixths)));
        Assert.Empty(game.Game.Snapshot.Territory.Controls);

        while (game.State.MovementTurn < longTrip.ArrivalTurn)
        {
            var save = game.ExportSaveJson();
            var hash = game.CurrentHash;
            var view = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, incidence);
            Assert.Equal(48UL, view.Territories.Sum(x => x.TotalSixths));
            Assert.Equal(hash, game.CurrentHash);
            Assert.Equal(save, game.ExportSaveJson());
            var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);
            Assert.Equal(hash, reopened.CurrentHash);
            Assert.Equal(view.Territories,
                StrategicTerritorialPresenceProjection.FromPhysicalState(reopened.State, incidence).Territories);
            game.AdvanceMovementTurn();
        }

        var arrived = StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, incidence);
        Assert.Equal(48UL, arrived.Territories.Sum(x => x.TotalSixths));
        Assert.Equal(0UL, arrived.Territories.Sum(x => x.InTransitSixths));
        Assert.Equal(8UL, arrived.TotalWorkers);
        Assert.Empty(game.State.LongTrips);
        Assert.Empty(game.State.Transits);
        Assert.Empty(game.Game.Snapshot.Territory.Controls);
    }

    [Fact]
    public void RejectsForeignStrategicIncidenceInsteadOfGuessingPopulationOwnership()
    {
        var game = Start();
        var foreign = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(1, 0),
                new GoldbergParameters(6, 0)));
        Assert.Throws<ArgumentException>(() =>
            StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, foreign));
        Assert.Throws<ArgumentNullException>(() =>
            StrategicTerritorialPresenceProjection.FromPhysicalState(game.State, null!));
    }
}
