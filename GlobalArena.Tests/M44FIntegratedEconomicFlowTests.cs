using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M44FIntegratedEconomicFlowTests
{
    private const ulong RawCostPerHop = 100_000UL;
    private const ulong RawBaseCapacity = 100UL;
    private const ulong RawInitialUnitValue = 1_000_000UL;

    [Fact]
    public void RouteCacheTransferAndMarketAllocateEndToEnd()
    {
        var scenario =
            CreateAdjacentScenario();

        var cache =
            new StrategicEconomicRouteCache(
                scenario.RouteResolver,
                StrategicEdgeAccessSnapshot.AllOpen);

        var result =
            RunPipeline(
                cache,
                scenario);

        Assert.True(
            result.Lookup.IsReachable);

        Assert.False(
            result.Lookup.WasCacheHit);

        Assert.NotNull(
            result.Signal);

        Assert.Equal(
            1,
            result.Lookup.Route!.HopCount);

        Assert.Equal(
            RawCostPerHop,
            result.Signal!.RawBaseUnitCost);

        Assert.Equal(
            RawBaseCapacity,
            result.Signal.RawEffectiveCapacity);

        var allocation =
            Assert.Single(
                result.Market.Allocations);

        Assert.Equal(
            100UL,
            allocation.RawQuantity);

        Assert.Equal(
            RawInitialUnitValue,
            allocation.RawUnitValue);

        Assert.Equal(
            RawCostPerHop,
            allocation.RawUnitTransferCost);

        Assert.Equal(
            100UL,
            allocation.RawGrossSettlementValue);

        Assert.Equal(
            10UL,
            allocation.RawTransferSettlementCost);

        Assert.Equal(
            90UL,
            allocation.RawNetSettlementReturn);
    }

    [Fact]
    public void BlockingDirectEdgeInvalidatesRouteAndReducesSettlementReturn()
    {
        var scenario =
            CreateAdjacentScenario();

        var cache =
            new StrategicEconomicRouteCache(
                scenario.RouteResolver,
                StrategicEdgeAccessSnapshot.AllOpen);

        var before =
            RunPipeline(
                cache,
                scenario);

        var invalidated =
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        scenario.DirectEdge
                    }));

        Assert.Equal(
            new[]
            {
                scenario.Key
            },
            invalidated);

        var after =
            RunPipeline(
                cache,
                scenario);

        Assert.False(
            after.Lookup.WasCacheHit);

        Assert.True(
            after.Lookup.Route!.HopCount > 1);

        Assert.True(
            after.Signal!.RawBaseUnitCost
            > before.Signal!.RawBaseUnitCost);

        Assert.True(
            after.Market.TotalRawNetSettlementReturn
            < before.Market.TotalRawNetSettlementReturn);

        Assert.Equal(
            before.Market.TotalRawAllocatedQuantity,
            after.Market.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void ReopeningDirectEdgeRestoresCanonicalRouteAndSettlement()
    {
        var scenario =
            CreateAdjacentScenario();

        var cache =
            new StrategicEconomicRouteCache(
                scenario.RouteResolver,
                StrategicEdgeAccessSnapshot.AllOpen);

        var baseline =
            RunPipeline(
                cache,
                scenario);

        _ =
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        scenario.DirectEdge
                    }));

        var blocked =
            RunPipeline(
                cache,
                scenario);

        Assert.True(
            blocked.Lookup.Route!.HopCount > 1);

        var invalidated =
            cache.UpdateAccessSnapshot(
                StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Equal(
            new[]
            {
                scenario.Key
            },
            invalidated);

        var reopened =
            RunPipeline(
                cache,
                scenario);

        var baselineRoute =
            baseline.Lookup.Route
            ?? throw new InvalidOperationException(
                "Baseline route must be reachable.");

        var reopenedRoute =
            reopened.Lookup.Route
            ?? throw new InvalidOperationException(
                "Reopened route must be reachable.");

        Assert.Equal(
            1,
            reopenedRoute.HopCount);

        Assert.Equal(
            baselineRoute.CellPath,
            reopenedRoute.CellPath);

        Assert.Equal(
            baselineRoute.EdgePath,
            reopenedRoute.EdgePath);

        Assert.Equal(
            baseline.Signal!.RawBaseUnitCost,
            reopened.Signal!.RawBaseUnitCost);

        Assert.Equal(
            baseline.Market.TotalRawNetSettlementReturn,
            reopened.Market.TotalRawNetSettlementReturn);
    }

    [Fact]
    public void UnreachableRouteSuppressesTradeUntilFrontierReopens()
    {
        var scenario =
            CreateAdjacentScenario();

        var sourceCell =
            scenario.World.StrategicTopology.Cells[
                checked(
                    (int)(
                        scenario.SourceCellId.Value
                        - 1UL))];

        var cache =
            new StrategicEconomicRouteCache(
                scenario.RouteResolver,
                new StrategicEdgeAccessSnapshot(
                    sourceCell.IncidentEdgeIds));

        var blocked =
            RunPipeline(
                cache,
                scenario);

        Assert.False(
            blocked.Lookup.IsReachable);

        Assert.Null(
            blocked.Signal);

        Assert.Empty(
            blocked.Market.Allocations);

        var stillClosed =
            sourceCell
                .IncidentEdgeIds
                .Where(
                    edgeId =>
                        edgeId != scenario.DirectEdge)
                .ToArray();

        var invalidated =
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    stillClosed));

        Assert.Equal(
            new[]
            {
                scenario.Key
            },
            invalidated);

        var reopened =
            RunPipeline(
                cache,
                scenario);

        Assert.True(
            reopened.Lookup.IsReachable);

        Assert.Equal(
            1,
            reopened.Lookup.Route!.HopCount);

        Assert.NotNull(
            reopened.Signal);

        Assert.Single(
            reopened.Market.Allocations);

        Assert.Equal(
            100UL,
            reopened.Market.TotalRawAllocatedQuantity);
    }

    [Fact]
    public void SamePointLocalTradeIntegratesZeroHopZeroTransferSettlement()
    {
        var world =
            CreateWorld();

        var cellId =
            world.StrategicTopology.Cells[0].Id;

        var economy =
            new EconomyRuntimeState(
                new[]
                {
                    Point(
                        1UL,
                        cellId)
                });

        var key =
            new StrategicEconomicRouteKey(
                new EconomicPointId(
                    1UL),
                new EconomicPointId(
                    1UL));

        var commodity =
            new CommodityId(
                1U);

        var productionDemand =
            new EconomicProductionDemandResult(
                new[]
                {
                    new EconomicSupplyFlow(
                        new EconomicPointId(
                            1UL),
                        commodity,
                        EconomicActivityKind.Agriculture,
                        100UL)
                },
                new[]
                {
                    new EconomicDemandFlow(
                        new EconomicPointId(
                            1UL),
                        commodity,
                        100UL)
                });

        var scenario =
            new Scenario(
                world,
                new StrategicEconomicRouteResolver(
                    world.StrategicSurfaceGraph),
                economy,
                key,
                cellId,
                cellId,
                default,
                productionDemand);

        var cache =
            new StrategicEconomicRouteCache(
                scenario.RouteResolver,
                StrategicEdgeAccessSnapshot.AllOpen);

        var result =
            RunPipeline(
                cache,
                scenario);

        Assert.True(
            result.Lookup.IsReachable);

        Assert.Empty(
            result.Lookup.SearchDependencyEdgeIds);

        Assert.Equal(
            0,
            result.Lookup.Route!.HopCount);

        Assert.Equal(
            0UL,
            result.Signal!.RawBaseUnitCost);

        var allocation =
            Assert.Single(
                result.Market.Allocations);

        Assert.Equal(
            0UL,
            allocation.RawUnitTransferCost);

        Assert.Equal(
            100UL,
            allocation.RawNetSettlementReturn);
    }

    [Fact]
    public void RepeatedIntegratedPipelineIsDeterministic()
    {
        var scenario =
            CreateAdjacentScenario();

        var first =
            RunPipeline(
                new StrategicEconomicRouteCache(
                    scenario.RouteResolver,
                    StrategicEdgeAccessSnapshot.AllOpen),
                scenario);

        var second =
            RunPipeline(
                new StrategicEconomicRouteCache(
                    scenario.RouteResolver,
                    StrategicEdgeAccessSnapshot.AllOpen),
                scenario);

        Assert.Equal(
            first.Lookup.Route!.CellPath,
            second.Lookup.Route!.CellPath);

        Assert.Equal(
            first.Lookup.Route.EdgePath,
            second.Lookup.Route.EdgePath);

        Assert.Equal(
            first.Lookup.SearchDependencyEdgeIds,
            second.Lookup.SearchDependencyEdgeIds);

        Assert.Equal(
            first.Signal!.HopCount,
            second.Signal!.HopCount);

        Assert.Equal(
            first.Signal.RawBaseUnitCost,
            second.Signal.RawBaseUnitCost);

        Assert.Equal(
            first.Signal.RawEffectiveCapacity,
            second.Signal.RawEffectiveCapacity);

        Assert.Equal(
            first.Market.TotalRawAllocatedQuantity,
            second.Market.TotalRawAllocatedQuantity);

        Assert.Equal(
            first.Market.TotalRawGrossSettlementValue,
            second.Market.TotalRawGrossSettlementValue);

        Assert.Equal(
            first.Market.TotalRawTransferSettlementCost,
            second.Market.TotalRawTransferSettlementCost);

        Assert.Equal(
            first.Market.TotalRawNetSettlementReturn,
            second.Market.TotalRawNetSettlementReturn);
    }

    private static PipelineResult RunPipeline(
        StrategicEconomicRouteCache cache,
        Scenario scenario)
    {
        var lookup =
            cache.Resolve(
                scenario.Economy,
                scenario.Key);

        if (!lookup.IsReachable)
        {
            return new PipelineResult(
                lookup,
                null,
                new EconomicMarketAllocationResolver()
                    .Resolve(
                        scenario.ProductionDemand,
                        Array.Empty<EconomicTransferSignal>(),
                        MarketPolicy()));
        }

        var signal =
            new EconomicTransferSignalResolver()
                .Resolve(
                    scenario.Economy,
                    lookup.Route!,
                    TransferPolicy());

        var market =
            new EconomicMarketAllocationResolver()
                .Resolve(
                    scenario.ProductionDemand,
                    new[]
                    {
                        signal
                    },
                    MarketPolicy());

        return new PipelineResult(
            lookup,
            signal,
            market);
    }

    private static Scenario CreateAdjacentScenario()
    {
        var world =
            CreateWorld();

        var sourceCell =
            world.StrategicTopology.Cells[0];

        var destinationCellId =
            sourceCell.AdjacentCellIds[0];

        var destinationCell =
            world.StrategicTopology.Cells[
                checked(
                    (int)(
                        destinationCellId.Value
                        - 1UL))];

        var directEdge =
            Assert.Single(
                sourceCell
                    .IncidentEdgeIds
                    .Intersect(
                        destinationCell.IncidentEdgeIds));

        var economy =
            new EconomyRuntimeState(
                new[]
                {
                    Point(
                        1UL,
                        sourceCell.Id),
                    Point(
                        2UL,
                        destinationCell.Id)
                });

        var key =
            new StrategicEconomicRouteKey(
                new EconomicPointId(
                    1UL),
                new EconomicPointId(
                    2UL));

        var commodity =
            new CommodityId(
                1U);

        var productionDemand =
            new EconomicProductionDemandResult(
                new[]
                {
                    new EconomicSupplyFlow(
                        new EconomicPointId(
                            1UL),
                        commodity,
                        EconomicActivityKind.Agriculture,
                        100UL)
                },
                new[]
                {
                    new EconomicDemandFlow(
                        new EconomicPointId(
                            2UL),
                        commodity,
                        100UL)
                });

        return new Scenario(
            world,
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph),
            economy,
            key,
            sourceCell.Id,
            destinationCell.Id,
            directEdge,
            productionDemand);
    }

    private static EconomicPointRuntimeState Point(
        ulong id,
        StrategicCellId cellId)
    {
        return new EconomicPointRuntimeState(
            new EconomicPointId(
                id),
            new CivilizationId(
                1UL),
            cellId,
            0UL);
    }

    private static EconomicTransferPolicy TransferPolicy()
    {
        return new EconomicTransferPolicy(
            RawCostPerHop,
            RawBaseCapacity,
            0UL,
            0UL);
    }

    private static EconomicMarketAllocationPolicy MarketPolicy()
    {
        return new EconomicMarketAllocationPolicy(
            RawInitialUnitValue,
            RawInitialUnitValue,
            1,
            1);
    }

    private static WorldGenerationResult CreateWorld()
    {
        return new DeterministicWorldGenerator()
            .Generate(
                new WorldGenerationRequest(
                    new WorldSeed(
                        4402UL),
                    WorldGenerationVersion.Initial,
                    new GoldbergParameters(
                        1,
                        0)));
    }

    private sealed record Scenario(
        WorldGenerationResult World,
        StrategicEconomicRouteResolver RouteResolver,
        EconomyRuntimeState Economy,
        StrategicEconomicRouteKey Key,
        StrategicCellId SourceCellId,
        StrategicCellId DestinationCellId,
        StrategicEdgeId DirectEdge,
        EconomicProductionDemandResult ProductionDemand);

    private sealed record PipelineResult(
        StrategicEconomicRouteCacheLookup Lookup,
        EconomicTransferSignal? Signal,
        EconomicMarketAllocationResult Market);
}
