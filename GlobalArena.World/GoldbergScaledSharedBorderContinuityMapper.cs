namespace GlobalArena.World;

public static class GoldbergScaledSharedBorderContinuityMapper
{
    public static GoldbergScaledSharedBorderContinuityMap Materialize(
        GoldbergScaledRefinementReferenceMap cellReferenceMap)
    {
        ArgumentNullException.ThrowIfNull(
            cellReferenceMap);

        if (!IsReferencePair(
            cellReferenceMap.Refinement))
        {
            throw new NotSupportedException(
                "M2.4.3 supports shared-border continuity only for G(1,0) to G(2,0) with scale 2.");
        }

        var coarse =
            GoldbergStrategicTopologyGenerator.Generate(
                cellReferenceMap.Refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator.Generate(
                cellReferenceMap.Refinement.FineParameters);

        var fineAnchorByCoarseCellId =
            cellReferenceMap.CellReferences.ToDictionary(
                reference => reference.CoarseCellId,
                reference => reference.FineCellId);

        var fineCellsById =
            fine.Cells.ToDictionary(
                cell => cell.Id);

        var fineEdgesByPair =
            fine.Edges.ToDictionary(
                edge =>
                    CreateCanonicalCellPair(
                        edge.IncidentCellIds[0],
                        edge.IncidentCellIds[1]));

        var coarseBandsByEdgeId =
            StrategicEdgeSharedBorderBandMaterializer
                .Materialize(
                    coarse)
                .ToDictionary(
                    band => band.StrategicEdgeId);

        var fineBandsByEdgeId =
            StrategicEdgeSharedBorderBandMaterializer
                .Materialize(
                    fine)
                .ToDictionary(
                    band => band.StrategicEdgeId);

        var references =
            new GoldbergScaledSharedBorderReference[
                coarse.Edges.Count];

        for (var index = 0;
             index < coarse.Edges.Count;
             index++)
        {
            var coarseEdge =
                coarse.Edges[index];

            if (!coarseBandsByEdgeId.ContainsKey(
                coarseEdge.Id))
            {
                throw new InvalidOperationException(
                    "A coarse strategic edge does not resolve to its shared-border band.");
            }

            if (
                !fineAnchorByCoarseCellId.TryGetValue(
                    coarseEdge.IncidentCellIds[0],
                    out var firstFineAnchorId)
                || !fineAnchorByCoarseCellId.TryGetValue(
                    coarseEdge.IncidentCellIds[1],
                    out var secondFineAnchorId))
            {
                throw new InvalidOperationException(
                    "A coarse strategic edge incident cell is missing its fine anchor provenance.");
            }

            var firstFineAnchor =
                fineCellsById[firstFineAnchorId];

            var secondFineAnchor =
                fineCellsById[secondFineAnchorId];

            if (
                firstFineAnchor.AdjacentCellIds.Contains(
                    secondFineAnchorId))
            {
                throw new InvalidOperationException(
                    "Reference-pair fine anchors must not be directly adjacent.");
            }

            var commonNeighbors =
                firstFineAnchor.AdjacentCellIds
                    .Intersect(
                        secondFineAnchor.AdjacentCellIds)
                    .ToArray();

            if (commonNeighbors.Length != 1)
            {
                throw new InvalidOperationException(
                    "Each coarse edge must resolve to exactly one middle fine cell.");
            }

            var middleFineCellId =
                commonNeighbors[0];

            var middleFineCell =
                fineCellsById[middleFineCellId];

            if (
                middleFineCell.Kind
                != StrategicCellKind.Hexagon)
            {
                throw new InvalidOperationException(
                    "The middle fine cell of a reference-pair border chain must be a hexagon.");
            }

            var firstFineEdge =
                GetFineEdge(
                    fineEdgesByPair,
                    firstFineAnchorId,
                    middleFineCellId);

            var secondFineEdge =
                GetFineEdge(
                    fineEdgesByPair,
                    middleFineCellId,
                    secondFineAnchorId);

            if (
                !fineBandsByEdgeId.ContainsKey(
                    firstFineEdge.Id)
                || !fineBandsByEdgeId.ContainsKey(
                    secondFineEdge.Id))
            {
                throw new InvalidOperationException(
                    "A mapped fine strategic edge does not resolve to its shared-border band.");
            }

            references[index] =
                new GoldbergScaledSharedBorderReference(
                    coarseEdge.Id,
                    middleFineCellId,
                    new[]
                    {
                        firstFineEdge.Id,
                        secondFineEdge.Id
                    });
        }

        return new GoldbergScaledSharedBorderContinuityMap(
            cellReferenceMap,
            references);
    }

    private static StrategicEdge GetFineEdge(
        IReadOnlyDictionary<(ulong First, ulong Second), StrategicEdge> fineEdgesByPair,
        StrategicCellId firstCellId,
        StrategicCellId secondCellId)
    {
        var pair =
            CreateCanonicalCellPair(
                firstCellId,
                secondCellId);

        if (!fineEdgesByPair.TryGetValue(
            pair,
            out var edge))
        {
            throw new InvalidOperationException(
                "A required fine strategic edge is missing from the continuity chain.");
        }

        return edge;
    }

    private static (ulong First, ulong Second) CreateCanonicalCellPair(
        StrategicCellId firstCellId,
        StrategicCellId secondCellId)
    {
        return firstCellId.Value < secondCellId.Value
            ? (
                firstCellId.Value,
                secondCellId.Value)
            : (
                secondCellId.Value,
                firstCellId.Value);
    }

    private static bool IsReferencePair(
        GoldbergScaledRefinement refinement)
    {
        return refinement.Scale == 2
            && refinement.CoarseParameters.M == 1
            && refinement.CoarseParameters.N == 0
            && refinement.FineParameters.M == 2
            && refinement.FineParameters.N == 0;
    }
}
