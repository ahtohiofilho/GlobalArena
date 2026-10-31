using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs53AbstractUnitMarketCalibrationTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId Buyer = new(101);
    private static readonly EconomicPointId Source = new(201);
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Stone = new(8);

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

    private sealed class Fixture
    {
        public CanonicalPhysicalTacticalSession World { get; } = Game();
        public EconomicProductionDemandResult Flows { get; }
        public ExperimentalSimultaneousFreightLane[] Lanes { get; }
        public ExperimentalBuyerCommodityPrice[] Prices { get; }
        public ExperimentalNativeFreightPriceEstimate[] Freight { get; }
        public ExperimentalAbstractPurchasingPolicy Purchasing { get; } = new(
            "GA-ABSTRACT-RESIDENT-V1", 1, F(5), F(5, 8));
        public ExperimentalAbstractMarketUnitCalibration Calibration { get; } =
            new("GA-NATIVE-QUOTES-ABSTRACT-V1", 1,
                "GA-ABSTRACT-RESIDENT-V1", 1,
                "ABSTRACT-PURCHASING-PER-CYCLE", "EXTERNAL-GOODS-UNIT",
                "EXTERNAL-FREIGHT-UNIT", F(2), F(1));
        public ExperimentalAbstractBuyerAnchor[] Buyers =>
            [new(Buyer, World.InitialTile, F(1))];
        public ExperimentalAbstractProducerAnchor[] Producers =>
            [new(Source, World.InitialTile)];

        public Fixture()
        {
            Flows = new EconomicProductionDemandResult(
                [
                    new EconomicSupplyFlow(Source, Food,
                        EconomicActivityKind.Agriculture, 3 * Unit),
                    new EconomicSupplyFlow(Source, Stone,
                        EconomicActivityKind.Mining, Unit)
                ],
                [
                    new EconomicDemandFlow(Buyer, Food, 3 * Unit),
                    new EconomicDemandFlow(Buyer, Stone, Unit)
                ]);
            var tech = new ExperimentalFreightTechnologyPricing(
                101, 7, 53, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 0, 1_000_000);
            var corridor = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(
                    new GoldbergParameters(18, 0), new StrategicEdgeId(1)),
                101, 7);
            ExperimentalSimultaneousFreightLane Route(
                CommodityId commodity, EconomicActivityKind activity,
                ulong quantity, ulong id) =>
                new(new EconomicSupplyFlow(Source, commodity, activity, quantity),
                    new EconomicDemandFlow(Buyer, commodity, quantity),
                    [ExperimentalSharedFreightOption.Synthetic(
                        id, new StrategicEconomicRouteKey(Source, Buyer),
                        tech, 1_000_000, [corridor])]);
            Lanes = [
                Route(Food, EconomicActivityKind.Agriculture, 3 * Unit, 17),
                Route(Stone, EconomicActivityKind.Mining, Unit, 18)
            ];
            Prices = [
                new ExperimentalBuyerCommodityPrice(Buyer, Food, F(4)),
                new ExperimentalBuyerCommodityPrice(Buyer, Stone, F(4))
            ];
            Freight = [
                new ExperimentalNativeFreightPriceEstimate(
                    Source, Buyer, Food, 17, 53, F(2)),
                new ExperimentalNativeFreightPriceEstimate(
                    Source, Buyer, Stone, 18, 53, F(2))
            ];
        }

        public ExperimentalAbstractUnitMarketPreview Preview(
            ExperimentalAbstractMarketUnitCalibration? calibration = null,
            ExperimentalAbstractPurchasingPolicy? purchasing = null,
            ExperimentalBuyerCommodityPrice[]? prices = null,
            ExperimentalNativeFreightPriceEstimate[]? freight = null,
            ExperimentalSimultaneousFreightLane[]? lanes = null,
            ExperimentalAbstractBuyerAnchor[]? buyers = null,
            ExperimentalAbstractProducerAnchor[]? producers = null) =>
            new ExperimentalAbstractMarketUnitCalibrator().Preview(
                World, Flows, buyers ?? Buyers, producers ?? Producers,
                lanes ?? Lanes, prices ?? Prices, freight ?? Freight,
                purchasing ?? Purchasing, calibration ?? Calibration);
    }

    [Fact]
    public void NormalizesGoodsFreightAndResidentPurchasingBeforeComputingAlpha()
    {
        var f = new Fixture();
        var before = f.World.ExportSaveJson();
        var hash = f.World.CurrentHash;
        var oldCash = f.World.FoodCash!.At(f.World.InitialTile);

        var result = f.Preview();
        var buyer = Assert.Single(result.Purchasing.Buyers);
        Equal(40, 1, buyer.TemporaryCycleBudget.GrossCash);
        Equal(5, 1, buyer.TemporaryCycleBudget.ProtectedResidual);
        Equal(35, 1, buyer.TemporaryCycleBudget.SpendableCash);
        Assert.Equal(2, result.DeliveredPrices.Count);
        foreach (var price in result.DeliveredPrices)
        {
            Equal(8, 1, price.AbstractCommodityPerWholeFlowUnit);
            Equal(2, 1, price.AbstractFreightPerWholeFlowUnit);
            Equal(10, 1, price.AbstractDeliveredPerWholeFlowUnit);
            Assert.Equal(Source, price.FreightService.Source);
            Assert.Equal(Buyer, price.FreightService.Destination);
        }
        var alpha = Assert.Single(result.Alpha.Buyers);
        Equal(40, 1, alpha.BaselineEstimatedSpending);
        Equal(35, 1, alpha.ScaledEstimatedSpending);
        Equal(7, 8, alpha.Alpha);
        Assert.Equal(3 * Unit * 7 / 8,
            result.Alpha.ProjectedDemand.Single(x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(Unit * 7 / 8,
            result.Alpha.ProjectedDemand.Single(x => x.CommodityId == Stone).RawQuantity);
        Assert.Equal(2, result.UnresolvedFreightBeneficiaries.Count);
        Assert.Equal("GA-NATIVE-QUOTES-ABSTRACT-V1", result.CalibrationId);
        Assert.Equal(1UL, result.CalibrationVersion);
        Assert.Equal("ABSTRACT-PURCHASING-PER-CYCLE", result.AbstractUnitId);
        Assert.Equal(320_000_000UL, oldCash.HouseholdMicrocredits);
        Assert.Equal(before, f.World.ExportSaveJson());
        Assert.Equal(hash, f.World.CurrentHash);
        Assert.Empty(f.World.Events);
    }

    [Fact]
    public void DifferentDeclaredConversionFactorsChangeForecastNotExistingMoney()
    {
        var f = new Fixture();
        var cheap = new ExperimentalAbstractMarketUnitCalibration(
            "CHEAPER", 2, f.Purchasing.PolicyId, f.Purchasing.Version,
            "ABSTRACT", "GOODS", "FREIGHT", F(1), F(1));
        var expensive = new ExperimentalAbstractMarketUnitCalibration(
            "EXPENSIVE", 2, f.Purchasing.PolicyId, f.Purchasing.Version,
            "ABSTRACT", "GOODS", "FREIGHT", F(4), F(2));
        var cashBefore = f.World.FoodCash!.At(f.World.InitialTile);
        var low = f.Preview(calibration: cheap);
        var high = f.Preview(calibration: expensive);
        Equal(1, 1, Assert.Single(low.Alpha.Buyers).Alpha);
        Equal(7, 16, Assert.Single(high.Alpha.Buyers).Alpha);
        Equal(24, 1, Assert.Single(low.Alpha.Buyers).BaselineEstimatedSpending);
        Equal(80, 1, Assert.Single(high.Alpha.Buyers).BaselineEstimatedSpending);
        Assert.Equal(cashBefore, f.World.FoodCash.At(f.World.InitialTile));
    }

    [Fact]
    public void AggregateProductivityChangesBudgetButNotCommodityOrFreightNativeQuotes()
    {
        var f = new Fixture();
        var alteredBuyers = new[] {
            new ExperimentalAbstractBuyerAnchor(Buyer,
                f.World.InitialTile, F(1, 2))
        };
        var projection = f.Preview(buyers: alteredBuyers);
        Equal(20, 1,
            Assert.Single(projection.Purchasing.Buyers).TemporaryCycleBudget.GrossCash);
        Equal(5, 1,
            Assert.Single(projection.Purchasing.Buyers).TemporaryCycleBudget.ProtectedResidual);
        Equal(3, 8, Assert.Single(projection.Alpha.Buyers).Alpha);
        Equal(40, 1,
            Assert.Single(projection.Alpha.Buyers).BaselineEstimatedSpending);
        Equal(15, 1, Assert.Single(projection.Alpha.Buyers).ScaledEstimatedSpending);
        Equal(8, 1,
            projection.DeliveredPrices[0].AbstractCommodityPerWholeFlowUnit);
    }

    [Fact]
    public void PolicyVersionAndInvalidScaleFactorsFailClosed()
    {
        var f = new Fixture();
        var other = new ExperimentalAbstractPurchasingPolicy(
            f.Purchasing.PolicyId, 2, F(5), F(5, 8));
        Assert.Throws<InvalidOperationException>(() =>
            f.Preview(purchasing: other));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalAbstractMarketUnitCalibration(
                "", 1, f.Purchasing.PolicyId, 1, "A", "B", "C", F(1), F(1)));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalAbstractMarketUnitCalibration(
                "CAL", 0, f.Purchasing.PolicyId, 1,
                "A", "B", "C", F(1), F(1)));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalAbstractMarketUnitCalibration(
                "CAL", 1, f.Purchasing.PolicyId, 1,
                "A", "B", "C", F(0), F(1)));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalAbstractMarketUnitCalibration(
                "CAL", 1, f.Purchasing.PolicyId, 1,
                "A", "B", "C", F(1), F(0)));
    }

    [Fact]
    public void FreightMustMatchAnOfferedRouteCommodityServiceAndRuleset()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() =>
            f.Preview(freight: [
                f.Freight[0] with { ServiceId = 999 }, f.Freight[1]
            ]));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(freight: [
                f.Freight[0] with { PricingRulesetVersion = 99 }, f.Freight[1]
            ]));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(freight: [
                f.Freight[0] with { Commodity = Stone }, f.Freight[1]
            ]));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(freight: [
                f.Freight[0] with { Source = Buyer }, f.Freight[1]
            ]));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(freight: [f.Freight[0]]));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(freight: [
                f.Freight[0], f.Freight[0], f.Freight[1]
            ]));
    }

    [Fact]
    public void CommodityQuoteCoverageAndPhysicalProvenanceCannotBeInvented()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() =>
            f.Preview(prices: [f.Prices[0]]));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(prices: [
                f.Prices[0], f.Prices[0], f.Prices[1]
            ]));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(buyers: []));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(producers: []));
        Assert.Throws<ArgumentException>(() =>
            f.Preview(lanes: []));
    }

    [Fact]
    public void OutputIsDeterministicAcrossReorderedQuotesAndDoesNotCreateCarriers()
    {
        var f = new Fixture();
        var a = f.Preview();
        var b = f.Preview(prices: f.Prices.Reverse().ToArray(),
            freight: f.Freight.Reverse().ToArray(),
            lanes: f.Lanes.Reverse().ToArray());
        Assert.Equal(a.DeliveredPrices, b.DeliveredPrices);
        Assert.Equal(a.Alpha.ProjectedDemand.Select(x => x.RawQuantity),
            b.Alpha.ProjectedDemand.Select(x => x.RawQuantity));
        Assert.Equal(a.Purchasing.UnresolvedCarriers,
            b.Purchasing.UnresolvedCarriers);
        Assert.All(a.UnresolvedFreightBeneficiaries,
            x => Assert.True(x.ServiceId > 0));
        Assert.Equal(0UL, a.Purchasing.TravellingWorkers);
    }

    [Fact]
    public void FractionalConversionIsExactAndPreservesProtectedResidual()
    {
        var f = new Fixture();
        var fractional = new ExperimentalAbstractMarketUnitCalibration(
            "FRACTIONAL", 1, f.Purchasing.PolicyId,
            f.Purchasing.Version, "ABSTRACT", "GOODS", "FREIGHT",
            F(1, 3), F(1, 4));
        var result = f.Preview(calibration: fractional);
        var row = Assert.Single(result.Alpha.Buyers);
        Equal(1, 1, row.Alpha);
        Equal(22, 3, row.BaselineEstimatedSpending);
        Equal(22, 3, row.ScaledEstimatedSpending);
        Equal(35, 1, result.Purchasing.Buyers[0]
            .TemporaryCycleBudget.SpendableCash);
        Equal(4, 3, result.DeliveredPrices[0]
            .AbstractCommodityPerWholeFlowUnit);
        Equal(1, 2, result.DeliveredPrices[0]
            .AbstractFreightPerWholeFlowUnit);
    }
}
