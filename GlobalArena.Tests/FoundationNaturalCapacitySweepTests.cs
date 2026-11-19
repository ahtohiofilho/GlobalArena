using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Issue #435. QA-ONLY sensitivity sweep. Frozen REAL workers and the same
/// universal market quotes across every row; ONLY natural carrying scales K
/// differ. It does not mutate the world, pay money, or promote game balance.
/// </summary>
public sealed class FoundationNaturalCapacitySweepTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private static readonly CommodityId One = new(1U);
    private static readonly CommodityId Two = new(2U);
    private readonly ITestOutputHelper _output;

    public FoundationNaturalCapacitySweepTests(ITestOutputHelper output) =>
        _output = output;

    private sealed record Scenario(
        FoundationTacticalHumanAiTurnHost Host,
        PhysicalWorkerRuntimeState Workers,
        FoundationActivityCommodityDemandPolicy Demand,
        FoundationCommodityCargoConversionPolicy Cargo);

    private sealed record Cycle(
        int Index, int UsedEdges, int ChangedEdges, string Signature,
        ulong TotalAvailableGoods, ulong TotalAllocatedGoods,
        double MaxLandRatio, double MaxCoastRatio, double MaxSeaRatio,
        double MaxCurrentSurchargePercent, double MaxNextSurchargePercent);

    private static Scenario BuildScenario()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        // Identical QA specialization to the interactive Godot economy lab.
        var workers = new PhysicalWorkerRuntimeState(
            host.Snapshot.Workers.Workers.Select(w =>
                new PhysicalWorkerRuntimeRecord(
                    w.Id, w.Owner, w.CellId,
                    w.Owner == Red ? One : Two,
                    w.OutputMicroPerTurn, w.Activity,
                    w.DemandMultiplierMicro, w.TacticalTileId, w.Occupation)),
            tacticalLocationsAuthoritative: true,
            occupationsAuthoritative: true);
        var demand = new FoundationActivityCommodityDemandPolicy(
            new[] { One, Two },
            Enum.GetValues<FoundationPhysicalDemandProfile>()
                .SelectMany(profile => new[] { One, Two }.Select(item =>
                    new FoundationActivityCommodityDemandCoefficient(
                        profile, item, 1_000_000UL))));
        var cargo = new FoundationCommodityCargoConversionPolicy(
            "explicit-godot-economic-qa-only", new[]
            {
                new FoundationCommodityCargoConversion(One, 1_000_000UL),
                new FoundationCommodityCargoConversion(Two, 1_000_000UL)
            });
        Assert.Equal(6, workers.Workers.Count);
        Assert.Equal(3, workers.Workers.Count(w => w.Owner == Red));
        Assert.Equal(3, workers.Workers.Count(w => w.Owner == Blue));
        return new Scenario(host, workers, demand, cargo);
    }

    private static IReadOnlyList<Cycle> Sweep(
        Scenario s, ulong land, ulong coast, ulong sea)
    {
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            s.Host.Session.GeneratedWorld, s.Host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, land),
                new FoundationNaturalTransportCostBand(5_000UL, coast),
                new FoundationNaturalTransportCostBand(25_000UL, sea),
                10_000UL));
        var roads = seed.Transport.Edges.Select(e =>
            new FoundationPhysicalRoutingEdgeCost(
                e.Edge, FoundationInfrastructureOwnershipKind.NoInfrastructure,
                null, e.FreePhysicalCostMicroPerWholeCargoUnit, 0UL)).ToArray();
        var scales = seed.Transport.Edges.Select(e =>
            new FoundationPhysicalEdgeCarryingScale(
                e.Edge, e.CargoMicroAtReferenceLoad)).ToArray();
        var quote = FoundationPhysicalUniversalMarketPricing
            .QuotesFromRealWorkers(s.Workers, s.Demand);
        // Ensure only K changes; the natural graph and C0 are held fixed.
        Assert.Equal(seed.LandCrossingCount + seed.CoastCrossingCount +
            seed.SeaCrossingCount, roads.Length);
        FoundationPhysicalLaggedFreightQuoteState? prior = null;
        Dictionary<PhysicalTacticalEdgeId, ulong> previous = new();
        var results = new List<Cycle>();
        for (int cycle = 1; cycle <= 4; cycle++)
        {
            var plan = FoundationPhysicalLaggedFreightCyclePlanner.CoordinateCandidate(
                s.Workers, s.Host.Incidence, s.Demand, s.Cargo,
                quote, roads, scales, previousClosedCycleQuotes: prior,
                maxProducerPasses: 64);
            var audit = plan.Distribution.AllocationAudit;
            Assert.Equal(audit.TotalAvailableGoodsMicro,
                audit.TotalAllocatedGoodsMicro);
            Assert.False(plan.RepricedWithinCycle);
            var loadMap = audit.Freight.UsedEdges.ToDictionary(
                e => e.Edge, e => e.TotalCargoMicro);
            int changes = loadMap.Keys.Union(previous.Keys)
                .Count(e => loadMap.GetValueOrDefault(e) !=
                    previous.GetValueOrDefault(e));
            static double MaxRatio(
                IEnumerable<FoundationPhysicalLaggedFreightEdgeReading> rows,
                ulong capacity) =>
                rows.Where(e => e.CargoMicroAtReferenceLoad == capacity)
                    .Select(e => (double)e.CurrentCargoMicro /
                        e.CargoMicroAtReferenceLoad)
                    .DefaultIfEmpty(0).Max();
            var readings = plan.EdgeReadings;
            results.Add(new Cycle(
                cycle, loadMap.Count, cycle == 1 ? 0 : changes,
                string.Join(";", loadMap.OrderBy(x => x.Key.FineStrategicEdgeId.Value)
                    .Select(x => $"{x.Key.FineStrategicEdgeId.Value}:{x.Value}")),
                audit.TotalAvailableGoodsMicro,
                audit.TotalAllocatedGoodsMicro,
                MaxRatio(readings, land), MaxRatio(readings, coast),
                MaxRatio(readings, sea),
                readings.Select(x => 100.0 *
                    ((double)x.CurrentPhysicalQuoteMicroPerWholeCargoUnit /
                     x.FreePhysicalCostMicroPerWholeCargoUnit - 1.0))
                    .DefaultIfEmpty(0).Max(),
                readings.Select(x => 100.0 *
                    ((double)x.DampedNextPhysicalQuoteMicroPerWholeCargoUnit /
                     x.FreePhysicalCostMicroPerWholeCargoUnit - 1.0))
                    .DefaultIfEmpty(0).Max()));
            previous = loadMap;
            prior = plan.NextCycleQuotes;
        }
        return results;
    }

    [Fact]
    public void CapacityOnlySweepQuantifiesThreeCycleRouteChurnWithoutChangingWorld()
    {
        var scenario = BuildScenario();
        var initialHash = scenario.Host.StateHash;
        var initialSnapshot = scenario.Host.Snapshot;

        // Baseline is the exact QA K hierarchy: land > coast > sea.
        // Later rows change K ONLY; the geometric graph, C0, demand, goods,
        // workers, universal prices, smoothing and seed remain identical.
        var rows = new (string Name, ulong Land, ulong Coast, ulong Sea)[]
        {
            ("qa_current", 1_000_000UL, 500_000UL, 100_000UL),
            ("all_x10", 10_000_000UL, 5_000_000UL, 1_000_000UL),
            ("all_x100", 100_000_000UL, 50_000_000UL, 10_000_000UL),
            ("all_x1000", 1_000_000_000UL, 500_000_000UL, 100_000_000UL),
            // Structurally cheaper bottleneck at coast, large maritime K.
            ("sea_abundant_coast_narrow",
                100_000_000UL, 5_000_000UL, 1_000_000_000UL)
        };
        _output.WriteLine("GA_K_SWEEP_HEADER profile cycle used_edges changed_edge_loads " +
            "max_V_K_land max_V_K_coast max_V_K_sea max_current_surcharge_pct " +
            "max_next_surcharge_pct");
        foreach (var row in rows)
        {
            var cycles = Sweep(scenario, row.Land, row.Coast, row.Sea);
            Assert.Equal(4, cycles.Count);
            Assert.All(cycles, cycle =>
                Assert.Equal(cycle.TotalAvailableGoods, cycle.TotalAllocatedGoods));
            foreach (var cycle in cycles)
                _output.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "GA_K_SWEEP profile={0} cycle={1} used_edges={2} " +
                    "changed_edge_loads={3} max_V_K_land={4:F6} " +
                    "max_V_K_coast={5:F6} max_V_K_sea={6:F6} " +
                    "max_current_surcharge_pct={7:F6} max_next_surcharge_pct={8:F6}",
                    row.Name, cycle.Index, cycle.UsedEdges, cycle.ChangedEdges,
                    cycle.MaxLandRatio, cycle.MaxCoastRatio, cycle.MaxSeaRatio,
                    cycle.MaxCurrentSurchargePercent,
                    cycle.MaxNextSurchargePercent));
            var anchor = cycles[0];
            _output.WriteLine($"GA_K_SWEEP_SUMMARY profile={row.Name} " +
                $"last_three_route_revisions={string.Join(",", cycles.Skip(1).Select(c => c.ChangedEdges))} " +
                $"signature_stable={cycles.All(c => c.Signature == anchor.Signature)}");
        }
        // All scenarios remain hypothetical and cannot credit treasury or
        // move workers; live authority stays byte-identical throughout.
        Assert.Same(initialSnapshot, scenario.Host.Snapshot);
        Assert.Equal(initialHash, scenario.Host.StateHash);
        Assert.Equal(1UL, scenario.Host.NextTurn.Value);
    }
    [Fact]
    public void RealCoastalAndSeaCrossingsExposeTheirOwnCapacityThresholds()
    {
        var s = BuildScenario();
        var frozen = s.Host.Snapshot;
        var digest = s.Host.StateHash;
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            s.Host.Session.GeneratedWorld, s.Host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(25_000UL, 100_000UL),
                10_000UL));
        Assert.True(seed.CoastCrossingCount > 0);
        Assert.True(seed.SeaCrossingCount > 0);
        var sourceRed = s.Workers.Workers.First(w => w.Owner == Red);
        var sourceBlue = s.Workers.Workers.First(w => w.Owner == Blue);

        foreach (var (kind, originalK, multiplier) in new[]
        {
            ("coast", 500_000UL, 1UL),
            ("coast", 500_000UL, 10UL),
            ("coast", 500_000UL, 100UL),
            ("sea", 100_000UL, 1UL),
            ("sea", 100_000UL, 100UL),
            ("sea", 100_000UL, 10_000UL)
        })
        {
            var infiniteSea = kind == "sea";
            var edge = seed.Transport.Edges.First(e =>
                e.CargoMicroAtReferenceLoad == (infiniteSea
                    ? FoundationPhysicalEndogenousCongestionFeedback
                        .UnboundedNaturalCargoCapacity
                    : originalK));
            var topology = s.Host.Incidence.FineTopology.Edges.First(e =>
                e.Id == edge.Edge.FineStrategicEdgeId);
            var a = new PhysicalTacticalTileId(
                s.Host.Incidence.Refinement.FineParameters,
                topology.IncidentCellIds[0]);
            var b = new PhysicalTacticalTileId(
                s.Host.Incidence.Refinement.FineParameters,
                topology.IncidentCellIds[1]);
            StrategicCellId coarse(PhysicalTacticalTileId tile) =>
                s.Host.Incidence.TileIncidences[
                    checked((int)tile.FineStrategicCellId.Value - 1)]
                    .IncidentCoarseCellIds[0];
            // Two of the SAME real demo worker IDs, explicitly relocated to
            // opposite sides of a REAL edge in this uncommitted test scenario.
            // One supplies each commodity; neither makes a game move.
            var census = new PhysicalWorkerRuntimeState(new[]
            {
                new PhysicalWorkerRuntimeRecord(sourceRed.Id, Red, coarse(a),
                    One, sourceRed.OutputMicroPerTurn,
                    PhysicalWorkerActivity.Producing,
                    sourceRed.DemandMultiplierMicro, a, sourceRed.Occupation),
                new PhysicalWorkerRuntimeRecord(sourceBlue.Id, Blue, coarse(b),
                    Two, sourceBlue.OutputMicroPerTurn,
                    PhysicalWorkerActivity.Producing,
                    sourceBlue.DemandMultiplierMicro, b, sourceBlue.Occupation)
            }, tacticalLocationsAuthoritative: true,
                occupationsAuthoritative: true);
            var road = new FoundationPhysicalRoutingEdgeCost(
                edge.Edge, FoundationInfrastructureOwnershipKind.NoInfrastructure,
                null, edge.FreePhysicalCostMicroPerWholeCargoUnit, 0UL);
            var scale = new FoundationPhysicalEdgeCarryingScale(
                edge.Edge, infiniteSea
                    ? FoundationPhysicalEndogenousCongestionFeedback
                        .UnboundedNaturalCargoCapacity
                    : checked(originalK * multiplier));
            var quotes = FoundationPhysicalUniversalMarketPricing
                .QuotesFromRealWorkers(census, s.Demand);
            FoundationPhysicalLaggedFreightQuoteState? prior = null;
            for (int turn = 1; turn <= 3; turn++)
            {
                var plan = FoundationPhysicalLaggedFreightCyclePlanner
                    .CoordinateCandidate(census, s.Host.Incidence,
                        s.Demand, s.Cargo, quotes, new[] { road },
                        new[] { scale }, previousClosedCycleQuotes: prior,
                        maxProducerPasses: 64);
                Assert.Equal(plan.Distribution.AllocationAudit.TotalAvailableGoodsMicro,
                    plan.Distribution.AllocationAudit.TotalAllocatedGoodsMicro);
                var reading = Assert.Single(plan.EdgeReadings);
                if (turn == 1)
                    Assert.True(reading.CurrentCargoMicro > 0UL,
                        "The isolated real coastal/sea edge must actually be used.");
                if (infiniteSea)
                    Assert.Equal(reading.FreePhysicalCostMicroPerWholeCargoUnit,
                        reading.DampedNextPhysicalQuoteMicroPerWholeCargoUnit);
                _output.WriteLine(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "GA_K_CROSSING class={0} K_multiplier={1} cycle={2} " +
                    "cargo_micro={3} V_over_K={4:F6} " +
                    "base_cost={5} current_quote={6} next_quote={7}",
                    kind, multiplier, turn, reading.CurrentCargoMicro,
                    (double)reading.CurrentCargoMicro / scale.CargoMicroAtReferenceLoad,
                    reading.FreePhysicalCostMicroPerWholeCargoUnit,
                    reading.CurrentPhysicalQuoteMicroPerWholeCargoUnit,
                    reading.DampedNextPhysicalQuoteMicroPerWholeCargoUnit));
                prior = plan.NextCycleQuotes;
            }
        }
        Assert.Same(frozen, s.Host.Snapshot);
        Assert.Equal(digest, s.Host.StateHash);
    }

}
