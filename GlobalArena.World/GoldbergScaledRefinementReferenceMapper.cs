namespace GlobalArena.World;

public static class GoldbergScaledRefinementReferenceMapper
{
    public static GoldbergScaledRefinementReferenceMap Materialize(
        GoldbergScaledRefinement refinement)
    {
        ArgumentNullException.ThrowIfNull(refinement);

        if (!IsReferencePair(refinement))
        {
            throw new NotSupportedException(
                "M2.4.2 supports reference mapping only for G(1,0) to G(2,0) with scale 2.");
        }

        var coarse =
            GoldbergStrategicTopologyGenerator
                .GenerateWithSeedVertexProvenance(
                    refinement.CoarseParameters);

        var fine =
            GoldbergStrategicTopologyGenerator
                .GenerateWithSeedVertexProvenance(
                    refinement.FineParameters);

        var references =
            new GoldbergScaledCellReference[12];

        for (var seedVertex = 1;
             seedVertex <= references.Length;
             seedVertex++)
        {
            if (!coarse.SeedVertexCellIds.TryGetValue(
                seedVertex,
                out var coarseCellId))
            {
                throw new InvalidOperationException(
                    "Coarse generation did not expose all canonical icosahedron seed vertices.");
            }

            if (!fine.SeedVertexCellIds.TryGetValue(
                seedVertex,
                out var fineCellId))
            {
                throw new InvalidOperationException(
                    "Fine generation did not expose all canonical icosahedron seed vertices.");
            }

            references[seedVertex - 1] =
                new GoldbergScaledCellReference(
                    new IcosahedronSeedVertexId(
                        seedVertex),
                    coarseCellId,
                    fineCellId);
        }

        return new GoldbergScaledRefinementReferenceMap(
            refinement,
            references);
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
