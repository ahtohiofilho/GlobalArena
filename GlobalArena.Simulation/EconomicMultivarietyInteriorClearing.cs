using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One fixed-supply origin variety. OriginPrice is NOT supplied: the
/// hypothetical clearing determines it from simultaneous market equations.
/// </summary>
public sealed class EconomicMultivarietyOrigin
{
    public EconomicPointId OriginId { get; }
    public double AvailableQuantity { get; }

    public EconomicMultivarietyOrigin(
        EconomicPointId originId, double availableQuantity)
    {
        if (!originId.IsValid || !double.IsFinite(availableQuantity) ||
            availableQuantity <= 0.0)
            throw new ArgumentOutOfRangeException(
                nameof(availableQuantity), "Origin requires positive finite supply.");
        OriginId = originId;
        AvailableQuantity = availableQuantity;
    }
}

public sealed class EconomicMultivarietyBuyer
{
    public EconomicPointId BuyerId { get; }
    public double Population { get; }
    public double Budget { get; }

    public EconomicMultivarietyBuyer(
        EconomicPointId buyerId, double population, double budget)
    {
        if (!buyerId.IsValid || !double.IsFinite(population) || population <= 0 ||
            !double.IsFinite(budget) || budget <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(budget), "Buyer requires valid identity, population and budget.");
        BuyerId = buyerId;
        Population = population;
        Budget = budget;
    }
}

/// <summary>
/// Ex ante frozen freight per one WHOLE food flow unit for one buyer/origin.
/// This quote is NOT the density resource cost or a monetary ledger entry.
/// All buyer-origin pairs must be quoted, including a zero domestic price.
/// </summary>
public sealed class EconomicMultivarietyDeliveredFreight
{
    public EconomicPointId BuyerId { get; }
    public EconomicPointId OriginId { get; }
    public double FreightPerUnit { get; }

    public EconomicMultivarietyDeliveredFreight(
        EconomicPointId buyerId, EconomicPointId originId, double freightPerUnit)
    {
        if (!buyerId.IsValid || !originId.IsValid ||
            !double.IsFinite(freightPerUnit) || freightPerUnit < 0)
            throw new ArgumentOutOfRangeException(
                nameof(freightPerUnit), "Delivered freight must be finite and nonnegative.");
        BuyerId = buyerId;
        OriginId = originId;
        FreightPerUnit = freightPerUnit;
    }
}

public sealed class EconomicMultivarietyConsumerAllocation
{
    public EconomicPointId BuyerId { get; }
    public double MonetaryResidual { get; }
    public double Utility { get; }
    public IReadOnlyList<double> QuantitiesByOrigin { get; }
    public IReadOnlyList<double> MarginalWillingnessByOrigin { get; }
    public IReadOnlyList<double> DeliveredPricesByOrigin { get; }

    internal EconomicMultivarietyConsumerAllocation(
        EconomicPointId id, double h, double utility,
        double[] q, double[] wtp, double[] delivered)
    {
        BuyerId = id;
        MonetaryResidual = h;
        Utility = utility;
        QuantitiesByOrigin = Array.AsReadOnly(q);
        MarginalWillingnessByOrigin = Array.AsReadOnly(wtp);
        DeliveredPricesByOrigin = Array.AsReadOnly(delivered);
    }
}

/// <summary>
/// Hypothetical stationary INTERIOR clearing (all q,H,p strictly positive),
/// NOT a global equilibrium, not a path/route choice, not a settlement.
/// </summary>
public sealed class EconomicMultivarietyInteriorClearingResult
{
    public IReadOnlyList<EconomicPointId> OriginOrder { get; }
    public IReadOnlyList<double> OriginPrices { get; }
    public IReadOnlyList<EconomicMultivarietyConsumerAllocation> Buyers { get; }
    public double SellerReceipts { get; }
    public double FreightReceipts { get; }
    public double BuyerExpenditure { get; }
    public double MaximumBudgetResidual { get; }
    public double MaximumSupplyResidual { get; }
    public double MaximumMarginalResidual { get; }
    public int NewtonIterations { get; }

    internal EconomicMultivarietyInteriorClearingResult(
        EconomicPointId[] origins, double[] prices,
        EconomicMultivarietyConsumerAllocation[] buyers,
        double sellerReceipts, double freightReceipts,
        double buyerExpenditure, double budgetResidual,
        double supplyResidual, double marginalResidual,
        int newtonIterations)
    {
        OriginOrder = Array.AsReadOnly(origins);
        OriginPrices = Array.AsReadOnly(prices);
        Buyers = Array.AsReadOnly(buyers);
        SellerReceipts = sellerReceipts;
        FreightReceipts = freightReceipts;
        BuyerExpenditure = buyerExpenditure;
        MaximumBudgetResidual = budgetResidual;
        MaximumSupplyResidual = supplyResidual;
        MaximumMarginalResidual = marginalResidual;
        NewtonIterations = newtonIterations;
    }
}

/// <summary>
/// Bounded, deterministic finite-difference Newton reference for a fixed
/// OD service/freight support, using the full n-variety GA-PROP-001 utility
/// gradient. Unknowns are all q[i,j], positive H[i] and positive p_origin[j].
/// Equations: WTP[i,j]=p[j]+t[i,j], H+sum(q*delivered)=budget,
/// sum_i(q[i,j])=fixed supply[j]. These conditions characterize an
/// INTERIOR stationary candidate only; nonconvexity, corner equilibria,
/// finite coordinated route entry and global selection remain unresolved.
///
/// The line search may examine worse hypothetical residuals but does not
/// mutate WorldState or create a negative financial balance. Failure to find
/// an interior candidate throws instead of returning a false clearing.
/// </summary>
public sealed class EconomicMultivarietyInteriorClearing
{
    private const int MaximumNewtonIterations = 100;
    private const int MaximumBacktracking = 36;
    private const double ResidualTolerance = 2.0e-9;
    private const double MinimumPositive = 1.0e-11;
    private readonly EconomicFoodPreferenceEvaluator _preferences = new();

    public EconomicMultivarietyInteriorClearingResult Resolve(
        IEnumerable<EconomicMultivarietyOrigin> origins,
        IEnumerable<EconomicMultivarietyBuyer> buyers,
        IEnumerable<EconomicMultivarietyDeliveredFreight> quotes,
        EconomicFoodPreferenceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(parameters);
        var source = origins.ToArray();
        var consumers = buyers.ToArray();
        var fares = quotes.ToArray();
        if (source.Length < 2 || consumers.Length < 1 ||
            source.Any(x => x is null) ||
            consumers.Any(x => x is null) ||
            fares.Any(x => x is null))
            throw new ArgumentException("Interior multivariety clearing needs at least two origins and one buyer.");
        source = source.OrderBy(x => x.OriginId.Value).ToArray();
        consumers = consumers.OrderBy(x => x.BuyerId.Value).ToArray();
        if (source.Select(x => x.OriginId).Distinct().Count() != source.Length ||
            consumers.Select(x => x.BuyerId).Distinct().Count() != consumers.Length)
            throw new ArgumentException("Origin and buyer identities must be unique.");

        var n = source.Length;
        var m = consumers.Length;
        var byOrigin = source.Select((x, j) => (x.OriginId, j))
            .ToDictionary(x => x.OriginId, x => x.j);
        var byBuyer = consumers.Select((x, i) => (x.BuyerId, i))
            .ToDictionary(x => x.BuyerId, x => x.i);
        var tariff = new double[m, n];
        var seen = new bool[m, n];
        foreach (var quote in fares)
        {
            if (!byBuyer.TryGetValue(quote.BuyerId, out var i) ||
                !byOrigin.TryGetValue(quote.OriginId, out var j) ||
                seen[i, j])
                throw new ArgumentException(
                    "Every quoted buyer/origin pair must exist exactly once.", nameof(quotes));
            seen[i, j] = true;
            tariff[i, j] = quote.FreightPerUnit;
        }
        if (fares.Length != m * n)
            throw new ArgumentException("Incomplete buyer/origin freight schedule.", nameof(quotes));

        // Fixed indices: q[buyer, origin], then H[buyer], then p[origin].
        var hBase = m * n;
        var pBase = hBase + m;
        var size = pBase + n;
        if (size > 256)
            throw new ArgumentOutOfRangeException(nameof(origins),
                "Independent reference Newton is intentionally bounded to 256 unknowns.");

        var x = new double[size];
        for (var i = 0; i < m; i++)
        {
            for (var j = 0; j < n; j++)
                x[i * n + j] = source[j].AvailableQuantity / m;
            x[hBase + i] = consumers[i].Budget * 0.55;
        }

        // Price initialization is a deterministic mean of origin-specific
        // WTP, net of the quote. It is NOT a declared origin price.
        var averagePrices = new double[n];
        for (var i = 0; i < m; i++)
        {
            var q = new double[n];
            for (var j = 0; j < n; j++) q[j] = x[i * n + j];
            var preference = _preferences.Evaluate(
                consumers[i].Population, x[hBase + i], q, parameters);
            for (var j = 0; j < n; j++)
                averagePrices[j] +=
                    (preference.MarginalWillingnessToPay[j] - tariff[i, j]) / m;
        }
        for (var j = 0; j < n; j++)
            x[pBase + j] = Math.Max(0.05, averagePrices[j]);
        var residual = new double[size];
        var trialResidual = new double[size];

        void EvaluateResidual(double[] state, double[] destination)
        {
            for (var i = 0; i < m; i++)
            {
                var q = new double[n];
                var spending = 0.0;
                for (var j = 0; j < n; j++)
                {
                    q[j] = state[i * n + j];
                    spending += q[j] * (state[pBase + j] + tariff[i, j]);
                }
                var preference = _preferences.Evaluate(
                    consumers[i].Population, state[hBase + i], q, parameters);
                for (var j = 0; j < n; j++)
                    destination[i * n + j] =
                        3.0 * (preference.MarginalWillingnessToPay[j] -
                            state[pBase + j] - tariff[i, j]);
                destination[hBase + i] =
                    (state[hBase + i] + spending - consumers[i].Budget) / 3.0;
            }
            for (var j = 0; j < n; j++)
            {
                var total = 0.0;
                for (var i = 0; i < m; i++)
                    total += state[i * n + j];
                destination[pBase + j] =
                    (total - source[j].AvailableQuantity) / 1.5;
            }
        }

        static double ErrorNorm(double[] array)
        {
            var sum = 0.0;
            foreach (var e in array) sum += e * e;
            return Math.Sqrt(sum);
        }

        EvaluateResidual(x, residual);
        var error = ErrorNorm(residual);
        var iterations = 0;
        for (; iterations < MaximumNewtonIterations && error > ResidualTolerance; iterations++)
        {
            var jacobian = new double[size, size];
            for (var k = 0; k < size; k++)
            {
                var step = 1.0e-5 * Math.Max(1.0, Math.Abs(x[k]));
                var plus = (double[])x.Clone();
                var minus = (double[])x.Clone();
                plus[k] += step;
                minus[k] -= step;
                if (minus[k] <= MinimumPositive)
                    minus[k] = x[k]; // One-sided if very near the domain boundary.
                var rp = new double[size];
                var rm = new double[size];
                EvaluateResidual(plus, rp);
                EvaluateResidual(minus, rm);
                var difference = plus[k] - minus[k];
                for (var row = 0; row < size; row++)
                    jacobian[row, k] = (rp[row] - rm[row]) / difference;
            }

            var direction = SolveLinear(jacobian, residual.Select(v => -v).ToArray());
            var accepted = false;
            var fraction = 1.0;
            for (var backtrack = 0; backtrack < MaximumBacktracking; backtrack++)
            {
                var candidate = new double[size];
                var valid = true;
                for (var k = 0; k < size; k++)
                {
                    candidate[k] = x[k] + fraction * direction[k];
                    if (!(candidate[k] > MinimumPositive) ||
                        !double.IsFinite(candidate[k]))
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid)
                {
                    EvaluateResidual(candidate, trialResidual);
                    var nextError = ErrorNorm(trialResidual);
                    if (double.IsFinite(nextError) &&
                        nextError < error * (1.0 - 1.0e-4 * fraction))
                    {
                        x = candidate;
                        (residual, trialResidual) = (trialResidual, residual);
                        error = nextError;
                        accepted = true;
                        break;
                    }
                }
                fraction /= 2.0;
            }
            if (!accepted)
                throw new InvalidOperationException(
                    "No admissible descent step: interior stationary solution was not certified.");
        }
        if (!double.IsFinite(error) || error > ResidualTolerance)
            throw new InvalidOperationException(
                "Multi-variety stationary clearing did not converge within its numerical budget.");

        var consumerResults = new EconomicMultivarietyConsumerAllocation[m];
        double maximumBudget = 0, maximumMarginal = 0, maximumSupply = 0;
        double sellerReceipts = 0, freightReceipts = 0, buyerExpenditure = 0;
        for (var i = 0; i < m; i++)
        {
            var q = new double[n];
            var price = new double[n];
            var purchase = 0.0;
            for (var j = 0; j < n; j++)
            {
                q[j] = x[i * n + j];
                price[j] = x[pBase + j] + tariff[i, j];
                purchase += q[j] * price[j];
                sellerReceipts += q[j] * x[pBase + j];
                freightReceipts += q[j] * tariff[i, j];
                buyerExpenditure += q[j] * price[j];
            }
            var h = x[hBase + i];
            var preferences = _preferences.Evaluate(
                consumers[i].Population, h, q, parameters);
            maximumBudget = Math.Max(maximumBudget,
                Math.Abs(h + purchase - consumers[i].Budget));
            for (var j = 0; j < n; j++)
                maximumMarginal = Math.Max(maximumMarginal,
                    Math.Abs(preferences.MarginalWillingnessToPay[j] - price[j]));
            consumerResults[i] = new EconomicMultivarietyConsumerAllocation(
                consumers[i].BuyerId, h, preferences.Utility, q,
                preferences.MarginalWillingnessToPay.ToArray(), price);
        }
        for (var j = 0; j < n; j++)
        {
            var total = 0.0;
            for (var i = 0; i < m; i++) total += x[i * n + j];
            maximumSupply = Math.Max(maximumSupply,
                Math.Abs(total - source[j].AvailableQuantity));
        }
        const double tolerance = 1.0e-7;
        var moneyError = Math.Abs(
            buyerExpenditure - sellerReceipts - freightReceipts);
        if (maximumBudget > tolerance ||
            maximumSupply > tolerance ||
            maximumMarginal > tolerance ||
            moneyError > tolerance * Math.Max(1, buyerExpenditure))
            throw new InvalidOperationException(
                "Stationary candidate failed independent budget, supply, WTP or payment checks.");

        return new EconomicMultivarietyInteriorClearingResult(
            source.Select(o => o.OriginId).ToArray(),
            x.Skip(pBase).Take(n).ToArray(),
            consumerResults, sellerReceipts, freightReceipts, buyerExpenditure,
            maximumBudget, maximumSupply, maximumMarginal, iterations);
    }

    // Deterministic Gaussian elimination with partial pivoting. Singular or
    // non-finite Jacobians fail closed; no random perturbation or hidden seed.
    private static double[] SolveLinear(double[,] matrix, double[] rhs)
    {
        var n = rhs.Length;
        var aug = new double[n, n + 1];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
                aug[i, j] = matrix[i, j];
            aug[i, n] = rhs[i];
        }
        for (var col = 0; col < n; col++)
        {
            var best = col;
            for (var row = col + 1; row < n; row++)
                if (Math.Abs(aug[row, col]) > Math.Abs(aug[best, col]))
                    best = row;
            if (!double.IsFinite(aug[best, col]) ||
                Math.Abs(aug[best, col]) < 1e-12)
                throw new InvalidOperationException(
                    "Singular or ill-conditioned multi-variety Jacobian.");
            if (best != col)
                for (var k = col; k <= n; k++)
                    (aug[col, k], aug[best, k]) =
                        (aug[best, k], aug[col, k]);
            for (var row = col + 1; row < n; row++)
            {
                var factor = aug[row, col] / aug[col, col];
                aug[row, col] = 0;
                for (var k = col + 1; k <= n; k++)
                    aug[row, k] -= factor * aug[col, k];
            }
        }
        var x = new double[n];
        for (var i = n - 1; i >= 0; i--)
        {
            var total = aug[i, n];
            for (var j = i + 1; j < n; j++)
                total -= aug[i, j] * x[j];
            x[i] = total / aug[i, i];
            if (!double.IsFinite(x[i]))
                throw new InvalidOperationException(
                    "Non-finite multi-variety Newton correction.");
        }
        return x;
    }
}
