using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalIncidenceMapperTests
{
    [Fact]
    public void NullRefinementIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                PhysicalTacticalIncidenceMapper.Materialize(
                    null!));
    }

    [Fact]
    public void ScaleTwoReferencePairIsNotTheM252CMaterializationTarget()
    {
        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(1, 0),
                new GoldbergParameters(2, 0));

        Assert.Throws<NotSupportedException>(
            () =>
                PhysicalTacticalIncidenceMapper.Materialize(
                    refinement));
    }

    [Fact]
    public void ScaleThreeVertexOnlyPairIsNotTheM252CMaterializationTarget()
    {
        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(1, 0),
                new GoldbergParameters(3, 0));

        Assert.Throws<NotSupportedException>(
            () =>
                PhysicalTacticalIncidenceMapper.Materialize(
                    refinement));
    }

    [Fact]
    public void DifferentOfficialClassIScaleSixPairIsAccepted()
    {
        var map =
            PhysicalTacticalIncidenceMapper.Materialize(
                new GoldbergScaledRefinement(
                    new GoldbergParameters(2, 0),
                    new GoldbergParameters(12, 0)));

        Assert.Equal(
            new GoldbergParameters(2, 0),
            map.CoarseTopology.Parameters);

        Assert.Equal(
            new GoldbergParameters(12, 0),
            map.FineTopology.Parameters);

        Assert.Equal(
            1442,
            map.TileIncidences.Count);
    }

    [Fact]
    public void G10ToG60MaterializesAuthoritativeTopologies()
    {
        var map =
            CreateMap();

        Assert.Equal(
            new GoldbergParameters(1, 0),
            map.CoarseTopology.Parameters);

        Assert.Equal(
            new GoldbergParameters(6, 0),
            map.FineTopology.Parameters);

        Assert.Equal(
            6,
            map.Refinement.Scale);
    }

    [Fact]
    public void EveryFineTileIsCoveredExactlyOnce()
    {
        var map =
            CreateMap();

        Assert.Equal(
            362,
            map.TileIncidences.Count);

        Assert.Equal(
            map.FineTopology.Cells.Select(
                cell =>
                    cell.Id),
            map.TileIncidences.Select(
                incidence =>
                    incidence
                        .PhysicalTacticalTileId
                        .FineStrategicCellId));

        Assert.Equal(
            362,
            map.TileIncidences
                .Select(
                    incidence =>
                        incidence.PhysicalTacticalTileId)
                .Distinct()
                .Count());
    }

    [Fact]
    public void MaterializationMatchesAudited3123020Signature()
    {
        var map =
            CreateMap();

        Assert.Equal(
            312,
            CountByIncidenceCardinality(
                map,
                1));

        Assert.Equal(
            30,
            CountByIncidenceCardinality(
                map,
                2));

        Assert.Equal(
            20,
            CountByIncidenceCardinality(
                map,
                3));
    }

    [Fact]
    public void EveryCoarseEdgeHasExactlyOneEdgeSharedPhysicalTile()
    {
        var map =
            CreateMap();

        var actual =
            map.TileIncidences
                .Where(
                    incidence =>
                        incidence.IncidentCoarseCellIds.Count == 2)
                .Select(
                    incidence =>
                        CreateCoarseSignature(
                            incidence.IncidentCoarseCellIds))
                .Order()
                .ToArray();

        var expected =
            map.CoarseTopology.Edges
                .Select(
                    edge =>
                        CreateCoarseSignature(
                            edge.IncidentCellIds))
                .Order()
                .ToArray();

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void EveryCoarseVertexHasExactlyOneVertexSharedPhysicalTile()
    {
        var map =
            CreateMap();

        var actual =
            map.TileIncidences
                .Where(
                    incidence =>
                        incidence.IncidentCoarseCellIds.Count == 3)
                .Select(
                    incidence =>
                        CreateCoarseSignature(
                            incidence.IncidentCoarseCellIds))
                .Order()
                .ToArray();

        var expected =
            map.CoarseTopology.Vertices
                .Select(
                    vertex =>
                        CreateCoarseSignature(
                            vertex.IncidentCellIds))
                .Order()
                .ToArray();

        Assert.Equal(
            expected,
            actual);
    }

    [Fact]
    public void TwelveFinePentagonsRemainInteriorAndCoverTwelveCoarseCells()
    {
        var map =
            CreateMap();

        var incidencesByFineCell =
            map.TileIncidences.ToDictionary(
                incidence =>
                    incidence
                        .PhysicalTacticalTileId
                        .FineStrategicCellId);

        var pentagonIncidences =
            map.FineTopology.Cells
                .Where(
                    cell =>
                        cell.Kind
                        == StrategicCellKind.Pentagon)
                .Select(
                    cell =>
                        incidencesByFineCell[cell.Id])
                .ToArray();

        Assert.Equal(
            12,
            pentagonIncidences.Length);

        Assert.All(
            pentagonIncidences,
            incidence =>
                Assert.Single(
                    incidence.IncidentCoarseCellIds));

        Assert.Equal(
            12,
            pentagonIncidences
                .Select(
                    incidence =>
                        incidence.IncidentCoarseCellIds[0])
                .Distinct()
                .Count());
    }

    [Fact]
    public void EveryPhysicalIdentityUsesFineG60Parameters()
    {
        var map =
            CreateMap();

        Assert.All(
            map.TileIncidences,
            incidence =>
                Assert.Equal(
                    new GoldbergParameters(6, 0),
                    incidence
                        .PhysicalTacticalTileId
                        .FineGoldbergParameters));
    }

    [Fact]
    public void RepeatedMaterializationIsDeterministic()
    {
        var first =
            CreateMap();

        var second =
            CreateMap();

        Assert.Equal(
            CreateSignature(first),
            CreateSignature(second));
    }

    [Fact]
    public void MaterializedIncidenceCollectionIsReadOnly()
    {
        var map =
            CreateMap();

        var mutableView =
            Assert.IsAssignableFrom<IList<PhysicalTacticalTileIncidence>>(
                map.TileIncidences);

        Assert.True(
            mutableView.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () =>
                mutableView.Add(
                    map.TileIncidences[0]));
    }

    private static PhysicalTacticalIncidenceMap CreateMap()
    {
        return PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(
                new GoldbergParameters(1, 0),
                new GoldbergParameters(6, 0)));
    }

    private static int CountByIncidenceCardinality(
        PhysicalTacticalIncidenceMap map,
        int cardinality)
    {
        return map.TileIncidences.Count(
            incidence =>
                incidence.IncidentCoarseCellIds.Count
                == cardinality);
    }

    private static string CreateCoarseSignature(
        IEnumerable<StrategicCellId> cellIds)
    {
        return string.Join(
            ",",
            cellIds
                .OrderBy(
                    id =>
                        id.Value)
                .Select(
                    id =>
                        id.Value));
    }

    private static string CreateSignature(
        PhysicalTacticalIncidenceMap map)
    {
        return string.Join(
            "|",
            map.TileIncidences.Select(
                incidence =>
                    incidence
                        .PhysicalTacticalTileId
                        .FineStrategicCellId
                        .Value
                    + ":"
                    + CreateCoarseSignature(
                        incidence.IncidentCoarseCellIds)));
    }
}