namespace GlobalArena.Foundation.Circulation;

/// <summary>
/// Market snapshot supplied by a prior, external clearing algorithm.
/// Quantity is the TOTAL sold by all origins and civilizations, not one lot.
/// All quantities and demands are fixed point with Scale=1_000_000.
/// No consumer wallet, inventory or money balance is represented.
/// </summary>
public readonly record struct DestinationMarketInput(
    long DestinationNodeId,
    long CommodityId,
    long DemandRaw,
    long TotalSoldQuantityRaw);

/// <summary>
/// A civilization's already incorporated capital on ONE edge.
/// Its ownership right never follows from territorial sovereignty.
/// </summary>
public readonly record struct EdgeCapitalHolding(
    long CivilizationId,
    long IncorporatedCapitalRaw);

/// <summary>
/// Local tariff reference and coownership observed in the same clearing
/// snapshot. The edge is NOT a persistent segment of a stored itinerary.
/// </summary>
public sealed record TransientEdgeUse(
    long StableEdgeId,
    EdgeTariffReference Tariff,
    IReadOnlyList<EdgeCapitalHolding> Holdings);

/// <summary>
/// A transient decomposition of an already-cleared sale to a destination
/// market. The evaluator DOES NOT choose its route, destination or volume.
/// Stable FlowId is a deterministic identity for this settlement batch,
/// not an authorization to run the journal twice.
/// </summary>
public sealed record TransientDestinationSale(
    long FlowId,
    long OriginNodeId,
    long DestinationNodeId,
    long CommodityId,
    long SellerCivilizationId,
    long SoldQuantityRaw,
    IReadOnlyList<TransientEdgeUse> UsedEdges);

public readonly record struct DestinationMarketQuote(
    long DestinationNodeId,
    long CommodityId,
    long TotalSoldQuantityRaw,
    long DemandRaw,
    long PriceRaw,
    long GrossRevenueRaw);

public readonly record struct TariffOwnerCredit(
    long StableEdgeId,
    long OwnerCivilizationId,
    long CreditRaw);

public sealed record TransientSaleRevenue(
    long FlowId,
    long OriginNodeId,
    long DestinationNodeId,
    long CommodityId,
    long SellerCivilizationId,
    long SoldQuantityRaw,
    long DestinationPriceRaw,
    long GrossRevenueRaw,
    long ExternalWeightRaw,
    long SellerNetRaw,
    IReadOnlyList<TariffOwnerCredit> ExternalOwnerCredits);

public readonly record struct CivilizationRevenueCredit(
    long CivilizationId,
    long CreditRaw);

/// <summary>
/// Immutable financial projection for ONE batch, without applying treasury
/// updates. The implicit outside consumer sector supplies TotalGrossRaw;
/// it is NOT an account to debit. The runtime/journal must apply this
/// projection once, causally and idempotently, after clearing convergence.
/// </summary>
public sealed record DestinationRevenueProjection(
    IReadOnlyList<DestinationMarketQuote> Markets,
    IReadOnlyList<TransientSaleRevenue> Sales,
    IReadOnlyList<CivilizationRevenueCredit> CivilizationCredits,
    long TotalGrossRaw,
    long TotalCreditsRaw);
