using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A signed exact rational for a net commercial indicator. It is NOT a
/// monetary wallet, sale proceeds, real profit or financial balance.
/// </summary>
public readonly record struct CanonicalPrimitiveIntegratedNetValue(
    BigInteger Numerator, BigInteger Denominator) :
    IComparable<CanonicalPrimitiveIntegratedNetValue>
{
    public int CompareTo(CanonicalPrimitiveIntegratedNetValue other)
    {
        if (Denominator <= 0 || other.Denominator <= 0)
            throw new InvalidOperationException("Net indicator denominator must be positive.");
        return (Numerator * other.Denominator)
            .CompareTo(other.Numerator * Denominator);
    }
}

/// <summary>
/// One authenticated alternative: exactly one of Local and Freight is present.
/// The caller cannot fabricate this identity. No dispatch, price creation or
/// special local/export ordering is inferred from the alternative's type.
/// </summary>
public sealed class CanonicalPrimitiveIntegratedUseOption
{
    public CanonicalPrimitiveLocalUseProposal? Local { get; }
    public CanonicalPrimitiveFreightCandidateUse? Freight { get; }
    public EconomicPointId Source => Local?.Point ?? Freight!.Source;
    public EconomicPointId Buyer => Local?.Point ?? Freight!.Buyer;
    public CommodityId Commodity => Local?.Commodity ?? Freight!.Commodity;
    public EconomicActivityKind Activity => Local?.Activity ?? Freight!.Activity;
    public ulong MaximumRawQuantity => Local?.RawQuantity ?? Freight!.MaximumRawQuantity;
    public bool IsLocal => Local is not null;

    internal CanonicalPrimitiveIntegratedUseOption(
        CanonicalPrimitiveLocalUseProposal? local,
        CanonicalPrimitiveFreightCandidateUse? freight)
    {
        if ((local is null) == (freight is null))
            throw new ArgumentException("Exactly one physical delivery kind is required.");
        Local = local;
        Freight = freight;
    }
}

public sealed record CanonicalPrimitiveIntegratedUseLot(
    CanonicalPrimitiveIntegratedUseOption Option, ulong RawQuantity);

internal readonly record struct CanonicalPrimitiveIntegratedScore(
    CanonicalPrimitiveIntegratedNetValue Net,
    FoodTransportCostFraction Coverage,
    FoodTransportCostFraction TotalCommitment);

internal sealed record CanonicalPrimitiveIntegratedEvaluation(
    CanonicalPrimitiveIntegratedUseLot[] Lots,
    CanonicalPrimitiveJointFreightBudgetPreview? FreightPreview,
    CanonicalPrimitiveIntegratedScore Score);

/// <summary>
/// A single VS76 finite domain of local AND freight options. Uses the EXISTING
/// VS65 joint freight preflight for all actual road/service/corridor costs;
/// local entries are checked against the same physical source, buyer demand
/// and buyer's protected-H abstract purchasing budget.
/// </summary>
internal sealed class CanonicalPrimitiveIntegratedSearchContext
{
    private static FoodTransportCostFraction Zero => new(0, 1);
    private readonly IReadOnlyDictionary<(EconomicPointId, CommodityId,
        EconomicActivityKind), ulong> _supply;
    private readonly IReadOnlyDictionary<(EconomicPointId, CommodityId), ulong> _demand;
    private readonly IReadOnlyDictionary<(EconomicPointId, CommodityId),
        FoodTransportCostFraction> _prices;
    private readonly IReadOnlyDictionary<EconomicPointId, FoodTransportCostFraction> _budgets;

    internal CanonicalPrimitiveFreightPortfolio Portfolio { get; }
    internal CanonicalPhysicalTacticalSession Session { get; }
    internal CanonicalPrimitiveIntegratedUseOption[] Options { get; }
    internal int[] MaximumQuanta { get; }
    internal ExperimentalBuyerCommodityPrice[] PostedPrices { get; }
    internal ExperimentalAbstractPurchasingPolicy Purchasing { get; }
    internal ExperimentalSharedFreightCongestionPolicy Congestion { get; }
    internal ulong Quantum { get; }
    internal BigInteger PossibleConfigurations { get; }

    internal CanonicalPrimitiveIntegratedSearchContext(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveLocalUseProposal> localCandidates,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> freightCandidates,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasing,
        ExperimentalSharedFreightCongestionPolicy congestion,
        ulong rawQuantum, int maximumQuantaPerCandidate, int maximumConfigurations)
    {
        Portfolio = portfolio ?? throw new ArgumentNullException(nameof(portfolio));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentNullException.ThrowIfNull(localCandidates);
        ArgumentNullException.ThrowIfNull(freightCandidates);
        ArgumentNullException.ThrowIfNull(postedPrices);
        Purchasing = purchasing ?? throw new ArgumentNullException(nameof(purchasing));
        Congestion = congestion ?? throw new ArgumentNullException(nameof(congestion));
        if (rawQuantum == 0 || maximumQuantaPerCandidate is < 1 or > 8 ||
            maximumConfigurations is < 1 or >
                CanonicalPrimitiveJointFreightSelector.MaximumConfigurations)
            throw new ArgumentOutOfRangeException(nameof(rawQuantum),
                "Integrated reference must use positive quantum, 1..8 quanta and <=4096 configurations.");
        portfolio.RequireCurrent(session);
        if (purchasing.PolicyId != portfolio.PurchasingPolicyId ||
            purchasing.Version != portfolio.PurchasingPolicyVersion)
            throw new ArgumentException("Purchasing policy does not match authenticated portfolio.");

        var sites = portfolio.CurrentSites;
        _supply = sites.Flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity), x => x.RawQuantity);
        _demand = sites.Flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        var locals = localCandidates.ToArray();
        var freight = freightCandidates.ToArray();
        if (locals.Length + freight.Length is < 1 or >
                CanonicalPrimitiveJointFreightSelector.MaximumCandidates ||
            locals.Length > CanonicalPrimitiveJointPhysicalUsePreview.MaximumLocalProposals ||
            locals.Any(x => x is null || !x.Point.IsValid ||
                !x.Commodity.IsValid || x.RawQuantity == 0 ||
                !_supply.TryGetValue((x.Point, x.Commodity, x.Activity), out var cap) ||
                x.RawQuantity > cap || !_demand.ContainsKey((x.Point, x.Commodity))) ||
            locals.Select(x => (x.Point, x.Commodity, x.Activity))
                .Distinct().Count() != locals.Length)
            throw new ArgumentException(
                "Local alternatives need unique, inhabited physical source/activity and same-tile demand.",
                nameof(localCandidates));
        if (freight.Any(x => x is null ||
                !portfolio.CandidateUses.Any(y => ReferenceEquals(x, y))) ||
            freight.Distinct().Count() != freight.Length)
            throw new ArgumentException(
                "Freight alternatives must be exact authenticated portfolio identities.",
                nameof(freightCandidates));

        Options = locals.Select(x => new CanonicalPrimitiveIntegratedUseOption(x, null))
            .Concat(freight.Select(x => new CanonicalPrimitiveIntegratedUseOption(null, x)))
            // Stable canonical key independent of input order. Local has no
            // service; ordering is by physical identity, not execution priority.
            .OrderBy(x => x.Commodity.Value)
            .ThenBy(x => x.Activity)
            .ThenBy(x => x.Source.Value)
            .ThenBy(x => x.Buyer.Value)
            .ThenBy(x => x.Freight?.ServiceId ?? 0UL)
            .ThenBy(x => x.Freight?.TechnologyTypeId ?? 0UL)
            .ThenBy(x => x.Freight?.TechnologyCatalogVersion ?? 0UL).ToArray();

        PostedPrices = postedPrices.ToArray();
        _prices = PostedPrices.ToDictionary(
            x => (x.Buyer, x.Commodity), x => x.PricePerWholeFlowUnit);
        if (!_prices.Keys.ToHashSet().SetEquals(_demand.Keys))
            throw new ArgumentException("One authenticated posted price is required per demand.");

        var projection = new ExperimentalAbstractPurchasingPowerProjector().Project(
            session, sites.Flows, sites.BuyerAnchors, sites.ProducerAnchors,
            Array.Empty<ExperimentalSimultaneousFreightLane>(),
            PostedPrices, purchasing);
        _budgets = projection.Buyers.ToDictionary(
            x => x.Buyer, x => x.TemporaryCycleBudget.SpendableCash);
        Quantum = rawQuantum;
        MaximumQuanta = Options.Select(x =>
            checked((int)Math.Min(x.MaximumRawQuantity / rawQuantum,
                (ulong)maximumQuantaPerCandidate))).ToArray();
        var cases = BigInteger.One;
        foreach (var n in MaximumQuanta)
        {
            cases *= n + 1;
            if (cases > maximumConfigurations)
                throw new ArgumentException(
                    "Integrated search exceeds bounded VS66 domain: no partial optimum.",
                    nameof(maximumConfigurations));
        }
        PossibleConfigurations = cases;
        portfolio.RequireCurrent(session);
    }

    private static FoodTransportCostFraction Add(
        FoodTransportCostFraction a, FoodTransportCostFraction b) =>
        new(a.Numerator * b.Denominator + b.Numerator * a.Denominator,
            a.Denominator * b.Denominator);

    private static CanonicalPrimitiveIntegratedNetValue Net(
        FoodTransportCostFraction goods, FoodTransportCostFraction freight) =>
        new(goods.Numerator * freight.Denominator -
            freight.Numerator * goods.Denominator,
            goods.Denominator * freight.Denominator);

    internal CanonicalPrimitiveIntegratedEvaluation? Evaluate(
        IReadOnlyList<int> digits)
    {
        if (digits.Count != Options.Length) throw new ArgumentException("Invalid digit vector.");
        var usedSources = new Dictionary<(EconomicPointId, CommodityId,
            EconomicActivityKind), BigInteger>();
        var usedBuyers = new Dictionary<(EconomicPointId, CommodityId), BigInteger>();
        var localCharges = new Dictionary<EconomicPointId, FoodTransportCostFraction>();
        var lots = new List<CanonicalPrimitiveIntegratedUseLot>();
        var shipments = new List<CanonicalPrimitiveJointFreightLot>();
        var goodsValue = Zero;
        for (var i = 0; i < Options.Length; i++)
        {
            var n = digits[i];
            if (n < 0 || n > MaximumQuanta[i]) return null;
            if (n == 0) continue;
            var option = Options[i];
            var raw = checked((ulong)n * Quantum);
            var sourceKey = (option.Source, option.Commodity, option.Activity);
            var demandKey = (option.Buyer, option.Commodity);
            usedSources.TryGetValue(sourceKey, out var priorSource);
            usedBuyers.TryGetValue(demandKey, out var priorDemand);
            usedSources[sourceKey] = priorSource + raw;
            usedBuyers[demandKey] = priorDemand + raw;
            if (usedSources[sourceKey] > _supply[sourceKey] ||
                usedBuyers[demandKey] > _demand[demandKey]) return null;
            var charge = ExperimentalCashMoney.Multiply(_prices[demandKey],
                new BigInteger(raw),
                new BigInteger(EconomicTransferPolicy.FixedPointDenominator));
            goodsValue = Add(goodsValue, charge);
            if (option.IsLocal)
            {
                localCharges.TryGetValue(option.Buyer, out var prior);
                localCharges[option.Buyer] = Add(prior ?? Zero, charge);
            }
            else
                shipments.Add(new CanonicalPrimitiveJointFreightLot(option.Freight!, raw));
            lots.Add(new CanonicalPrimitiveIntegratedUseLot(option, raw));
        }

        CanonicalPrimitiveJointFreightBudgetPreview? preview = shipments.Count == 0
            ? null : Portfolio.PreviewJointBudget(shipments, PostedPrices,
                Purchasing, Congestion);
        if (preview is not null && !preview.IsBudgetFeasible) return null;
        var freightByBuyer = preview?.Buyers.Buyers.ToDictionary(
            x => x.Buyer, x => x.TotalCommitment);
        var commitments = Zero;
        foreach (var buyer in _budgets)
        {
            var freight = freightByBuyer is not null &&
                freightByBuyer.TryGetValue(buyer.Key, out var f) ? f : Zero;
            localCharges.TryGetValue(buyer.Key, out var local);
            var amount = Add(freight, local ?? Zero);
            if (amount.CompareTo(buyer.Value) > 0) return null;
            commitments = Add(commitments, amount);
        }

        var coverage = Zero;
        foreach (var pair in usedBuyers)
            coverage = Add(coverage, new FoodTransportCostFraction(
                pair.Value, _demand[pair.Key]));
        var freightCost = preview?.Freight.TotalCost ?? Zero;
        return new CanonicalPrimitiveIntegratedEvaluation(lots.ToArray(), preview,
            new CanonicalPrimitiveIntegratedScore(Net(goodsValue, freightCost),
                coverage, commitments));
    }
}

/// <summary>
/// Exact finite simultaneous optimum, NOT consumption or settlement.
/// VS66 freight-only remains unchanged; freight values and costs are obtained
/// exclusively through its existing, independently authenticated budget gate.
/// </summary>
public sealed class CanonicalPrimitiveIntegratedSelection
{
    private readonly CanonicalPrimitiveIntegratedSearchContext _context;
    private readonly int[] _bestDigits;

    public IReadOnlyList<CanonicalPrimitiveIntegratedUseLot> Lots { get; }
    public CanonicalPrimitiveJointFreightBudgetPreview? FreightPreview { get; }
    public CanonicalPrimitiveIntegratedNetValue ProjectedNetValue { get; }
    public FoodTransportCostFraction FulfilledDemandScore { get; }
    public FoodTransportCostFraction TotalBuyerCommitment { get; }
    public BigInteger PossibleConfigurations => _context.PossibleConfigurations;
    public int EvaluatedConfigurations { get; }
    public int FeasibleConfigurations { get; }
    public bool CanAuthorizeRealSettlement => false;
    public bool IsActualConsumption => false;
    public bool IsCommitted => false;

    internal CanonicalPrimitiveIntegratedSelection(
        CanonicalPrimitiveIntegratedSearchContext context, int[] bestDigits,
        CanonicalPrimitiveIntegratedEvaluation evaluation,
        int evaluated, int feasible)
    {
        _context = context;
        _bestDigits = (int[])bestDigits.Clone();
        Lots = Array.AsReadOnly(evaluation.Lots);
        FreightPreview = evaluation.FreightPreview;
        ProjectedNetValue = evaluation.Score.Net;
        FulfilledDemandScore = evaluation.Score.Coverage;
        TotalBuyerCommitment = evaluation.Score.TotalCommitment;
        EvaluatedConfigurations = evaluated;
        FeasibleConfigurations = feasible;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _context.Session))
            throw new InvalidOperationException(
                "Integrated selection belongs to another physical session.");
        _context.Portfolio.RequireCurrent(session);
        FreightPreview?.RequireCurrent(session);
    }

    public void VerifyIndependent()
    {
        RequireCurrent(_context.Session);
        var possible = _context.PossibleConfigurations;
        var best = new int[_context.Options.Length];
        CanonicalPrimitiveIntegratedEvaluation? winner = null;
        var feasible = 0;
        var evaluated = 0;
        // Independent flat mixed-radix traversal, rather than the selector's
        // depth-first recursion. Re-evaluate every admissible physical/H batch.
        for (var serial = BigInteger.Zero; serial < possible; serial++)
        {
            var carry = serial;
            var digits = new int[_context.Options.Length];
            for (var i = 0; i < digits.Length; i++)
            {
                var radix = _context.MaximumQuanta[i] + 1;
                digits[i] = (int)(carry % radix);
                carry /= radix;
            }
            var evaluation = _context.Evaluate(digits);
            evaluated++;
            if (evaluation is null) continue;
            feasible++;
            if (winner is null ||
                CanonicalPrimitiveIntegratedSelector.Compare(
                    evaluation.Score, winner.Score, digits, best) > 0)
            {
                winner = evaluation;
                best = digits;
            }
        }
        if (winner is null || evaluated != EvaluatedConfigurations ||
            feasible != FeasibleConfigurations ||
            !_bestDigits.SequenceEqual(best) ||
            !Lots.SequenceEqual(winner.Lots) ||
            ProjectedNetValue.CompareTo(winner.Score.Net) != 0 ||
            FulfilledDemandScore.CompareTo(winner.Score.Coverage) != 0 ||
            TotalBuyerCommitment.CompareTo(winner.Score.TotalCommitment) != 0)
            throw new InvalidOperationException(
                "Integrated local/freight optimum failed independent finite-domain replay.");
        RequireCurrent(_context.Session);
    }
}

public sealed class CanonicalPrimitiveIntegratedSelector
{
    internal static int Compare(
        CanonicalPrimitiveIntegratedScore a,
        CanonicalPrimitiveIntegratedScore b,
        IReadOnlyList<int> digitsA, IReadOnlyList<int> digitsB)
    {
        var cmp = a.Net.CompareTo(b.Net);
        if (cmp != 0) return cmp;
        cmp = a.Coverage.CompareTo(b.Coverage);
        if (cmp != 0) return cmp;
        cmp = b.TotalCommitment.CompareTo(a.TotalCommitment);
        if (cmp != 0) return cmp;
        for (var i = 0; i < digitsA.Count; i++)
            if (digitsA[i] != digitsB[i])
                return digitsA[i].CompareTo(digitsB[i]);
        return 0;
    }

    public CanonicalPrimitiveIntegratedSelection Select(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveLocalUseProposal> localCandidates,
        IEnumerable<CanonicalPrimitiveFreightCandidateUse> freightCandidates,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy,
        ulong rawQuantum, int maximumQuantaPerCandidate = 1,
        int maximumConfigurations =
            CanonicalPrimitiveJointFreightSelector.MaximumConfigurations)
    {
        var ctx = new CanonicalPrimitiveIntegratedSearchContext(
            portfolio, session, localCandidates, freightCandidates, postedPrices,
            purchasingPolicy, congestionPolicy, rawQuantum,
            maximumQuantaPerCandidate, maximumConfigurations);
        var digits = new int[ctx.Options.Length];
        var best = new int[ctx.Options.Length];
        var winner = ctx.Evaluate(best)!; // All-zero feasible reference.
        var evaluated = 0;
        var feasible = 0;
        void Visit(int index)
        {
            if (index != digits.Length)
            {
                for (var i = 0; i <= ctx.MaximumQuanta[index]; i++)
                {
                    digits[index] = i;
                    Visit(index + 1);
                }
                return;
            }
            evaluated++;
            var candidate = ctx.Evaluate(digits);
            if (candidate is null) return;
            feasible++;
            if (Compare(candidate.Score, winner.Score, digits, best) > 0)
            {
                winner = candidate;
                best = (int[])digits.Clone();
            }
        }
        Visit(0);
        var result = new CanonicalPrimitiveIntegratedSelection(
            ctx, best, winner, evaluated, feasible);
        result.VerifyIndependent();
        return result;
    }
}
