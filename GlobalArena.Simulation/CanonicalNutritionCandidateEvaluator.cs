using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// ONE named food's actual physical quantity, provided by a caller.
/// The pure candidate evaluator does not certify this quantity was executed.
/// UnitsPerRaw is an EXPLICIT, EXPERIMENTAL conversion to a common nutritional
/// reference unit; raw commodity quantities are never silently equated.
/// </summary>
public sealed record CanonicalNutritionFoodIntake(
    CommodityId Commodity,
    ulong ConsumedRaw,
    double UnitsPerRaw);

/// <summary>
/// GA-NUTR-ARCH-01. Shape parameters only, not approved physiological constants.
/// Nu=2.8 from earlier experiments is deliberately NOT a runtime default.
/// </summary>
public sealed class CanonicalNutritionCandidateParameters
{
    public double Sigma { get; }
    public double Nu { get; }
    public bool IsExperimental => true;

    public CanonicalNutritionCandidateParameters(double sigma, double nu)
    {
        if (!double.IsFinite(sigma) || sigma <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(sigma),
                "Experimental diversity sigma must be finite and positive.");
        if (!double.IsFinite(nu) || nu < 0.0)
            throw new ArgumentOutOfRangeException(nameof(nu),
                "Experimental variety intensity must be finite and nonnegative.");
        Sigma = sigma;
        Nu = nu;
    }
}

/// <summary>
/// Read-only mathematical candidate. It is NOT a physiological measurement,
/// live labor multiplier, completed physical observation or financial posting.
/// CandidateQuality is null until an explicit A(f) curve is supplied.
/// </summary>
public sealed class CanonicalNutritionCandidateResult
{
    public double FoodPerPerson { get; }
    public double Diversity { get; }
    public double DiversityActivation { get; }
    public double CandidateVarietyFactor { get; }
    public double? CandidateQuality { get; }
    public bool HasExplicitSufficiencyCurve => CandidateQuality.HasValue;
    public bool CertifiesExecutedPhysicalConsumption => false;
    public bool AuthorizesProductivityChange => false;
    public bool AuthorizesRealPurchasingPower => false;
    public bool MutatesCanonicalWorldOrSave => false;

    internal CanonicalNutritionCandidateResult(double foodPerPerson,
        double diversity, double diversityActivation,
        double candidateVarietyFactor, double? candidateQuality)
    {
        FoodPerPerson = foodPerPerson;
        Diversity = diversity;
        DiversityActivation = diversityActivation;
        CandidateVarietyFactor = candidateVarietyFactor;
        CandidateQuality = candidateQuality;
    }
}

/// <summary>
/// Mathematical contract, independent of prices, demand and money:
/// z[j] = ConsumedRaw[j] * UnitsPerRaw[j] / population; f = sum(z[j]);
/// g(z) = z/(z+sigma); D = (sum(g(z[j])) - g(f))/5 for six food slots;
/// E(f) = f^4/(1+f^4); V = 1+nu*E(f)*D;
/// optional Q(f,D) = A(f)*V, where A MUST be explicitly provided.
/// This pure method never infers six foods from the legacy aggregate food
/// commodity, never sets nutritional conversion or sufficiency constants,
/// and never participates in a production or economic settlement cycle.
/// </summary>
public sealed class CanonicalNutritionCandidateEvaluator
{
    public const int RequiredFoodVarieties = 6;

    public CanonicalNutritionCandidateResult Evaluate(
        ulong population,
        IEnumerable<CanonicalNutritionFoodIntake> executedQuantityCandidates,
        CanonicalNutritionCandidateParameters parameters,
        Func<double, double>? candidateSufficiencyCurve = null)
    {
        ArgumentNullException.ThrowIfNull(executedQuantityCandidates);
        ArgumentNullException.ThrowIfNull(parameters);
        if (population == 0UL)
            throw new ArgumentOutOfRangeException(nameof(population),
                "Nutrition cannot be normalized without a resident population.");

        // Fixed identity ordering makes floating-point summation independent
        // of caller enumeration order. Every category must be explicit,
        // including those with a certified zero quantity in a future adapter.
        var foods = executedQuantityCandidates.ToArray();
        if (foods.Length != RequiredFoodVarieties ||
            foods.Any(x => x is null ||
                !x.Commodity.IsValid ||
                !double.IsFinite(x.UnitsPerRaw) || x.UnitsPerRaw <= 0.0) ||
            foods.Select(x => x.Commodity.Value).Distinct().Count() !=
                RequiredFoodVarieties)
            throw new ArgumentException(
                "Nutrition requires six distinct named foods with explicit positive finite unit conversions; an aggregate food quantity is insufficient.",
                nameof(executedQuantityCandidates));

        Array.Sort(foods, (a, b) =>
            a.Commodity.Value.CompareTo(b.Commodity.Value));

        var z = new double[RequiredFoodVarieties];
        for (var i = 0; i < z.Length; i++)
        {
            z[i] = ((double)foods[i].ConsumedRaw *
                foods[i].UnitsPerRaw) / population;
            if (!double.IsFinite(z[i]) || z[i] < 0.0)
                throw new ArgumentOutOfRangeException(
                    nameof(executedQuantityCandidates),
                    "The nutritional conversion exceeds the supported numeric domain.");
        }
        var f = z.Sum();
        if (!double.IsFinite(f))
            throw new ArgumentOutOfRangeException(
                nameof(executedQuantityCandidates),
                "Normalized total food must be finite.");

        double G(double value) => value / (value + parameters.Sigma);
        // D is a mathematically bounded diversity index for six categories.
        // Clamp only floating-point rounding at either endpoint.
        var diversity = Math.Clamp(
            (z.Sum(G) - G(f)) / (RequiredFoodVarieties - 1), 0.0, 1.0);

        // Stable reciprocal formulation avoids overflow of f^4 for large f.
        double activation;
        if (f <= 1.0)
        {
            var f2 = f * f;
            var f4 = f2 * f2;
            activation = f4 / (1.0 + f4);
        }
        else
        {
            var inv = 1.0 / f;
            var inv2 = inv * inv;
            var inv4 = inv2 * inv2;
            activation = 1.0 / (1.0 + inv4);
        }

        var varietyFactor = 1.0 + parameters.Nu * activation * diversity;
        if (!double.IsFinite(varietyFactor))
            throw new InvalidOperationException(
                "Candidate variety factor exceeds the supported numeric domain.");

        double? quality = null;
        if (candidateSufficiencyCurve is not null)
        {
            var sufficiency = candidateSufficiencyCurve(f);
            if (!double.IsFinite(sufficiency) || sufficiency < 0.0)
                throw new InvalidOperationException(
                    "Experimental A(f) must be finite and nonnegative.");
            var candidateQuality = sufficiency * varietyFactor;
            if (!double.IsFinite(candidateQuality))
                throw new InvalidOperationException(
                    "Candidate nutrition quality exceeds the supported numeric domain.");
            quality = candidateQuality;
        }

        return new CanonicalNutritionCandidateResult(
            f, diversity, activation, varietyFactor, quality);
    }
}
