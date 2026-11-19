using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;
using Xunit.Abstractions;

namespace GlobalArena.Tests;

/// <summary>
/// Two ACTUAL land settlements and competing real terrestrial/maritime
/// paths; no imaginary ferry/port edges, or cost injected into live games.
/// </summary>
public sealed class FoundationPhysicalMultimodalRouteComparatorTests
{
    private static readonly CivilizationId Red = new(1UL);
    private static readonly CivilizationId Blue = new(2UL);
    private readonly ITestOutputHelper _output;

    public FoundationPhysicalMultimodalRouteComparatorTests(
        ITestOutputHelper output) => _output = output;

    [Fact]
    public void TwoLandSettlementsCompareRealNaturalRoadAndInvestedSeaOption()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var originalHash = host.StateHash;
        var originalSnapshot = host.Snapshot;
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
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
        var all = seed.Transport.Edges.ToDictionary(x =>
            x.Edge.FineStrategicEdgeId);
        var physical = host.Session.GeneratedWorld.StrategicPhysicalFields;
        var land = host.Incidence.TileIncidences.Select(tile =>
        {
            long sum = tile.IncidentCoarseCellIds.Aggregate(0L,
                (total, cell) => checked(total +
                    physical.Elevation.GetRawValue(cell)));
            return sum / tile.IncidentCoarseCellIds.Count >
                physical.SeaLevel.RawValue;
        }).ToArray();

        var landGraph = new Dictionary<ulong, List<ulong>>();
        var seaGraph = new Dictionary<ulong, List<ulong>>();
        var coast = new List<(
            PhysicalTransportEdgeRuntimeRecord Crossing,
            ulong Land, ulong Sea)>();
        static void Add(Dictionary<ulong, List<ulong>> graph,
            ulong a, ulong b)
        {
            if (!graph.TryGetValue(a, out var aa))
                graph[a] = aa = new();
            if (!graph.TryGetValue(b, out var bb))
                graph[b] = bb = new();
            aa.Add(b);
            bb.Add(a);
        }
        foreach (var edge in host.Incidence.FineTopology.Edges)
        {
            ulong a = edge.IncidentCellIds[0].Value;
            ulong b = edge.IncidentCellIds[1].Value;
            bool la = land[checked((int)a - 1)];
            bool lb = land[checked((int)b - 1)];
            if (la && lb) Add(landGraph, a, b);
            else if (!la && !lb) Add(seaGraph, a, b);
            else coast.Add((all[edge.Id], la ? a : b, la ? b : a));
        }
        static HashSet<ulong> Reachable(
            ulong origin, Dictionary<ulong, List<ulong>> graph)
        {
            var seen = new HashSet<ulong> { origin };
            var todo = new Queue<ulong>();
            todo.Enqueue(origin);
            while (todo.TryDequeue(out ulong at))
                if (graph.TryGetValue(at, out var neighbors))
                    foreach (var next in neighbors)
                        if (seen.Add(next)) todo.Enqueue(next);
            return seen;
        }

        // Find two DIFFERENT land settlements with both real routes.
        // Prefer ports on neighboring sea tiles for a tiny sea alternative,
        // but insist that a terrestrial path exists too.
        var candidate = (
            from a in coast
            let reachableLand = Reachable(a.Land, landGraph)
            let reachableSea = Reachable(a.Sea, seaGraph)
            from b in coast
            where a.Crossing.Edge != b.Crossing.Edge &&
                a.Land != b.Land &&
                reachableLand.Contains(b.Land) &&
                reachableSea.Contains(b.Sea) &&
                a.Sea != b.Sea
            orderby a.Crossing.Edge.FineStrategicEdgeId.Value,
                b.Crossing.Edge.FineStrategicEdgeId.Value
            select (A: a, B: b)).First();

        var fine = host.Incidence.Refinement.FineParameters;
        var source = new PhysicalTacticalTileId(
            fine, new StrategicCellId(candidate.A.Land));
        var destination = new PhysicalTacticalTileId(
            fine, new StrategicCellId(candidate.B.Land));
        var snapshot = originalSnapshot
            .DeployInitialPhysicalTransport(seed.Transport,
                host.Session.GeneratedWorld, host.Incidence)
            .DeployInitialTreasury(
                CivilizationTreasuryRuntimeState.CreateOpening(new[]
                {
                    (Red, 1_000_000_000_000UL),
                    (Blue, 1_000_000_000_000UL)
                }));
        var landEdge = seed.Transport.Edges.First(x =>
            x.CargoMicroAtReferenceLoad != ulong.MaxValue &&
            x.CargoMicroAtReferenceLoad == 1_000_000UL &&
            host.Incidence.FineTopology.Edges.First(e =>
                e.Id == x.Edge.FineStrategicEdgeId)
                .IncidentCellIds.Any(t =>
                    t.Value == candidate.A.Land));
        var baseLandRate = FoundationPhysicalMultimodalRouteComparator
            .OperationalCost(landEdge);
        var landBuilt = landEdge with
        {
            CommittedInvestmentMoneyMicro = checked(
                landEdge.NaturalConstructionCostMoneyMicro!.Value * 4UL),
            OwnershipKind = PhysicalTransportOwnershipKind.Owned,
            InfrastructureOwner = Red
        };
        Assert.True(FoundationPhysicalMultimodalRouteComparator
            .OperationalCost(landBuilt) < baseLandRate);
        Assert.Equal(landEdge.FreePhysicalCostMicroPerWholeCargoUnit,
            baseLandRate);

        // The frozen sea graph must be identical across snapshots with
        // DIFFERENT land/coast capital. Caller-owned bounded cache is an
        // amortization mechanism, not a new input to market decisions.
        var frozenSea =
            new FoundationPhysicalMultimodalRouteComparator
                .FrozenSeaDistanceCache();
        var natural = FoundationPhysicalMultimodalRouteComparator.Compare(
            host.Session.GeneratedWorld, host.Incidence,
            snapshot.Transport, source, destination,
            maxEligibleEmbarkations: 4096,
            frozenSeaDistances: frozenSea);
        Assert.True(natural.ExploredSeaOrigins > 0);
        Assert.Equal(natural.ExploredSeaOrigins, frozenSea.CachedSeaOrigins);
        Assert.NotNull(natural.LandOnlyRateMicro);
        Assert.NotNull(natural.OneSeaLegRateMicro);
        Assert.True(natural.ExploredSeaOrigins > 0);
        Assert.NotNull(natural.EmbarkEdge);
        Assert.NotNull(natural.DisembarkEdge);
        // Compare the exact R55 baseline algorithm with the R56 cached,
        // smaller-side, early-stopping Dijkstra. Both must report the SAME
        // optimal cost and the SAME actual terminal pair, not just a route
        // with a similar final number.
        var baseline = FoundationPhysicalMultimodalRouteComparator.Compare(
            host.Session.GeneratedWorld, host.Incidence,
            snapshot.Transport, source, destination,
            maxEligibleEmbarkations: 4096,
            useOptimizedSeaSearch: false);
        Assert.Equal(baseline.LandOnlyRateMicro,
            natural.LandOnlyRateMicro);
        Assert.Equal(baseline.OneSeaLegRateMicro,
            natural.OneSeaLegRateMicro);
        Assert.Equal(baseline.EmbarkEdge, natural.EmbarkEdge);
        Assert.Equal(baseline.DisembarkEdge, natural.DisembarkEdge);
        Assert.True(natural.ExploredSeaOrigins <=
            baseline.ExploredSeaOrigins);
        Assert.True(natural.ExpandedSeaTiles <=
            baseline.ExpandedSeaTiles);
        Assert.True(natural.ExploredSeaOrigins > 0);
        _output.WriteLine(
            $"GA_R56_AB profile=natural " +
            $"baseline_dijkstra={baseline.ExploredSeaOrigins} " +
            $"optimized_dijkstra={natural.ExploredSeaOrigins} " +
            $"baseline_sea_settled_tiles={baseline.ExpandedSeaTiles} " +
            $"optimized_sea_settled_tiles={natural.ExpandedSeaTiles} " +
            $"optimal_cost={natural.OneSeaLegRateMicro} " +
            $"identical_terminal_pair=true");

        var coastA = candidate.A.Crossing;
        var coastB = candidate.B.Crossing;
        ulong costA = checked(
            coastA.NaturalConstructionCostMoneyMicro!.Value * 4UL);
        ulong costB = checked(
            coastB.NaturalConstructionCostMoneyMicro!.Value * 4UL);
        var fundedCoast = snapshot.RecordMonetaryInfrastructureInvestment(
            Red, coastA.Edge, costA);
        fundedCoast = fundedCoast.RecordMonetaryInfrastructureInvestment(
            Blue, coastB.Edge, costB);
        var coastRoute = FoundationPhysicalMultimodalRouteComparator.Compare(
            host.Session.GeneratedWorld, host.Incidence,
            fundedCoast.Transport, source, destination,
            maxEligibleEmbarkations: 4096,
            frozenSeaDistances: frozenSea);
        Assert.Equal(0, coastRoute.ExploredSeaOrigins);
        Assert.Equal(0L, coastRoute.ExpandedSeaTiles);
        var coastWithoutCache =
            FoundationPhysicalMultimodalRouteComparator.Compare(
                host.Session.GeneratedWorld, host.Incidence,
                fundedCoast.Transport, source, destination,
                maxEligibleEmbarkations: 4096);
        Assert.Equal(coastWithoutCache.OneSeaLegRateMicro,
            coastRoute.OneSeaLegRateMicro);
        Assert.Equal(coastWithoutCache.EmbarkEdge, coastRoute.EmbarkEdge);
        Assert.Equal(coastWithoutCache.DisembarkEdge, coastRoute.DisembarkEdge);
        Assert.Equal(natural.LandOnlyRateMicro,
            coastRoute.LandOnlyRateMicro);
        Assert.True(coastRoute.OneSeaLegRateMicro <=
            natural.OneSeaLegRateMicro);
        var fundedLand = snapshot.RecordMonetaryInfrastructureInvestment(
            Red, landEdge.Edge,
            checked(landEdge.NaturalConstructionCostMoneyMicro!.Value * 4UL));
        var landRoute = FoundationPhysicalMultimodalRouteComparator.Compare(
            host.Session.GeneratedWorld, host.Incidence,
            fundedLand.Transport, source, destination,
            maxEligibleEmbarkations: 4096,
            frozenSeaDistances: frozenSea);
        Assert.Equal(0, landRoute.ExploredSeaOrigins);
        Assert.True(landRoute.LandOnlyRateMicro <=
            natural.LandOnlyRateMicro);
        var both = fundedCoast.RecordMonetaryInfrastructureInvestment(
            Red, landEdge.Edge,
            checked(landEdge.NaturalConstructionCostMoneyMicro!.Value * 4UL));
        var bothRoute = FoundationPhysicalMultimodalRouteComparator.Compare(
            host.Session.GeneratedWorld, host.Incidence, both.Transport,
            source, destination, maxEligibleEmbarkations: 4096,
            frozenSeaDistances: frozenSea);
        Assert.True(bothRoute.OneSeaLegRateMicro <=
            coastRoute.OneSeaLegRateMicro);
        Assert.True(bothRoute.LandOnlyRateMicro <=
            landRoute.LandOnlyRateMicro);

        // STRESS QA: invest in EVERY real coast/land crossing, not just
        // the two hand-picked terminals. This exposes whether shared
        // physical infrastructure changes the preferred route at all.
        var improvedCoasts = snapshot;
        var improvedLand = snapshot;
        int portCount = 0, landCount = 0;
        foreach (var record in seed.Transport.Edges)
        {
            if (record.CargoMicroAtReferenceLoad == 500_000UL)
            {
                improvedCoasts =
                    improvedCoasts.RecordMonetaryInfrastructureInvestment(
                        Red, record.Edge,
                        checked(record.NaturalConstructionCostMoneyMicro!.Value
                            * 4UL));
                portCount++;
            }
            else if (record.CargoMicroAtReferenceLoad == 1_000_000UL)
            {
                improvedLand = improvedLand.RecordMonetaryInfrastructureInvestment(
                    Red, record.Edge,
                    checked(record.NaturalConstructionCostMoneyMicro!.Value
                        * 4UL));
                landCount++;
            }
        }
        Assert.True(portCount > 0 && landCount > 0);
        var allPortRoutes = FoundationPhysicalMultimodalRouteComparator
            .Compare(host.Session.GeneratedWorld, host.Incidence,
                improvedCoasts.Transport, source, destination,
                maxEligibleEmbarkations: 4096,
                frozenSeaDistances: frozenSea);
        Assert.Equal(0, allPortRoutes.ExploredSeaOrigins);
        var allLandRoutes = FoundationPhysicalMultimodalRouteComparator
            .Compare(host.Session.GeneratedWorld, host.Incidence,
                improvedLand.Transport, source, destination,
                maxEligibleEmbarkations: 4096,
                frozenSeaDistances: frozenSea);
        Assert.Equal(0, allLandRoutes.ExploredSeaOrigins);
        Assert.Equal(natural.LandOnlyRateMicro,
            allPortRoutes.LandOnlyRateMicro);
        Assert.True(allPortRoutes.OneSeaLegRateMicro <
            natural.OneSeaLegRateMicro);
        Assert.True(allLandRoutes.LandOnlyRateMicro <
            natural.LandOnlyRateMicro);
        // Much stronger terminal development as a sensitivity test; this
        // is NOT a historical coefficient or a proposed gameplay default.
        var intensiveCoasts = snapshot;
        foreach (var record in seed.Transport.Edges.Where(x =>
            x.CargoMicroAtReferenceLoad == 500_000UL))
            intensiveCoasts =
                intensiveCoasts.RecordMonetaryInfrastructureInvestment(
                    Red, record.Edge,
                    checked(record.NaturalConstructionCostMoneyMicro!.Value
                        * 256UL));
        var intensiveSea = FoundationPhysicalMultimodalRouteComparator
            .Compare(host.Session.GeneratedWorld, host.Incidence,
                intensiveCoasts.Transport, source, destination,
                maxEligibleEmbarkations: 4096,
                frozenSeaDistances: frozenSea);
        Assert.Equal(0, intensiveSea.ExploredSeaOrigins);
        Assert.True(intensiveSea.OneSeaLegRateMicro <=
            allPortRoutes.OneSeaLegRateMicro);
        Assert.Equal(natural.LandOnlyRateMicro,
            intensiveSea.LandOnlyRateMicro);
        Assert.True(intensiveSea.SeaRoutePreferred,
            "The research curve must eventually make developed coastal " +
            "logistics competitive without any sea-sea investment.");
        // The R61 lower bound MUST also preserve the chosen maritime
        // route when SEA really beats land. This complements the R58
        // cases, where the losing sea alternative may be safely pruned.
        var maritimeWinnerBound =
            FoundationPhysicalMaritimePairLowerBoundAudit.Analyze(
                host.Session.GeneratedWorld, host.Incidence,
                intensiveCoasts.Transport, source, destination,
                intensiveSea);
        Assert.True(maritimeWinnerBound.GlobalWinnerPreserved);
        Assert.Equal(
            maritimeWinnerBound.PotentialPairs,
            maritimeWinnerBound.ProvablyExcludedPairs +
                maritimeWinnerBound.RemainingPairs);
        _output.WriteLine(
            $"GA_R61_SEA_WINNER potential_pairs=" +
            $"{maritimeWinnerBound.PotentialPairs} " +
            $"excluded_pairs=" +
            $"{maritimeWinnerBound.ProvablyExcludedPairs} " +
            $"remaining_pairs={maritimeWinnerBound.RemainingPairs} " +
            $"global_maritime_winner_preserved=true");
        var intensiveBaseline =
            FoundationPhysicalMultimodalRouteComparator.Compare(
                host.Session.GeneratedWorld, host.Incidence,
                intensiveCoasts.Transport, source, destination,
                maxEligibleEmbarkations: 4096,
                useOptimizedSeaSearch: false);
        Assert.Equal(intensiveBaseline.LandOnlyRateMicro,
            intensiveSea.LandOnlyRateMicro);
        Assert.Equal(intensiveBaseline.OneSeaLegRateMicro,
            intensiveSea.OneSeaLegRateMicro);
        Assert.Equal(intensiveBaseline.EmbarkEdge,
            intensiveSea.EmbarkEdge);
        Assert.Equal(intensiveBaseline.DisembarkEdge,
            intensiveSea.DisembarkEdge);
        Assert.True(intensiveSea.ExploredSeaOrigins <=
            intensiveBaseline.ExploredSeaOrigins);
        Assert.True(intensiveSea.ExpandedSeaTiles <=
            intensiveBaseline.ExpandedSeaTiles);
        _output.WriteLine(
            $"GA_R56_AB profile=intensive_coast " +
            $"baseline_dijkstra={intensiveBaseline.ExploredSeaOrigins} " +
            $"optimized_dijkstra={intensiveSea.ExploredSeaOrigins} " +
            $"baseline_sea_settled_tiles={intensiveBaseline.ExpandedSeaTiles} " +
            $"optimized_sea_settled_tiles={intensiveSea.ExpandedSeaTiles} " +
            $"optimal_cost={intensiveSea.OneSeaLegRateMicro} " +
            $"identical_terminal_pair=true");
        _output.WriteLine(
            $"GA_R55_NETWORK all_ports={portCount} all_land_edges={landCount} " +
            $"all_ports_sea={allPortRoutes.OneSeaLegRateMicro} " +
            $"all_land_terrestrial={allLandRoutes.LandOnlyRateMicro} " +
            $"intensive_ports_sea={intensiveSea.OneSeaLegRateMicro} " +
            $"intensive_ports_preferred={intensiveSea.SeaRoutePreferred} " +
            $"modeled_legs=one exploration_upper_bound=4096");

        // Quantify return on complexity: the expensive sea component is
        // calculated only on the first query, then reused across all the
        // differing CAPEX snapshots in this one scenario.
        Assert.True(frozenSea.CachedSeaOrigins > 0);
        Assert.True(frozenSea.DistanceEntries <= 4_000_000L);
        Assert.True(frozenSea.Hits >= frozenSea.CachedSeaOrigins * 5L);
        _output.WriteLine(
            $"GA_R57_CACHE first_dijkstra={natural.ExploredSeaOrigins} " +
            $"subsequent_dijkstra={coastRoute.ExploredSeaOrigins} " +
            $"cached_origins={frozenSea.CachedSeaOrigins} " +
            $"cached_distance_entries={frozenSea.DistanceEntries} " +
            $"cache_hits={frozenSea.Hits} " +
            $"cache_misses={frozenSea.Misses} " +
            $"coast_same_optimum=true" +
            " repeatable_atlas_scope=caller_owned");

        // No sea edge can receive investments, title or any expense.
        Assert.All(both.Transport.Edges.Where(x =>
            x.CargoMicroAtReferenceLoad == ulong.MaxValue), x =>
        {
            Assert.Equal(PhysicalTransportOwnershipKind.NoInfrastructure,
                x.OwnershipKind);
            Assert.Null(x.InfrastructureOwner);
            Assert.Equal(0UL,
                x.CommittedInvestmentMoneyMicro.GetValueOrDefault());
        });
        Assert.Same(originalSnapshot, host.Snapshot);
        Assert.Equal(originalHash, host.StateHash);
        _output.WriteLine(
            $"GA_R55 landA={candidate.A.Land} landB={candidate.B.Land} " +
            $"natural_land={natural.LandOnlyRateMicro} " +
            $"natural_sea={natural.OneSeaLegRateMicro} " +
            $"coast_invested_land={coastRoute.LandOnlyRateMicro} " +
            $"coast_invested_sea={coastRoute.OneSeaLegRateMicro} " +
            $"land_invested_land={landRoute.LandOnlyRateMicro} " +
            $"land_invested_sea={landRoute.OneSeaLegRateMicro} " +
            $"both_land={bothRoute.LandOnlyRateMicro} " +
            $"both_sea={bothRoute.OneSeaLegRateMicro} " +
            $"sea_wins_after_both={bothRoute.SeaRoutePreferred} " +
            $"searched_ports={natural.ExploredSeaOrigins} " +
            $"no_dynamic_feedback=true");
    }

    [Fact]
    public void PortDiscoveryNeverSilentlyTruncatesItsResearchBudget()
    {
        var host = FoundationConcentratedWorkforceScenario.CreatePreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)),
            workersPerCivilization: 3);
        var seed = FoundationPhysicalNaturalTransportSeeder.Create(
            host.Session.GeneratedWorld, host.Incidence,
            new FoundationNaturalTransportSeedingPolicy(
                new FoundationNaturalTransportCostBand(
                    1_000UL, 1_000_000UL),
                new FoundationNaturalTransportCostBand(
                    5_000UL, 500_000UL),
                new FoundationNaturalTransportCostBand(
                    25_000UL, 100_000UL),
                10_000UL));
        // No physical node can be missing or fake; for this sanity case,
        // use the known land tile of a real coastal edge.
        var coast = seed.Transport.Edges.First(x =>
            x.CargoMicroAtReferenceLoad == 500_000UL);
        var top = host.Incidence.FineTopology.Edges.Single(x =>
            x.Id == coast.Edge.FineStrategicEdgeId);
        var elevation = host.Session.GeneratedWorld.StrategicPhysicalFields;
        ulong landTile = top.IncidentCellIds.Select(t => t.Value)
            .Single(id =>
            {
                var incident = host.Incidence.TileIncidences[
                    checked((int)id - 1)].IncidentCoarseCellIds;
                long value = incident.Aggregate(0L,
                    (sum, c) => checked(sum +
                        elevation.Elevation.GetRawValue(c))) /
                    incident.Count;
                return value > elevation.SeaLevel.RawValue;
            });
        var fine = host.Incidence.Refinement.FineParameters;
        var landId = new PhysicalTacticalTileId(
            fine, new StrategicCellId(landTile));
        Assert.Throws<ArgumentException>(() =>
            FoundationPhysicalMultimodalRouteComparator.Compare(
                host.Session.GeneratedWorld, host.Incidence,
                seed.Transport, landId, landId, 1));
    }
}
