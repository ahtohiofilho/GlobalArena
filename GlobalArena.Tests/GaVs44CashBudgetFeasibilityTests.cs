using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs44CashBudgetFeasibilityTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly GoldbergParameters Fine = new(18, 0);
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Stone = new(8);
    private static readonly ExperimentalFreightTechnologyPricing PriceCalibration = new(
        101, 7, 44, ExperimentalLandInfrastructureKind.StoneRoad,
        1, 0, 0, 0, 1_000_000, 0, 1_000_000);

    private sealed class ConstantValue(ulong price) : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong allocated) =>
            new(price, 1);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong allocated) =>
            new((BigInteger)price * allocated, Unit);
    }

    private static FoodTransportCostFraction Money(long value) => new(value, 1);

    private static ExperimentalSharedFreightCorridorKey Corridor(ulong id) =>
        new(new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(id)), 101, 7);

    private static ExperimentalSharedFreightOption Option(
        ulong source, ulong buyer, ulong service,
        ulong corridorId) => ExperimentalSharedFreightOption.Synthetic(
            service, new StrategicEconomicRouteKey(
                new EconomicPointId(source), new EconomicPointId(buyer)),
            PriceCalibration, 1_000_000, new[] { Corridor(corridorId) });

    private sealed class Fixture
    {
        public EconomicProductionDemandResult Flows { get; }
        public FoodCommodityCatalog Catalog { get; }
        public EconomicTurnCommodityValueOffer[] Values { get; }
        public EconomicTurnSourceReservation[] Reservations { get; }
        public ExperimentalSimultaneousFreightLane[] Lanes { get; }

        public Fixture(ulong buyerValue = 100, bool sharedCorridor = false,
            bool multipleCommodities = false)
        {
            var s1 = new EconomicSupplyFlow(new EconomicPointId(1),
                Food, EconomicActivityKind.Agriculture, Unit);
            var s2 = new EconomicSupplyFlow(new EconomicPointId(2),
                multipleCommodities ? Stone : Food,
                multipleCommodities ? EconomicActivityKind.Mining :
                    EconomicActivityKind.Agriculture, Unit);
            var d1 = new EconomicDemandFlow(new EconomicPointId(3), Food,
                multipleCommodities ? Unit : 2 * Unit);
            var demands = new List<EconomicDemandFlow> { d1 };
            if (multipleCommodities)
                demands.Add(new EconomicDemandFlow(new EconomicPointId(3),
                    Stone, Unit));

            Flows = new EconomicProductionDemandResult(new[] { s1, s2 }, demands);
            Catalog = new FoodCommodityCatalog(multipleCommodities
                ? new[] { new FoodCommodityDefinition(Food, "food", 1),
                    new FoodCommodityDefinition(Stone, "stone", 1) }
                : new[] { new FoodCommodityDefinition(Food, "food", 1) });
            Values = demands.Select(d => new EconomicTurnCommodityValueOffer(
                d.EconomicPointId, d.CommodityId,
                new ConstantValue(buyerValue))).ToArray();
            Reservations = new[] {
                new EconomicTurnSourceReservation(s1.EconomicPointId,
                    s1.CommodityId, s1.Activity, 0),
                new EconomicTurnSourceReservation(s2.EconomicPointId,
                    s2.CommodityId, s2.Activity, 0)
            };
            Lanes = new[] {
                new ExperimentalSimultaneousFreightLane(s1, d1,
                    new[] { Option(1, 3, 10, 1) }),
                new ExperimentalSimultaneousFreightLane(s2,
                    multipleCommodities ? demands[1] : d1,
                    new[] { Option(2, 3, 20, sharedCorridor ? 1UL : 2UL) })
            };
        }

        public ExperimentalSimultaneousFreightComparison Solve(
            long gross, long reserve = 0, long foodPrice = 10,
            long stonePrice = 20, long saturation = 0,
            int exact = 100, bool withCash = true)
        {
            var buyer = new EconomicPointId(3);
            var budget = withCash ? new ExperimentalBuyerCashSnapshot(
                new[] { new ExperimentalBuyerCashBudget(
                    buyer, Money(gross), Money(reserve)) },
                Flows.DemandFlows.Select(d =>
                    new ExperimentalBuyerCommodityPrice(buyer,
                        d.CommodityId,
                        Money(d.CommodityId == Stone ? stonePrice : foodPrice))))
                : null;
            return new ExperimentalSimultaneousFreightReference().Compare(
                Flows, Catalog, Values, Reservations, Lanes,
                new ExperimentalSharedFreightCongestionPolicy(
                    saturation, 1_000_000),
                Unit, maxLocalPasses: 5, maxExactConfigurations: exact,
                cashSnapshot: budget);
        }
    }

    [Fact]
    public void ABuyerCanPurchaseOneButNotTwoUnitsWithElevenUnitsPerDelivery()
    {
        var f = new Fixture();
        var constrained = f.Solve(gross: 21);
        Assert.NotNull(constrained.Exact);
        Assert.Single(constrained.Exact!.Trades);
        Assert.Equal(Unit, constrained.Exact.Trades[0].RawQuantity);
        var ledger = Assert.Single(constrained.Exact.CashLedger);
        Assert.Equal(0, ledger.CommodityCharges.CompareTo(Money(10)));
        Assert.Equal(0, ledger.FreightCharges.CompareTo(Money(1)));
        Assert.Equal(0, ledger.TotalCommitment.CompareTo(Money(11)));
        Assert.Equal(0, ledger.RemainingSpendableCash.CompareTo(Money(10)));
        Assert.True(constrained.Coordinated.NetEconomicValue.CompareTo(
            constrained.Exact.NetEconomicValue) <= 0);
    }

    [Fact]
    public void ProtectedResidualHNeverBecomesAvailablePurchasingPower()
    {
        var f = new Fixture();
        var noTrade = f.Solve(gross: 15, reserve: 5);
        Assert.Empty(noTrade.Exact!.Trades);
        Assert.Equal(0, Assert.Single(noTrade.Exact.CashLedger)
            .RemainingSpendableCash.CompareTo(Money(10)));

        var one = f.Solve(gross: 16, reserve: 5);
        Assert.Single(one.Exact!.Trades);
        Assert.Equal(0, Assert.Single(one.Exact.CashLedger)
            .RemainingSpendableCash.CompareTo(Money(0)));

        var two = f.Solve(gross: 27, reserve: 5);
        Assert.Equal(2, two.Exact!.Trades.Count);
        Assert.Equal(0, Assert.Single(two.Exact.CashLedger)
            .RemainingSpendableCash.CompareTo(Money(0)));
    }

    [Fact]
    public void ExactCorridorLoadAllocationDeterminesAffordabilityWithoutDoubleCharging()
    {
        var f = new Fixture(sharedCorridor: true);
        var tooLittle = f.Solve(gross: 101, saturation: 10);
        Assert.Single(tooLittle.Exact!.Trades);
        var full = f.Solve(gross: 102, saturation: 10);
        Assert.Equal(2, full.Exact!.Trades.Count);
        var ledger = Assert.Single(full.Exact.CashLedger);
        Assert.Equal(0, ledger.FreightCharges.CompareTo(Money(82)));
        Assert.Equal(0, ledger.CommodityCharges.CompareTo(Money(20)));
        Assert.Equal(0, ledger.TotalCommitment.CompareTo(Money(102)));
        Assert.Equal(0, ledger.RemainingSpendableCash.CompareTo(Money(0)));
        Assert.Equal(0, ledger.FreightCharges.CompareTo(
            full.Exact.SharedFreightCost));
        Assert.Equal(new BigInteger(2_000_000),
            full.Exact.CorridorLoads[Corridor(1)]);
    }

    [Fact]
    public void TwoCommoditiesCompeteForTheSameHouseholdCashNotSeparateItemWallets()
    {
        var f = new Fixture(multipleCommodities: true);
        var one = f.Solve(gross: 22);
        Assert.Single(one.Exact!.Trades);
        Assert.Equal(0, Assert.Single(one.Exact.CashLedger)
            .TotalCommitment.CompareTo(Money(11)));

        var both = f.Solve(gross: 32);
        Assert.Equal(2, both.Exact!.Trades.Count);
        Assert.Equal(2, both.Exact.Trades.Select(x => x.Commodity).Distinct().Count());
        var ledger = Assert.Single(both.Exact.CashLedger);
        Assert.Equal(0, ledger.CommodityCharges.CompareTo(Money(30)));
        Assert.Equal(0, ledger.FreightCharges.CompareTo(Money(2)));
        Assert.Equal(0, ledger.RemainingSpendableCash.CompareTo(Money(0)));
    }

    [Fact]
    public void TheCounterfactualWorksWithoutCashAndNeverInventsFutureIncome()
    {
        var f = new Fixture();
        var unrestricted = f.Solve(gross: 0, withCash: false);
        var limited = f.Solve(gross: 0);
        Assert.Empty(limited.Exact!.Trades);
        Assert.Equal(2, unrestricted.Exact!.Trades.Count);
        Assert.Empty(unrestricted.Exact.CashLedger);
        Assert.Equal(0, limited.Exact.NetEconomicValue.Numerator.Sign);
        Assert.Equal(0, Assert.Single(limited.Exact.CashLedger)
            .GrossCash.CompareTo(Money(0)));
    }

    [Fact]
    public void ProportionalCongestionChargesReconcileAcrossSeparateBuyers()
    {
        var key = Corridor(1);
        var optionA = Option(1, 3, 10, 1);
        var optionB = Option(2, 4, 20, 1);
        var deliveries = new[] {
            new ExperimentalBuyerCashDelivery(new EconomicPointId(3), Food,
                Unit, 1_000_000, optionA),
            new ExperimentalBuyerCashDelivery(new EconomicPointId(4), Food,
                Unit, 1_000_000, optionB)
        };
        var snapshot = new ExperimentalBuyerCashSnapshot(
            new[] { new ExperimentalBuyerCashBudget(new EconomicPointId(3),
                        Money(51), Money(0)),
                new ExperimentalBuyerCashBudget(new EconomicPointId(4),
                        Money(51), Money(0)) },
            new[] { new ExperimentalBuyerCommodityPrice(
                        new EconomicPointId(3), Food, Money(10)),
                    new ExperimentalBuyerCommodityPrice(
                        new EconomicPointId(4), Food, Money(10)) });
        var cash = snapshot.Evaluate(deliveries,
            new Dictionary<ExperimentalSharedFreightCorridorKey, BigInteger>
            { [key] = 2_000_000 }, new(10, 1_000_000));
        Assert.True(cash.CanSettleAtSight);
        Assert.Equal(2, cash.Buyers.Count);
        Assert.All(cash.Buyers, ledger => {
            Assert.Equal(0, ledger.FreightCharges.CompareTo(Money(41)));
            Assert.Equal(0, ledger.RemainingSpendableCash.CompareTo(Money(0)));
        });
        Assert.Equal(0, cash.TotalBuyerFreight.CompareTo(Money(82)));
    }

    [Fact]
    public void MissingDuplicateOrAlienAccountsPricesAndInsufficientReserveFailClosed()
    {
        var f = new Fixture();
        var buyer = new EconomicPointId(3);
        var price = new ExperimentalBuyerCommodityPrice(buyer, Food, Money(10));
        var budget = new ExperimentalBuyerCashBudget(buyer, Money(20), Money(2));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalBuyerCashBudget(buyer, Money(3), Money(4)));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalBuyerCashSnapshot(new[] { budget, budget },
                new[] { price }));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalBuyerCashSnapshot(new[] { budget },
                new[] { price, price }));
        var noPrice = new ExperimentalBuyerCashSnapshot(
            new[] { budget }, Array.Empty<ExperimentalBuyerCommodityPrice>());
        Assert.Throws<ArgumentException>(() =>
            noPrice.ValidateCoverage(f.Flows));
        var noBudget = new ExperimentalBuyerCashSnapshot(
            Array.Empty<ExperimentalBuyerCashBudget>(), new[] { price });
        Assert.Throws<ArgumentException>(() =>
            noBudget.ValidateCoverage(f.Flows));
        var snapshot = new ExperimentalBuyerCashSnapshot(
            new[] { budget }, new[] { price });
        Assert.Throws<ArgumentException>(() => snapshot.Evaluate(
            new[] { new ExperimentalBuyerCashDelivery(
                buyer, Food, 0, 1_000_000, Option(1, 3, 10, 1)) },
            new Dictionary<ExperimentalSharedFreightCorridorKey, BigInteger>(),
            new(0, 1_000_000)));
    }

    [Fact]
    public void CashConstrainedAllocationsRepeatIdenticallyUnderReorderedInputs()
    {
        var f = new Fixture(sharedCorridor: true);
        var buyer = new EconomicPointId(3);
        var cash = new ExperimentalBuyerCashSnapshot(
            new[] { new ExperimentalBuyerCashBudget(
                buyer, Money(102), Money(0)) },
            new[] { new ExperimentalBuyerCommodityPrice(
                buyer, Food, Money(10)) });
        var a = f.Solve(gross: 102, saturation: 10);
        var b = new ExperimentalSimultaneousFreightReference().Compare(
            f.Flows, f.Catalog, f.Values.Reverse(),
            f.Reservations.Reverse(), f.Lanes.Reverse(),
            new ExperimentalSharedFreightCongestionPolicy(10, 1_000_000),
            Unit, maxLocalPasses: 5, maxExactConfigurations: 100,
            cashSnapshot: cash);
        Assert.Equal(a.Exact!.Trades.ToArray(), b.Exact!.Trades.ToArray());
        Assert.Equal(a.Coordinated.Trades.ToArray(), b.Coordinated.Trades.ToArray());
        Assert.Equal(a.Exact.NetEconomicValue, b.Exact.NetEconomicValue);
        Assert.Equal(a.Exact.CashLedger.ToArray(), b.Exact.CashLedger.ToArray());
    }
}
