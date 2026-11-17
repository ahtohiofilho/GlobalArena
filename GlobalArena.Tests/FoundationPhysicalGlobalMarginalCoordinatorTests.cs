using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalGlobalMarginalCoordinatorTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private const string Unit = "test-normalized-physical-cargo-micro";

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand(
        params CommodityId[] commodities) =>
        new(commodities,
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => commodities.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo(
        params CommodityId[] commodities) =>
        new(Unit, commodities.Select(item =>
            new FoundationCommodityCargoConversion(item, 1_000_000UL)));

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        PhysicalWorkerRuntimeState census,
        FoundationActivityCommodityDemandPolicy policy,
        Func<FoundationPhysicalCommodityDemandLine, ulong>? quote = null) =>
        FoundationPhysicalCommodityDemandProjector.Project(census, policy)
            .DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    quote?.Invoke(line) ?? 10_000_000UL)).ToArray();

    private static (PhysicalTacticalTileId A, PhysicalTacticalTileId B,
        PhysicalTacticalEdgeId Edge) Corridor(PhysicalTacticalIncidenceMap map)
    {
        var first = map.FineTopology.Edges[0];
        var fine = map.Refinement.FineParameters;
        return (new PhysicalTacticalTileId(fine, first.IncidentCellIds[0]),
            new PhysicalTacticalTileId(fine, first.IncidentCellIds[1]),
            new PhysicalTacticalEdgeId(fine, first.Id));
    }

    private static PhysicalWorkerRuntimeState TwoSources(
        PhysicalTacticalIncidenceMap map, ulong eachOutputMicro = 50_000_000UL)
    {
        var (a, b, _) = Corridor(map);
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];
        return new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red, coarse(a), One,
                eachOutputMicro, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Blue, coarse(b), One,
                eachOutputMicro, PhysicalWorkerActivity.Producing,
                1_000_000U, b, PhysicalWorkerOccupation.Forestry)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
    }

    private static FoundationPhysicalRoutingEdgeCost Road(
        PhysicalTacticalEdgeId edge, ulong cost = 100_000UL,
        ulong tariff = 0UL, CivilizationId? owner = null) =>
        new(edge, owner.HasValue
                ? FoundationInfrastructureOwnershipKind.Owned
                : FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, cost, tariff);

    [Fact]
    public void MultipleSellersRebalanceSharedMarginalValuesInsteadOfLockingFirstPass()
    {
        var host = Host();
        var census = TwoSources(host.Incidence);
        var (_, _, edge) = Corridor(host.Incidence);
        var demand = Demand(One);
        var quotes = Quotes(census, demand);
        var costs = new[] { Road(edge) };

        var sequential = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, host.Incidence, demand, Cargo(One), quotes, costs);
        var coordinated = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            census, host.Incidence, demand, Cargo(One), quotes, costs,
            maxPasses: 128);

        Assert.True(coordinated.StableUnderOneFullCanonicalPass);
        Assert.InRange(coordinated.Passes, 2, 128);
        Assert.True(coordinated.TotalReallocatedGoodsMicro > 0UL);
        Assert.Equal(2, coordinated.ProductiveLotCount);
        Assert.Equal(sequential.MarketTotals.ToArray(),
            coordinated.InitialMarketTotals.ToArray());
        Assert.Equal(100_000_000UL,
            coordinated.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(100_000_000UL,
            coordinated.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(100_000_000UL,
            coordinated.MarketTotals.Aggregate(0UL, (total, line) =>
                checked(total + line.AllocatedGoodsMicro)));
        Assert.Equal(2, coordinated.MarketTotals.Count);
        Assert.NotEqual(
            sequential.MarketTotals.Select(x => x.AllocatedGoodsMicro),
            coordinated.MarketTotals.Select(x => x.AllocatedGoodsMicro));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void CrossCivilizationFlowsAfterRebalanceUseRealEdgeAndHistoricalOwner()
    {
        var host = Host();
        var census = TwoSources(host.Incidence,
            eachOutputMicro: 10_000_000UL);
        var (_, b, edge) = Corridor(host.Incidence);
        var demand = Demand(One);
        var quotes = Quotes(census, demand, line =>
            line.Tile == b ? 30_000_000UL : 5_000_000UL);
        var plans = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            census, host.Incidence, demand, Cargo(One), quotes,
            new[] { Road(edge, cost: 100_000UL,
                tariff: 200_000UL, owner: Blue) },
            maxPasses: 128);

        Assert.Equal(20_000_000UL,
            plans.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(plans.AllocationAudit.TotalAvailableGoodsMicro,
            plans.AllocationAudit.TotalAllocatedGoodsMicro);
        var across = plans.Candidates.Where(x =>
            x.SourceTile != x.BuyerTile).ToArray();
        Assert.NotEmpty(across);
        Assert.All(across, x =>
        {
            Assert.Equal(Red, x.Seller);
            Assert.Equal(Blue, x.Buyer);
            Assert.Equal(2, x.RouteTiles.Count);
            Assert.Equal(b, x.BuyerTile);
        });
        var load = Assert.Single(plans.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(edge, load.Edge);
        Assert.Equal(Blue, load.Owner);
        Assert.Equal(FoundationInfrastructureOwnershipKind.Owned,
            load.OwnershipKind);
        Assert.Equal(across.Aggregate(0UL, (sum, x) =>
            checked(sum + x.GoodsMicro)), load.TotalCargoMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Blue).Availability);
    }

    [Fact]
    public void WorkerQuoteAndRoadPermutationDoesNotChangeGlobalCandidateLedger()
    {
        var host = Host();
        var census = TwoSources(host.Incidence);
        var (_, _, edge) = Corridor(host.Incidence);
        var reversed = new PhysicalWorkerRuntimeState(census.Workers.Reverse(),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = Demand(One);
        var quotes = Quotes(census, demand);
        var edgeCosts = new[] { Road(edge) };
        var a = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            census, host.Incidence, demand, Cargo(One), quotes,
            edgeCosts, maxPasses: 128);
        var b = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            reversed, host.Incidence, demand, Cargo(One),
            quotes.Reverse(), edgeCosts.Reverse(), maxPasses: 128);
        Assert.Equal(a.Passes, b.Passes);
        Assert.Equal(a.TotalReallocatedGoodsMicro,
            b.TotalReallocatedGoodsMicro);
        Assert.Equal(a.MarketTotals.ToArray(), b.MarketTotals.ToArray());
        Assert.Equal(a.Candidates.Select(x =>
            (x.DeliveryOrdinal, x.Seller, x.Buyer, x.GoodsMicro,
             string.Join(",", x.RouteTiles.Select(t =>
                 t.FineStrategicCellId.Value)))),
            b.Candidates.Select(x =>
            (x.DeliveryOrdinal, x.Seller, x.Buyer, x.GoodsMicro,
             string.Join(",", x.RouteTiles.Select(t =>
                 t.FineStrategicCellId.Value)))));
    }

    [Fact]
    public void ExplicitlyInsufficientPassBudgetFailsClosedRatherThanClaimingEquilibrium()
    {
        var host = Host();
        var census = TwoSources(host.Incidence);
        var (_, _, edge) = Corridor(host.Incidence);
        var demand = Demand(One);
        var quotes = Quotes(census, demand);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
                census, host.Incidence, demand, Cargo(One), quotes,
                new[] { Road(edge) }, maxPasses: 0));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
                census, host.Incidence, demand, Cargo(One), quotes,
                new[] { Road(edge) }, maxPasses: 1));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void WithoutRoadsEachSellerRemainsLocalAndNoCongestionIsInvented()
    {
        var host = Host();
        var census = TwoSources(host.Incidence);
        var demand = Demand(One);
        var result = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            census, host.Incidence, demand, Cargo(One),
            Quotes(census, demand),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            maxPasses: 3);
        Assert.True(result.StableUnderOneFullCanonicalPass);
        Assert.Equal(1, result.Passes);
        Assert.Equal(0UL, result.TotalReallocatedGoodsMicro);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(result.Candidates, x =>
        {
            Assert.Equal(x.SourceTile, x.BuyerTile);
            Assert.Single(x.RouteTiles);
        });
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(100_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
    }

    [Fact]
    public void TenThousandWorkersStillUseOneAggregatedLotWithTwoRealBuyers()
    {
        var host = Host();
        var sample = host.Snapshot.Workers.Workers[0];
        var tile = sample.TacticalTileId!.Value;
        var workers = Enumerable.Range(1, 10_000).Select(i =>
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)i), Red,
                sample.CellId, One, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                tile, PhysicalWorkerOccupation.Agriculture))
            .Concat(new[]
            {
                new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId(10_001UL), Blue,
                    sample.CellId, One, 0UL,
                    PhysicalWorkerActivity.Other, 1_000_000U,
                    tile, PhysicalWorkerOccupation.Unassigned)
            });
        var census = new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = Demand(One);
        var result = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            census, host.Incidence, demand, Cargo(One),
            Quotes(census, demand),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            maxPasses: 4);
        Assert.Equal(1, result.ProductiveLotCount);
        Assert.Equal(1, result.Passes);
        Assert.Equal(0UL, result.TotalReallocatedGoodsMicro);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(10_000_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
    }

    [Fact]
    public void PendingWorkerOrdersDoNotRebalanceUntilAuthoritativeCollectiveTurn()
    {
        var host = Host();
        var demand = Demand(One, Two);
        var cargo = Cargo(One, Two);
        FoundationCommodityMarketPlanningQuote[] quote()
        {
            var lines = host.GetCommodityMarketInputs(demand)
                .DemandByCivilization;
            return lines.Select(line => new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                1_000_000UL)).ToArray();
        }

        var hash = host.StateHash;
        var snapshot = host.Snapshot;
        var initial = host.CoordinateGlobalMarginalMarketCandidates(
            demand, cargo, quote(),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(), maxPasses: 8);
        Assert.Equal(11_000_000UL,
            initial.AllocationAudit.TotalAllocatedGoodsMicro);
        host.QueueHumanOccupation(Red, new PhysicalWorkerId(1UL),
            PhysicalWorkerOccupation.Mining);
        var pending = host.CoordinateGlobalMarginalMarketCandidates(
            demand, cargo, quote(),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(), maxPasses: 8);
        Assert.Equal(initial.MarketTotals.ToArray(),
            pending.MarketTotals.ToArray());
        Assert.Equal(hash, host.StateHash);
        Assert.Same(snapshot, host.Snapshot);

        host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        var confirmed = host.CoordinateGlobalMarginalMarketCandidates(
            demand, cargo, quote(),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(), maxPasses: 8);
        Assert.Equal(12_000_000UL,
            confirmed.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Contains(confirmed.Candidates, x =>
            x.Seller == Red &&
            x.SourceOccupation == PhysicalWorkerOccupation.Mining);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }
}
