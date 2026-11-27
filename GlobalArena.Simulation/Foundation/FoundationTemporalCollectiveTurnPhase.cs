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
public sealed record FoundationPersistentOrderEditCommand(
    CommandId Id, FoundationPersistentOrderEdit Edit) : ISimulationCommand
{
    public CivilizationId Issuer => Edit.Issuer;
}

public sealed record FoundationFundedWorkerIntentCommand(
    CommandId Id, CivilizationId Issuer, PhysicalTacticalEdgeId Edge,
    ulong MoneyMicro, IReadOnlyList<FoundationFundedWorkerInsertion> Assignments) : ISimulationCommand;

public sealed record FoundationProductiveFundingCommand(
    CommandId Id, CivilizationId Issuer, FoundationProductiveTarget Target,
    ulong MoneyMicro) : ISimulationCommand;

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
    private readonly FoundationPersistentWorkerTurnScheduler? _persistent;
    private readonly FoundationFundedWorkerIntentJournal? _funded;
    private readonly bool _growth;
    private readonly bool _navalCollective;
    private readonly bool _navalSettlement;
    private readonly FoundationAgriculturalProductionPolicy? _agriculture;
    private readonly FoundationOperationalFreightShadowPolicy? _operationalPolicy;

    public FoundationTemporalCollectiveTurnPhase(
        WorldGenerationResult world,
        PhysicalTacticalIncidenceMap incidence,
        FoundationGroundEdgeTravelTimePolicy movementPolicy,
        FoundationAuthoritativeEconomicSettlementPolicy economy,
        FoundationFineFirstPhysicalGeography? fine = null,
        FoundationFineFirstWorldgenIdentity? signature = null,
        FoundationConstructionCollectiveTurnPhase? construction = null,
        ulong? persistentMoneyMicroPerWholeWork = null, bool enableDemographicGrowth = false, FoundationAgriculturalProductionPolicy? agriculture = null, FoundationOperationalFreightShadowPolicy? operationalPolicy = null,
        bool enableExperimentalAutomaticBase = false, bool enableLocalPriceAllocation = false,
        bool enableDefaultAutomaticBase = false, bool enableExclusiveNodeLabor = false,
        ulong? navalDepartureTimeMicro = null,
        IReadOnlyDictionary<CivilizationId, FoundationCivilizationKnowledgeScope>? navalScopes = null, bool enableNavalSettlement = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        _incidence = incidence ?? throw new ArgumentNullException(nameof(incidence));
        _movementPolicy = movementPolicy ?? throw new ArgumentNullException(nameof(movementPolicy));
        _movementPolicy.RequireValid();
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        if ((fine is null) != (signature is null))
            throw new InvalidOperationException(
                "Temporal collective settlement requires complete signed geography.");
        if (enableExperimentalAutomaticBase && (!persistentMoneyMicroPerWholeWork.HasValue || agriculture is null || fine is null))
            throw new InvalidOperationException("Experimental automatic base requires signed persistent crop scheduling.");
        if (economy.UseTemporalProgressiveDistribution && !enableLocalPriceAllocation)
            throw new InvalidOperationException("Progressive distribution requires the explicit local allocation experiment.");
        if (enableLocalPriceAllocation && !enableExperimentalAutomaticBase)
            throw new InvalidOperationException("Local allocation requires experimental automatic scheduling.");
        if (agriculture?.BasicResources is not null && !enableLocalPriceAllocation)
            throw new InvalidOperationException("Basic resource production requires explicit local allocation.");
        if (enableDefaultAutomaticBase && (!enableLocalPriceAllocation || agriculture?.BasicResources is null))
            throw new InvalidOperationException("Default automatic base requires the complete local economy experiment.");
        _navalCollective = navalDepartureTimeMicro.HasValue;
        _navalSettlement = enableNavalSettlement;
        if (_navalSettlement && (!_navalCollective || operationalPolicy is not null))
            throw new InvalidOperationException("Naval settlement requires collective capital-only naval QA.");
        if (_navalCollective && (!persistentMoneyMicroPerWholeWork.HasValue || fine is null ||
            navalDepartureTimeMicro == 0 || !enableLocalPriceAllocation || !enableDefaultAutomaticBase))
            throw new InvalidOperationException("Collective naval QA requires signed persistent local economy and explicit positive duration.");
        if (!_navalCollective && navalScopes is not null)
            throw new InvalidOperationException("Naval knowledge requires collective naval QA.");
        _growth = enableDemographicGrowth;
        _agriculture = agriculture;
        _operationalPolicy = operationalPolicy;
        operationalPolicy?.RequireValid();
        if (operationalPolicy is not null && (!persistentMoneyMicroPerWholeWork.HasValue || fine is null)) throw new InvalidOperationException("Operational freight requires signed persistent scheduling.");
        if (agriculture is not null && (!persistentMoneyMicroPerWholeWork.HasValue || !ReferenceEquals(fine, agriculture.Geography))) throw new InvalidOperationException("Crop policy requires signed persistent scheduling.");
        if (_growth && !persistentMoneyMicroPerWholeWork.HasValue) throw new InvalidOperationException("Demographic growth requires persistent scheduling.");
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
        if (persistentMoneyMicroPerWholeWork.HasValue)
        {
            if (fine is null || construction is not null)
                throw new InvalidOperationException("Persistent scheduling requires signed geography and exclusive construction execution.");
            _persistent = new FoundationPersistentWorkerTurnScheduler(new FoundationConstructionApproachRouter(
                world, incidence, fine, signature!, movementPolicy), agriculture,
                enableExperimentalAutomaticBase ? economy : null,
                enableExperimentalAutomaticBase ? operationalPolicy : null, enableLocalPriceAllocation, enableDefaultAutomaticBase, enableExclusiveNodeLabor,
                enableNavalContinuation: _navalCollective, navalDepartureTimeMicro: navalDepartureTimeMicro,
                navalScopes: navalScopes);
            _funded = new FoundationFundedWorkerIntentJournal(new FoundationConstructionFundingExecutor(
                _capitalExecutor, persistentMoneyMicroPerWholeWork.Value));
        }
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
        if (stagedState.WorkerOrders.IsInitialized != (_persistent is not null))
            throw new InvalidOperationException("Persistent queue deployment and collective phase must match.");
        if ((stagedState.EdgeTravels.NavalVoyages is not null) != _navalCollective)
            throw new InvalidOperationException("Naval ledger deployment and collective phase must match.");
        if (stagedState.EdgeTravels.NavalCargoMicroPerWorker.HasValue && _operationalPolicy is not null)
            throw new InvalidOperationException("Priced naval QA requires the canonical capital-only freight profile until operational freight quotes are integrated.");
        if (_persistent is not null) return ResolvePersistent(input, stagedState);
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

    private TurnResolutionResult ResolvePersistent(TurnResolutionInput input, WorldState beginning)
    {
        using var profile = FoundationTurnProfiler.Measure("persistent_resolution");
        if (input.Commands.Any(c => c.Id.Turn != input.Context.Turn || c.Id.Sequence == 0) ||
            input.Commands.Select(c => c.Id).Distinct().Count() != input.Commands.Count)
            throw new InvalidOperationException("Persistent collective commands must be unique and current.");
        var state = beginning;
        var operations = new List<ISimulationEvent>();
        foreach (var command in input.Commands.OrderBy(c => c.Id.Sequence))
        {
            var id = new EventId(command.Id, 1UL);
            if (command is FoundationPersistentOrderEditCommand edit)
            {
                var recorded = FoundationPersistentOrderEditJournal.Record(state, id, edit.Edit, input.Context);
                state = recorded.Next;
                operations.Add(recorded.Event);
            }
            else if (command is FoundationFundedWorkerIntentCommand funding)
            {
                var recorded = _funded!.Record(state, id, funding.Issuer, funding.Edge,
                    funding.MoneyMicro, funding.Assignments, input.Context);
                state = recorded.Next;
                operations.Add(recorded.Event);
            }
            else if (command is FoundationProductiveFundingCommand productive)
            {
                var recorded = _funded!.RecordProductive(state, id, productive.Issuer,
                    productive.Target, productive.MoneyMicro, input.Context);
                state = recorded.Next;
                operations.Add(recorded.Event);
            }
            else throw new InvalidOperationException("Legacy commands cannot compete with persistent scheduling.");
        }
        var timeline = _persistent!.Record(state, input.Context);
        operations.Add(timeline.Event);
        var receipt = FoundationTemporalEconomicJournalGate.CloseAndReplayWithInvestments(
            beginning, operations, _movement, _movementPolicy, _incidence, _economy, input.Context,
            _capitalExecutor, _fine, persistentScheduler: _persistent, fundedIntents: _funded, enableDemographicGrowth: _growth, agriculture: _agriculture, operationalPolicy: _operationalPolicy, enableNavalSettlement: _navalSettlement);
        if (receipt.EconomicEvent.PreSettlementHash != new CanonicalWorldStateHasher().Compute(timeline.Next))
            throw new InvalidOperationException("Persistent timeline and economic settlement diverged.");
        return new TurnResolutionResult(receipt.Next,
            receipt.Journal.Entries.Select(e => e.Event).ToArray(), receipt.Journal);
    }

    public WorldState ReplayPersistentJournal(WorldState beginning, SimulationEventLog log, SimulationContext context)
    {
        if (_persistent is null) throw new InvalidOperationException("Persistent phase not configured.");
        return FoundationTemporalEconomicJournalGate.ReplayJournal(beginning, log, _movement,
            _movementPolicy, _incidence, _economy, context, _fine, _capitalExecutor,
            persistentScheduler: _persistent, fundedIntents: _funded, enableDemographicGrowth: _growth, agriculture: _agriculture, operationalPolicy: _operationalPolicy, enableNavalSettlement: _navalSettlement);
    }
}
