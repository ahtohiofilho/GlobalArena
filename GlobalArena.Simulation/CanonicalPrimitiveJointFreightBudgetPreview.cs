using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// An explicit, hypothetical shipment of one authenticated VS64 candidate.
/// A proposed lot is NOT an inventory debit, delivery order or settlement.
/// </summary>
public sealed record CanonicalPrimitiveJointFreightLot(
    CanonicalPrimitiveFreightCandidateUse Candidate,
    ulong RawQuantity);

/// <summary>
/// One joint, non-mutating physical/cash preflight. Shares the SAME buyer
/// purchasing power and H across every commodity, and the SAME physical
/// corridor loads across all proposed lots. The caller, not a hidden
/// optimizer, declares trial quantities. Insufficient funds are reported;
/// oversubscribed physical supply/demand is rejected, never partially paid.
/// </summary>
public sealed class CanonicalPrimitiveJointFreightBudgetPreview
{
    public const int MaximumLots = 12;
    private readonly CanonicalPrimitiveFreightPortfolio _portfolio;
    private readonly CanonicalPhysicalTacticalSession _session;

    public IReadOnlyList<CanonicalPrimitiveJointFreightLot> Lots { get; }
    public ExperimentalAbstractPurchasingProjection Purchasing { get; }
    public ExperimentalSharedFreightBatchEvaluation Freight { get; }
    public ExperimentalBuyerCashEvaluation Buyers { get; }
    public bool IsBudgetFeasible => Buyers.CanSettleAtSight;
    public bool CanAuthorizeRealSettlement => false;
    public bool IsCommitted => false;

    private CanonicalPrimitiveJointFreightBudgetPreview(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveJointFreightLot[] lots,
        ExperimentalAbstractPurchasingProjection purchasing,
        ExperimentalSharedFreightBatchEvaluation freight,
        ExperimentalBuyerCashEvaluation buyers)
    {
        _portfolio = portfolio;
        _session = session;
        Lots = Array.AsReadOnly(lots);
        Purchasing = purchasing;
        Freight = freight;
        Buyers = buyers;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "Joint preflight cannot be reused in another physical session.");
        _portfolio.RequireCurrent(session);
    }

    public static CanonicalPrimitiveJointFreightBudgetPreview FromPortfolio(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        IEnumerable<CanonicalPrimitiveJointFreightLot> proposedLots,
        IEnumerable<ExperimentalBuyerCommodityPrice> postedPrices,
        ExperimentalAbstractPurchasingPolicy purchasingPolicy,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(proposedLots);
        ArgumentNullException.ThrowIfNull(postedPrices);
        ArgumentNullException.ThrowIfNull(purchasingPolicy);
        ArgumentNullException.ThrowIfNull(congestionPolicy);
        portfolio.RequireCurrent(session);
        if (purchasingPolicy.PolicyId != portfolio.PurchasingPolicyId ||
            purchasingPolicy.Version != portfolio.PurchasingPolicyVersion)
            throw new ArgumentException(
                "Joint purchasing policy does not match the portfolio's versioned institutional provenance.",
                nameof(purchasingPolicy));
        // Capture lazy inputs ONCE. No caller can change live physical state
        // or institutional rights during this forecast unnoticed.
        var lots = proposedLots.ToArray();
        var prices = postedPrices.ToArray();
        if (lots.Length is < 1 or > MaximumLots ||
            lots.Any(x => x is null || x.Candidate is null || x.RawQuantity == 0UL))
            throw new ArgumentException(
                "Joint forecast requires 1..12 positive candidate lots.", nameof(proposedLots));
        var selected = lots
            .OrderBy(x => x.Candidate.Commodity.Value)
            .ThenBy(x => x.Candidate.Activity)
            .ThenBy(x => x.Candidate.Source.Value)
            .ThenBy(x => x.Candidate.Buyer.Value)
            .ThenBy(x => x.Candidate.ServiceId)
            .ThenBy(x => x.Candidate.TechnologyTypeId)
            .ThenBy(x => x.Candidate.TechnologyCatalogVersion)
            .ToArray();
        if (selected.Select(x => x.Candidate).Distinct().Count() != selected.Length)
            throw new ArgumentException(
                "The same candidate may appear only once in a joint preflight.", nameof(proposedLots));

        var sites = portfolio.CurrentSites;
        var supply = sites.Flows.SupplyFlows.ToDictionary(x =>
            (x.EconomicPointId, x.CommodityId, x.Activity), x => x.RawQuantity);
        var demand = sites.Flows.DemandFlows.ToDictionary(x =>
            (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        var committedSupply = new Dictionary<
            (EconomicPointId, CommodityId, EconomicActivityKind), BigInteger>();
        var committedDemand = new Dictionary<(EconomicPointId, CommodityId), BigInteger>();
        var physicalLots = new List<ExperimentalSharedFreightLot>();
        var cashLots = new List<ExperimentalBuyerCashDelivery>();

        for (var i = 0; i < selected.Length; i++)
        {
            var lot = selected[i];
            var candidate = lot.Candidate;
            // Identity must be the actual reference emitted by this portfolio,
            // not a record reconstructed by an untrusted caller.
            if (!portfolio.CandidateUses.Any(x => ReferenceEquals(x, candidate)) ||
                lot.RawQuantity > candidate.MaximumRawQuantity)
                throw new ArgumentException(
                    "Joint lot has no current authenticated candidate or exceeds its per-offer bound.",
                    nameof(proposedLots));

            var sourceKey = (candidate.Source, candidate.Commodity, candidate.Activity);
            var demandKey = (candidate.Buyer, candidate.Commodity);
            if (!supply.TryGetValue(sourceKey, out var sourceCap) ||
                !demand.TryGetValue(demandKey, out var buyerCap))
                throw new InvalidOperationException(
                    "One candidate has no current physical supply or consumer demand.");
            committedSupply.TryGetValue(sourceKey, out var usedSupply);
            committedDemand.TryGetValue(demandKey, out var usedDemand);
            committedSupply[sourceKey] = usedSupply + lot.RawQuantity;
            committedDemand[demandKey] = usedDemand + lot.RawQuantity;
            if (committedSupply[sourceKey] > sourceCap ||
                committedDemand[demandKey] > buyerCap)
                throw new ArgumentException(
                    "Joint freight lots oversubscribe a shared physical producer or consumer quantity.",
                    nameof(proposedLots));

            var slice = portfolio.Slices.Single(x =>
                x.Policy.Commodity == candidate.Commodity &&
                x.Policy.Activity == candidate.Activity &&
                x.Policy.Pricing.TechnologyTypeId == candidate.TechnologyTypeId &&
                x.Policy.Pricing.TechnologyCatalogVersion == candidate.TechnologyCatalogVersion);
            var authenticated = slice.Discovery.Services.Single(x =>
                x.Offer.ServiceId == candidate.ServiceId &&
                x.Entitlement.Source == candidate.Source &&
                x.Entitlement.Destination == candidate.Buyer);
            var definition = slice.Policy.Catalog.Commodities.Single(x =>
                x.CommodityId == candidate.Commodity);
            var loadMicrograms = (BigInteger)lot.RawQuantity * definition.GramsPerFlowUnit;
            physicalLots.Add(new ExperimentalSharedFreightLot(
                (ulong)(i + 1), loadMicrograms, [authenticated.Option]));
            cashLots.Add(new ExperimentalBuyerCashDelivery(
                candidate.Buyer, candidate.Commodity, lot.RawQuantity,
                loadMicrograms, authenticated.Option));
        }

        // One household per inhabited tile, rather than one per item.
        // No unresolved carrier is inferred from budget projection; VS63
        // separately attests each chosen real service and infrastructure.
        var purchasing = new ExperimentalAbstractPurchasingPowerProjector()
            .Project(session, sites.Flows, sites.BuyerAnchors,
                sites.ProducerAnchors,
                Array.Empty<ExperimentalSimultaneousFreightLane>(),
                prices, purchasingPolicy);
        var congestion = ExperimentalSharedFreightBatchCost.Evaluate(
            physicalLots, new int[physicalLots.Count], congestionPolicy);
        var buyers = purchasing.BudgetSnapshot.Evaluate(
            cashLots, congestion.CorridorLoads, congestionPolicy);
        if (buyers.TotalBuyerFreight.CompareTo(congestion.TotalCost) != 0)
            throw new InvalidOperationException(
                "Joint buyers' freight charges do not reconcile to physical corridor costs.");
        var result = new CanonicalPrimitiveJointFreightBudgetPreview(
            portfolio, session, selected, purchasing, congestion, buyers);
        result.RequireCurrent(session);
        return result;
    }
}
