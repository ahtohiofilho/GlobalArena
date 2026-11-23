using System.Diagnostics;
using System.Numerics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
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

        // Independent first-pass candidate: same workers, prices and fixed
        // costs; do not approximate or mutate the production solver.
        var firstPass = FoundationPhysicalMarginalMarketDistributor.Distribute(
            workers, incidence, demand, cargo, quotes, roads);

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

        // The exact canonical price function from the accounting reconciler:
        // floor(sqrt(D/Q) * 1e6), with one shared physical Q per market.
        // Gross = sum(Q * price * 10_000), in the accounting scaled units.
        // This is an audit of gross revenue, not a new pricing mechanism.
        static BigInteger GrossRevenue(
            IReadOnlyList<FoundationPhysicalMarketAllocationTotal> totals)
        {
            BigInteger gross = BigInteger.Zero;
            foreach (var market in totals.GroupBy(
                x => (x.Tile, x.Commodity)))
            {
                ulong q = market.Aggregate(0UL,
                    (sum, row) => checked(sum + row.AllocatedGoodsMicro));
                if (q == 0UL) continue;
                ulong d = market.Aggregate(0UL,
                    (sum, row) => checked(sum + row.DemandMicro));
                BigInteger price =
                    FoundationPhysicalMarginalIntegratedUtility.IntegerSqrt(
                        (BigInteger)d * 1_000_000UL * 1_000_000UL / q);
                gross += (BigInteger)q * price * 10_000UL;
            }
            return gross;
        }
        BigInteger firstGross = GrossRevenue(firstPass.MarketTotals);
        BigInteger finalGross = GrossRevenue(result.MarketTotals);
        BigInteger grossDelta = finalGross - firstGross;
        BigInteger grossDeltaBasisPoints = firstGross == BigInteger.Zero
            ? BigInteger.Zero
            : grossDelta * 10_000 / firstGross;

        Assert.Equal(firstPass.MarketTotals.ToArray(),
            result.InitialMarketTotals.ToArray());
        Assert.Equal(firstPass.AllocationAudit.TotalAllocatedGoodsMicro,
            result.AllocationAudit.TotalAllocatedGoodsMicro);


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
            $"freightEdgeTraversals={result.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro} " +
            $"firstGrossScaled={firstGross} finalGrossScaled={finalGross} " +
            $"grossDeltaScaled={grossDelta} grossDeltaBasisPoints={grossDeltaBasisPoints}");
    }

    [Theory]
    [InlineData(5_000_000UL)]
    [InlineData(50_000_000UL)]
    public void MeasureCanonicalNetBenefitWithActualFreight(ulong outputPerWorker)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var map = host.Incidence;
        var edge = map.FineTopology.Edges[0];
        var fine = map.Refinement.FineParameters;
        var a = new PhysicalTacticalTileId(fine, edge.IncidentCellIds[0]);
        var b = new PhysicalTacticalTileId(fine, edge.IncidentCellIds[1]);
        StrategicCellId Coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];

        // The third worker is a real non-producing consumer at B. This
        // creates physical demand that can attract exports from A without
        // any artificial non-universal price quote or congestion feedback.
        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red, Coarse(a), Food,
                outputPerWorker, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Blue, Coarse(b), Food,
                outputPerWorker, PhysicalWorkerActivity.Producing,
                1_000_000U, b, PhysicalWorkerOccupation.Forestry),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(3UL), Blue, Coarse(b), Food,
                0UL, PhysicalWorkerActivity.Other,
                10_000_000U, b, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
           occupationsAuthoritative: true);
        var demand = new FoundationActivityCommodityDemandPolicy(
            new[] { Food },
            Enum.GetValues<FoundationPhysicalDemandProfile>().Select(profile =>
                new FoundationActivityCommodityDemandCoefficient(
                    profile, Food, 1_000_000UL)));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "audit-real-freight-cargo",
            new[] { new FoundationCommodityCargoConversion(Food, 1_000_000UL) });
        var market = FoundationPhysicalCommodityDemandProjector.Project(workers, demand);
        var quotes = market.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 1_000_000UL)).ToArray();
        const ulong freightRate = 100_000UL;
        var costs = new[] { new FoundationPhysicalRoutingEdgeCost(
            new PhysicalTacticalEdgeId(fine, edge.Id),
            FoundationInfrastructureOwnershipKind.NoInfrastructure,
            null, freightRate, 0UL) };

        var first = FoundationPhysicalMarginalMarketDistributor.Distribute(
            workers, map, demand, cargo, quotes, costs);
        var observations = new List<FoundationMarginalPassObservation>();
        var clock = Stopwatch.StartNew();
        var final = FoundationPhysicalGlobalMarginalCoordinator.CoordinateAudited(
            market, map, cargo, quotes, costs, maxPasses: 128,
            preferIncumbentThresholdTies: false,
            acceptOnlyStrictIntegratedGain: false,
            observer: observations.Add);
        clock.Stop();

        // Same exact integer square-root pricing as the canonical financial
        // reconciler. No rounding to cents and no independent price rule.
        static BigInteger Gross(
            IReadOnlyList<FoundationPhysicalMarketAllocationTotal> totals)
        {
            BigInteger gross = BigInteger.Zero;
            foreach (var group in totals.GroupBy(x => (x.Tile, x.Commodity)))
            {
                ulong q = group.Aggregate(0UL,
                    (sum, x) => checked(sum + x.AllocatedGoodsMicro));
                if (q == 0UL) continue;
                ulong d = group.Aggregate(0UL,
                    (sum, x) => checked(sum + x.DemandMicro));
                BigInteger price = FoundationPhysicalMarginalIntegratedUtility
                    .IntegerSqrt((BigInteger)d * 1_000_000UL * 1_000_000UL / q);
                gross += (BigInteger)q * price * 10_000UL;
            }
            return gross;
        }

        // With exactly one declared physical edge and one cargo micro-unit
        // per goods micro-unit, these are the same scaled physical charges
        // as the existing accounting reconciler. No tariffs or income taxes.
        static BigInteger Freight(
            IEnumerable<FoundationPhysicalCommodityDeliveryCandidate> candidates)
        {
            BigInteger total = BigInteger.Zero;
            foreach (var delivery in candidates)
            {
                if (delivery.RouteTiles.Count is not (1 or 2))
                    throw new InvalidOperationException("Unexpected audit route.");
                if (delivery.RouteTiles.Count == 2)
                    total += (BigInteger)delivery.GoodsMicro *
                        freightRate * 10_000UL;
            }
            return total;
        }

        // Use the exact same shared D/Q financial rule at every observed
        // point, including pass zero. Each reading is an immutable snapshot.
        BigInteger ObservedGross(FoundationMarginalPassObservation point)
        {
            BigInteger gross = BigInteger.Zero;
            foreach (var row in market.MarketDemand)
            {
                ulong q = point.MarketGoodsMicro[
                    (row.Tile, row.Commodity)];
                if (q == 0UL) continue;
                BigInteger price = FoundationPhysicalMarginalIntegratedUtility
                    .IntegerSqrt((BigInteger)row.DemandMicro *
                        1_000_000UL * 1_000_000UL / q);
                gross += (BigInteger)q * price * 10_000UL;
            }
            return gross;
        }
        var perPassNet = observations.Select(point =>
            ObservedGross(point) - point.PhysicalFreightScaled).ToArray();

        BigInteger firstGross = Gross(first.MarketTotals);
        BigInteger finalGross = Gross(final.MarketTotals);
        BigInteger firstFreight = Freight(first.Candidates);
        BigInteger finalFreight = Freight(final.Candidates);
        BigInteger firstNet = firstGross - firstFreight;
        BigInteger finalNet = finalGross - finalFreight;
        BigInteger difference = finalNet - firstNet;
        BigInteger basisPoints = firstNet.IsZero
            ? BigInteger.Zero : difference * 10_000 / firstNet;
        Assert.Equal(final.Passes + 1, observations.Count);
        Assert.Equal(0, observations[0].PassIndex);
        Assert.Equal(firstGross, ObservedGross(observations[0]));
        Assert.Equal(firstFreight, observations[0].PhysicalFreightScaled);
        Assert.Equal(finalGross, ObservedGross(observations[^1]));
        Assert.Equal(finalFreight, observations[^1].PhysicalFreightScaled);
        Assert.Equal(firstNet, perPassNet[0]);
        Assert.Equal(finalNet, perPassNet[^1]);
        Assert.Equal(0UL, observations[^1].MovedGoodsMicro);
        Assert.Equal(perPassNet[^1], perPassNet[^2]);
        Assert.True(firstFreight > BigInteger.Zero,
            "Audit must route real cargo across the edge initially.");
        Assert.True(finalFreight > BigInteger.Zero,
            "Audit must route real cargo across the edge finally.");
        Assert.Equal(checked(outputPerWorker * 2UL),
            first.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(first.AllocationAudit.TotalAllocatedGoodsMicro,
            final.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(first.MarketTotals.ToArray(),
            final.InitialMarketTotals.ToArray());
        Assert.Equal(0UL, final.MovedGoodsByPass.Last());

        _output.WriteLine(
            $"R174 REAL FREIGHT outputMicro={outputPerWorker} " +
            $"passes={final.Passes} firstGross={firstGross} finalGross={finalGross} " +
            $"firstFreight={firstFreight} finalFreight={finalFreight} " +
            $"firstNet={firstNet} finalNet={finalNet} netDelta={difference} " +
            $"netDeltaBasisPoints={basisPoints} elapsedMs={clock.Elapsed.TotalMilliseconds:F3} " +
            $"firstUsedEdges={first.AllocationAudit.Freight.UsedEdges.Count} " +
            $"finalUsedEdges={final.AllocationAudit.Freight.UsedEdges.Count} " +
            $"perPassNetScaled=[{string.Join(",", perPassNet)}] " +
            $"perPassGainBasisPoints=[{string.Join(",", perPassNet.Select(
                value => firstNet.IsZero ? BigInteger.Zero :
                    (value - firstNet) * 10_000 / firstNet))}]");
    }

    [Theory]
    [InlineData(5_000_000UL)]
    [InlineData(20_000_000UL)]
    public void ThreeRealProducerLotsExposeIncrementalNetBenefit(
        ulong goodsPerProducer)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var map = host.Incidence;
        var edge = map.FineTopology.Edges[0];
        var fine = map.Refinement.FineParameters;
        var a = new PhysicalTacticalTileId(fine, edge.IncidentCellIds[0]);
        var b = new PhysicalTacticalTileId(fine, edge.IncidentCellIds[1]);
        StrategicCellId Coarse(PhysicalTacticalTileId tile) =>
            map.TileIncidences[checked((int)tile.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds[0];

        // Three distinct real (tile, owner, occupation, commodity) lots:
        // two producers on A, one on B, and a real additional consumer on B.
        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(1UL), Red, Coarse(a), Food,
                goodsPerProducer, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(2UL), Red, Coarse(a), Food,
                goodsPerProducer, PhysicalWorkerActivity.Producing,
                1_000_000U, a, PhysicalWorkerOccupation.Forestry),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(3UL), Blue, Coarse(b), Food,
                goodsPerProducer, PhysicalWorkerActivity.Producing,
                1_000_000U, b, PhysicalWorkerOccupation.Agriculture),
            new PhysicalWorkerRuntimeRecord(
                new PhysicalWorkerId(4UL), Blue, Coarse(b), Food,
                0UL, PhysicalWorkerActivity.Other,
                12_000_000U, b, PhysicalWorkerOccupation.Unassigned)
        }, tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = new FoundationActivityCommodityDemandPolicy(
            new[] { Food },
            Enum.GetValues<FoundationPhysicalDemandProfile>().Select(profile =>
                new FoundationActivityCommodityDemandCoefficient(
                    profile, Food, 1_000_000UL)));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "audit-3-lot-cargo",
            new[] { new FoundationCommodityCargoConversion(Food, 1_000_000UL) });
        var market = FoundationPhysicalCommodityDemandProjector.Project(
            workers, demand);
        var quotes = market.DemandByCivilization.Select(line =>
            new FoundationCommodityMarketPlanningQuote(
                line.Tile, line.Owner, line.Commodity, 1_000_000UL)).ToArray();
        var costs = new[] { new FoundationPhysicalRoutingEdgeCost(
            new PhysicalTacticalEdgeId(fine, edge.Id),
            FoundationInfrastructureOwnershipKind.NoInfrastructure,
            null, 100_000UL, 0UL) };
        var first = FoundationPhysicalMarginalMarketDistributor.Distribute(
            workers, map, demand, cargo, quotes, costs);
        var readings = new List<FoundationMarginalPassObservation>();
        var clock = Stopwatch.StartNew();
        var final = FoundationPhysicalGlobalMarginalCoordinator.CoordinateAudited(
            market, map, cargo, quotes, costs, maxPasses: 128,
            preferIncumbentThresholdTies: false,
            acceptOnlyStrictIntegratedGain: false,
            observer: readings.Add);
        clock.Stop();

        BigInteger Net(FoundationMarginalPassObservation observation)
        {
            BigInteger revenue = BigInteger.Zero;
            foreach (var line in market.MarketDemand)
            {
                ulong q = observation.MarketGoodsMicro[(line.Tile, line.Commodity)];
                if (q == 0UL) continue;
                BigInteger price = FoundationPhysicalMarginalIntegratedUtility
                    .IntegerSqrt((BigInteger)line.DemandMicro *
                        1_000_000UL * 1_000_000UL / q);
                revenue += (BigInteger)q * price * 10_000UL;
            }
            return revenue - observation.PhysicalFreightScaled;
        }
        var net = readings.Select(Net).ToArray();
        Assert.Equal(3, final.ProductiveLotCount);
        Assert.Equal(3, market.SupplyLots.Count);
        Assert.Equal(final.Passes + 1, readings.Count);
        Assert.Equal(checked(goodsPerProducer * 3UL),
            final.AllocationAudit.TotalAllocatedGoodsMicro);
        Assert.Equal(first.MarketTotals.ToArray(),
            final.InitialMarketTotals.ToArray());
        Assert.True(first.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro > 0UL,
            "The multi-producer audit must pay real transport.");
        Assert.True(final.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro > 0UL,
            "The stabilized audit must keep a physical crossing.");
        Assert.Equal(0UL, readings[^1].MovedGoodsMicro);
        Assert.Equal(net[^2], net[^1]);
        Assert.Equal(net.Length, readings.Count);
        _output.WriteLine(
            $"R176 MULTI PRODUCER goodsPerLotMicro={goodsPerProducer} " +
            $"lots={final.ProductiveLotCount} passes={final.Passes} " +
            $"firstNet={net[0]} finalNet={net[^1]} " +
            $"deltaNet={net[^1] - net[0]} " +
            $"perPassNetScaled=[{string.Join(",", net)}] " +
            $"perPassDeltaBp=[{string.Join(",", net.Select(x =>
                net[0].IsZero ? BigInteger.Zero :
                    (x - net[0]) * 10_000 / net[0]))}] " +
            $"firstCargo={first.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro} " +
            $"finalCargo={final.AllocationAudit.Freight.TotalEdgeTraversalCargoMicro} " +
            $"elapsedMs={clock.Elapsed.TotalMilliseconds:F3}");
    }
}
