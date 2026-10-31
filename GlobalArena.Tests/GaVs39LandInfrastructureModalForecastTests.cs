using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs39LandInfrastructureModalForecastTests
{
    private const ulong Catalog = 7UL;
    private const ulong Stone = 101UL;
    private const ulong Highway = 102UL;
    private const ulong Rail = 103UL;
    private static readonly ExperimentalLandMobilityCalibration Trial =
        new(100U, 80U, 60U, 35U);

    private static CanonicalPhysicalTacticalSession Start() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static IReadOnlyDictionary<(ulong TechnologyTypeId, ulong CatalogVersion),
        ExperimentalLandInfrastructureKind> Technologies() =>
        new Dictionary<(ulong, ulong), ExperimentalLandInfrastructureKind>
        {
            [(Stone, Catalog)] = ExperimentalLandInfrastructureKind.StoneRoad,
            [(Highway, Catalog)] = ExperimentalLandInfrastructureKind.Highway,
            [(Rail, Catalog)] = ExperimentalLandInfrastructureKind.Railway
        };

    private static (PhysicalTacticalTileId First,
        PhysicalTacticalTileId Middle, PhysicalTacticalTileId Last) LegPair(
        CanonicalPhysicalTacticalSession game)
    {
        var start = game.InitialTile;
        var near = game.AdjacentWorkable(start).ToHashSet();
        var distant = near.SelectMany(game.AdjacentWorkable)
            .Where(x => x != start && !near.Contains(x))
            .OrderBy(x => x.FineStrategicCellId.Value).First();
        var middle = game.AdjacentWorkable(distant)
            .Where(near.Contains)
            .OrderBy(x => x.FineStrategicCellId.Value).First();
        return (start, middle, distant);
    }

    private static EconomicTransportBuiltAsset Build(
        CanonicalPhysicalTacticalSession game,
        PhysicalTacticalTileId from, PhysicalTacticalTileId to,
        ulong assetId, ulong technology = Stone,
        ulong version = Catalog, bool operational = true,
        GoldbergParameters? overrideResolution = null)
    {
        var edge = game.FineWorld.StrategicTopology.Edges.First(x =>
            x.IncidentCellIds.Contains(from.FineStrategicCellId) &&
            x.IncidentCellIds.Contains(to.FineStrategicCellId));
        return new EconomicTransportBuiltAsset(assetId, technology,
            new PhysicalTacticalEdgeId(
                overrideResolution ?? game.InitialTile.FineGoldbergParameters, edge.Id),
            version, operational);
    }

    private static ExperimentalLandInfrastructureSnapshot Network(
        CanonicalPhysicalTacticalSession game,
        CanonicalLandEdgeFactsSnapshot facts,
        IEnumerable<EconomicTransportBuiltAsset> assets,
        StrategicEdgeAccessSnapshot? access = null,
        ulong revision = 1UL) =>
        new(game, facts, assets, Technologies(),
            access ?? StrategicEdgeAccessSnapshot.AllOpen, revision);

    private static ExperimentalModalLandEdgeCostProvider Mode(
        CanonicalLandEdgeFactsSnapshot facts,
        ExperimentalLandInfrastructureSnapshot network,
        ExperimentalLandTransportMode mode) =>
        new(facts, network, mode, Trial);

    private static ScalarPhysicalRoutePreview Route(
        CanonicalPhysicalTacticalSession game,
        PhysicalTacticalTileId target,
        ExperimentalModalLandEdgeCostProvider provider) =>
        ScalarPhysicalRouteProjection.Preview(
            game, game.InitialTile, target,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 1UL, 200UL, provider);

    [Fact]
    public void UnbuiltLandAllowsPrimitiveTravelButNotRoadVehiclesOrRail()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var network = Network(game, facts, Array.Empty<EconomicTransportBuiltAsset>());
        var (first, middle, last) = LegPair(game);
        Assert.Equal(0, network.CrossingsWithBuiltInfrastructure);
        Assert.Equal(ExperimentalLandRoadQuality.NaturalTrail,
            network.At(game, first, middle).RoadQuality);
        Assert.True(Mode(facts, network, ExperimentalLandTransportMode.Walking)
            .CanTraverse(game, first, middle));
        Assert.True(Mode(facts, network, ExperimentalLandTransportMode.PackAnimals)
            .CanTraverse(game, first, middle));
        foreach (var mode in new[] { ExperimentalLandTransportMode.RoadVehicle,
                     ExperimentalLandTransportMode.RailVehicle })
        {
            var provider = Mode(facts, network, mode);
            Assert.False(provider.CanTraverse(game, first, middle));
            Assert.Throws<InvalidOperationException>(() =>
                provider.CostPoints(game, first, middle));
            var route = Route(game, last, provider);
            Assert.False(route.HasRoute);
            Assert.Equal(PhysicalPrimitiveRouteAvailability.NoCompatibleLandTransportMode,
                route.Availability);
            Assert.Empty(route.EdgeCosts);
            Assert.Null(route.Timing);
        }
    }

    [Fact]
    public void OnlyActuallyBuiltRoadProducesAUsableRoadVehicleCorridor()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var (first, middle, last) = LegPair(game);
        var built = new[]
        {
            Build(game, first, middle, 11UL),
            Build(game, middle, last, 12UL)
        };
        var saved = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var network = Network(game, facts, built);
        Assert.Equal(2, network.CrossingsWithBuiltInfrastructure);
        var vehicle = Mode(facts, network, ExperimentalLandTransportMode.RoadVehicle);
        var route = Route(game, last, vehicle);
        Assert.True(route.HasRoute);
        Assert.Equal(new[] { first, middle, last }, route.Tiles);
        Assert.Equal(2, route.EdgeCosts.Count);
        Assert.Equal(vehicle.CostPoints(game, first, middle),
            route.EdgeCosts[0]);
        var baseCost = facts.CostPoints(game, first, middle);
        Assert.Equal((baseCost * 80UL + 99UL) / 100UL,
            vehicle.CostPoints(game, first, middle));
        Assert.Equal(game.State.MovementTurn + route.Timing!.TotalMovementTurns,
            route.Timing.ArrivalTurn);
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void HighwaySupersedesStoneAndRailCanCoexistWithoutStackingRoads()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var (first, middle, _) = LegPair(game);
        var installed = new[]
        {
            Build(game, first, middle, 1UL, Stone),
            Build(game, first, middle, 2UL, Highway),
            Build(game, first, middle, 3UL, Rail)
        };
        var network = Network(game, facts, installed);
        Assert.Equal(1, network.CrossingsWithBuiltInfrastructure);
        var attributes = network.At(game, first, middle);
        Assert.Equal(ExperimentalLandRoadQuality.Highway, attributes.RoadQuality);
        Assert.True(attributes.HasRailway);
        Assert.Equal(attributes, network.At(game, middle, first));
        var road = Mode(facts, network, ExperimentalLandTransportMode.RoadVehicle);
        var train = Mode(facts, network, ExperimentalLandTransportMode.RailVehicle);
        var baseCost = facts.CostPoints(game, first, middle);
        Assert.Equal((baseCost * 60UL + 99UL) / 100UL,
            road.CostPoints(game, first, middle));
        Assert.Equal((baseCost * 35UL + 99UL) / 100UL,
            train.CostPoints(game, first, middle));
    }

    [Fact]
    public void RailwayRequiresContinuityAndNeverGrantsFreeModalTransfers()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var (first, middle, last) = LegPair(game);
        var oneRail = new[] { Build(game, first, middle, 1UL, Rail) };
        var incomplete = Network(game, facts, oneRail);
        var partial = Route(game, last,
            Mode(facts, incomplete, ExperimentalLandTransportMode.RailVehicle));
        Assert.Equal(PhysicalPrimitiveRouteAvailability.NoCompatibleLandTransportMode,
            partial.Availability);
        Assert.False(partial.HasRoute);

        var twoRails = Network(game, facts,
            oneRail.Append(Build(game, middle, last, 2UL, Rail)));
        var complete = Route(game, last,
            Mode(facts, twoRails, ExperimentalLandTransportMode.RailVehicle));
        Assert.True(complete.HasRoute);
        Assert.Equal(new[] { first, middle, last }, complete.Tiles);
        Assert.False(Mode(facts, twoRails, ExperimentalLandTransportMode.RoadVehicle)
            .CanTraverse(game, first, middle));
        Assert.Equal(PhysicalPrimitiveRouteAvailability.NoCompatibleLandTransportMode,
            Route(game, last,
                Mode(facts, twoRails, ExperimentalLandTransportMode.RoadVehicle))
                .Availability);
    }

    [Fact]
    public void VersionedCatalogAndFineResolutionCannotBeGuessedOrAliased()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var (first, middle, _) = LegPair(game);
        var unknown = Build(game, first, middle, 1UL, Stone, version: 99UL);
        Assert.Throws<ArgumentException>(() => Network(game, facts, new[] { unknown }));
        var coarse = Build(game, first, middle, 2UL, Stone,
            overrideResolution: new GoldbergParameters(19, 0));
        Assert.Throws<ArgumentException>(() => Network(game, facts, new[] { coarse }));
        var duplicate = Build(game, first, middle, 1UL, Stone);
        Assert.Throws<ArgumentException>(() =>
            Network(game, facts, new[] { duplicate, duplicate }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Network(game, facts, Array.Empty<EconomicTransportBuiltAsset>(),
                revision: 0UL));
    }

    [Fact]
    public void ClosedOrNonOperationalBuiltAssetsDoNotCreateRoads()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var (first, middle, _) = LegPair(game);
        var inactive = Build(game, first, middle, 1UL, Stone, operational: false);
        var live = Build(game, first, middle, 2UL, Highway);
        var closed = new StrategicEdgeAccessSnapshot(new[] { live.GeographicEdgeId });
        var network = Network(game, facts, new[] { inactive, live }, closed);
        Assert.Equal(0, network.CrossingsWithBuiltInfrastructure);
        Assert.Equal(ExperimentalLandRoadQuality.NaturalTrail,
            network.At(game, first, middle).RoadQuality);
    }

    [Fact]
    public void SeaCrossingsCannotBeLaunderedIntoImplicitLandCorridors()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var seaEdge = game.FineWorld.StrategicTopology.Edges.First(x =>
            x.IncidentCellIds.Any(id => !game.IsPhysicalWorkableLand(id)));
        var illegal = new EconomicTransportBuiltAsset(
            1UL, Stone, new PhysicalTacticalEdgeId(
                game.InitialTile.FineGoldbergParameters, seaEdge.Id), Catalog);
        Assert.Throws<InvalidOperationException>(() =>
            Network(game, facts, new[] { illegal }));
    }

    [Fact]
    public void NetworkRevisionsDoNotMutateEarlierQuotesOrCrossSessions()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var (first, middle, _) = LegPair(game);
        var empty = Network(game, facts, Array.Empty<EconomicTransportBuiltAsset>());
        var newer = Network(game, facts,
            new[] { Build(game, first, middle, 1UL, Stone) }, revision: 2UL);
        Assert.Equal(1UL, empty.NetworkRevision);
        Assert.Equal(2UL, newer.NetworkRevision);
        Assert.Equal(ExperimentalLandRoadQuality.NaturalTrail,
            empty.At(game, first, middle).RoadQuality);
        Assert.Equal(ExperimentalLandRoadQuality.StoneRoad,
            newer.At(game, first, middle).RoadQuality);
        var foreign = Start();
        Assert.Throws<InvalidOperationException>(() =>
            newer.At(foreign, first, middle));
        Assert.Throws<InvalidOperationException>(() =>
            Mode(facts, newer, ExperimentalLandTransportMode.Walking)
                .CanTraverse(foreign, first, middle));
    }

    [Fact]
    public void WalkingRetainsLegacyScalarCostWhenCalibratedAtOneHundredPercent()
    {
        var game = Start();
        var facts = new CanonicalLandEdgeFactsSnapshot(game);
        var network = Network(game, facts, Array.Empty<EconomicTransportBuiltAsset>());
        var (_, _, last) = LegPair(game);
        var walking = Mode(facts, network, ExperimentalLandTransportMode.Walking);
        var direct = ScalarPhysicalRouteProjection.Preview(
            game, game.InitialTile, last,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry,
            1UL, 200UL, facts);
        var modal = Route(game, last, walking);
        Assert.Equal(direct.Availability, modal.Availability);
        Assert.Equal(direct.Tiles.ToArray(), modal.Tiles.ToArray());
        Assert.Equal(direct.EdgeCosts.ToArray(), modal.EdgeCosts.ToArray());
        Assert.Equal(direct.Timing!.ArrivalTurn, modal.Timing!.ArrivalTurn);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExperimentalLandMobilityCalibration(0U, 80U, 60U, 35U));
    }
}
