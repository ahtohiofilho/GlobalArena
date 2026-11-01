using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A named, explicit SHADOW hypothesis. Different versions of nominal prices,
/// purchasing/H and congestion are alternatives, never cumulative cycles,
/// budget updates or institutional permissions. The policy identifier/version
/// must remain compatible with the authentic VS64 portfolio provenance.
/// </summary>
public sealed record CanonicalPrimitiveFreightScenario(
    string ScenarioId,
    IReadOnlyList<ExperimentalBuyerCommodityPrice> PostedPrices,
    ExperimentalAbstractPurchasingPolicy PurchasingPolicy,
    ExperimentalSharedFreightCongestionPolicy CongestionPolicy);

public sealed record CanonicalPrimitiveFreightScenarioOutcome(
    CanonicalPrimitiveFreightScenario Scenario,
    CanonicalPrimitiveFreightComponentSelection Selection,
    int FulfilledDemandDirectionVsBaseline,
    int BuyerCommitmentDirectionVsBaseline)
{
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;
}

/// <summary>
/// One non-mutating comparison of mutually exclusive laboratory assumptions.
/// Scenario labels have no authority to change state or authorize SETTLE.
/// Every row is separately authenticated/audited using VS67, which itself
/// audits VS66 and reconciles buyer H / physical corridor costs with VS65.
/// We do NOT rank economic welfare across differently priced hypotheses.
/// </summary>
public sealed class CanonicalPrimitiveFreightScenarioComparison
{
    public const int MaximumScenarios = 6;
    private readonly CanonicalPrimitiveFreightPortfolio _portfolio;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveFreightCandidateUse[] _candidates;
    private readonly ulong _rawQuantum;
    private readonly int _maximumQuantaPerCandidate;
    private readonly int _maximumConfigurationsPerComponent;

    public string BaselineScenarioId { get; }
    public IReadOnlyList<CanonicalPrimitiveFreightScenarioOutcome> Outcomes { get; }
    public CanonicalPrimitiveFreightScenarioOutcome Baseline =>
        Outcomes.Single(x => x.Scenario.ScenarioId == BaselineScenarioId);
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;

    private CanonicalPrimitiveFreightScenarioComparison(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveFreightCandidateUse[] candidates,
        string baselineScenarioId,
        CanonicalPrimitiveFreightScenarioOutcome[] outcomes,
        ulong rawQuantum, int maximumQuantaPerCandidate,
        int maximumConfigurationsPerComponent)
    {
        _portfolio = portfolio;
        _session = session;
        _candidates = (CanonicalPrimitiveFreightCandidateUse[])candidates.Clone();
        BaselineScenarioId = baselineScenarioId;
        Outcomes = Array.AsReadOnly(outcomes);
        _rawQuantum = rawQuantum;
        _maximumQuantaPerCandidate = maximumQuantaPerCandidate;
        _maximumConfigurationsPerComponent = maximumConfigurationsPerComponent;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Scenario comparison cannot be reused in another physical session.");
        _portfolio.RequireCurrent(session);
        foreach (var row in Outcomes) row.Selection.RequireCurrent(session);
    }

    /// <summary>
    /// Re-evaluate each scenario from its snapshotted posted prices, H policy
    /// and congestion inputs, and compare exact financial, physical and
    /// selected-lot results. The underlying VS66 optimum is independently
    /// re-enumerated by VS67. This is replay verification, not an authority
    /// to settle or a new general economic solver.
    /// </summary>
    public void VerifyRecomputed()
    {
        RequireCurrent(_session);
        var baseline = Baseline.Selection;
        foreach (var row in Outcomes)
        {
            row.Selection.VerifyIndependent();
            var replay = _portfolio.SelectIndependentFreightComponents(
                _candidates, row.Scenario.PostedPrices,
                row.Scenario.PurchasingPolicy, row.Scenario.CongestionPolicy,
                _rawQuantum, _maximumQuantaPerCandidate,
                _maximumConfigurationsPerComponent);
            if (row.Selection.IndependentComponentCount != replay.IndependentComponentCount ||
                row.Selection.PossibleGlobalConfigurations != replay.PossibleGlobalConfigurations ||
                row.Selection.EvaluatedComponentConfigurations != replay.EvaluatedComponentConfigurations ||
                row.Selection.FulfilledDemandScore.CompareTo(replay.FulfilledDemandScore) != 0 ||
                row.Selection.TotalBuyerCommitment.CompareTo(replay.TotalBuyerCommitment) != 0 ||
                row.Selection.Lots.Count != replay.Lots.Count ||
                row.Selection.Lots.Where((lot, i) =>
                    !ReferenceEquals(lot.Candidate, replay.Lots[i].Candidate) ||
                    lot.RawQuantity != replay.Lots[i].RawQuantity).Any() ||
                (row.Selection.Preview is null) != (replay.Preview is null))
                throw new InvalidOperationException(
                    "Scenario replay differs in selected physical lots or nominal budget.");
            if (row.Selection.Preview is { } original)
            {
                var repeated = replay.Preview!;
                if (original.Freight.TotalCost.CompareTo(repeated.Freight.TotalCost) != 0 ||
                    original.Freight.CorridorLoads.Count != repeated.Freight.CorridorLoads.Count ||
                    original.Freight.CorridorLoads.Any(x =>
                        !repeated.Freight.CorridorLoads.TryGetValue(x.Key, out var q) ||
                        q != x.Value) ||
                    !original.Buyers.Buyers.Select(x => (
                        x.Buyer, x.GrossCash.Numerator, x.GrossCash.Denominator,
                        x.ProtectedResidual.Numerator, x.ProtectedResidual.Denominator,
                        x.TotalCommitment.Numerator, x.TotalCommitment.Denominator))
                      .SequenceEqual(repeated.Buyers.Buyers.Select(x => (
                        x.Buyer, x.GrossCash.Numerator, x.GrossCash.Denominator,
                        x.ProtectedResidual.Numerator, x.ProtectedResidual.Denominator,
                        x.TotalCommitment.Numerator, x.TotalCommitment.Denominator))))
                    throw new InvalidOperationException(
                        "Scenario replay diverged in shared corridor loads, H or buyer commitments.");
            }
            if (row.FulfilledDemandDirectionVsBaseline != Math.Sign(
                    row.Selection.FulfilledDemandScore.CompareTo(
                        baseline.FulfilledDemandScore)) ||
                row.BuyerCommitmentDirectionVsBaseline != Math.Sign(
                    row.Selection.TotalBuyerCommitment.CompareTo(
                        baseline.TotalBuyerCommitment)))
                throw new InvalidOperationException(
                    "Reported scenario deltas contradict the baseline.");
        }
        RequireCurrent(_session);
    }

    public static CanonicalPrimitiveFreightScenarioComparison FromPortfolio(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> candidates,
        IEnumerable<CanonicalPrimitiveFreightScenario> scenarios,
        string baselineScenarioId,
        ulong rawQuantum, int maximumQuantaPerCandidate = 1,
        int maximumConfigurationsPerComponent = 4096)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(scenarios);
        portfolio.RequireCurrent(session);
        if (string.IsNullOrWhiteSpace(baselineScenarioId))
            throw new ArgumentException(
                "A named, explicit baseline is mandatory.", nameof(baselineScenarioId));
        var choices = candidates.ToArray();
        // Copy and freeze lazy caller-provided scenario inputs before any
        // expensive analysis. Do not snapshot only the list reference.
        var rawScenarios = scenarios.ToArray();
        if (rawScenarios.Length is < 1 or > MaximumScenarios ||
            rawScenarios.Any(x => x is null || x.PurchasingPolicy is null ||
                x.CongestionPolicy is null || x.PostedPrices is null ||
                string.IsNullOrWhiteSpace(x.ScenarioId) ||
                x.ScenarioId.Length > 64) ||
            rawScenarios.Select(x => x.ScenarioId)
                .Distinct(StringComparer.Ordinal).Count() != rawScenarios.Length)
            throw new ArgumentException(
                "Scenario comparison requires 1..6 unique named, complete hypothetical scenarios.",
                nameof(scenarios));
        var frozen = rawScenarios.Select(x => x with {
            PostedPrices = Array.AsReadOnly(x.PostedPrices.ToArray())
        }).OrderBy(x => x.ScenarioId, StringComparer.Ordinal).ToArray();
        if (!frozen.Any(x => x.ScenarioId == baselineScenarioId))
            throw new ArgumentException(
                "The declared baseline must be present in the same finite scenario set.",
                nameof(baselineScenarioId));
        foreach (var s in frozen)
            if (s.PostedPrices.Any(x => x is null) ||
                s.PurchasingPolicy.PolicyId != portfolio.PurchasingPolicyId ||
                s.PurchasingPolicy.Version != portfolio.PurchasingPolicyVersion)
                throw new ArgumentException(
                    "Every shadow policy must match the portfolio identity/version, with complete posted prices.",
                    nameof(scenarios));

        // Scenarios are mutually exclusive. We do NOT debit one scenario
        // against another, change the world, or select the 'best policy'.
        var solved = frozen.Select(s => (
            Scenario: s,
            Selection: portfolio.SelectIndependentFreightComponents(
                choices, s.PostedPrices, s.PurchasingPolicy,
                s.CongestionPolicy, rawQuantum,
                maximumQuantaPerCandidate,
                maximumConfigurationsPerComponent))).ToArray();
        var baseline = solved.Single(x => x.Scenario.ScenarioId == baselineScenarioId)
            .Selection;
        var outcomes = solved.Select(x =>
            new CanonicalPrimitiveFreightScenarioOutcome(
                x.Scenario, x.Selection,
                Math.Sign(x.Selection.FulfilledDemandScore.CompareTo(
                    baseline.FulfilledDemandScore)),
                Math.Sign(x.Selection.TotalBuyerCommitment.CompareTo(
                    baseline.TotalBuyerCommitment)))).ToArray();
        var result = new CanonicalPrimitiveFreightScenarioComparison(
            portfolio, session, choices, baselineScenarioId, outcomes,
            rawQuantum, maximumQuantaPerCandidate,
            maximumConfigurationsPerComponent);
        result.VerifyRecomputed();
        return result;
    }
}
