using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// One named, local buyer-and-commodity POSTED PRICE hypothesis. The change
/// is a shadow price, not a subsidy, transaction, production shock or wallet.
/// H and physical congestion policy are identical across hypotheses.
/// </summary>
public sealed record CanonicalPrimitiveLocalPriceShock(
    string ScenarioId,
    EconomicPointId Buyer,
    CommodityId Commodity,
    FoodTransportCostFraction NewPricePerWholeFlowUnit);

public sealed record CanonicalPrimitiveLocalPriceImpact(
    CanonicalPrimitiveLocalPriceShock Shock,
    FoodTransportCostFraction BaselinePricePerWholeFlowUnit,
    IReadOnlyList<int> CoupledComponentIndices,
    int VerifiedUnchangedComponents,
    CanonicalPrimitiveFreightScenarioOutcome Outcome)
{
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;
}

/// <summary>
/// VS69: isolates the causal scope of finite regional price hypotheses.
/// Every comparison uses the SAME signed candidate portfolio, physical
/// network, resident purchasing/H policy and congestion assumptions.
/// An independent VS67 component must be unchanged by price perturbations
/// outside that component. Connected markets are re-solved jointly, without
/// asserting a fictitious no-spillover guarantee within a shared corridor.
/// </summary>
public sealed class CanonicalPrimitiveLocalPriceComparison
{
    public const int MaximumShocks = 5;
    public const string BaselineScenarioId = "baseline";

    private readonly CanonicalPrimitiveFreightPortfolio _portfolio;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly ExperimentalBuyerCommodityPrice[] _baselinePrices;
    private readonly CanonicalPrimitiveLocalPriceShock[] _shocks;

    public CanonicalPrimitiveFreightScenarioComparison Scenarios { get; }
    public IReadOnlyList<CanonicalPrimitiveLocalPriceImpact> Impacts { get; }
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;

    private CanonicalPrimitiveLocalPriceComparison(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        ExperimentalBuyerCommodityPrice[] prices,
        CanonicalPrimitiveLocalPriceShock[] shocks,
        CanonicalPrimitiveFreightScenarioComparison scenarios,
        CanonicalPrimitiveLocalPriceImpact[] impacts)
    {
        _portfolio = portfolio;
        _session = session;
        _baselinePrices = (ExperimentalBuyerCommodityPrice[])prices.Clone();
        _shocks = (CanonicalPrimitiveLocalPriceShock[])shocks.Clone();
        Scenarios = scenarios;
        Impacts = Array.AsReadOnly(impacts);
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Localized price comparisons cannot migrate to another session.");
        _portfolio.RequireCurrent(session);
        Scenarios.RequireCurrent(session);
    }

    private static bool SameLots(
        IReadOnlyList<CanonicalPrimitiveJointFreightLot> a,
        IReadOnlyList<CanonicalPrimitiveJointFreightLot> b) =>
        a.Count == b.Count && !a.Where((lot, i) =>
            !ReferenceEquals(lot.Candidate, b[i].Candidate) ||
            lot.RawQuantity != b[i].RawQuantity).Any();

    private static bool SameCorridorLoads(
        CanonicalPrimitiveJointFreightBudgetPreview? a,
        CanonicalPrimitiveJointFreightBudgetPreview? b)
    {
        if (a is null || b is null) return a is null && b is null;
        var left = a.Freight.CorridorLoads;
        var right = b.Freight.CorridorLoads;
        return left.Count == right.Count &&
            left.All(x => right.TryGetValue(x.Key, out var q) && q == x.Value);
    }

    /// <summary>
    /// Verify the exact single-key price replacement for each scenario and
    /// the unaffected-component invariance separately from VS68's full
    /// re-enumeration audit. No inference from scenario labels is trusted.
    /// </summary>
    public void VerifyLocalizedImpacts()
    {
        RequireCurrent(_session);
        Scenarios.VerifyRecomputed();
        if (Impacts.Count != _shocks.Length)
            throw new InvalidOperationException(
                "The local shock ledger does not cover every scenario.");
        var baseline = Scenarios.Baseline.Selection;
        var prices = _baselinePrices.ToDictionary(
            x => (x.Buyer, x.Commodity), x => x.PricePerWholeFlowUnit);
        foreach (var row in Impacts)
        {
            var shock = row.Shock;
            if (!_shocks.Any(x => x == shock) ||
                !prices.TryGetValue((shock.Buyer, shock.Commodity), out var oldPrice) ||
                row.BaselinePricePerWholeFlowUnit.CompareTo(oldPrice) != 0)
                throw new InvalidOperationException(
                    "The local shock has no authenticated baseline price.");
            var outcome = Scenarios.Outcomes.Single(x =>
                x.Scenario.ScenarioId == shock.ScenarioId);
            if (!ReferenceEquals(row.Outcome, outcome))
                throw new InvalidOperationException(
                    "Local shock reports a different scenario receipt.");
            var scenarioPrices = outcome.Scenario.PostedPrices.ToDictionary(
                x => (x.Buyer, x.Commodity), x => x.PricePerWholeFlowUnit);
            if (scenarioPrices.Count != prices.Count ||
                prices.Any(kv => !scenarioPrices.TryGetValue(kv.Key, out var actual) ||
                    actual.CompareTo(kv.Key == (shock.Buyer, shock.Commodity)
                        ? shock.NewPricePerWholeFlowUnit : kv.Value) != 0))
                throw new InvalidOperationException(
                    "A regional price hypothesis modified other buyer/commodity prices.");

            var coupled = new List<int>();
            var unchanged = 0;
            var selected = outcome.Selection;
            if (selected.Components.Count != baseline.Components.Count)
                throw new InvalidOperationException(
                    "A local price change unexpectedly changed physical component boundaries.");
            for (var i = 0; i < baseline.Components.Count; i++)
            {
                var a = baseline.Components[i];
                var b = selected.Components[i];
                if (!a.Context.Candidates.SequenceEqual(b.Context.Candidates))
                    throw new InvalidOperationException(
                        "A local price hypothesis changed institutional candidates.");
                if (a.Context.Candidates.Any(x => x.Buyer == shock.Buyer))
                {
                    coupled.Add(i);
                    continue;
                }
                if (a.FulfilledDemandScore.CompareTo(b.FulfilledDemandScore) != 0 ||
                    a.TotalBuyerCommitment.CompareTo(b.TotalBuyerCommitment) != 0 ||
                    !SameLots(a.Lots, b.Lots) ||
                    !SameCorridorLoads(a.Preview, b.Preview))
                    throw new InvalidOperationException(
                        "Price pressure propagated across independently proven buyer/corridor components.");
                unchanged++;
            }
            if (!coupled.SequenceEqual(row.CoupledComponentIndices) ||
                unchanged != row.VerifiedUnchangedComponents)
                throw new InvalidOperationException(
                    "Local-impact component certificate does not match actual physical coupling.");
        }
        RequireCurrent(_session);
    }

    public static CanonicalPrimitiveLocalPriceComparison FromPortfolio(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> candidates,
        IEnumerable<ExperimentalBuyerCommodityPrice> baselinePrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        IEnumerable<CanonicalPrimitiveLocalPriceShock> shocks,
        ulong rawQuantum, int maximumQuantaPerCandidate = 1,
        int maximumConfigurationsPerComponent = 4096)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(baselinePrices);
        ArgumentNullException.ThrowIfNull(purchasingPolicy);
        ArgumentNullException.ThrowIfNull(congestionPolicy);
        ArgumentNullException.ThrowIfNull(shocks);
        portfolio.RequireCurrent(session);

        var selected = candidates.ToArray();
        var original = baselinePrices.ToArray();
        var trial = shocks.ToArray();
        if (trial.Length is < 1 or > MaximumShocks ||
            trial.Any(x => x is null ||
                string.IsNullOrWhiteSpace(x.ScenarioId) ||
                x.ScenarioId.Length > 64 ||
                x.ScenarioId == BaselineScenarioId ||
                !x.Buyer.IsValid || !x.Commodity.IsValid ||
                x.NewPricePerWholeFlowUnit is null) ||
            trial.Select(x => x.ScenarioId)
                .Distinct(StringComparer.Ordinal).Count() != trial.Length)
            throw new ArgumentException(
                "Localized comparisons require 1..5 complete uniquely named nonbaseline shocks.",
                nameof(shocks));
        var byKey = new Dictionary<(EconomicPointId, CommodityId),
            ExperimentalBuyerCommodityPrice>();
        foreach (var price in original)
            if (price is null || !byKey.TryAdd((price.Buyer, price.Commodity), price))
                throw new ArgumentException(
                    "Baseline prices must be unique and non-null.",
                    nameof(baselinePrices));
        foreach (var shock in trial)
            if (!byKey.ContainsKey((shock.Buyer, shock.Commodity)))
                throw new ArgumentException(
                    "A local shock must target a real buyer/item from the baseline.",
                    nameof(shocks));

        var orderedPrices = byKey.Values.OrderBy(x => x.Buyer.Value)
            .ThenBy(x => x.Commodity.Value).ToArray();
        var orderedShocks = trial.OrderBy(x => x.ScenarioId,
            StringComparer.Ordinal).ToArray();
        var cases = new List<CanonicalPrimitiveFreightScenario> {
            new(BaselineScenarioId, Array.AsReadOnly(orderedPrices),
                purchasingPolicy, congestionPolicy)
        };
        foreach (var shock in orderedShocks)
        {
            var alternative = orderedPrices.Select(x =>
                x.Buyer == shock.Buyer && x.Commodity == shock.Commodity
                    ? new ExperimentalBuyerCommodityPrice(
                        x.Buyer, x.Commodity, shock.NewPricePerWholeFlowUnit)
                    : x).ToArray();
            cases.Add(new(shock.ScenarioId, Array.AsReadOnly(alternative),
                purchasingPolicy, congestionPolicy));
        }
        var comparison = portfolio.CompareFreightScenarios(
            selected, cases, BaselineScenarioId, rawQuantum,
            maximumQuantaPerCandidate, maximumConfigurationsPerComponent);
        var baseline = comparison.Baseline.Selection;
        var impacts = orderedShocks.Select(shock =>
        {
            var changed = comparison.Outcomes.Single(x =>
                x.Scenario.ScenarioId == shock.ScenarioId);
            var coupled = baseline.Components.Select((x, i) => (x, i))
                .Where(x => x.x.Context.Candidates.Any(c => c.Buyer == shock.Buyer))
                .Select(x => x.i).ToArray();
            return new CanonicalPrimitiveLocalPriceImpact(
                shock, byKey[(shock.Buyer, shock.Commodity)].PricePerWholeFlowUnit,
                Array.AsReadOnly(coupled), baseline.Components.Count - coupled.Length,
                changed);
        }).ToArray();
        var result = new CanonicalPrimitiveLocalPriceComparison(
            portfolio, session, orderedPrices, orderedShocks, comparison, impacts);
        result.VerifyLocalizedImpacts();
        return result;
    }
}
