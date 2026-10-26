using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Optional single-phase resident-demand, market-allocation and food-demand
/// trade-coverage observation. Does not change the accepted production, market
/// or hash contracts and must replace, not accompany, the other
/// economy.production-demand phases in a turn.
/// </summary>
public sealed class EconomicResidentFoodMarketTurnPhase : ITurnResolutionPhase
{
    private readonly EconomicResidentMarketTurnPhase _marketPhase;
    private readonly FoodCommodityCatalog _catalog;
    private readonly FoodMarketDemandCoverageResolver _coverageResolver = new();

    public string PhaseId => EconomicProductionDemandTurnPhase.StablePhaseId;

    public EconomicResidentFoodMarketTurnPhase(
        IEnumerable<EconomicProductionPotential> productionPotentials,
        IEnumerable<EconomicResidentDemandProfile> residentDemandProfiles,
        IEnumerable<EconomicTransferSignal> transferSignals,
        EconomicMarketAllocationPolicy marketPolicy,
        FoodCommodityCatalog foodCatalog)
    {
        ArgumentNullException.ThrowIfNull(foodCatalog);
        _marketPhase = new EconomicResidentMarketTurnPhase(
            productionPotentials, residentDemandProfiles,
            transferSignals, marketPolicy);
        _catalog = foodCatalog;
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput,
        WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);

        // Delegate to accepted, pure production/consumer/market solvers.
        var resolvedMarket = _marketPhase.Resolve(planningInput, stagedState);
        var flow = (EconomicProductionDemandResolvedEvent)resolvedMarket.Events[0];
        var market = (EconomicMarketAllocationResolvedEvent)resolvedMarket.Events[1];

        // Fail atomically before publishing any event or state if coverage
        // validation detects a contradictory market allocation.
        var coverage = _coverageResolver.Resolve(
            flow.Flows, market.Allocation, _catalog);
        var coverageEvent = new EconomicFoodMarketCoverageResolvedEvent(
            EventId.ForSystemPhase(
                planningInput.Context.Turn, PhaseId, ordinal: 3UL),
            coverage);

        return new TurnResolutionResult(
            stagedState,
            new ISimulationEvent[] { flow, market, coverageEvent },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(
                    1UL, flow, wasEligible: true, wasExecuted: true),
                new SimulationEventLogEntry(
                    2UL, market, wasEligible: true, wasExecuted: true),
                new SimulationEventLogEntry(
                    3UL, coverageEvent, wasEligible: true, wasExecuted: true)
            }));
    }
}
