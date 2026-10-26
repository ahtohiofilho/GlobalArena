using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Optional single economic phase that emits production/demand, profitable
/// market allocation, commercial food coverage and finally *observed* food
/// access, in that order. No observation is produced from an allocation.
/// Caller must provide a turn-scoped access observation snapshot.
/// </summary>
public sealed class EconomicResidentFoodAccessTurnPhase : ITurnResolutionPhase
{
    private readonly EconomicResidentFoodMarketTurnPhase _foodMarketPhase;
    private readonly FoodCommodityCatalog _catalog;
    private readonly FoodAccessObservationSnapshot _observations;
    private readonly FoodDemandAccessCoverageResolver _accessResolver = new();

    public string PhaseId => EconomicProductionDemandTurnPhase.StablePhaseId;

    public EconomicResidentFoodAccessTurnPhase(
        IEnumerable<EconomicProductionPotential> productionPotentials,
        IEnumerable<EconomicResidentDemandProfile> residentDemandProfiles,
        IEnumerable<EconomicTransferSignal> transferSignals,
        EconomicMarketAllocationPolicy marketPolicy,
        FoodCommodityCatalog catalog,
        FoodAccessObservationSnapshot observations)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(observations);

        _foodMarketPhase = new EconomicResidentFoodMarketTurnPhase(
            productionPotentials, residentDemandProfiles,
            transferSignals, marketPolicy, catalog);
        _catalog = catalog;
        _observations = observations;
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput,
        WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);

        // Do not publish any prior event if observations are stale.
        if (_observations.Turn != planningInput.Context.Turn)
            throw new InvalidOperationException(
                "Access snapshot is not from the current economic turn.");

        var baseResult = _foodMarketPhase.Resolve(planningInput, stagedState);
        var commercialCoverage = (EconomicFoodMarketCoverageResolvedEvent)
            baseResult.Events[2];

        var accessResult = _accessResolver.Resolve(
            commercialCoverage.Coverage,
            _catalog,
            _observations,
            planningInput.Context.Turn);

        var observedEvent = new EconomicFoodAccessCoverageResolvedEvent(
            EventId.ForSystemPhase(
                planningInput.Context.Turn, PhaseId, ordinal: 4UL),
            accessResult);

        return new TurnResolutionResult(
            stagedState,
            new ISimulationEvent[]
            {
                baseResult.Events[0],
                baseResult.Events[1],
                baseResult.Events[2],
                observedEvent
            },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(
                    1UL, baseResult.Events[0],
                    wasEligible: true, wasExecuted: true),
                new SimulationEventLogEntry(
                    2UL, baseResult.Events[1],
                    wasEligible: true, wasExecuted: true),
                new SimulationEventLogEntry(
                    3UL, baseResult.Events[2],
                    wasEligible: true, wasExecuted: true),
                new SimulationEventLogEntry(
                    4UL, observedEvent,
                    wasEligible: true, wasExecuted: true)
            }));
    }
}
