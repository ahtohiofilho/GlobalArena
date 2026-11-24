using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// R170 all-ready, opt-in atomic economic phase. Executes canonical local
/// workers, R167 travelers and R14 funded infrastructure in that order, then
/// closes ONE temporal SYSTEM sale through the independently verified journal.
/// An unstable market aborts the entire private resolution, without publishing
/// intermediate worker, capital or treasury states.
/// </summary>
public sealed class FoundationTemporalCollectiveTurnPhase : ITurnResolutionPhase
{
    // The system sale is emitted by THIS sole atomic phase. The generic
    // phase engine requires the SYSTEM event origin to equal its phase ID.
    public const string StablePhaseId =
        FoundationTemporalEconomicJournalGate.StablePhaseId;
    public string PhaseId => StablePhaseId;

    private readonly PhysicalTacticalWorkerMovementTurnPhase _local;
    private readonly FoundationEdgeTravelTurnPhase _travel;
    private readonly FoundationMonetaryInfrastructureTurnPhase _capital;
    private readonly FoundationMonetaryInfrastructureEventExecutor _capitalExecutor;
    private readonly PhysicalTacticalWorkerMoveEventExecutor _movement;
    private readonly PhysicalTacticalIncidenceMap _incidence;
    private readonly FoundationGroundEdgeTravelTimePolicy _movementPolicy;
    private readonly FoundationAuthoritativeEconomicSettlementPolicy _economy;
    private readonly FoundationFineFirstPhysicalGeography? _fine;
    private readonly FoundationConstructionCollectiveTurnPhase? _construction;

    public FoundationTemporalCollectiveTurnPhase(
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationGroundEdgeTravelTimePolicy movementPolicy,
        FoundationAuthoritativeEconomicSettlementPolicy economy,
        FoundationFineFirstPhysicalGeography? fine = null,
        FoundationFineFirstWorldgenIdentity? signature = null,
        FoundationConstructionCollectiveTurnPhase? construction = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _incidence = incidence ?? throw new ArgumentNullException(nameof(incidence));
        _movementPolicy = movementPolicy ?? throw new ArgumentNullException(nameof(movementPolicy));
        _movementPolicy.RequireValid();
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        if ((fine is null) != (signature is null))
            throw new InvalidOperationException(
                "Temporal collective settlement requires complete signed geography.");
        _fine = fine;
        _construction = construction;
        if (fine is null)
        {
            _local = new PhysicalTacticalWorkerMovementTurnPhase(world, incidence);
            _movement = new PhysicalTacticalWorkerMoveEventExecutor(world, incidence);
            _capital = new FoundationMonetaryInfrastructureTurnPhase(world, incidence);
            _capitalExecutor = new FoundationMonetaryInfrastructureEventExecutor(world, incidence);
        }
        else
        {
            _local = new PhysicalTacticalWorkerMovementTurnPhase(world, incidence, fine, signature!);
            _movement = new PhysicalTacticalWorkerMoveEventExecutor(world, incidence, fine, signature!);
            _capital = new FoundationMonetaryInfrastructureTurnPhase(world, incidence, fine, signature!);
            _capitalExecutor = new FoundationMonetaryInfrastructureEventExecutor(
                world, incidence, fine, signature!);
        }
        _travel = new FoundationEdgeTravelTurnPhase(_movement, movementPolicy);
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput input, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(stagedState);
        if (!stagedState.EdgeTravels.IsInitialized ||
            !stagedState.Treasury.IsInitialized ||
            !stagedState.Transport.HasMonetaryConstructionCosts)
            throw new InvalidOperationException(
                "Temporal collective economy requires a complete opt-in physical world.");
        // Each existing phase selects only its own command type.
        var workers = _local.Resolve(input, stagedState);
        var travel = _travel.Resolve(input, workers.ResultingWorldState);
        var capital = _construction is null
            ? _capital.Resolve(input, travel.ResultingWorldState)
            : _construction.Resolve(input, travel.ResultingWorldState);
        var operations = workers.Events.Concat(travel.Events)
            .Concat(capital.Events).ToArray();
        int workCommands = input.Commands.OfType<FoundationConstructionWorkCommand>().Count();
        if (operations.Length != input.Commands.Count - workCommands + (workCommands > 0 ? 1 : 0) ||
            operations.OfType<FoundationEdgeTravelTurnEvent>()
                .Select(ev => ev.WorkerId)
                .Intersect(operations.OfType<PhysicalTacticalWorkerMovedEvent>()
                    .Select(ev => ev.WorkerId)).Any())
            throw new InvalidOperationException(
                "Temporal collective operation journal contains missing or competing orders.");
        var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
            stagedState, operations, _movement, _movementPolicy, _incidence,
            _economy, input.Context, _capitalExecutor, _fine, _construction);
        if (receipt.EconomicEvent.PreSettlementHash !=
                new CanonicalWorldStateHasher().Compute(capital.ResultingWorldState) ||
            receipt.EconomicEvent.PreSettlementRevision !=
                capital.ResultingWorldState.Revision)
            throw new InvalidOperationException(
                "Temporal journal did not settle the exact staged collective state.");
        return new TurnResolutionResult(
            receipt.Next,
            operations.Append((ISimulationEvent)receipt.EconomicEvent).ToArray(),
            receipt.Journal);
    }
}
