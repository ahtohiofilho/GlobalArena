using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs45PhysicalCashAlphaProjectionTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CommodityId Food = new(7);
    private static readonly CommodityId Stone = new(8);
    private static readonly EconomicPointId Buyer = new(101);
    private static readonly EconomicPointId BuyerTwo = new(102);

    private static CanonicalPhysicalTacticalSession Game(
        PhysicalFoodMarketMode mode = PhysicalFoodMarketMode.LocalCashSettlement) =>
        CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0),
            PhysicalEconomicCadenceMode.AdaptiveThirty,
            PhysicalTacticalGeographyMode.Canonical01C, mode);

    private static EconomicProductionDemandResult Demands(
        ulong food = 3 * Unit, ulong stone = Unit) =>
        new(Array.Empty<EconomicSupplyFlow>(), new[] {
            new EconomicDemandFlow(Buyer, Food, food),
            new EconomicDemandFlow(Buyer, Stone, stone)
        });

    private static FoodTransportCostFraction Money(long v) => new(v, 1);
    private static ExperimentalBuyerCashBudget Budget(
        long gross = 40, long h = 5) =>
        new(Buyer, Money(gross), Money(h));

    private static ExperimentalDeliveredDemandEstimate Quote(
        CommodityId commodity, long price, long freight = 0,
        EconomicPointId? buyer = null) =>
        new(buyer ?? Buyer, commodity, Money(price), Money(freight));

    [Fact]
    public void TheCanonicalPhysicalV14CashAccountProjectsToAnExplicitBuyerWithoutIssuingMoney()
    {
        var game = Game();
        var before = game.ExportSaveJson();
        var hash = game.CurrentHash;
        var cash = game.FoodCash!;
        var result = new ExperimentalPhysicalCashBudgetBridge().Project(
            game, Demands(),
            new[] { new ExperimentalCashBuyerTileAnchor(Buyer, game.InitialTile) },
            new[] { new ExperimentalBuyerCommodityPrice(Buyer, Food, Money(1)),
                new ExperimentalBuyerCommodityPrice(Buyer, Stone, Money(1)) },
            new Dictionary<EconomicPointId, FoodTransportCostFraction>
            { [Buyer] = Money(5) },
            microcreditsPerEconomicUnit: PhysicalFoodCashMarketState.MonetaryScale);

        Assert.Equal(new BigInteger(320_000_000), result.VerifiedHouseholdMicrocredits);
        Assert.Equal(BigInteger.Zero, result.VerifiedProducerMicrocredits);
        Assert.Equal(cash.InitialMoneySupplyMicrocredits,
            (ulong)(result.VerifiedHouseholdMicrocredits +
                    result.VerifiedProducerMicrocredits));
        var b = Assert.Single(result.Budgets);
        Assert.Equal(Buyer, b.Buyer);
        Assert.Equal(0, b.GrossCash.CompareTo(Money(320)));
        Assert.Equal(0, b.SpendableCash.CompareTo(Money(315)));
        var snapshot = result.Snapshot;
        snapshot.ValidateCoverage(Demands());
        Assert.Equal(before, game.ExportSaveJson());
        Assert.Equal(hash, game.CurrentHash);
        Assert.Empty(game.Events);
    }

    [Fact]
    public void ProducerCashIsNotTreatAsSpendableHouseholdMoneyAfterARealFoodCycle()
    {
        var game = Game();
        game.AdvanceMovementTurn();
        var canonical = game.FoodCash!;
        var before = game.ExportSaveJson();
        var projection = new ExperimentalPhysicalCashBudgetBridge().Project(
            game, Demands(),
            new[] { new ExperimentalCashBuyerTileAnchor(Buyer, game.InitialTile) },
            new[] { new ExperimentalBuyerCommodityPrice(Buyer, Food, Money(10)),
                new ExperimentalBuyerCommodityPrice(Buyer, Stone, Money(10)) },
            new Dictionary<EconomicPointId, FoodTransportCostFraction>
            { [Buyer] = Money(0) }, PhysicalFoodCashMarketState.MonetaryScale);
        var account = canonical.At(game.InitialTile);
        Assert.True(account.ProducerMicrocredits > 0UL);
        Assert.Equal(0, Assert.Single(projection.Budgets).GrossCash.CompareTo(
            new FoodTransportCostFraction(account.HouseholdMicrocredits,
                PhysicalFoodCashMarketState.MonetaryScale)));
        Assert.Equal(new BigInteger(canonical.InitialMoneySupplyMicrocredits),
            projection.VerifiedProducerMicrocredits +
            projection.VerifiedHouseholdMicrocredits);
        Assert.Equal(before, game.ExportSaveJson());
    }

    [Fact]
    public void ExplicitMonetaryScaleIsNotGuessedFromMovementPointsOrCommodityUnits()
    {
        var game = Game();
        var anchors = new[] { new ExperimentalCashBuyerTileAnchor(
            Buyer, game.InitialTile) };
        var estimates = new[] {
            new ExperimentalBuyerCommodityPrice(Buyer, Food, Money(1)),
            new ExperimentalBuyerCommodityPrice(Buyer, Stone, Money(1))
        };
        var h = new Dictionary<EconomicPointId, FoodTransportCostFraction>
            { [Buyer] = Money(0) };
        var bridge = new ExperimentalPhysicalCashBudgetBridge();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bridge.Project(game, Demands(), anchors, estimates, h, 0UL));
        var half = bridge.Project(game, Demands(), anchors,
            estimates, h, 2_000_000UL);
        Assert.Equal(0, Assert.Single(half.Budgets).GrossCash.CompareTo(
            Money(160)));
        Assert.Equal(0, bridge.Project(game, Demands(), anchors,
            estimates, h, 1_000_000UL).Budgets[0].GrossCash.CompareTo(
            Money(320)));
    }

    [Fact]
    public void MissingOrAliasedBuyerAnchorsCannotSpendTheSameTileTwice()
    {
        var game = Game();
        var flows = new EconomicProductionDemandResult(
            Array.Empty<EconomicSupplyFlow>(), new[] {
                new EconomicDemandFlow(Buyer, Food, Unit),
                new EconomicDemandFlow(BuyerTwo, Food, Unit)
            });
        var prices = new[] {
            new ExperimentalBuyerCommodityPrice(Buyer, Food, Money(1)),
            new ExperimentalBuyerCommodityPrice(BuyerTwo, Food, Money(1))
        };
        var h = new Dictionary<EconomicPointId, FoodTransportCostFraction>
        { [Buyer] = Money(0), [BuyerTwo] = Money(0) };
        var bridge = new ExperimentalPhysicalCashBudgetBridge();
        Assert.Throws<ArgumentException>(() => bridge.Project(
            game, flows,
            new[] { new ExperimentalCashBuyerTileAnchor(Buyer, game.InitialTile),
                new ExperimentalCashBuyerTileAnchor(BuyerTwo, game.InitialTile) },
            prices, h, 1_000_000));
        Assert.Throws<ArgumentException>(() => bridge.Project(
            game, flows,
            new[] { new ExperimentalCashBuyerTileAnchor(Buyer, game.InitialTile) },
            prices, h, 1_000_000));
        Assert.Throws<ArgumentException>(() => bridge.Project(
            game, flows,
            new[] { new ExperimentalCashBuyerTileAnchor(Buyer, game.InitialTile),
                new ExperimentalCashBuyerTileAnchor(Buyer, game.InitialTile) },
            prices, h, 1_000_000));
        Assert.Throws<InvalidOperationException>(() => bridge.Project(
            Game(PhysicalFoodMarketMode.LegacyDirectConsumption), Demands(),
            new[] { new ExperimentalCashBuyerTileAnchor(Buyer, game.InitialTile) },
            prices.Take(1), h, 1_000_000));
    }

    [Fact]
    public void AlphaPreservesProtectedHAndScalesAllGoodsUsingOneWallet()
    {
        var requested = Demands();
        var plan = new ExperimentalCashDemandAlphaPlanner().Project(
            requested, new[] { Budget() },
            new[] { Quote(Food, 10), Quote(Stone, 10) });
        var row = Assert.Single(plan.Buyers);
        Assert.Equal(0, row.Alpha.CompareTo(
            new FoodTransportCostFraction(7, 8)));
        Assert.Equal(0, row.BaselineEstimatedSpending.CompareTo(Money(40)));
        Assert.Equal(0, row.SpendableBudget.CompareTo(Money(35)));
        Assert.Equal(0, row.ScaledEstimatedSpending.CompareTo(Money(35)));
        Assert.Equal(2, plan.ProjectedDemand.Count);
        Assert.Equal(2_625_000UL, plan.ProjectedDemand[0].RawQuantity);
        Assert.Equal(875_000UL, plan.ProjectedDemand[1].RawQuantity);
        Assert.True(row.ScaledEstimatedSpending.CompareTo(
            row.SpendableBudget) <= 0);
    }

    [Fact]
    public void FullBudgetGivesOneAlphaAndLeavesDemandExactlyUnchanged()
    {
        var demand = Demands();
        var plan = new ExperimentalCashDemandAlphaPlanner().Project(
            demand, new[] { Budget(gross: 60) },
            new[] { Quote(Food, 10), Quote(Stone, 10) });
        Assert.Equal(0, Assert.Single(plan.Buyers).Alpha.CompareTo(Money(1)));
        Assert.Equal(demand.DemandFlows.Select(x => x.RawQuantity),
            plan.ProjectedDemand.Select(x => x.RawQuantity));
    }

    [Fact]
    public void ZeroBudgetAndHighFreightNeverProduceNegativeOrFictionalDemand()
    {
        var zero = new ExperimentalCashDemandAlphaPlanner().Project(
            Demands(), new[] { Budget(gross: 5, h: 5) },
            new[] { Quote(Food, 10, 5), Quote(Stone, 10, 5) });
        Assert.Empty(zero.ProjectedDemand);
        Assert.Equal(0, Assert.Single(zero.Buyers).Alpha.CompareTo(Money(0)));
        var withFreight = new ExperimentalCashDemandAlphaPlanner().Project(
            Demands(), new[] { Budget() },
            new[] { Quote(Food, 10, 5), Quote(Stone, 10, 5) });
        Assert.Equal(0, Assert.Single(withFreight.Buyers).Alpha.CompareTo(
            new FoodTransportCostFraction(7, 12)));
        Assert.Equal(0, Assert.Single(withFreight.Buyers).ScaledEstimatedSpending.CompareTo(
            Money(35)));
    }

    [Fact]
    public void FractionalRawQuantitiesAreConservativelyTruncatedWithoutOverspending()
    {
        var flows = Demands(food: Unit, stone: Unit);
        var plan = new ExperimentalCashDemandAlphaPlanner().Project(
            flows, new[] { Budget(gross: 2, h: 0) },
            new[] { Quote(Food, 3), Quote(Stone, 3) });
        Assert.Equal(0, Assert.Single(plan.Buyers).Alpha.CompareTo(
            new FoodTransportCostFraction(1, 3)));
        Assert.All(plan.ProjectedDemand, x =>
            Assert.Equal(333_333UL, x.RawQuantity));
        Assert.True(plan.Buyers[0].ScaledEstimatedSpending.CompareTo(
            Money(2)) < 0);
    }

    [Fact]
    public void AlphaIsStableUnderInputReorderingAndDoesNotMutateTheSource()
    {
        var flows = Demands();
        var before = flows.DemandFlows.Select(x => x.RawQuantity).ToArray();
        var planner = new ExperimentalCashDemandAlphaPlanner();
        var normal = planner.Project(flows, new[] { Budget() },
            new[] { Quote(Food, 10), Quote(Stone, 10) });
        var reverse = planner.Project(flows, new[] { Budget() },
            new[] { Quote(Stone, 10), Quote(Food, 10) });
        Assert.Equal(normal.ProjectedDemand.Select(x => x.RawQuantity),
            reverse.ProjectedDemand.Select(x => x.RawQuantity));
        Assert.Equal(normal.Buyers[0].Alpha.Numerator,
            reverse.Buyers[0].Alpha.Numerator);
        Assert.Equal(before, flows.DemandFlows.Select(x => x.RawQuantity));
    }

    [Fact]
    public void MissingAndDuplicateDemandPricesFailClosed()
    {
        var planner = new ExperimentalCashDemandAlphaPlanner();
        Assert.Throws<ArgumentException>(() => planner.Project(
            Demands(), new[] { Budget() }, new[] { Quote(Food, 10) }));
        Assert.Throws<ArgumentException>(() => planner.Project(
            Demands(), new[] { Budget() },
            new[] { Quote(Food, 10), Quote(Food, 10), Quote(Stone, 10) }));
        Assert.Throws<ArgumentException>(() => planner.Project(
            Demands(), new[] { Budget(), Budget() },
            new[] { Quote(Food, 10), Quote(Stone, 10) }));
        Assert.Throws<ArgumentException>(() => planner.Project(
            Demands(), Array.Empty<ExperimentalBuyerCashBudget>(),
            new[] { Quote(Food, 10), Quote(Stone, 10) }));
        Assert.Throws<ArgumentException>(() =>
            new ExperimentalBuyerCashBudget(Buyer, Money(3), Money(5)));
    }
}
