using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G2: executable economic reference for two economic points of one civilization.
/// These are strategic anchors, not yet a one-to-one mapping to physical fine tiles.
/// Tests the accepted baseline; it does NOT attest a built road, transport service,
/// physically delivered goods, or completed construction.
/// </summary>
public sealed class G2NeolithicTwoPointMarketReferenceTests
{
    private const ulong Unit = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly CommodityId Food = new(1U);  // Fixture IDs, not canonical catalog IDs.
    private static readonly CommodityId Stone = new(2U);
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);

    private static EconomicMarketAllocationPolicy Policy() =>
        new(900_000UL, 900_000UL, 1, 1);

    private static EconomicTransferSignal Signal(
        EconomicPointId from, EconomicPointId to, ulong rawCost) =>
        new(new StrategicEconomicRouteKey(from, to), 1, rawCost, 10UL * Unit, 0UL);

    private static WorldState TwoPointWorld()
    {
        // A and B use the same runtime rules. Production specializes only because
        // their worker allocations differ. There is no City or terminal record.
        var economy = new EconomyRuntimeState(new[]
        {
            new EconomicPointRuntimeState(
                A, new CivilizationId(1UL), new StrategicCellId(1UL), 4UL,
                new[] { new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Agriculture, 4UL) }),
            new EconomicPointRuntimeState(
                B, new CivilizationId(1UL), new StrategicCellId(2UL), 3UL,
                new[] { new EconomicActivityWorkforceAllocation(
                    EconomicActivityKind.Mining, 3UL) })
        });

        return WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(
                new[] { new CivilizationId(1UL) }))
            .WithEconomyAndPopulation(economy, new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 4UL),
                new PopulationPointRuntimeState(B, 3UL)
            }));
    }

    private static EconomicProductionDemandResult Flows(WorldState world) =>
        new EconomicProductionDemandResolver().Resolve(
            world.Economy,
            new[]
            {
                new EconomicProductionPotential(
                    A, Food, EconomicActivityKind.Agriculture, 1_000_000L),
                new EconomicProductionPotential(
                    B, Stone, EconomicActivityKind.Mining, 1_000_000L)
            },
            new[]
            {
                new EconomicDemandProfile(B, Food, 1_000_000L),
                new EconomicDemandProfile(A, Stone, 500_000L)
            });

    [Fact]
    public void TwoPointsExchangeFoodAndStoneWithBaselineTransferAndNoTerminal()
    {
        var world = TwoPointWorld();
        var before = new CanonicalWorldStateHasher().Compute(world);
        var flows = Flows(world);

        Assert.Equal(4UL * Unit,
            Assert.Single(flows.SupplyFlows, x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(3UL * Unit,
            Assert.Single(flows.SupplyFlows, x => x.CommodityId == Stone).RawQuantity);

        // Legacy baseline transfer signals require neither terminal nor
        // infrastructure record. Physical route eligibility remains unproven here.
        var market = new EconomicMarketAllocationResolver().Resolve(
            flows, new[]
            {
                Signal(A, B, 100_000UL),
                Signal(B, A, 100_000UL)
            }, Policy());

        Assert.Equal(5UL * Unit, market.TotalRawAllocatedQuantity);
        Assert.Equal(2, market.Allocations.Count);
        Assert.Contains(market.Allocations, x =>
            x.SourceEconomicPointId == A &&
            x.DestinationEconomicPointId == B &&
            x.CommodityId == Food &&
            x.RawQuantity == 3UL * Unit);
        Assert.Contains(market.Allocations, x =>
            x.SourceEconomicPointId == B &&
            x.DestinationEconomicPointId == A &&
            x.CommodityId == Stone &&
            x.RawQuantity == 2UL * Unit);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void OpeningSecondConsumerMarketRaisesEndogenousOriginPriceInReferenceCase()
    {
        // This GA-PROP-001 solver is a hypothetical one-food market, distinct
        // from the historical banded production/market turn integration.
        var preferences = new EconomicFoodPreferenceParameters(0.1, 2.8, 1.0);
        var buyerA = new EconomicSingleVarietyBuyer(A, 10.0, 50.0, 0.0);
        var buyerB = new EconomicSingleVarietyBuyer(B, 12.0, 60.0, 0.3);
        var solver = new EconomicSingleVarietyOriginClearing();

        var local = solver.Resolve(8.0, new[] { buyerA }, preferences);
        var connected = solver.Resolve(8.0, new[] { buyerA, buyerB }, preferences);

        Assert.True(local.OriginPrice > 0.0);
        Assert.True(connected.OriginPrice > local.OriginPrice);
        Assert.True(connected.SellerReceipts > local.SellerReceipts);
        Assert.True(connected.Buyers.Single(x => x.PointId == B).PurchasedQuantity > 0.0);
        Assert.InRange(connected.Unsold, 0.0, 2e-7);
    }

    [Fact]
    public void LowerQuotedTransportCostRaisesNetReturnForSameDeliveredQuantity()
    {
        // Cost input varies exogenously: this is NOT a constructed-road command.
        var flows = new EconomicProductionDemandResult(
            new[] { new EconomicSupplyFlow(
                A, Food, EconomicActivityKind.Agriculture, 3UL * Unit) },
            new[] { new EconomicDemandFlow(B, Food, 3UL * Unit) });
        var resolver = new EconomicMarketAllocationResolver();
        var expensive = resolver.Resolve(
            flows, new[] { Signal(A, B, 600_000UL) }, Policy());
        var cheaper = resolver.Resolve(
            flows, new[] { Signal(A, B, 100_000UL) }, Policy());

        Assert.Equal(3UL * Unit, expensive.TotalRawAllocatedQuantity);
        Assert.Equal(expensive.TotalRawAllocatedQuantity,
            cheaper.TotalRawAllocatedQuantity);
        Assert.Equal(expensive.TotalRawGrossSettlementValue,
            cheaper.TotalRawGrossSettlementValue);
        Assert.True(cheaper.TotalRawNetSettlementReturn >
            expensive.TotalRawNetSettlementReturn);
    }

    [Fact]
    public void NoViableBuyerDoesNotProduceFictitiousFullSales()
    {
        var result = new EconomicSingleVarietyOriginClearing().Resolve(
            200.0,
            new[] { new EconomicSingleVarietyBuyer(A, 10.0, 2.0, 20.0) },
            new EconomicFoodPreferenceParameters(0.1, 2.8, 1.0));

        Assert.Equal(0.0, result.OriginPrice);
        Assert.Equal(0.0, result.Sold);
        Assert.Equal(200.0, result.Unsold);
        Assert.Equal(0.0, result.SellerReceipts);
    }

    [Fact]
    public void ReorderingBuyersDoesNotChangeEndogenousPriceOrQuantities()
    {
        var preferences = new EconomicFoodPreferenceParameters(0.1, 2.8, 1.0);
        var buyers = new[]
        {
            new EconomicSingleVarietyBuyer(A, 10.0, 50.0, 0.0),
            new EconomicSingleVarietyBuyer(B, 12.0, 60.0, 0.3)
        };
        var solver = new EconomicSingleVarietyOriginClearing();
        var ordered = solver.Resolve(8.0, buyers, preferences);
        var reversed = solver.Resolve(8.0, buyers.Reverse(), preferences);

        Assert.Equal(ordered.OriginPrice, reversed.OriginPrice);
        Assert.Equal(ordered.SellerReceipts, reversed.SellerReceipts);
        Assert.Equal(
            ordered.Buyers.Select(x => x.PurchasedQuantity),
            reversed.Buyers.Select(x => x.PurchasedQuantity));
    }
}
