using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Optional integrated economic phase: production from allocated workforce,
/// demand from authoritative resident population, and deterministic market
/// allocation over caller-provided transfer signals and explicit market policy.
///
/// Shares the stable economic phase identity with the worker-demand and
/// resident-demand-only alternatives. PhasedTurnResolutionEngine rejects
/// installing two of them in the same turn, preventing accidental double
/// counting. Market settlements remain derived turn events, not WorldState.
/// </summary>
public sealed class EconomicResidentMarketTurnPhase : ITurnResolutionPhase
{
    private readonly EconomicResidentProductionDemandTurnPhase _productionDemandPhase;
    private readonly EconomicTransferSignal[] _transferSignals;
    private readonly EconomicMarketAllocationPolicy _marketPolicy;
    private readonly EconomicMarketAllocationResolver _marketResolver = new();

    public string PhaseId => EconomicProductionDemandTurnPhase.StablePhaseId;

    public EconomicResidentMarketTurnPhase(
        IEnumerable<EconomicProductionPotential> productionPotentials,
        IEnumerable<EconomicResidentDemandProfile> residentDemandProfiles,
        IEnumerable<EconomicTransferSignal> transferSignals,
        EconomicMarketAllocationPolicy marketPolicy)
    {
        ArgumentNullException.ThrowIfNull(transferSignals);
        ArgumentNullException.ThrowIfNull(marketPolicy);

        _productionDemandPhase = new EconomicResidentProductionDemandTurnPhase(
            productionPotentials, residentDemandProfiles);

        _transferSignals = transferSignals.ToArray();
        if (_transferSignals.Any(signal => signal is null))
            throw new ArgumentException(
                "Market transfer signals cannot contain null entries.",
                nameof(transferSignals));

        _marketPolicy = marketPolicy;
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput,
        WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);

        // The resident demand phase uses the staged census and the staged
        // economy, not stale values from the beginning of the turn.
        var productionDemand = _productionDemandPhase.Resolve(
            planningInput, stagedState);

        var flowEvent = (EconomicProductionDemandResolvedEvent)
            productionDemand.Events.Single();

        // Failure is atomic: no event or state is published until the market
        // solver completes and the phase returns the entire result.
        var allocation = _marketResolver.Resolve(
            flowEvent.Flows, _transferSignals, _marketPolicy);

        var marketEvent = new EconomicMarketAllocationResolvedEvent(
            EventId.ForSystemPhase(
                planningInput.Context.Turn,
                PhaseId,
                ordinal: 2UL),
            allocation);

        return new TurnResolutionResult(
            stagedState,
            new ISimulationEvent[] { flowEvent, marketEvent },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(
                    1UL, flowEvent,
                    wasEligible: true, wasExecuted: true),
                new SimulationEventLogEntry(
                    2UL, marketEvent,
                    wasEligible: true, wasExecuted: true)
            }));
    }
}
