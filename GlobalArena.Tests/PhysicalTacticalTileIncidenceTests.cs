using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalTileIncidenceTests
{
    private static readonly StrategicTopology CoarseTopology =
        GoldbergStrategicTopologyGenerator.Generate(
            new GoldbergParameters(
                1,
                0));

    [Fact]
    public void NullCoarseTopologyIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    null!,
                    new[]
                    {
                        CoarseTopology.Cells[0].Id
                    }));
    }

    [Fact]
    public void InvalidPhysicalTileIdIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    default,
                    CoarseTopology,
                    new[]
                    {
                        CoarseTopology.Cells[0].Id
                    }));
    }

    [Fact]
    public void NullIncidentCellCollectionIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    null!));
    }

    [Fact]
    public void EmptyIncidenceIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    Array.Empty<StrategicCellId>()));
    }

    [Fact]
    public void IncidenceAboveThreeCellsIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    CoarseTopology.Cells
                        .Take(4)
                        .Select(cell => cell.Id)));
    }

    [Fact]
    public void InvalidIncidentCellIdIsRejected()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    new[]
                    {
                        default(StrategicCellId)
                    }));
    }

    [Fact]
    public void DuplicateIncidentCellIdsAreRejected()
    {
        var cellId =
            CoarseTopology.Cells[0].Id;

        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    new[]
                    {
                        cellId,
                        cellId
                    }));
    }

    [Fact]
    public void IncidentCellOutsideCoarseTopologyIsRejected()
    {
        var outside =
            new StrategicCellId(
                (ulong)CoarseTopology.Cells.Count
                + 1UL);

        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    new[]
                    {
                        outside
                    }));
    }

    [Fact]
    public void IncompatibleFineResolutionIsRejected()
    {
        var incompatiblePhysicalId =
            new PhysicalTacticalTileId(
                new GoldbergParameters(
                    2,
                    2),
                new StrategicCellId(
                    1UL));

        Assert.Throws<NotSupportedException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    incompatiblePhysicalId,
                    CoarseTopology,
                    new[]
                    {
                        CoarseTopology.Cells[0].Id
                    }));
    }

    [Fact]
    public void OneCellIncidenceCreatesInteriorContract()
    {
        var coarseCellId =
            CoarseTopology.Cells[0].Id;

        var incidence =
            new PhysicalTacticalTileIncidence(
                CreatePhysicalId(),
                CoarseTopology,
                new[]
                {
                    coarseCellId
                });

        Assert.Equal(
            CreatePhysicalId(),
            incidence.PhysicalTacticalTileId);

        Assert.Equal(
            new[]
            {
                coarseCellId
            },
            incidence.IncidentCoarseCellIds);
    }

    [Fact]
    public void TwoCellIncidenceAcceptsAuthoritativeStrategicEdge()
    {
        var edge =
            CoarseTopology.Edges[0];

        var incidence =
            new PhysicalTacticalTileIncidence(
                CreatePhysicalId(),
                CoarseTopology,
                edge.IncidentCellIds.Reverse());

        Assert.Equal(
            edge.IncidentCellIds,
            incidence.IncidentCoarseCellIds);
    }

    [Fact]
    public void TwoCellIncidenceRejectsNonEdgePair()
    {
        var pair =
            FindNonEdgePair();

        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    pair));
    }

    [Fact]
    public void ThreeCellIncidenceAcceptsAuthoritativeStrategicVertex()
    {
        var vertex =
            CoarseTopology.Vertices[0];

        var incidence =
            new PhysicalTacticalTileIncidence(
                CreatePhysicalId(),
                CoarseTopology,
                vertex.IncidentCellIds.Reverse());

        Assert.Equal(
            vertex.IncidentCellIds,
            incidence.IncidentCoarseCellIds);
    }

    [Fact]
    public void ThreeCellIncidenceRejectsNonVertexTriple()
    {
        var triple =
            FindNonVertexTriple();

        Assert.Throws<ArgumentException>(
            () =>
                new PhysicalTacticalTileIncidence(
                    CreatePhysicalId(),
                    CoarseTopology,
                    triple));
    }

    [Fact]
    public void IncidentCellsAreCanonicalReadOnlySnapshot()
    {
        var source =
            CoarseTopology.Vertices[0]
                .IncidentCellIds
                .Reverse()
                .ToArray();

        var expected =
            source
                .OrderBy(id => id.Value)
                .ToArray();

        var incidence =
            new PhysicalTacticalTileIncidence(
                CreatePhysicalId(),
                CoarseTopology,
                source);

        source[0] =
            CoarseTopology.Cells[^1].Id;

        Assert.Equal(
            expected,
            incidence.IncidentCoarseCellIds);

        var mutableView =
            Assert.IsAssignableFrom<IList<StrategicCellId>>(
                incidence.IncidentCoarseCellIds);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView[0] =
                    CoarseTopology.Cells[0].Id);
    }

    private static PhysicalTacticalTileId CreatePhysicalId()
    {
        return new PhysicalTacticalTileId(
            new GoldbergParameters(
                3,
                0),
            new StrategicCellId(
                1UL));
    }

    private static StrategicCellId[] FindNonEdgePair()
    {
        for (var first = 0;
             first < CoarseTopology.Cells.Count;
             first++)
        {
            for (var second = first + 1;
                 second < CoarseTopology.Cells.Count;
                 second++)
            {
                var candidate =
                    new[]
                    {
                        CoarseTopology.Cells[first].Id,
                        CoarseTopology.Cells[second].Id
                    }
                    .OrderBy(id => id.Value)
                    .ToArray();

                if (!CoarseTopology.Edges.Any(
                    edge =>
                        edge.IncidentCellIds.SequenceEqual(
                            candidate)))
                {
                    return candidate;
                }
            }
        }

        throw new InvalidOperationException(
            "Reference topology unexpectedly has no non-edge cell pair.");
    }

    private static StrategicCellId[] FindNonVertexTriple()
    {
        for (var first = 0;
             first < CoarseTopology.Cells.Count;
             first++)
        {
            for (var second = first + 1;
                 second < CoarseTopology.Cells.Count;
                 second++)
            {
                for (var third = second + 1;
                     third < CoarseTopology.Cells.Count;
                     third++)
                {
                    var candidate =
                        new[]
                        {
                            CoarseTopology.Cells[first].Id,
                            CoarseTopology.Cells[second].Id,
                            CoarseTopology.Cells[third].Id
                        }
                        .OrderBy(id => id.Value)
                        .ToArray();

                    if (!CoarseTopology.Vertices.Any(
                        vertex =>
                            vertex.IncidentCellIds.SequenceEqual(
                                candidate)))
                    {
                        return candidate;
                    }
                }
            }
        }

        throw new InvalidOperationException(
            "Reference topology unexpectedly has no non-vertex cell triple.");
    }
}