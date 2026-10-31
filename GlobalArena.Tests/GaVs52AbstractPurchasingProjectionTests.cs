using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs52AbstractPurchasingProjectionTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId Buyer = new(101);
    private static readonly EconomicPointId Supplier = new(201);
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Stone = new(8);

    private static CanonicalPhysicalTacticalSession Game() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static FoodTransportCostFraction Money(long amount) =>
        new(amount, 1);

    private static ExperimentalAbstractPurchasingPolicy Policy(
        long basic = 5) =>
        new("GA-PURCHASING-ABSTRACT-REFERENCE", 1,
            Money(basic), new FoodTransportCostFraction(5, 8));

    private static ExperimentalAbstractBuyerAnchor Anchor(
        PhysicalTacticalTileId tile,
        EconomicPointId? buyer = null,
        FoodTransportCostFraction? factor = null) =>
        new(buyer ?? Buyer, tile, factor ?? Money(1));

    private static EconomicProductionDemandResult Flows(
        EconomicPointId? buyer = null, EconomicPointId? source = null) =>
        new(new[] { new EconomicSupplyFlow(source ?? Supplier,
            Food, EconomicActivityKind.Agriculture, Unit) },
            new[] {
                new EconomicDemandFlow(buyer ?? Buyer, Food, 3 * Unit),
                new EconomicDemandFlow(buyer ?? Buyer, Stone, Unit)
            });

    private static ExperimentalBuyerCommodityPrice[] Prices(
        EconomicPointId? buyer = null) =>
        new[] {
            new ExperimentalBuyerCommodityPrice(buyer ?? Buyer, Food, Money(10)),
            new ExperimentalBuyerCommodityPrice(buyer ?? Buyer, Stone, Money(10))
        };

    private static ExperimentalAbstractPurchasingProjection Project(
        CanonicalPhysicalTacticalSession game,
        FoodTransportCostFraction? factor = null,
        ExperimentalAbstractPurchasingPolicy? policy = null,
        ExperimentalAbstractProducerAnchor[]? producers = null,
        ExperimentalSimultaneousFreightLane[]? lanes = null) =>
        new ExperimentalAbstractPurchasingPowerProjector().Project(
            game, Flows(), new[] { Anchor(game.InitialTile, factor: factor) },
            producers ?? new[] {
                new ExperimentalAbstractProducerAnchor(Supplier, game.InitialTile)
            }, lanes ?? [], Prices(), policy ?? Policy());

    private static void Equal(long amount, FoodTransportCostFraction actual) =>
        Assert.Equal(0, actual.CompareTo(Money(amount)));

    [Fact]
    public void InitialWorkerSettlementProducesAbstractBudgetWithoutReadingV14CashOrMutatingSave()
    {
        var game = Game();
        var saved = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var cash = game.FoodCash!;
        Assert.Equal(320_000_000UL, cash.At(game.InitialTile).HouseholdMicrocredits);

        var projected = Project(game);
        var buyer = Assert.Single(projected.Buyers);
        Assert.Equal(8UL, buyer.ResidentWorkers);
        Assert.Equal(game.InitialTile, buyer.Tile);
        Equal(40, buyer.TemporaryCycleBudget.GrossCash);
        Equal(5, buyer.TemporaryCycleBudget.ProtectedResidual);
        Equal(35, buyer.TemporaryCycleBudget.SpendableCash);
        Equal(40, projected.TotalTemporaryPurchasingUnits);
        Assert.Equal(8UL, projected.ResidentWorkersInWorld);
        Assert.Equal(8UL, projected.ResidentWorkersInProjectedBuyers);
        Assert.Equal(0UL, projected.UnprojectedResidentWorkers);
        Assert.Equal(0UL, projected.TravellingWorkers);
        Assert.Single(projected.Producers);
        Assert.Empty(projected.UnresolvedCarriers);
        Assert.Equal("GA-PURCHASING-ABSTRACT-REFERENCE", projected.PolicyId);
        Assert.Equal(1UL, projected.PolicyVersion);
        projected.BudgetSnapshot.ValidateCoverage(Flows());
        Assert.Equal(saved, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void AbstractDemandAlphaUsesDerivedTemporaryCycleCapacityNotTheV14Ledger()
    {
        var game = Game();
        var projected = Project(game);
        var forecasts = new[] {
            new ExperimentalDeliveredDemandEstimate(
                Buyer, Food, Money(10), Money(0)),
            new ExperimentalDeliveredDemandEstimate(
                Buyer, Stone, Money(10), Money(0))
        };
        var result = new ExperimentalCashDemandAlphaPlanner().Project(
            Flows(), projected.Buyers.Select(x => x.TemporaryCycleBudget),
            forecasts);
        var plan = Assert.Single(result.Buyers);
        Assert.Equal(0, plan.Alpha.CompareTo(
            new FoodTransportCostFraction(7, 8)));
        Equal(40, plan.BaselineEstimatedSpending);
        Equal(35, plan.ScaledEstimatedSpending);
        Assert.Equal(2, result.ProjectedDemand.Count);
        Assert.Equal(3 * Unit * 7 / 8,
            result.ProjectedDemand.Single(x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(Unit * 7 / 8,
            result.ProjectedDemand.Single(x => x.CommodityId == Stone).RawQuantity);
    }

    [Fact]
    public void ProductivityFactorDifferentiatesAggregatedPurchasingWithoutChangingWorkforceOrCash()
    {
        var game = Game();
        var before = game.FoodCash!.InitialMoneySupplyMicrocredits;
        var low = Project(game, new FoodTransportCostFraction(1, 2));
        var high = Project(game, new FoodTransportCostFraction(2, 1));
        Equal(20, Assert.Single(low.Buyers).TemporaryCycleBudget.GrossCash);
        Equal(80, Assert.Single(high.Buyers).TemporaryCycleBudget.GrossCash);
        Equal(5, Assert.Single(low.Buyers).TemporaryCycleBudget.ProtectedResidual);
        Equal(5, Assert.Single(high.Buyers).TemporaryCycleBudget.ProtectedResidual);
        Assert.Equal(8UL, low.ResidentWorkersInWorld);
        Assert.Equal(8UL, high.ResidentWorkersInWorld);
        Assert.Equal(before, game.FoodCash.InitialMoneySupplyMicrocredits);
        Assert.Throws<ArgumentException>(() =>
            Project(game, Money(0)));
    }

    [Fact]
    public void WorkersInTransitAreNotCountedTwiceAndNewSettlementCanBeMappedAfterArrival()
    {
        var game = Game();
        var target = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, target,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2);
        var departing = Project(game);
        Assert.Equal(6UL, departing.ResidentWorkersInWorld);
        Assert.Equal(2UL, departing.TravellingWorkers);
        Equal(30, Assert.Single(departing.Buyers).TemporaryCycleBudget.GrossCash);

        game.AdvanceMovementTurn();
        game.AdvanceMovementTurn();
        var settlement = EmergentSettlementProjection.FromSession(game);
        Assert.Equal(2, settlement.Sites.Count);
        Assert.Equal(8UL, settlement.TotalResidentWorkers);
        Assert.Equal(0UL, settlement.TotalTravellers);
        var partial = Project(game);
        Assert.Equal(6UL, partial.ResidentWorkersInProjectedBuyers);
        Assert.Equal(2UL, partial.UnprojectedResidentWorkers);

        var flows = new EconomicProductionDemandResult(
            Array.Empty<EconomicSupplyFlow>(), new[] {
                new EconomicDemandFlow(Buyer, Food, Unit),
                new EconomicDemandFlow(new EconomicPointId(102), Food, Unit)
            });
        var complete = new ExperimentalAbstractPurchasingPowerProjector().Project(
            game, flows, new[] {
                Anchor(game.InitialTile),
                Anchor(target, new EconomicPointId(102))
            }, Array.Empty<ExperimentalAbstractProducerAnchor>(),
            Array.Empty<ExperimentalSimultaneousFreightLane>(),
            new[] {
                new ExperimentalBuyerCommodityPrice(Buyer, Food, Money(10)),
                new ExperimentalBuyerCommodityPrice(new EconomicPointId(102),
                    Food, Money(10))
            }, Policy());
        Assert.Equal(8UL, complete.ResidentWorkersInProjectedBuyers);
        Assert.Equal(0UL, complete.UnprojectedResidentWorkers);
        Assert.Equal(2, complete.Buyers.Count);
        Equal(30, complete.Buyers[0].TemporaryCycleBudget.GrossCash);
        Equal(10, complete.Buyers[1].TemporaryCycleBudget.GrossCash);
        Equal(40, complete.TotalTemporaryPurchasingUnits);
    }

    [Fact]
    public void SuppliedPhysicalServiceIsRecordedButCarrierFinancialIdentityRemainsUnresolved()
    {
        var game = Game();
        var supply = Assert.Single(Flows().SupplyFlows);
        var demand = Flows().DemandFlows[0];
        var pricing = new ExperimentalFreightTechnologyPricing(
            101, 7, 52, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var route = new StrategicEconomicRouteKey(Supplier, Buyer);
        var corridor = new ExperimentalSharedFreightCorridorKey(
            new PhysicalTacticalEdgeId(
                new GoldbergParameters(18, 0), new StrategicEdgeId(1)), 101, 7);
        var option = ExperimentalSharedFreightOption.Synthetic(
            17, route, pricing, 1_000_000, new[] { corridor });
        var lane = new ExperimentalSimultaneousFreightLane(
            supply, demand, new[] { option });
        var projected = Project(game, lanes: new[] { lane });
        var carrier = Assert.Single(projected.UnresolvedCarriers);
        Assert.Equal(Supplier, carrier.Source);
        Assert.Equal(Buyer, carrier.Destination);
        Assert.Equal(17UL, carrier.ServiceId);
        Assert.Single(projected.Producers);
        Assert.Single(projected.Buyers);
        Assert.Equal(game.InitialTile, Assert.Single(projected.Producers).Tile);
        Assert.Equal(game.InitialTile, Assert.Single(projected.Buyers).Tile);
        Assert.Throws<ArgumentException>(() =>
            Project(game, lanes: new[] { lane, lane }));
    }

    [Fact]
    public void MissingDuplicatedAndContradictoryEconomicIdentitiesFailClosed()
    {
        var game = Game();
        var projector = new ExperimentalAbstractPurchasingPowerProjector();
        var flows = Flows();
        var producers = new[] {
            new ExperimentalAbstractProducerAnchor(Supplier, game.InitialTile)
        };
        var buyers = new[] { Anchor(game.InitialTile) };
        Assert.Throws<ArgumentException>(() => projector.Project(
            game, flows, Array.Empty<ExperimentalAbstractBuyerAnchor>(),
            producers, [], Prices(), Policy()));
        Assert.Throws<ArgumentException>(() => projector.Project(
            game, flows, new[] { buyers[0], buyers[0] },
            producers, [], Prices(), Policy()));
        Assert.Throws<ArgumentException>(() => projector.Project(
            game, flows, buyers,
            Array.Empty<ExperimentalAbstractProducerAnchor>(),
            [], Prices(), Policy()));
        Assert.Throws<ArgumentException>(() => projector.Project(
            game, flows, buyers, new[] { producers[0], producers[0] },
            [], Prices(), Policy()));
        Assert.Throws<ArgumentException>(() => projector.Project(
            game, flows, buyers, producers, [],
            Array.Empty<ExperimentalBuyerCommodityPrice>(), Policy()));
    }

    [Fact]
    public void ProducerAndConsumerWithOneEconomicIdentityMustShareOnePhysicalTile()
    {
        var game = Game();
        var same = new EconomicPointId(404);
        var flows = Flows(buyer: same, source: same);
        var projected = new ExperimentalAbstractPurchasingPowerProjector().Project(
            game, flows,
            new[] { Anchor(game.InitialTile, same) },
            new[] { new ExperimentalAbstractProducerAnchor(same, game.InitialTile) },
            [], Prices(same), Policy());
        Assert.Single(projected.Buyers);
        Assert.Single(projected.Producers);
        Assert.Equal(projected.Buyers[0].Tile, projected.Producers[0].Tile);
    }
}
