using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Explicit alternative economic phase. Production is allocated workforce,
/// demand is canonical residents; all food offers and marginal-price bids are
/// supplied, and joint convex congestion is priced BEFORE final selection.
/// Does not use legacy RawEffectiveCapacity nor mutate authoritative state.
/// </summary>
public sealed class EconomicResidentCongestionPricedTurnPhase : ITurnResolutionPhase
{
    private readonly WorldGenerationResult _generatedWorld;
    private readonly StrategicEdgeAccessSnapshot _edgeAccess;
    private readonly EconomicResidentProductionDemandTurnPhase _flowPhase;
    private readonly EconomicCongestionDemandBid[] _bids;
    private readonly EconomicCongestionRouteOffer[] _offers;
    private readonly FoodCommodityCatalog _catalog;
    private readonly FoodStrategicEdgeCongestionProfile[] _profiles;
    private readonly EconomicCongestionMarketSearchPolicy _policy;
    private readonly StrategicCongestionRoutingMode _routingMode;
    private readonly EconomicCongestionPricedFoodMarketResolver _marketResolver = new();

    public string PhaseId => EconomicProductionDemandTurnPhase.StablePhaseId;

    public EconomicResidentCongestionPricedTurnPhase(
        WorldGenerationResult generatedWorld,
        StrategicEdgeAccessSnapshot edgeAccess,
        IEnumerable<EconomicProductionPotential> productionPotentials,
        IEnumerable<EconomicResidentDemandProfile> residentDemandProfiles,
        IEnumerable<EconomicCongestionDemandBid> bids,
        IEnumerable<EconomicCongestionRouteOffer> routeOffers,
        FoodCommodityCatalog foodCatalog,
        IEnumerable<FoodStrategicEdgeCongestionProfile> profiles,
        EconomicCongestionMarketSearchPolicy policy,
        StrategicCongestionRoutingMode routingMode =
            StrategicCongestionRoutingMode.FixedShortestHop)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(edgeAccess);
        ArgumentNullException.ThrowIfNull(bids);
        ArgumentNullException.ThrowIfNull(routeOffers);
        ArgumentNullException.ThrowIfNull(foodCatalog);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(policy);
        if (routingMode != StrategicCongestionRoutingMode.FixedShortestHop &&
            routingMode != StrategicCongestionRoutingMode.AdaptiveLowestMarginalCost)
            throw new ArgumentOutOfRangeException(nameof(routingMode));

        _generatedWorld = generatedWorld;
        _edgeAccess = edgeAccess;
        _flowPhase = new EconomicResidentProductionDemandTurnPhase(
            productionPotentials, residentDemandProfiles);
        _bids = bids.ToArray();
        _offers = routeOffers.ToArray();
        _profiles = profiles.ToArray();
        if (_bids.Any(row => row is null) ||
            _offers.Any(row => row is null) ||
            _profiles.Any(row => row is null))
            throw new ArgumentException(
                "Congestion-priced economic turn collections cannot contain null entries.");
        _catalog = foodCatalog;
        _policy = policy;
        _routingMode = routingMode;
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);

        var flowResolution = _flowPhase.Resolve(planningInput, stagedState);
        var flow = (EconomicProductionDemandResolvedEvent)flowResolution.Events.Single();

        // All validations and market decisions finish before returning events.
        var market = _marketResolver.Resolve(
            _generatedWorld, stagedState, _edgeAccess,
            flow.Flows, _bids, _offers, _catalog, _profiles, _policy, _routingMode);
        var marketEvent = new EconomicCongestionPricedMarketResolvedEvent(
            EventId.ForSystemPhase(
                planningInput.Context.Turn, PhaseId, ordinal: 2UL), market);

        return new TurnResolutionResult(
            stagedState,
            new ISimulationEvent[] { flow, marketEvent },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(
                    1UL, flow, wasEligible: true, wasExecuted: true),
                new SimulationEventLogEntry(
                    2UL, marketEvent, wasEligible: true, wasExecuted: true)
            }));
    }
}
