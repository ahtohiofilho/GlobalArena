using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Gate 01B.2D-8: opt-in cross-block market quantity and physical multipath
/// improvements must strictly increase the SAME audited integrated potential.
/// No test assumes endogenously priced global optimality or monetary clearing.
/// </summary>
public sealed class FoundationPhysicalJointMarketRoutePotentialTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalJointMarketRoutePotentialTests(
        ITestOutputHelper output) => _output = output;

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain },
            Enum.GetValues<FoundationPhysicalDemandProfile>().Select(
                profile => new FoundationActivityCommodityDemandCoefficient(
                    profile, Grain, 1_000_000UL)));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("joint-market-route-cargo-micro", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL)
        });

    private sealed record Case(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationCommodityMarketPlanningQuote[] Quotes,
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Scales);

    private static Case AdjacentBuyerCase(ulong referenceLoad)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var map = host.Incidence;
        var border = map.FineTopology.Edges[0];
        var source = new PhysicalTacticalTileId(
            map.Refinement.FineParameters, border.IncidentCellIds[0]);
        var buyer = new PhysicalTacticalTileId(
            map.Refinement.FineParameters, border.IncidentCellIds[1]);
        StrategicCellId coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[
                checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];

        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                Red, coarse(source), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                source, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                Blue, coarse(buyer), Grain, 0UL,
                PhysicalWorkerActivity.Other, 1_000_000U,
                buyer, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);

        var quotes = FoundationPhysicalCommodityDemandProjector
            .Project(workers, Demand()).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    100_000_000UL)).ToArray();
        var road = new FoundationPhysicalRoutingEdgeCost(
            new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                border.Id),
            FoundationInfrastructureOwnershipKind.NoInfrastructure,
            null, 100_000UL, 0UL);
        return new Case(host, workers, quotes, new[] { road },
            new[] { new FoundationPhysicalEdgeCarryingScale(
                road.Edge, referenceLoad) });
    }

    [Fact]
    public void LowCongestionRealWorkerMarketEndsOnNonImprovingFullBlockPass()
    {
        var scenario = AdjacentBuyerCase(100_000_000_000UL);
        var hash = scenario.Host.StateHash;
        var snapshot = scenario.Host.Snapshot;
        var result = FoundationPhysicalJointMarketRoutePotentialCoordinator
            .Coordinate(scenario.Workers, scenario.Host.Incidence, Demand(),
                Cargo(), scenario.Quotes, scenario.Roads, scenario.Scales,
                segmentsPerAverageMarket: 8,
                maxFlowAugmentationsPerProposal: 256,
                maxRouteBatchesPerParent: 4,
                maxTotalRouteBatches: 16,
                maxCompletePasses: 24);
        Assert.True(result.StableUnderOneCompleteBlockPass);
        Assert.InRange(result.CompletePasses, 1, 24);
        Assert.Equal(1_000_000UL,
            result.Allocation.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(result.Allocation.AllocationAudit.TotalAvailableGoodsMicro,
            result.Allocation.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(result.FinalIntegratedNetPotentialScaled,
            FoundationPhysicalJointMarketRoutePotentialCoordinator
                .IntegratedNetPotentialScaled(
                    result.Allocation, scenario.Quotes));
        Assert.Equal(result.AcceptedMarketRouteProposals +
            result.AcceptedRouteOnlyProposals + 1,
            result.AcceptedPotentialHistoryScaled.Count);
        Assert.All(result.AcceptedPotentialHistoryScaled.Skip(1)
            .Zip(result.AcceptedPotentialHistoryScaled),
            pair => Assert.True(pair.First > pair.Second));
        Assert.Equal(hash, scenario.Host.StateHash);
        Assert.Same(snapshot, scenario.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void CongestedMarketCanMoveDestinationQuantaOnlyOnStrictJointGain()
    {
        var scenario = AdjacentBuyerCase(100_000UL);
        var hash = scenario.Host.StateHash;
        var snapshot = scenario.Host.Snapshot;
        var result = FoundationPhysicalJointMarketRoutePotentialCoordinator
            .Coordinate(scenario.Workers, scenario.Host.Incidence, Demand(),
                Cargo(), scenario.Quotes, scenario.Roads, scenario.Scales,
                segmentsPerAverageMarket: 8,
                maxFlowAugmentationsPerProposal: 256,
                maxRouteBatchesPerParent: 4,
                maxTotalRouteBatches: 16,
                maxCompletePasses: 32);
        _output.WriteLine(
            $"jointPotential passes={result.CompletePasses}, " +
            $"marketRouteAccepts={result.AcceptedMarketRouteProposals}, " +
            $"routeOnlyAccepts={result.AcceptedRouteOnlyProposals}, " +
            $"potentialHistory=" +
            string.Join(",", result.AcceptedPotentialHistoryScaled));
        Assert.True(result.AcceptedMarketRouteProposals > 0);
        Assert.True(result.StableUnderOneCompleteBlockPass);
        Assert.All(result.AcceptedPotentialHistoryScaled.Skip(1)
            .Zip(result.AcceptedPotentialHistoryScaled),
            pair => Assert.True(pair.First > pair.Second));
        Assert.Equal(1_000_000UL,
            result.Allocation.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(result.FinalIntegratedNetPotentialScaled,
            FoundationPhysicalJointMarketRoutePotentialCoordinator
                .IntegratedNetPotentialScaled(
                    result.Allocation, scenario.Quotes));
        Assert.Equal(hash, scenario.Host.StateHash);
        Assert.Same(snapshot, scenario.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void OnePassBudgetDoesNotConcealAcceptedButUnstableMarketResponse()
    {
        var scenario = AdjacentBuyerCase(100_000UL);
        var hash = scenario.Host.StateHash;
        var snapshot = scenario.Host.Snapshot;
        var failure = Assert.Throws<
            FoundationPhysicalJointMarketRouteBudgetException>(() =>
            FoundationPhysicalJointMarketRoutePotentialCoordinator
                .Coordinate(scenario.Workers, scenario.Host.Incidence, Demand(),
                    Cargo(), scenario.Quotes, scenario.Roads, scenario.Scales,
                    segmentsPerAverageMarket: 8,
                    maxFlowAugmentationsPerProposal: 256,
                    maxRouteBatchesPerParent: 4,
                    maxTotalRouteBatches: 16,
                    maxCompletePasses: 1));
        Assert.Equal(1, failure.CompletePassBudget);
        Assert.True(failure.AcceptedProposals > 0);
        Assert.Equal(hash, scenario.Host.StateHash);
        Assert.Same(snapshot, scenario.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            scenario.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void JointMarketRouteRejectsInvalidBudgetBeforeBuildingCandidate()
    {
        var scenario = AdjacentBuyerCase(100_000UL);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalJointMarketRoutePotentialCoordinator.Coordinate(
                scenario.Workers, scenario.Host.Incidence, Demand(), Cargo(),
                scenario.Quotes, scenario.Roads, scenario.Scales,
                segmentsPerAverageMarket: 8,
                maxFlowAugmentationsPerProposal: 256,
                maxRouteBatchesPerParent: 4, maxTotalRouteBatches: 16,
                maxCompletePasses: 0));
    }
}
