using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// G3-A tests intent projection only, not persistent physical inventories,
/// realized purchases, construction or tile occupation.
/// </summary>
public sealed class G3DeliberateMaterialFlowTests
{
    private const ulong U = EconomicTransferPolicy.FixedPointDenominator;
    private static readonly EconomicPointId A = new(1UL);
    private static readonly EconomicPointId B = new(2UL);
    private static readonly CommodityId Food = new(1U);
    private static readonly CommodityId Stone = new(2U);
    private static readonly CivilizationId Owner = new(1UL);
    private readonly EconomicDeliberateMaterialFlowResolver _resolver = new();

    private static WorldState World() =>
        WorldState.CreateInitial()
            .WithCivilizations(new CivilizationRuntimeState(new[]
            {
                Owner, new CivilizationId(2UL)
            }))
            .WithEconomyAndPopulation(new EconomyRuntimeState(new[]
            {
                new EconomicPointRuntimeState(A, Owner, new StrategicCellId(1UL), 4UL,
                    new[] { new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Agriculture, 4UL) }),
                new EconomicPointRuntimeState(B, Owner, new StrategicCellId(2UL), 3UL,
                    new[] { new EconomicActivityWorkforceAllocation(
                        EconomicActivityKind.Mining, 3UL) }),
                new EconomicPointRuntimeState(new EconomicPointId(3UL),
                    new CivilizationId(2UL), new StrategicCellId(3UL), 0UL)
            }), new PopulationRuntimeState(new[]
            {
                new PopulationPointRuntimeState(A, 4UL),
                new PopulationPointRuntimeState(B, 3UL),
                new PopulationPointRuntimeState(new EconomicPointId(3UL), 0UL)
            }));

    private static EconomicProductionDemandResult Flows() => new(
        new[]
        {
            new EconomicSupplyFlow(A, Food, EconomicActivityKind.Agriculture, 4UL * U),
            new EconomicSupplyFlow(B, Stone, EconomicActivityKind.Mining, 3UL * U)
        },
        new[]
        {
            new EconomicDemandFlow(B, Food, 2UL * U),
            new EconomicDemandFlow(A, Stone, U)
        });

    private EconomicDeliberateMaterialFlowResult Plan(
        IEnumerable<EconomicMaterialOriginHoldOrder>? holds = null,
        IEnumerable<EconomicMaterialDestinationAcquisitionOrder>? purchases = null,
        WorldState? state = null,
        EconomicProductionDemandResult? flows = null) =>
        _resolver.Resolve(state ?? World(), Owner, flows ?? Flows(),
            holds ?? Array.Empty<EconomicMaterialOriginHoldOrder>(),
            purchases ?? Array.Empty<EconomicMaterialDestinationAcquisitionOrder>());

    [Fact]
    public void WithholdingProductionAndBuyingForDestinationReserveChangesMarketFlows()
    {
        var world = World();
        var hash = new CanonicalWorldStateHasher().Compute(world);
        var result = Plan(
            new[]
            {
                new EconomicMaterialOriginHoldOrder(A, Food,
                    EconomicActivityKind.Agriculture, U),
                new EconomicMaterialOriginHoldOrder(B, Stone,
                    EconomicActivityKind.Mining, 2UL * U)
            },
            new[]
            {
                new EconomicMaterialDestinationAcquisitionOrder(B, Food, U),
                new EconomicMaterialDestinationAcquisitionOrder(A, Stone, U)
            }, world);

        Assert.Equal(3UL * U, Assert.Single(result.MarketFlows.SupplyFlows,
            x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(U, Assert.Single(result.MarketFlows.SupplyFlows,
            x => x.CommodityId == Stone).RawQuantity);
        Assert.Equal(3UL * U, Assert.Single(result.MarketFlows.DemandFlows,
            x => x.CommodityId == Food).RawQuantity);
        Assert.Equal(2UL * U, Assert.Single(result.MarketFlows.DemandFlows,
            x => x.CommodityId == Stone).RawQuantity);

        // The legacy market receives the transformed flows without any stock
        // entries, free terminal or fictitious demand fulfillment.
        var trade = new EconomicMarketAllocationResolver().Resolve(
            result.MarketFlows, new[]
            {
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(A, B), 1, 100_000UL, 10UL * U, 0UL),
                new EconomicTransferSignal(
                    new StrategicEconomicRouteKey(B, A), 1, 100_000UL, 10UL * U, 0UL)
            }, new EconomicMarketAllocationPolicy(900_000UL, 900_000UL, 1, 1));

        Assert.Equal(4UL * U, trade.TotalRawAllocatedQuantity);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(world));
        Assert.Equal(2, result.OriginHolds.Count);
        Assert.Equal(2, result.DestinationAcquisitions.Count);
    }

    [Fact]
    public void EntireSourceHoldRemovesItsMarketOfferWithoutCreatingZeroQuantityFlow()
    {
        var result = Plan(new[]
        {
            new EconomicMaterialOriginHoldOrder(A, Food,
                EconomicActivityKind.Agriculture, 4UL * U)
        });
        Assert.DoesNotContain(result.MarketFlows.SupplyFlows,
            x => x.EconomicPointId == A);
        Assert.Single(result.MarketFlows.SupplyFlows);
        Assert.Equal(2, result.MarketFlows.DemandFlows.Count);
    }

    [Fact]
    public void AcquisitionCanCreateNewDemandWhenResidentsDoNotNormallyBuyThatGood()
    {
        var result = Plan(purchases: new[]
        {
            new EconomicMaterialDestinationAcquisitionOrder(B, Stone, U)
        });
        Assert.Equal(U, Assert.Single(result.MarketFlows.DemandFlows,
            x => x.EconomicPointId == B && x.CommodityId == Stone).RawQuantity);
        Assert.Equal(3, result.MarketFlows.DemandFlows.Count);
    }

    [Fact]
    public void MaterialOrdersAreCanonicalRegardlessOfInputOrder()
    {
        var holds = new[]
        {
            new EconomicMaterialOriginHoldOrder(B, Stone, EconomicActivityKind.Mining, U),
            new EconomicMaterialOriginHoldOrder(A, Food, EconomicActivityKind.Agriculture, U)
        };
        var buy = new[]
        {
            new EconomicMaterialDestinationAcquisitionOrder(B, Food, U),
            new EconomicMaterialDestinationAcquisitionOrder(A, Stone, U)
        };
        var forward = Plan(holds, buy);
        var reversed = Plan(holds.Reverse(), buy.Reverse());

        Assert.Equal(forward.MarketFlows.SupplyFlows.Select(x => x.RawQuantity),
            reversed.MarketFlows.SupplyFlows.Select(x => x.RawQuantity));
        Assert.Equal(forward.MarketFlows.DemandFlows.Select(x => x.RawQuantity),
            reversed.MarketFlows.DemandFlows.Select(x => x.RawQuantity));
        Assert.Equal(forward.OriginHolds.Select(x => x.EconomicPointId),
            reversed.OriginHolds.Select(x => x.EconomicPointId));
        Assert.Equal(forward.DestinationAcquisitions.Select(x => x.EconomicPointId),
            reversed.DestinationAcquisitions.Select(x => x.EconomicPointId));
    }

    [Fact]
    public void RejectsOverholdAndDuplicateMaterialOrders()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan(new[]
        {
            new EconomicMaterialOriginHoldOrder(A, Food,
                EconomicActivityKind.Agriculture, 5UL * U)
        }));
        var duplicate = new EconomicMaterialOriginHoldOrder(A, Food,
            EconomicActivityKind.Agriculture, U);
        Assert.Throws<ArgumentException>(() => Plan(new[] { duplicate, duplicate }));
        var buy = new EconomicMaterialDestinationAcquisitionOrder(B, Food, U);
        Assert.Throws<ArgumentException>(() => Plan(purchases: new[] { buy, buy }));
    }

    [Fact]
    public void RejectsForeignOrUnknownPointOrders()
    {
        Assert.Throws<InvalidOperationException>(() => Plan(purchases: new[]
        {
            new EconomicMaterialDestinationAcquisitionOrder(new EconomicPointId(3UL), Food, U)
        }));
        Assert.Throws<InvalidOperationException>(() => Plan(purchases: new[]
        {
            new EconomicMaterialDestinationAcquisitionOrder(new EconomicPointId(9UL), Food, U)
        }));
        Assert.Throws<ArgumentException>(() => Plan(new[]
        {
            new EconomicMaterialOriginHoldOrder(B, Food,
                EconomicActivityKind.Agriculture, U)
        }));
    }

    [Fact]
    public void RejectsDemandOverflowWithoutMutatingWorld()
    {
        var world = World();
        var baseline = new EconomicProductionDemandResult(
            Flows().SupplyFlows, new[]
            {
                new EconomicDemandFlow(B, Food, ulong.MaxValue)
            });
        var before = new CanonicalWorldStateHasher().Compute(world);
        Assert.Throws<OverflowException>(() => Plan(
            purchases: new[] { new EconomicMaterialDestinationAcquisitionOrder(
                B, Food, U) }, state: world, flows: baseline));
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(world));
    }

    [Fact]
    public void ZeroQuantityAndInvalidIssuerFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicMaterialOriginHoldOrder(
                A, Food, EconomicActivityKind.Agriculture, 0UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EconomicMaterialDestinationAcquisitionOrder(B, Food, 0UL));
        Assert.Throws<ArgumentException>(() => _resolver.Resolve(
            World(), new CivilizationId(9UL), Flows(),
            Array.Empty<EconomicMaterialOriginHoldOrder>(),
            Array.Empty<EconomicMaterialDestinationAcquisitionOrder>()));
    }
}
