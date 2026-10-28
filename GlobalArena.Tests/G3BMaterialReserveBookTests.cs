using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-B tests a candidate immutable, turn-indexed reserve book. It is not
/// authoritative WorldState persistence or completed construction.
/// </summary>
public sealed class G3BMaterialReserveBookTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly EconomicPointId C = new(3UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CivilizationId First = new(1UL);
    private static readonly CivilizationId Second = new(2UL);

    private static WorldState World() =>
        WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(new[] { First, Second }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(
                    A, First, new StrategicCellId(1UL), 2UL, new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Agriculture, 2UL)
                    }),
                new EconomicPointRuntimeState(
                    B, First, new StrategicCellId(2UL), 3UL, new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining, 3UL)
                    }),
                new EconomicPointRuntimeState(
                    C, Second, new StrategicCellId(3UL), 1UL, new[]
                    {
                        new EconomicActivityWorkforceAllocation(
                            EconomicActivityKind.Mining, 1UL)
                    })
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 2UL),
                new PopulationPointRuntimeState(B, 3UL),
                new PopulationPointRuntimeState(C, 1UL)
            }));

    private static EconomicDeliberateMaterialFlowResult Plan(
        WorldState world, CivilizationId owner,
        EconomicPointId point, CommodityId commodity,
        EconomicActivityKind activity, ulong production, ulong withheld,
        bool alsoBuy = false)
    {
        var flows = new EconomicProductionDemandResult(new[]
        {
            new EconomicSupplyFlow(point, commodity, activity, production)
        }, Array.Empty<EconomicDemandFlow>());

        return new EconomicDeliberateMaterialFlowResolver().Resolve(
            world, owner, flows,
            new[] { new EconomicMaterialOriginHoldOrder(
                point, commodity, activity, withheld) },
            alsoBuy
                ? new[] { new EconomicMaterialDestinationAcquisitionOrder(
                    point, commodity, U) }
                : Array.Empty<EconomicMaterialDestinationAcquisitionOrder>());
    }

    [Fact]
    public void OriginWithholdingAccumulatesAcrossTurnsWithoutChangingWorld()
    {
        var world = World();
        var hashBefore = new CanonicalWorldStateHasher().Compute(world);
        var planner = Plan(world, First, B, Stone,
            EconomicActivityKind.Mining, 3UL * U, U);
        var accrual = new EconomicMaterialReserveAccrualResolver();

        var turn1 = accrual.AccrueOriginHolds(
            world, First, 1UL, EconomicMaterialReserveBook.Empty, planner);
        var turn2 = accrual.AccrueOriginHolds(
            world, First, 2UL, turn1, planner);

        Assert.Equal(U, turn1.RawAvailable(B, Stone));
        Assert.Equal(2UL * U, turn2.RawAvailable(B, Stone));
        Assert.Equal(1UL, turn1.LastAppliedTurn(First));
        Assert.Equal(2UL, turn2.LastAppliedTurn(First));
        Assert.Equal(0UL, EconomicMaterialReserveBook.Empty.RawAvailable(B, Stone));
        Assert.Equal(hashBefore, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void DestinationAcquisitionOrderNeverCreditsUnprovenPurchase()
    {
        var world = World();
        var planned = Plan(world, First, A, Food,
            EconomicActivityKind.Agriculture, 2UL * U, U, alsoBuy: true);
        var result = new EconomicMaterialReserveAccrualResolver()
            .AccrueOriginHolds(world, First, 1UL,
                EconomicMaterialReserveBook.Empty, planned);

        Assert.Equal(U, result.RawAvailable(A, Food));
        Assert.Equal(0UL, result.RawAvailable(A, Stone));
        Assert.Single(planned.DestinationAcquisitions);
        Assert.Equal(U, Assert.Single(planned.MarketFlows.DemandFlows).RawQuantity);
    }

    [Fact]
    public void SameOrEarlierTurnIsRejectedButAnotherCivilizationMayApplySameTurn()
    {
        var world = World();
        var firstPlan = Plan(world, First, B, Stone,
            EconomicActivityKind.Mining, 3UL * U, U);
        var secondPlan = Plan(world, Second, C, Stone,
            EconomicActivityKind.Mining, U, U);
        var accrual = new EconomicMaterialReserveAccrualResolver();
        var first = accrual.AccrueOriginHolds(
            world, First, 5UL, EconomicMaterialReserveBook.Empty, firstPlan);

        Assert.Throws<InvalidOperationException>(() => accrual.AccrueOriginHolds(
            world, First, 5UL, first, firstPlan));
        Assert.Throws<InvalidOperationException>(() => accrual.AccrueOriginHolds(
            world, First, 4UL, first, firstPlan));

        var both = accrual.AccrueOriginHolds(
            world, Second, 5UL, first, secondPlan);
        Assert.Equal(U, both.RawAvailable(B, Stone));
        Assert.Equal(U, both.RawAvailable(C, Stone));
        Assert.Equal(5UL, both.LastAppliedTurn(First));
        Assert.Equal(5UL, both.LastAppliedTurn(Second));
    }

    [Fact]
    public void ConstructionQuoteReportsShortagesWithoutSpendingOrBuilding()
    {
        var world = World();
        var plan = Plan(world, First, B, Stone,
            EconomicActivityKind.Mining, 3UL * U, 2UL * U);
        var book = new EconomicMaterialReserveAccrualResolver()
            .AccrueOriginHolds(world, First, 1UL,
                EconomicMaterialReserveBook.Empty, plan);
        var evaluator = new EconomicConstructionMaterialQuoteResolver();

        var affordable = evaluator.Evaluate(world, First, book, new[]
        {
            new EconomicConstructionMaterialRequirement(B, Stone, U)
        });
        var expensive = evaluator.Evaluate(world, First, book, new[]
        {
            new EconomicConstructionMaterialRequirement(B, Stone, 3UL * U)
        });

        Assert.True(affordable.IsMateriallyAffordable);
        Assert.False(expensive.IsMateriallyAffordable);
        Assert.Equal(U, Assert.Single(expensive.Lines).RawShortage);
        Assert.Equal(2UL * U, book.RawAvailable(B, Stone));
    }

    [Fact]
    public void SameCommodityAtDifferentPointsCannotPayForConstructionRemotely()
    {
        var world = World();
        var book = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(A, Stone, 4UL * U)
        }, Array.Empty<EconomicMaterialReserveTurnCursor>());

        var quote = new EconomicConstructionMaterialQuoteResolver()
            .Evaluate(world, First, book, new[]
            {
                new EconomicConstructionMaterialRequirement(B, Stone, U)
            });

        Assert.False(quote.IsMateriallyAffordable);
        Assert.Equal(U, Assert.Single(quote.Lines).RawShortage);
        Assert.Equal(4UL * U, book.RawAvailable(A, Stone));
    }

    [Fact]
    public void ReserveBookCanonicalizesRowsAndRejectsDuplicates()
    {
        var first = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(B, Stone, U),
            new EconomicMaterialReserveBalance(A, Food, U)
        }, new[]
        {
            new EconomicMaterialReserveTurnCursor(Second, 2UL),
            new EconomicMaterialReserveTurnCursor(First, 1UL)
        });
        var second = new EconomicMaterialReserveBook(
            first.Balances.Reverse(), first.AppliedTurnCursors.Reverse());
        Assert.Equal(first.Balances.Select(x => x.EconomicPointId),
            second.Balances.Select(x => x.EconomicPointId));
        Assert.Equal(first.AppliedTurnCursors.Select(x => x.CivilizationId),
            second.AppliedTurnCursors.Select(x => x.CivilizationId));
        Assert.Throws<ArgumentException>(() => new EconomicMaterialReserveBook(
            new[]
            {
                new EconomicMaterialReserveBalance(B, Stone, U),
                new EconomicMaterialReserveBalance(B, Stone, U)
            }, Array.Empty<EconomicMaterialReserveTurnCursor>()));
    }

    [Fact]
    public void OverflowAndForeignPointAccrualAreRejected()
    {
        var world = World();
        var plan = Plan(world, First, B, Stone,
            EconomicActivityKind.Mining, 3UL * U, U);
        var prior = new EconomicMaterialReserveBook(new[]
        {
            new EconomicMaterialReserveBalance(B, Stone, ulong.MaxValue)
        }, Array.Empty<EconomicMaterialReserveTurnCursor>());

        Assert.Throws<OverflowException>(() =>
            new EconomicMaterialReserveAccrualResolver().AccrueOriginHolds(
                world, First, 1UL, prior, plan));

        var foreignPlan = Plan(world, Second, C, Stone,
            EconomicActivityKind.Mining, U, U);
        Assert.Throws<InvalidOperationException>(() =>
            new EconomicMaterialReserveAccrualResolver().AccrueOriginHolds(
                world, First, 1UL, EconomicMaterialReserveBook.Empty, foreignPlan));
    }

    [Fact]
    public void QuoteRejectsForeignPointAndDuplicateCostRequirements()
    {
        var world = World();
        var evaluator = new EconomicConstructionMaterialQuoteResolver();
        var req = new EconomicConstructionMaterialRequirement(B, Stone, U);
        Assert.Throws<InvalidOperationException>(() => evaluator.Evaluate(
            world, First, EconomicMaterialReserveBook.Empty, new[]
            {
                new EconomicConstructionMaterialRequirement(C, Stone, U)
            }));
        Assert.Throws<ArgumentException>(() => evaluator.Evaluate(
            world, First, EconomicMaterialReserveBook.Empty,
            new[] { req, req }));
    }
}
