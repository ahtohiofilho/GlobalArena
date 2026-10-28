using System.Collections.ObjectModel;
using System.Numerics;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Hypothetical real-cost model with economies of density and congestion:
/// C(q) = a*q + 2*b*sqrt(q) + c*q^3/(3*K^2), q in physical grams.
/// a,b,c are nonnegative thousandths of raw cost units in their respective
/// dimensional coefficients, K is positive grams per economic period.
/// This is NOT a freight tariff, a financial balance or the existing
/// convex FoodStrategicEdgeCongestionProfile contract.
/// </summary>
public sealed class EconomicDensityResourceCostProfile
{
    private static readonly BigInteger MicrogramsPerGram = 1_000_000;
    private static readonly BigInteger Milli = 1_000;
    private static readonly BigInteger SquareRootPrecision = 1_000_000_000_000;

    public StrategicEdgeId ResourceId { get; }
    public ulong LinearMilliCostPerGram { get; }
    public ulong DensityMilliCostPerSqrtGram { get; }
    public ulong CongestionMilliCostPerGram { get; }
    public ulong CharacteristicGramsPerEconomicCycle { get; }

    public EconomicDensityResourceCostProfile(
        StrategicEdgeId resourceId,
        ulong linearMilliCostPerGram,
        ulong densityMilliCostPerSqrtGram,
        ulong congestionMilliCostPerGram,
        ulong characteristicGramsPerEconomicCycle)
    {
        if (!resourceId.IsValid)
            throw new ArgumentException("Real-cost resource requires an identity.", nameof(resourceId));
        if (characteristicGramsPerEconomicCycle == 0UL)
            throw new ArgumentOutOfRangeException(nameof(characteristicGramsPerEconomicCycle));
        ResourceId = resourceId;
        LinearMilliCostPerGram = linearMilliCostPerGram;
        DensityMilliCostPerSqrtGram = densityMilliCostPerSqrtGram;
        CongestionMilliCostPerGram = congestionMilliCostPerGram;
        CharacteristicGramsPerEconomicCycle = characteristicGramsPerEconomicCycle;
    }

    /// <summary>
    /// Exact rational lower/upper bounds on the real cost. The only
    /// approximation is a certified floor of sqrt(q) on a fixed 1e-12
    /// grid, computed by integer arithmetic, never floating point.
    /// </summary>
    public EconomicDensityCostBounds EvaluateTotalCostBounds(BigInteger loadMicrograms)
    {
        if (loadMicrograms.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(loadMicrograms));

        var reference = (BigInteger)CharacteristicGramsPerEconomicCycle;
        var linear = new FoodTransportCostFraction(
            (BigInteger)LinearMilliCostPerGram * loadMicrograms,
            Milli * MicrogramsPerGram);
        var congestion = new FoodTransportCostFraction(
            (BigInteger)CongestionMilliCostPerGram * BigInteger.Pow(loadMicrograms, 3),
            3 * Milli * reference * reference * BigInteger.Pow(MicrogramsPerGram, 3));

        var common = Add(linear, congestion);
        if (DensityMilliCostPerSqrtGram == 0UL || loadMicrograms.IsZero)
            return new EconomicDensityCostBounds(common, common);

        // floor(sqrt(q) * P) == floor(sqrt(floor(L*P^2/M))).
        // The lower bound is exact when L*P^2/M is a perfect square.
        var scaledSquared = BigInteger.DivRem(
            loadMicrograms * SquareRootPrecision * SquareRootPrecision,
            MicrogramsPerGram, out var remainder);
        var lowerRoot = FloorSquareRoot(scaledSquared);
        var rootIsExact = remainder.IsZero && lowerRoot * lowerRoot == scaledSquared;
        var rootDenominator = Milli * SquareRootPrecision;
        var densityLower = new FoodTransportCostFraction(
            2 * (BigInteger)DensityMilliCostPerSqrtGram * lowerRoot,
            rootDenominator);
        var densityUpper = new FoodTransportCostFraction(
            2 * (BigInteger)DensityMilliCostPerSqrtGram *
                (rootIsExact ? lowerRoot : lowerRoot + BigInteger.One),
            rootDenominator);

        return new EconomicDensityCostBounds(
            Add(common, densityLower), Add(common, densityUpper));
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction left, FoodTransportCostFraction right) =>
        new(left.Numerator * right.Denominator +
            right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);

    private static BigInteger FloorSquareRoot(BigInteger n)
    {
        if (n.Sign < 0)
            throw new ArgumentOutOfRangeException(nameof(n));
        if (n <= BigInteger.One)
            return n;

        var bigEndian = n.ToByteArray(isUnsigned: true, isBigEndian: true);
        var first = bigEndian[0];
        var leadingBits = 8;
        while ((first & 0x80) == 0)
        {
            first <<= 1;
            leadingBits--;
        }
        var bitLength = checked((bigEndian.Length - 1) * 8 + leadingBits);
        var guess = BigInteger.One << ((bitLength + 1) / 2);
        while (true)
        {
            var next = (guess + n / guess) >> 1;
            if (next >= guess)
                return guess;
            guess = next;
        }
    }
}

/// <summary>
/// Closed rational bounds; never a settlement quote or cash transaction.
/// </summary>
public sealed class EconomicDensityCostBounds
{
    public FoodTransportCostFraction Lower { get; }
    public FoodTransportCostFraction Upper { get; }

    internal EconomicDensityCostBounds(
        FoodTransportCostFraction lower, FoodTransportCostFraction upper)
    {
        if (lower.CompareTo(upper) > 0)
            throw new ArgumentException("Invalid real-cost interval.");
        Lower = lower;
        Upper = upper;
    }
}

public enum EconomicDensityCostDeltaClassification
{
    Indeterminate = 0,
    CertifiedCostDecrease = 1,
    CertifiedCostIncrease = 2
}

/// <summary>
/// Pure evaluation of a finite hypothetical change of resource loads.
/// Cost decrease is NOT authorization to commit an economically different
/// market regime. Route validity, market clearing and settlement live elsewhere.
/// </summary>
public sealed class EconomicFiniteLotDensityCostExplorer
{
    public EconomicFiniteLotDensityCostComparison Compare(
        IReadOnlyDictionary<StrategicEdgeId, BigInteger> currentLoadsMicrograms,
        IEnumerable<EconomicDensityResourceCostProfile> profiles,
        IEnumerable<StrategicEdgeId> previousPath,
        IEnumerable<StrategicEdgeId> proposedPath,
        BigInteger lotMicrograms)
    {
        ArgumentNullException.ThrowIfNull(currentLoadsMicrograms);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(previousPath);
        ArgumentNullException.ThrowIfNull(proposedPath);
        if (lotMicrograms.Sign <= 0)
            throw new ArgumentOutOfRangeException(nameof(lotMicrograms));

        var byResource = new Dictionary<StrategicEdgeId, EconomicDensityResourceCostProfile>();
        foreach (var profile in profiles)
            if (profile is null || !byResource.TryAdd(profile.ResourceId, profile))
                throw new ArgumentException("Null or duplicate resource profile.", nameof(profiles));

        var original = new Dictionary<StrategicEdgeId, BigInteger>();
        foreach (var pair in currentLoadsMicrograms)
        {
            if (!pair.Key.IsValid || pair.Value.Sign < 0)
                throw new ArgumentException("Physical loads must be nonnegative and have valid resource IDs.",
                    nameof(currentLoadsMicrograms));
            if (pair.Value.Sign > 0 && !byResource.ContainsKey(pair.Key))
                throw new ArgumentException("A loaded resource has no declared cost function.",
                    nameof(profiles));
            original.Add(pair.Key, pair.Value);
        }

        StrategicEdgeId[] ValidatePath(IEnumerable<StrategicEdgeId> path)
        {
            var sequence = path.ToArray();
            if (sequence.Any(id => !id.IsValid || !byResource.ContainsKey(id)) ||
                sequence.Distinct().Count() != sequence.Length)
                throw new ArgumentException("Path contains invalid, undeclared or repeated resources.");
            return sequence;
        }

        var from = ValidatePath(previousPath);
        var to = ValidatePath(proposedPath);
        foreach (var edge in from)
            if (original.GetValueOrDefault(edge) < lotMicrograms)
                throw new InvalidOperationException("Original path cannot release the proposed physical batch.");

        var hypothetical = new Dictionary<StrategicEdgeId, BigInteger>(original);
        foreach (var edge in from)
            hypothetical[edge] = hypothetical.GetValueOrDefault(edge) - lotMicrograms;
        foreach (var edge in to)
            hypothetical[edge] = hypothetical.GetValueOrDefault(edge) + lotMicrograms;

        var lower = (N: BigInteger.Zero, D: BigInteger.One);
        var upper = (N: BigInteger.Zero, D: BigInteger.One);
        foreach (var edge in from.Concat(to).Distinct().OrderBy(e => e.Value))
        {
            var beforeLoad = original.GetValueOrDefault(edge);
            var afterLoad = hypothetical.GetValueOrDefault(edge);
            if (beforeLoad == afterLoad)
                continue; // Identical shared resources cancel exactly.
            var profile = byResource[edge];
            var before = profile.EvaluateTotalCostBounds(beforeLoad);
            var after = profile.EvaluateTotalCostBounds(afterLoad);
            lower = Add(lower, Subtract(after.Lower, before.Upper));
            upper = Add(upper, Subtract(after.Upper, before.Lower));
        }
        if (lower.N * upper.D > upper.N * lower.D)
            throw new InvalidOperationException("Invalid computed signed cost interval.");

        return new EconomicFiniteLotDensityCostComparison(
            lower.N, lower.D, upper.N, upper.D,
            new ReadOnlyDictionary<StrategicEdgeId, BigInteger>(
                hypothetical.OrderBy(row => row.Key.Value)
                    .ToDictionary(row => row.Key, row => row.Value)));
    }

    private static (BigInteger N, BigInteger D) Subtract(
        FoodTransportCostFraction left, FoodTransportCostFraction right) =>
        Normalize(left.Numerator * right.Denominator -
            right.Numerator * left.Denominator,
            left.Denominator * right.Denominator);

    private static (BigInteger N, BigInteger D) Add(
        (BigInteger N, BigInteger D) left,
        (BigInteger N, BigInteger D) right) =>
        Normalize(left.N * right.D + right.N * left.D, left.D * right.D);

    private static (BigInteger N, BigInteger D) Normalize(BigInteger numerator, BigInteger denominator)
    {
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        return (numerator / gcd, denominator / gcd);
    }
}

public sealed class EconomicFiniteLotDensityCostComparison
{
    public BigInteger SignedCostDeltaLowerNumerator { get; }
    public BigInteger CostDeltaLowerDenominator { get; }
    public BigInteger SignedCostDeltaUpperNumerator { get; }
    public BigInteger CostDeltaUpperDenominator { get; }
    public IReadOnlyDictionary<StrategicEdgeId, BigInteger> HypotheticalLoadsMicrograms { get; }

    public EconomicDensityCostDeltaClassification CostSign =>
        SignedCostDeltaUpperNumerator.Sign < 0
            ? EconomicDensityCostDeltaClassification.CertifiedCostDecrease
            : SignedCostDeltaLowerNumerator.Sign > 0
                ? EconomicDensityCostDeltaClassification.CertifiedCostIncrease
                : EconomicDensityCostDeltaClassification.Indeterminate;

    internal EconomicFiniteLotDensityCostComparison(
        BigInteger lowerNumerator, BigInteger lowerDenominator,
        BigInteger upperNumerator, BigInteger upperDenominator,
        IReadOnlyDictionary<StrategicEdgeId, BigInteger> hypotheticalLoadsMicrograms)
    {
        SignedCostDeltaLowerNumerator = lowerNumerator;
        CostDeltaLowerDenominator = lowerDenominator;
        SignedCostDeltaUpperNumerator = upperNumerator;
        CostDeltaUpperDenominator = upperDenominator;
        HypotheticalLoadsMicrograms = hypotheticalLoadsMicrograms;
    }
}
