using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs54AbstractPhysicalFreightReconciliationTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private const string QuoteUnit = "VS40-NOMINAL-FREIGHT";
    private static readonly EconomicPointId Buyer = new(101);
    private static readonly EconomicPointId BuyerTwo = new(102);
    private static readonly EconomicPointId Supplier = new(201);
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Stone = new(8);
    private static readonly GoldbergParameters Fine = new(18, 0);
    private static readonly ExperimentalSharedFreightCongestionPolicy Shared =
        new(1, 1_000_000);

    private static FoodTransportCostFraction F(long numerator, long denominator = 1) =>
        new(numerator, denominator);

    private static void Equal(long numerator, long denominator,
        FoodTransportCostFraction value) =>
        Assert.Equal(0, value.CompareTo(F(numerator, denominator)));

    private static CanonicalPhysicalTacticalSession Game() =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0), Fine,
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C,
            PhysicalFoodMarketMode.LocalCashSettlement,
            PhysicalWorkerMovementMode.TimedAdjacent,
            PhysicalCivilizationIdentityMode.BornWithPopulation);

    private static ExperimentalSharedFreightCorridorKey Corridor(ulong edge) =>
        new(new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(edge)), 101, 7);

    private static ExperimentalSharedFreightOption Option(
        ulong service, EconomicPointId buyer, ulong edge = 1) =>
        ExperimentalSharedFreightOption.Synthetic(
            service, new StrategicEconomicRouteKey(Supplier, buyer),
            new ExperimentalFreightTechnologyPricing(
                101, 7, 54, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 1_000, 1_000_000),
            1_000_000, new[] { Corridor(edge) });

    private static ExperimentalAbstractPriceFreightCalibrationPolicy Policy(
        FoodTransportCostFraction? scale = null) =>
        new("VS54-CALIBRATION", 1, "VS54-PURCHASING", 1, QuoteUnit,
            scale ?? F(1));

    private static ExperimentalAbstractNominalDeliveredQuote Quote(
        EconomicPointId buyer, CommodityId commodity,
        FoodTransportCostFraction? price = null,
        FoodTransportCostFraction? forecast = null) =>
        new(buyer, commodity, price ?? F(1), forecast ?? F(1), QuoteUnit);

    private static FoodCommodityCatalog Catalog() =>
        new(new[] {
            new FoodCommodityDefinition(Food, "food", 1),
            new FoodCommodityDefinition(Stone, "stone", 1)
        });

    private sealed class Fixture
    {
        public CanonicalPhysicalTacticalSession Game { get; } = Game();
        public EconomicProductionDemandResult Flows { get; }
        public ExperimentalAbstractPurchasingProjection Purchasing { get; }
        public ExperimentalAbstractNominalDeliveredQuote[] Quotes { get; }
        public ExperimentalAbstractPhysicalFreightDelivery[] Deliveries { get; }

        public Fixture(bool twoBuyers = false, bool sharedCorridor = true,
            ulong quantity = Unit)
        {
            if (twoBuyers)
            {
                var target = Game.AdjacentWorkable(Game.InitialTile)[0];
                Game.OrderWorkerTransit(Game.InitialTile, target,
                    EconomicActivityKind.Agriculture,
                    EconomicActivityKind.Forestry, 2);
                Game.AdvanceMovementTurn();
                Game.AdvanceMovementTurn();
            }

            var offers = new[] {
                new EconomicSupplyFlow(Supplier, Food,
                    EconomicActivityKind.Agriculture, 2 * Unit)
            };
            var demands = twoBuyers
                ? new[] {
                    new EconomicDemandFlow(Buyer, Food, Unit),
                    new EconomicDemandFlow(BuyerTwo, Food, Unit)
                }
                : new[] {
                    new EconomicDemandFlow(Buyer, Food, Unit),
                    new EconomicDemandFlow(Buyer, Stone, Unit)
                };
            if (!twoBuyers)
                offers = new[] {
                    new EconomicSupplyFlow(Supplier, Food,
                        EconomicActivityKind.Agriculture, Unit),
                    new EconomicSupplyFlow(Supplier, Stone,
                        EconomicActivityKind.Mining, Unit)
                };
            if (quantity != Unit && !twoBuyers)
            {
                offers = new[] {
                    new EconomicSupplyFlow(Supplier, Food,
                        EconomicActivityKind.Agriculture, Unit),
                    new EconomicSupplyFlow(Supplier, Stone,
                        EconomicActivityKind.Mining, Unit)
                };
            }

            Flows = new EconomicProductionDemandResult(offers, demands);
            var first = Option(17, Buyer);
            var second = Option(18, twoBuyers ? BuyerTwo : Buyer,
                sharedCorridor ? 1UL : 2UL);
            var lanes = twoBuyers
                ? new[] {
                    new ExperimentalSimultaneousFreightLane(offers[0], demands[0],
                        new[] { first }),
                    new ExperimentalSimultaneousFreightLane(offers[0], demands[1],
                        new[] { second })
                }
                : new[] {
                    new ExperimentalSimultaneousFreightLane(offers[0], demands[0],
                        new[] { first }),
                    new ExperimentalSimultaneousFreightLane(offers[1], demands[1],
                        new[] { second })
                };

            var buyerAnchors = twoBuyers
                ? new[] {
                    new ExperimentalAbstractBuyerAnchor(
                        Buyer, Game.InitialTile, F(1)),
                    new ExperimentalAbstractBuyerAnchor(
                        BuyerTwo, Game.AdjacentWorkable(Game.InitialTile)[0], F(1))
                }
                : new[] {
                    new ExperimentalAbstractBuyerAnchor(
                        Buyer, Game.InitialTile, F(1))
                };
            var prices = demands.Select(x => new ExperimentalBuyerCommodityPrice(
                x.EconomicPointId, x.CommodityId, F(1))).ToArray();
            Purchasing = new ExperimentalAbstractPurchasingPowerProjector()
                .Project(Game, Flows, buyerAnchors,
                    new[] { new ExperimentalAbstractProducerAnchor(
                        Supplier, Game.InitialTile) },
                    lanes, prices, new ExperimentalAbstractPurchasingPolicy(
                        "VS54-PURCHASING", 1, F(5), F(5, 8)));

            Quotes = demands.Select(x =>
                Quote(x.EconomicPointId, x.CommodityId)).ToArray();
            Deliveries = twoBuyers
                ? new[] {
                    new ExperimentalAbstractPhysicalFreightDelivery(
                        1, Supplier, EconomicActivityKind.Agriculture,
                        Buyer, Food, quantity, first),
                    new ExperimentalAbstractPhysicalFreightDelivery(
                        2, Supplier, EconomicActivityKind.Agriculture,
                        BuyerTwo, Food, quantity, second)
                }
                : new[] {
                    new ExperimentalAbstractPhysicalFreightDelivery(
                        1, Supplier, EconomicActivityKind.Agriculture,
                        Buyer, Food, quantity, first),
                    new ExperimentalAbstractPhysicalFreightDelivery(
                        2, Supplier, EconomicActivityKind.Mining,
                        Buyer, Stone, quantity, second)
                };
        }

        public ExperimentalAbstractPhysicalFreightReconciliation Run(
            ExperimentalAbstractPhysicalFreightDelivery[]? deliveries = null,
            ExperimentalAbstractPriceFreightCalibrationPolicy? policy = null,
            string freightUnit = QuoteUnit,
            ExperimentalAbstractNominalDeliveredQuote[]? quotes = null,
            FoodCommodityCatalog? catalog = null) =>
            new ExperimentalAbstractPhysicalFreightReconciler().Project(
                Purchasing, Flows, catalog ?? Catalog(), quotes ?? Quotes,
                policy ?? Policy(), freightUnit, deliveries ?? Deliveries, Shared);
    }

    [Fact]
    public void SharedCorridorReconcilesActualVs42OnceWithoutVs40StandaloneSaturation()
    {
        var fixture = new Fixture();
        var beforeSave = fixture.Game.ExportSaveJson();
        var beforeHash = fixture.Game.CurrentHash;
        var cash = fixture.Game.FoodCash!.At(fixture.Game.InitialTile)
            .HouseholdMicrocredits;

        var result = fixture.Run();
        Assert.Equal(2, result.Deliveries.Count);
        Assert.Single(result.CorridorLoads);
        Assert.Equal(2_000_000, result.CorridorLoads[Corridor(1)]);
        Equal(10, 1, result.NominalPhysicalFreight);
        Equal(10, 1, result.AbstractPhysicalFreight);
        Equal(2, 1, result.AbstractCommodityCharges);
        Equal(2, 1, result.AbstractEstimatedFreightForSelectedLots);
        Equal(12, 1, result.AbstractDeliveredCharges);
        Assert.All(result.Deliveries, row => {
            Equal(1, 1, row.NominalUncongestedFreight);
            Equal(4, 1, row.NominalSharedSaturationFreight);
            Equal(5, 1, row.AbstractPhysicalFreight);
            Assert.Equal(1_000_000, row.PhysicalLoadMicrograms);
        });
        var buyer = Assert.Single(result.Buyers);
        Equal(40, 1, buyer.GrossAbstractPurchasing);
        Equal(5, 1, buyer.ProtectedInvisibleH);
        Equal(35, 1, buyer.SpendableAbstractPurchasing);
        Equal(12, 1, buyer.AbstractDeliveredCharge);
        Equal(23, 1, buyer.RemainingIfAffordable);
        Assert.True(result.AllBuyersCanCoverHypotheticalCharges);
        Assert.False(result.CanAuthorizeRealSettlement);
        Assert.Equal(beforeSave, fixture.Game.ExportSaveJson());
        Assert.Equal(beforeHash, fixture.Game.CurrentHash);
        Assert.Equal(cash, fixture.Game.FoodCash.At(fixture.Game.InitialTile)
            .HouseholdMicrocredits);
        Assert.Empty(fixture.Game.Events);
    }

    [Fact]
    public void SeparateCorridorsChargeTheirOwnCongestionInsteadOfDoubleSharedPenalty()
    {
        var shared = new Fixture(sharedCorridor: true).Run();
        var separate = new Fixture(sharedCorridor: false).Run();
        Equal(10, 1, shared.NominalPhysicalFreight);
        Equal(4, 1, separate.NominalPhysicalFreight);
        Assert.Equal(2, separate.CorridorLoads.Count);
        Assert.All(separate.Deliveries, row => Equal(
            1, 1, row.NominalSharedSaturationFreight));
    }

    [Fact]
    public void ExactFractionalLoadsAndNominalConversionRetainMassAndCostConservation()
    {
        var fixture = new Fixture(quantity: Unit / 2);
        var result = fixture.Run(policy: Policy(F(3, 2)));
        Assert.Single(result.CorridorLoads);
        Assert.Equal(1_000_000, result.CorridorLoads[Corridor(1)]);
        Equal(2, 1, result.NominalPhysicalFreight);
        Equal(3, 1, result.AbstractPhysicalFreight);
        Equal(3, 2, result.AbstractCommodityCharges);
        Equal(3, 2, result.AbstractEstimatedFreightForSelectedLots);
        Equal(9, 2, result.AbstractDeliveredCharges);
        Assert.All(result.Deliveries, row => {
            Assert.Equal(500_000, row.PhysicalLoadMicrograms);
            Equal(1, 2, row.NominalUncongestedFreight);
            Equal(1, 2, row.NominalSharedSaturationFreight);
        });
        Assert.False(result.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void MultipleBuyersShareOneCorridorProportionallyAndNeverShareTheirBudgets()
    {
        var fixture = new Fixture(twoBuyers: true);
        var save = fixture.Game.ExportSaveJson();
        var hash = fixture.Game.CurrentHash;
        var result = fixture.Run();
        Assert.Equal(6UL, fixture.Purchasing.Buyers[0].ResidentWorkers);
        Assert.Equal(2UL, fixture.Purchasing.Buyers[1].ResidentWorkers);
        Equal(10, 1, result.NominalPhysicalFreight);
        Assert.Equal(2, result.Buyers.Count);
        Assert.All(result.Buyers, x => {
            Equal(5, 1, x.AbstractPhysicalFreight);
            Equal(1, 1, x.AbstractCommodityCharge);
            Equal(6, 1, x.AbstractDeliveredCharge);
            Assert.True(x.CanCoverHypotheticalCharge);
        });
        Equal(105, 4, result.Buyers[0].SpendableAbstractPurchasing);
        Equal(35, 4, result.Buyers[1].SpendableAbstractPurchasing);

        var doubled = fixture.Run(policy: Policy(F(2)));
        Equal(20, 1, doubled.AbstractPhysicalFreight);
        Equal(4, 1, doubled.AbstractCommodityCharges);
        Assert.True(doubled.Buyers[0].CanCoverHypotheticalCharge);
        Assert.False(doubled.Buyers[1].CanCoverHypotheticalCharge);
        Assert.False(doubled.AllBuyersCanCoverHypotheticalCharges);
        Equal(0, 1, doubled.Buyers[1].RemainingIfAffordable);
        Assert.False(doubled.CanAuthorizeRealSettlement);
        Assert.Equal(save, fixture.Game.ExportSaveJson());
        Assert.Equal(hash, fixture.Game.CurrentHash);
    }

    [Fact]
    public void UnaffordableHypotheticalBatchIsReportedButNeverSettledOrReducedSilently()
    {
        var f = new Fixture();
        var result = f.Run(policy: Policy(F(4)));
        Equal(40, 1, result.AbstractPhysicalFreight);
        Equal(8, 1, result.AbstractCommodityCharges);
        Equal(48, 1, result.AbstractDeliveredCharges);
        Assert.False(result.AllBuyersCanCoverHypotheticalCharges);
        var buyer = Assert.Single(result.Buyers);
        Assert.False(buyer.CanCoverHypotheticalCharge);
        Equal(0, 1, buyer.RemainingIfAffordable);
        Equal(35, 1, buyer.SpendableAbstractPurchasing);
        Assert.Equal(2, result.Deliveries.Count);
        Assert.False(result.CanAuthorizeRealSettlement);
    }

    [Fact]
    public void UnknownServiceUnitDuplicateLotOrOverAllocationFailsClosed()
    {
        var f = new Fixture();
        Assert.Throws<ArgumentException>(() => f.Run(
            freightUnit: "UNRELATED-QUOTE-UNIT"));
        Assert.Throws<ArgumentException>(() => f.Run(
            quotes: f.Quotes.Take(1).ToArray()));
        Assert.Throws<ArgumentException>(() => f.Run(
            deliveries: new[] { f.Deliveries[0], f.Deliveries[0] }));
        Assert.Throws<ArgumentException>(() => f.Run(
            deliveries: new[] {
                f.Deliveries[0] with { Service = Option(99, Buyer) }
            }));
        Assert.Throws<ArgumentException>(() => f.Run(
            deliveries: new[] {
                f.Deliveries[0] with { RawQuantity = 2 * Unit }
            }));
        Assert.Throws<ArgumentException>(() => f.Run(
            deliveries: new[] {
                f.Deliveries[0] with { Commodity = Stone }
            }));
        Assert.Throws<ArgumentException>(() => f.Run(
            deliveries: new[] {
                f.Deliveries[0] with { Source = new EconomicPointId(999) }
            }));
    }

    [Fact]
    public void ReorderedInputsProduceIdenticalSortedChargesAndEmptyBatchIsNotASettlement()
    {
        var f = new Fixture();
        var a = f.Run();
        var b = f.Run(deliveries: f.Deliveries.Reverse().ToArray(),
            quotes: f.Quotes.Reverse().ToArray());
        Assert.Equal(a.Deliveries.ToArray(), b.Deliveries.ToArray());
        Equal(10, 1, b.AbstractPhysicalFreight);
        var empty = f.Run(deliveries: []);
        Assert.Empty(empty.Deliveries);
        Equal(0, 1, empty.NominalPhysicalFreight);
        Equal(0, 1, empty.AbstractDeliveredCharges);
        Assert.True(empty.AllBuyersCanCoverHypotheticalCharges);
        Assert.False(empty.CanAuthorizeRealSettlement);
    }
}
