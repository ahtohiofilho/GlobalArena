using System.Numerics;

namespace GlobalArena.Simulation;

public sealed record ExperimentalSharedFreightAllocation(
    FoodTransportCostFraction TotalCost,
    IReadOnlyList<ulong> ChosenServiceIds,
    IReadOnlyDictionary<ExperimentalSharedFreightCorridorKey, BigInteger> CorridorLoads);

/// <summary>
/// Independent = cheapest uncongested route per lot.
/// Coordinated = deterministic strictly-improving one-lot-at-a-time local
/// search over the coupled batch objective (NOT a general global optimum).
/// Exact = optional exhaustive enumeration within a bounded reference scope.
/// These are hypothetical forecasts; none is an economic market settlement.
/// </summary>
public sealed record ExperimentalSharedFreightComparison(
    ExperimentalSharedFreightAllocation Independent,
    ExperimentalSharedFreightAllocation Coordinated,
    ExperimentalSharedFreightAllocation? Exact,
    int EvaluatedConfigurations,
    int LocalImprovementPasses,
    BigInteger PossibleConfigurations);

public sealed class ExperimentalSharedFreightAllocationLaboratory
{
    public ExperimentalSharedFreightComparison Compare(
        IEnumerable<ExperimentalSharedFreightLot> inputLots,
        ExperimentalSharedFreightCongestionPolicy policy,
        int maxImprovementPasses = 5,
        int maxExactConfigurations = 0)
    {
        ArgumentNullException.ThrowIfNull(inputLots);
        ArgumentNullException.ThrowIfNull(policy);
        if (maxImprovementPasses < 0 || maxExactConfigurations < 0)
            throw new ArgumentOutOfRangeException(nameof(maxImprovementPasses));

        var lots = inputLots.ToArray();
        if (lots.Any(x => x is null) ||
            lots.Select(x => x.LotId).Distinct().Count() != lots.Length)
            throw new ArgumentException("Lots must have unique valid identities.",
                nameof(inputLots));
        if (lots.Length == 0)
            throw new ArgumentException("A shared-transport batch needs at least one lot.",
                nameof(inputLots));
        Array.Sort(lots, (a, b) => a.LotId.CompareTo(b.LotId));

        var configurations = BigInteger.One;
        foreach (var lot in lots)
            configurations *= lot.Alternatives.Count;
        var evaluations = 0;

        ExperimentalSharedFreightBatchEvaluation Evaluate(int[] selection)
        {
            evaluations++;
            return ExperimentalSharedFreightBatchCost.Evaluate(lots, selection, policy);
        }

        var independentIndices = new int[lots.Length];
        for (var i = 0; i < lots.Length; i++)
        {
            var best = ExperimentalSharedFreightBatchCost.UncongestedCost(
                lots[i].ExpectedLoadMicrograms, lots[i].Alternatives[0].Pricing);
            for (var alternative = 1; alternative < lots[i].Alternatives.Count;
                 alternative++)
            {
                var cost = ExperimentalSharedFreightBatchCost.UncongestedCost(
                    lots[i].ExpectedLoadMicrograms,
                    lots[i].Alternatives[alternative].Pricing);
                // Alternatives are ordered by ascending ServiceId;
                // equal cost selects the same canonical ID on every host.
                if (cost.CompareTo(best) < 0)
                {
                    best = cost;
                    independentIndices[i] = alternative;
                }
            }
        }

        var independentEvaluation = Evaluate(independentIndices);
        var coordinatedIndices = (int[])independentIndices.Clone();
        var coordinatedEvaluation = independentEvaluation;
        var completedPasses = 0;
        for (var pass = 0; pass < maxImprovementPasses; pass++)
        {
            var changed = false;
            for (var i = 0; i < lots.Length; i++)
            {
                var chosen = coordinatedIndices[i];
                var winning = coordinatedEvaluation;
                for (var candidate = 0;
                     candidate < lots[i].Alternatives.Count; candidate++)
                {
                    if (candidate == coordinatedIndices[i]) continue;
                    coordinatedIndices[i] = candidate;
                    var alternate = Evaluate(coordinatedIndices);
                    if (alternate.TotalCost.CompareTo(winning.TotalCost) < 0)
                    {
                        chosen = candidate;
                        winning = alternate;
                    }
                }
                coordinatedIndices[i] = chosen;
                if (winning.TotalCost.CompareTo(coordinatedEvaluation.TotalCost) < 0)
                {
                    coordinatedEvaluation = winning;
                    changed = true;
                }
            }
            completedPasses++;
            if (!changed) break;
        }

        ExperimentalSharedFreightAllocation? exactAllocation = null;
        if (maxExactConfigurations > 0 &&
            configurations <= maxExactConfigurations)
        {
            var candidates = new int[lots.Length];
            int[]? optimumIndices = null;
            ExperimentalSharedFreightBatchEvaluation? optimum = null;
            void Enumerate(int i)
            {
                if (i == lots.Length)
                {
                    var evaluation = Evaluate(candidates);
                    // The visit order is already canonical by LotId then
                    // ServiceId. On ties retain the first assignment.
                    if (optimum is null ||
                        evaluation.TotalCost.CompareTo(optimum.TotalCost) < 0)
                    {
                        optimum = evaluation;
                        optimumIndices = (int[])candidates.Clone();
                    }
                    return;
                }
                for (var j = 0; j < lots[i].Alternatives.Count; j++)
                {
                    candidates[i] = j;
                    Enumerate(i + 1);
                }
            }

            Enumerate(0);
            exactAllocation = ToAllocation(lots, optimumIndices!, optimum!);
            if (coordinatedEvaluation.TotalCost.CompareTo(optimum!.TotalCost) < 0)
                throw new InvalidOperationException(
                    "An independently enumerated optimum cannot be beaten by local search.");
        }

        return new ExperimentalSharedFreightComparison(
            ToAllocation(lots, independentIndices, independentEvaluation),
            ToAllocation(lots, coordinatedIndices, coordinatedEvaluation),
            exactAllocation, evaluations, completedPasses, configurations);
    }

    private static ExperimentalSharedFreightAllocation ToAllocation(
        IReadOnlyList<ExperimentalSharedFreightLot> lots,
        IReadOnlyList<int> selected,
        ExperimentalSharedFreightBatchEvaluation result) =>
        new(result.TotalCost, Array.AsReadOnly(
                lots.Select((lot, i) => lot.Alternatives[selected[i]].ServiceId)
                    .ToArray()),
            result.CorridorLoads);
}
