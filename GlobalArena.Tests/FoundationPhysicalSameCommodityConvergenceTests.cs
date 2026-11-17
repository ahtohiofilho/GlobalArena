using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Replays the exact difficult scenario encountered while testing #396:
/// distinct productive source tiles sell ONE AND THE SAME commodity into
/// many physical buyer markets connected through a shared fine Goldberg
/// road graph. This is not eight separate commodity problems.
/// </summary>
public sealed class FoundationPhysicalSameCommodityConvergenceTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalSameCommodityConvergenceTests(
        ITestOutputHelper output) => _output = output;

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain },
            Enum.GetValues<FoundationPhysicalDemandProfile>().Select(
                profile => new FoundationActivityCommodityDemandCoefficient(
                    profile, Grain, 1_000_000UL)));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("same-commodity-shared-cargo-micro", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL)
        });

    private sealed record Case(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationCommodityMarketPlanningQuote[] Quotes,
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Scales);

    private static Case ConnectedEightSourceCase()
    {
        const int physicalCells = 112;
        const int sellers = 8;
        const int nonSellers = 96;
        var host = Host();
        var map = host.Incidence;
        var seed = map.FineTopology.Cells[0].Id;
        var queue = new Queue<StrategicCellId>();
        var discovered = new HashSet<StrategicCellId> { seed };
        var component = new List<StrategicCellId>();
        queue.Enqueue(seed);
        while (queue.Count > 0 && component.Count < physicalCells)
        {
            var id = queue.Dequeue();
            component.Add(id);
            foreach (var adjacent in map.FineTopology.Cells[
                         checked((int)id.Value - 1)].AdjacentCellIds
                         .OrderBy(next => next.Value))
                if (discovered.Add(adjacent))
                    queue.Enqueue(adjacent);
        }
        if (component.Count != physicalCells)
            throw new InvalidOperationException(
                "Expected a connected 112-tile fine topology.");
        var chosen = component.ToHashSet();
        var borders = map.FineTopology.Edges.Where(edge =>
            chosen.Contains(edge.IncidentCellIds[0]) &&
            chosen.Contains(edge.IncidentCellIds[1])).ToArray();
        if (borders.Length <= physicalCells)
            throw new InvalidOperationException(
                "Stress graph must have shared alternative corridors.");

        PhysicalTacticalTileId tile(StrategicCellId id) =>
            new(map.Refinement.FineParameters, id);
        StrategicCellId coarse(StrategicCellId id) =>
            map.TileIncidences[checked((int)id.Value - 1)]
                .IncidentCoarseCellIds[0];

        var people = new List<PhysicalWorkerRuntimeRecord>(sellers + nonSellers);
        for (int i = 0; i < sellers; i++)
            people.Add(new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId((ulong)i + 1UL),
                Red, coarse(component[i]), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U,
                tile(component[i]), PhysicalWorkerOccupation.Agriculture));
        for (int i = 0; i < nonSellers; i++)
        {
            var location = component[physicalCells - nonSellers + i];
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
        var roads = borders.Select(border =>
            new FoundationPhysicalRoutingEdgeCost(
                new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                    border.Id),
                FoundationInfrastructureOwnershipKind.NoInfrastructure,
                null, 100_000UL, 0UL)).ToArray();
        var scales = roads.Select(road =>
            new FoundationPhysicalEdgeCarryingScale(
                road.Edge, 100_000_000UL)).ToArray();
        return new Case(host, census, quotes, roads, scales);
    }

    [Fact]
    public void OriginalSameCommodityStressCannotPretendThirtyTwoPassesAreStable()
    {
        var stress = ConnectedEightSourceCase();
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
                stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
                stress.Quotes, stress.Roads, maxPasses: 32));
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void IncumbentAwareThresholdsFindFullPassFixedPointInConnectedStress()
    {
        var stress = ConnectedEightSourceCase();
        var timer = Stopwatch.StartNew();
        var plan = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
            stress.Quotes, stress.Roads, maxPasses: 256,
            preferIncumbentThresholdTies: true,
            acceptOnlyStrictIntegratedGain: true);
        timer.Stop();

        _output.WriteLine(
            $"Same commodity stressed: sourceLots={plan.ProductiveLotCount}, " +
            $"realBuyerMarkets={plan.MarketTotals.Count}, " +
            $"physicalRoads={stress.Roads.Length}, " +
            $"passes={plan.Passes}, movedGoodsMicro={plan.TotalReallocatedGoodsMicro}, " +
            $"rejectedNonImproving={plan.RejectedNonImprovingSellerProposals}, " +
            $"acceptedPotential={plan.AcceptedPotentialGainScaled}, " +
            $"milliseconds={timer.ElapsedMilliseconds}");
        Assert.True(plan.StableUnderOneFullCanonicalPass);
        Assert.True(plan.PreferIncumbentThresholdTies);
        Assert.True(plan.StrictIntegratedPotentialAcceptance);
        Assert.True(plan.AcceptedPotentialGainScaled >= 0);
        Assert.Equal(8, plan.ProductiveLotCount);
        Assert.Equal(104, plan.MarketTotals.Count);
        Assert.InRange(plan.Passes, 1, 256);
        Assert.Equal(plan.Passes, plan.MovedGoodsByPass.Count);
        Assert.Equal(0UL, plan.MovedGoodsByPass[^1]);
        Assert.Equal(8_000_000UL,
            plan.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(plan.AllocationAudit.TotalAvailableGoodsMicro,
            plan.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(8_000_000UL,
            plan.MarketTotals.Aggregate(0UL, (sum, x) =>
                checked(sum + x.AllocatedGoodsMicro)));
        Assert.True(plan.AllocationAudit.Freight.UsedEdges.Count > 1);
        Assert.All(plan.AllocationAudit.Freight.UsedEdges, crossing =>
        {
            Assert.Contains(stress.Roads, road => road.Edge == crossing.Edge);
        });
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            stress.Host.GetLastClosedEconomicTradeView(Red).Availability);
    }

    [Fact]
    public void OptInRemainsDeterministicAndDoesNotModifyPhysicalWorkerSnapshot()
    {
        var host = Host();
        var snapshot = host.Snapshot;
        var hash = host.StateHash;
        var policy = new FoundationActivityCommodityDemandPolicy(
            new[] { Grain, new CommodityId(2U) },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(activity => new[] { Grain, new CommodityId(2U) }
                    .Select(item => new FoundationActivityCommodityDemandCoefficient(
                        activity, item, 1_000_000UL))));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "same-commodity-shared-cargo-micro", new[]
            {
                new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
                new FoundationCommodityCargoConversion(new CommodityId(2U),
                    1_000_000UL)
            });
        var quote = host.GetCommodityMarketInputs(policy)
            .DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    1_000_000UL)).ToArray();
        var original = host.CoordinateGlobalMarginalMarketCandidates(
            policy, cargo, quote,
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            maxPasses: 8);
        var first = host.CoordinateGlobalMarginalMarketCandidates(
            policy, cargo, quote,
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            maxPasses: 8, preferIncumbentThresholdTies: true,
            acceptOnlyStrictIntegratedGain: true);
        var second = host.CoordinateGlobalMarginalMarketCandidates(
            policy, cargo, quote.Reverse(),
            Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
            maxPasses: 8, preferIncumbentThresholdTies: true,
            acceptOnlyStrictIntegratedGain: true);
        Assert.False(original.PreferIncumbentThresholdTies);
        Assert.True(first.PreferIncumbentThresholdTies);
        Assert.True(first.StrictIntegratedPotentialAcceptance);
        Assert.Equal(
            first.Candidates.Select(x => (
                x.DeliveryOrdinal, x.Seller, x.Buyer, x.Commodity,
                x.GoodsMicro, x.SourceTile, x.BuyerTile,
                Route: string.Join(",", x.RouteTiles.Select(tile =>
                    tile.FineStrategicCellId.Value)))),
            second.Candidates.Select(x => (
                x.DeliveryOrdinal, x.Seller, x.Buyer, x.Commodity,
                x.GoodsMicro, x.SourceTile, x.BuyerTile,
                Route: string.Join(",", x.RouteTiles.Select(tile =>
                    tile.FineStrategicCellId.Value)))));
        Assert.Equal(first.MovedGoodsByPass.ToArray(),
            second.MovedGoodsByPass.ToArray());
        Assert.Equal(11_000_000UL,
            first.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(hash, host.StateHash);
        Assert.Same(snapshot, host.Snapshot);
    }

    [Fact]
    public void EvenWithIncumbentTiesInsufficientBudgetMustFailClosed()
    {
        var stress = ConnectedEightSourceCase();
        Assert.Throws<InvalidOperationException>(() =>
            FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
                stress.Workers, stress.Host.Incidence, Demand(), Cargo(),
                stress.Quotes, stress.Roads, maxPasses: 1,
                preferIncumbentThresholdTies: true,
                acceptOnlyStrictIntegratedGain: true));
    }
}
