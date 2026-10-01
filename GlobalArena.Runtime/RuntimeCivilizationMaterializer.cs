using GlobalArena.World;

namespace GlobalArena.Runtime;

public static class RuntimeCivilizationMaterializer
{
    public static CivilizationRuntimeState Materialize(
        WorldGenerationResult generatedWorld,
        int civilizationCount)
    {
        ArgumentNullException.ThrowIfNull(
            generatedWorld);

        if (civilizationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(civilizationCount),
                "Civilization count must be positive.");
        }

        var policy =
            StrategicCivilizationPlacementPolicy.Default;

        var graph =
            generatedWorld.StrategicSurfaceGraph;

        var maximumCandidateCount =
            graph.NodeCount
            - (policy.ExcludeInitialReference
                ? 1
                : 0);

        if (civilizationCount
            > maximumCandidateCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(civilizationCount),
                $"Civilization count cannot exceed {maximumCandidateCount} candidates for the accepted placement policy.");
        }

        var requestedCandidateCount =
            civilizationCount;

        while (true)
        {
            var candidates =
                StrategicCivilizationStartCandidateSelector.Select(
                    generatedWorld.Request,
                    graph,
                    generatedWorld.StrategicCivilizationPlacementSuitability,
                    requestedCandidateCount,
                    policy);

            var landCandidates =
                candidates
                    .CandidateCellIds
                    .Where(
                        cellId =>
                            generatedWorld
                                .StrategicPhysicalFields
                                .LandWater
                                .GetKind(
                                    cellId)
                            == StrategicLandWaterKind.Land)
                    .ToArray();

            if (landCandidates.Length
                >= civilizationCount)
            {
                var records =
                    new CivilizationRuntimeRecord[
                        civilizationCount];

                for (var index = 0;
                     index < records.Length;
                     index++)
                {
                    records[index] =
                        new CivilizationRuntimeRecord(
                            new CivilizationId(
                                checked(
                                    (ulong)index
                                    + 1UL)),
                            landCandidates[index]);
                }

                return new CivilizationRuntimeState(
                    records);
            }

            if (requestedCandidateCount
                == maximumCandidateCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(civilizationCount),
                    $"Only {landCandidates.Length} deterministic land start candidates are available for the accepted placement policy.");
            }

            requestedCandidateCount =
                checked(
                    (int)Math.Min(
                        (long)maximumCandidateCount,
                        (long)requestedCandidateCount
                            * 2L));
        }
    }
}
