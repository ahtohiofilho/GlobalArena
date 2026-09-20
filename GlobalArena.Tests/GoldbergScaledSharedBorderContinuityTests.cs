using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GoldbergScaledSharedBorderContinuityTests
{
    [Fact]
    public void NullCellReferenceMapIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => GoldbergScaledSharedBorderContinuityMapper.Materialize(
                null!));
    }

    [Fact]
    public void ReferencePairProducesThirtyReferencesInCoarseEdgeOrder()
    {
        var map =
            CreateContinuityMap();

        Assert.Equal(
            30,
            map.BorderReferences.Count);

        Assert.Equal(
            Enumerable.Range(
                1,
                30)
                .Select(value => (ulong)value),
            map.BorderReferences.Select(
                reference =>
                    reference.CoarseStrategicEdgeId.Value));
    }

    [Fact]
    public void EveryCoarseEdgeIsCoveredExactlyOnce()
    {
        var map =
            CreateContinuityMap();

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.CoarseParameters);

        Assert.Equal(
            coarse.Edges
                .Select(edge => edge.Id)
                .OrderBy(id => id.Value),
            map.BorderReferences
                .Select(reference => reference.CoarseStrategicEdgeId)
                .OrderBy(id => id.Value));
    }

    [Fact]
    public void EveryReferenceContainsExactlyTwoFineEdges()
    {
        var map =
            CreateContinuityMap();

        Assert.All(
            map.BorderReferences,
            reference =>
                Assert.Equal(
                    2,
                    reference.FineStrategicEdgeIds.Count));
    }

    [Fact]
    public void AllSixtyMappedFineEdgeIdsAreGloballyUnique()
    {
        var map =
            CreateContinuityMap();

        var fineEdgeIds =
            map.BorderReferences
                .SelectMany(reference => reference.FineStrategicEdgeIds)
                .ToArray();

        Assert.Equal(
            60,
            fineEdgeIds.Length);

        Assert.Equal(
            60,
            fineEdgeIds
                .Distinct()
                .Count());
    }

    [Fact]
    public void AllThirtyMiddleFineCellIdsAreUnique()
    {
        var map =
            CreateContinuityMap();

        Assert.Equal(
            30,
            map.BorderReferences
                .Select(reference => reference.MiddleFineCellId)
                .Distinct()
                .Count());
    }

    [Fact]
    public void MiddleFineCellsAreExactlyTheThirtyFineHexagons()
    {
        var map =
            CreateContinuityMap();

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.FineParameters);

        var middleIds =
            map.BorderReferences
                .Select(reference => reference.MiddleFineCellId)
                .OrderBy(id => id.Value)
                .ToArray();

        var hexagonIds =
            fine.Cells
                .Where(
                    cell =>
                        cell.Kind
                        == StrategicCellKind.Hexagon)
                .Select(cell => cell.Id)
                .OrderBy(id => id.Value)
                .ToArray();

        Assert.Equal(
            hexagonIds,
            middleIds);
    }

    [Fact]
    public void FineAnchorsAreNotDirectlyAdjacent()
    {
        var map =
            CreateContinuityMap();

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.FineParameters);

        var fineAnchorByCoarseCellId =
            map.CellReferenceMap.CellReferences.ToDictionary(
                reference => reference.CoarseCellId,
                reference => reference.FineCellId);

        var fineCellById =
            fine.Cells.ToDictionary(
                cell => cell.Id);

        foreach (var reference in map.BorderReferences)
        {
            var coarseEdge =
                coarse.Edges[
                    checked(
                        (int)reference.CoarseStrategicEdgeId.Value
                        - 1)];

            var firstFineAnchorId =
                fineAnchorByCoarseCellId[
                    coarseEdge.IncidentCellIds[0]];

            var secondFineAnchorId =
                fineAnchorByCoarseCellId[
                    coarseEdge.IncidentCellIds[1]];

            Assert.DoesNotContain(
                secondFineAnchorId,
                fineCellById[firstFineAnchorId]
                    .AdjacentCellIds);
        }
    }

    [Fact]
    public void EveryChainHasUniqueAnchorMiddleAnchorTopology()
    {
        var map =
            CreateContinuityMap();

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.FineParameters);

        var fineAnchorByCoarseCellId =
            map.CellReferenceMap.CellReferences.ToDictionary(
                reference => reference.CoarseCellId,
                reference => reference.FineCellId);

        var fineCellById =
            fine.Cells.ToDictionary(
                cell => cell.Id);

        var fineEdgeById =
            fine.Edges.ToDictionary(
                edge => edge.Id);

        foreach (var reference in map.BorderReferences)
        {
            var coarseEdge =
                coarse.Edges[
                    checked(
                        (int)reference.CoarseStrategicEdgeId.Value
                        - 1)];

            var firstFineAnchorId =
                fineAnchorByCoarseCellId[
                    coarseEdge.IncidentCellIds[0]];

            var secondFineAnchorId =
                fineAnchorByCoarseCellId[
                    coarseEdge.IncidentCellIds[1]];

            var commonNeighbors =
                fineCellById[firstFineAnchorId]
                    .AdjacentCellIds
                    .Intersect(
                        fineCellById[secondFineAnchorId]
                            .AdjacentCellIds)
                    .ToArray();

            var middle =
                Assert.Single(
                    commonNeighbors);

            Assert.Equal(
                reference.MiddleFineCellId,
                middle);

            AssertEdgeConnects(
                fineEdgeById[reference.FineStrategicEdgeIds[0]],
                firstFineAnchorId,
                middle);

            AssertEdgeConnects(
                fineEdgeById[reference.FineStrategicEdgeIds[1]],
                middle,
                secondFineAnchorId);
        }
    }

    [Fact]
    public void EveryMappedEdgeResolvesToCurrentSharedBorderBand()
    {
        var map =
            CreateContinuityMap();

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.FineParameters);

        var coarseBandIds =
            StrategicEdgeSharedBorderBandMaterializer
                .Materialize(
                    coarse)
                .Select(band => band.StrategicEdgeId)
                .ToHashSet();

        var fineBandIds =
            StrategicEdgeSharedBorderBandMaterializer
                .Materialize(
                    fine)
                .Select(band => band.StrategicEdgeId)
                .ToHashSet();

        Assert.All(
            map.BorderReferences,
            reference =>
            {
                Assert.Contains(
                    reference.CoarseStrategicEdgeId,
                    coarseBandIds);

                Assert.All(
                    reference.FineStrategicEdgeIds,
                    fineEdgeId =>
                        Assert.Contains(
                            fineEdgeId,
                            fineBandIds));
            });
    }

    [Fact]
    public void MappedBandsPreserveCurrentSingleElementSemantics()
    {
        var map =
            CreateContinuityMap();

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.FineParameters);

        var coarseBandById =
            StrategicEdgeSharedBorderBandMaterializer
                .Materialize(
                    coarse)
                .ToDictionary(
                    band => band.StrategicEdgeId);

        var fineBandById =
            StrategicEdgeSharedBorderBandMaterializer
                .Materialize(
                    fine)
                .ToDictionary(
                    band => band.StrategicEdgeId);

        foreach (var reference in map.BorderReferences)
        {
            Assert.Single(
                coarseBandById[
                    reference.CoarseStrategicEdgeId]
                    .Elements);

            Assert.All(
                reference.FineStrategicEdgeIds,
                fineEdgeId =>
                    Assert.Single(
                        fineBandById[
                            fineEdgeId]
                            .Elements));
        }
    }

    [Fact]
    public void CanonicalContinuityVectorMatchesFrozenOracle()
    {
        var map =
            CreateContinuityMap();

        var expected =
            new[]
            {
                "1:1:3,6",
                "2:2:8,11",
                "3:3:13,16",
                "4:4:18,21",
                "5:5:22,25",
                "6:7:27,29",
                "7:8:31,33",
                "8:9:35,38",
                "9:10:39,42",
                "10:12:44,46",
                "11:13:48,50",
                "12:14:51,54",
                "13:16:56,58",
                "14:17:60,62",
                "15:18:63,66",
                "16:20:68,70",
                "17:21:72,74",
                "18:22:75,78",
                "19:24:80,82",
                "20:25:83,85",
                "21:27:87,89",
                "22:28:91,93",
                "23:29:94,97",
                "24:31:99,101",
                "25:32:102,104",
                "26:34:106,108",
                "27:35:109,111",
                "28:37:113,115",
                "29:38:116,118",
                "30:40:119,120"
            };

        Assert.Equal(
            expected,
            map.BorderReferences.Select(
                reference =>
                    $"{reference.CoarseStrategicEdgeId.Value}:"
                    + $"{reference.MiddleFineCellId.Value}:"
                    + $"{reference.FineStrategicEdgeIds[0].Value},"
                    + $"{reference.FineStrategicEdgeIds[1].Value}"));
    }

    [Fact]
    public void RepeatedMaterializationIsDeterministicAndCollectionsAreReadOnly()
    {
        var first =
            CreateContinuityMap();

        var second =
            CreateContinuityMap();

        Assert.Equal(
            CreateSignature(
                first),
            CreateSignature(
                second));

        var borderReferences =
            Assert.IsAssignableFrom<IList<GoldbergScaledSharedBorderReference>>(
                first.BorderReferences);

        Assert.True(
            borderReferences.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () => borderReferences.RemoveAt(
                0));

        var fineEdgeIds =
            Assert.IsAssignableFrom<IList<StrategicEdgeId>>(
                first.BorderReferences[0]
                    .FineStrategicEdgeIds);

        Assert.True(
            fineEdgeIds.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () => fineEdgeIds.RemoveAt(
                0));
    }

    private static GoldbergScaledSharedBorderContinuityMap CreateContinuityMap()
    {
        return GoldbergScaledSharedBorderContinuityMapper.Materialize(
            CreateCellReferenceMap());
    }

    private static GoldbergScaledRefinementReferenceMap CreateCellReferenceMap()
    {
        var refinement =
            new GoldbergScaledRefinement(
                new GoldbergParameters(
                    1,
                    0),
                new GoldbergParameters(
                    2,
                    0));

        return GoldbergScaledRefinementReferenceMapper.Materialize(
            refinement);
    }

    private static void AssertEdgeConnects(
        StrategicEdge edge,
        StrategicCellId firstCellId,
        StrategicCellId secondCellId)
    {
        Assert.Equal(
            new[]
            {
                firstCellId,
                secondCellId
            }
            .OrderBy(id => id.Value),
            edge.IncidentCellIds);
    }

    private static string CreateSignature(
        GoldbergScaledSharedBorderContinuityMap map)
    {
        return string.Join(
            "|",
            map.BorderReferences.Select(
                reference =>
                    $"{reference.CoarseStrategicEdgeId.Value}:"
                    + $"{reference.MiddleFineCellId.Value}:"
                    + $"{reference.FineStrategicEdgeIds[0].Value}:"
                    + $"{reference.FineStrategicEdgeIds[1].Value}"));
    }
}
