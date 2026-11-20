using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class PhysicalTacticalIncidenceMapperTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void CandidateScaleUsesActualLineageWithoutCertifyingProduction(int scale)
    {
        // R106: research does not weaken the materialization gate.
        var refinement = new GoldbergScaledRefinement(
            new GoldbergParameters(1, 0),
            new GoldbergParameters(scale, 0));
        var first = PhysicalTacticalIncidenceMapper.DiagnoseCandidate(refinement);
        var replay = PhysicalTacticalIncidenceMapper.DiagnoseCandidate(refinement);
        Assert.Equal(first, replay);
        Assert.Equal(12, first.CoarseCells);
        Assert.Equal(10 * scale * scale + 2, first.FineCells);
        Assert.Equal(first.FineCells, first.CoveredTiles);
        Assert.Equal(0, first.UnsupportedOwnerCountTiles);
        Assert.Equal(30, first.ExpectedCoarseEdges);
        Assert.Equal(20, first.ExpectedCoarseVertices);
        Assert.InRange(first.MatchedCoarseEdgeSignatures, 0,
            first.ExpectedCoarseEdges);
        Assert.InRange(first.MatchedCoarseVertexSignatures, 0,
            first.ExpectedCoarseVertices);
        if (scale != 6)
            Assert.Throws<NotSupportedException>(() =>
                PhysicalTacticalIncidenceMapper.Materialize(refinement));
        else
        {
            var authorized = PhysicalTacticalIncidenceMapper.Materialize(refinement);
            Assert.Equal(authorized.TileIncidences.Count, first.FineCells);
            Assert.Equal(312, first.InteriorTiles);
            Assert.Equal(30, first.EdgeSharedTiles);
            Assert.Equal(20, first.VertexSharedTiles);
            Assert.True(first.EveryCoarseEdgeSignatureRepresented);
            Assert.True(first.EveryCoarseVertexSignatureRepresented);
        }
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    [InlineData(1, 5)]
    [InlineData(1, 6)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(2, 4)]
    [InlineData(2, 5)]
    [InlineData(2, 6)]
    public void ResearchConnectivityAuditReadsSameFineAdjacency(
        int coarseFrequency, int scale)
    {
        var coarse = new GoldbergParameters(coarseFrequency, 0);
        var fine = new GoldbergParameters(
            checked(coarseFrequency * scale), 0);
        var refinement = new GoldbergScaledRefinement(coarse, fine);
        var first = PhysicalTacticalIncidenceMapper
            .DiagnoseConnectivity(refinement);
        var replay = PhysicalTacticalIncidenceMapper
            .DiagnoseConnectivity(refinement);
        Assert.Equal(first, replay);
        Assert.Equal((int)coarse.StrategicCellCount, first.CoarseRegions);
        Assert.Equal((int)coarse.StrategicEdgeCount, first.CoarseEdges);
        Assert.InRange(first.EmptyOwnerRegions, 0, first.CoarseRegions);
        Assert.InRange(first.DisconnectedOwnerRegions, 0, first.CoarseRegions);
        Assert.InRange(first.CoarseEdgesWithoutFineContact, 0, first.CoarseEdges);
        Assert.InRange(first.MinimumFineContactsPerCoarseEdge,
            0, first.MaximumFineContactsPerCoarseEdge);
        // Only the accepted scale six is entitled to structural guarantees.
        // Other results remain evidence, not permission to use Materialize.
        if (scale == 6)
        {
            Assert.True(first.RegionConnectivityVerified);
            Assert.True(first.CoarseAdjacencyRepresented);
        }
        else
            Assert.Throws<NotSupportedException>(() =>
                PhysicalTacticalIncidenceMapper.Materialize(refinement));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    [InlineData(1, 5)]
    [InlineData(1, 6)]
    [InlineData(2, 6)]
    public void CandidateBoundaryCorridorsAreAuditedWithoutScalePromotion(
        int coarseFrequency, int scale)
    {
        var coarse = new GoldbergParameters(coarseFrequency, 0);
        var fine = new GoldbergParameters(
            checked(coarseFrequency * scale), 0);
        var refinement = new GoldbergScaledRefinement(coarse, fine);
        var first = PhysicalTacticalIncidenceMapper
            .DiagnoseBoundaryPaths(refinement);
        var replay = PhysicalTacticalIncidenceMapper
            .DiagnoseBoundaryPaths(refinement);
        Assert.Equal(first, replay);
        Assert.Equal(coarse.StrategicEdgeCount,
            (ulong)first.StrategicEdgeCount);
        Assert.Equal(first.StrategicEdgeCount,
            first.CompleteEdgeCorridors +
            first.MissingAnchorEdges +
            first.AmbiguousAnchorEdges +
            first.DisconnectedEdgeCorridors +
            first.RepeatedTileCorridors);
        Assert.InRange(first.MinimumCompletedPathCells,
            0, first.MaximumCompletedPathCells);
        if (scale != 6)
        {
            Assert.Throws<NotSupportedException>(() =>
                PhysicalTacticalIncidenceMapper.Materialize(refinement));
        }
        else
        {
            Assert.True(first.EveryStrategicEdgeHasAUniqueAuditedCorridor);
            Assert.Equal(5, first.MinimumCompletedPathCells);
            Assert.Equal(5, first.MaximumCompletedPathCells);
        }
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(1, 3)]
    [InlineData(1, 4)]
    [InlineData(1, 5)]
    [InlineData(1, 6)]
    [InlineData(2, 2)]
    [InlineData(2, 3)]
    [InlineData(2, 4)]
    [InlineData(2, 5)]
    [InlineData(2, 6)]
    public void CandidatePolygonSidesUseActualFineVerticesAndRemainQaOnly(
        int coarseFrequency, int scale)
    {
        var coarse = new GoldbergParameters(coarseFrequency, 0);
        var fine = new GoldbergParameters(
            checked(coarseFrequency * scale), 0);
        var refinement = new GoldbergScaledRefinement(coarse, fine);
        var first = PhysicalTacticalIncidenceMapper
            .DiagnosePolygonBoundaries(refinement);
        var replay = PhysicalTacticalIncidenceMapper
            .DiagnosePolygonBoundaries(refinement);
        Assert.Equal(first, replay);
        Assert.Equal(coarse, first.CoarseParameters);
        Assert.Equal(fine, first.FineParameters);
        Assert.Equal((int)fine.StrategicEdgeCount, first.FinePolygonEdges);
        Assert.Equal((int)coarse.StrategicEdgeCount,
            first.ExpectedStrategicPairs);
        Assert.Equal(0, first.SegmentsWithInvalidVertexIdentity);
        Assert.InRange(first.PolygonBoundaryEdges, 0, first.FinePolygonEdges);
        Assert.InRange(first.RepresentedStrategicPairs,
            0, first.ExpectedStrategicPairs);
        Console.WriteLine(
            $"GA_NEXT_R109_POLYGON_AUDIT " +
            $"coarse=G({coarse.M},0) fine=G({fine.M},0) scale={scale} " +
            $"sides={first.PolygonBoundaryEdges} " +
            $"shared_tiles={first.SharedOwnerTiles} " +
            $"missing={first.MissingStrategicPairs} " +
            $"nonneighbors={first.UnexpectedNonNeighborPairs} " +
            $"branching={first.BranchingStrategicPairPaths} " +
            $"disconnected={first.DisconnectedStrategicPairPaths} " +
            $"closed={first.ClosedStrategicPairCycles}");
        // Even a perfectly connected polygon edge overlay is NOT approval
        // for physical movement, economic trades or other refinements.
        if (scale != 6)
            Assert.Throws<NotSupportedException>(() =>
                PhysicalTacticalIncidenceMapper.Materialize(refinement));
        else
            Assert.Equal((int)fine.StrategicCellCount,
                PhysicalTacticalIncidenceMapper.Materialize(refinement)
                    .TileIncidences.Count);
    }

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