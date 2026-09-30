namespace GlobalArena.World;

public static class StrategicCivilizationStartCandidateSelector
{
    public static StrategicCivilizationStartCandidateSet Select(
        WorldGenerationRequest request,
        StrategicSurfaceGraph surfaceGraph,
        StrategicScalarField localSuitability,
        int candidateCount)
    {
        return Select(
            request,
            surfaceGraph,
            localSuitability,
            candidateCount,
            StrategicCivilizationPlacementPolicy.Default);
    }

    public static StrategicCivilizationStartCandidateSet Select(
        WorldGenerationRequest request,
        StrategicSurfaceGraph surfaceGraph,
        StrategicScalarField localSuitability,
        int candidateCount,
        StrategicCivilizationPlacementPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        ArgumentNullException.ThrowIfNull(
            surfaceGraph);

        ArgumentNullException.ThrowIfNull(
            localSuitability);

        ArgumentNullException.ThrowIfNull(
            policy);

        if (surfaceGraph.StrategicTopology.Parameters
            != request.StrategicParameters)
        {
            throw new ArgumentException(
                "Strategic surface graph parameters must match the generation request.",
                nameof(surfaceGraph));
        }

        if (!ReferenceEquals(
            surfaceGraph,
            localSuitability.SurfaceGraph))
        {
            throw new ArgumentException(
                "Placement suitability must share the supplied strategic surface graph.",
                nameof(localSuitability));
        }

        var maximumCandidateCount =
            surfaceGraph.NodeCount
            - (policy.ExcludeInitialReference
                ? 1
                : 0);

        if (candidateCount <= 0
            || candidateCount
            > maximumCandidateCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(candidateCount),
                $"Candidate count must be between 1 and {maximumCandidateCount} for the selected policy.");
        }

        for (var index = 0;
             index < localSuitability.Count;
             index++)
        {
            var value =
                localSuitability.GetRawValue(
                    index);

            if (value
                < StrategicCivilizationPlacementSuitabilityGenerator.MinimumRawSuitability
                || value
                > StrategicCivilizationPlacementSuitabilityGenerator.MaximumRawSuitability)
            {
                throw new ArgumentException(
                    "Placement suitability must remain inside the normalized fixed-point range.",
                    nameof(localSuitability));
            }
        }

        var stream =
            WorldGenerationRandomStreamFactory.Create(
                request,
                WorldGenerationRandomDomain.CivilizationPlacement);

        var referenceIndex =
            checked(
                (int)(
                    stream.NextUInt64()
                    % (ulong)surfaceGraph.NodeCount));

        var selected =
            new bool[
                surfaceGraph.NodeCount];

        var selectedIndexes =
            new List<int>(
                candidateCount);

        while (selectedIndexes.Count
            < candidateCount)
        {
            var distances =
                selectedIndexes.Count == 0
                ? ComputeDistances(
                    surfaceGraph,
                    new[]
                    {
                        referenceIndex
                    })
                : ComputeDistances(
                    surfaceGraph,
                    selectedIndexes);

            var maximumDistance =
                0;

            for (var index = 0;
                 index < surfaceGraph.NodeCount;
                 index++)
            {
                if (selected[index])
                {
                    continue;
                }

                if (policy.ExcludeInitialReference
                    && index == referenceIndex)
                {
                    continue;
                }

                if (distances[index] < 0)
                {
                    throw new InvalidOperationException(
                        "Strategic surface graph must be connected for civilization placement.");
                }

                maximumDistance =
                    Math.Max(
                        maximumDistance,
                        distances[index]);
            }

            var bestIndex =
                -1;

            Int128 bestScore =
                -1;

            var scoreWeightSum =
                checked(
                    policy.LocalSuitabilityWeight
                    + policy.DispersionWeight);

            for (var index = 0;
                 index < surfaceGraph.NodeCount;
                 index++)
            {
                if (selected[index])
                {
                    continue;
                }

                if (policy.ExcludeInitialReference
                    && index == referenceIndex)
                {
                    continue;
                }

                var local =
                    localSuitability.GetRawValue(
                        index);

                var dispersion =
                    maximumDistance == 0
                        ? 0L
                        : checked(
                            (long)(
                                ((Int128)distances[index]
                                    * StrategicScalarField.Denominator)
                                / maximumDistance));

                var weighted =
                    ((Int128)local
                        * policy.LocalSuitabilityWeight)
                    + ((Int128)dispersion
                        * policy.DispersionWeight);

                var score =
                    weighted
                    / scoreWeightSum;

                if (score > bestScore
                    || (score == bestScore
                        && (bestIndex < 0
                            || index < bestIndex)))
                {
                    bestScore =
                        score;

                    bestIndex =
                        index;
                }
            }

            if (bestIndex < 0)
            {
                throw new InvalidOperationException(
                    "No eligible civilization start candidate could be selected.");
            }

            selected[bestIndex] =
                true;

            selectedIndexes.Add(
                bestIndex);
        }

        return new StrategicCivilizationStartCandidateSet(
            surfaceGraph.GetCellId(
                referenceIndex),
            selectedIndexes.Select(
                surfaceGraph.GetCellId));
    }

    private static int[] ComputeDistances(
        StrategicSurfaceGraph surfaceGraph,
        IEnumerable<int> sourceIndexes)
    {
        var distances =
            Enumerable.Repeat(
                -1,
                surfaceGraph.NodeCount)
                .ToArray();

        var queue =
            new Queue<int>();

        foreach (var sourceIndex in sourceIndexes)
        {
            if (sourceIndex < 0
                || sourceIndex >= surfaceGraph.NodeCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sourceIndexes),
                    "Distance source index is outside the canonical strategic node range.");
            }

            if (distances[sourceIndex] == 0)
            {
                continue;
            }

            distances[sourceIndex] =
                0;

            queue.Enqueue(
                sourceIndex);
        }

        while (queue.Count > 0)
        {
            var current =
                queue.Dequeue();

            var nextDistance =
                checked(
                    distances[current]
                    + 1);

            foreach (var neighbor
                in surfaceGraph.GetNeighborIndexes(
                    current))
            {
                if (distances[neighbor] >= 0)
                {
                    continue;
                }

                distances[neighbor] =
                    nextDistance;

                queue.Enqueue(
                    neighbor);
            }
        }

        return distances;
    }
}
