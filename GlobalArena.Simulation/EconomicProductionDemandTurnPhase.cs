using GlobalArena.Kernel;
using GlobalArena.Runtime;

namespace GlobalArena.Simulation;

/// <summary>
/// Bridges the existing pure M4.3 production/demand resolver into the phased
/// authoritative turn engine. Source potentials/profiles are explicit input
/// snapshots, not invented worldgen defaults. A later worldgen adapter may
/// provide these without changing the economic resolver.
/// </summary>
public sealed class EconomicProductionDemandTurnPhase : ITurnResolutionPhase
{
    public const string StablePhaseId = "economy.production-demand";

    private readonly EconomicProductionPotential[] _productionPotentials;
    private readonly EconomicDemandProfile[] _demandProfiles;
    private readonly EconomicProductionDemandResolver _resolver = new();

    public string PhaseId => StablePhaseId;

    public EconomicProductionDemandTurnPhase(
        IEnumerable<EconomicProductionPotential> productionPotentials,
        IEnumerable<EconomicDemandProfile> demandProfiles)
    {
        ArgumentNullException.ThrowIfNull(productionPotentials);
        ArgumentNullException.ThrowIfNull(demandProfiles);

        _productionPotentials = productionPotentials.ToArray();
        _demandProfiles = demandProfiles.ToArray();

        if (_productionPotentials.Any(potential => potential is null))
            throw new ArgumentException(
                "Production potentials cannot contain null entries.",
                nameof(productionPotentials));

        if (_demandProfiles.Any(profile => profile is null))
            throw new ArgumentException(
                "Demand profiles cannot contain null entries.",
                nameof(demandProfiles));
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput planningInput,
        WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(planningInput);
        ArgumentNullException.ThrowIfNull(stagedState);

        // Use the staged economic snapshot, which may contain validated
        // changes from earlier turn phases. Do not mutate or persist flows.
        var flows = _resolver.Resolve(
            stagedState.Economy,
            _productionPotentials,
            _demandProfiles);

        var resolvedEvent = new EconomicProductionDemandResolvedEvent(
            EventId.ForSystemPhase(
                planningInput.Context.Turn,
                PhaseId,
                ordinal: 1UL),
            flows);

        // Emit exactly one audit event per evaluated turn, including an empty
        // economy. The world instance and canonical state hash stay unchanged.
        return new TurnResolutionResult(
            stagedState,
            new ISimulationEvent[] { resolvedEvent },
            new SimulationEventLog(new[]
            {
                new SimulationEventLogEntry(
                    1UL,
                    resolvedEvent,
                    wasEligible: true,
                    wasExecuted: true)
            }));
    }
}
