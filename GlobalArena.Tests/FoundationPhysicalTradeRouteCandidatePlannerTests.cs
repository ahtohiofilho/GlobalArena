using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationPhysicalTradeRouteCandidatePlannerTests
{
    private const string CargoUnit = "test-policy-defined-cargo-micro";
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);

    private static FoundationTacticalHumanAiTurnHost Start() =>
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
        new(CargoUnit, commodities.Select(item =>
            new FoundationCommodityCargoConversion(item, 1_000_000UL)));

    private static FoundationCommodityMarketPlanningQuote[] Quotes(
        FoundationPhysicalCommodityMarketInputs markets,
        Func<FoundationPhysicalCommodityDemandLine, ulong>? choose = null) =>
        markets.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity,
                choose?.Invoke(line) ?? 1_000_000UL)).ToArray();

    private static PhysicalTacticalEdgeId Edge(
        PhysicalTacticalIncidenceMap map,
        PhysicalTacticalTileId first, PhysicalTacticalTileId second)
    {
        var geometry = Assert.Single(map.FineTopology.Edges, edge =>
            edge.IncidentCellIds.Contains(first.FineStrategicCellId) &&
            edge.IncidentCellIds.Contains(second.FineStrategicCellId));
        return new PhysicalTacticalEdgeId(
            map.Refinement.FineParameters, geometry.Id);
    }

    private static FoundationPhysicalRoutingEdgeCost Cost(
        PhysicalTacticalEdgeId edge, ulong physical = 1_000_000UL,
        ulong tariff = 0UL, CivilizationId? owner = null) =>
        new(edge, owner.HasValue
                ? FoundationInfrastructureOwnershipKind.Owned
                : FoundationInfrastructureOwnershipKind.NoInfrastructure,
            owner, physical, tariff);

    private static PhysicalTacticalTileId[] Path(
        PhysicalTacticalIncidenceMap map,
        PhysicalTacticalTileId from, PhysicalTacticalTileId to)
    {
        var queue = new Queue<StrategicCellId>();
        var previous = new Dictionary<StrategicCellId, StrategicCellId>();
        var seen = new HashSet<StrategicCellId> { from.FineStrategicCellId };
        queue.Enqueue(from.FineStrategicCellId);
        while (queue.Count != 0 && !seen.Contains(to.FineStrategicCellId))
        {
            var current = queue.Dequeue();
            foreach (var next in map.FineTopology.Cells[
                checked((int)current.Value - 1)]
                .AdjacentCellIds.OrderBy(id => id.Value))
            {
                if (!seen.Add(next)) continue;
                previous.Add(next, current);
                queue.Enqueue(next);
            }
        }
        Assert.Contains(to.FineStrategicCellId, seen);
        var reverse = new List<StrategicCellId> { to.FineStrategicCellId };
        while (reverse[^1] != from.FineStrategicCellId)
            reverse.Add(previous[reverse[^1]]);
        reverse.Reverse();
        return reverse.Select(id => new PhysicalTacticalTileId(
            map.Refinement.FineParameters, id)).ToArray();
    }

    private static FoundationPhysicalRoutingEdgeCost[] CorridorCosts(
        PhysicalTacticalIncidenceMap map,
        IReadOnlyList<PhysicalTacticalTileId> path,
        ulong physical, ulong toll, CivilizationId? owner)
    {
        var result = new List<FoundationPhysicalRoutingEdgeCost>();
        for (int i = 1; i < path.Count; i++)
            result.Add(Cost(Edge(map, path[i - 1], path[i]),
                physical, toll, owner));
        return result.ToArray();
    }

    private static (PhysicalTacticalTileId A, PhysicalTacticalTileId B,
        PhysicalTacticalTileId C) Triangle(PhysicalTacticalIncidenceMap map)
    {
        var fine = map.Refinement.FineParameters;
        foreach (var cell in map.FineTopology.Cells)
        {
            foreach (var neighborId in cell.AdjacentCellIds)
            {
                var neighbor = map.FineTopology.Cells[
                    checked((int)neighborId.Value - 1)];
                var common = cell.AdjacentCellIds.FirstOrDefault(id =>
                    id != neighborId && neighbor.AdjacentCellIds.Contains(id));
                if (common.IsValid)
                    return (
                        new PhysicalTacticalTileId(fine, cell.Id),
                        new PhysicalTacticalTileId(fine, neighborId),
                        new PhysicalTacticalTileId(fine, common));
            }
        }
        throw new InvalidOperationException("Fine Goldberg topology has no triangular corridor.");
    }

    private static PhysicalWorkerRuntimeState TriangleWorkers(
        PhysicalTacticalIncidenceMap map,
        PhysicalTacticalTileId a, PhysicalTacticalTileId b)
    {
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];
        return new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1),
                Red, coarse(a), One, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                a, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2),
                Blue, coarse(a), One, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                a, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(3),
                Blue, coarse(b), One, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                b, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
    }

    private static FoundationCommodityMarketPlanningQuote[] TriangleQuotes(
        PhysicalWorkerRuntimeState workers)
    {
        var markets = FoundationPhysicalCommodityDemandProjector.Project(
            workers, Demand(One));
        return Quotes(markets, line =>
            line.Tile == workers.Workers[2].TacticalTileId!.Value
                ? 30_000_000UL : 1_000_000UL);
    }

    [Fact]
    public void EqualQuotesPreferRealLocalBuyersWithoutInventedEdges()
    {
        var host = Start();
        var state = host.Snapshot;
        var hash = host.StateHash;
        var policy = Demand(One, Two);
        var inputs = host.GetCommodityMarketInputs(policy);
        var choices = host.PlanTradeCandidates(policy, Cargo(One, Two),
            Quotes(inputs), Array.Empty<FoundationPhysicalRoutingEdgeCost>());

        Assert.Equal(inputs.SupplyLots.Count, choices.Choices.Count);
        Assert.Equal(11_000_000UL,
            choices.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(choices.AllocationAudit.TotalAvailableGoodsMicro,
            choices.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.All(choices.Choices, choice =>
        {
            Assert.Equal(choice.Candidate.SourceTile, choice.Candidate.BuyerTile);
            Assert.Equal(choice.Candidate.Seller, choice.Candidate.Buyer);
            Assert.Single(choice.Candidate.RouteTiles);
            Assert.Equal(0UL, choice.PhysicalFreightCostMicro);
            Assert.Equal(0UL, choice.ExternalTariffCostMicro);
            Assert.Equal((Int128)choice.QuotedValueMicro,
                choice.NetPlanningScoreMicro);
        });
        Assert.Equal(0UL, choices.SumPhysicalFreightCostMicro);
        Assert.Equal(0UL, choices.SumExternalTariffCostMicro);
        Assert.Empty(choices.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(hash, host.StateHash);
        Assert.Same(state, host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ExplicitExternalQuoteCanSelectRealCrossCivilizationBuyer()
    {
        var host = Start();
        var demand = Demand(One, Two);
        var markets = host.GetCommodityMarketInputs(demand);
        var redLot = Assert.Single(markets.SupplyLots, lot =>
            lot.Owner == Red && lot.Commodity == One);
        var blueBuyer = Assert.Single(markets.DemandByCivilization, line =>
            line.Owner == Blue && line.Commodity == One);
        var path = Path(host.Incidence, redLot.Tile, blueBuyer.Tile);
        Assert.True(path.Length > 1);
        var quote = Quotes(markets, line =>
            line.Owner == Blue && line.Commodity == One
                ? 100_000_000UL : 1_000_000UL);
        var costs = CorridorCosts(host.Incidence, path,
            physical: 1_000_000UL, toll: 500_000UL, owner: Blue);
        var result = host.PlanTradeCandidates(
            demand, Cargo(One, Two), quote, costs);
        var selected = Assert.Single(result.Choices, choice =>
            choice.Candidate.Seller == Red &&
            choice.Candidate.Commodity == One);
        Assert.Equal(Blue, selected.Candidate.Buyer);
        Assert.Equal(path, selected.Candidate.RouteTiles);
        Assert.Equal((ulong)(path.Length - 1) *
            redLot.CapacityMicroPerTurn,
            result.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro);
        Assert.Equal((ulong)(path.Length - 1) * 500_000UL,
            selected.ExternalTariffCostMicro);
        Assert.Equal((ulong)(path.Length - 1) * 1_000_000UL,
            selected.PhysicalFreightCostMicro);
        Assert.Equal(result.AllocationAudit.TotalAvailableGoodsMicro,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Blue).Availability);
    }

    [Fact]
    public void LongerPhysicalRouteCanBeCheaperForForeignOwner()
    {
        var host = Start();
        var (a, b, c) = Triangle(host.Incidence);
        var census = TriangleWorkers(host.Incidence, a, b);
        var costs = new[]
        {
            Cost(Edge(host.Incidence, a, b), physical: 1_000_000UL,
                tariff: 10_000_000UL, owner: Blue),
            Cost(Edge(host.Incidence, a, c), physical: 2_000_000UL),
            Cost(Edge(host.Incidence, c, b), physical: 2_000_000UL)
        };
        var result = FoundationPhysicalTradeRouteCandidatePlanner.Plan(
            census, host.Incidence, Demand(One), Cargo(One),
            TriangleQuotes(census), costs);
        var red = Assert.Single(result.Choices,
            line => line.Candidate.Seller == Red);
        var blue = Assert.Single(result.Choices,
            line => line.Candidate.Seller == Blue);
        Assert.Equal(new[] { a, c, b }, red.Candidate.RouteTiles);
        Assert.Equal(new[] { a, b }, blue.Candidate.RouteTiles);
        Assert.Equal(4_000_000UL, red.PhysicalFreightCostMicro);
        Assert.Equal(1_000_000UL, blue.PhysicalFreightCostMicro);
        Assert.Equal(0UL, result.SumExternalTariffCostMicro);
        Assert.Equal(3_000_000UL,
            result.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro);
        Assert.Equal(2_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
    }

    [Fact]
    public void NonOwnerPaysExplicitTollWhenNoAlternativePathExists()
    {
        var host = Start();
        var (a, b, _) = Triangle(host.Incidence);
        var census = TriangleWorkers(host.Incidence, a, b);
        var costs = new[]
        {
            Cost(Edge(host.Incidence, a, b),
                physical: 1_000_000UL,
                tariff: 10_000_000UL, owner: Blue)
        };
        var result = FoundationPhysicalTradeRouteCandidatePlanner.Plan(
            census, host.Incidence, Demand(One), Cargo(One),
            TriangleQuotes(census), costs);
        Assert.Equal(10_000_000UL, result.SumExternalTariffCostMicro);
        var red = Assert.Single(result.Choices,
            line => line.Candidate.Seller == Red);
        var blue = Assert.Single(result.Choices,
            line => line.Candidate.Seller == Blue);
        Assert.Equal(10_000_000UL, red.ExternalTariffCostMicro);
        Assert.Equal(0UL, blue.ExternalTariffCostMicro);
        Assert.Equal(new[] { a, b }, red.Candidate.RouteTiles);
        Assert.Equal(new[] { a, b }, blue.Candidate.RouteTiles);
        Assert.Single(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(2_000_000UL,
            result.AllocationAudit.Freight.UsedEdges[0].TotalCargoMicro);
    }

    [Fact]
    public void MissingEdgesAreNotImaginaryFreeRoadsAndMissingBuyerQuotesFail()
    {
        var host = Start();
        var (a, b, _) = Triangle(host.Incidence);
        var census = TriangleWorkers(host.Incidence, a, b);
        var quotes = TriangleQuotes(census);
        var result = FoundationPhysicalTradeRouteCandidatePlanner.Plan(
            census, host.Incidence, Demand(One), Cargo(One),
            quotes, Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        // All workers produce demand at their own tile. If no route is
        // provided, source buyers are reachable; buyers elsewhere are not.
        Assert.All(result.Choices, choice =>
        {
            Assert.Equal(a, choice.Candidate.BuyerTile);
            Assert.Single(choice.Candidate.RouteTiles);
        });
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeRouteCandidatePlanner.Plan(
                census, host.Incidence, Demand(One), Cargo(One),
                quotes.Skip(1), Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeRouteCandidatePlanner.Plan(
                census, host.Incidence, Demand(One), Cargo(One),
                quotes.Append(quotes[0]),
                Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeRouteCandidatePlanner.Plan(
                census, host.Incidence, Demand(One), Cargo(One),
                quotes.Select((q, i) => i == 0
                    ? q with { QuoteMicroPerWholeCommodityUnit = 0UL } : q),
                Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeRouteCandidatePlanner.Plan(
                census, host.Incidence, Demand(One), Cargo(One),
                quotes.Append(new FoundationCommodityMarketPlanningQuote(
                    b, new CivilizationId(999), One, 1_000_000UL)),
                Array.Empty<FoundationPhysicalRoutingEdgeCost>()));
    }

    [Fact]
    public void EdgeCostPolicyMustBeRealExplicitAndOwnershipConsistent()
    {
        var host = Start();
        var (a, b, _) = Triangle(host.Incidence);
        var census = TriangleWorkers(host.Incidence, a, b);
        var quotes = TriangleQuotes(census);
        var edge = Edge(host.Incidence, a, b);
        foreach (var invalid in new[]
        {
            Cost(edge, physical: 0UL),
            Cost(edge, physical: 1_000_000UL, tariff: 1UL),
            new FoundationPhysicalRoutingEdgeCost(edge,
                FoundationInfrastructureOwnershipKind.Owned, null,
                1_000_000UL, 1_000_000UL),
            new FoundationPhysicalRoutingEdgeCost(edge,
                FoundationInfrastructureOwnershipKind.Unknown, Blue,
                1_000_000UL, 0UL),
            new FoundationPhysicalRoutingEdgeCost(
                new PhysicalTacticalEdgeId(new GoldbergParameters(12, 0),
                    edge.FineStrategicEdgeId),
                FoundationInfrastructureOwnershipKind.NoInfrastructure, null,
                1_000_000UL, 0UL)
        })
            Assert.Throws<ArgumentException>(() =>
                FoundationPhysicalTradeRouteCandidatePlanner.Plan(
                    census, host.Incidence, Demand(One), Cargo(One),
                    quotes, new[] { invalid }));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalTradeRouteCandidatePlanner.Plan(
                census, host.Incidence, Demand(One), Cargo(One),
                quotes, new[] { Cost(edge), Cost(edge) }));
    }

    [Fact]
    public void WorkerAndPolicyEnumerationDoNotChangeCanonicalDestinationsOrRoutes()
    {
        var host = Start();
        var (a, b, c) = Triangle(host.Incidence);
        var census = TriangleWorkers(host.Incidence, a, b);
        var reversed = new PhysicalWorkerRuntimeState(census.Workers.Reverse(),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var quote = TriangleQuotes(census);
        var edges = new[]
        {
            Cost(Edge(host.Incidence, a, b), physical: 1_000_000UL,
                tariff: 10_000_000UL, owner: Blue),
            Cost(Edge(host.Incidence, a, c), physical: 2_000_000UL),
            Cost(Edge(host.Incidence, c, b), physical: 2_000_000UL)
        };
        var one = FoundationPhysicalTradeRouteCandidatePlanner.Plan(census,
            host.Incidence, Demand(One), Cargo(One), quote, edges);
        var two = FoundationPhysicalTradeRouteCandidatePlanner.Plan(reversed,
            host.Incidence, Demand(One), Cargo(One),
            quote.Reverse(), edges.Reverse());
        Assert.Equal(one.SumQuotedValueMicro, two.SumQuotedValueMicro);
        Assert.Equal(one.SumPhysicalFreightCostMicro,
            two.SumPhysicalFreightCostMicro);
        Assert.Equal(one.SumExternalTariffCostMicro,
            two.SumExternalTariffCostMicro);
        Assert.Equal(one.Choices.Select(x => (
            x.Candidate.DeliveryOrdinal, x.Candidate.Seller,
            x.Candidate.Buyer, x.Candidate.Commodity,
            x.NetPlanningScoreMicro)),
            two.Choices.Select(x => (
            x.Candidate.DeliveryOrdinal, x.Candidate.Seller,
            x.Candidate.Buyer, x.Candidate.Commodity,
            x.NetPlanningScoreMicro)));
        Assert.Equal(one.Choices.Select(x =>
            string.Join(",", x.Candidate.RouteTiles.Select(t =>
                t.FineStrategicCellId.Value))),
            two.Choices.Select(x =>
            string.Join(",", x.Candidate.RouteTiles.Select(t =>
                t.FineStrategicCellId.Value))));
    }

    [Fact]
    public void PendingAndCommittedOccupationChangesUseOnlyAuthoritativeSupply()
    {
        var host = Start();
        var policy = Demand(One, Two);
        var original = host.Snapshot;
        var hash = host.StateHash;
        var quotes = Quotes(host.GetCommodityMarketInputs(policy));
        host.QueueHumanOccupation(Red, new PhysicalWorkerId(1UL),
            PhysicalWorkerOccupation.Mining);
        var before = host.PlanTradeCandidates(policy,
            Cargo(One, Two), quotes,
            Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(11_000_000UL,
            before.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Same(original, host.Snapshot);
        Assert.Equal(hash, host.StateHash);

        host.SetHumanReady(Red);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.True(receipt.ReplayMatched);
        var current = host.GetCommodityMarketInputs(policy);
        var after = host.PlanTradeCandidates(policy, Cargo(One, Two),
            Quotes(current),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(12_000_000UL, after.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Contains(after.Choices, x =>
            x.Candidate.Seller == Red &&
            x.Candidate.SourceOccupation == PhysicalWorkerOccupation.Mining);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void TenThousandActualWorkersAreOneAggregatedSourceCandidate()
    {
        var host = Start();
        var worker = host.Snapshot.Workers.Workers[0];
        var census = new PhysicalWorkerRuntimeState(
            Enumerable.Range(1, 10_000).Select(i =>
                new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId((ulong)i),
                    Red, worker.CellId, One, 1_000_000UL,
                    PhysicalWorkerActivity.Producing, 1_000_000U,
                    worker.TacticalTileId,
                    PhysicalWorkerOccupation.Agriculture)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var policy = Demand(One);
        var markets = FoundationPhysicalCommodityDemandProjector.Project(
            census, policy);
        var result = FoundationPhysicalTradeRouteCandidatePlanner.Plan(
            census, host.Incidence, policy, Cargo(One),
            Quotes(markets), Array.Empty<FoundationPhysicalRoutingEdgeCost>());
        Assert.Equal(10_000, result.AllocationAudit.MarketInputs.DistinctWorkers);
        var only = Assert.Single(result.Choices);
        Assert.Equal(10_000_000_000UL, only.Candidate.GoodsMicro);
        Assert.Single(only.Candidate.RouteTiles);
        Assert.Empty(result.AllocationAudit.Freight.UsedEdges);
        Assert.Equal(10_000_000_000UL,
            result.AllocationAudit.TotalAllocatedGoodsMicro);
    }
}
