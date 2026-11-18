using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Architectural pivot: congestion is a next-cycle freight quotation,
/// NEVER an input recalculated by the CURRENT cycle's water-filling.
/// These are uncommitted candidate tests; no settled cycle is invented.
/// </summary>
public sealed class FoundationPhysicalLaggedFreightCycleTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Grain = new(1U);
    private static readonly CommodityId Wood = new(2U);

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial,
                new GoldbergParameters(3, 0)));

    private static FoundationActivityCommodityDemandPolicy Demand() =>
        new(new[] { Grain, Wood },
            Enum.GetValues<FoundationPhysicalDemandProfile>().SelectMany(
                profile => new[] { Grain, Wood }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));

    private static FoundationCommodityCargoConversionPolicy Cargo() =>
        new("lagged-two-commodity-shared-cargo", new[]
        {
            new FoundationCommodityCargoConversion(Grain, 1_000_000UL),
            new FoundationCommodityCargoConversion(Wood, 1_000_000UL)
        });

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationCommodityMarketPlanningQuote[] Quotes,
        FoundationPhysicalRoutingEdgeCost[] Roads,
        FoundationPhysicalEdgeCarryingScale[] Scales,
        PhysicalTacticalEdgeId Crossing);

    private static Scenario TwoGoodsOneRealCrossing()
    {
        var host = Host();
        var map = host.Incidence;
        var border = map.FineTopology.Edges[0];
        var fine = map.Refinement.FineParameters;
        var a = new PhysicalTacticalTileId(fine, border.IncidentCellIds[0]);
        var b = new PhysicalTacticalTileId(fine, border.IncidentCellIds[1]);
        var crossing = new PhysicalTacticalEdgeId(fine, border.Id);
        StrategicCellId coarse(PhysicalTacticalTileId t) =>
            map.TileIncidences[checked((int)t.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];
        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                Red, coarse(a), Grain, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U, a,
                PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                Blue, coarse(a), Wood, 1_000_000UL,
                PhysicalWorkerActivity.Producing, 1_000_000U, a,
                PhysicalWorkerOccupation.Forestry),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(3UL),
                Blue, coarse(b), Grain, 0UL,
                PhysicalWorkerActivity.Traveling, 1_000_000U, b,
                PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var policy = Demand();
        var quotes = FoundationPhysicalCommodityDemandProjector.Project(
            workers, policy).DemandByCivilization.Select(line =>
                new FoundationCommodityMarketPlanningQuote(
                    line.Tile, line.Owner, line.Commodity,
                    line.Tile == b ? 1_000_000_000UL :
                        1_000_000UL)).ToArray();
        var road = new FoundationPhysicalRoutingEdgeCost(
            crossing, FoundationInfrastructureOwnershipKind.Owned, Blue,
            100_000UL, 30_000UL);
        return new Scenario(host, workers, quotes,
            new[] { road },
            new[] { new FoundationPhysicalEdgeCarryingScale(
                crossing, 1_000_000UL) }, crossing);
    }

    [Fact]
    public void FirstCycleQuotesFreeInfrastructureAndLaterCycleUsesOnlyPreviousLoad()
    {
        var s = TwoGoodsOneRealCrossing();
        var hash = s.Host.StateHash;
        var snapshot = s.Host.Snapshot;
        var first = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateCandidate(s.Workers, s.Host.Incidence,
                Demand(), Cargo(), s.Quotes, s.Roads, s.Scales,
                maxProducerPasses: 8);

        Assert.False(first.RepricedWithinCycle);
        Assert.Equal(1UL, first.NextCycleQuotes.AuditedPreviewCycles);
        Assert.True(first.Distribution.StableUnderOneFullCanonicalPass);
        Assert.Equal(2_000_000UL,
            first.Distribution.AllocationAudit.TotalAvailableGoodsMicro);
        Assert.Equal(2_000_000UL,
            first.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
        var initialRoad = Assert.Single(first.FixedCycleEdges);
        Assert.Equal(100_000UL,
            initialRoad.PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(30_000UL, initialRoad.ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(Blue, initialRoad.InfrastructureOwner);

        var firstReading = Assert.Single(first.EdgeReadings);
        Assert.Equal(s.Crossing, firstReading.Edge);
        Assert.True(firstReading.CurrentCargoMicro > 0UL);
        Assert.Equal(100_000UL,
            firstReading.CurrentPhysicalQuoteMicroPerWholeCargoUnit);
        Assert.True(firstReading.TargetNextPhysicalQuoteMicroPerWholeCargoUnit >
            firstReading.CurrentPhysicalQuoteMicroPerWholeCargoUnit);
        Assert.True(firstReading.DampedNextPhysicalQuoteMicroPerWholeCargoUnit >
            firstReading.CurrentPhysicalQuoteMicroPerWholeCargoUnit);
        Assert.True(firstReading.DampedNextPhysicalQuoteMicroPerWholeCargoUnit <
            firstReading.TargetNextPhysicalQuoteMicroPerWholeCargoUnit);

        // The same turn has already closed its fixed-cost allocation.
        // A different quote is legal only for the next *candidate* cycle.
        var next = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateCandidate(s.Workers, s.Host.Incidence,
                Demand(), Cargo(), s.Quotes, s.Roads, s.Scales,
                previousClosedCycleQuotes: first.NextCycleQuotes,
                maxProducerPasses: 8);
        Assert.False(next.RepricedWithinCycle);
        Assert.Equal(2UL, next.NextCycleQuotes.AuditedPreviewCycles);
        Assert.Equal(firstReading.DampedNextPhysicalQuoteMicroPerWholeCargoUnit,
            Assert.Single(next.FixedCycleEdges)
                .PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(30_000UL,
            Assert.Single(next.FixedCycleEdges)
                .ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(hash, s.Host.StateHash);
        Assert.Same(snapshot, s.Host.Snapshot);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Red).Availability);
        Assert.Equal(FoundationEconomicTradeOverlayAvailability.NoClosedCycle,
            s.Host.GetLastClosedEconomicTradeView(Blue).Availability);
    }

    [Fact]
    public void FixedFreightCandidateExactlyMatchesExistingWaterFillingSolve()
    {
        var s = TwoGoodsOneRealCrossing();
        var freeze = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateCandidate(s.Workers, s.Host.Incidence,
                Demand(), Cargo(), s.Quotes, s.Roads, s.Scales,
                maxProducerPasses: 8);
        var reference = FoundationPhysicalGlobalMarginalCoordinator
            .Coordinate(s.Workers, s.Host.Incidence,
                Demand(), Cargo(), s.Quotes, s.Roads,
                maxPasses: 8);
        Assert.Equal(reference.Passes, freeze.Distribution.Passes);
        Assert.Equal(reference.MarketTotals, freeze.Distribution.MarketTotals);
        Assert.Equal(reference.Candidates.Select(x => (
                x.Seller, x.SourceTile, x.Buyer, x.BuyerTile, x.Commodity,
                x.GoodsMicro, string.Join(",",
                    x.RouteTiles.Select(t => t.FineStrategicCellId.Value)))),
            freeze.Distribution.Candidates.Select(x => (
                x.Seller, x.SourceTile, x.Buyer, x.BuyerTile, x.Commodity,
                x.GoodsMicro, string.Join(",",
                    x.RouteTiles.Select(t => t.FineStrategicCellId.Value)))));
        Assert.Equal(reference.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro,
            freeze.Distribution.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro);
    }

    [Fact]
    public void DampedSurchargeApproachesConstantPriorCargoWithoutOverreacting()
    {
        ulong free = 100_000UL;
        ulong quoteSurcharge = 0UL;
        ulong targetSurcharge = free; // V/reference=1 implies target=2*free.
        for (int cycle = 0; cycle < 20; cycle++)
        {
            ulong next = FoundationPhysicalLaggedFreightCyclePlanner
                .NextSurcharge(free, quoteSurcharge, 1_000_000UL,
                    1_000_000UL, smoothingDenominator: 4);
            Assert.True(next >= quoteSurcharge);
            Assert.True(next <= targetSurcharge);
            if (cycle == 0)
                Assert.Equal(25_000UL, next);
            quoteSurcharge = next;
        }
        Assert.InRange(quoteSurcharge, 99_000UL, 100_000UL);

        // With zero observed cargo, the same smoothing policy reduces the
        // surcharge gradually and NEVER reprices the already-solved turn.
        ulong cooling = FoundationPhysicalLaggedFreightCyclePlanner
            .NextSurcharge(free, 100_000UL, 0UL, 1_000_000UL,
                smoothingDenominator: 4);
        Assert.Equal(75_000UL, cooling);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalLaggedFreightCyclePlanner.NextSurcharge(
                free, 0UL, 1_000_000UL, 1_000_000UL, 0U));
    }

    [Fact]
    public void InfrastructureChangeResetsOnlyObsoleteQuoteAndPreservesToll()
    {
        var s = TwoGoodsOneRealCrossing();
        var first = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateCandidate(s.Workers, s.Host.Incidence,
                Demand(), Cargo(), s.Quotes, s.Roads, s.Scales);
        var upgraded = s.Roads.Select(r => r with
        {
            PhysicalCostMicroPerWholeCargoUnit = 80_000UL,
            ExternalTariffMicroPerWholeCargoUnit = 45_000UL
        }).ToArray();
        var next = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateCandidate(s.Workers, s.Host.Incidence,
                Demand(), Cargo(), s.Quotes, upgraded, s.Scales,
                previousClosedCycleQuotes: first.NextCycleQuotes);
        var rate = Assert.Single(next.FixedCycleEdges);
        Assert.Equal(80_000UL, rate.PhysicalCostMicroPerWholeCargoUnit);
        Assert.Equal(45_000UL, rate.ExternalTariffMicroPerWholeCargoUnit);
        Assert.Equal(Blue, rate.InfrastructureOwner);
    }

    [Fact]
    public void InvalidRoadCapacityAndStaleCrossingStateRejectBeforeAnyTrade()
    {
        var s = TwoGoodsOneRealCrossing();
        var initial = FoundationPhysicalLaggedFreightCyclePlanner
            .CoordinateCandidate(s.Workers, s.Host.Incidence,
                Demand(), Cargo(), s.Quotes, s.Roads, s.Scales);
        var badScales = new[]
        {
            Array.Empty<FoundationPhysicalEdgeCarryingScale>(),
            new[] { s.Scales[0], s.Scales[0] },
            new[] { s.Scales[0] with { CargoMicroAtReferenceLoad = 0UL } }
        };
        foreach (var bad in badScales)
            Assert.Throws<ArgumentException>(() =>
                FoundationPhysicalLaggedFreightCyclePlanner.CoordinateCandidate(
                    s.Workers, s.Host.Incidence, Demand(), Cargo(),
                    s.Quotes, s.Roads, bad));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalLaggedFreightCyclePlanner.CoordinateCandidate(
                s.Workers, s.Host.Incidence, Demand(), Cargo(),
                s.Quotes, Array.Empty<FoundationPhysicalRoutingEdgeCost>(),
                s.Scales, previousClosedCycleQuotes: initial.NextCycleQuotes));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FoundationPhysicalLaggedFreightCyclePlanner.CoordinateCandidate(
                s.Workers, s.Host.Incidence, Demand(), Cargo(),
                s.Quotes, s.Roads, s.Scales,
                smoothingDenominator: 0U));
    }
}
