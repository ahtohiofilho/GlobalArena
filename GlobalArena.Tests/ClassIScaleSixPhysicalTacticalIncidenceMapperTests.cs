using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class ClassIScaleSixPhysicalTacticalIncidenceMapperTests
{
[Fact]
public void InvertedUnitClassIScaleSixMaterializes()
{
    var map =
        CreateMap(
            new GoldbergParameters(0, 1),
            new GoldbergParameters(0, 6));

    Assert.Equal(
        362,
        map.TileIncidences.Count);

    Assert.Equal(
        312,
        CountByCardinality(
            map,
            1));

    Assert.Equal(
        30,
        CountByCardinality(
            map,
            2));

    Assert.Equal(
        20,
        CountByCardinality(
            map,
            3));
}

[Fact]
public void G20ToG120MatchesGeneralizedSignature()
{
    var map =
        CreateMap(
            new GoldbergParameters(2, 0),
            new GoldbergParameters(12, 0));

    Assert.Equal(
        1442,
        map.TileIncidences.Count);

    Assert.Equal(
        1242,
        CountByCardinality(
            map,
            1));

    Assert.Equal(
        120,
        CountByCardinality(
            map,
            2));

    Assert.Equal(
        80,
        CountByCardinality(
            map,
            3));
}

[Fact]
public void G02ToG012MatchesGeneralizedSignature()
{
    var map =
        CreateMap(
            new GoldbergParameters(0, 2),
            new GoldbergParameters(0, 12));

    Assert.Equal(
        1442,
        map.TileIncidences.Count);

    Assert.Equal(
        1242,
        CountByCardinality(
            map,
            1));

    Assert.Equal(
        120,
        CountByCardinality(
            map,
            2));

    Assert.Equal(
        80,
        CountByCardinality(
            map,
            3));
}

[Fact]
public void G30ToG180MatchesGeneralizedSignature()
{
    var map =
        CreateMap(
            new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0));

    Assert.Equal(
        3242,
        map.TileIncidences.Count);

    Assert.Equal(
        2792,
        CountByCardinality(
            map,
            1));

    Assert.Equal(
        270,
        CountByCardinality(
            map,
            2));

    Assert.Equal(
        180,
        CountByCardinality(
            map,
            3));
}

[Fact]
public void G20EveryCoarseEdgeHasExactlyOneEdgeSharedTile()
{
    var map =
        CreateG20Map();

    var observed =
        map.TileIncidences
            .Where(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 2)
            .Select(
                incidence =>
                    CreateSignature(
                        incidence.IncidentCoarseCellIds))
            .Order()
            .ToArray();

    var expected =
        map.CoarseTopology.Edges
            .Select(
                edge =>
                    CreateSignature(
                        edge.IncidentCellIds))
            .Order()
            .ToArray();

    Assert.Equal(
        expected,
        observed);
}

[Fact]
public void G20EveryCoarseVertexHasExactlyOneVertexSharedTile()
{
    var map =
        CreateG20Map();

    var observed =
        map.TileIncidences
            .Where(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 3)
            .Select(
                incidence =>
                    CreateSignature(
                        incidence.IncidentCoarseCellIds))
            .Order()
            .ToArray();

    var expected =
        map.CoarseTopology.Vertices
            .Select(
                vertex =>
                    CreateSignature(
                        vertex.IncidentCellIds))
            .Order()
            .ToArray();

    Assert.Equal(
        expected,
        observed);
}

[Fact]
public void G20EveryFineTileIsCoveredExactlyOnce()
{
    var map =
        CreateG20Map();

    Assert.Equal(
        map.FineTopology.Cells.Count,
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
        map.TileIncidences.Count,
        map.TileIncidences
            .Select(
                incidence =>
                    incidence.PhysicalTacticalTileId)
            .Distinct()
            .Count());
}

[Fact]
public void G20RepeatedMaterializationIsDeterministic()
{
    var first =
        CreateG20Map();

    var second =
        CreateG20Map();

    Assert.Equal(
        CreateMapSignature(
            first),
        CreateMapSignature(
            second));
}

[Fact]
public void ClassIWrongScaleIsRejected()
{
    var refinement =
        new GoldbergScaledRefinement(
            new GoldbergParameters(2, 0),
            new GoldbergParameters(6, 0));

    Assert.Equal(
        3,
        refinement.Scale);

    Assert.Throws<NotSupportedException>(
        () =>
            PhysicalTacticalIncidenceMapper.Materialize(
                refinement));
}

[Fact]
public void ClassIIRemainsOutsidePhysicalHierarchyScope()
{
    var refinement =
        new GoldbergScaledRefinement(
            new GoldbergParameters(1, 1),
            new GoldbergParameters(6, 6));

    Assert.Throws<NotSupportedException>(
        () =>
            PhysicalTacticalIncidenceMapper.Materialize(
                refinement));
}

[Fact]
public void ClassIIIRemainsOutsidePhysicalHierarchyScope()
{
    var refinement =
        new GoldbergScaledRefinement(
            new GoldbergParameters(2, 1),
            new GoldbergParameters(12, 6));

    Assert.Throws<NotSupportedException>(
        () =>
            PhysicalTacticalIncidenceMapper.Materialize(
                refinement));
}

[Fact]
public void G20PathfinderTraversesAcrossDistinctExclusiveOwners()
{
    var map =
        CreateG20Map();

    var exclusive =
        map.TileIncidences
            .Where(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 1)
            .GroupBy(
                incidence =>
                    incidence.IncidentCoarseCellIds[0])
            .OrderBy(
                group =>
                    group.Key.Value)
            .ToArray();

    Assert.True(
        exclusive.Length > 1);

    var start =
        exclusive[0]
            .First()
            .PhysicalTacticalTileId;

    var end =
        exclusive[^1]
            .First()
            .PhysicalTacticalTileId;

    var path =
        PhysicalTacticalPathfinder.FindShortestPath(
            map,
            start,
            end);

    Assert.True(
        path.StepCount > 0);

    Assert.Equal(
        start,
        path.Start);

    Assert.Equal(
        end,
        path.End);

    for (var index = 1;
         index < path.TileIds.Count;
         index++)
    {
        var previousCell =
            map.FineTopology.Cells[
                checked(
                    (int)path.TileIds[index - 1]
                        .FineStrategicCellId
                        .Value
                    - 1)];

        Assert.Contains(
            path.TileIds[index]
                .FineStrategicCellId,
            previousCell.AdjacentCellIds);
    }
}

private static PhysicalTacticalIncidenceMap CreateG20Map()
{
    return CreateMap(
        new GoldbergParameters(2, 0),
        new GoldbergParameters(12, 0));
}

private static PhysicalTacticalIncidenceMap CreateMap(
    GoldbergParameters coarse,
    GoldbergParameters fine)
{
    return PhysicalTacticalIncidenceMapper.Materialize(
        new GoldbergScaledRefinement(
            coarse,
            fine));
}

private static int CountByCardinality(
    PhysicalTacticalIncidenceMap map,
    int cardinality)
{
    return map.TileIncidences.Count(
        incidence =>
            incidence.IncidentCoarseCellIds.Count
            == cardinality);
}

private static string CreateSignature(
    IEnumerable<StrategicCellId> ids)
{
    return string.Join(
        ",",
        ids
            .OrderBy(
                id =>
                    id.Value)
            .Select(
                id =>
                    id.Value));
}

private static string CreateMapSignature(
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
                + CreateSignature(
                    incidence.IncidentCoarseCellIds)));
}
}