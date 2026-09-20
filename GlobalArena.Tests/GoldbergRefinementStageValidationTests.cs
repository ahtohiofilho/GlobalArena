using GlobalArena.World;
using System.Security.Cryptography;
using System.Text;

namespace GlobalArena.Tests;

public sealed class GoldbergRefinementStageValidationTests
{
    private static readonly PairSpec[] RepresentativePairs =
    {
        new("ClassIBaseScale2", 1, 0, 2, 0, 2),
        new("ClassIGeneralScale2", 2, 0, 4, 0, 2),
        new("ClassIInvertedScale2", 0, 2, 0, 4, 2),
        new("ClassIScale3", 1, 0, 3, 0, 3),
        new("ClassIIBaseScale2", 1, 1, 2, 2, 2),
        new("ClassIIGeneralScale2", 2, 2, 4, 4, 2),
        new("ClassIIScale3", 1, 1, 3, 3, 3),
        new("ClassIIIRightScale2", 2, 1, 4, 2, 2),
        new("ClassIIILeftScale2", 1, 2, 2, 4, 2),
        new("ClassIIIRightScale3", 2, 1, 6, 3, 3),
        new("ClassIIILeftScale3", 1, 2, 3, 6, 3)
    };

    [Fact]
    public void AllRepresentativePairsPreservePublicScaleAndCountInvariants()
    {
        foreach (var pair in RepresentativePairs)
        {
            var refinement =
                CreateRefinement(
                    pair);

            Assert.Equal(
                pair.Scale,
                refinement.Scale);

            Assert.Equal(
                checked(
                    pair.CoarseM
                    * pair.Scale),
                refinement.FineParameters.M);

            Assert.Equal(
                checked(
                    pair.CoarseN
                    * pair.Scale),
                refinement.FineParameters.N);

            var coarse =
                GoldbergStrategicTopologyGenerator.Generate(
                    refinement.CoarseParameters);

            var fine =
                GoldbergStrategicTopologyGenerator.Generate(
                    refinement.FineParameters);

            Assert.Equal(
                refinement.CoarseParameters.StrategicCellCount,
                (ulong)coarse.Cells.Count);

            Assert.Equal(
                refinement.CoarseParameters.StrategicEdgeCount,
                (ulong)coarse.Edges.Count);

            Assert.Equal(
                refinement.CoarseParameters.StrategicVertexCount,
                (ulong)coarse.Vertices.Count);

            Assert.Equal(
                refinement.FineParameters.StrategicCellCount,
                (ulong)fine.Cells.Count);

            Assert.Equal(
                refinement.FineParameters.StrategicEdgeCount,
                (ulong)fine.Edges.Count);

            Assert.Equal(
                refinement.FineParameters.StrategicVertexCount,
                (ulong)fine.Vertices.Count);

            var scaleSquared =
                checked(
                    (ulong)pair.Scale
                    * (ulong)pair.Scale);

            Assert.Equal(
                checked(
                    scaleSquared
                    * checked(
                        (ulong)coarse.Cells.Count
                        - 2UL)
                    + 2UL),
                (ulong)fine.Cells.Count);

            Assert.Equal(
                checked(
                    scaleSquared
                    * (ulong)coarse.Edges.Count),
                (ulong)fine.Edges.Count);

            Assert.Equal(
                checked(
                    scaleSquared
                    * (ulong)coarse.Vertices.Count),
                (ulong)fine.Vertices.Count);
        }
    }

    [Fact]
    public void AllRepresentativeTopologiesReproduceCanonicalSignatures()
    {
        foreach (var pair in RepresentativePairs)
        {
            var coarseParameters =
                new GoldbergParameters(
                    pair.CoarseM,
                    pair.CoarseN);

            var fineParameters =
                new GoldbergParameters(
                    pair.FineM,
                    pair.FineN);

            Assert.Equal(
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        coarseParameters)),
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        coarseParameters)));

            Assert.Equal(
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        fineParameters)),
                CreateTopologySignature(
                    GoldbergStrategicTopologyGenerator.Generate(
                        fineParameters)));
        }
    }

    [Fact]
    public void AllRepresentativePairsPreserveEulerAndTwelvePentagons()
    {
        foreach (var pair in RepresentativePairs)
        {
            AssertEulerAndPentagons(
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        pair.CoarseM,
                        pair.CoarseN)));

            AssertEulerAndPentagons(
                GoldbergStrategicTopologyGenerator.Generate(
                    new GoldbergParameters(
                        pair.FineM,
                        pair.FineN)));
        }
    }

    [Fact]
    public void CurrentReferenceMapperAcceptsOnlyTheFrozenReferencePair()
    {
        var supported =
            GoldbergScaledRefinementReferenceMapper.Materialize(
                CreateRefinement(
                    RepresentativePairs[0]));

        Assert.Equal(
            12,
            supported.CellReferences.Count);

        foreach (var pair in RepresentativePairs.Skip(1))
        {
            Assert.Throws<NotSupportedException>(
                () => GoldbergScaledRefinementReferenceMapper.Materialize(
                    CreateRefinement(
                        pair)));
        }
    }

    [Fact]
    public void ReferenceMapContainsTwelveCanonicalSeedReferencesWithUniqueAnchors()
    {
        var map =
            CreateReferenceMap();

        Assert.Equal(
            12,
            map.CellReferences.Count);

        Assert.Equal(
            Enumerable.Range(
                1,
                12),
            map.CellReferences.Select(
                reference =>
                    reference.SeedVertexId.Value));

        Assert.Equal(
            12,
            map.CellReferences
                .Select(reference => reference.CoarseCellId)
                .Distinct()
                .Count());

        Assert.Equal(
            12,
            map.CellReferences
                .Select(reference => reference.FineCellId)
                .Distinct()
                .Count());

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.Refinement.FineParameters);

        Assert.Equal(
            coarse.Cells
                .Select(cell => cell.Id)
                .OrderBy(id => id.Value),
            map.CellReferences
                .Select(reference => reference.CoarseCellId)
                .OrderBy(id => id.Value));

        Assert.Equal(
            fine.Cells
                .Where(
                    cell =>
                        cell.Kind
                        == StrategicCellKind.Pentagon)
                .Select(cell => cell.Id)
                .OrderBy(id => id.Value),
            map.CellReferences
                .Select(reference => reference.FineCellId)
                .OrderBy(id => id.Value));
    }

    [Fact]
    public void RepeatedReferenceMapMaterializationIsDeterministic()
    {
        Assert.Equal(
            CreateReferenceSignature(
                CreateReferenceMap()),
            CreateReferenceSignature(
                CreateReferenceMap()));
    }

    [Fact]
    public void ContinuityMapContainsThirtyReferencesInCanonicalCoarseEdgeOrder()
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

        var fineHexagonIds =
            fine.Cells
                .Where(
                    cell =>
                        cell.Kind
                        == StrategicCellKind.Hexagon)
                .Select(cell => cell.Id)
                .OrderBy(id => id.Value)
                .ToArray();

        Assert.Equal(
            30,
            middleIds.Length);

        Assert.Equal(
            30,
            middleIds.Distinct().Count());

        Assert.Equal(
            fineHexagonIds,
            middleIds);
    }

    [Fact]
    public void ContinuityUsesExactlySixtyGloballyUniqueFineEdges()
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
            fineEdgeIds.Distinct().Count());
    }

    [Fact]
    public void EveryOrderedTwoEdgeChainConnectsExpectedFineAnchorsThroughMiddleCell()
    {
        var map =
            CreateContinuityMap();

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.FineParameters);

        var fineAnchorByCoarseCell =
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

            var firstFineAnchor =
                fineAnchorByCoarseCell[
                    coarseEdge.IncidentCellIds[0]];

            var secondFineAnchor =
                fineAnchorByCoarseCell[
                    coarseEdge.IncidentCellIds[1]];

            var middle =
                reference.MiddleFineCellId;

            Assert.Contains(
                middle,
                fineCellById[firstFineAnchor]
                    .AdjacentCellIds);

            Assert.Contains(
                secondFineAnchor,
                fineCellById[middle]
                    .AdjacentCellIds);

            AssertEdgeConnects(
                fineEdgeById[
                    reference.FineStrategicEdgeIds[0]],
                firstFineAnchor,
                middle);

            AssertEdgeConnects(
                fineEdgeById[
                    reference.FineStrategicEdgeIds[1]],
                middle,
                secondFineAnchor);
        }
    }

    [Fact]
    public void MappedBandsExistAndPreserveCurrentCoverageAndSingleElementSemantics()
    {
        var map =
            CreateContinuityMap();

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                map.CellReferenceMap.Refinement.FineParameters);

        var coarseBands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                coarse);

        var fineBands =
            StrategicEdgeSharedBorderBandMaterializer.Materialize(
                fine);

        var coarseBandById =
            coarseBands.ToDictionary(
                band => band.StrategicEdgeId);

        var fineBandById =
            fineBands.ToDictionary(
                band => band.StrategicEdgeId);

        var mappedFineEdgeIds =
            map.BorderReferences
                .SelectMany(reference => reference.FineStrategicEdgeIds)
                .ToArray();

        Assert.Equal(
            60,
            mappedFineEdgeIds.Length);

        Assert.Equal(
            120,
            fineBands.Count);

        foreach (var reference in map.BorderReferences)
        {
            Assert.True(
                coarseBandById.ContainsKey(
                    reference.CoarseStrategicEdgeId));

            Assert.Single(
                coarseBandById[
                    reference.CoarseStrategicEdgeId]
                    .Elements);

            foreach (var fineEdgeId in reference.FineStrategicEdgeIds)
            {
                Assert.True(
                    fineBandById.ContainsKey(
                        fineEdgeId));

                Assert.Single(
                    fineBandById[
                        fineEdgeId]
                        .Elements);
            }
        }
    }

    [Fact]
    public void RepeatedContinuityMaterializationIsDeterministicAndCollectionsAreReadOnly()
    {
        var first =
            CreateContinuityMap();

        var second =
            CreateContinuityMap();

        Assert.Equal(
            CreateContinuitySignature(
                first),
            CreateContinuitySignature(
                second));

        var borderReferences =
            Assert.IsAssignableFrom<IList<GoldbergScaledSharedBorderReference>>(
                first.BorderReferences);

        Assert.True(
            borderReferences.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () => borderReferences.RemoveAt(
                0));

        var cellReferences =
            Assert.IsAssignableFrom<IList<GoldbergScaledCellReference>>(
                first.CellReferenceMap.CellReferences);

        Assert.True(
            cellReferences.IsReadOnly);

        Assert.Throws<NotSupportedException>(
            () => cellReferences.RemoveAt(
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

    private static GoldbergScaledRefinement CreateRefinement(
        PairSpec pair)
    {
        return new GoldbergScaledRefinement(
            new GoldbergParameters(
                pair.CoarseM,
                pair.CoarseN),
            new GoldbergParameters(
                pair.FineM,
                pair.FineN));
    }

    private static GoldbergScaledRefinementReferenceMap CreateReferenceMap()
    {
        return GoldbergScaledRefinementReferenceMapper.Materialize(
            CreateRefinement(
                RepresentativePairs[0]));
    }

    private static GoldbergScaledSharedBorderContinuityMap CreateContinuityMap()
    {
        return GoldbergScaledSharedBorderContinuityMapper.Materialize(
            CreateReferenceMap());
    }

    private static void AssertEulerAndPentagons(
        StrategicTopology topology)
    {
        Assert.Equal(
            12,
            topology.Cells.Count(
                cell =>
                    cell.Kind
                    == StrategicCellKind.Pentagon));

        Assert.Equal(
            2L,
            checked(
                (long)topology.Cells.Count
                - topology.Edges.Count
                + topology.Vertices.Count));
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

    private static string CreateReferenceSignature(
        GoldbergScaledRefinementReferenceMap map)
    {
        return string.Join(
            "|",
            map.CellReferences.Select(
                reference =>
                    $"{reference.SeedVertexId.Value}:"
                    + $"{reference.CoarseCellId.Value}:"
                    + $"{reference.FineCellId.Value}"));
    }

    private static string CreateContinuitySignature(
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

    private static string CreateTopologySignature(
        StrategicTopology topology)
    {
        var builder =
            new StringBuilder();

        builder.Append(
            topology.Parameters.M);
        builder.Append(':');
        builder.Append(
            topology.Parameters.N);
        builder.Append('|');

        foreach (var cell in topology.Cells)
        {
            builder.Append(
                cell.Id.Value);
            builder.Append(':');
            builder.Append(
                (int)cell.Kind);
            builder.Append(':');

            AppendIds(
                builder,
                cell.AdjacentCellIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                cell.IncidentEdgeIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                cell.IncidentVertexIds.Select(
                    id => id.Value));

            builder.Append('|');
        }

        foreach (var edge in topology.Edges)
        {
            builder.Append(
                edge.Id.Value);
            builder.Append(':');

            AppendIds(
                builder,
                edge.IncidentCellIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                edge.IncidentVertexIds.Select(
                    id => id.Value));

            builder.Append('|');
        }

        foreach (var vertex in topology.Vertices)
        {
            builder.Append(
                vertex.Id.Value);
            builder.Append(':');

            AppendIds(
                builder,
                vertex.IncidentCellIds.Select(
                    id => id.Value));

            builder.Append(':');

            AppendIds(
                builder,
                vertex.IncidentEdgeIds.Select(
                    id => id.Value));

            builder.Append('|');
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    builder.ToString())));
    }

    private static void AppendIds(
        StringBuilder builder,
        IEnumerable<ulong> values)
    {
        foreach (var value in values)
        {
            builder.Append(
                value);
            builder.Append(',');
        }
    }

    private sealed record PairSpec(
        string Name,
        int CoarseM,
        int CoarseN,
        int FineM,
        int FineN,
        int Scale);
}
