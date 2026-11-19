using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// R58 decision gate: do not confuse fewer Dijkstra solves with a faster
/// overall trade market. Measure cold and warm calls, managed allocations,
/// resident cache entries, exact chosen coast crossings and both modes,
/// at two distinct real Goldberg world sizes. No authority is mutated.
/// These times are CI-environment observations, not deterministic game data.
/// </summary>
public sealed class FoundationPhysicalMultimodalRoiBenchmarkTests
{
    private readonly ITestOutputHelper _log;
    private static readonly CivilizationId Red = new(1UL);

    public FoundationPhysicalMultimodalRoiBenchmarkTests(
        ITestOutputHelper log) => _log = log;

    private static HashSet<ulong> Flood(
        ulong source, Dictionary<ulong, List<ulong>> links)
    {
        var reachable = new HashSet<ulong> { source };
        var pending = new Queue<ulong>();
        pending.Enqueue(source);
        while (pending.TryDequeue(out ulong at))
            if (links.TryGetValue(at, out var neighbors))
                foreach (ulong next in neighbors)
                    if (reachable.Add(next)) pending.Enqueue(next);
        return reachable;
    }

    private sealed record Timing(
        FoundationPhysicalMultimodalRouteComparator.Result Result,
        double Milliseconds, long AllocatedBytes);

    private static Timing Measure(
        Func<FoundationPhysicalMultimodalRouteComparator.Result> run)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var result = run();
        watch.Stop();
        return new Timing(result, watch.Elapsed.TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - bytes);
    }

    private static void Equivalent(
        FoundationPhysicalMultimodalRouteComparator.Result left,
        FoundationPhysicalMultimodalRouteComparator.Result right)
    {
        Assert.Equal(left.LandOnlyRateMicro, right.LandOnlyRateMicro);
        Assert.Equal(left.OneSeaLegRateMicro, right.OneSeaLegRateMicro);
        Assert.Equal(left.EmbarkEdge, right.EmbarkEdge);
        Assert.Equal(left.DisembarkEdge, right.DisembarkEdge);
        Assert.Equal(left.SeaRoutePreferred, right.SeaRoutePreferred);
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public void ColdVersusWarmSeaAtlasIsExactAndReportsActualRoi(
        int m, int n)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(m, n)),
            workersPerCivilization: 3);
        var original = host.Snapshot;
        var hash = host.StateHash;
        var seeded = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(
                    1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(
                    5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(
                    25_000UL, 100_000UL),
                10_000UL),
            new FoundationMonetaryConstructionPolicy(
                2_000_000UL, 4_000_000UL, 6_000_000UL, 1_000_000UL));
        var fine = host.Incidence.Refinement.FineParameters;
        var natural = seeded.Transport.Edges.ToDictionary(
            r => r.Edge.FineStrategicEdgeId);
        var elevation = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var land = host.Incidence.TileIncidences.Select(tile =>
        {
            long total = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                total = checked(total +
                    elevation.Elevation.GetRawValue(coarse));
            return total / tile.IncidentCoarseCellIds.Count >
                elevation.SeaLevel.RawValue;
        }).ToArray();
        var roads = new Dictionary<ulong, List<ulong>>();
        var seas = new Dictionary<ulong, List<ulong>>();
        var coasts = new List<(PhysicalTransportEdgeRuntimeRecord Record,
            ulong Land, ulong Sea)>();
        static void Add(Dictionary<ulong, List<ulong>> graph,
            ulong a, ulong b)
        {
            if (!graph.TryGetValue(a, out var first))
                graph[a] = first = new();
            if (!graph.TryGetValue(b, out var second))
                graph[b] = second = new();
            first.Add(b);
            second.Add(a);
        }
        foreach (var crossing in host.Incidence.FineTopology.Edges)
        {
            ulong a = crossing.IncidentCellIds[0].Value;
            ulong b = crossing.IncidentCellIds[1].Value;
            bool la = land[checked((int)a - 1)];
            bool lb = land[checked((int)b - 1)];
            if (la && lb) Add(roads, a, b);
            else if (!la && !lb) Add(seas, a, b);
            else coasts.Add((natural[crossing.Id],
                la ? a : b, la ? b : a));
        }
        // A single flood-fill per network, rather than the repeated
        // nested flood-fills in the original R55 fixture.
        (PhysicalTransportEdgeRuntimeRecord Record, ulong Land, ulong Sea) portA =
            default, portB = default;
        bool found = false;
        foreach (var first in coasts)
        {
            var landReach = Flood(first.Land, roads);
            var seaReach = Flood(first.Sea, seas);
            foreach (var second in coasts)
                if (first.Record.Edge != second.Record.Edge &&
                    first.Land != second.Land && first.Sea != second.Sea &&
                    landReach.Contains(second.Land) &&
                    seaReach.Contains(second.Sea))
                {
                    portA = first;
                    portB = second;
                    found = true;
                    break;
                }
            if (found) break;
        }
        Assert.True(found, "Both alternatives must exist in the real world.");
        var start = new PhysicalTacticalTileId(
            fine, new StrategicCellId(portA.Land));
        var end = new PhysicalTacticalTileId(
            fine, new StrategicCellId(portB.Land));
        var opening = original.DeployInitialPhysicalTransport(
            seeded.Transport, host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (new CivilizationId(1UL), 1_000_000_000UL),
                    (new CivilizationId(2UL), 1_000_000_000UL)
                }));
        var improved = opening.RecordMonetaryInfrastructureInvestment(
            Red, portA.Record!.Edge,
            checked(portA.Record.NaturalConstructionCostMoneyMicro!.Value *
                4UL));
        var cache = new FoundationPhysicalMultimodalRouteComparator
            .FrozenSeaDistanceCache(4_000_000);
        FoundationPhysicalMultimodalRouteComparator.Result Evaluate(
            PhysicalTransportInfrastructureRuntimeState inventory,
            FoundationPhysicalMultimodalRouteComparator
                .FrozenSeaDistanceCache? atlas = null)
            => FoundationPhysicalMultimodalRouteComparator.Compare(
                host.Session.GeneratedWorld, host.Incidence,
                inventory, start, end, maxEligibleEmbarkations: 20_000,
                frozenSeaDistances: atlas);

        // Baseline R56: no cross-call cache, target-aware Dijkstra.
        Timing reference = Measure(() => Evaluate(opening.Transport));
        Timing stateArchitecture = Measure(() =>
            FoundationPhysicalMultimodalRouteComparator.Compare(
                host.Session.GeneratedWorld, host.Incidence,
                opening.Transport, start, end,
                maxEligibleEmbarkations: 20_000,
                architecture: FoundationPhysicalMultimodalRouteComparator
                    .MaritimeResearchArchitecture.EmbarkationState));
        Equivalent(reference.Result, stateArchitecture.Result);
        Timing cacheCold = Measure(() =>
            Evaluate(opening.Transport, cache));
        Timing cacheWarm = Measure(() =>
            Evaluate(opening.Transport, cache));
        Timing changedCapital = Measure(() =>
            Evaluate(improved.Transport, cache));
        Timing independentlyChecked = Measure(() =>
            Evaluate(improved.Transport));
        Equivalent(reference.Result, cacheCold.Result);
        Equivalent(reference.Result, cacheWarm.Result);
        Equivalent(changedCapital.Result, independentlyChecked.Result);
        // A structural candidate-count analysis, NOT a hidden change in
        // freight selection. The oracle is held fixed and is only used as
        // a feasible cost upper bound: no sea distance is computed here.
        var naturalBound = FoundationPhysicalMaritimePairLowerBoundAudit
            .Analyze(host.Session.GeneratedWorld, host.Incidence,
                opening.Transport, start, end, reference.Result);
        var capitalBound = FoundationPhysicalMaritimePairLowerBoundAudit
            .Analyze(host.Session.GeneratedWorld, host.Incidence,
                improved.Transport, start, end,
                independentlyChecked.Result);
        Assert.True(naturalBound.ChosenSeaPairPreserved);
        Assert.True(capitalBound.ChosenSeaPairPreserved);
        Assert.Equal(naturalBound.PotentialPairs,
            naturalBound.ProvablyExcludedPairs +
            naturalBound.RemainingPairs);
        Assert.Equal(capitalBound.PotentialPairs,
            capitalBound.ProvablyExcludedPairs +
            capitalBound.RemainingPairs);
        Assert.Equal(reference.Result.EligibleEmbarkations,
            naturalBound.ReachableEmbarkations);
        Assert.Equal(reference.Result.EligibleDisembarkations,
            naturalBound.ReachableDisembarkations);
        Assert.Equal(independentlyChecked.Result.EligibleEmbarkations,
            capitalBound.ReachableEmbarkations);
        Assert.Equal(independentlyChecked.Result.EligibleDisembarkations,
            capitalBound.ReachableDisembarkations);
        // Compare a second reconstruction to establish deterministic
        // candidate counts without changing the transport inventory.
        Assert.Equal(naturalBound,
            FoundationPhysicalMaritimePairLowerBoundAudit.Analyze(
                host.Session.GeneratedWorld, host.Incidence,
                opening.Transport, start, end, reference.Result));
        _log.WriteLine(
            $"GA_R61_BOUNDS coarse={m},{n} " +
            $"fine_tiles={host.Incidence.FineTopology.Cells.Count} " +
            $"natural_pairs={naturalBound.PotentialPairs} " +
            $"natural_provably_excluded={naturalBound.ProvablyExcludedPairs} " +
            $"natural_remaining={naturalBound.RemainingPairs} " +
            $"natural_excluded_percent={naturalBound.ExcludedPercent:F3} " +
            $"capital_pairs={capitalBound.PotentialPairs} " +
            $"capital_provably_excluded={capitalBound.ProvablyExcludedPairs} " +
            $"capital_remaining={capitalBound.RemainingPairs} " +
            $"capital_excluded_percent={capitalBound.ExcludedPercent:F3} " +
            $"winner_preserved=true " +
            $"bound_source=known_feasible_global_route");
        var stateAfterCapital = Measure(() =>
            FoundationPhysicalMultimodalRouteComparator.Compare(
                host.Session.GeneratedWorld, host.Incidence,
                improved.Transport, start, end,
                maxEligibleEmbarkations: 20_000,
                architecture: FoundationPhysicalMultimodalRouteComparator
                    .MaritimeResearchArchitecture.EmbarkationState));
        Equivalent(independentlyChecked.Result, stateAfterCapital.Result);
        _log.WriteLine(
            $"GA_R60_AB coarse={m},{n} " +
            $"fine_tiles={host.Incidence.FineTopology.Cells.Count} " +
            $"A_supergraph_ms={reference.Milliseconds:F3} " +
            $"B_embarkation_state_ms={stateArchitecture.Milliseconds:F3} " +
            $"A_capital_ms={independentlyChecked.Milliseconds:F3} " +
            $"B_capital_ms={stateAfterCapital.Milliseconds:F3} " +
            $"A_states={reference.Result.ExpandedSeaTiles} " +
            $"B_states={stateArchitecture.Result.ExpandedSeaTiles} " +
            $"A_alloc_bytes={reference.AllocatedBytes} " +
            $"B_alloc_bytes={stateArchitecture.AllocatedBytes} " +
            $"same_two_terminal_ids=true same_optimal_cost=true");
        Assert.Equal(0, cacheWarm.Result.ExploredSeaOrigins);
        Assert.Equal(0, changedCapital.Result.ExploredSeaOrigins);
        Assert.True(cacheCold.Result.ExploredSeaOrigins > 0);
        Assert.True(cache.CachedSeaOrigins > 0);
        Assert.True(cache.DistanceEntries <= 4_000_000L);
        Assert.True(cacheWarm.Result.ExpandedSeaTiles == 0L);
        Assert.Same(original, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        _log.WriteLine(
            $"GA_R58_ROI coarse={m},{n} fine_tiles=" +
            $"{host.Incidence.FineTopology.Cells.Count} " +
            $"physical_edges={seeded.Transport.Edges.Count} " +
            $"coast_edges={coasts.Count} " +
            $"natural_rate={reference.Result.OneSeaLegRateMicro} " +
            $"cold_ms={reference.Milliseconds:F3} " +
            $"cache_fill_ms={cacheCold.Milliseconds:F3} " +
            $"warm_ms={cacheWarm.Milliseconds:F3} " +
            $"capital_warm_ms={changedCapital.Milliseconds:F3} " +
            $"capital_no_cache_ms={independentlyChecked.Milliseconds:F3} " +
            $"cold_alloc_bytes={reference.AllocatedBytes} " +
            $"cache_fill_alloc_bytes={cacheCold.AllocatedBytes} " +
            $"warm_alloc_bytes={cacheWarm.AllocatedBytes} " +
            $"cache_distances={cache.DistanceEntries} " +
            $"cached_sources={cache.CachedSeaOrigins} " +
            $"cache_hits={cache.Hits} " +
            $"exact_paths=true");
    }
}
