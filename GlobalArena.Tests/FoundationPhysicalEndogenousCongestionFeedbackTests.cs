using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalEndogenousCongestionFeedbackTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private const string CargoUnit = "test-shared-physical-cargo-micro";

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand(
        params CommodityId[] items) =>
        new(items, Enum.GetValues<FoundationPhysicalDemandProfile>()
            .SelectMany(profile => items.Select(item =>
                new FoundationActivityCommodityDemandCoefficient(
                    profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo(
        params CommodityId[] items) =>
        new(CargoUnit, items.Select(item =>
            new FoundationCommodityCargoConversion(item, 1_000_000UL)));

    private static (PhysicalTacticalTileId A, PhysicalTacticalTileId B,
        PhysicalTacticalEdgeId Crossing) Corridor(PhysicalTacticalIncidenceMap map)
    {
        var border = map.FineTopology.Edges[0];
        var fine = map.Refinement.FineParameters;
        return (new PhysicalTacticalTileId(fine, border.IncidentCellIds[0]),
            new PhysicalTacticalTileId(fine, border.IncidentCellIds[1]),
            new PhysicalTacticalEdgeId(fine, border.Id));
    }

    private static PhysicalWorkerRuntimeState TwoGoodsOneCrossing(
        PhysicalTacticalIncidenceMap map)
    {
        var (a, b, _) = Corridor(map);
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];
        return new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                Red, coarse(a), One, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U, a,
                PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                Blue, coarse(a), Two, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U, a,
                PhysicalWorkerOccupation.Forestry),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(3UL),
                Blue, coarse(b), One, 0UL,
                PhysicalWorkerActivity.Traveling, 1_000_000U, b,
                PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
    }

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        PhysicalWorkerRuntimeState state,
        FoundationActivityCommodityDemandPolicy policy,
        PhysicalTacticalTileId? preferred = null) =>
        FoundationPhysicalCommodityDemandProjector.Project(state, policy)
            .DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(line.Tile,
                    line.Owner, line.Commodity,
                    line.Tile == preferred ? 1_000_000_000UL : 1_000_000UL))
            .ToArray();

    private static FoundationPhysicalRoutingEdgeCost Road(
        PhysicalTacticalEdgeId edge, CivilizationId? owner = null) =>
        new(edge,
            owner.HasValue ? FoundationInfrastructureOwnershipKind.Owned
                : FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, 100_000UL, owner.HasValue ? 30_000UL : 0UL);

    [Fact]
    public void QuarticCongestionIsMonotoneAndHasNoArtificialShipmentLimit()
    {
        Assert.Equal(100_000UL,
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                100_000UL, 0UL, 1_000_000UL));
        Assert.Equal(200_000UL,
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                100_000UL, 1_000_000UL, 1_000_000UL));
        Assert.Equal(1_700_000UL,
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                100_000UL, 2_000_000UL, 1_000_000UL));
        // Insignificant physical loads must not get a fabricated surcharge.
        Assert.Equal(100_000UL,
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                100_000UL, 1UL, 1_000_000UL));
        Assert.Equal(100_000UL,
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                100_000UL, 100_000UL, 1_000_000_000UL));
        Assert.Throws<OverflowException>(() =>
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                ulong.MaxValue, ulong.MaxValue, 1UL));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalEndogenousCongestionFeedback.CalculateCongestedRate(
                100_000UL, 1UL, 0UL));
    }

    [Fact]
    public void TwoDifferentCommoditiesConsumeSamePhysicalCrossingCapacity()
    {
        var host = Host();
        var (a, b, edge) = Corridor(host.Incidence);
        var state = TwoGoodsOneCrossing(host.Incidence);
        var demand = Demand(One, Two);
        var result = FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
            state, host.Incidence, demand, Cargo(One, Two),
            Quotes(state, demand, preferred: b),
            new[] { Road(edge, owner: Blue) },
            new[] { new FoundationPhysicalEdgeCarryingScale(
                edge, 100_000_000UL) },
            maxProducerPasses: 8, maxCongestionIterations: 8);

        Assert.True(result.ConsistentWithFinalEdgeLoads);
        Assert.InRange(result.CongestionIterations, 2, 8);
        Assert.Equal(2_000_000UL,
            result.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(2_000_000UL,
            result.Distribution.AllocationAudit.TotalAvailableGoodsMicro);
        var used = Assert.Single(result.Distribution.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(edge, used.Edge);
        Assert.Equal(2, used.ByCommodity.Count);
        Assert.True(used.ByCommodity.All(x => x.CargoMicro > 0UL));
        var reading = Assert.Single(result.Edges);
        Assert.Equal(edge, reading.Edge);
        Assert.Equal(used.TotalCargoMicro, reading.CargoMicro);
        Assert.Equal(100_000_000UL, reading.CargoMicroAtReferenceLoad);
        Assert.True(reading.CongestedPhysicalCostMicroPerWholeCargoUnit >
            reading.FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(30_000UL, reading.ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(Blue, reading.InfrastructureOwner);
        Assert.Equal(used.ByCommodity.Aggregate(0UL,
            (sum, x) => checked(sum + x.CargoMicro)), reading.CargoMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void InvalidCarryingScalePolicyFailsClosedBeforeCandidatePublication()
    {
        var host = Host();
        var (_, _, edge) = Corridor(host.Incidence);
        var state = TwoGoodsOneCrossing(host.Incidence);
        var demand = Demand(One, Two);
        var quotes = Quotes(state, demand);
        var road = new[] { Road(edge) };
        var scale = new FoundationPhysicalEdgeCarryingScale(
            edge, 1_000_000UL);
        foreach (var bad in new[]
        {
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(),
            new[] { scale, scale },
            new[] { scale with { CargoMicroAtReferenceLoad = 0UL } },
            new[]
            {
                new FoundationPhysicalEdgeCarryingScale(
                    new PhysicalTacticalEdgeId(
                        new GoldbergParameters(12, 0),
                        edge.FineStrategicEdgeId), 1_000_000UL)
            }
        })
            Assert.Throws<ArgumentException>(() =>
                FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
                    state, host.Incidence, demand, Cargo(One, Two),
                    quotes, road, bad, 4, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
                state, host.Incidence, demand, Cargo(One, Two),
                quotes, road, new[] { scale }, 0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
                state, host.Incidence, demand, Cargo(One, Two),
                quotes, road, new[] { scale }, 4, 0));
    }

    [Fact]
    public void InsufficientBudgetCannotClaimCongestionHasSettled()
    {
        var host = Host();
        var (_, b, edge) = Corridor(host.Incidence);
        var census = TwoGoodsOneCrossing(host.Incidence);
        var demand = Demand(One, Two);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
                census, host.Incidence, demand, Cargo(One, Two),
                Quotes(census, demand, b),
                new[] { Road(edge) },
                new[] { new FoundationPhysicalEdgeCarryingScale(
                    edge, 1_000_000UL) },
                maxProducerPasses: 8, maxCongestionIterations: 1));
    }

    [Fact]
    public void ResultsRemainIdenticalAcrossInputEnumerationOrder()
    {
        var host = Host();
        var (_, b, edge) = Corridor(host.Incidence);
        var census = TwoGoodsOneCrossing(host.Incidence);
        var reverse = new PhysicalWorkerRuntimeState(census.Workers.Reverse(),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = Demand(One, Two);
        var quotes = Quotes(census, demand, b);
        var road = new[] { Road(edge, owner: Blue) };
        var capacity = new[] { new FoundationPhysicalEdgeCarryingScale(
            edge, 100_000_000UL) };
        var first = FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
            census, host.Incidence, demand, Cargo(One, Two),
            quotes, road, capacity, 8, 8);
        var second = FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
            reverse, host.Incidence, demand, Cargo(One, Two),
            quotes.Reverse(), road.Reverse(), capacity.Reverse(), 8, 8);
        Assert.Equal(first.CongestionIterations, second.CongestionIterations);
        Assert.Equal(first.Edges.ToArray(), second.Edges.ToArray());
        Assert.Equal(first.Distribution.MarketTotals.ToArray(),
            second.Distribution.MarketTotals.ToArray());
        Assert.Equal(first.Distribution.Candidates.Select(x => (
            x.DeliveryOrdinal, x.Seller, x.Buyer, x.Commodity,
            x.GoodsMicro, string.Join(",", x.RouteTiles.Select(tile =>
                tile.FineStrategicCellId.Value)))),
            second.Distribution.Candidates.Select(x => (
            x.DeliveryOrdinal, x.Seller, x.Buyer, x.Commodity,
            x.GoodsMicro, string.Join(",", x.RouteTiles.Select(tile =>
                tile.FineStrategicCellId.Value)))));
    }

    [Fact]
    public void LocalSalesAndTenThousandWorkersDoNotCreateCongestionOnAbsentRoads()
    {
        var host = Host();
        var sample = host.Snapshot.Workers.Workers[0];
        var tile = sample.TacticalTileId!.Value;
        var state = new PhysicalWorkerRuntimeState(
            Enumerable.Range(1, 10_000).Select(i =>
                new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId((ulong)i), Red, sample.CellId,
                    One, 1_000_000UL,
                    PhysicalWorkerActivity.Producing,
                    1_000_000U, tile, PhysicalWorkerOccupation.Agriculture)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = Demand(One);
        var result = FoundationPhysicalEndogenousCongestionFeedback.Coordinate(
            state, host.Incidence, demand, Cargo(One),
            Quotes(state, demand),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(),
            maxProducerPasses: 4, maxCongestionIterations: 3);
        Assert.Equal(1, result.CongestionIterations);
        Assert.Empty(result.Edges);
        Assert.Empty(result.Distribution.AllocationAudit.Freight.UsedEdges);
        Assert.Single(result.Distribution.Candidates);
        Assert.Equal(10_000_000_000UL,
            result.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
    }

    [Fact]
    public void HostCongestionPlanReadsOnlyCommittedWorkersWithoutEconomicJournal()
    {
        var host = Host();
        var policy = Demand(One, Two);
        var cargo = Cargo(One, Two);
        FoundationCommodityMarketPlanningQuote[] quotes()
        {
            var data = host.GetCommodityMarketInputs(policy).DemandByCivilization;
            return data.Select(line => new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 1_000_000UL)).ToArray();
        }

        var initialState = host.Snapshot;
        var initialHash = host.StateHash;
        var initial = host.CoordinateCongestedMarketCandidates(
            policy, cargo, quotes(),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(), 8, 3);
        Assert.Equal(11_000_000UL,
            initial.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        host.QueueHumanOccupation(Red, new PhysicalWorkerId(1UL),
            PhysicalWorkerOccupation.Mining);
        var pending = host.CoordinateCongestedMarketCandidates(
            policy, cargo, quotes(),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(), 8, 3);
        Assert.Equal(initial.Distribution.MarketTotals.ToArray(),
            pending.Distribution.MarketTotals.ToArray());
        Assert.Same(initialState, host.Snapshot);
        Assert.Equal(initialHash, host.StateHash);

        host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        var confirmed = host.CoordinateCongestedMarketCandidates(
            policy, cargo, quotes(),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(), 8, 3);
        Assert.Equal(12_000_000UL,
            confirmed.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }
}
