using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalMarginalMarketDistributorTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);

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
        ulong factor = 1_000_000UL) =>
        new("test-physical-common-cargo-micro", new[]
        {
            new FoundationCommodityCargoConversion(One, factor)
        });

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        PhysicalWorkerRuntimeState census,
        Func<FoundationPhysicalCommodityDemandLine, ulong>? scale = null)
    {
        var inputs = FoundationPhysicalCommodityDemandProjector.Project(
            census, Demand(One));
        return inputs.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                scale?.Invoke(line) ?? 1_000_000UL)).ToArray();
    }

    private static PhysicalWorkerRuntimeState Census(
        PhysicalTacticalIncidenceMap map,
        ulong redOutputMicro = 10_000_000UL,
        bool remoteConsumer = false,
        bool twoProducers = false)
    {
        var a = map.TileIncidences[0].PhysicalTacticalTileId;
        var bId = map.FineTopology.Cells[0].AdjacentCellIds[0];
        var b = new PhysicalTacticalTileId(map.Refinement.FineParameters, bId);
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];
        var people = new List<PhysicalWorkerRuntimeRecord>
        {
            new(new PhysicalWorkerId(1), Red, coarse(a), One,
                redOutputMicro, PhysicalWorkerActivity.Producing, 1_000_000U,
                a, PhysicalWorkerOccupation.Agriculture),
            new(new PhysicalWorkerId(2), Blue, coarse(a), One,
                twoProducers ? 1_000_000UL : 0UL,
                twoProducers ? PhysicalWorkerActivity.Producing
                    : PhysicalWorkerActivity.Other,
                3_000_000U, a,
                twoProducers ? PhysicalWorkerOccupation.Forestry
                    : PhysicalWorkerOccupation.Unassigned)
        };
        if (remoteConsumer)
            people.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(3), Blue, coarse(b), One,
                0UL, PhysicalWorkerActivity.Traveling, 2_000_000U,
                b, PhysicalWorkerOccupation.Unassigned));
        return new PhysicalWorkerRuntimeState(people,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
    }

    private static FoundationPhysicalRoutingEdgeCost Edge(
        PhysicalTacticalIncidenceMap map, PhysicalTacticalTileId a,
        PhysicalTacticalTileId b, ulong physical = 1_000_000UL,
        ulong toll = 0UL, CivilizationId? owner = null)
    {
        var geometric = Assert.Single(map.FineTopology.Edges, edge =>
            edge.IncidentCellIds.Contains(a.FineStrategicCellId) &&
            edge.IncidentCellIds.Contains(b.FineStrategicCellId));
        return new FoundationPhysicalRoutingEdgeCost(
            new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                geometric.Id),
            owner.HasValue ? FoundationInfrastructureOwnershipKind.Owned
                : FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, physical, toll);
    }

    [Fact]
    public void OneRealLotSplitsBetweenTwoCoLocatedBuyersInProportionToTheirDemand()
    {
        var host = Host();
        var census = Census(host.Incidence);
        var result = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, host.Incidence, Demand(One), Cargo(),
            Quotes(census), Array.Empty<FoundationPhysicalRoutingEdgeCost>());

        Assert.Equal(1, result.ProductiveLotCount);
        Assert.Equal(2, result.BuyerCount);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(10_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(10_000_000UL,
            result.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(2, result.MarketTotals.Count);
        var red = Assert.Single(result.MarketTotals, line =>
            line.Buyer == Red);
        var blue = Assert.Single(result.MarketTotals, line =>
            line.Buyer == Blue);
        Assert.InRange(red.AllocatedGoodsMicro, 2_499_995UL, 2_500_005UL);
        Assert.InRange(blue.AllocatedGoodsMicro, 7_499_995UL, 7_500_005UL);
        Assert.Equal(1_000_000UL, red.DemandMicro);
        Assert.Equal(3_000_000UL, blue.DemandMicro);
        // No artificial maximum D: each physical consumer receives more
        // goods than its demand-intensity score, while all supply is sold.
        Assert.True(red.AllocatedGoodsMicro > red.DemandMicro);
        Assert.True(blue.AllocatedGoodsMicro > blue.DemandMicro);
        Assert.All(result.Candidates, delivery =>
        {
            Assert.Single(delivery.RouteTiles);
            Assert.Equal(delivery.SourceTile, delivery.BuyerTile);
            Assert.Equal(Red, delivery.Seller);
        });
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(0UL,
            result.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro);
    }

    [Fact]
    public void RemoteBuyerSharesSupplyAndPhysicalEdgeCarriesOnlyItsAllocatedFraction()
    {
        var host = Host();
        var census = Census(host.Incidence,
            redOutputMicro: 1_000_000UL, remoteConsumer: true);
        var a = census.Workers[0].TacticalTileId!.Value;
        var b = census.Workers[2].TacticalTileId!.Value;
        var edge = Edge(host.Incidence, a, b, physical: 1_000_000UL,
            toll: 200_000UL, owner: Blue);
        var result = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, host.Incidence, Demand(One), Cargo(),
            Quotes(census, line => line.Tile == b
                ? 10_000_000UL : 1_000_000UL), new[] { edge });

        Assert.Equal(3, result.BuyerCount);
        Assert.Equal(3, result.Candidates.Count);
        Assert.Equal(1_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        var remote = Assert.Single(result.Candidates, candidate =>
            candidate.BuyerTile == b);
        Assert.Equal(Blue, remote.Buyer);
        Assert.Equal(new[] { a, b }, remote.RouteTiles);
        Assert.InRange(remote.GoodsMicro, 1UL, 999_999UL);
        var traffic = Assert.Single(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(edge.Edge, traffic.Edge);
        Assert.Equal(remote.GoodsMicro, traffic.TotalCargoMicro);
        Assert.Equal(FoundationInfrastructureOwnershipKind.Owned,
            traffic.OwnershipKind);
        Assert.Equal(Blue, traffic.Owner);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void SuccessiveProductiveLotsShareDiminishingBuyerValues()
    {
        var host = Host();
        var census = Census(host.Incidence,
            redOutputMicro: 1_000_000UL, twoProducers: true);
        var result = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, host.Incidence, Demand(One), Cargo(),
            Quotes(census), Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(2, result.ProductiveLotCount);
        Assert.Equal(4, result.Candidates.Count);
        Assert.Equal(2_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        var red = Assert.Single(result.MarketTotals, x => x.Buyer == Red);
        var blue = Assert.Single(result.MarketTotals, x => x.Buyer == Blue);
        Assert.InRange(red.AllocatedGoodsMicro, 499_990UL, 500_010UL);
        Assert.InRange(blue.AllocatedGoodsMicro, 1_499_990UL, 1_500_010UL);
        Assert.Equal(2_000_000UL,
            result.MarketTotals.Aggregate(0UL, (sum, line) =>
                checked(sum + line.AllocatedGoodsMicro)));
        Assert.Equal(0UL, result.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro);
    }

    [Fact]
    public void MissingRoadsCannotBecomeArtificialFreeTrade()
    {
        var host = Host();
        var census = Census(host.Incidence,
            redOutputMicro: 1_000_000UL, remoteConsumer: true);
        var result = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, host.Incidence, Demand(One), Cargo(), Quotes(census),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(2, result.Candidates.Count);
        Assert.DoesNotContain(result.Candidates, x =>
            x.BuyerTile == census.Workers[2].TacticalTileId!.Value);
        Assert.Equal(0UL, Assert.Single(result.MarketTotals, x =>
            x.Tile == census.Workers[2].TacticalTileId!.Value).AllocatedGoodsMicro);
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(1_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
    }

    [Fact]
    public void DemandQuotesAndFreightPoliciesAreExplicitOrPlanningFailsClosed()
    {
        var host = Host();
        var census = Census(host.Incidence);
        var quotes = Quotes(census);
        var item = One;
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMarginalMarketDistributor.Distribute(
                census, host.Incidence, Demand(item), Cargo(),
                quotes.Skip(1), Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMarginalMarketDistributor.Distribute(
                census, host.Incidence, Demand(item), Cargo(),
                quotes.Append(quotes[0]),
                Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMarginalMarketDistributor.Distribute(
                census, host.Incidence, Demand(item), Cargo(),
                quotes.Select((q, i) => i == 0 ?
                    q with { QuoteMicroPerWholeCommodityUnit = 0UL } : q),
                Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMarginalMarketDistributor.Distribute(
                census, host.Incidence, Demand(item),
                new FoundationCommodityCargoConversionPolicy("bad", new[]
                {
                    new FoundationCommodityCargoConversion(Two, 1_000_000UL)
                }), quotes, Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
    }

    [Fact]
    public void CollocatedDemandCannotCarryDifferentCivilizationReferenceQuotes()
    {
        var host = Host();
        var census = Census(host.Incidence);
        var quotes = Quotes(census, line => line.Owner == Red
            ? 1_000_000UL : 2_000_000UL);
        var error = Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMarginalMarketDistributor.Distribute(
                census, host.Incidence, Demand(One), Cargo(), quotes,
                Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        Assert.Contains("shared physical tile/commodity market",
            error.Message);
    }

    [Fact]
    public void OddGoodsAreRejectedWhenTheirCargoCannotBeRepresentedExactly()
    {
        var host = Host();
        var census = Census(host.Incidence, redOutputMicro: 1UL);
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalMarginalMarketDistributor.Distribute(
                census, host.Incidence, Demand(One), Cargo(500_000UL),
                Quotes(census), Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        var ordinary = Census(host.Incidence);
        var good = FoundationPhysicalMarginalMarketDistributor.Distribute(
            ordinary, host.Incidence, Demand(One), Cargo(500_000UL),
            Quotes(ordinary), Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(10_000_000UL, good.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(5_000_000UL,
            good.AllocationAudit.Freight.TotalDeliveredCargoMicro);
        Assert.All(good.Candidates, candidate =>
            Assert.Equal(0UL, candidate.GoodsMicro % 2UL));
    }

    [Fact]
    public void PermutingWorkerAndQuoteEnumerationPreservesAllAllocations()
    {
        var host = Host();
        var census = Census(host.Incidence, twoProducers: true);
        var reverse = new PhysicalWorkerRuntimeState(census.Workers.Reverse(),
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        var quotes = Quotes(census);
        var one = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, host.Incidence, Demand(One), Cargo(), quotes,
            Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        var two = FoundationPhysicalMarginalMarketDistributor.Distribute(
            reverse, host.Incidence, Demand(One), Cargo(),
            quotes.Reverse(), Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(one.MarketTotals.ToArray(), two.MarketTotals.ToArray());
        Assert.Equal(one.Candidates.Count, two.Candidates.Count);
        Assert.Equal(
            one.Candidates.Select(x => (
                x.DeliveryOrdinal, x.Buyer, x.Seller, x.GoodsMicro,
                string.Join(",", x.RouteTiles.Select(t =>
                    t.FineStrategicCellId.Value)))),
            two.Candidates.Select(x => (
                x.DeliveryOrdinal, x.Buyer, x.Seller, x.GoodsMicro,
                string.Join(",", x.RouteTiles.Select(t =>
                    t.FineStrategicCellId.Value)))));
    }

    [Fact]
    public void PendingOrdersNeverChangeAllocationAndCommittedAiResumptionDoes()
    {
        var host = Host();
        var policy = Demand(One, Two);
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "test-physical-common-cargo-micro", new[]
            {
                new FoundationCommodityCargoConversion(One, 1_000_000UL),
                new FoundationCommodityCargoConversion(Two, 1_000_000UL)
            });
        FoundationCommodityMarketPlanningQuote[] quotes()
        {
            var lines = host.GetCommodityMarketInputs(policy).DemandByCivilization;
            return lines.Select(line => new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 1_000_000UL)).ToArray();
        }

        var before = host.Snapshot;
        var hash = host.StateHash;
        var initial = host.DistributeMarginalMarketCandidates(policy, cargo,
            quotes(), Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(11_000_000UL,
            initial.AllocationAudit.TotalAllocatedGoodsMicro);
        host.QueueHumanOccupation(Red, new PhysicalWorkerId(1UL),
            PhysicalWorkerOccupation.Mining);
        var pending = host.DistributeMarginalMarketCandidates(policy, cargo,
            quotes(), Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(initial.MarketTotals.ToArray(), pending.MarketTotals.ToArray());
        Assert.Equal(hash, host.StateHash);
        Assert.Same(before, host.Snapshot);

        host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        var committed = host.DistributeMarginalMarketCandidates(policy, cargo,
            quotes(), Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(12_000_000UL,
            committed.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Contains(committed.Candidates,
            x => x.Seller == Red &&
                 x.SourceOccupation == PhysicalWorkerOccupation.Mining);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void TenThousandWorkersAggregateIntoAtMostTwoBuyersNotTenThousandRoutes()
    {
        var host = Host();
        var sample = host.Snapshot.Workers.Workers[0];
        var tile = sample.TacticalTileId!.Value;
        var workers = Enumerable.Range(1, 10_000).Select(i =>
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId((ulong)i),
                Red, sample.CellId, One, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                tile, PhysicalWorkerOccupation.Agriculture))
            .Concat(new[]
            {
                new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(10_001),
                    Blue, sample.CellId, One, 0UL,
                    PhysicalWorkerActivity.Other, 1_000_000U, tile,
                    PhysicalWorkerOccupation.Unassigned)
            });
        var census = new PhysicalWorkerRuntimeState(workers,
            tacticalLocationsAuthoritative: true, occupationsAuthoritative: true);
        var result = FoundationPhysicalMarginalMarketDistributor.Distribute(
            census, host.Incidence, Demand(One), Cargo(), Quotes(census),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(10_001, result.AllocationAudit.MarketInputs.DistinctWorkers);
        Assert.Equal(1, result.ProductiveLotCount);
        Assert.Equal(2, result.BuyerCount);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(10_000_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
    }
}
