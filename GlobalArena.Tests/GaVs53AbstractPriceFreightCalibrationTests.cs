using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs53AbstractPriceFreightCalibrationTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId Buyer = new(101);
    private static readonly EconomicPointId Producer = new(201);
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Stone = new(8);
    private const string QuoteUnit = "VS40-FREIGHT-NOMINAL-LAB";

    private static FoodTransportCostFraction F(long numerator, long denominator = 1) =>
        new(numerator, denominator);

    private static void Equal(long numerator, long denominator,
        FoodTransportCostFraction actual) =>
        Assert.Equal(0, actual.CompareTo(F(numerator, denominator)));

    private static CanonicalPhysicalTacticalSession Game() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static EconomicProductionDemandResult Flows() =>
        new(new[] {
            new EconomicSupplyFlow(Producer, Food,
                EconomicActivityKind.Agriculture, Unit)
        }, new[] {
            new EconomicDemandFlow(Buyer, Food, 3 * Unit),
            new EconomicDemandFlow(Buyer, Stone, Unit)
        });

    private static ExperimentalAbstractPurchasingProjection Purchasing(
        CanonicalPhysicalTacticalSession game,
        EconomicProductionDemandResult? flows = null) =>
        new ExperimentalAbstractPurchasingPowerProjector().Project(
            game, flows ?? Flows(),
            new[] { new ExperimentalAbstractBuyerAnchor(
                Buyer, game.InitialTile, F(1)) },
            new[] { new ExperimentalAbstractProducerAnchor(
                Producer, game.InitialTile) },
            Array.Empty<ExperimentalSimultaneousFreightLane>(),
            new[] {
                new ExperimentalBuyerCommodityPrice(Buyer, Food, F(4)),
                new ExperimentalBuyerCommodityPrice(Buyer, Stone, F(4))
            },
            new ExperimentalAbstractPurchasingPolicy(
                "VS53-PURCHASING", 1, F(5), F(5, 8)));

    private static ExperimentalAbstractPriceFreightCalibrationPolicy Policy(
        FoodTransportCostFraction? factor = null,
        string purchasingId = "VS53-PURCHASING",
        ulong purchasingVersion = 1) =>
        new("VS53-CALIBRATION-SCENARIO", 1, purchasingId,
            purchasingVersion, QuoteUnit, factor ?? F(2));

    private static ExperimentalAbstractNominalDeliveredQuote[] Quotes(
        string quoteUnit = QuoteUnit) =>
        new[] {
            new ExperimentalAbstractNominalDeliveredQuote(
                Buyer, Food, F(4), F(1), quoteUnit),
            new ExperimentalAbstractNominalDeliveredQuote(
                Buyer, Stone, F(4), F(1), quoteUnit)
        };

    private static ExperimentalAbstractPriceFreightCalibrationResult Calibrate(
        CanonicalPhysicalTacticalSession game,
        FoodTransportCostFraction? factor = null) =>
        new ExperimentalAbstractPriceFreightCalibrator().Project(
            Purchasing(game), Flows(), Quotes(), Policy(factor));

    [Fact]
    public void EightResidentsGoodsFreightAndProtectedHShareOneExplicitAbstractUnit()
    {
        var game = Game();
        var save = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var v14 = game.FoodCash!.At(game.InitialTile).HouseholdMicrocredits;

        var result = Calibrate(game);
        Equal(32, 1, result.TotalBaselineCommodityUnits);
        Equal(8, 1, result.TotalBaselineFreightUnits);
        Equal(40, 1, result.TotalBaselineDeliveredUnits);
        Equal(5, 1, result.TotalProtectedInvisibleUnits);
        Equal(35, 1, result.TotalSpendablePurchasingUnits);
        Assert.Equal(2, result.Quotes.Count);
        Equal(8, 1, result.Quotes[0].CommodityUnitPriceAbstract);
        Equal(2, 1, result.Quotes[0].EstimatedFreightPerWholeUnitAbstract);

        var buyer = Assert.Single(result.AlphaDemand.Buyers);
        Equal(7, 8, buyer.Alpha);
        Equal(40, 1, buyer.BaselineEstimatedSpending);
        Equal(35, 1, buyer.ScaledEstimatedSpending);
        Assert.Equal(3 * Unit * 7 / 8,
            result.AlphaDemand.ProjectedDemand.Single(
                x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(Unit * 7 / 8,
            result.AlphaDemand.ProjectedDemand.Single(
                x => x.CommodityId == Stone).RawQuantity);
        Assert.Equal(0, result.UnresolvedTransportServiceCount);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.Equal("VS53-CALIBRATION-SCENARIO", result.CalibrationId);
        Assert.Equal(1UL, result.PurchasingPolicyVersion);

        Assert.Equal(v14, game.FoodCash.At(game.InitialTile).HouseholdMicrocredits);
        Assert.Equal(hash, game.CurrentHash);
        Assert.Equal(save, game.ExportSaveJson());
        Assert.Empty(game.Events);
    }

    [Fact]
    public void DifferentExplicitConversionScenariosChangeAlphaNotResidentWealth()
    {
        var game = Game();
        var low = Calibrate(game, F(1));
        var high = Calibrate(game, F(4));
        Equal(20, 1, low.TotalBaselineDeliveredUnits);
        Equal(80, 1, high.TotalBaselineDeliveredUnits);
        Equal(1, 1, Assert.Single(low.AlphaDemand.Buyers).Alpha);
        Equal(7, 16, Assert.Single(high.AlphaDemand.Buyers).Alpha);
        Equal(35, 1, low.TotalSpendablePurchasingUnits);
        Equal(35, 1, high.TotalSpendablePurchasingUnits);
        Equal(5, 1, low.TotalProtectedInvisibleUnits);
        Equal(5, 1, high.TotalProtectedInvisibleUnits);
        Assert.False(low.CanAuthorizeRealSettlement);
        Assert.False(high.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void FractionalConversionAndZeroFreightReconcileWithoutRoundingLoss()
    {
        var game = Game();
        var purchasing = Purchasing(game);
        var quotes = new[] {
            new ExperimentalAbstractNominalDeliveredQuote(
                Buyer, Food, F(7, 3), F(1, 3), QuoteUnit),
            new ExperimentalAbstractNominalDeliveredQuote(
                Buyer, Stone, F(7, 3), F(1, 3), QuoteUnit)
        };
        var result = new ExperimentalAbstractPriceFreightCalibrator().Project(
            purchasing, Flows(), quotes, Policy(F(3, 2)));
        Equal(14, 1, result.TotalBaselineCommodityUnits);
        Equal(2, 1, result.TotalBaselineFreightUnits);
        Equal(16, 1, result.TotalBaselineDeliveredUnits);
        Equal(1, 1, Assert.Single(result.AlphaDemand.Buyers).Alpha);

        var freeShipping = quotes.Select(x => x with {
            NominalEstimatedFreightPerWholeUnit = F(0)
        });
        var withoutFreight = new ExperimentalAbstractPriceFreightCalibrator()
            .Project(purchasing, Flows(), freeShipping, Policy(F(3, 2)));
        Equal(0, 1, withoutFreight.TotalBaselineFreightUnits);
        Equal(14, 1, withoutFreight.TotalBaselineDeliveredUnits);
        Assert.False(withoutFreight.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void MissingDuplicateForeignOrMixedUnitQuotesFailClosed()
    {
        var game = Game();
        var purchasing = Purchasing(game);
        var calibrator = new ExperimentalAbstractPriceFreightCalibrator();
        Assert.Throws<ArgumentException>(() => calibrator.Project(
            purchasing, Flows(), Quotes().Take(1), Policy()));
        Assert.Throws<ArgumentException>(() => calibrator.Project(
            purchasing, Flows(), Quotes().Concat(Quotes()), Policy()));
        Assert.Throws<ArgumentException>(() => calibrator.Project(
            purchasing, Flows(), Quotes("OTHER-CURRENCY"), Policy()));
        Assert.Throws<ArgumentException>(() => calibrator.Project(
            purchasing, Flows(), Quotes().Append(
                new ExperimentalAbstractNominalDeliveredQuote(
                    Buyer, new CommodityId(99), F(1), F(1), QuoteUnit)),
            Policy()));
        Assert.Throws<ArgumentException>(() => calibrator.Project(
            purchasing, Flows(), Quotes(), Policy(purchasingId: "OTHER")));
        Assert.Throws<ArgumentException>(() => calibrator.Project(
            purchasing, Flows(), Quotes(), Policy(purchasingVersion: 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Policy(F(0)));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "", 1, "VS53-PURCHASING", 1, QuoteUnit, F(1)));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalAbstractPriceFreightCalibrationPolicy(
                "VS53", 0, "VS53-PURCHASING", 1, QuoteUnit, F(1)));
    }

    [Fact]
    public void DepartingWorkersReduceAbstractBudgetButNeverCreateTransitBuyer()
    {
        var game = Game();
        var target = game.AdjacentWorkable(game.InitialTile)[0];
        game.OrderWorkerTransit(game.InitialTile, target,
            EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 2);
        var before = game.ExportSaveJson();
        var beforeHash = game.CurrentHash;
        var buying = Purchasing(game);
        Assert.Equal(6UL, buying.ResidentWorkersInWorld);
        Assert.Equal(2UL, buying.TravellingWorkers);
        var result = new ExperimentalAbstractPriceFreightCalibrator().Project(
            buying, Flows(), Quotes(), Policy());
        Equal(30, 1, Assert.Single(buying.Buyers)
            .TemporaryCycleBudget.GrossCash);
        Equal(15, 4, result.TotalProtectedInvisibleUnits);
        Equal(105, 4, result.TotalSpendablePurchasingUnits);
        Equal(21, 32, Assert.Single(result.AlphaDemand.Buyers).Alpha);
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(beforeHash, game.CurrentHash);
        Assert.False(result.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void RealPhysicalServiceDoesNotCreateAFinancialOwnerOrSettlementPermission()
    {
        var game = Game();
        var flows = Flows();
        var pricing = new ExperimentalFreightTechnologyPricing(
            101, 7, 53, ExperimentalLandInfrastructureKind.StoneRoad,
            1, 0, 0, 0, 1_000_000, 0, 1_000_000);
        var route = new StrategicEconomicRouteKey(Producer, Buyer);
        var crossing = new ExperimentalSharedFreightCorridorKey(
            new PhysicalTacticalEdgeId(new GoldbergParameters(18, 0),
                new StrategicEdgeId(1)), 101, 7);
        var option = ExperimentalSharedFreightOption.Synthetic(
            17, route, pricing, 1_000_000, new[] { crossing });
        var lane = new ExperimentalSimultaneousFreightLane(
            flows.SupplyFlows[0], flows.DemandFlows[0], new[] { option });
        var purchasing = new ExperimentalAbstractPurchasingPowerProjector()
            .Project(game, flows,
                new[] { new ExperimentalAbstractBuyerAnchor(
                    Buyer, game.InitialTile, F(1)) },
                new[] { new ExperimentalAbstractProducerAnchor(
                    Producer, game.InitialTile) },
                new[] { lane },
                new[] {
                    new ExperimentalBuyerCommodityPrice(Buyer, Food, F(4)),
                    new ExperimentalBuyerCommodityPrice(Buyer, Stone, F(4))
                },
                new ExperimentalAbstractPurchasingPolicy(
                    "VS53-PURCHASING", 1, F(5), F(5, 8)));
        var result = new ExperimentalAbstractPriceFreightCalibrator()
            .Project(purchasing, flows, Quotes(), Policy());
        Assert.Equal(1, result.UnresolvedTransportServiceCount);
        Assert.False(result.CanAuthorizeRealSettlement);
        Equal(40, 1, result.TotalBaselineDeliveredUnits);
    }
}
