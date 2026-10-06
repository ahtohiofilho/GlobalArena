using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class M44BStrategicEconomicRouteTests
{
    [Fact]
    public void RouteKeyRejectsInvalidSource()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicEconomicRouteKey(
                default,
                new EconomicPointId(1UL)));
    }

    [Fact]
    public void RouteKeyRejectsInvalidDestination()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicEconomicRouteKey(
                new EconomicPointId(1UL),
                default));
    }

    [Fact]
    public void RouteKeyIsDirectional()
    {
        var forward =
            new StrategicEconomicRouteKey(
                new EconomicPointId(1UL),
                new EconomicPointId(2UL));

        var reverse =
            new StrategicEconomicRouteKey(
                new EconomicPointId(2UL),
                new EconomicPointId(1UL));

        Assert.NotEqual(
            forward,
            reverse);
    }

    [Fact]
    public void AccessSnapshotRejectsInvalidEdge()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicEdgeAccessSnapshot(
                new[]
                {
                    default(StrategicEdgeId)
                }));
    }

    [Fact]
    public void AccessSnapshotRejectsDuplicateClosedEdge()
    {
        var edgeId =
            new StrategicEdgeId(1UL);

        Assert.Throws<ArgumentException>(
            () => new StrategicEdgeAccessSnapshot(
                new[]
                {
                    edgeId,
                    edgeId
                }));
    }

    [Fact]
    public void AccessSnapshotCanonicalizesClosedEdges()
    {
        var snapshot =
            new StrategicEdgeAccessSnapshot(
                new[]
                {
                    new StrategicEdgeId(3UL),
                    new StrategicEdgeId(1UL),
                    new StrategicEdgeId(2UL)
                });

        Assert.Equal(
            new ulong[]
            {
                1UL,
                2UL,
                3UL
            },
            snapshot.ClosedEdgeIds.Select(
                edgeId =>
                    edgeId.Value));
    }

    [Fact]
    public void AccessSnapshotDefaultsAbsentEdgesToOpen()
    {
        var snapshot =
            new StrategicEdgeAccessSnapshot(
                new[]
                {
                    new StrategicEdgeId(2UL)
                });

        Assert.True(
            snapshot.IsOpen(
                new StrategicEdgeId(1UL)));

        Assert.False(
            snapshot.IsOpen(
                new StrategicEdgeId(2UL)));
    }

    [Fact]
    public void ResolverRejectsNullGraph()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StrategicEconomicRouteResolver(
                null!));
    }

    [Fact]
    public void ResolverRejectsUnknownSourcePoint()
    {
        var world =
            CreateWorld();

        var resolver =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph);

        var economy =
            Economy(
                Point(
                    2UL,
                    world.StrategicTopology.Cells[1].Id));

        Assert.Throws<KeyNotFoundException>(
            () => resolver.TryResolve(
                economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(2UL)),
                StrategicEdgeAccessSnapshot.AllOpen,
                out _));
    }

    [Fact]
    public void ResolverRejectsUnknownDestinationPoint()
    {
        var world =
            CreateWorld();

        var resolver =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph);

        var economy =
            Economy(
                Point(
                    1UL,
                    world.StrategicTopology.Cells[0].Id));

        Assert.Throws<KeyNotFoundException>(
            () => resolver.TryResolve(
                economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(2UL)),
                StrategicEdgeAccessSnapshot.AllOpen,
                out _));
    }

    [Fact]
    public void ResolverRejectsPointAnchorOutsideGraph()
    {
        var world =
            CreateWorld();

        var resolver =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph);

        var invalidAnchor =
            new StrategicCellId(
                checked(
                    (ulong)world.StrategicSurfaceGraph.NodeCount
                    + 1UL));

        var economy =
            Economy(
                Point(
                    1UL,
                    invalidAnchor));

        Assert.Throws<KeyNotFoundException>(
            () => resolver.TryResolve(
                economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(1UL)),
                StrategicEdgeAccessSnapshot.AllOpen,
                out _));
    }

    [Fact]
    public void ResolverRejectsClosedEdgeOutsideGraph()
    {
        var world =
            CreateWorld();

        var resolver =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph);

        var source =
            world.StrategicTopology.Cells[0].Id;

        var economy =
            Economy(
                Point(
                    1UL,
                    source));

        var outside =
            new StrategicEdgeId(
                checked(
                    (ulong)world.StrategicTopology.Edges.Count
                    + 1UL));

        Assert.Throws<KeyNotFoundException>(
            () => resolver.TryResolve(
                economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(1UL)),
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        outside
                    }),
                out _));
    }

    [Fact]
    public void SamePointRouteUsesOneCellAndZeroEdges()
    {
        var world =
            CreateWorld();

        var source =
            world.StrategicTopology.Cells[0].Id;

        var economy =
            Economy(
                Point(
                    1UL,
                    source));

        var resolved =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph)
                .TryResolve(
                    economy,
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL),
                        new EconomicPointId(1UL)),
                    StrategicEdgeAccessSnapshot.AllOpen,
                    out var route);

        Assert.True(
            resolved);

        Assert.NotNull(
            route);

        Assert.Equal(
            new[]
            {
                source
            },
            route!.CellPath);

        Assert.Empty(
            route.EdgePath);

        Assert.Equal(
            0,
            route.HopCount);
    }

    [Fact]
    public void CoLocatedDistinctPointsUseZeroStrategicEdges()
    {
        var world =
            CreateWorld();

        var anchor =
            world.StrategicTopology.Cells[0].Id;

        var economy =
            Economy(
                Point(
                    1UL,
                    anchor),
                Point(
                    2UL,
                    anchor));

        var resolved =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph)
                .TryResolve(
                    economy,
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL),
                        new EconomicPointId(2UL)),
                    StrategicEdgeAccessSnapshot.AllOpen,
                    out var route);

        Assert.True(
            resolved);

        Assert.NotNull(
            route);

        Assert.Single(
            route!.CellPath);

        Assert.Empty(
            route.EdgePath);
    }

    [Fact]
    public void AdjacentPointsResolveThroughTheirSharedStrategicEdge()
    {
        var world =
            CreateWorld();

        var sourceCell =
            world.StrategicTopology.Cells[0];

        var destinationId =
            sourceCell.AdjacentCellIds[0];

        var destinationCell =
            world.StrategicTopology.Cells[
                checked(
                    (int)(
                        destinationId.Value
                        - 1UL))];

        var sharedEdge =
            FindSharedEdge(
                sourceCell,
                destinationCell);

        var route =
            ResolveRequired(
                world,
                sourceCell.Id,
                destinationCell.Id,
                StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Equal(
            1,
            route.HopCount);

        Assert.Equal(
            new[]
            {
                sourceCell.Id,
                destinationCell.Id
            },
            route.CellPath);

        Assert.Equal(
            new[]
            {
                sharedEdge
            },
            route.EdgePath);
    }

    [Fact]
    public void RouteEdgePathMatchesEveryCellTransition()
    {
        var world =
            CreateWorld();

        var source =
            world.StrategicTopology.Cells[0].Id;

        var destination =
            world.StrategicTopology.Cells[^1].Id;

        var route =
            ResolveRequired(
                world,
                source,
                destination,
                StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Equal(
            route.CellPath.Count - 1,
            route.EdgePath.Count);

        for (var index = 0;
             index < route.EdgePath.Count;
             index++)
        {
            var edge =
                world.StrategicTopology.Edges[
                    checked(
                        (int)(
                            route.EdgePath[index].Value
                            - 1UL))];

            Assert.Contains(
                route.CellPath[index],
                edge.IncidentCellIds);

            Assert.Contains(
                route.CellPath[index + 1],
                edge.IncidentCellIds);
        }
    }

    [Fact]
    public void BlockingDirectEdgeForcesAlternateRoute()
    {
        var world =
            CreateWorld();

        var sourceCell =
            world.StrategicTopology.Cells[0];

        var destinationId =
            sourceCell.AdjacentCellIds[0];

        var destinationCell =
            world.StrategicTopology.Cells[
                checked(
                    (int)(
                        destinationId.Value
                        - 1UL))];

        var directEdge =
            FindSharedEdge(
                sourceCell,
                destinationCell);

        var route =
            ResolveRequired(
                world,
                sourceCell.Id,
                destinationCell.Id,
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        directEdge
                    }));

        Assert.True(
            route.HopCount > 1);

        Assert.DoesNotContain(
            directEdge,
            route.EdgePath);
    }

    [Fact]
    public void ClosingEverySourceIncidentEdgeMakesDestinationUnreachable()
    {
        var world =
            CreateWorld();

        var sourceCell =
            world.StrategicTopology.Cells[0];

        var destinationId =
            sourceCell.AdjacentCellIds[0];

        var economy =
            Economy(
                Point(
                    1UL,
                    sourceCell.Id),
                Point(
                    2UL,
                    destinationId));

        var resolver =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph);

        var resolved =
            resolver.TryResolve(
                economy,
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(2UL)),
                new StrategicEdgeAccessSnapshot(
                    sourceCell.IncidentEdgeIds),
                out var route);

        Assert.False(
            resolved);

        Assert.Null(
            route);
    }

    [Fact]
    public void ResolvedRouteNeverUsesClosedEdge()
    {
        var world =
            CreateWorld();

        var sourceCell =
            world.StrategicTopology.Cells[0];

        var closedEdge =
            sourceCell.IncidentEdgeIds[0];

        var destination =
            world.StrategicTopology.Cells[^1].Id;

        var route =
            ResolveRequired(
                world,
                sourceCell.Id,
                destination,
                new StrategicEdgeAccessSnapshot(
                    new[]
                    {
                        closedEdge
                    }));

        Assert.DoesNotContain(
            closedEdge,
            route.DependencyEdgeIds);
    }

    [Fact]
    public void RepeatedResolutionIsDeterministic()
    {
        var world =
            CreateWorld();

        var source =
            world.StrategicTopology.Cells[0].Id;

        var destination =
            world.StrategicTopology.Cells[^1].Id;

        var first =
            ResolveRequired(
                world,
                source,
                destination,
                StrategicEdgeAccessSnapshot.AllOpen);

        var second =
            ResolveRequired(
                world,
                source,
                destination,
                StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Equal(
            first.Key,
            second.Key);

        Assert.Equal(
            first.CellPath,
            second.CellPath);

        Assert.Equal(
            first.EdgePath,
            second.EdgePath);
    }

    [Fact]
    public void EqualHopAlternativesChooseCanonicalLowestIntermediateCell()
    {
        var world =
            CreateWorld();

        var graph =
            world.StrategicSurfaceGraph;

        var sourceIndex =
            0;

        var sourceNeighbors =
            graph
                .GetNeighborIndexes(
                    sourceIndex)
                .ToArray();

        var selectedDestination =
            -1;

        int[] selectedIntermediates =
            Array.Empty<int>();

        for (var destinationIndex = 1;
             destinationIndex < graph.NodeCount;
             destinationIndex++)
        {
            if (sourceNeighbors.Contains(
                destinationIndex))
            {
                continue;
            }

            var intermediates =
                sourceNeighbors
                    .Where(
                        neighborIndex =>
                            graph
                                .GetNeighborIndexes(
                                    neighborIndex)
                                .Contains(
                                    destinationIndex))
                    .Order()
                    .ToArray();

            if (intermediates.Length >= 2)
            {
                selectedDestination =
                    destinationIndex;

                selectedIntermediates =
                    intermediates;

                break;
            }
        }

        Assert.True(
            selectedDestination >= 0);

        var sourceCellId =
            graph.GetCellId(
                sourceIndex);

        var destinationCellId =
            graph.GetCellId(
                selectedDestination);

        var route =
            ResolveRequired(
                world,
                sourceCellId,
                destinationCellId,
                StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Equal(
            2,
            route.HopCount);

        Assert.Equal(
            graph.GetCellId(
                selectedIntermediates[0]),
            route.CellPath[1]);
    }

    [Fact]
    public void ReverseKeyProducesReverseDirectionIdentity()
    {
        var world =
            CreateWorld();

        var first =
            world.StrategicTopology.Cells[0].Id;

        var second =
            world.StrategicTopology.Cells[1].Id;

        var forward =
            ResolveRequired(
                world,
                first,
                second,
                StrategicEdgeAccessSnapshot.AllOpen);

        var reverse =
            ResolveRequired(
                world,
                second,
                first,
                StrategicEdgeAccessSnapshot.AllOpen);

        Assert.Equal(
            new EconomicPointId(1UL),
            forward.Key.SourceEconomicPointId);

        Assert.Equal(
            new EconomicPointId(2UL),
            forward.Key.DestinationEconomicPointId);

        Assert.Equal(
            new EconomicPointId(1UL),
            reverse.Key.SourceEconomicPointId);

        Assert.Equal(
            new EconomicPointId(2UL),
            reverse.Key.DestinationEconomicPointId);

        Assert.Equal(
            first,
            forward.SourceStrategicCellId);

        Assert.Equal(
            second,
            forward.DestinationStrategicCellId);

        Assert.Equal(
            second,
            reverse.SourceStrategicCellId);

        Assert.Equal(
            first,
            reverse.DestinationStrategicCellId);
    }

    [Fact]
    public void RouteRejectsInvalidKey()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicEconomicRoute(
                default,
                new[]
                {
                    new StrategicCellId(1UL)
                },
                Array.Empty<StrategicEdgeId>()));
    }

    [Fact]
    public void RouteCopiesInputPathCollections()
    {
        var key =
            new StrategicEconomicRouteKey(
                new EconomicPointId(1UL),
                new EconomicPointId(2UL));

        var cells =
            new[]
            {
                new StrategicCellId(1UL),
                new StrategicCellId(2UL)
            };

        var edges =
            new[]
            {
                new StrategicEdgeId(1UL)
            };

        var route =
            new StrategicEconomicRoute(
                key,
                cells,
                edges);

        cells[0] =
            new StrategicCellId(3UL);

        edges[0] =
            new StrategicEdgeId(2UL);

        Assert.Equal(
            1UL,
            route.CellPath[0].Value);

        Assert.Equal(
            1UL,
            route.EdgePath[0].Value);
    }

    [Fact]
    public void RouteRejectsRepeatedCells()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicEconomicRoute(
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(2UL)),
                new[]
                {
                    new StrategicCellId(1UL),
                    new StrategicCellId(2UL),
                    new StrategicCellId(1UL)
                },
                new[]
                {
                    new StrategicEdgeId(1UL),
                    new StrategicEdgeId(2UL)
                }));
    }

    [Fact]
    public void RouteRejectsMismatchedCellAndEdgeCounts()
    {
        Assert.Throws<ArgumentException>(
            () => new StrategicEconomicRoute(
                new StrategicEconomicRouteKey(
                    new EconomicPointId(1UL),
                    new EconomicPointId(2UL)),
                new[]
                {
                    new StrategicCellId(1UL),
                    new StrategicCellId(2UL)
                },
                Array.Empty<StrategicEdgeId>()));
    }

    private static WorldGenerationResult CreateWorld()
    {
        return new DeterministicWorldGenerator()
            .Generate(
                new WorldGenerationRequest(
                    new WorldSeed(
                        4401UL),
                    WorldGenerationVersion.Initial,
                    new GoldbergParameters(
                        1,
                        0)));
    }

    private static EconomyRuntimeState Economy(
        params EconomicPointRuntimeState[] points)
    {
        return new EconomyRuntimeState(
            points);
    }

    private static EconomicPointRuntimeState Point(
        ulong id,
        StrategicCellId cellId)
    {
        return new EconomicPointRuntimeState(
            new EconomicPointId(
                id),
            new CivilizationId(
                1UL),
            cellId,
            0UL);
    }

    private static StrategicEconomicRoute ResolveRequired(
        WorldGenerationResult world,
        StrategicCellId source,
        StrategicCellId destination,
        StrategicEdgeAccessSnapshot access)
    {
        var economy =
            Economy(
                Point(
                    1UL,
                    source),
                Point(
                    2UL,
                    destination));

        var resolved =
            new StrategicEconomicRouteResolver(
                world.StrategicSurfaceGraph)
                .TryResolve(
                    economy,
                    new StrategicEconomicRouteKey(
                        new EconomicPointId(1UL),
                        new EconomicPointId(2UL)),
                    access,
                    out var route);

        Assert.True(
            resolved);

        return Assert.IsType<StrategicEconomicRoute>(
            route);
    }

    private static StrategicEdgeId FindSharedEdge(
        StrategicCell first,
        StrategicCell second)
    {
        return Assert.Single(
            first.IncidentEdgeIds.Intersect(
                second.IncidentEdgeIds));
    }
}
