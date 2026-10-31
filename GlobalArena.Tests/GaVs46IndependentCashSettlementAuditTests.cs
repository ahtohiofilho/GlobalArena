using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs46IndependentCashSettlementAuditTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CommodityId Commodity = new(7);
    private static readonly GoldbergParameters Fine = new(18, 0);

    private sealed class FlatValue : IEconomicTurnCommodityValueCurve
    {
        public FoodTransportCostFraction EvaluateMarginalRawUnitValue(ulong allocated) =>
            new(100, 1);

        public FoodTransportCostFraction EvaluateIntegratedRawValue(ulong allocated) =>
            new((BigInteger)100 * allocated, Unit);
    }

    private sealed class Fixture
    {
        public EconomicProductionDemandResult Flows { get; }
        public FoodCommodityCatalog Catalog { get; }
        public ExperimentalSharedFreightCongestionPolicy Congestion { get; } =
            new(10, 1_000_000);
        public ExperimentalSimultaneousFreightLane[] Lanes { get; }
        public ExperimentalBuyerCashBudget[] Budgets { get; }
        public ExperimentalBuyerCommodityPrice[] Prices { get; }
        public ExperimentalSimultaneousFreightAllocation Allocation { get; }

        public Fixture(bool differentBuyers = false, long firstCash = 102,
            long protectedH = 0)
        {
            var s1 = new EconomicSupplyFlow(new EconomicPointId(1),
                Commodity, EconomicActivityKind.Agriculture, Unit);
            var s2 = new EconomicSupplyFlow(new EconomicPointId(2),
                Commodity, EconomicActivityKind.Agriculture, Unit);
            var buyer1 = new EconomicPointId(3);
            var buyer2 = differentBuyers ? new EconomicPointId(4) : buyer1;
            var d1 = new EconomicDemandFlow(buyer1, Commodity,
                differentBuyers ? Unit : 2 * Unit);
            var d2 = differentBuyers
                ? new EconomicDemandFlow(buyer2, Commodity, Unit) : d1;
            var demands = differentBuyers ? new[] { d1, d2 } : new[] { d1 };
            Flows = new EconomicProductionDemandResult(new[] { s1, s2 }, demands);
            Catalog = new FoodCommodityCatalog(new[] {
                new FoodCommodityDefinition(Commodity, "food", 1)
            });
            var cal = new ExperimentalFreightTechnologyPricing(
                101, 7, 46, ExperimentalLandInfrastructureKind.StoneRoad,
                1, 0, 0, 0, 1_000_000, 0, 1_000_000);
            var edge = new ExperimentalSharedFreightCorridorKey(
                new PhysicalTacticalEdgeId(Fine, new StrategicEdgeId(1)), 101, 7);
            ExperimentalSharedFreightOption Option(
                EconomicPointId source, EconomicPointId buyer, ulong service) =>
                ExperimentalSharedFreightOption.Synthetic(
                    service, new StrategicEconomicRouteKey(source, buyer),
                    cal, 1_000_000, new[] { edge });

            Lanes = new[] {
                new ExperimentalSimultaneousFreightLane(s1, d1,
                    new[] { Option(s1.EconomicPointId, buyer1, 10) }),
                new ExperimentalSimultaneousFreightLane(s2, d2,
                    new[] { Option(s2.EconomicPointId, buyer2, 10) })
            };
            Budgets = demands.Select(d => new ExperimentalBuyerCashBudget(
                d.EconomicPointId,
                new FoodTransportCostFraction(
                    differentBuyers ? 51 : firstCash, 1),
                new FoodTransportCostFraction(
                    differentBuyers ? 0 : protectedH, 1))).ToArray();
            Prices = demands.Select(d => new ExperimentalBuyerCommodityPrice(
                d.EconomicPointId, Commodity, new FoodTransportCostFraction(10, 1)))
                .ToArray();
            var snapshot = new ExperimentalBuyerCashSnapshot(Budgets, Prices);
            var reference = new ExperimentalSimultaneousFreightReference();
            var comparison = reference.Compare(
                Flows, Catalog,
                demands.Select(x => new EconomicTurnCommodityValueOffer(
                    x.EconomicPointId, Commodity, new FlatValue())),
                new[] {
                    new EconomicTurnSourceReservation(
                        s1.EconomicPointId, Commodity, s1.Activity, 0),
                    new EconomicTurnSourceReservation(
                        s2.EconomicPointId, Commodity, s2.Activity, 0)
                }, Lanes, Congestion, Unit,
                maxLocalPasses: 5, maxExactConfigurations: 16,
                cashSnapshot: snapshot);
            Allocation = comparison.Exact!;
        }

        public ExperimentalCashAuditResult Audit(
            ExperimentalSimultaneousFreightAllocation? candidate = null,
            IEnumerable<ExperimentalBuyerCashBudget>? budgets = null,
            IEnumerable<ExperimentalBuyerCommodityPrice>? prices = null,
            IEnumerable<ExperimentalSimultaneousFreightLane>? lanes = null) =>
            new ExperimentalIndependentCashSettlementAuditor().Audit(
                candidate ?? Allocation, Flows, Catalog,
                lanes ?? Lanes, budgets ?? Budgets, prices ?? Prices, Congestion);
    }

    private static void Equal(long expected, FoodTransportCostFraction actual) =>
        Assert.Equal(0, actual.CompareTo(new FoodTransportCostFraction(expected, 1)));

    [Fact]
    public void WholeBatchIsBalancedAndRecipientsAreProducerAndCarrierNotTheBuyer()
    {
        var f = new Fixture();
        Assert.Equal(2, f.Allocation.Trades.Count);
        var result = f.Audit();
        Equal(102, result.TotalOpening);
        Equal(102, result.TotalClosing);
        Equal(20, result.TotalCommodityPurchases);
        Equal(82, result.TotalFreight);
        Assert.Equal(4, result.Transfers.Count);
        var buyer = ExperimentalCashAuditAccountKey.Household(new EconomicPointId(3));
        var source1 = ExperimentalCashAuditAccountKey.Producer(new EconomicPointId(1));
        var source2 = ExperimentalCashAuditAccountKey.Producer(new EconomicPointId(2));
        var carrier1 = ExperimentalCashAuditAccountKey.Operator(
            new EconomicPointId(1), new EconomicPointId(3), 10);
        var carrier2 = ExperimentalCashAuditAccountKey.Operator(
            new EconomicPointId(2), new EconomicPointId(3), 10);
        Equal(0, result.Closing[buyer]);
        Equal(10, result.Closing[source1]);
        Equal(10, result.Closing[source2]);
        Equal(41, result.Closing[carrier1]);
        Equal(41, result.Closing[carrier2]);
        Assert.All(result.Transfers, x => Assert.Equal(buyer, x.Debit));
        Assert.DoesNotContain(result.Transfers,
            x => x.Credit.Role == ExperimentalCashAuditAccountRole.Household);
        Assert.All(result.Transfers,
            x => Assert.True(x.Amount.Numerator.Sign > 0));
    }

    [Fact]
    public void MultipleBuyersPaySharedCorridorCostsWithoutCrossFinancing()
    {
        var f = new Fixture(differentBuyers: true);
        var result = f.Audit();
        Equal(102, result.TotalOpening);
        Equal(102, result.TotalClosing);
        Equal(82, result.TotalFreight);
        Assert.Equal(2, result.Buyers.Count);
        Assert.All(result.Buyers, x => {
            Equal(51, x.OpeningGrossCash);
            Equal(41, x.FreightDebits);
            Equal(10, x.CommodityDebits);
            Equal(0, x.ClosingGrossCash);
        });
        Assert.Equal(2, result.Transfers
            .Where(x => x.Kind == ExperimentalCashAuditTransferKind.Freight)
            .Select(x => x.Credit).Distinct().Count());
    }

    [Fact]
    public void ProtectedHRemainsInHouseholdAndCannotBeSpentAtEndOfCycle()
    {
        var f = new Fixture(firstCash: 107, protectedH: 5);
        var report = f.Audit();
        var buyer = Assert.Single(report.Buyers);
        Equal(5, buyer.ProtectedH);
        Equal(5, buyer.ClosingGrossCash);
        Equal(107, report.TotalOpening);
        Equal(107, report.TotalClosing);
        Assert.Throws<ArgumentException>(() => new Fixture(firstCash: 102,
            protectedH: 103));
    }

    [Fact]
    public void FakeFreightTotalsAndFabricatedSharedLoadsAreRejectedIndependently()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                SharedFreightCost = new FoodTransportCostFraction(81, 1)
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                CorridorLoads =
                    new Dictionary<ExperimentalSharedFreightCorridorKey, BigInteger>()
            }));
        var key = Assert.Single(f.Allocation.CorridorLoads).Key;
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                CorridorLoads =
                    new Dictionary<ExperimentalSharedFreightCorridorKey, BigInteger>
                    { [key] = 1_000_000 }
            }));
    }

    [Fact]
    public void FakeBuyerDebitAndMissingCashLedgerAreNotTrusted()
    {
        var f = new Fixture();
        var ledger = Assert.Single(f.Allocation.CashLedger);
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                CashLedger = new[] { ledger with {
                    FreightCharges = new FoodTransportCostFraction(1, 1)
                }}
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                CashLedger = Array.Empty<ExperimentalBuyerCashLedger>()
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                CashLedger = new[] { ledger, ledger }
            }));
    }

    [Fact]
    public void UnknownServiceDuplicateTradeAndDemandOversubscriptionFailClosed()
    {
        var f = new Fixture();
        var t = f.Allocation.Trades[0];
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                Trades = new[] { t with { ServiceId = 404 } }
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                Trades = new[] { t, t }
            }));
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(f.Allocation with {
                Trades = new[] { t with { RawQuantity = 3 * Unit } }
            }));
        Assert.Throws<ArgumentException>(() => f.Audit(
            prices: Array.Empty<ExperimentalBuyerCommodityPrice>()));
        Assert.Throws<ArgumentException>(() => f.Audit(
            budgets: Array.Empty<ExperimentalBuyerCashBudget>()));
        Assert.Throws<ArgumentException>(() => f.Audit(
            lanes: new[] { f.Lanes[0], f.Lanes[0] }));
    }

    [Fact]
    public void ReducingOpeningCashCannotBeOffsetBySameTurnSalesOrCarrierIncome()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() =>
            f.Audit(budgets: new[] {
                new ExperimentalBuyerCashBudget(new EconomicPointId(3),
                    new FoodTransportCostFraction(101, 1),
                    new FoodTransportCostFraction(0, 1))
            }));
        // A participant can also be a producer, but these are distinct
        // household and producer roles and same-turn revenue is not cash.
        var report = f.Audit();
        Assert.DoesNotContain(report.Transfers,
            t => t.Debit.Role != ExperimentalCashAuditAccountRole.Household);
    }

    [Fact]
    public void UnchangedCandidateAndReorderedIndependentInputsProduceSameJournal()
    {
        var f = new Fixture();
        var before = f.Allocation.Trades.ToArray();
        var original = f.Audit();
        var shuffled = f.Audit(lanes: f.Lanes.Reverse(),
            budgets: f.Budgets.Reverse(), prices: f.Prices.Reverse());
        Assert.Equal(original.Transfers.Select(x => x.Debit),
            shuffled.Transfers.Select(x => x.Debit));
        Assert.Equal(original.Transfers.Select(x => x.Credit),
            shuffled.Transfers.Select(x => x.Credit));
        Assert.Equal(original.Transfers.Select(x => x.Amount.Numerator),
            shuffled.Transfers.Select(x => x.Amount.Numerator));
        Assert.Equal(original.Transfers.Select(x => x.Amount.Denominator),
            shuffled.Transfers.Select(x => x.Amount.Denominator));
        Assert.Equal(before, f.Allocation.Trades);
    }
}
