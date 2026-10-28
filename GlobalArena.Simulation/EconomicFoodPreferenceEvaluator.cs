namespace GlobalArena.Simulation;

/// <summary>
/// Calibrated, opt-in parameters from the GA-PROP-001 food preference
/// potential. They are ruleset inputs, NOT universal historical values.
/// </summary>
public sealed class EconomicFoodPreferenceParameters
{
    public double Sigma { get; }
    public double Nu { get; }
    public double Lambda { get; }

    public EconomicFoodPreferenceParameters(double sigma, double nu, double lambda)
    {
        if (!double.IsFinite(sigma) || sigma <= 0 ||
            !double.IsFinite(nu) || nu < 0 ||
            !double.IsFinite(lambda) || lambda < 0)
            throw new ArgumentOutOfRangeException(
                nameof(sigma), "Food parameters require sigma>0, nu>=0, lambda>=0.");
        Sigma = sigma;
        Nu = nu;
        Lambda = lambda;
    }
}

/// <summary>
/// The consumption-side canonical utility and monetary marginal willingness
/// to pay for every distinct food origin, evaluated on ONE hypothetical state.
/// This is not an origin-price quote, delivery, balance-sheet entry or
/// equilibrium certificate.
/// </summary>
public sealed class EconomicFoodPreferenceResult
{
    public double Utility { get; }
    public double FoodPerResident { get; }
    public double Diversity { get; }
    public double AbundanceFactor { get; }
    public double VarietyMultiplier { get; }
    public IReadOnlyList<double> MarginalWillingnessToPay { get; }

    internal EconomicFoodPreferenceResult(
        double utility, double foodPerResident,
        double diversity, double abundanceFactor,
        double varietyMultiplier, double[] marginalWillingnessToPay)
    {
        Utility = utility;
        FoodPerResident = foodPerResident;
        Diversity = diversity;
        AbundanceFactor = abundanceFactor;
        VarietyMultiplier = varietyMultiplier;
        MarginalWillingnessToPay = Array.AsReadOnly(marginalWillingnessToPay);
    }
}

/// <summary>
/// Pure GA-PROP-001 preference evaluator:
/// U = P*[2 log(1+f) + lambda log(V)] - (2P)^2/H
/// f = sum(q[j]/P); g(z)=z/(z+sigma);
/// D=(sum(g(z[j]))-g(f))/(n-1), n>1, else 0;
/// E=f^4/(1+f^4), V=1+nu*E*D;
/// WTP[j] = (H/(2P))^2 * [2/(1+f) + lambda*d(log V)/d(z[j])].
///
/// H is a strictly positive NUMERAIRE/RESIDUAL of the budget, NOT a bank
/// deposit. All q, P, H and prices are expressed in the same whole economic
/// units as the GA-PROP-001 mathematical benchmark, not legacy raw integers.
/// No mutation; no floating-point value is a ledger posting.
/// </summary>
public sealed class EconomicFoodPreferenceEvaluator
{
    public EconomicFoodPreferenceResult Evaluate(
        double population,
        double monetaryResidual,
        IEnumerable<double> foodQuantities,
        EconomicFoodPreferenceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(foodQuantities);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!double.IsFinite(population) || population <= 0 ||
            !double.IsFinite(monetaryResidual) || monetaryResidual <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(population), "Population and residual H must be finite and positive.");

        var q = foodQuantities.ToArray();
        if (q.Length == 0 || q.Any(x => !double.IsFinite(x) || x < 0))
            throw new ArgumentException(
                "One or more food quantities must be finite and nonnegative.",
                nameof(foodQuantities));

        var z = q.Select(x => x / population).ToArray();
        var f = z.Sum();
        if (!double.IsFinite(f))
            throw new ArgumentOutOfRangeException(nameof(foodQuantities),
                "Normalized total food is outside the supported numerical domain.");

        double G(double x) => x / (x + parameters.Sigma);
        double GPrime(double x)
        {
            var denominator = x + parameters.Sigma;
            return parameters.Sigma / denominator / denominator;
        }

        var diversity = q.Length == 1
            ? 0.0
            : (z.Sum(G) - G(f)) / (q.Length - 1);

        // Avoid f^4 overflow and E' underflow/overflow at large f.
        double abundance;
        double abundancePrime;
        if (f <= 1.0)
        {
            var f2 = f * f;
            var f4 = f2 * f2;
            abundance = f4 / (1.0 + f4);
            abundancePrime = 4.0 * f2 * f / Math.Pow(1.0 + f4, 2);
        }
        else
        {
            var inverse = 1.0 / f;
            var inverse2 = inverse * inverse;
            var inverse4 = inverse2 * inverse2;
            abundance = 1.0 / (1.0 + inverse4);
            abundancePrime = 4.0 * inverse4 * inverse /
                ((1.0 + inverse4) * (1.0 + inverse4));
        }

        var v = 1.0 + parameters.Nu * abundance * diversity;
        if (!double.IsFinite(v) || v <= 0)
            throw new InvalidOperationException(
                "GA-PROP-001 variety multiplier must remain finite and positive.");

        var h0 = 2.0 * population;
        var hRatio = monetaryResidual / h0;
        var derivativeRatio = hRatio * hRatio;
        var utility = population *
            (2.0 * Math.Log(1.0 + f) + parameters.Lambda * Math.Log(v)) -
            h0 * (h0 / monetaryResidual);

        var marginal = new double[q.Length];
        for (var j = 0; j < q.Length; j++)
        {
            var dLogVariety = q.Length == 1
                ? 0.0
                : parameters.Nu *
                    (abundancePrime * diversity +
                    abundance * (GPrime(z[j]) - GPrime(f)) / (q.Length - 1))
                    / v;
            marginal[j] = derivativeRatio *
                (2.0 / (1.0 + f) + parameters.Lambda * dLogVariety);
            if (!double.IsFinite(marginal[j]))
                throw new InvalidOperationException(
                    "Food willingness to pay is outside the supported numerical domain.");
        }
        if (!double.IsFinite(utility))
            throw new InvalidOperationException(
                "Food utility is outside the supported numerical domain.");

        return new EconomicFoodPreferenceResult(
            utility, f, diversity, abundance, v, marginal);
    }
}
