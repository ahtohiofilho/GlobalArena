using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalPathfinderTests
{
    [Fact]
    public void NullIncidenceMapIsRejected()
    {
        var tileId =
            new PhysicalTacticalTileId(
                new GoldbergParameters(6, 0),
                new StrategicCellId(1));

        Assert.Throws<ArgumentNullException>(
            () =>
                PhysicalTacticalPathfinder.FindShortestPath(
                    null!,
                    tileId,
                    tileId));
    }

    [Fact]
    public void DefaultStartIdentityIsRejected()
    {
        var map =
            CreateMap();

        Assert.Throws<ArgumentException>(
            () =>
                PhysicalTacticalPathfinder.FindShortestPath(
                    map,
                    default,
                    map.TileIncidences[0].PhysicalTacticalTileId));
    }

    [Fact]
    public void DefaultEndIdentityIsRejected()
    {
        var map =
            CreateMap();

        Assert.Throws<ArgumentException>(
            () =>
                PhysicalTacticalPathfinder.FindShortestPath(
                    map,
                    map.TileIncidences[0].PhysicalTacticalTileId,
                    default));
    }

    [Fact]
    public void StartFromDifferentFineTopologyIsRejected()
    {
        var map =
            CreateMap();

        var foreign =
            new PhysicalTacticalTileId(
                new GoldbergParameters(12, 0),
                new StrategicCellId(1));

        Assert.Throws<ArgumentException>(
            () =>
                PhysicalTacticalPathfinder.FindShortestPath(
                    map,
                    foreign,
                    map.TileIncidences[0].PhysicalTacticalTileId));
    }

    [Fact]
    public void EndFromDifferentFineTopologyIsRejected()
    {
        var map =
            CreateMap();

        var foreign =
            new PhysicalTacticalTileId(
                new GoldbergParameters(12, 0),
                new StrategicCellId(1));

        Assert.Throws<ArgumentException>(
            () =>
                PhysicalTacticalPathfinder.FindShortestPath(
                    map,
                    map.TileIncidences[0].PhysicalTacticalTileId,
                    foreign));
    }

    [Fact]
    public void SameStartAndEndProducesSingletonZeroStepPath()
    {
        var map =
            CreateMap();

        var tileId =
            map.TileIncidences[100]
                .PhysicalTacticalTileId;

        var path =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                tileId,
                tileId);

        Assert.Single(
            path.TileIds);

        Assert.Equal(
            0,
            path.StepCount);

        Assert.Equal(
            tileId,
            path.Start);

        Assert.Equal(
            tileId,
            path.End);
    }

    [Fact]
    public void AdjacentFineTilesProduceOneStepPath()
    {
        var map =
            CreateMap();

        var firstCell =
            map.FineTopology.Cells[0];

        var secondCellId =
            firstCell.AdjacentCellIds[0];

        var start =
            ToPhysicalId(
                map,
                firstCell.Id);

        var end =
            ToPhysicalId(
                map,
                secondCellId);

        var path =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                start,
                end);

        Assert.Equal(
            1,
            path.StepCount);

        Assert.Equal(
            new[]
            {
                start,
                end
            },
            path.TileIds);
    }

    [Fact]
    public void EveryReturnedStepUsesAuthoritativeFineAdjacency()
    {
        var map =
            CreateMap();

        var path =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                map.TileIncidences[0].PhysicalTacticalTileId,
                map.TileIncidences[^1].PhysicalTacticalTileId);

        Assert.True(
            path.StepCount > 0);

        for (var index = 1; index < path.TileIds.Count; index++)
        {
            var previous =
                map.FineTopology.Cells[
                    checked(
                        (int)path.TileIds[index - 1]
                            .FineStrategicCellId
                            .Value
                        - 1)];

            Assert.Contains(
                path.TileIds[index]
                    .FineStrategicCellId,
                previous.AdjacentCellIds);
        }
    }

    [Fact]
    public void ReturnedPathNeverRepeatsPhysicalIdentity()
    {
        var map =
            CreateMap();

        var path =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                map.TileIncidences[0].PhysicalTacticalTileId,
                map.TileIncidences[^1].PhysicalTacticalTileId);

        Assert.Equal(
            path.TileIds.Count,
            path.TileIds.Distinct().Count());
    }

    [Fact]
    public void RepeatedTraversalIsDeterministic()
    {
        var map =
            CreateMap();

        var start =
            map.TileIncidences[0]
                .PhysicalTacticalTileId;

        var end =
            map.TileIncidences[^1]
                .PhysicalTacticalTileId;

        var first =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                start,
                end);

        var second =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                start,
                end);

        Assert.Equal(
            first.TileIds,
            second.TileIds);
    }

    [Fact]
    public void EdgeSharedTileProvidesTwoStepCrossCoarseBridge()
    {
        var map =
            CreateMap();

        var incidenceByFineId =
            CreateIncidenceLookup(
                map);

        var edgeShared =
            map.TileIncidences.First(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 2);

        var centerId =
            edgeShared
                .PhysicalTacticalTileId
                .FineStrategicCellId;

        var centerCell =
            map.FineTopology.Cells[
                checked((int)centerId.Value - 1)];

        var coarseA =
            edgeShared.IncidentCoarseCellIds[0];

        var coarseB =
            edgeShared.IncidentCoarseCellIds[1];

        var sideA =
            centerCell.AdjacentCellIds
                .Where(
                    neighborId =>
                        IsSingleCoarseOwner(
                            incidenceByFineId[neighborId],
                            coarseA))
                .ToArray();

        var sideB =
            centerCell.AdjacentCellIds
                .Where(
                    neighborId =>
                        IsSingleCoarseOwner(
                            incidenceByFineId[neighborId],
                            coarseB))
                .ToArray();

        Assert.Equal(
            3,
            sideA.Length);

        Assert.Equal(
            3,
            sideB.Length);

        var matchingPath =
            FindTwoStepPathThrough(
                map,
                sideA,
                sideB,
                centerId);

        Assert.NotNull(
            matchingPath);

        Assert.Equal(
            2,
            matchingPath!.StepCount);

        Assert.Equal(
            centerId,
            matchingPath.TileIds[1]
                .FineStrategicCellId);
    }

    [Fact]
    public void VertexSharedTileProvidesTwoStepBridgeBetweenIncidentCoarseRegions()
    {
        var map =
            CreateMap();

        var incidenceByFineId =
            CreateIncidenceLookup(
                map);

        var vertexShared =
            map.TileIncidences.First(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 3);

        var centerId =
            vertexShared
                .PhysicalTacticalTileId
                .FineStrategicCellId;

        var centerCell =
            map.FineTopology.Cells[
                checked((int)centerId.Value - 1)];

        var coarseA =
            vertexShared.IncidentCoarseCellIds[0];

        var coarseB =
            vertexShared.IncidentCoarseCellIds[1];

        var sideA =
            centerCell.AdjacentCellIds
                .Where(
                    neighborId =>
                        IsSingleCoarseOwner(
                            incidenceByFineId[neighborId],
                            coarseA))
                .ToArray();

        var sideB =
            centerCell.AdjacentCellIds
                .Where(
                    neighborId =>
                        IsSingleCoarseOwner(
                            incidenceByFineId[neighborId],
                            coarseB))
                .ToArray();

        Assert.Equal(
            2,
            sideA.Length);

        Assert.Equal(
            2,
            sideB.Length);

        var matchingPath =
            FindTwoStepPathThrough(
                map,
                sideA,
                sideB,
                centerId);

        Assert.NotNull(
            matchingPath);

        Assert.Equal(
            2,
            matchingPath!.StepCount);

        Assert.Equal(
            centerId,
            matchingPath.TileIds[1]
                .FineStrategicCellId);

        Assert.Equal(
            3,
            incidenceByFineId[
                matchingPath.TileIds[1]
                    .FineStrategicCellId]
                .IncidentCoarseCellIds
                .Count);
    }

    [Fact]
    public void DistantPentagonsTraverseAcrossDistinctCoarseOwnership()
    {
        var map =
            CreateMap();

        var incidenceByFineId =
            CreateIncidenceLookup(
                map);

        var pentagons =
            map.FineTopology.Cells
                .Where(
                    cell =>
                        cell.Kind
                        == StrategicCellKind.Pentagon)
                .ToArray();

        Assert.Equal(
            12,
            pentagons.Length);

        var start =
            ToPhysicalId(
                map,
                pentagons[0].Id);

        var end =
            ToPhysicalId(
                map,
                pentagons[^1].Id);

        var startOwner =
            Assert.Single(
                incidenceByFineId[
                    start.FineStrategicCellId]
                    .IncidentCoarseCellIds);

        var endOwner =
            Assert.Single(
                incidenceByFineId[
                    end.FineStrategicCellId]
                    .IncidentCoarseCellIds);

        Assert.NotEqual(
            startOwner,
            endOwner);

        var path =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                start,
                end);

        Assert.Equal(
            start,
            path.Start);

        Assert.Equal(
            end,
            path.End);

        var ownershipTransitionObserved = false;

        for (var index = 1;
             index < path.TileIds.Count;
             index++)
        {
            var previousIncidence =
                incidenceByFineId[
                    path.TileIds[index - 1]
                        .FineStrategicCellId];

            var currentIncidence =
                incidenceByFineId[
                    path.TileIds[index]
                        .FineStrategicCellId];

            if (!previousIncidence
                .IncidentCoarseCellIds
                .SequenceEqual(
                    currentIncidence
                        .IncidentCoarseCellIds))
            {
                ownershipTransitionObserved = true;
                break;
            }
        }

        Assert.True(
            ownershipTransitionObserved);
    }

    [Fact]
    public void ReturnedPathIsShortestAgainstIndependentBreadthFirstDistance()
    {
        var map =
            CreateMap();

        var starts =
            new[]
            {
                map.FineTopology.Cells[0].Id,
                map.FineTopology.Cells[91].Id,
                map.FineTopology.Cells[181].Id
            };

        var ends =
            new[]
            {
                map.FineTopology.Cells[361].Id,
                map.FineTopology.Cells[250].Id,
                map.FineTopology.Cells[17].Id
            };

        for (var index = 0; index < starts.Length; index++)
        {
            var start =
                ToPhysicalId(
                    map,
                    starts[index]);

            var end =
                ToPhysicalId(
                    map,
                    ends[index]);

            var path =
                PhysicalTacticalPathfinder.FindShortestPath(
                    map,
                    start,
                    end);

            Assert.Equal(
                ComputeIndependentDistance(
                    map,
                    starts[index],
                    ends[index]),
                path.StepCount);
        }
    }

    [Fact]
    public void PathTileCollectionIsReadOnly()
    {
        var map =
            CreateMap();

        var start =
            map.TileIncidences[0]
                .PhysicalTacticalTileId;

        var end =
            map.TileIncidences[1]
                .PhysicalTacticalTileId;

        var path =
            PhysicalTacticalPathfinder.FindShortestPath(
                map,
                start,
                end);

        var mutableView =
            Assert.IsAssignableFrom<IList<PhysicalTacticalTileId>>(
                path.TileIds);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.Add(
                    start));
    }

    private static PhysicalTacticalIncidenceMap CreateMap()
    {
        return PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(
                new GoldbergParameters(1, 0),
                new GoldbergParameters(6, 0)));
    }

    private static PhysicalTacticalTileId ToPhysicalId(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId fineCellId)
    {
        return new PhysicalTacticalTileId(
            map.Refinement.FineParameters,
            fineCellId);
    }

    private static Dictionary<StrategicCellId, PhysicalTacticalTileIncidence>
        CreateIncidenceLookup(
            PhysicalTacticalIncidenceMap map)
    {
        return map.TileIncidences.ToDictionary(
            incidence =>
                incidence
                    .PhysicalTacticalTileId
                    .FineStrategicCellId);
    }

    private static bool IsSingleCoarseOwner(
        PhysicalTacticalTileIncidence incidence,
        StrategicCellId expectedOwner)
    {
        return incidence.IncidentCoarseCellIds.Count == 1
            && incidence.IncidentCoarseCellIds[0] == expectedOwner;
    }

    private static PhysicalTacticalPath? FindTwoStepPathThrough(
        PhysicalTacticalIncidenceMap map,
        IEnumerable<StrategicCellId> starts,
        IEnumerable<StrategicCellId> ends,
        StrategicCellId expectedMiddle)
    {
        foreach (var startId in starts)
        {
            foreach (var endId in ends)
            {
                var path =
                    PhysicalTacticalPathfinder.FindShortestPath(
                        map,
                        ToPhysicalId(
                            map,
                            startId),
                        ToPhysicalId(
                            map,
                            endId));

                if (path.StepCount == 2
                    && path.TileIds[1].FineStrategicCellId
                        == expectedMiddle)
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static int ComputeIndependentDistance(
        PhysicalTacticalIncidenceMap map,
        StrategicCellId start,
        StrategicCellId end)
    {
        if (start == end)
        {
            return 0;
        }

        var distances =
            new Dictionary<StrategicCellId, int>
            {
                [start] = 0
            };

        var queue =
            new Queue<StrategicCellId>();

        queue.Enqueue(
            start);

        while (queue.Count > 0)
        {
            var current =
                queue.Dequeue();

            var distance =
                distances[current];

            var cell =
                map.FineTopology.Cells[
                    checked((int)current.Value - 1)];

            foreach (var neighborId in cell.AdjacentCellIds)
            {
                if (distances.ContainsKey(neighborId))
                {
                    continue;
                }

                var neighborDistance =
                    distance + 1;

                if (neighborId == end)
                {
                    return neighborDistance;
                }

                distances.Add(
                    neighborId,
                    neighborDistance);

                queue.Enqueue(
                    neighborId);
            }
        }

        throw new InvalidOperationException(
            "Independent distance probe did not find a path.");
    }
}