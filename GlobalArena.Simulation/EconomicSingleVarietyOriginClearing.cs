using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One buyer of ONE homogeneous origin variety in a strictly hypothetical
/// GA-PROP-001 stationary food market. Budget and freight are provided in
/// whole monetary units, quantity in whole physical flow units. No inference
/// from raw fixed-point balances or bank accounts is permitted.
/// </summary>
public sealed class EconomicSingleVarietyBuyer
{
    public EconomicPointId PointId { get; }
    public double Population { get; }
    public double Budget { get; }
    public double FreightPerUnit { get; }

    public EconomicSingleVarietyBuyer(
        EconomicPointId pointId, double population,
        double budget, double freightPerUnit)
    {
        if (!pointId.IsValid ||
            !double.IsFinite(population) || population <= 0 ||
            !double.IsFinite(budget) || budget <= 0 ||
            !double.IsFinite(freightPerUnit) || freightPerUnit < 0)
            throw new ArgumentOutOfRangeException(
                nameof(pointId), "Buyer identity and economic inputs must be valid.");

        PointId = pointId;
        Population = population;
        Budget = budget;
        FreightPerUnit = freightPerUnit;
    }
}

public sealed class EconomicSingleVarietyBuyerClearing
{
    public EconomicPointId PointId { get; }
    public double PurchasedQuantity { get; }
    public double MonetaryResidual { get; }
    public double DeliveredPrice { get; }
    public double MarginalWillingnessToPay { get; }
    public double CommodityExpenditure { get; }
    public double FreightExpenditure { get; }
    public double Utility { get; }

    internal EconomicSingleVarietyBuyerClearing(
        EconomicSingleVarietyBuyer buyer, double q, double h,
        double deliveredPrice, EconomicFoodPreferenceResult preferences)
    {
        PointId = buyer.PointId;
        PurchasedQuantity = q;
        MonetaryResidual = h;
        DeliveredPrice = deliveredPrice;
        MarginalWillingnessToPay = preferences.MarginalWillingnessToPay[0];
        CommodityExpenditure = (deliveredPrice - buyer.FreightPerUnit) * q;
        FreightExpenditure = buyer.FreightPerUnit * q;
        Utility = preferences.Utility;
    }
}

/// <summary>
/// Fixed-supply single-variety specialization of GA-PROP-001.
/// The price p_origin is ENDOGENOUS: each consumer chooses q to equate
/// canonical GA-PROP-001 marginal monetary willingness to pay with
/// p_origin + its explicitly quoted freight, subject to its own budget.
/// Price is found by monotone aggregate excess-demand bisection.
/// If demand at p_origin=0 is insufficient, goods remain UNSOLD.
/// No negative origin prices, budget borrowing, or mandatory full sales.
/// This does not solve multi-variety, multi-origin, density-cost feedback,
/// social-planner selection, cash ledger, or a production kernel turn.
/// </summary>
public sealed class EconomicSingleVarietyOriginClearingResult
{
    public double Supply { get; }
    public double OriginPrice { get; }
    public double Sold { get; }
    public double Unsold { get; }
    public double SellerReceipts { get; }
    public double FreightReceipts { get; }
    public double TotalBuyerExpenditure { get; }
    public double MaximumBudgetResidual { get; }
    public double MaximumMarginalConditionResidual { get; }
    public IReadOnlyList<EconomicSingleVarietyBuyerClearing> Buyers { get; }

    internal EconomicSingleVarietyOriginClearingResult(
        double supply, double price,
        EconomicSingleVarietyBuyerClearing[] buyers,
        double budgetResidual, double marginalResidual)
    {
        Supply = supply;
        OriginPrice = price;
        Buyers = Array.AsReadOnly(buyers);
        Sold = buyers.Sum(b => b.PurchasedQuantity);
        Unsold = Math.Max(0.0, supply - Sold);
        SellerReceipts = price * Sold;
        FreightReceipts = buyers.Sum(b => b.FreightExpenditure);
        TotalBuyerExpenditure = buyers.Sum(
            b => b.CommodityExpenditure + b.FreightExpenditure);
        MaximumBudgetResidual = budgetResidual;
        MaximumMarginalConditionResidual = marginalResidual;
    }
}

/// <summary>
/// Bounded deterministic bisection reference (96 iterations for each
/// monotone 1-D inversion); no external optimization dependency. The
/// n=1 preference specialization has D=0 and WTP decreases strictly with
/// q at a fixed delivered price. This monotonicity does NOT extend without
/// proof to the full interacting-variety market.
/// </summary>
public sealed class EconomicSingleVarietyOriginClearing
{
    private const int Bisections = 96;
    private readonly EconomicFoodPreferenceEvaluator _preference = new();

    public EconomicSingleVarietyOriginClearingResult Resolve(
        double supply,
        IEnumerable<EconomicSingleVarietyBuyer> buyers,
        EconomicFoodPreferenceParameters parameters)
    {
        if (!double.IsFinite(supply) || supply <= 0)
            throw new ArgumentOutOfRangeException(nameof(supply));
        ArgumentNullException.ThrowIfNull(buyers);
        ArgumentNullException.ThrowIfNull(parameters);

        var ordered = buyers.ToArray();
        if (ordered.Length == 0 || ordered.Any(b => b is null))
            throw new ArgumentException("At least one valid buyer is required.", nameof(buyers));
        ordered = ordered.OrderBy(b => b.PointId.Value).ToArray();
        for (var index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].PointId == ordered[index].PointId)
                throw new ArgumentException("Duplicate buyer identity.", nameof(buyers));

        // Each origin price at/above this bound eliminates all demand.
        // It is a mathematically safe positive bracket; tariffs never become
        // a negative origin price or an extension of the lender's balance.
        var ceiling = ordered.Max(b =>
            Math.Max(0.0, WillingnessAtZero(b, parameters) - b.FreightPerUnit));
        if (!double.IsFinite(ceiling))
            throw new InvalidOperationException("Price bracket overflow.");

        double TotalDemand(double price)
        {
            double total = 0;
            foreach (var buyer in ordered)
                total += ConsumerDemand(buyer, supply, price, parameters);
            return total;
        }

        double price;
        var atZero = TotalDemand(0.0);
        if (atZero < supply)
            price = 0.0; // Unsold stock. No artificial negative price.
        else
        {
            var lower = 0.0;
            var upper = ceiling;
            for (var iteration = 0; iteration < Bisections; iteration++)
            {
                var mid = lower + (upper - lower) / 2.0;
                if (mid <= lower || mid >= upper)
                    break; // IEEE-754 adjacent prices; never silently iterate forever.
                if (TotalDemand(mid) > supply)
                    lower = mid;
                else
                    upper = mid;
            }
            price = upper;
        }

        var cleared = new EconomicSingleVarietyBuyerClearing[ordered.Length];
        double maxBudgetResidual = 0.0;
        double maxMarginalResidual = 0.0;
        for (var i = 0; i < ordered.Length; i++)
        {
            var buyer = ordered[i];
            var q = ConsumerDemand(buyer, supply, price, parameters);
            var delivered = price + buyer.FreightPerUnit;
            var h = buyer.Budget - delivered * q;
            if (!(h > 0))
                throw new InvalidOperationException("Market clearing violates positive residual H.");
            var pref = _preference.Evaluate(
                buyer.Population, h, new[] { q }, parameters);
            cleared[i] = new EconomicSingleVarietyBuyerClearing(
                buyer, q, h, delivered, pref);
            var residual = Math.Abs(h + delivered * q - buyer.Budget);
            maxBudgetResidual = Math.Max(maxBudgetResidual, residual);
            if (q > 1e-10 && q < supply - 1e-10)
                maxMarginalResidual = Math.Max(
                    maxMarginalResidual, Math.Abs(
                        pref.MarginalWillingnessToPay[0] - delivered));
            if (q <= 1e-10 && pref.MarginalWillingnessToPay[0] > delivered + 1e-7)
                throw new InvalidOperationException(
                    "Rejected goods have marginal WTP above delivered price.");
        }
        var result = new EconomicSingleVarietyOriginClearingResult(
            supply, price, cleared, maxBudgetResidual, maxMarginalResidual);
        var tol = 1e-8 * Math.Max(1.0, supply);
        if (result.Sold > supply + tol ||
            (price > 0 && Math.Abs(result.Sold - supply) > tol) ||
            result.Unsold < 0 ||
            Math.Abs(result.TotalBuyerExpenditure -
                result.SellerReceipts - result.FreightReceipts) >
                1e-8 * Math.Max(1.0, result.TotalBuyerExpenditure))
            throw new InvalidOperationException(
                "Reference clearing did not reconcile its goods and payments.");
        return result;
    }

    private double WillingnessAtZero(
        EconomicSingleVarietyBuyer buyer,
        EconomicFoodPreferenceParameters parameters) =>
        _preference.Evaluate(
            buyer.Population, buyer.Budget, new[] { 0.0 }, parameters)
            .MarginalWillingnessToPay[0];

    private double ConsumerDemand(
        EconomicSingleVarietyBuyer buyer, double supply,
        double originPrice, EconomicFoodPreferenceParameters parameters)
    {
        var delivered = originPrice + buyer.FreightPerUnit;
        if (!double.IsFinite(delivered))
            throw new InvalidOperationException("Delivered price overflow.");
        var initialWtp = WillingnessAtZero(buyer, parameters);
        if (initialWtp <= delivered)
            return 0.0;
        if (delivered == 0.0)
            return supply; // This ONE buyer would demand at least all stock.

        var upper = Math.Min(supply, buyer.Budget / delivered);
        if (upper <= 0.0)
            return 0.0;
        var hAtUpper = buyer.Budget - delivered * upper;
        if (hAtUpper > 0 &&
            _preference.Evaluate(buyer.Population, hAtUpper,
                new[] { upper }, parameters)
                .MarginalWillingnessToPay[0] >= delivered)
            return upper;

        var lower = 0.0;
        for (var iteration = 0; iteration < Bisections; iteration++)
        {
            var middle = lower + (upper - lower) / 2.0;
            if (middle <= lower || middle >= upper)
                break;
            var h = buyer.Budget - delivered * middle;
            var wtp = h <= 0.0 ? 0.0 :
                _preference.Evaluate(buyer.Population, h,
                    new[] { middle }, parameters)
                .MarginalWillingnessToPay[0];
            if (wtp > delivered)
                lower = middle;
            else
                upper = middle;
        }
        return lower;
    }
}
