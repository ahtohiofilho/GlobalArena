namespace GlobalArena.World;

public static class PhysicalTacticalIncidenceMapper
{
public static PhysicalTacticalIncidenceMap Materialize(
    GoldbergScaledRefinement refinement)
{
    ArgumentNullException.ThrowIfNull(refinement);

    if (!IsOfficialClassIScaleSix(
        refinement))
    {
        throw new NotSupportedException(
            "M2.5.3 physical incidence materialization supports only official Class I scale-6 refinements: G(k,0) to G(6k,0) or G(0,k) to G(0,6k).");
    }

    var lineage =
        GoldbergStrategicTopologyGenerator
            .GenerateClassIScaledRefinementLineage(
                refinement);

    var ownerSets =
        BuildNearestOwnerPartition(
            lineage.CoarseTopology,
            lineage.FineTopology,
            lineage.FineAnchorByCoarseCellId);

    var incidences =
        new PhysicalTacticalTileIncidence[
            lineage.FineTopology.Cells.Count];

    for (var index = 0;
         index < lineage.FineTopology.Cells.Count;
         index++)
    {
        var fineCellId =
            lineage.FineTopology.Cells[index].Id;

        var coarseCellIds =
            ownerSets[index]
                .OrderBy(
                    id =>
                        id.Value)
                .ToArray();

        incidences[index] =
            new PhysicalTacticalTileIncidence(
                new PhysicalTacticalTileId(
                    refinement.FineParameters,
                    fineCellId),
                lineage.CoarseTopology,
                coarseCellIds);
    }

    ValidateGeneralizedSignature(
        refinement,
        lineage.CoarseTopology,
        incidences);

    return new PhysicalTacticalIncidenceMap(
        refinement,
        lineage.CoarseTopology,
        lineage.FineTopology,
        incidences);
}

private static IReadOnlyCollection<StrategicCellId>[] BuildNearestOwnerPartition(
    StrategicTopology coarseTopology,
    StrategicTopology fineTopology,
    IReadOnlyDictionary<StrategicCellId, StrategicCellId>
        fineAnchorByCoarseCellId)
{
    if (fineAnchorByCoarseCellId.Count
        != coarseTopology.Cells.Count)
    {
        throw new InvalidOperationException(
            "Class I lineage must provide one fine anchor for every coarse strategic cell.");
    }

    var distances =
        Enumerable
            .Repeat(
                -1,
                fineTopology.Cells.Count)
            .ToArray();

    var owners =
        new HashSet<StrategicCellId>?[
            fineTopology.Cells.Count];

    var queue =
        new Queue<int>();

    foreach (var pair in
        fineAnchorByCoarseCellId
            .OrderBy(
                pair =>
                    pair.Key.Value))
    {
        var fineIndex =
            checked(
                (int)pair.Value.Value - 1);

        if (fineIndex < 0
            || fineIndex >= fineTopology.Cells.Count)
        {
            throw new InvalidOperationException(
                "Class I lineage referenced a fine anchor outside the authoritative fine topology.");
        }

        if (distances[fineIndex] != -1)
        {
            throw new InvalidOperationException(
                "Class I lineage fine anchors must be distinct.");
        }

        distances[fineIndex] =
            0;

        owners[fineIndex] =
            new HashSet<StrategicCellId>
            {
                pair.Key
            };

        queue.Enqueue(
            fineIndex);
    }

    while (queue.Count > 0)
    {
        var currentIndex =
            queue.Dequeue();

        var currentOwners =
            owners[currentIndex]
            ?? throw new InvalidOperationException(
                "Visited physical tile must have coarse ownership state.");

        var nextDistance =
            checked(
                distances[currentIndex] + 1);

        foreach (var neighborId in
            fineTopology.Cells[currentIndex]
                .AdjacentCellIds)
        {
            var neighborIndex =
                checked(
                    (int)neighborId.Value - 1);

            if (distances[neighborIndex] < 0)
            {
                distances[neighborIndex] =
                    nextDistance;

                owners[neighborIndex] =
                    new HashSet<StrategicCellId>(
                        currentOwners);

                queue.Enqueue(
                    neighborIndex);

                continue;
            }

            if (distances[neighborIndex]
                != nextDistance)
            {
                continue;
            }

            var neighborOwners =
                owners[neighborIndex]
                ?? throw new InvalidOperationException(
                    "Reached physical tile must have coarse ownership state.");

            var changed =
                false;

            foreach (var owner in currentOwners)
            {
                if (neighborOwners.Add(
                    owner))
                {
                    changed =
                        true;
                }
            }

            if (neighborOwners.Count > 3)
            {
                throw new InvalidOperationException(
                    "A physical tactical tile cannot be incident to more than three coarse strategic cells.");
            }

            if (changed)
            {
                queue.Enqueue(
                    neighborIndex);
            }
        }
    }

    if (distances.Any(
        distance =>
            distance < 0)
        || owners.Any(
            owner =>
                owner is null))
    {
        throw new InvalidOperationException(
            "Class I physical ownership partition must cover every fine topology cell.");
    }

    foreach (var pair in fineAnchorByCoarseCellId)
    {
        var fineIndex =
            checked(
                (int)pair.Value.Value - 1);

        var anchorOwners =
            owners[fineIndex]
            ?? throw new InvalidOperationException(
                "Class I fine anchor must have ownership state.");

        if (distances[fineIndex] != 0
            || anchorOwners.Count != 1
            || !anchorOwners.Contains(
                pair.Key))
        {
            throw new InvalidOperationException(
                "Every Class I fine anchor must remain exclusively owned by its corresponding coarse strategic cell.");
        }
    }

    return owners
        .Select(
            owner =>
                (IReadOnlyCollection<StrategicCellId>)(
                    owner
                    ?? throw new InvalidOperationException(
                        "Physical ownership state cannot be null.")))
        .ToArray();
}

private static void ValidateGeneralizedSignature(
    GoldbergScaledRefinement refinement,
    StrategicTopology coarseTopology,
    IReadOnlyCollection<PhysicalTacticalTileIncidence> incidences)
{
    var interior =
        incidences.Count(
            incidence =>
                incidence.IncidentCoarseCellIds.Count == 1);

    var edgeShared =
        incidences.Count(
            incidence =>
                incidence.IncidentCoarseCellIds.Count == 2);

    var vertexShared =
        incidences.Count(
            incidence =>
                incidence.IncidentCoarseCellIds.Count == 3);

    var coarseTriangulationNumber =
        refinement
            .CoarseParameters
            .TriangulationNumber;

    var expectedInterior =
        checked(
            310UL * coarseTriangulationNumber
            + 2UL);

    var expectedEdgeShared =
        checked(
            30UL * coarseTriangulationNumber);

    var expectedVertexShared =
        checked(
            20UL * coarseTriangulationNumber);

    var expectedFineCount =
        checked(
            360UL * coarseTriangulationNumber
            + 2UL);

    if ((ulong)interior != expectedInterior
        || (ulong)edgeShared != expectedEdgeShared
        || (ulong)vertexShared != expectedVertexShared
        || (ulong)incidences.Count != expectedFineCount)
    {
        throw new InvalidOperationException(
            "Class I scale-6 physical incidence materialization did not reproduce the frozen generalized incidence signature.");
    }

    var observedEdgeSignatures =
        incidences
            .Where(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 2)
            .Select(
                incidence =>
                    CreateCoarseSignature(
                        incidence.IncidentCoarseCellIds))
            .Order()
            .ToArray();

    var expectedEdgeSignatures =
        coarseTopology.Edges
            .Select(
                edge =>
                    CreateCoarseSignature(
                        edge.IncidentCellIds))
            .Order()
            .ToArray();

    if (!observedEdgeSignatures.SequenceEqual(
        expectedEdgeSignatures))
    {
        throw new InvalidOperationException(
            "Class I scale-6 physical incidence must cover every authoritative coarse edge exactly once.");
    }

    var observedVertexSignatures =
        incidences
            .Where(
                incidence =>
                    incidence.IncidentCoarseCellIds.Count == 3)
            .Select(
                incidence =>
                    CreateCoarseSignature(
                        incidence.IncidentCoarseCellIds))
            .Order()
            .ToArray();

    var expectedVertexSignatures =
        coarseTopology.Vertices
            .Select(
                vertex =>
                    CreateCoarseSignature(
                        vertex.IncidentCellIds))
            .Order()
            .ToArray();

    if (!observedVertexSignatures.SequenceEqual(
        expectedVertexSignatures))
    {
        throw new InvalidOperationException(
            "Class I scale-6 physical incidence must cover every authoritative coarse vertex exactly once.");
    }
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

private static bool IsOfficialClassIScaleSix(
    GoldbergScaledRefinement refinement)
{
    if (refinement.Scale != 6)
    {
        return false;
    }

    var coarse =
        refinement.CoarseParameters;

    var fine =
        refinement.FineParameters;

    var coarseIsClassI =
        (coarse.M == 0)
        != (coarse.N == 0);

    var fineIsClassI =
        (fine.M == 0)
        != (fine.N == 0);

    return coarseIsClassI
        && fineIsClassI
        && ((coarse.M == 0)
            == (fine.M == 0))
        && ((coarse.N == 0)
            == (fine.N == 0));
}
}