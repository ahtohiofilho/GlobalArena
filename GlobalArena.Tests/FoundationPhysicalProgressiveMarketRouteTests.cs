using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Gate 01B.2D-13: bounded incremental proposal evaluations retain an
/// audited best candidate across invocations. No economic settlement.
/// </summary>
public sealed class FoundationPhysicalProgressiveMarketRouteTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain },
            Enum.GetValues<FoundationPhysicalDemandProfile>().Select(
                profile => new FoundationActivityCommodityDemandCoefficient(
                    profile, Grain, 1_000_000UL)));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("progressive-test-grain-cargo", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL)
        });

    private sealed record Case(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationCommodityMarketPlanningQuote[] Quotes,
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Scales,
        FoundationActivityCommodityDemandPolicy Demand,
        FoundationCommodityCargoConversionPolicy Cargo);

    private static Case TwoRealMarkets(ulong cargoAtReferenceLoad)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var map = host.Incidence;
        var edge = map.FineTopology.Edges[0];
        var producer = new PhysicalTacticalTileId(
            map.Refinement.FineParameters, edge.IncidentCellIds[0]);
        var buyer = new PhysicalTacticalTileId(
            map.Refinement.FineParameters, edge.IncidentCellIds[1]);
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];

        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                Red, coarse(producer), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                producer, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                Blue, coarse(buyer), Grain, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                buyer, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
        var policy = Demand();
        var cargo = Cargo();
        var quotes = FoundationPhysicalCommodityDemandProjector
            .Project(workers, policy).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    100_000_000UL)).ToArray();
        var road = new FoundationPhysicalRoutingEdgeCost(
            new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                edge.Id), FoundationInfrastructureOwnershipKind.NoInfrastructure,
            null, 100_000UL, 0UL);
        return new Case(host, workers, quotes, new[] { road },
            new[] { new FoundationPhysicalEdgeCarryingScale(
                road.Edge, cargoAtReferenceLoad) }, policy, cargo);
    }

    private static FoundationPhysicalProgressiveMarketRouteState Begin(
        Case scenario, BigInteger minGain,
        FoundationPhysicalProgressiveMarketRouteState? previous = null,
        FoundationCommodityMarketPlanningQuote[]? quotes = null) =>
        FoundationPhysicalProgressiveMarketRouteCoordinator.BeginOrResume(
            scenario.Workers, scenario.Host.Incidence,
            scenario.Demand, scenario.Cargo,
            quotes ?? scenario.Quotes, scenario.Roads, scenario.Scales,
            segmentsPerAverageMarket: 8,
            maxFlowAugmentationsPerProposal: 256,
            maxRouteBatchesPerParent: 4,
            maxTotalRouteBatches: 16,
            minimumAcceptedGainScaled: minGain,
            previous: previous);

    [Fact]
    public void OneProposalPerInvocationReproducesFullJointSearchExactly()
    {
        var scenario = TwoRealMarkets(100_000UL);
        var hash = scenario.Host.StateHash;
        var snapshot = scenario.Host.Snapshot;
        var reference = FoundationPhysicalJointMarketRoutePotentialCoordinator
            .Coordinate(scenario.Workers, scenario.Host.Incidence,
                scenario.Demand, scenario.Cargo, scenario.Quotes,
                scenario.Roads, scenario.Scales,
                segmentsPerAverageMarket: 8,
                maxFlowAugmentationsPerProposal: 256,
                maxRouteBatchesPerParent: 4,
                maxTotalRouteBatches: 16, maxCompletePasses: 32);

        var state = Begin(scenario, BigInteger.Zero);
        Assert.Equal(0, state.ProposalsEvaluated);
        Assert.False(state.StableUnderDeclaredGainFloor);
        var initial = state.BestPotentialScaled;
        int invocations = 0;
        while (!state.StableUnderDeclaredGainFloor && invocations < 64)
        {
            var resumed = Begin(scenario, BigInteger.Zero, previous: state);
            Assert.Same(state, resumed); // Never rerun initial global flow.
            var updated = FoundationPhysicalProgressiveMarketRouteCoordinator
                .Advance(resumed, maxProposalEvaluations: 1);
            Assert.Equal(state.ProposalsEvaluated + 1,
                updated.ProposalsEvaluated);
            Assert.True(updated.BestPotentialScaled >= state.BestPotentialScaled);
            Assert.Equal(1_000_000UL,
                updated.BestAuditedCandidate.AllocationAudit
                    .TotalAllocatedGoodsMicro);
            Assert.Equal(updated.BestAuditedCandidate.AllocationAudit
                    .TotalAvailableGoodsMicro,
                updated.BestAuditedCandidate.AllocationAudit
                    .TotalAllocatedGoodsMicro);
            state = updated;
            invocations++;
        }
        Assert.True(state.StableUnderDeclaredGainFloor);
        Assert.Equal(reference.CompletePasses, state.CompletedPasses);
        Assert.Equal(reference.AcceptedMarketRouteProposals,
            state.AcceptedMarketProposals);
        Assert.Equal(reference.AcceptedRouteOnlyProposals,
            state.AcceptedRouteProposals);
        Assert.Equal(reference.RejectedNonImprovingProposals,
            state.RejectedProposals);
        Assert.Equal(reference.FinalIntegratedNetPotentialScaled,
            state.BestPotentialScaled);
        Assert.Equal(reference.AcceptedPotentialHistoryScaled,
            state.AcceptedPotentialHistoryScaled);
        Assert.True(state.BestPotentialScaled > initial);
        Assert.Same(state, FoundationPhysicalProgressiveMarketRouteCoordinator
            .Advance(state, maxProposalEvaluations: 1));
        Assert.Equal(hash, scenario.Host.StateHash);
        Assert.Same(snapshot, scenario.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void GainFloorSkipsInsignificantSearchWithoutRoundingAnyMarketPrice()
    {
        var scenario = TwoRealMarkets(100_000UL);
        var initial = Begin(scenario, BigInteger.One << 512);
        var halfway = FoundationPhysicalProgressiveMarketRouteCoordinator
            .Advance(initial, maxProposalEvaluations: 1);
        Assert.False(halfway.StableUnderDeclaredGainFloor);
        Assert.Equal(1, halfway.NextProposalIndex);
        Assert.Equal(0, halfway.CompletedPasses);
        Assert.Equal(initial.BestPotentialScaled, halfway.BestPotentialScaled);
        var stable = FoundationPhysicalProgressiveMarketRouteCoordinator
            .Advance(halfway, maxProposalEvaluations: 1);
        Assert.True(stable.StableUnderDeclaredGainFloor);
        Assert.Equal(2, stable.ProposalsEvaluated);
        Assert.Equal(1, stable.CompletedPasses);
        Assert.Equal(0, stable.AcceptedMarketProposals +
            stable.AcceptedRouteProposals);
        Assert.Equal(initial.BestPotentialScaled, stable.BestPotentialScaled);
        Assert.Single(stable.AcceptedPotentialHistoryScaled);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ChangedQuoteRevaluesAuditedIncumbentWhileChangedCensusForcesFullSolve()
    {
        var scenario = TwoRealMarkets(100_000UL);
        var initial = Begin(scenario, BigInteger.Zero);
        var advanced = FoundationPhysicalProgressiveMarketRouteCoordinator
            .Advance(initial, maxProposalEvaluations: 1);
        var prices = scenario.Quotes.Select((line, index) =>
            index == 0
                ? new FoundationCommodityMarketPlanningQuote(
                    line.BuyerTile, line.Buyer, line.Commodity,
                    checked(line.QuoteMicroPerWholeCommodityUnit + 1UL))
                : line).ToArray();
        var repriced = Begin(scenario, BigInteger.Zero,
            previous: advanced, quotes: prices);
        Assert.NotSame(advanced, repriced);
        Assert.Equal(
            FoundationPhysicalProgressiveSeedKind.AuditedQuoteRevaluation,
            repriced.SeedKind);
        Assert.Same(advanced.BestAuditedCandidate,
            repriced.BestAuditedCandidate);
        Assert.Equal(
            FoundationPhysicalJointMarketRoutePotentialCoordinator
                .IntegratedNetPotentialScaled(
                    advanced.BestAuditedCandidate, prices),
            repriced.BestPotentialScaled);
        Assert.Equal(0, repriced.ProposalsEvaluated);
        Assert.Equal(0, repriced.CompletedPasses);
        Assert.False(repriced.StableUnderDeclaredGainFloor);
        Assert.Single(repriced.AcceptedPotentialHistoryScaled);
        Assert.Equal(1_000_000UL,
            repriced.BestAuditedCandidate.AllocationAudit
                .TotalAllocatedGoodsMicro);
        Assert.Same(repriced,
            Begin(scenario, BigInteger.Zero,
                previous: repriced, quotes: prices));
        // If a changed physical worker state is supplied, even with
        // identical economic output, never inherit a stale continuation.
        var separateCensus = new PhysicalWorkerRuntimeState(
            scenario.Workers.Workers,
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var restarted = FoundationPhysicalProgressiveMarketRouteCoordinator
            .BeginOrResume(separateCensus, scenario.Host.Incidence,
                scenario.Demand, scenario.Cargo, prices, scenario.Roads,
                scenario.Scales, 8, 256, 4, 16,
                BigInteger.Zero, previous: repriced);
        Assert.NotSame(repriced, restarted);
        Assert.Equal(FoundationPhysicalProgressiveSeedKind.FullGlobalSolve,
            restarted.SeedKind);
        Assert.Equal(0, restarted.ProposalsEvaluated);
    }

    [Fact]
    public void RoadAndCarryingScaleChangesReauditAndRerouteCurrentBuyerQuantities()
    {
        var scenario = TwoRealMarkets(100_000UL);
        var hostHash = scenario.Host.StateHash;
        var before = Begin(scenario, BigInteger.Zero);
        var advanced = FoundationPhysicalProgressiveMarketRouteCoordinator
            .Advance(before, maxProposalEvaluations: 1);
        var revisedRoads = scenario.Roads.Select(road => road with
        {
            PhysicalCostMicroPerWholeCargoUnit =
                checked(road.PhysicalCostMicroPerWholeCargoUnit + 25_000UL),
            ExternalTariffMicroPerWholeCargoUnit = 17_000UL,
            OwnershipKind = FoundationInfrastructureOwnershipKind.Owned,
            InfrastructureOwner = Blue
        }).ToArray();
        var revisedScales = scenario.Scales.Select(scale => scale with
        {
            CargoMicroAtReferenceLoad =
                checked(scale.CargoMicroAtReferenceLoad + 25_000UL)
        }).ToArray();

        var rebased = FoundationPhysicalProgressiveMarketRouteCoordinator
            .BeginOrResume(scenario.Workers, scenario.Host.Incidence,
                scenario.Demand, scenario.Cargo, scenario.Quotes,
                revisedRoads, revisedScales, 8, 256, 4, 16,
                BigInteger.Zero, previous: advanced);
        Assert.Equal(
            FoundationPhysicalProgressiveSeedKind.ReauditedFixedMarketRouting,
            rebased.SeedKind);
        Assert.NotSame(advanced.BestAuditedCandidate,
            rebased.BestAuditedCandidate);
        Assert.Same(advanced.BestAuditedCandidate.MarketAllocation,
            rebased.BestAuditedCandidate.MarketAllocation);
        Assert.Equal(0, rebased.ProposalsEvaluated);
        Assert.Single(rebased.AcceptedPotentialHistoryScaled);
        Assert.False(rebased.StableUnderDeclaredGainFloor);
        Assert.Equal(1_000_000UL,
            rebased.BestAuditedCandidate.AllocationAudit
                .TotalAllocatedGoodsMicro);
        Assert.Equal(rebased.BestAuditedCandidate.AllocationAudit
                .TotalAvailableGoodsMicro,
            rebased.BestAuditedCandidate.AllocationAudit
                .TotalAllocatedGoodsMicro);
        Assert.Equal(revisedScales[0].CargoMicroAtReferenceLoad,
            rebased.BestAuditedCandidate.ProjectedEdges[0]
                .CargoMicroAtReferenceLoad);
        Assert.Equal(revisedRoads[0].PhysicalCostMicroPerWholeCargoUnit,
            rebased.BestAuditedCandidate.ProjectedEdges[0]
                .FreePhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(rebased.BestPotentialScaled,
            FoundationPhysicalJointMarketRoutePotentialCoordinator
                .IntegratedNetPotentialScaled(
                    rebased.BestAuditedCandidate, scenario.Quotes));
        Assert.Same(rebased,
            FoundationPhysicalProgressiveMarketRouteCoordinator
                .BeginOrResume(scenario.Workers, scenario.Host.Incidence,
                    scenario.Demand, scenario.Cargo, scenario.Quotes,
                    revisedRoads, revisedScales, 8, 256, 4, 16,
                    BigInteger.Zero, previous: rebased));

        var next = FoundationPhysicalProgressiveMarketRouteCoordinator
            .Advance(rebased, maxProposalEvaluations: 1);
        Assert.True(next.BestPotentialScaled >= rebased.BestPotentialScaled);
        Assert.Equal(1, next.ProposalsEvaluated);
        Assert.Equal(hostHash, scenario.Host.StateHash);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void ChangedDemandIdentityPreventsWarmStartAndRebuildsRealGlobalMarkets()
    {
        var scenario = TwoRealMarkets(100_000UL);
        var original = Begin(scenario, BigInteger.Zero);
        var changedOrder = scenario.Quotes.Reverse().ToArray();
        var reset = Begin(scenario, BigInteger.Zero,
            previous: original, quotes: changedOrder);
        Assert.NotSame(original, reset);
        Assert.Equal(FoundationPhysicalProgressiveSeedKind.FullGlobalSolve,
            reset.SeedKind);
        Assert.NotSame(original.BestAuditedCandidate,
            reset.BestAuditedCandidate);
        Assert.Equal(0, reset.ProposalsEvaluated);
        Assert.Equal(reset.BestAuditedCandidate.AllocationAudit
            .TotalAvailableGoodsMicro,
            reset.BestAuditedCandidate.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void RepricedWarmSeedAlwaysResetsCrossEpochPotentialHistoryAndBudget()
    {
        var scenario = TwoRealMarkets(100_000UL);
        var state = Begin(scenario, BigInteger.Zero);
        state = FoundationPhysicalProgressiveMarketRouteCoordinator
            .Advance(state, maxProposalEvaluations: 2);
        Assert.True(state.ProposalsEvaluated > 0);
        var quote = scenario.Quotes[0];
        var revised = scenario.Quotes.ToArray();
        revised[0] = new FoundationCommodityMarketPlanningQuote(
            quote.BuyerTile, quote.Buyer, quote.Commodity,
            checked(quote.QuoteMicroPerWholeCommodityUnit + 5_000UL));
        var nextEpoch = Begin(scenario, BigInteger.Zero,
            previous: state, quotes: revised);
        Assert.Equal(
            FoundationPhysicalProgressiveSeedKind.AuditedQuoteRevaluation,
            nextEpoch.SeedKind);
        Assert.Same(state.BestAuditedCandidate,
            nextEpoch.BestAuditedCandidate);
        Assert.Equal(0, nextEpoch.ProposalsEvaluated);
        Assert.Equal(0, nextEpoch.AcceptedMarketProposals +
            nextEpoch.AcceptedRouteProposals);
        Assert.Single(nextEpoch.AcceptedPotentialHistoryScaled);
        Assert.Equal(nextEpoch.BestPotentialScaled,
            nextEpoch.AcceptedPotentialHistoryScaled[0]);
        Assert.False(nextEpoch.StableUnderDeclaredGainFloor);
        Assert.Equal(0, nextEpoch.CompletedPasses);
        Assert.Equal(0, nextEpoch.NextProposalIndex);
        Assert.Equal(nextEpoch.BestAuditedCandidate.AllocationAudit
            .TotalAvailableGoodsMicro,
            nextEpoch.BestAuditedCandidate.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Blue).Availability);
    }

    [Fact]
    public void InvalidBudgetAndNegativeGainFloorAreRejectedBeforeAnyProposal()
    {
        var scenario = TwoRealMarkets(100_000UL);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Begin(scenario, BigInteger.MinusOne));
        var state = Begin(scenario, BigInteger.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalProgressiveMarketRouteCoordinator
                .Advance(state, maxProposalEvaluations: 0));
    }

    [Fact]
    public void DuplicateRouteElisionMatchesOriginalAcrossColdQuoteRoadAndGainFloor()
    {
        var scenario = TwoRealMarkets(100_000UL);
        var hash = scenario.Host.StateHash;
        var snapshot = scenario.Host.Snapshot;

        static string RouteSignature(
            FoundationPhysicalProgressiveMarketRouteState state) =>
            string.Join(";", state.BestAuditedCandidate.Parts.Select(part =>
                string.Join("|", part.ParentDeliveryOrdinal,
                    part.Delivery.Seller.Value,
                    part.Delivery.Buyer.Value,
                    part.Delivery.GoodsMicro,
                    string.Join(",", part.Delivery.RouteTiles.Select(
                        tile => tile.FineStrategicCellId.Value)))));

        void AssertSameEconomics(
            FoundationPhysicalProgressiveMarketRouteState expected,
            FoundationPhysicalProgressiveMarketRouteState actual)
        {
            Assert.Equal(expected.BestPotentialScaled, actual.BestPotentialScaled);
            Assert.Equal(expected.SeedKind, actual.SeedKind);
            Assert.Equal(expected.ProposalsEvaluated, actual.ProposalsEvaluated);
            Assert.Equal(expected.AcceptedMarketProposals,
                actual.AcceptedMarketProposals);
            Assert.Equal(expected.AcceptedRouteProposals,
                actual.AcceptedRouteProposals);
            Assert.Equal(expected.RejectedProposals, actual.RejectedProposals);
            Assert.Equal(expected.CompletedPasses, actual.CompletedPasses);
            Assert.Equal(expected.NextProposalIndex, actual.NextProposalIndex);
            Assert.Equal(expected.ImprovedInCurrentPass,
                actual.ImprovedInCurrentPass);
            Assert.Equal(expected.StableUnderDeclaredGainFloor,
                actual.StableUnderDeclaredGainFloor);
            Assert.Equal(expected.AcceptedPotentialHistoryScaled,
                actual.AcceptedPotentialHistoryScaled);
            Assert.Equal(RouteSignature(expected), RouteSignature(actual));
            Assert.Equal(
                expected.BestAuditedCandidate.AllocationAudit.TotalAvailableGoodsMicro,
                actual.BestAuditedCandidate.AllocationAudit.TotalAvailableGoodsMicro);
            Assert.Equal(
                expected.BestAuditedCandidate.AllocationAudit.TotalAllocatedGoodsMicro,
                actual.BestAuditedCandidate.AllocationAudit.TotalAllocatedGoodsMicro);
            Assert.Equal(
                expected.BestAuditedCandidate.FinalIntegratedFreightCost,
                actual.BestAuditedCandidate.FinalIntegratedFreightCost);
            Assert.Equal(
                expected.BestAuditedCandidate.ProjectedEdges.Select(edge =>
                    (edge.Edge, edge.CargoMicro,
                        edge.CongestedPhysicalCostMicroPerWholeCargoUnit)),
                actual.BestAuditedCandidate.ProjectedEdges.Select(edge =>
                    (edge.Edge, edge.CargoMicro,
                        edge.CongestedPhysicalCostMicroPerWholeCargoUnit)));
        }

        void ReplayToStability(
            FoundationPhysicalProgressiveMarketRouteState initial)
        {
            var reference = initial;
            var optimized = initial;
            int evaluatedRoutes = 0;
            for (int i = 0; i < 64 && !reference.StableUnderDeclaredGainFloor; i++)
            {
                int before = reference.NextProposalIndex;
                reference = FoundationPhysicalProgressiveMarketRouteCoordinator
                    .AdvanceWithOriginalRoutingForTests(reference, 1);
                optimized = FoundationPhysicalProgressiveMarketRouteCoordinator
                    .Advance(optimized, 1);
                if (before == 1)
                    evaluatedRoutes++;
                AssertSameEconomics(reference, optimized);
            }
            Assert.True(reference.StableUnderDeclaredGainFloor);
            Assert.True(optimized.StableUnderDeclaredGainFloor);
            Assert.True(evaluatedRoutes >= 1);
            Assert.Equal(hash, scenario.Host.StateHash);
            Assert.Same(snapshot, scenario.Host.Snapshot);
            Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
            Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
                scenario.Host.GetLastClosedEconomicTradeView(Blue).Availability);
        }

        ReplayToStability(Begin(scenario, BigInteger.Zero));
        ReplayToStability(Begin(scenario, BigInteger.One << 512));
        var original = Begin(scenario, BigInteger.Zero);
        var quote = scenario.Quotes[0];
        var revisedQuotes = scenario.Quotes.ToArray();
        revisedQuotes[0] = new FoundationCommodityMarketPlanningQuote(
            quote.BuyerTile, quote.Buyer, quote.Commodity,
            checked(quote.QuoteMicroPerWholeCommodityUnit + 50_000UL));
        var repriced = Begin(scenario, BigInteger.Zero,
            previous: original, quotes: revisedQuotes);
        Assert.Equal(FoundationPhysicalProgressiveSeedKind.AuditedQuoteRevaluation,
            repriced.SeedKind);
        ReplayToStability(repriced);

        var roads = scenario.Roads.Select(road => road with
        {
            PhysicalCostMicroPerWholeCargoUnit =
                checked(road.PhysicalCostMicroPerWholeCargoUnit + 25_000UL),
            ExternalTariffMicroPerWholeCargoUnit = 17_000UL,
            OwnershipKind = FoundationInfrastructureOwnershipKind.Owned,
            InfrastructureOwner = Blue
        }).ToArray();
        var scales = scenario.Scales.Select(scale => scale with
        {
            CargoMicroAtReferenceLoad =
                checked(scale.CargoMicroAtReferenceLoad + 25_000UL)
        }).ToArray();
        var rerouted = FoundationPhysicalProgressiveMarketRouteCoordinator
            .BeginOrResume(scenario.Workers, scenario.Host.Incidence,
                scenario.Demand, scenario.Cargo,
                revisedQuotes, roads, scales, 8, 256, 4, 16,
                BigInteger.Zero, previous: repriced);
        Assert.Equal(
            FoundationPhysicalProgressiveSeedKind.ReauditedFixedMarketRouting,
            rerouted.SeedKind);
        ReplayToStability(rerouted);
    }
}
