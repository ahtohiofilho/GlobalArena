using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Opt-in alternative to EconomicProductionDemandTurnPhase. Produces from
/// allocated workforce but computes consumption from initialized resident
/// population. It emits the same typed economic-flow event and stable phase ID
/// so the two incompatible demand bases must not be installed together.
/// All rates are supplied explicitly by the caller.
/// </summary>
public sealed class EconomicResidentProductionDemandTurnPhase : ITurnResolutionPhase
{
    private readonly EconomicProductionPotential[] _productionPotentials;
    private readonly EconomicResidentDemandProfile[] _residentDemandProfiles;
    private readonly EconomicProductionDemandResolver _productionResolver = new();
    private readonly EconomicResidentDemandResolver _residentResolver = new();

    public string PhaseId => EconomicProductionDemandTurnPhase.StablePhaseId;

    public EconomicResidentProductionDemandTurnPhase(
        IEnumerable<EconomicProductionPotential> productionPotentials,
        IEnumerable<EconomicResidentDemandProfile> residentDemandProfiles)
    {
        ArgumentNullException.ThrowIfNull(productionPotentials);
        ArgumentNullException.ThrowIfNull(residentDemandProfiles);

        _productionPotentials = productionPotentials.ToArray();
        _residentDemandProfiles = residentDemandProfiles.ToArray();

        if (_productionPotentials.Any(potential => potential is null))
            throw new ArgumentException(
                "Production potentials cannot contain null entries.",
                nameof(productionPotentials));
        if (_residentDemandProfiles.Any(profile => profile is null))
            throw new ArgumentException(
                "Resident demand profiles cannot contain null entries.",
                nameof(residentDemandProfiles));
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput,
        WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);

        // Resolve production only; do not also generate workforce-based demand.
        var productionOnly = _productionResolver.Resolve(
            stagedState.Economy,
            _productionPotentials,
            Array.Empty<EconomicDemandProfile>());

        var residentDemands = _residentResolver.Resolve(
            stagedState, _residentDemandProfiles);

        var flows = new EconomicProductionDemandResult(
            productionOnly.SupplyFlows, residentDemands);

        var resolvedEvent = new EconomicProductionDemandResolvedEvent(
            EventId.ForSystemPhase(
                planningInput.Context.Turn, PhaseId, ordinal: 1UL),
            flows);

        return new TurnResolutionResult(
            stagedState,
            new ISimulationEvent[] { resolvedEvent },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(
                    1UL, resolvedEvent,
                    wasEligible: true, wasExecuted: true)
            }));
    }
}
