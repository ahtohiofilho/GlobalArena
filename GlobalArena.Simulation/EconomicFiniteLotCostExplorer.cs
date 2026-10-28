using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Evaluates a complete, finite hypothetical reassignment of shared physical
/// edge load, without changing the world, prices, trades, or cash balances.
/// Uses the EXISTING convex food-edge cost family. It is not a market solver,
/// route reachability check, or proof of density-economy activation.
/// </summary>
public sealed class EconomicFiniteLotCostExplorer
{
    public EconomicFiniteLotCostComparison Compare(
        IReadOnlyDictionary<StrategicEdgeId, BigInteger> currentLoadsMicrograms,
        IEnumerable<FoodStrategicEdgeCongestionProfile> profiles,
        IEnumerable<StrategicEdgeId> currentPath,
        IEnumerable<StrategicEdgeId> proposedPath,
        BigInteger lotMicrograms)
    {
        ArgumentNullException.ThrowIfNull(currentLoadsMicrograms);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(currentPath);
        ArgumentNullException.ThrowIfNull(proposedPath);
        if (lotMicrograms.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(lotMicrograms));

        var profileTable = new Dictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile>();
        foreach (var profile in profiles)
        {
            if (profile is null || !profileTable.TryAdd(profile.StrategicEdgeId, profile))
                throw new ArgumentException("Duplicate or null edge cost profile.", nameof(profiles));
        }

        var original = new Dictionary<StrategicEdgeId, BigInteger>();
        foreach (var row in currentLoadsMicrograms)
        {
            if (!row.Key.IsValid || row.Value.Sign < 0)
                throw new ArgumentException("Existing physical loads must have valid edges and nonnegative mass.",
                    nameof(currentLoadsMicrograms));
            if (row.Value.Sign > 0 && !profileTable.ContainsKey(row.Key))
                throw new ArgumentException("Every loaded edge requires an explicit cost profile.",
                    nameof(profiles));
            original.Add(row.Key, row.Value);
        }

        static StrategicEdgeId[] ValidatePath(
            IEnumerable<StrategicEdgeId> path,
            IReadOnlyDictionary<StrategicEdgeId, FoodStrategicEdgeCongestionProfile> known)
        {
            var edges = path.ToArray();
            if (edges.Any(edge => !edge.IsValid) ||
                edges.Distinct().Count() != edges.Length ||
                edges.Any(edge => !known.ContainsKey(edge)))
                throw new ArgumentException("A candidate physical-edge path has unknown, invalid or repeated edges.");
            return edges;
        }

        var from = ValidatePath(currentPath, profileTable);
        var to = ValidatePath(proposedPath, profileTable);
        var touched = from.Concat(to).Distinct().OrderBy(edge => edge.Value).ToArray();

        // This caller-supplied path is a hypothetical LOAD DECOMPOSITION,
        // not verified routing provenance. Require the source edges to contain
        // enough aggregate mass before removing the lot.
        foreach (var edge in from)
            if (original.GetValueOrDefault(edge).CompareTo(lotMicrograms) < 0)
                throw new InvalidOperationException("The original path does not carry enough mass to remove the lot.");

        var projected = new Dictionary<StrategicEdgeId, BigInteger>(original);
        foreach (var edge in from)
            projected[edge] = projected.GetValueOrDefault(edge) - lotMicrograms;
        foreach (var edge in to)
            projected[edge] = projected.GetValueOrDefault(edge) + lotMicrograms;

        var before = Zero();
        var after = Zero();
        foreach (var edge in touched)
        {
            var profile = profileTable[edge];
            before = Add(before, profile.EvaluateTotalCost(original.GetValueOrDefault(edge)));
            after = Add(after, profile.EvaluateTotalCost(projected.GetValueOrDefault(edge)));
        }

        // A signed change in REAL COST is not a debit, cash balance or
        // borrowing permission. Negative means the proposed real resource
        // utilization costs less, not that anybody has a negative account.
        var numerator = after.Numerator * before.Denominator
            - before.Numerator * after.Denominator;
        var denominator = after.Denominator * before.Denominator;
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        return new EconomicFiniteLotCostComparison(
            numerator / gcd, denominator / gcd,
            new ReadOnlyDictionary<StrategicEdgeId, BigInteger>(
                projected.OrderBy(x => x.Key.Value).ToDictionary(x => x.Key, x => x.Value)));
    }

    private static FoodTransportCostFraction Zero() =>
        new(BigInteger.Zero, BigInteger.One);

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction left, FoodTransportCostFraction right) =>
        new(left.Numerator * right.Denominator +
                right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);
}

/// <summary>
/// An immutable hypothetical result; no world mutation and no settlement.
/// SignedCostDelta = proposed total resource cost - current total resource cost.
/// </summary>
public sealed class EconomicFiniteLotCostComparison
{
    public BigInteger SignedCostDeltaNumerator { get; }
    public BigInteger CostDeltaDenominator { get; }
    public IReadOnlyDictionary<StrategicEdgeId, BigInteger> HypotheticalLoadsMicrograms { get; }

    internal EconomicFiniteLotCostComparison(
        BigInteger signedNumerator, BigInteger positiveDenominator,
        IReadOnlyDictionary<StrategicEdgeId, BigInteger> hypotheticalLoadsMicrograms)
    {
        SignedCostDeltaNumerator = signedNumerator;
        CostDeltaDenominator = positiveDenominator;
        HypotheticalLoadsMicrograms = hypotheticalLoadsMicrograms;
    }
}
