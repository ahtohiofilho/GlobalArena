using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// R172 diagnostic-only: measured costs and quantity changes of the EXISTING
/// marginal planner. Never a new economic algorithm or an elapsed-time gate.
/// </summary>
public sealed class FoundationMarginalMarketComplexityAuditTests
{
    private readonly ITestOutputHelper _output;
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId Food = new(1U);

    public FoundationMarginalMarketComplexityAuditTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(5_000_000UL, true)]
    [InlineData(50_000_000UL, true)]
    [InlineData(50_000_000UL, false)]
    public void RecordPassesAndFirstToFinalMarketChangesWithoutChangingEconomics(
        ulong outputPerWorker, bool connectMarkets)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var incidence = host.Incidence;
        var first = incidence.FineTopology.Edges[0];
        var fine = incidence.Refinement.FineParameters;
        var a = new PhysicalTacticalTileId(fine, first.IncidentCellIds[0]);
        var b = new PhysicalTacticalTileId(fine, first.IncidentCellIds[1]);
        StrategicCellId Coarse(PhysicalTacticalTileId tile) =>
            incidence.TileIncidences[
                checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];
        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red, Coarse(a), Food,
                outputPerWorker, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Blue, Coarse(b), Food,
                outputPerWorker, PhysicalWorkerActivity.Producing,
                1_000_000U, b, PhysicalWorkerOccupation.Forestry)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
        var demand = new FoundationActivityCommodityDemandPolicy(
            new[] { Food },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .Select(profile =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, Food, 1_000_000UL)));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "audit-cargo-micro",
            new[] { new FoundationCommodityCargoConversion(Food, 1_000_000UL) });
        var market = FoundationPhysicalCommodityDemandProjector.Project(
            workers, demand);
        var quotes = market.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 10_000_000UL)).ToArray();
        var roads = connectMarkets
            ? new[] { new FoundationPhysicalRoutingEdgeCost(
                new PhysicalTacticalEdgeId(fine, first.Id),
                FoundationInfrastructureOwnershipKind.NoInfrastructure,
                null, 100_000UL, 0UL) }
            : Array.Empty<FoundationPhysicalRoutingEdgeCost>();

        var clock = Stopwatch.StartNew();
        var result = FoundationPhysicalGlobalMarginalCoordinator.Coordinate(
            workers, incidence, demand, cargo, quotes, roads, maxPasses: 128);
        clock.Stop();

        var initial = result.InitialMarketTotals
            .GroupBy(x => (x.Tile, x.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL,
                (sum, x) => checked(sum + x.AllocatedGoodsMicro)));
        var final = result.MarketTotals
            .GroupBy(x => (x.Tile, x.Commodity))
            .ToDictionary(g => g.Key, g => g.Aggregate(0UL,
                (sum, x) => checked(sum + x.AllocatedGoodsMicro)));
        ulong quantityShift = 0UL;
        foreach (var key in initial.Keys)
        {
            var before = initial[key];
            var after = final[key];
            quantityShift = checked(quantityShift +
                (before >= after ? before - after : after - before));
        }
        quantityShift /= 2UL; // half of absolute market deltas, one transfer

        Assert.True(result.StableUnderOneFullCanonicalPass);
        Assert.Equal(2, result.ProductiveLotCount);
        Assert.Equal(checked(outputPerWorker * 2UL),
            result.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(0UL, result.MovedGoodsByPass.Last());
        Assert.True(result.Passes <= 128);
        if (!connectMarkets)
        {
            Assert.Equal(1, result.Passes);
            Assert.Equal(0UL, quantityShift);
        }
        _output.WriteLine(
            $"R172 AUDIT connected={connectMarkets} workerOutputMicro={outputPerWorker} " +
            $"lots={result.ProductiveLotCount} passes={result.Passes} " +
            $"firstToFinalShiftMicro={quantityShift} " +
            $"movedByPass=[{string.Join(",", result.MovedGoodsByPass)}] " +
            $"elapsedMs={clock.Elapsed.TotalMilliseconds:F3} " +
            $"freightEdgeTraversals={result.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro}");
    }
}
