using System.Diagnostics;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// R64: measure the COMPLETE cost of repeated independent market-pair
/// comparisons, not a Dijkstra counter. Same world/capital and same
/// explicitly caller-owned R57 sea atlas for both R62 modes.
/// The bound's cheap pair scan is included in measured wall-clock time.
/// The oracle's losing maritime quote need not be preserved, but
/// the globally optimal modality and both actual terminals must be.
/// No live production orders or market settlement are modified.
/// </summary>
public sealed class FoundationPhysicalLargeWorldMultimarketGateTests
{
    private readonly ITestOutputHelper _output;
    private static readonly CivilizationId Red = new(1UL);

    public FoundationPhysicalLargeWorldMultimarketGateTests(
        ITestOutputHelper output) => _output = output;

    private sealed record Metric(
        FoundationPhysicalMultimodalRouteComparator.Result Route,
        double Milliseconds, long AllocatedBytes);

    private static Metric Time(
        Func<FoundationPhysicalMultimodalRouteComparator.Result> run)
    {
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        var watch = Stopwatch.StartNew();
        var route = run();
        watch.Stop();
        return new Metric(route, watch.Elapsed.TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - bytes);
    }

    private static void SameGlobalWinner(
        FoundationPhysicalMultimodalRouteComparator.Result oracle,
        FoundationPhysicalMultimodalRouteComparator.Result bounded)
    {
        Assert.Equal(oracle.LandOnlyRateMicro, bounded.LandOnlyRateMicro);
        Assert.Equal(oracle.SeaRoutePreferred, bounded.SeaRoutePreferred);
        if (oracle.SeaRoutePreferred)
        {
            Assert.Equal(oracle.OneSeaLegRateMicro,
                bounded.OneSeaLegRateMicro);
            Assert.Equal(oracle.EmbarkEdge, bounded.EmbarkEdge);
            Assert.Equal(oracle.DisembarkEdge, bounded.DisembarkEdge);
        }
        else if (!oracle.LandOnlyRateMicro.HasValue)
        {
            // If land is disconnected, no terrestrial upper bound exists.
            // The pruning branch must not claim to reject the sea route.
            Assert.Equal(oracle.OneSeaLegRateMicro,
                bounded.OneSeaLegRateMicro);
        }
    }

    [Theory]
    [InlineData(4, 0)]
    public void LargerPhysicalWorldKeepsExactMarketDecisionsAndReportsMarginalRoi(
        int m, int n)
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(m, n)),
            workersPerCivilization: 3);
        var originalHash = host.StateHash;
        var originalSnapshot = host.Snapshot;
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
        var fields = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var land = host.Incidence.TileIncidences.Select(tile =>
        {
            long total = 0L;
            foreach (var coarse in tile.IncidentCoarseCellIds)
                total = checked(total +
                    fields.Elevation.GetRawValue(coarse));
            return total / tile.IncidentCoarseCellIds.Count >
                fields.SeaLevel.RawValue;
        }).ToArray();

        // No fabricated port objects: identify real land endpoints of
        // canonical, finite coast-crossing edges and use a deterministic
        // spatially distributed selection of distinct market pairs.
        var coastRecords = seeded.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        var marketTiles = new SortedSet<ulong>();
        foreach (var edge in host.Incidence.FineTopology.Edges)
        {
            var record = coastRecords[edge.Id];
            if (record.CargoMicroAtReferenceLoad != 500_000UL)
                continue;
            ulong landTile = edge.IncidentCellIds
                .Select(t => t.Value)
                .Single(id => land[checked((int)id - 1)]);
            marketTiles.Add(landTile);
        }
        ulong[] markets = marketTiles.ToArray();
        Assert.True(markets.Length > 20);

        var selected = new List<(ulong From, ulong To)>();
        // On this actual 3,0 QA world, the R55 reference established
        // two real land settlements (16, 27) with a sea-capable route.
        // This guards the scenario where developing the coasts wins.
        for (int k = 0; k < 4; k++)
        {
            ulong from = markets[checked(k * (markets.Length - 1) / 11)];
            ulong to = markets[checked((k + 6) * (markets.Length - 1) / 11)];
            if (from != to && !selected.Contains((from, to)))
                selected.Add((from, to));
        }
        Assert.True(selected.Count >= 4);

        var naturalWorld = originalSnapshot.DeployInitialPhysicalTransport(
                seeded.Transport, host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (Red, 10_000_000_000_000UL),
                    (new CivilizationId(2UL), 10_000_000_000_000UL)
                }));
        // Investments are REAL monetary events, cash-debited by
        // WorldState, not a synthetic replacement of physics. The
        // 256x coast profile is a QA sensitivity stress, not game balance.
        var richCoasts = naturalWorld;
        foreach (var edge in seeded.Transport.Edges)
        {
            if (edge.CargoMicroAtReferenceLoad != 500_000UL)
                continue;
            richCoasts = richCoasts.RecordMonetaryInfrastructureInvestment(
                Red, edge.Edge,
                checked(edge.NaturalConstructionCostMoneyMicro!.Value *
                    256UL));
        }
        var fine = host.Incidence.Refinement.FineParameters;
        var states = new[]
        {
            (Label: "natural", Inventory: naturalWorld.Transport),
            (Label: "coast256", Inventory: richCoasts.Transport)
        };
        var cache = new FoundationPhysicalMultimodalRouteComparator
            .FrozenSeaDistanceCache(4_000_000);
        var unboundedPhases = new List<
            FoundationPhysicalMultimodalRouteComparator.ResearchPhaseTelemetry>();
        var boundedPhases = new List<
            FoundationPhysicalMultimodalRouteComparator.ResearchPhaseTelemetry>();
        FoundationPhysicalMultimodalRouteComparator.Result Compare(
            PhysicalTransportInfrastructureRuntimeState transport,
            ulong from, ulong to, bool prune)
            => FoundationPhysicalMultimodalRouteComparator.Compare(
                host.Session.GeneratedWorld, host.Incidence,
                transport,
                new PhysicalTacticalTileId(fine, new StrategicCellId(from)),
                new PhysicalTacticalTileId(fine, new StrategicCellId(to)),
                maxEligibleEmbarkations: 20_000,
                frozenSeaDistances: cache,
                useProvenLandBound: prune,
                reportPhases: report => (prune ? boundedPhases :
                    unboundedPhases).Add(report));

        // Warm BOTH architectures with the same caller-owned cache and
        // one real market pair, then measure identical per-market calls.
        // This prevents attributing an ordinary atlas miss to the bound.
        Compare(states[0].Inventory, selected[0].From,
            selected[0].To, prune: false);
        unboundedPhases.Clear();
        boundedPhases.Clear();

        double originalMs = 0.0, boundedMs = 0.0;
        long originalAlloc = 0L, boundedAlloc = 0L;
        long totalPairs = 0L, removedPairs = 0L;
        long originalSearches = 0L, boundedSearches = 0L;
        int seaWinners = 0, landWinners = 0, queryCount = 0;
        foreach (var (label, inventory) in states)
        {
            double profileOriginalMs = 0, profileBoundedMs = 0;
            int profileSeaWins = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                var (from, to) = selected[i];
                // Alternate execution order to reduce systematic
                // first-run bias, while keeping the same physical state.
                Metric oracle, bounded;
                if ((i & 1) == 0)
                {
                    oracle = Time(() => Compare(inventory, from, to, false));
                    bounded = Time(() => Compare(inventory, from, to, true));
                }
                else
                {
                    bounded = Time(() => Compare(inventory, from, to, true));
                    oracle = Time(() => Compare(inventory, from, to, false));
                }
                SameGlobalWinner(oracle.Route, bounded.Route);
                Assert.True(bounded.Route.PotentialMaritimePairs >=
                    bounded.Route.ProvenExcludedMaritimePairs);
                originalMs += oracle.Milliseconds;
                boundedMs += bounded.Milliseconds;
                profileOriginalMs += oracle.Milliseconds;
                profileBoundedMs += bounded.Milliseconds;
                originalAlloc = checked(originalAlloc + oracle.AllocatedBytes);
                boundedAlloc = checked(boundedAlloc + bounded.AllocatedBytes);
                totalPairs = checked(totalPairs +
                    bounded.Route.PotentialMaritimePairs);
                removedPairs = checked(removedPairs +
                    bounded.Route.ProvenExcludedMaritimePairs);
                originalSearches = checked(originalSearches +
                    oracle.Route.ExploredSeaOrigins);
                boundedSearches = checked(boundedSearches +
                    bounded.Route.ExploredSeaOrigins);
                if (oracle.Route.SeaRoutePreferred)
                {
                    seaWinners++;
                    profileSeaWins++;
                }
                else landWinners++;
                queryCount++;
            }
            _output.WriteLine(
                $"GA_R64_PROFILE coarse={m},{n} profile={label} " +
                $"queries={selected.Count} sea_winners={profileSeaWins} " +
                $"unbounded_ms={profileOriginalMs:F3} " +
                $"bounded_ms={profileBoundedMs:F3}");
        }
        Assert.Equal(queryCount, unboundedPhases.Count);
        Assert.Equal(queryCount, boundedPhases.Count);
        // Diagnostic reports are observational: phase clocks and counters
        // cannot influence transport or state. They identify whether
        // another routing micro-optimization would have any ROI.
        void LogPhases(string mode, IReadOnlyList<
            FoundationPhysicalMultimodalRouteComparator.ResearchPhaseTelemetry> phases)
        {
            _output.WriteLine(
                $"GA_R65_PHASES coarse={m},{n} mode={mode} " +
                $"queries={phases.Count} " +
                $"preparation_ms={phases.Sum(p => p.PreparationMs):F3} " +
                $"land_search_ms={phases.Sum(p => p.LandSearchMs):F3} " +
                $"bound_ms={phases.Sum(p => p.BoundMs):F3} " +
                $"sea_search_ms={phases.Sum(p => p.SeaSearchMs):F3} " +
                $"pair_scoring_ms={phases.Sum(p => p.PairEvaluationMs):F3} " +
                $"pair_slots={phases.Sum(p => p.EvaluatedPairSlots)} " +
                $"sea_quotes={phases.Sum(p => p.SeaQuotes)} " +
                $"potential_pairs={phases.Sum(p => p.PotentialPairs)} " +
                $"proven_excluded={phases.Sum(p => p.ProvenExcludedPairs)}");
        }
        LogPhases("oracle", unboundedPhases);
        LogPhases("proven_bound", boundedPhases);
        Assert.True(unboundedPhases.Sum(p => p.EvaluatedPairSlots) >=
            boundedPhases.Sum(p => p.EvaluatedPairSlots));
        Assert.True(unboundedPhases.Sum(p => p.SeaQuotes) >=
            boundedPhases.Sum(p => p.SeaQuotes));
        Assert.True(totalPairs > 0L);
        Assert.True(removedPairs > 0L);
        Assert.Equal(queryCount, seaWinners + landWinners);
        Assert.Same(originalSnapshot, host.Snapshot);
        Assert.Equal(originalHash, host.StateHash);
        _output.WriteLine(
            $"GA_R64_MARKETS coarse={m},{n} " +
            $"fine_tiles={host.Incidence.FineTopology.Cells.Count} " +
            $"market_pairs={selected.Count} snapshots={states.Length} " +
            $"queries={queryCount} maritime_winners={seaWinners} " +
            $"land_winners={landWinners} " +
            $"potential_port_pairs={totalPairs} " +
            $"provably_excluded_pairs={removedPairs} " +
            $"excluded_percent={100.0 * removedPairs / totalPairs:F3} " +
            $"unbounded_total_ms={originalMs:F3} " +
            $"bounded_total_ms={boundedMs:F3} " +
            $"unbounded_alloc_bytes={originalAlloc} " +
            $"bounded_alloc_bytes={boundedAlloc} " +
            $"unbounded_sea_searches={originalSearches} " +
            $"bounded_sea_searches={boundedSearches} " +
            $"cache_entries={cache.DistanceEntries} " +
            $"same_global_decisions=true " +
            $"sea_winner_exact_terminal_ids=true");
    }
}
