using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Exact optimum of a DECLARED finite laboratory search domain, not an
/// equilibrium, game-wide optimum or authorization to execute a shipment.
/// A zero-lot solution is valid when no positive combination fits budget H.
/// </summary>
public sealed class CanonicalPrimitiveJointFreightSelection
{
    internal readonly CanonicalPrimitiveJointFreightSearchContext Context;
    public IReadOnlyList<CanonicalPrimitiveJointFreightLot> Lots { get; }
    public CanonicalPrimitiveJointFreightBudgetPreview? Preview { get; }
    public FoodTransportCostFraction FulfilledDemandScore { get; }
    public FoodTransportCostFraction TotalBuyerCommitment { get; }
    public int EvaluatedConfigurations { get; }
    public int BudgetFeasibleConfigurations { get; }
    public BigInteger PossibleConfigurations { get; }
    public ulong RawQuantum => Context.RawQuantum;
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;

    internal CanonicalPrimitiveJointFreightSelection(
        CanonicalPrimitiveJointFreightSearchContext context,
        CanonicalPrimitiveJointFreightSearchOutcome result)
    {
        Context = context;
        Lots = Array.AsReadOnly(result.Lots);
        Preview = result.Preview;
        FulfilledDemandScore = result.Score;
        TotalBuyerCommitment = result.Commitment;
        EvaluatedConfigurations = result.Evaluated;
        BudgetFeasibleConfigurations = result.Feasible;
        PossibleConfigurations = context.PossibleConfigurations;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        Context.Portfolio.RequireCurrent(session);
        if (!ReferenceEquals(Context.Session, session))
            throw new InvalidOperationException(
                "Joint selection belongs to another physical session.");
        Preview?.RequireCurrent(session);
    }

    /// <summary>
    /// Rebuilds the entire declared domain using a DIFFERENT mixed-radix
    /// enumerator, rather than accepting the selector's cached optimum.
    /// </summary>
    public void VerifyIndependent() =>
        new CanonicalPrimitiveJointFreightSelectionAuditor().Verify(this);
}

internal sealed class CanonicalPrimitiveJointFreightSearchContext
{
    public CanonicalPrimitiveFreightPortfolio Portfolio { get; }
    public CanonicalPhysicalTacticalSession Session { get; }
    public CanonicalPrimitiveFreightCandidateUse[] Candidates { get; }
    public int[] MaximumQuanta { get; }
    public ExperimentalBuyerCommodityPrice[] Prices { get; }
    public ExperimentalAbstractPurchasingPolicy Purchasing { get; }
    public ExperimentalSharedFreightCongestionPolicy Congestion { get; }
    public ulong RawQuantum { get; }
    public BigInteger PossibleConfigurations { get; }
    public IReadOnlyDictionary<(EconomicPointId, CommodityId, EconomicActivityKind), ulong> Supply { get; }
    public IReadOnlyDictionary<(EconomicPointId, CommodityId), ulong> Demand { get; }

    public CanonicalPrimitiveJointFreightSearchContext(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> inputCandidates,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasing,
        ExperimentalSharedFreightCongestionPolicy congestion,
        ulong rawQuantum, int maximumQuantaPerCandidate,
        int maximumConfigurations)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(inputCandidates);
        ArgumentNullException.ThrowIfNull(postedPrices);
        Purchasing = purchasing ?? throw new ArgumentNullException(nameof(purchasing));
        Congestion = congestion ?? throw new ArgumentNullException(nameof(congestion));
        if (rawQuantum == 0UL || maximumQuantaPerCandidate is < 1 or > 8 ||
            maximumConfigurations is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(rawQuantum),
                "Selection needs positive quantum, 1..8 quanta and at most 4096 configurations.");
        portfolio.RequireCurrent(session);
        if (purchasing.PolicyId != portfolio.PurchasingPolicyId ||
            purchasing.Version != portfolio.PurchasingPolicyVersion)
            throw new ArgumentException(
                "Selection purchasing policy must match portfolio provenance.",
                nameof(purchasing));
        var choices = inputCandidates.ToArray();
        if (choices.Length is < 1 or > CanonicalPrimitiveJointFreightSelector.MaximumCandidates ||
            choices.Any(x => x is null || !portfolio.CandidateUses.Any(y => ReferenceEquals(x, y))) ||
            choices.Distinct().Count() != choices.Length)
            throw new ArgumentException(
                "Selection requires 1..8 unique, authenticated VS64 candidates.",
                nameof(inputCandidates));
        Candidates = choices.OrderBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity)
            .ThenBy(x => x.Source.Value)
            .ThenBy(x => x.Buyer.Value)
            .ThenBy(x => x.ServiceId)
            .ThenBy(x => x.TechnologyTypeId)
            .ThenBy(x => x.TechnologyCatalogVersion).ToArray();
        Prices = postedPrices.ToArray();
        Portfolio = portfolio;
        Session = session;
        RawQuantum = rawQuantum;
        var sites = portfolio.CurrentSites;
        Supply = sites.Flows.SupplyFlows.ToDictionary(x =>
            (x.EconomicPointId, x.CommodityId, x.Activity), x => x.RawQuantity);
        Demand = sites.Flows.DemandFlows.ToDictionary(x =>
            (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);

        // Even the zero-shipment optimum must not bypass required prices,
        // residency, H or policy checks.
        _ = new ExperimentalAbstractPurchasingPowerProjector().Project(
            session, sites.Flows, sites.BuyerAnchors, sites.ProducerAnchors,
            Array.Empty<ExperimentalSimultaneousFreightLane>(), Prices, purchasing);
        MaximumQuanta = Candidates.Select(x =>
            checked((int)Math.Min(x.MaximumRawQuantity / rawQuantum,
                (ulong)maximumQuantaPerCandidate))).ToArray();
        BigInteger cases = BigInteger.One;
        foreach (var maximum in MaximumQuanta)
        {
            cases *= maximum + 1;
            if (cases > maximumConfigurations)
                throw new ArgumentException(
                    "Joint selection domain exceeds the explicit exhaustive cap; no partial optimum returned.",
                    nameof(maximumConfigurations));
        }
        PossibleConfigurations = cases;
        portfolio.RequireCurrent(session);
    }

    public CanonicalPrimitiveJointFreightBudgetPreview? Evaluate(int[] digits)
    {
        var sourceUsed = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), BigInteger>();
        var demandUsed = new Dictionary<(EconomicPointId, CommodityId), BigInteger>();
        var lots = new List<CanonicalPrimitiveJointFreightLot>();
        for (var i = 0; i < Candidates.Length; i++)
        {
            if (digits[i] == 0) continue;
            var x = Candidates[i];
            var quantity = checked((ulong)digits[i] * RawQuantum);
            var sourceKey = (x.Source, x.Commodity, x.Activity);
            var demandKey = (x.Buyer, x.Commodity);
            sourceUsed.TryGetValue(sourceKey, out var usedSource);
            demandUsed.TryGetValue(demandKey, out var usedDemand);
            sourceUsed[sourceKey] = usedSource + quantity;
            demandUsed[demandKey] = usedDemand + quantity;
            if (sourceUsed[sourceKey] > Supply[sourceKey] ||
                demandUsed[demandKey] > Demand[demandKey])
                return null; // Invalid physical batch: no speculative allocation.
            lots.Add(new CanonicalPrimitiveJointFreightLot(x, quantity));
        }
        if (lots.Count == 0) return null;
        return Portfolio.PreviewJointBudget(
            lots, Prices, Purchasing, Congestion);
    }

    public FoodTransportCostFraction Score(
        IReadOnlyList<CanonicalPrimitiveJointFreightLot> lots)
    {
        var score = new FoodTransportCostFraction(0, 1);
        foreach (var lot in lots)
        {
            var denominator = Demand[(lot.Candidate.Buyer, lot.Candidate.Commodity)];
            score = ExperimentalCashMoney.Add(score,
                new FoodTransportCostFraction(lot.RawQuantity, denominator));
        }
        return score;
    }

    public static FoodTransportCostFraction Commitment(
        CanonicalPrimitiveJointFreightBudgetPreview preview)
    {
        var cost = new FoodTransportCostFraction(0, 1);
        foreach (var row in preview.Buyers.Buyers)
            cost = ExperimentalCashMoney.Add(cost, row.TotalCommitment);
        return cost;
    }
}

internal sealed record CanonicalPrimitiveJointFreightSearchOutcome(
    CanonicalPrimitiveJointFreightLot[] Lots,
    CanonicalPrimitiveJointFreightBudgetPreview? Preview,
    FoodTransportCostFraction Score,
    FoodTransportCostFraction Commitment,
    int Evaluated, int Feasible);

public sealed class CanonicalPrimitiveJointFreightSelector
{
    public const int MaximumCandidates = 8;
    public const int MaximumConfigurations = 4096;

    /// <summary>
    /// Bounded exhaustive baseline: maximize the sum of delivered fractions
    /// of each buyer/product demand; then minimize all buyer commitments
    /// (commodity prices PLUS congestion), then retain the first candidate
    /// vector in stable canonical order. This is an explicit lab objective
    /// without claimed consumer utility or substitutability.
    /// </summary>
    public CanonicalPrimitiveJointFreightSelection Select(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> candidates,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        ulong rawQuantum, int maximumQuantaPerCandidate = 1,
        int maximumConfigurations = MaximumConfigurations)
    {
        var ctx = new CanonicalPrimitiveJointFreightSearchContext(
            portfolio, session, candidates, postedPrices, purchasingPolicy,
            congestionPolicy, rawQuantum, maximumQuantaPerCandidate,
            maximumConfigurations);
        var digits = new int[ctx.Candidates.Length];
        var bestLots = Array.Empty<CanonicalPrimitiveJointFreightLot>();
        var bestDigits = new int[ctx.Candidates.Length];
        CanonicalPrimitiveJointFreightBudgetPreview? bestPreview = null;
        var bestScore = new FoodTransportCostFraction(0, 1);
        var bestCommitment = new FoodTransportCostFraction(0, 1);
        var evaluated = 0;
        var feasible = 0;

        bool PreferEarlierServiceOnExactTie()
        {
            for (var i = 0; i < digits.Length; i++)
                if (digits[i] != bestDigits[i])
                    return digits[i] > bestDigits[i];
            return false;
        }

        void Visit(int index)
        {
            if (index < digits.Length)
            {
                for (var n = 0; n <= ctx.MaximumQuanta[index]; n++)
                {
                    digits[index] = n;
                    Visit(index + 1);
                }
                return;
            }
            evaluated++;
            var preview = ctx.Evaluate(digits);
            if (digits.Any(x => x > 0) && preview is null) return;
            if (preview is not null && !preview.IsBudgetFeasible) return;
            feasible++;
            var lots = preview?.Lots.ToArray() ??
                Array.Empty<CanonicalPrimitiveJointFreightLot>();
            var score = ctx.Score(lots);
            var cost = preview is null
                ? new FoodTransportCostFraction(0, 1)
                : CanonicalPrimitiveJointFreightSearchContext.Commitment(preview);
            if (score.CompareTo(bestScore) > 0 ||
                (score.CompareTo(bestScore) == 0 &&
                    (cost.CompareTo(bestCommitment) < 0 ||
                     (cost.CompareTo(bestCommitment) == 0 &&
                      PreferEarlierServiceOnExactTie()))))
            {
                bestDigits = (int[])digits.Clone();
                bestLots = lots;
                bestPreview = preview;
                bestScore = score;
                bestCommitment = cost;
            }
        }
        Visit(0);
        var selection = new CanonicalPrimitiveJointFreightSelection(
            ctx, new(bestLots, bestPreview, bestScore, bestCommitment,
                evaluated, feasible));
        selection.RequireCurrent(session);
        // Promotion from candidate to selected preview is gated by an
        // independent mixed-radix audit, not merely a search receipt.
        selection.VerifyIndependent();
        return selection;
    }
}

/// <summary>
/// Independent finite-domain audit using flat mixed-radix enumeration.
/// Rebuilds each physical/budget preflight and refuses an alleged optimum
/// or search count that does not match the domain. Never settles anything.
/// </summary>
public sealed class CanonicalPrimitiveJointFreightSelectionAuditor
{
    public void Verify(CanonicalPrimitiveJointFreightSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var ctx = selection.Context;
        selection.RequireCurrent(ctx.Session);
        var bestLots = Array.Empty<CanonicalPrimitiveJointFreightLot>();
        var bestDigits = new int[ctx.Candidates.Length];
        var bestScore = new FoodTransportCostFraction(0, 1);
        var bestCost = new FoodTransportCostFraction(0, 1);
        var feasible = 0;
        var evaluated = 0;
        for (var ordinal = BigInteger.Zero; ordinal < ctx.PossibleConfigurations; ordinal++)
        {
            // Same lexicographic domain as recursive DFS, but reconstructed
            // independently from the flat ordinal, last digit fastest.
            var digits = new int[ctx.Candidates.Length];
            var remainder = ordinal;
            for (var i = digits.Length - 1; i >= 0; i--)
            {
                var radix = ctx.MaximumQuanta[i] + 1;
                digits[i] = (int)(remainder % radix);
                remainder /= radix;
            }
            evaluated++;
            var preview = ctx.Evaluate(digits);
            if (digits.Any(x => x > 0) && preview is null) continue;
            if (preview is not null && !preview.IsBudgetFeasible) continue;
            feasible++;
            var lots = preview?.Lots.ToArray() ??
                Array.Empty<CanonicalPrimitiveJointFreightLot>();
            var score = ctx.Score(lots);
            var cost = preview is null
                ? new FoodTransportCostFraction(0, 1)
                : CanonicalPrimitiveJointFreightSearchContext.Commitment(preview);
            // Independent canonical tie check: prefer greater quantity at
            // the earliest candidate/service in sorted order.
            var preferredTie = false;
            if (score.CompareTo(bestScore) == 0 && cost.CompareTo(bestCost) == 0)
                for (var i = 0; i < digits.Length; i++)
                {
                    if (digits[i] == bestDigits[i]) continue;
                    preferredTie = digits[i] > bestDigits[i];
                    break;
                }
            if (score.CompareTo(bestScore) > 0 ||
                (score.CompareTo(bestScore) == 0 &&
                    (cost.CompareTo(bestCost) < 0 || preferredTie)))
            {
                bestDigits = (int[])digits.Clone();
                bestLots = lots;
                bestScore = score;
                bestCost = cost;
            }
        }
        if (evaluated != selection.EvaluatedConfigurations ||
            feasible != selection.BudgetFeasibleConfigurations ||
            evaluated != (int)ctx.PossibleConfigurations ||
            selection.PossibleConfigurations != ctx.PossibleConfigurations ||
            selection.FulfilledDemandScore.CompareTo(bestScore) != 0 ||
            selection.TotalBuyerCommitment.CompareTo(bestCost) != 0 ||
            selection.Lots.Count != bestLots.Length ||
            selection.Lots.Where((x, i) =>
                !ReferenceEquals(x.Candidate, bestLots[i].Candidate) ||
                x.RawQuantity != bestLots[i].RawQuantity).Any() ||
            (selection.Preview is null) != (bestLots.Length == 0))
            throw new InvalidOperationException(
                "Independent joint freight audit rejected the claimed finite-domain optimum.");
        selection.RequireCurrent(ctx.Session);
    }
}
