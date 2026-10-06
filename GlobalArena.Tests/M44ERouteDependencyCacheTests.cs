using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M44ERouteDependencyCacheTests
{
    [Fact]
    public void ResolutionRejectsReachableWithoutRoute()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicEconomicRouteResolution(
                true,
                null,
                Array.Empty<StrategicEdgeId>()));
    }

    [Fact]
    public void ResolutionCanonicalizesSearchDependencies()
    {
        var resolution =
            new StrategicEconomicRouteResolution(
                true,
                LocalRoute(),
                new[]
                {
                    new StrategicEdgeId(3UL),
                    new StrategicEdgeId(1UL),
                    new StrategicEdgeId(2UL)
                });

        Assert.Equal(
            new ulong[] { 1UL, 2UL, 3UL },
            resolution.SearchDependencyEdgeIds.Select(edgeId => edgeId.Value));
    }

    [Fact]
    public void DetailedSamePointHasNoSearchDependencies()
    {
        var world = CreateWorld();
        var cell = world.StrategicTopology.Cells[0].Id;
        var economy = Economy(Point(1UL, cell));

        var result =
            Resolver(world)
                .ResolveDetailed(
                    economy,
                    Key(1UL, 1UL),
                    StrategicEdgeAccessSnapshot.AllOpen);

        Assert.True(result.IsReachable);
        Assert.Empty(result.SearchDependencyEdgeIds);
        Assert.Empty(result.Route!.DependencyEdgeIds);
    }

    [Fact]
    public void ClosedDirectEdgeIsSearchDependencyButNotTraversalDependency()
    {
        var s = ClosedDirectScenario();

        var result =
            s.Resolver.ResolveDetailed(
                s.Economy,
                s.Key,
                s.Access);

        Assert.True(result.IsReachable);
        Assert.Contains(s.DirectEdge, result.SearchDependencyEdgeIds);
        Assert.DoesNotContain(s.DirectEdge, result.Route!.DependencyEdgeIds);
        Assert.True(result.Route.HopCount > 1);
    }

    [Fact]
    public void UnreachableResolutionCapturesClosedSourceFrontier()
    {
        var world = CreateWorld();
        var source = world.StrategicTopology.Cells[0];
        var destination = Cell(world, source.AdjacentCellIds[0]);
        var access = new StrategicEdgeAccessSnapshot(source.IncidentEdgeIds);

        var result =
            ResolveDetailed(
                world,
                source.Id,
                destination.Id,
                access);

        Assert.False(result.IsReachable);
        Assert.Null(result.Route);

        Assert.Equal(
            source.IncidentEdgeIds.OrderBy(edgeId => edgeId.Value),
            result.SearchDependencyEdgeIds);
    }

    [Fact]
    public void TryResolveRemainsBehaviorallyCompatible()
    {
        var s = OpenAdjacentScenario();

        var detailed =
            s.Resolver.ResolveDetailed(
                s.Economy,
                s.Key,
                StrategicEdgeAccessSnapshot.AllOpen);

        var resolved =
            s.Resolver.TryResolve(
                s.Economy,
                s.Key,
                StrategicEdgeAccessSnapshot.AllOpen,
                out var route);

        Assert.True(resolved);
        AssertRouteEqual(detailed.Route!, route!);
    }

    [Fact]
    public void CacheRejectsOutOfGraphAccessAtConstruction()
    {
        var world = CreateWorld();

        var outside =
            new StrategicEdgeId(
                checked(
                    (ulong)world.StrategicTopology.Edges.Count
                    + 1UL));

        Assert.Throws<KeyNotFoundException>(
            () => new StrategicEconomicRouteCache(
                Resolver(world),
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        outside
                    })));
    }

    [Fact]
    public void FirstLookupMissesAndSecondLookupHits()
    {
        var s = OpenAdjacentScenario();
        var cache = Cache(s.Resolver, StrategicEdgeAccessSnapshot.AllOpen);

        Assert.False(cache.Resolve(s.Economy, s.Key).WasCacheHit);
        Assert.True(cache.Resolve(s.Economy, s.Key).WasCacheHit);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void CachedReachableOutputMatchesDirectResolver()
    {
        var s = OpenAdjacentScenario();

        var direct =
            s.Resolver.ResolveDetailed(
                s.Economy,
                s.Key,
                StrategicEdgeAccessSnapshot.AllOpen);

        var cached =
            Cache(
                s.Resolver,
                StrategicEdgeAccessSnapshot.AllOpen)
                .Resolve(
                    s.Economy,
                    s.Key);

        Assert.True(cached.IsReachable);
        AssertRouteEqual(direct.Route!, cached.Route!);
        Assert.Equal(direct.SearchDependencyEdgeIds, cached.SearchDependencyEdgeIds);
    }

    [Fact]
    public void CachedUnreachableOutputMatchesDirectResolver()
    {
        var world = CreateWorld();
        var source = world.StrategicTopology.Cells[0];
        var destination = Cell(world, source.AdjacentCellIds[0]);
        var access = new StrategicEdgeAccessSnapshot(source.IncidentEdgeIds);
        var economy = Economy(Point(1UL, source.Id), Point(2UL, destination.Id));
        var resolver = Resolver(world);
        var key = Key(1UL, 2UL);

        var direct =
            resolver.ResolveDetailed(
                economy,
                key,
                access);

        var cached =
            Cache(resolver, access)
                .Resolve(
                    economy,
                    key);

        Assert.False(cached.IsReachable);
        Assert.Null(cached.Route);
        Assert.Equal(direct.SearchDependencyEdgeIds, cached.SearchDependencyEdgeIds);
    }

    [Fact]
    public void ReopeningQueriedNonTraversedEdgeInvalidatesAndRestoresDirectRoute()
    {
        var s = ClosedDirectScenario();
        var cache = Cache(s.Resolver, s.Access);

        var first =
            cache.Resolve(
                s.Economy,
                s.Key);

        Assert.Contains(s.DirectEdge, first.SearchDependencyEdgeIds);
        Assert.DoesNotContain(s.DirectEdge, first.Route!.DependencyEdgeIds);

        var invalidated =
            cache.UpdateAccessSnapshot(
                StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Equal(new[] { s.Key }, invalidated);

        var refreshed =
            cache.Resolve(
                s.Economy,
                s.Key);

        Assert.False(refreshed.WasCacheHit);
        Assert.Equal(1, refreshed.Route!.HopCount);
        Assert.Contains(s.DirectEdge, refreshed.Route.DependencyEdgeIds);
    }

    [Fact]
    public void ClosingTraversedEdgeInvalidatesEntry()
    {
        var s = OpenAdjacentScenario();
        var cache = Cache(s.Resolver, StrategicEdgeAccessSnapshot.AllOpen);

        _ = cache.Resolve(s.Economy, s.Key);

        var invalidated =
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        s.DirectEdge
                    }));

        Assert.Equal(new[] { s.Key }, invalidated);
        Assert.False(cache.Resolve(s.Economy, s.Key).WasCacheHit);
    }

    [Fact]
    public void IdenticalAccessUpdateInvalidatesNothing()
    {
        var s = ClosedDirectScenario();
        var cache = Cache(s.Resolver, s.Access);

        _ = cache.Resolve(s.Economy, s.Key);

        var invalidated =
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        s.DirectEdge
                    }));

        Assert.Empty(invalidated);
        Assert.True(cache.Resolve(s.Economy, s.Key).WasCacheHit);
    }

    [Fact]
    public void UnreachableCacheRecomputesWhenFrontierEdgeOpens()
    {
        var world = CreateWorld();
        var source = world.StrategicTopology.Cells[0];
        var destination = Cell(world, source.AdjacentCellIds[0]);
        var direct = FindSharedEdge(source, destination);
        var economy = Economy(Point(1UL, source.Id), Point(2UL, destination.Id));
        var key = Key(1UL, 2UL);

        var cache =
            Cache(
                Resolver(world),
                new StrategicEdgeAccessSnapshot(source.IncidentEdgeIds));

        Assert.False(cache.Resolve(economy, key).IsReachable);

        var stillClosed =
            source
                .IncidentEdgeIds
                .Where(edgeId => edgeId != direct)
                .ToArray();

        Assert.Equal(
            new[] { key },
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(stillClosed)));

        var refreshed = cache.Resolve(economy, key);

        Assert.True(refreshed.IsReachable);
        Assert.Equal(1, refreshed.Route!.HopCount);
    }

    [Fact]
    public void SamePointEntrySurvivesUnrelatedAccessChange()
    {
        var world = CreateWorld();
        var economy = Economy(Point(1UL, world.StrategicTopology.Cells[0].Id));
        var key = Key(1UL, 1UL);
        var cache = Cache(Resolver(world), StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Empty(cache.Resolve(economy, key).SearchDependencyEdgeIds);

        var changedEdge = world.StrategicTopology.Edges[0].Id;

        Assert.Empty(
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        changedEdge
                    })));

        Assert.True(cache.Resolve(economy, key).WasCacheHit);
    }

    [Fact]
    public void UnrelatedAdjacentEntrySurvivesSelectiveInvalidation()
    {
        var world = CreateWorld();
        var firstSource = world.StrategicTopology.Cells[0];
        var firstDestination = Cell(world, firstSource.AdjacentCellIds[0]);
        var changedEdge = FindSharedEdge(firstSource, firstDestination);

        var secondSource =
            world.StrategicTopology.Cells.First(
                cell =>
                    !cell.IncidentEdgeIds.Contains(changedEdge)
                    && cell.AdjacentCellIds.Count > 0);

        var secondDestination = Cell(world, secondSource.AdjacentCellIds[0]);

        var economy =
            Economy(
                Point(1UL, firstSource.Id),
                Point(2UL, firstDestination.Id),
                Point(3UL, secondSource.Id),
                Point(4UL, secondDestination.Id));

        var cache = Cache(Resolver(world), StrategicEdgeAccessSnapshot.AllOpen);
        var firstKey = Key(1UL, 2UL);
        var secondKey = Key(3UL, 4UL);

        _ = cache.Resolve(economy, firstKey);

        var second =
            cache.Resolve(
                economy,
                secondKey);

        Assert.DoesNotContain(changedEdge, second.SearchDependencyEdgeIds);

        Assert.Equal(
            new[] { firstKey },
            cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        changedEdge
                    })));

        Assert.True(cache.Resolve(economy, secondKey).WasCacheHit);
    }

    [Fact]
    public void ExplicitInvalidationRejectsOutOfGraphEdge()
    {
        var world = CreateWorld();
        var cache = Cache(Resolver(world), StrategicEdgeAccessSnapshot.AllOpen);

        var outside =
            new StrategicEdgeId(
                checked(
                    (ulong)world.StrategicTopology.Edges.Count
                    + 1UL));

        Assert.Throws<KeyNotFoundException>(
            () => cache.InvalidateDependencies(
                new[]
                {
                    outside
                }));
    }

    [Fact]
    public void ExplicitInvalidationReturnsCanonicalAffectedKeys()
    {
        var world = CreateWorld();
        var source = world.StrategicTopology.Cells[0];
        var firstDestination = Cell(world, source.AdjacentCellIds[0]);
        var secondDestination = Cell(world, source.AdjacentCellIds[1]);

        var economy =
            Economy(
                Point(1UL, source.Id),
                Point(2UL, firstDestination.Id),
                Point(3UL, secondDestination.Id));

        var cache = Cache(Resolver(world), StrategicEdgeAccessSnapshot.AllOpen);
        var firstKey = Key(1UL, 2UL);
        var secondKey = Key(1UL, 3UL);

        var first = cache.Resolve(economy, firstKey);
        var second = cache.Resolve(economy, secondKey);

        var shared =
            first.SearchDependencyEdgeIds
                .Intersect(second.SearchDependencyEdgeIds)
                .First();

        Assert.Equal(
            new[]
            {
                firstKey,
                secondKey
            },
            cache.InvalidateDependencies(
                new[]
                {
                    shared,
                    shared
                }));
    }

    [Fact]
    public void AnchorDriftForcesRelationshipRecompute()
    {
        var world = CreateWorld();
        var source = world.StrategicTopology.Cells[0].Id;
        var firstDestination = world.StrategicTopology.Cells[1].Id;
        var secondDestination = world.StrategicTopology.Cells[^1].Id;
        var cache = Cache(Resolver(world), StrategicEdgeAccessSnapshot.AllOpen);
        var key = Key(1UL, 2UL);

        var first =
            cache.Resolve(
                Economy(Point(1UL, source), Point(2UL, firstDestination)),
                key);

        var second =
            cache.Resolve(
                Economy(Point(1UL, source), Point(2UL, secondDestination)),
                key);

        Assert.False(first.WasCacheHit);
        Assert.False(second.WasCacheHit);
        Assert.Equal(secondDestination, second.Route!.DestinationStrategicCellId);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void DeletedPointFailsInsteadOfServingStaleEntry()
    {
        var s = OpenAdjacentScenario();
        var cache = Cache(s.Resolver, StrategicEdgeAccessSnapshot.AllOpen);

        _ = cache.Resolve(s.Economy, s.Key);

        Assert.Throws<KeyNotFoundException>(
            () => cache.Resolve(
                Economy(Point(2UL, s.Destination.Id)),
                s.Key));

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void ClearReturnsCanonicalKeysAndEmptiesCache()
    {
        var s = OpenAdjacentScenario();
        var economy =
            Economy(
                s.Economy.EconomicPoints[0],
                s.Economy.EconomicPoints[1],
                Point(3UL, s.Source.Id));

        var cache = Cache(s.Resolver, StrategicEdgeAccessSnapshot.AllOpen);
        var late = Key(3UL, 2UL);
        var early = s.Key;

        _ = cache.Resolve(economy, late);
        _ = cache.Resolve(economy, early);

        Assert.Equal(
            new[]
            {
                early,
                late
            },
            cache.Clear());

        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void DirectionalKeysAreCachedSeparately()
    {
        var s = OpenAdjacentScenario();
        var cache = Cache(s.Resolver, StrategicEdgeAccessSnapshot.AllOpen);
        var reverse = Key(2UL, 1UL);

        Assert.False(cache.Resolve(s.Economy, s.Key).WasCacheHit);
        Assert.False(cache.Resolve(s.Economy, reverse).WasCacheHit);
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void RepeatedAccessChangesAlwaysMatchDirectResolver()
    {
        var s = OpenAdjacentScenario();
        var cache = Cache(s.Resolver, StrategicEdgeAccessSnapshot.AllOpen);

        var snapshots =
            new[]
            {
                StrategicEdgeAccessSnapshot.AllOpen,
                new StrategicEdgeAccessSnapshot(new[] { s.DirectEdge }),
                StrategicEdgeAccessSnapshot.AllOpen,
                new StrategicEdgeAccessSnapshot(new[] { s.DirectEdge }),
                StrategicEdgeAccessSnapshot.AllOpen
            };

        foreach (var snapshot in snapshots)
        {
            _ = cache.UpdateAccessSnapshot(snapshot);

            var cached = cache.Resolve(s.Economy, s.Key);
            var direct = s.Resolver.ResolveDetailed(s.Economy, s.Key, snapshot);

            Assert.Equal(direct.IsReachable, cached.IsReachable);

            if (direct.IsReachable)
            {
                AssertRouteEqual(direct.Route!, cached.Route!);
            }

            Assert.Equal(direct.SearchDependencyEdgeIds, cached.SearchDependencyEdgeIds);
        }
    }

    [Fact]
    public void InvalidAccessUpdateLeavesCurrentSnapshotUntouched()
    {
        var world = CreateWorld();
        var cache = Cache(Resolver(world), StrategicEdgeAccessSnapshot.AllOpen);

        var outside =
            new StrategicEdgeId(
                checked(
                    (ulong)world.StrategicTopology.Edges.Count
                    + 1UL));

        Assert.Throws<KeyNotFoundException>(
            () => cache.UpdateAccessSnapshot(
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        outside
                    })));

        Assert.Same(StrategicEdgeAccessSnapshot.AllOpen, cache.AccessSnapshot);
    }

    private static StrategicEconomicRoute LocalRoute()
    {
        return new StrategicEconomicRoute(
            Key(1UL, 1UL),
            new[]
            {
                new StrategicCellId(1UL)
            },
            Array.Empty<StrategicEdgeId>());
    }

    private static Scenario OpenAdjacentScenario()
    {
        var world = CreateWorld();
        var source = world.StrategicTopology.Cells[0];
        var destination = Cell(world, source.AdjacentCellIds[0]);
        var directEdge = FindSharedEdge(source, destination);
        var economy = Economy(Point(1UL, source.Id), Point(2UL, destination.Id));

        return new Scenario(
            world,
            Resolver(world),
            economy,
            Key(1UL, 2UL),
            source,
            destination,
            directEdge,
            StrategicEdgeAccessSnapshot.AllOpen);
    }

    private static Scenario ClosedDirectScenario()
    {
        var open = OpenAdjacentScenario();

        return open with
        {
            Access = new StrategicEdgeAccessSnapshot(
                new[]
                {
                    open.DirectEdge
                })
        };
    }

    private static StrategicEconomicRouteResolution ResolveDetailed(
        WorldGenerationResult world,
        StrategicCellId source,
        StrategicCellId destination,
        StrategicEdgeAccessSnapshot access)
    {
        var economy = Economy(Point(1UL, source), Point(2UL, destination));

        return Resolver(world).ResolveDetailed(
            economy,
            Key(1UL, 2UL),
            access);
    }

    private static WorldGenerationResult CreateWorld()
    {
        return new DeterministicWorldGenerator()
            .Generate(
                new WorldGenerationRequest(
                    new WorldSeed(4401UL),
                    WorldGenerationVersion.Initial,
                    new GoldbergParameters(1, 0)));
    }

    private static StrategicEconomicRouteResolver Resolver(
        WorldGenerationResult world)
    {
        return new StrategicEconomicRouteResolver(
            world.StrategicSurfaceGraph);
    }

    private static StrategicEconomicRouteCache Cache(
        StrategicEconomicRouteResolver resolver,
        StrategicEdgeAccessSnapshot access)
    {
        return new StrategicEconomicRouteCache(
            resolver,
            access);
    }

    private static EconomyRuntimeState Economy(
        params EconomicPointRuntimeState[] points)
    {
        return new EconomyRuntimeState(points);
    }

    private static EconomicPointRuntimeState Point(
        ulong id,
        StrategicCellId cellId)
    {
        return new EconomicPointRuntimeState(
            new EconomicPointId(id),
            new CivilizationId(1UL),
            cellId,
            0UL);
    }

    private static StrategicEconomicRouteKey Key(
        ulong sourceId,
        ulong destinationId)
    {
        return new StrategicEconomicRouteKey(
            new EconomicPointId(sourceId),
            new EconomicPointId(destinationId));
    }

    private static StrategicCell Cell(
        WorldGenerationResult world,
        StrategicCellId id)
    {
        return world.StrategicTopology.Cells[
            checked(
                (int)(id.Value - 1UL))];
    }

    private static StrategicEdgeId FindSharedEdge(
        StrategicCell first,
        StrategicCell second)
    {
        return Assert.Single(
            first.IncidentEdgeIds.Intersect(
                second.IncidentEdgeIds));
    }

    private static void AssertRouteEqual(
        StrategicEconomicRoute expected,
        StrategicEconomicRoute actual)
    {
        Assert.Equal(expected.Key, actual.Key);
        Assert.Equal(expected.CellPath, actual.CellPath);
        Assert.Equal(expected.EdgePath, actual.EdgePath);
    }

    private sealed record Scenario(
        WorldGenerationResult World,
        StrategicEconomicRouteResolver Resolver,
        EconomyRuntimeState Economy,
        StrategicEconomicRouteKey Key,
        StrategicCell Source,
        StrategicCell Destination,
        StrategicEdgeId DirectEdge,
        StrategicEdgeAccessSnapshot Access);
}
