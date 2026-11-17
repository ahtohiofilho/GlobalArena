using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Gate 01B.2D-9: the exact 112-connected-tile / eight same-grain sellers /
/// 104 real worker demand markets adversarial fixture from #397-#400,
/// now run against the opt-in joint economic potential coordinator of #401.
/// Budget exhaustion is explicitly distinguished from a certified full-pass
/// absence of improving market or route proposals.
/// </summary>
public sealed class FoundationPhysicalJointAdversarialMarketRouteTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalJointAdversarialMarketRouteTests(
        ITestOutputHelper output) => _output = output;

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain },
            Enum.GetValues<FoundationPhysicalDemandProfile>().Select(
                profile => new FoundationActivityCommodityDemandCoefficient(
                    profile, Grain, 1_000_000UL)));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("joint-adversarial-shared-grain-cargo-micro", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL)
        });

    private sealed record Case(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationCommodityMarketPlanningQuote[] Quotes,
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Scales,
        int TileCount,
        int SellerCount,
        int MarketCount,
        ulong ReferenceCargoMicro);

    private static Case EightCompetingSellersInConnectedGoldbergMarkets(
        ulong edgeReferenceCargoMicro,
        int worldFrequency = 3,
        int tileCount = 112,
        int sellerCount = 8,
        int marketCount = 104)
    {
        if (worldFrequency < 1 || tileCount <= 112 && worldFrequency > 3 ||
            sellerCount < 1 || marketCount < sellerCount ||
            marketCount > tileCount)
            throw new ArgumentOutOfRangeException(
                "Adversarial fixture requires valid connected market and seller counts.");
        int cells = tileCount;
        int sellers = sellerCount;
        int nonSellers = marketCount - sellers;
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(worldFrequency, 0)));
        var map = host.Incidence;
        var seed = map.FineTopology.Cells[0].Id;
        var queue = new Queue<StrategicCellId>();
        var discovered = new HashSet<StrategicCellId> { seed };
        var component = new List<StrategicCellId>();
        queue.Enqueue(seed);
        while (queue.Count > 0 && component.Count < cells)
        {
            var id = queue.Dequeue();
            component.Add(id);
            foreach (var adjacent in map.FineTopology.Cells[
                         checked((int)id.Value - 1)].AdjacentCellIds
                         .OrderBy(next => next.Value))
                if (discovered.Add(adjacent))
                    queue.Enqueue(adjacent);
        }
        Assert.Equal(cells, component.Count);
        var chosen = component.ToHashSet();
        var borders = map.FineTopology.Edges.Where(edge =>
            chosen.Contains(edge.IncidentCellIds[0]) &&
            chosen.Contains(edge.IncidentCellIds[1])).ToArray();
        Assert.True(borders.Length > cells);

        PhysicalTacticalTileId tile(StrategicCellId id) =>
            new(map.Refinement.FineParameters, id);
        StrategicCellId coarse(StrategicCellId id) =>
            map.TileIncidences[checked((int)id.Value - 1)]
                .IncidentCoarseCellIds[0];

        var people = new List<PhysicalWorkerRuntimeRecord>();
        for (int i = 0; i < sellers; i++)
            people.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)i + 1UL),
                Red, coarse(component[i]), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                tile(component[i]), PhysicalWorkerOccupation.Agriculture));
        for (int i = 0; i < nonSellers; i++)
        {
            var location = component[cells - nonSellers + i];
            people.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)(sellers + i + 1)),
                Blue, coarse(location), Grain, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                tile(location), PhysicalWorkerOccupation.Unassigned));
        }

        var census = new PhysicalWorkerRuntimeState(people,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var quotes = FoundationPhysicalCommodityDemandProjector
            .Project(census, Demand()).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Owner == Blue ? 1_000_000_000UL :
                        1_000_000UL)).ToArray();
        Assert.Equal(marketCount, quotes.Length);
        var roads = borders.Select(border =>
            new FoundationPhysicalRoutingEdgeCost(
                new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                    border.Id),
                FoundationInfrastructureOwnershipKind.NoInfrastructure,
                null, 100_000UL, 0UL)).ToArray();
        var scales = roads.Select(road =>
            new FoundationPhysicalEdgeCarryingScale(
                road.Edge, edgeReferenceCargoMicro)).ToArray();
        return new Case(host, census, quotes, roads, scales,
            tileCount, sellerCount, marketCount, edgeReferenceCargoMicro);
    }

    private static FoundationPhysicalJointMarketRoutePotentialPlan Coordinate(
        Case scenario, int maxPasses, FoundationCommodityMarketPlanningQuote[]?
            quotes = null, int maxRouteBatches = 4096)
    {
        return FoundationPhysicalJointMarketRoutePotentialCoordinator
            .Coordinate(scenario.Workers, scenario.Host.Incidence,
                Demand(), Cargo(), quotes ?? scenario.Quotes, scenario.Roads,
                scenario.Scales, segmentsPerAverageMarket: 4,
                maxFlowAugmentationsPerProposal: 4096,
                maxRouteBatchesPerParent: 2,
                maxTotalRouteBatches: maxRouteBatches,
                maxCompletePasses: maxPasses);
    }

    private static void AssertStrictPotential(IReadOnlyList<BigInteger> history)
    {
        Assert.NotEmpty(history);
        foreach (var (prior, later) in history.Zip(history.Skip(1)))
            Assert.True(later > prior,
                "Every accepted market/route state must strictly improve " +
                "the one audited integrated economic potential.");
    }

    private static string RouteSignature(
        FoundationPhysicalJointMarketRoutePotentialPlan plan)
    {
        return string.Join(";", plan.Allocation.Parts.Select(part =>
            string.Join("|",
                part.ParentDeliveryOrdinal,
                part.Delivery.DeliveryOrdinal,
                part.Delivery.GoodsMicro,
                part.Delivery.Seller.Value,
                part.Delivery.Buyer.Value,
                part.Delivery.BuyerTile.FineStrategicCellId.Value,
                string.Join(",", part.Delivery.RouteTiles.Select(
                    tile => tile.FineStrategicCellId.Value)))));
    }

    private static void AssertRealWorkerMarketConservation(
        Case scenario, FoundationPhysicalJointMarketRoutePotentialPlan plan)
    {
        Assert.True(plan.StableUnderOneCompleteBlockPass);
        Assert.Equal(scenario.SellerCount,
            plan.Allocation.MarketAllocation.ProductiveLotCount);
        Assert.Equal(scenario.MarketCount,
            plan.Allocation.MarketAllocation.RealMarketCount);
        var audit = plan.Allocation.AllocationAudit;
        Assert.Equal(checked((ulong)scenario.SellerCount * 1_000_000UL),
            audit.TotalAvailableGoodsMicro);
        Assert.Equal(audit.TotalAvailableGoodsMicro,
            audit.TotalAllocatedGoodsMicro);
        Assert.Equal(audit.TotalAllocatedGoodsMicro,
            plan.Allocation.MarketAllocation.AllocationAudit
                .TotalAllocatedGoodsMicro);

        var parentById = plan.Allocation.MarketAllocation.Candidates
            .ToDictionary(parent => parent.DeliveryOrdinal);
        var childLots = plan.Allocation.Parts
            .GroupBy(part => part.ParentDeliveryOrdinal)
            .ToDictionary(group => group.Key,
                group => group.ToArray());
        Assert.Equal(parentById.Count, childLots.Count);
        foreach (var (id, parent) in parentById)
        {
            Assert.True(childLots.TryGetValue(id, out var children));
            Assert.NotNull(children);
            Assert.Equal(new BigInteger(parent.GoodsMicro),
                children!.Aggregate(BigInteger.Zero,
                    (sum, child) => sum + child.Delivery.GoodsMicro));
            Assert.All(children, child =>
            {
                Assert.Equal(parent.Seller, child.Delivery.Seller);
                Assert.Equal(parent.Buyer, child.Delivery.Buyer);
                Assert.Equal(parent.SourceTile, child.Delivery.SourceTile);
                Assert.Equal(parent.BuyerTile, child.Delivery.BuyerTile);
                Assert.Equal(parent.Commodity, child.Delivery.Commodity);
            });
        }

        var byEdge = audit.Freight.UsedEdges.ToDictionary(
            reading => reading.Edge,
            reading => reading.TotalCargoMicro);
        Assert.Equal(scenario.Roads.Length,
            plan.Allocation.ProjectedEdges.Count);
        Assert.Equal(audit.Freight.TotalEdgeTraversalCargoMicro,
            plan.Allocation.ProjectedEdges.Aggregate(BigInteger.Zero,
                (total, edge) => total + edge.CargoMicro));
        Assert.All(plan.Allocation.ProjectedEdges, reading =>
        {
            Assert.Equal(byEdge.GetValueOrDefault(reading.Edge),
                reading.CargoMicro);
            Assert.Equal(
                FoundationPhysicalEndogenousCongestionFeedback
                    .CalculateCongestedRate(
                        reading.FreePhysicalCostMicroPerWholeCargoUnit,
                        reading.CargoMicro,
                        reading.CargoMicroAtReferenceLoad),
                reading.CongestedPhysicalCostMicroPerWholeCargoUnit);
        });
        Assert.Equal(plan.AcceptedMarketRouteProposals +
            plan.AcceptedRouteOnlyProposals + 1,
            plan.AcceptedPotentialHistoryScaled.Count);
        AssertStrictPotential(plan.AcceptedPotentialHistoryScaled);
        Assert.Equal(plan.FinalIntegratedNetPotentialScaled,
            FoundationPhysicalJointMarketRoutePotentialCoordinator
                .IntegratedNetPotentialScaled(
                    plan.Allocation, scenario.Quotes));
    }

    private void RecordOutcome(Case scenario, string fileName,
        string outcome, int passBudget, int completePasses,
        int marketAccepts, int routeAccepts, int rejected,
        IReadOnlyList<BigInteger> acceptedHistory, long elapsedMilliseconds)
    {
        Assert.Contains(outcome,
            new[] { "BLOCK_STATIONARY", "BUDGET_EXHAUSTED" });
        AssertStrictPotential(acceptedHistory);
        Assert.Equal(1 + marketAccepts + routeAccepts,
            acceptedHistory.Count);
        var record = new
        {
            Schema = "ga-next.joint-stress.v1",
            Scenario = fileName,
            Outcome = outcome,
            scenario.TileCount,
            scenario.SellerCount,
            scenario.MarketCount,
            TotalSupplyGoodsMicro =
                checked((ulong)scenario.SellerCount * 1_000_000UL),
            scenario.ReferenceCargoMicro,
            PassBudget = passBudget,
            CompletePasses = completePasses,
            AcceptedMarketProposals = marketAccepts,
            AcceptedRouteProposals = routeAccepts,
            RejectedProposals = rejected,
            PotentialHistoryScaled =
                acceptedHistory.Select(value => value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray(),
            ElapsedMilliseconds = elapsedMilliseconds,
            CandidateSettledEconomically = false
        };
        _output.WriteLine(
            $"GA_JOINT_STRESS_OUTCOME scenario={fileName} " +
            $"outcome={outcome} tiles={scenario.TileCount} " +
            $"sellers={scenario.SellerCount} markets={scenario.MarketCount} " +
            $"passBudget={passBudget} completed={completePasses} " +
            $"accepted={marketAccepts + routeAccepts} " +
            $"elapsedMs={elapsedMilliseconds}; NoClosedCycle");
        var reportDirectory = Environment.GetEnvironmentVariable(
            "GA_JOINT_STRESS_REPORT_DIR");
        if (string.IsNullOrWhiteSpace(reportDirectory))
            return; // Local test runs remain file-free.
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllText(Path.Combine(reportDirectory, fileName + ".json"),
            JsonSerializer.Serialize(record,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void EightSellers104MarketsMildCongestionReachesAuditedBlockStationarity()
    {
        var scenario = EightCompetingSellersInConnectedGoldbergMarkets(
            edgeReferenceCargoMicro: 100_000_000UL);
        var snapshot = scenario.Host.Snapshot;
        var hash = scenario.Host.StateHash;
        var clock = Stopwatch.StartNew();
        var result = Coordinate(scenario, maxPasses: 12);
        clock.Stop();
        Assert.InRange(result.CompletePasses, 1, 12);
        AssertRealWorkerMarketConservation(scenario, result);
        _output.WriteLine(
            $"8seller104market referenceCargo=100000000; " +
            $"passes={result.CompletePasses}; acceptedMarket=" +
            $"{result.AcceptedMarketRouteProposals}; acceptedRoutes=" +
            $"{result.AcceptedRouteOnlyProposals}; rejected=" +
            $"{result.RejectedNonImprovingProposals}; " +
            $"parentDeliveries={result.Allocation.MarketAllocation.Candidates.Count}; " +
            $"parts={result.Allocation.Parts.Count}; " +
            $"routeBatches={result.Allocation.RouteBatchDecisions}; " +
            $"milliseconds={clock.ElapsedMilliseconds}; " +
            $"potentialHistory={string.Join(",", result.AcceptedPotentialHistoryScaled)}");
        Assert.Equal(hash, scenario.Host.StateHash);
        Assert.Same(snapshot, scenario.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Blue).Availability);

        // Order of independently supplied market quotes cannot change a
        // deterministic solution for the same real worker-backed demand.
        var reversed = Coordinate(scenario, maxPasses: 12,
            quotes: scenario.Quotes.Reverse().ToArray());
        AssertRealWorkerMarketConservation(scenario, reversed);
        Assert.Equal(result.FinalIntegratedNetPotentialScaled,
            reversed.FinalIntegratedNetPotentialScaled);
        Assert.Equal(RouteSignature(result), RouteSignature(reversed));
        Assert.Equal(result.AcceptedPotentialHistoryScaled,
            reversed.AcceptedPotentialHistoryScaled);
        RecordOutcome(scenario, "eight-sellers-104-markets-mild",
            "BLOCK_STATIONARY", 12, result.CompletePasses,
            result.AcceptedMarketRouteProposals,
            result.AcceptedRouteOnlyProposals,
            result.RejectedNonImprovingProposals,
            result.AcceptedPotentialHistoryScaled,
            clock.ElapsedMilliseconds);
    }

    [Fact]
    public void EightSellers104MarketsHeavyCongestionConvergesOrFailsClosedWithMonotoneDiagnostics()
    {
        var scenario = EightCompetingSellersInConnectedGoldbergMarkets(
            edgeReferenceCargoMicro: 1_000_000UL);
        var snapshot = scenario.Host.Snapshot;
        var hash = scenario.Host.StateHash;
        var clock = Stopwatch.StartNew();
        try
        {
            // Research-only heavy load: a pass budget is not a promise of
            // exact equilibrium. A successful return must pass every audit.
            var settledCandidate = Coordinate(scenario, maxPasses: 4);
            Assert.InRange(settledCandidate.CompletePasses, 1, 4);
            AssertRealWorkerMarketConservation(scenario, settledCandidate);
            _output.WriteLine(
                $"8seller104market heavyCongestion: block-stationary " +
                $"passes={settledCandidate.CompletePasses}, " +
                $"accepted={settledCandidate.AcceptedMarketRouteProposals +
                    settledCandidate.AcceptedRouteOnlyProposals}, " +
                $"milliseconds={clock.ElapsedMilliseconds}");
            RecordOutcome(scenario, "eight-sellers-104-markets-severe",
                "BLOCK_STATIONARY", 4, settledCandidate.CompletePasses,
                settledCandidate.AcceptedMarketRouteProposals,
                settledCandidate.AcceptedRouteOnlyProposals,
                settledCandidate.RejectedNonImprovingProposals,
                settledCandidate.AcceptedPotentialHistoryScaled,
                clock.ElapsedMilliseconds);
        }
        catch (FoundationPhysicalJointMarketRouteBudgetException failure)
        {
            Assert.Equal(4, failure.CompletePassBudget);
            Assert.True(failure.AcceptedProposals > 0);
            Assert.Equal(failure.AcceptedProposals + 1,
                failure.AcceptedPotentialHistoryScaled.Count);
            AssertStrictPotential(failure.AcceptedPotentialHistoryScaled);
            _output.WriteLine(
                $"8seller104market heavyCongestion: BUDGET EXHAUSTED, " +
                $"not converged; accepted={failure.AcceptedProposals}, " +
                $"milliseconds={clock.ElapsedMilliseconds}; " +
                $"potentialHistory={string.Join(",",
                    failure.AcceptedPotentialHistoryScaled)}");
            RecordOutcome(scenario, "eight-sellers-104-markets-severe",
                "BUDGET_EXHAUSTED", 4, 4,
                failure.AcceptedProposals, 0, 0,
                failure.AcceptedPotentialHistoryScaled,
                clock.ElapsedMilliseconds);
        }
        finally
        {
            clock.Stop();
            Assert.Equal(hash, scenario.Host.StateHash);
            Assert.Same(snapshot, scenario.Host.Snapshot);
            Assert.Equal(
                FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
            Assert.Equal(
                FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                scenario.Host.GetLastClosedEconomicTradeView(Blue).Availability);
        }
    }

    [Fact]
    public void SixteenSellers192MarketsLargerGoldbergReportsBudgetOrAuditedStationarity()
    {
        // Different, larger physical Goldberg topology and more real worker
        // markets. This is a bounded scaling probe, not proof of >10k scale.
        var scenario = EightCompetingSellersInConnectedGoldbergMarkets(
            edgeReferenceCargoMicro: 10_000_000UL,
            worldFrequency: 5, tileCount: 208,
            sellerCount: 16, marketCount: 192);
        Assert.True(scenario.TileCount > 112);
        Assert.True(scenario.Roads.Length > 112);
        var snapshot = scenario.Host.Snapshot;
        var hash = scenario.Host.StateHash;
        var timer = Stopwatch.StartNew();
        try
        {
            var result = Coordinate(scenario, maxPasses: 3,
                maxRouteBatches: 8192);
            Assert.InRange(result.CompletePasses, 1, 3);
            AssertRealWorkerMarketConservation(scenario, result);
            RecordOutcome(scenario, "sixteen-sellers-192-markets-scale",
                "BLOCK_STATIONARY", 3, result.CompletePasses,
                result.AcceptedMarketRouteProposals,
                result.AcceptedRouteOnlyProposals,
                result.RejectedNonImprovingProposals,
                result.AcceptedPotentialHistoryScaled,
                timer.ElapsedMilliseconds);
        }
        catch (FoundationPhysicalJointMarketRouteBudgetException ex)
        {
            Assert.Equal(3, ex.CompletePassBudget);
            Assert.True(ex.AcceptedProposals > 0);
            Assert.Equal(ex.AcceptedProposals + 1,
                ex.AcceptedPotentialHistoryScaled.Count);
            AssertStrictPotential(ex.AcceptedPotentialHistoryScaled);
            RecordOutcome(scenario, "sixteen-sellers-192-markets-scale",
                "BUDGET_EXHAUSTED", 3, 3, ex.AcceptedProposals,
                0, 0, ex.AcceptedPotentialHistoryScaled,
                timer.ElapsedMilliseconds);
        }
        finally
        {
            timer.Stop();
            Assert.Equal(hash, scenario.Host.StateHash);
            Assert.Same(snapshot, scenario.Host.Snapshot);
            Assert.Equal(
                FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
            Assert.Equal(
                FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                scenario.Host.GetLastClosedEconomicTradeView(Blue).Availability);
        }
    }

}
