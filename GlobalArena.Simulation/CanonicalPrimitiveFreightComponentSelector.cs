using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Diagnostic-only coupling declaration. User-created descriptors never
/// authorize routes: the live selector constructs these from the exact
/// authenticated VS63/VS64 service and installed asset evidence.
/// </summary>
public sealed record CanonicalPrimitiveFreightCouplingDescriptor(
    CanonicalPrimitiveFreightCandidateUse Candidate,
    IReadOnlyList<ExperimentalSharedFreightCorridorKey> Corridors,
    IReadOnlyList<ulong> AssetIds);

/// <summary>
/// Conservative factorization certificate. Separate connected components
/// share NO buyer/H, supply flow, service, installed asset or physical
/// congestion corridor. Separating by commodity alone is never sufficient.
/// </summary>
public static class CanonicalPrimitiveFreightCouplingPlanner
{
    public static bool Coupled(
        CanonicalPrimitiveFreightCouplingDescriptor a,
        CanonicalPrimitiveFreightCouplingDescriptor b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Candidate is null || b.Candidate is null ||
            a.Corridors is null || b.Corridors is null ||
            a.AssetIds is null || b.AssetIds is null)
            throw new ArgumentException("Coupling requires complete candidate and route evidence.");
        return a.Candidate.Buyer == b.Candidate.Buyer ||
            (a.Candidate.Source == b.Candidate.Source &&
             a.Candidate.Commodity == b.Candidate.Commodity &&
             a.Candidate.Activity == b.Candidate.Activity) ||
            a.Candidate.ServiceId == b.Candidate.ServiceId ||
            a.AssetIds.Intersect(b.AssetIds).Any() ||
            a.Corridors.Intersect(b.Corridors).Any();
    }

    public static IReadOnlyList<IReadOnlyList<int>> Partition(
        IEnumerable<CanonicalPrimitiveFreightCouplingDescriptor> input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var rows = input.ToArray();
        if (rows.Length is < 1 or > 12 ||
            rows.Any(x => x is null || x.Candidate is null ||
                x.Corridors is null || x.AssetIds is null))
            throw new ArgumentException("Coupling planner needs 1..12 complete candidates.");
        var parent = Enumerable.Range(0, rows.Length).ToArray();
        int Root(int i)
        {
            while (parent[i] != i) i = parent[i];
            return i;
        }
        for (var i = 0; i < rows.Length; i++)
            for (var j = i + 1; j < rows.Length; j++)
                if (Coupled(rows[i], rows[j]))
                    parent[Root(j)] = Root(i);
        var groups = Enumerable.Range(0, rows.Length)
            .GroupBy(Root)
            .Select(g => (IReadOnlyList<int>)Array.AsReadOnly(g.OrderBy(x => x).ToArray()))
            .OrderBy(g => g[0])
            .ToArray();
        return Array.AsReadOnly(groups);
    }
}

/// <summary>
/// A compositional VS67 forecast, never real clearing or settlement. Exact
/// finite optimum over the PRODUCT of independently audited VS66 domains.
/// No global Cartesian enumeration is run if and only if physical and cash
/// independence is proven against the live authenticated service paths.
/// </summary>
public sealed class CanonicalPrimitiveFreightComponentSelection
{
    private readonly CanonicalPrimitiveFreightPortfolio _portfolio;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveFreightCandidateUse[] _candidates;
    private readonly ExperimentalBuyerCommodityPrice[] _prices;
    private readonly ExperimentalAbstractPurchasingPolicy _purchasing;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;

    public IReadOnlyList<CanonicalPrimitiveJointFreightSelection> Components { get; }
    public IReadOnlyList<CanonicalPrimitiveJointFreightLot> Lots { get; }
    public CanonicalPrimitiveJointFreightBudgetPreview? Preview { get; }
    public FoodTransportCostFraction FulfilledDemandScore { get; }
    public FoodTransportCostFraction TotalBuyerCommitment { get; }
    public BigInteger PossibleGlobalConfigurations { get; }
    public int EvaluatedComponentConfigurations { get; }
    public int IndependentComponentCount => Components.Count;
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;

    internal CanonicalPrimitiveFreightComponentSelection(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveFreightCandidateUse[] candidates,
        ExperimentalBuyerCommodityPrice[] prices,
        ExperimentalAbstractPurchasingPolicy purchasing,
        ExperimentalSharedFreightCongestionPolicy congestion,
        CanonicalPrimitiveJointFreightSelection[] components,
        CanonicalPrimitiveJointFreightBudgetPreview? preview,
        CanonicalPrimitiveJointFreightLot[] lots)
    {
        _portfolio = portfolio;
        _session = session;
        _candidates = (CanonicalPrimitiveFreightCandidateUse[])candidates.Clone();
        _prices = (ExperimentalBuyerCommodityPrice[])prices.Clone();
        _purchasing = purchasing;
        _congestion = congestion;
        Components = Array.AsReadOnly(components);
        Lots = Array.AsReadOnly(lots);
        Preview = preview;
        var score = new FoodTransportCostFraction(0, 1);
        var cost = new FoodTransportCostFraction(0, 1);
        BigInteger global = 1;
        var evaluated = 0;
        foreach (var component in components)
        {
            score = ExperimentalCashMoney.Add(score, component.FulfilledDemandScore);
            cost = ExperimentalCashMoney.Add(cost, component.TotalBuyerCommitment);
            global *= component.PossibleConfigurations;
            evaluated = checked(evaluated + component.EvaluatedConfigurations);
        }
        FulfilledDemandScore = score;
        TotalBuyerCommitment = cost;
        PossibleGlobalConfigurations = global;
        EvaluatedComponentConfigurations = evaluated;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException("Independent freight forecast belongs to a different physical session.");
        _portfolio.RequireCurrent(session);
        foreach (var component in Components) component.RequireCurrent(session);
        Preview?.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var descriptors = CanonicalPrimitiveFreightComponentSelector.Describe(
            _portfolio, _candidates);
        var membership = new Dictionary<CanonicalPrimitiveFreightCandidateUse, int>();
        for (var componentIndex = 0; componentIndex < Components.Count; componentIndex++)
        {
            var component = Components[componentIndex];
            component.VerifyIndependent();
            foreach (var candidate in component.Context.Candidates)
                if (!membership.TryAdd(candidate, componentIndex))
                    throw new InvalidOperationException("Candidate was reused across independent components.");
        }
        if (membership.Count != _candidates.Length ||
            _candidates.Any(x => !membership.ContainsKey(x)))
            throw new InvalidOperationException("Independence certificate omits authenticated candidates.");

        // Pairwise witness audit independent of the partitioner's union-find:
        // every pair in different components MUST have disjoint physical and
        // buyer resources. Connectivity is reconstructed as a BFS per group.
        for (var i = 0; i < descriptors.Length; i++)
            for (var j = i + 1; j < descriptors.Length; j++)
                if (membership[descriptors[i].Candidate] != membership[descriptors[j].Candidate] &&
                    CanonicalPrimitiveFreightCouplingPlanner.Coupled(descriptors[i], descriptors[j]))
                    throw new InvalidOperationException("A shared physical or cash resource crosses component boundaries.");
        foreach (var group in Enumerable.Range(0, descriptors.Length)
            .GroupBy(i => membership[descriptors[i].Candidate]))
        {
            var members = group.ToArray();
            var visited = new HashSet<int> { members[0] };
            var queue = new Queue<int>();
            queue.Enqueue(members[0]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var other in members)
                    if (!visited.Contains(other) &&
                        CanonicalPrimitiveFreightCouplingPlanner.Coupled(
                            descriptors[current], descriptors[other]))
                    {
                        visited.Add(other);
                        queue.Enqueue(other);
                    }
            }
            if (visited.Count != members.Length)
                throw new InvalidOperationException("One claimed component is not a connected resource group.");
        }

        var rebuiltLots = Components.SelectMany(x => x.Lots)
            .OrderBy(x => x.Candidate.Commodity.Value)
            .ThenBy(x => x.Candidate.Activity)
            .ThenBy(x => x.Candidate.Source.Value)
            .ThenBy(x => x.Candidate.Buyer.Value)
            .ThenBy(x => x.Candidate.ServiceId)
            .ThenBy(x => x.Candidate.TechnologyTypeId)
            .ThenBy(x => x.Candidate.TechnologyCatalogVersion).ToArray();
        if (rebuiltLots.Length != Lots.Count ||
            rebuiltLots.Where((x, i) =>
                !ReferenceEquals(x.Candidate, Lots[i].Candidate) ||
                x.RawQuantity != Lots[i].RawQuantity).Any())
            throw new InvalidOperationException("Combined freight lots differ from independent audited selections.");
        var rebuilt = rebuiltLots.Length == 0 ? null :
            _portfolio.PreviewJointBudget(rebuiltLots, _prices, _purchasing, _congestion);
        if ((rebuilt is null) != (Preview is null) ||
            (rebuilt is not null &&
                (!rebuilt.IsBudgetFeasible ||
                 CanonicalPrimitiveJointFreightSearchContext.Commitment(rebuilt)
                    .CompareTo(TotalBuyerCommitment) != 0)))
            throw new InvalidOperationException("Independent component costs do not reconcile to the combined buyer budget.");
        var demand = _portfolio.CurrentSites.Flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        var score = new FoodTransportCostFraction(0, 1);
        foreach (var lot in rebuiltLots)
            score = ExperimentalCashMoney.Add(score,
                new FoodTransportCostFraction(lot.RawQuantity,
                    demand[(lot.Candidate.Buyer, lot.Candidate.Commodity)]));
        if (score.CompareTo(FulfilledDemandScore) != 0)
            throw new InvalidOperationException("Independent component fulfilled-demand score does not reconcile.");
        RequireCurrent(_session);
    }
}

public sealed class CanonicalPrimitiveFreightComponentSelector
{
    public const int MaximumCandidates = 12;

    internal static CanonicalPrimitiveFreightCouplingDescriptor[] Describe(
        CanonicalPrimitiveFreightPortfolio portfolio,
        IReadOnlyList<CanonicalPrimitiveFreightCandidateUse> candidates)
    {
        var descriptions = new List<CanonicalPrimitiveFreightCouplingDescriptor>();
        foreach (var use in candidates)
        {
            // Every route/asset is taken from the captured VS63 legal
            // discovery; a caller may not inject coupling evidence.
            var slice = portfolio.Slices.Single(x =>
                x.Policy.Commodity == use.Commodity &&
                x.Policy.Activity == use.Activity &&
                x.Policy.Pricing.TechnologyTypeId == use.TechnologyTypeId &&
                x.Policy.Pricing.TechnologyCatalogVersion == use.TechnologyCatalogVersion);
            var service = slice.Discovery.Services.Single(x =>
                x.Offer.ServiceId == use.ServiceId &&
                x.Entitlement.Source == use.Source &&
                x.Entitlement.Destination == use.Buyer);
            descriptions.Add(new(use,
                Array.AsReadOnly(service.Option.Corridors.ToArray()),
                Array.AsReadOnly(service.Offer.AssetPath.ToArray())));
        }
        return descriptions.ToArray();
    }

    public CanonicalPrimitiveFreightComponentSelection Select(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> candidates,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        ulong rawQuantum, int maximumQuantaPerCandidate = 1,
        int maximumConfigurationsPerComponent = 4096)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(postedPrices);
        ArgumentNullException.ThrowIfNull(purchasingPolicy);
        ArgumentNullException.ThrowIfNull(congestionPolicy);
        portfolio.RequireCurrent(session);
        var input = candidates.ToArray();
        if (input.Length is < 1 or > MaximumCandidates ||
            input.Any(x => x is null || !portfolio.CandidateUses.Any(y => ReferenceEquals(x, y))) ||
            input.Distinct().Count() != input.Length)
            throw new ArgumentException("Component selection requires 1..12 unique authentic VS64 candidates.", nameof(candidates));
        var ordered = input.OrderBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity)
            .ThenBy(x => x.Source.Value)
            .ThenBy(x => x.Buyer.Value)
            .ThenBy(x => x.ServiceId)
            .ThenBy(x => x.TechnologyTypeId)
            .ThenBy(x => x.TechnologyCatalogVersion).ToArray();
        var prices = postedPrices.ToArray();
        var descriptions = Describe(portfolio, ordered);
        var groups = CanonicalPrimitiveFreightCouplingPlanner.Partition(descriptions);
        if (groups.Any(x => x.Count > CanonicalPrimitiveJointFreightSelector.MaximumCandidates))
            throw new ArgumentException(
                "Coupled component exceeds VS66's eight-candidate exhaustive reference; cannot decompose.", nameof(candidates));

        var solved = new List<CanonicalPrimitiveJointFreightSelection>();
        foreach (var group in groups)
        {
            var subset = group.Select(i => ordered[i]).ToArray();
            var solution = portfolio.SelectJointFreight(subset, prices, purchasingPolicy,
                congestionPolicy, rawQuantum, maximumQuantaPerCandidate,
                maximumConfigurationsPerComponent);
            solved.Add(solution);
        }
        var lots = solved.SelectMany(x => x.Lots)
            .OrderBy(x => x.Candidate.Commodity.Value)
            .ThenBy(x => x.Candidate.Activity)
            .ThenBy(x => x.Candidate.Source.Value)
            .ThenBy(x => x.Candidate.Buyer.Value)
            .ThenBy(x => x.Candidate.ServiceId)
            .ThenBy(x => x.Candidate.TechnologyTypeId)
            .ThenBy(x => x.Candidate.TechnologyCatalogVersion).ToArray();
        var combined = lots.Length == 0 ? null :
            portfolio.PreviewJointBudget(lots, prices, purchasingPolicy, congestionPolicy);
        if (combined is not null && !combined.IsBudgetFeasible)
            throw new InvalidOperationException("Independent purchases unexpectedly compete for one protected buyer budget.");
        var result = new CanonicalPrimitiveFreightComponentSelection(
            portfolio, session, ordered, prices, purchasingPolicy,
            congestionPolicy, solved.ToArray(), combined, lots);
        result.VerifyIndependent();
        return result;
    }
}
