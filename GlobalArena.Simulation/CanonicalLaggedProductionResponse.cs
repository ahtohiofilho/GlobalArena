using System.Numerics;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// A certified frozen VS83 local-market quote driving ONE subsequent
/// hypothetical production response. Price is dimensionless, not money.
/// </summary>
public sealed record CanonicalLaggedProductionActivitySignal(
    EconomicActivityKind Activity,
    BigInteger PreviouslyLocallyAbsorbedRaw,
    CanonicalEndogenousPriceIndex FrozenWeightedLocalPriceIndex,
    int PriorUtilizationPermille,
    int BoundedUtilizationDeltaPermille,
    int NextUtilizationPermille,
    int UnchangedRetentionPermille);

internal sealed record CanonicalLaggedProductionResponseComputation(
    CanonicalLaggedProductionActivitySignal[] Signals,
    CanonicalWorkforceRealizationProjection NextProjection);

/// <summary>
/// VS84 experimental two-step lab. A prior authenticated VS83 local
/// macro-absorption report is FROZEN before the next hypothetical production
/// decision, avoiding circular same-step price/supply solving. Both
/// projections are in the same live session: this is NOT proof of an actual
/// movement/economic-cycle advance or persisted historical state.
/// </summary>
public sealed class CanonicalLaggedProductionResponse
{
    public const int MaximumResponseStepPermille = 250;
    private static readonly CanonicalEndogenousPriceIndex One = new(1, 1);
    private static readonly CanonicalEndogenousPriceIndex Neutral = new(1, 2);
    private readonly CanonicalPhysicalTacticalSession _session;
    private readonly CanonicalPrimitiveSiteDiscovery _sites;
    private readonly CanonicalSimultaneousFreightReference _freight;
    private readonly CanonicalWorkforceRealizationProjection _prior;
    private readonly CanonicalSimultaneousFreightLot[] _nextRemoteLots;
    private readonly ExperimentalSharedFreightCongestionPolicy _congestion;
    private readonly int _step;

    public IReadOnlyList<CanonicalLaggedProductionActivitySignal> Signals { get; }
    public CanonicalWorkforceRealizationProjection NextProjection { get; }
    public ulong SourceMovementTurn => _sites.MovementTurn;
    public bool IsRealHistoricalCycleAdvance => false;
    public bool UsesFrozenPriorLocalPrices => true;
    public bool UsesFuturePricesToDecideCurrentProduction => false;
    public bool ComputesNominalNetProfit => false;
    public bool InventsRemoteBuyersOrBuyerBudget => false;
    public bool CapsProductionByResidentDemand => false;
    public bool CreatesInvoluntaryStockOrCredit => false;
    public bool ChangesRealWorkerAssignments => false;
    public bool CanAuthorizeRealSettlement => false;

    private CanonicalLaggedProductionResponse(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freight,
        CanonicalWorkforceRealizationProjection prior,
        CanonicalSimultaneousFreightLot[] remoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion,
        int step,
        CanonicalLaggedProductionResponseComputation built)
    {
        _session = session;
        _sites = sites;
        _freight = freight;
        _prior = prior;
        _nextRemoteLots = (CanonicalSimultaneousFreightLot[])remoteLots.Clone();
        _congestion = congestion;
        _step = step;
        Signals = Array.AsReadOnly(built.Signals);
        NextProjection = built.NextProjection;
    }

    public void RequireCurrent(CanonicalPhysicalTacticalSession session)
    {
        if (!ReferenceEquals(session, _session))
            throw new InvalidOperationException(
                "Lagged market trial cannot be reused for another physical session.");
        _sites.RequireCurrent(session);
        _freight.RequireCurrent(session);
        _prior.RequireCurrent(session);
        NextProjection.RequireCurrent(session);
    }

    /// <summary>
    /// Experimental neutral price is 1/2, NOT a monetary equilibrium.
    /// The exact bounded response is trunc((2*p-1)*maxStep), clamped to
    /// [-maxStep,+maxStep]. p is a real VS78 relative price in [0,1].
    /// This pure calibration helper never certifies quote provenance:
    /// only FromFrozenProjection can authorize its input as genuine.
    /// </summary>
    public static int CalculateBoundedResponse(
        CanonicalEndogenousPriceIndex price, int maximumStepPermille)
    {
        if (maximumStepPermille is < 1 or > MaximumResponseStepPermille)
            throw new ArgumentOutOfRangeException(nameof(maximumStepPermille));
        if (price.Denominator <= 0 || price.Numerator < 0 ||
            price.CompareTo(One) > 0)
            throw new ArgumentOutOfRangeException(nameof(price),
                "Feedback requires a genuine dimensionless index between 0 and 1.");
        return checked((int)(
            ((2 * price.Numerator - price.Denominator) *
             maximumStepPermille) / price.Denominator));
    }

    private static int ClampUtilization(int prior, int delta) =>
        Math.Clamp(checked(prior + delta), 0,
            CanonicalWorkforceRealizationProjection.Permille);

    private static CanonicalLaggedProductionResponseComputation Reconstruct(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freight,
        CanonicalWorkforceRealizationProjection prior,
        CanonicalSimultaneousFreightLot[] nextRemoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion,
        int maximumStepPermille)
    {
        sites.RequireCurrent(session);
        freight.RequireCurrent(session);
        prior.RequireCurrent(session);
        prior.VerifyIndependent();
        var previousAllocation = prior.Allocation ??
            throw new InvalidOperationException(
                "A previous cycle with no realized local offer supplies no frozen observed price; fail closed rather than invent a neutral quote.");
        previousAllocation.VerifyIndependent();

        var flows = sites.Flows.SupplyFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId, x.Activity),
            x => x.RawQuantity);
        var oldFlows = prior.Sources.ToDictionary(
            x => (x.Source, x.Commodity, x.Activity), x => x);
        if (oldFlows.Count != flows.Count ||
            oldFlows.Any(x => !flows.TryGetValue(x.Key, out var available) ||
                available != x.Value.PotentialRaw))
            throw new InvalidOperationException(
                "Frozen workforce quotation does not belong to the current real physical production and worker assignment.");

        var demands = sites.Flows.DemandFlows.ToDictionary(
            x => (x.EconomicPointId, x.CommodityId), x => x.RawQuantity);
        var localPrices = previousAllocation.LocalMarkets.ToDictionary(
            x => (x.LocalMarket, x.Commodity), x => x);
        foreach (var quote in previousAllocation.LocalMarkets)
        {
            if (!demands.TryGetValue((quote.LocalMarket, quote.Commodity),
                    out var demand) ||
                quote.ResidentDemandPressureRaw != demand)
                throw new InvalidOperationException(
                    "Frozen local price originated with a different resident demand profile.");
        }

        var oldSources = previousAllocation.Sources.ToDictionary(
            x => (x.Source, x.Commodity, x.Activity), x => x);
        var signals = new List<CanonicalLaggedProductionActivitySignal>();
        var nextPolicies = new List<CanonicalWorkforceActivityRealizationPolicy>();
        foreach (var policy in prior.Policies.OrderBy(x => x.Activity))
        {
            var localQuantity = BigInteger.Zero;
            var sumWeightedPrice = CanonicalEndogenousPriceIndex.Zero;
            foreach (var entry in prior.Sources.Where(x =>
                x.Activity == policy.Activity).OrderBy(x => x.Source.Value)
                .ThenBy(x => x.Commodity.Value))
            {
                if (!oldSources.TryGetValue(
                        (entry.Source, entry.Commodity, entry.Activity),
                        out var balance))
                {
                    if (entry.DerivedProducedRaw == 0)
                        continue;
                    throw new InvalidOperationException(
                        "Missing frozen physical allocation for productive local source.");
                }
                var q = balance.OfferedAndLocallyAbsorbedRaw;
                if (q == 0)
                    continue;
                if (q < 0 || !localPrices.TryGetValue(
                        (entry.Source, entry.Commodity), out var quote))
                    throw new InvalidOperationException(
                        "A productive source is missing the actual frozen local price.");
                var p = quote.GentleAveragePriceIndex;
                if (p.Denominator <= 0 || p.Numerator < 0 ||
                    p.CompareTo(One) > 0)
                    throw new InvalidOperationException(
                        "Frozen local price must be a genuine relative VS78 index.");
                localQuantity += q;
                sumWeightedPrice = CanonicalEndogenousPriceIndex.Add(
                    sumWeightedPrice, p.Times(q));
            }
            if (localQuantity <= 0)
                throw new InvalidOperationException(
                    "No previously observed local sale quantity exists for this activity; remote or retained output cannot be fabricated into a local price.");
            var frozen = sumWeightedPrice.DividedBy(localQuantity);
            var delta = CalculateBoundedResponse(frozen, maximumStepPermille);
            var next = ClampUtilization(policy.UtilizationPermille, delta);
            signals.Add(new(policy.Activity, localQuantity, frozen,
                policy.UtilizationPermille, delta, next,
                policy.RetentionPermille));
            nextPolicies.Add(new(policy.Activity, next,
                policy.RetentionPermille));
        }
        var projected = CanonicalWorkforceRealizationProjection.FromSites(
            session, sites, freight, nextPolicies, nextRemoteLots, congestion);
        sites.RequireCurrent(session);
        freight.RequireCurrent(session);
        prior.RequireCurrent(session);
        return new(signals.ToArray(), projected);
    }

    private static bool SameSignal(
        CanonicalLaggedProductionActivitySignal a,
        CanonicalLaggedProductionActivitySignal b) =>
        a.Activity == b.Activity &&
        a.PreviouslyLocallyAbsorbedRaw == b.PreviouslyLocallyAbsorbedRaw &&
        a.FrozenWeightedLocalPriceIndex.CompareTo(
            b.FrozenWeightedLocalPriceIndex) == 0 &&
        a.PriorUtilizationPermille == b.PriorUtilizationPermille &&
        a.BoundedUtilizationDeltaPermille == b.BoundedUtilizationDeltaPermille &&
        a.NextUtilizationPermille == b.NextUtilizationPermille &&
        a.UnchangedRetentionPermille == b.UnchangedRetentionPermille;

    public void VerifyIndependent()
    {
        RequireCurrent(_session);
        var replay = Reconstruct(_session, _sites, _freight,
            _prior, _nextRemoteLots, _congestion, _step);
        if (Signals.Count != replay.Signals.Length ||
            Signals.Where((x, i) => !SameSignal(x, replay.Signals[i])).Any() ||
            !NextProjection.Policies.SequenceEqual(
                replay.NextProjection.Policies) ||
            !NextProjection.Sources.SequenceEqual(
                replay.NextProjection.Sources) ||
            (NextProjection.Allocation is null) !=
                (replay.NextProjection.Allocation is null) ||
            (NextProjection.Allocation is not null &&
                (!NextProjection.Allocation.Sources.SequenceEqual(
                    replay.NextProjection.Allocation!.Sources) ||
                 !NextProjection.Allocation.LocalMarkets.SequenceEqual(
                    replay.NextProjection.Allocation.LocalMarkets) ||
                 NextProjection.Allocation.AuthenticatedNominalFreightCost.CompareTo(
                    replay.NextProjection.Allocation.AuthenticatedNominalFreightCost) != 0)))
            throw new InvalidOperationException(
                "Frozen price signal, lagged activity response or downstream authenticated production diverged in independent replay.");
        NextProjection.VerifyIndependent();
        RequireCurrent(_session);
    }

    public static CanonicalLaggedProductionResponse FromFrozenProjection(
        CanonicalPhysicalTacticalSession session,
        CanonicalPrimitiveSiteDiscovery sites,
        CanonicalSimultaneousFreightReference freight,
        CanonicalWorkforceRealizationProjection previous,
        int maximumResponseStepPermille,
        IEnumerable<CanonicalSimultaneousFreightLot> nextRemoteLots,
        ExperimentalSharedFreightCongestionPolicy congestion)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(freight);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(nextRemoteLots);
        ArgumentNullException.ThrowIfNull(congestion);
        if (maximumResponseStepPermille is < 1 or > MaximumResponseStepPermille)
            throw new ArgumentOutOfRangeException(
                nameof(maximumResponseStepPermille));
        var lots = nextRemoteLots.ToArray();
        if (lots.Length > CanonicalSimultaneousFreightReference.MaximumLots ||
            lots.Any(x => x is null || x.Offer is null || x.RawQuantity == 0))
            throw new ArgumentException(
                "Next-stage scenarios must declare at most 12 genuine positive authenticated lots.",
                nameof(nextRemoteLots));
        var ordered = lots.OrderBy(x => x.Offer.Commodity.Value)
            .ThenBy(x => x.Offer.Activity)
            .ThenBy(x => x.Offer.Source.Value)
            .ThenBy(x => x.Offer.Buyer.Value)
            .ThenBy(x => x.Offer.ServiceId)
            .ThenBy(x => x.Offer.TechnologyTypeId)
            .ThenBy(x => x.Offer.TechnologyCatalogVersion).ToArray();
        var reconstructed = Reconstruct(session, sites, freight,
            previous, ordered, congestion, maximumResponseStepPermille);
        var trial = new CanonicalLaggedProductionResponse(
            session, sites, freight, previous, ordered, congestion,
            maximumResponseStepPermille, reconstructed);
        trial.VerifyIndependent();
        return trial;
    }
}
