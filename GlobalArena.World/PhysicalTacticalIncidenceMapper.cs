namespace GlobalArena.World;

public static class PhysicalTacticalIncidenceMapper
{
    public static PhysicalTacticalIncidenceMap Materialize(
        GoldbergScaledRefinement refinement)
    {
        ArgumentNullException.ThrowIfNull(refinement);

        if (!IsReferenceTarget(refinement))
        {
            throw new NotSupportedException(
                "M2.5.2-C physical incidence materialization currently supports only G(1,0) to G(6,0) with scale 6.");
        }

        var coarse =
            GoldbergStrategicTopologyGenerator
                .GenerateWithSeedVertexProvenance(
                    refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator
                .GenerateWithSeedVertexProvenance(
                    refinement.FineParameters);

        if (fine.DominantSeedVertexIdsByCell.Count
            != fine.Topology.Cells.Count)
        {
            throw new InvalidOperationException(
                "Fine triangular-seed provenance must cover every fine strategic cell.");
        }

        var incidences =
            new PhysicalTacticalTileIncidence[
                fine.Topology.Cells.Count];

        for (var index = 0;
             index < fine.Topology.Cells.Count;
             index++)
        {
            var fineCellId =
                fine.Topology.Cells[index].Id;

            if (!fine.DominantSeedVertexIdsByCell.TryGetValue(
                fineCellId,
                out var dominantSeedVertexIds))
            {
                throw new InvalidOperationException(
                    "Fine generation did not expose dominant seed provenance for every fine strategic cell.");
            }

            var coarseCellIds =
                dominantSeedVertexIds
                    .Select(
                        seedVertexId =>
                        {
                            if (!coarse.SeedVertexCellIds.TryGetValue(
                                seedVertexId,
                                out var coarseCellId))
                            {
                                throw new InvalidOperationException(
                                    "Fine provenance referenced a seed vertex that is not represented by the coarse G(1,0) topology.");
                            }

                            return coarseCellId;
                        })
                    .OrderBy(
                        coarseCellId =>
                            coarseCellId.Value)
                    .ToArray();

            incidences[index] =
                new PhysicalTacticalTileIncidence(
                    new PhysicalTacticalTileId(
                        refinement.FineParameters,
                        fineCellId),
                    coarse.Topology,
                    coarseCellIds);
        }

        ValidateReferenceSignature(
            incidences);

        return new PhysicalTacticalIncidenceMap(
            refinement,
            coarse.Topology,
            fine.Topology,
            incidences);
    }

    private static void ValidateReferenceSignature(
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

        if (interior != 312
            || edgeShared != 30
            || vertexShared != 20
            || incidences.Count != 362)
        {
            throw new InvalidOperationException(
                "G(1,0) to G(6,0) physical incidence materialization did not reproduce the audited 312/30/20/362 provenance signature.");
        }
    }

    private static bool IsReferenceTarget(
        GoldbergScaledRefinement refinement)
    {
        return refinement.Scale == 6
            && refinement.CoarseParameters.M == 1
            && refinement.CoarseParameters.N == 0
            && refinement.FineParameters.M == 6
            && refinement.FineParameters.N == 0;
    }
}