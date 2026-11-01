using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Prices are dimensionless experimental indices; the authenticated freight
/// cost retains its existing nominal physical transport unit. They must NOT
/// be subtracted until a common unit and end-to-end clearing is ratified.
/// </summary>
public sealed record CanonicalEndogenousFreightPriceObservation(
    EconomicPointId Source, EconomicPointId Buyer, CommodityId Commodity,
    EconomicActivityKind Activity, ulong ServiceId, ulong RawQuantity,
    BigInteger SourceDemandPressure, BigInteger BuyerDemandPressure,
    CanonicalEndogenousPriceIndex SourceAveragePriceIndex,
    CanonicalEndogenousPriceIndex BuyerAveragePriceIndex,
    CanonicalEndogenousPriceIndex SourceLastMarginalPriceIndex,
    CanonicalEndogenousPriceIndex BuyerLastMarginalPriceIndex,
    CanonicalEndogenousPriceIndex SourceGrossRevenueIndex,
    CanonicalEndogenousPriceIndex BuyerGrossRevenueIndex,
    FoodTransportCostFraction AuthenticatedNominalFreightCost);

/// <summary>
/// VS79: one authenticated commercial candidate, one physical lot, two
/// destination demand signals. No preallocated private cash, prices from a
/// caller, artificial buyer, physical-demand consumption cap or net-profit
/// comparison between incompatible index and nominal freight units.
/// </summary>
public sealed class CanonicalEndogenousFreightPriceComparison
{
    private readonly CanonicalPrimitiveFreightPortfolio _portfolio;
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveFreightCandidateUse _candidate;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;
    private readonly ulong _quantity;

    public CanonicalEndogenousFreightPriceObservation Gentle { get; }
    public CanonicalEndogenousFreightPriceObservation Steep { get; }
    public bool IsNominalFreightConvertedIntoPriceIndex => false;
    public bool CanCalculateMonetaryNetRevenue => false;
    public bool UsesExogenousBuyerBudget => false;
    public bool LimitsConsumptionToPhysicalDemand => false;
    public bool IsDispatchOrSettlement => false;
    public bool HasCommittedPhysicalInventory => false;
    public bool CanAuthorizeRealSettlement => false;

    private CanonicalEndogenousFreightPriceComparison(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveFreightCandidateUse candidate, ulong quantity,
        ExperimentalSharedFreightCongestionPolicy congestion,
        CanonicalEndogenousFreightPriceObservation gentle,
        CanonicalEndogenousFreightPriceObservation steep)
    {
        _portfolio = portfolio;
        _session = session;
        _candidate = candidate;
        _quantity = quantity;
        _congestion = congestion;
        Gentle = gentle;
        Steep = steep;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(_session, session))
            throw new InvalidOperationException(
                "Remote price comparison belongs to a different canonical session.");
        _portfolio.RequireCurrent(session);
    }

    private static (
        CanonicalEndogenousFreightPriceObservation Gentle,
        CanonicalEndogenousFreightPriceObservation Steep)
        Rebuild(CanonicalPrimitiveFreightPortfolio portfolio,
            CanonicalPrimitiveFreightCandidateUse candidate, ulong quantity,
            ExperimentalSharedFreightCongestionPolicy congestion)
    {
        var sites = portfolio.CurrentSites;
        var sourceSupply = sites.Flows.SupplyFlows.SingleOrDefault(x =>
            x.EconomicPointId == candidate.Source &&
            x.CommodityId == candidate.Commodity &&
            x.Activity == candidate.Activity);
        var sourceDemand = sites.Flows.DemandFlows.SingleOrDefault(x =>
            x.EconomicPointId == candidate.Source &&
            x.CommodityId == candidate.Commodity);
        var buyerDemand = sites.Flows.DemandFlows.SingleOrDefault(x =>
            x.EconomicPointId == candidate.Buyer &&
            x.CommodityId == candidate.Commodity);

        if (sourceSupply is null || sourceDemand is null || buyerDemand is null ||
            sites.At(candidate.Source)?.ResidentWorkers is not > 0 ||
            sites.At(candidate.Buyer)?.ResidentWorkers is not > 0 ||
            candidate.Source == candidate.Buyer ||
            quantity == 0 || quantity > sourceSupply.RawQuantity ||
            quantity > candidate.MaximumRawQuantity)
            throw new ArgumentException(
                "Commercial comparison requires one positive physically valid lot, an inhabited source and a distinct inhabited buyer with demand signal.");

        var slice = portfolio.Slices.Single(x =>
            x.Policy.Commodity == candidate.Commodity &&
            x.Policy.Activity == candidate.Activity &&
            x.Policy.Pricing.TechnologyTypeId == candidate.TechnologyTypeId &&
            x.Policy.Pricing.TechnologyCatalogVersion ==
                candidate.TechnologyCatalogVersion);
        var service = slice.Discovery.Services.Single(x =>
            x.Offer.ServiceId == candidate.ServiceId &&
            x.Entitlement.Source == candidate.Source &&
            x.Entitlement.Destination == candidate.Buyer);
        var massPerUnit = slice.Policy.Catalog.Commodities.Single(x =>
            x.CommodityId == candidate.Commodity).GramsPerFlowUnit;
        var mass = new BigInteger(quantity) * massPerUnit;

        // This is exactly the authenticated shared-corridor freight evaluator
        // used by VS65, with ONE lot and NO cash / purchasing preflight.
        var freight = ExperimentalSharedFreightBatchCost.Evaluate(
            [new ExperimentalSharedFreightLot(1UL, mass, [service.Option])],
            [0], congestion);
        var cost = freight.TotalCost;
        if (cost.Numerator < 0 || cost.Denominator <= 0)
            throw new InvalidOperationException(
                "Authenticated freight must have a finite nonnegative nominal cost.");

        CanonicalEndogenousFreightPriceObservation Compute(int sensitivity)
        {
            var local = CanonicalEndogenousLocalPriceComparison.Calculate(
                quantity, sourceDemand.RawQuantity, sensitivity);
            var remote = CanonicalEndogenousLocalPriceComparison.Calculate(
                quantity, buyerDemand.RawQuantity, sensitivity);
            return new(candidate.Source, candidate.Buyer, candidate.Commodity,
                candidate.Activity, candidate.ServiceId, quantity,
                sourceDemand.RawQuantity, buyerDemand.RawQuantity,
                local.Average, remote.Average, local.Last, remote.Last,
                local.Revenue, remote.Revenue, cost);
        }

        return (Compute(1), Compute(2));
    }

    private static bool SameIndex(CanonicalEndogenousPriceIndex a,
        CanonicalEndogenousPriceIndex b) => a.CompareTo(b) == 0;

    private static bool SameObservation(
        CanonicalEndogenousFreightPriceObservation a,
        CanonicalEndogenousFreightPriceObservation b) =>
        a.Source == b.Source && a.Buyer == b.Buyer &&
        a.Commodity == b.Commodity && a.Activity == b.Activity &&
        a.ServiceId == b.ServiceId && a.RawQuantity == b.RawQuantity &&
        a.SourceDemandPressure == b.SourceDemandPressure &&
        a.BuyerDemandPressure == b.BuyerDemandPressure &&
        SameIndex(a.SourceAveragePriceIndex, b.SourceAveragePriceIndex) &&
        SameIndex(a.BuyerAveragePriceIndex, b.BuyerAveragePriceIndex) &&
        SameIndex(a.SourceLastMarginalPriceIndex,
            b.SourceLastMarginalPriceIndex) &&
        SameIndex(a.BuyerLastMarginalPriceIndex,
            b.BuyerLastMarginalPriceIndex) &&
        SameIndex(a.SourceGrossRevenueIndex, b.SourceGrossRevenueIndex) &&
        SameIndex(a.BuyerGrossRevenueIndex, b.BuyerGrossRevenueIndex) &&
        a.AuthenticatedNominalFreightCost.CompareTo(
            b.AuthenticatedNominalFreightCost) == 0;

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var reconstructed = Rebuild(_portfolio, _candidate,
            _quantity, _congestion);
        if (!SameObservation(Gentle, reconstructed.Gentle) ||
            !SameObservation(Steep, reconstructed.Steep))
            throw new InvalidOperationException(
                "Endogenous remote/local price comparison or authenticated freight cost diverged.");
        RequireCurrent(_session);
    }

    public static CanonicalEndogenousFreightPriceComparison FromPortfolio(
        CanonicalPrimitiveFreightPortfolio portfolio,
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveFreightCandidateUse candidate,
        ulong rawQuantity,
        ExperimentalSharedFreightCongestionPolicy congestionPolicy)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(congestionPolicy);
        portfolio.RequireCurrent(session);
        // Record equality is insufficient: must be exact authenticated
        // candidate object in this session's VS64/VS63 discovery inventory.
        if (!portfolio.CandidateUses.Any(x => ReferenceEquals(x, candidate)))
            throw new ArgumentException(
                "Caller cannot forge a freight candidate for price comparison.",
                nameof(candidate));
        var evaluated = Rebuild(portfolio, candidate, rawQuantity,
            congestionPolicy);
        var result = new CanonicalEndogenousFreightPriceComparison(
            portfolio, session, candidate, rawQuantity, congestionPolicy,
            evaluated.Gentle, evaluated.Steep);
        result.VerifyIndependent();
        return result;
    }
}
