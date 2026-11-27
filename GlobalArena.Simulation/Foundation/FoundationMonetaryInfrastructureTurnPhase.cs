using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

/// <summary>
/// Capital investment is a monetary civilization action, NOT worker labor.
/// Amount is expressed in money micro-units and must already exist in the
/// authoritative civilizational treasury. No materials or stocks are debited.
/// </summary>
public sealed record FoundationMonetaryInfrastructureCommand : ISimulationCommand
{
    public CommandId Id { get; }
    public CivilizationId Issuer { get; }
    public PhysicalTacticalEdgeId Edge { get; }
    public ulong MoneyMicro { get; }

    public FoundationMonetaryInfrastructureCommand(
        CommandId id, CivilizationId issuer,
        PhysicalTacticalEdgeId edge, ulong moneyMicro)
    {
        if (id.Turn.Value == 0UL || id.Sequence == 0UL ||
            !issuer.IsValid || !edge.IsValid || moneyMicro == 0UL)
            throw new ArgumentException(
                "Investment commands need one real civilization, " +
                "real physical edge and strictly positive monetary funds.");
        Id = id;
        Issuer = issuer;
        Edge = edge;
        MoneyMicro = moneyMicro;
    }
}

/// <summary>
/// An exact cash debit and one edge capital credit. This is NOT a physical
/// cargo lot, market settlement, employment contract or completed road.
/// </summary>
public sealed record FoundationMonetaryInfrastructureInvestedEvent
    : ISimulationEvent
{
    public EventId Id { get; }
    public CivilizationId Investor { get; }
    public PhysicalTacticalEdgeId Edge { get; }
    public ulong MoneyMicro { get; }
    public ulong ExpectedRevision { get; }

    public FoundationMonetaryInfrastructureInvestedEvent(
        EventId id, CivilizationId investor,
        PhysicalTacticalEdgeId edge, ulong moneyMicro,
        ulong expectedRevision)
    {
        if (id.OriginKind != EventOriginKind.Command ||
            !investor.IsValid || !edge.IsValid || moneyMicro == 0UL)
            throw new ArgumentException(
                "Monetary journal event needs a real originating command, " +
                "positive treasury spend and a real physical edge.");
        Id = id;
        Investor = investor;
        Edge = edge;
        MoneyMicro = moneyMicro;
        ExpectedRevision = expectedRevision;
    }
}

/// <summary>
/// Shared resolution and independent replay executor. The treasury and
/// transport ledgers are debited/credited in the SAME immutable WorldState
/// update; either both pass or neither state is published.
/// </summary>
public sealed class FoundationMonetaryInfrastructureEventExecutor
    : ISimulationEventExecutor
{
    private readonly RuntimeWorldBinding _binding;
    private readonly FoundationFineFirstPhysicalGeography? _productiveGeography;
    private readonly PhysicalTacticalIncidenceMap _productiveIncidence;
    private readonly HashSet<PhysicalTacticalEdgeId> _actualEdges;
    private readonly HashSet<PhysicalTacticalEdgeId> _blockedIceSeaEdges = new();

    public FoundationMonetaryInfrastructureEventExecutor(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence)
        : this(world, incidence,
            RuntimeWorldBinding.FromGeneratedWorld(world)) { }

    public FoundationMonetaryInfrastructureEventExecutor(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine,
        FoundationFineFirstWorldgenIdentity signature)
        : this(world, incidence,
            RuntimeWorldBinding.FromFineFirst(
                world, incidence, fine, signature), fine) { }

    private FoundationMonetaryInfrastructureEventExecutor(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence,
        RuntimeWorldBinding binding,
        FoundationFineFirstPhysicalGeography? fineGeography = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(incidence);
        _productiveGeography = fineGeography;
        _productiveIncidence = incidence;
        if (incidence.Refinement.CoarseParameters !=
            world.Request.StrategicParameters)
            throw new InvalidOperationException(
                "Monetary investment needs the actual bound fine topology.");
        _binding = binding;
        var fine = incidence.Refinement.FineParameters;
        _actualEdges = incidence.FineTopology.Edges.Select(e =>
            new PhysicalTacticalEdgeId(fine, e.Id)).ToHashSet();
        // R95: natural ice/open-water contacts are not buildable ports.
        // Validate this in the event executor so private resolution and
        // independent journal replay make the same decision.
        if (fineGeography is not null)
            foreach (var edge in incidence.FineTopology.Edges)
            {
                int a = checked((int)edge.IncidentCellIds[0].Value - 1);
                int b = checked((int)edge.IncidentCellIds[1].Value - 1);
                if (fineGeography.CrossingAtIndices(a, b) ==
                    FoundationFineTransportCrossingKind.BlockedIceSeaBoundary)
                    _blockedIceSeaEdges.Add(new PhysicalTacticalEdgeId(fine, edge.Id));
            }
    }

    public WorldState Execute(WorldState state,
        ISimulationEvent simulationEvent, SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (simulationEvent is not FoundationMonetaryInfrastructureInvestedEvent ev)
            throw new InvalidOperationException(
                "This executor only applies monetary infrastructure events.");
        ValidateFunding(state, ev.Id, ev.ExpectedRevision, ev.Investor, ev.Edge, context);
        return state.RecordMonetaryInfrastructureInvestment(
            ev.Investor, ev.Edge, ev.MoneyMicro).AdvanceRevision();
    }

    internal void ValidateProductiveFunding(WorldState state, EventId id,
        CivilizationId investor, FoundationProductiveTarget target, SimulationContext context)
    {
        if (id.OriginKind != EventOriginKind.Command || id.Turn != context.Turn ||
            state.WorldBinding != _binding || _productiveGeography is null ||
            !state.ProductiveInfrastructure.IsInitialized ||
            !state.Civilizations.Civilizations.Contains(investor) || !target.IsValid ||
            target.Node.FineGoldbergParameters != _productiveIncidence.Refinement.FineParameters ||
            target.Node.FineStrategicCellId.Value > _productiveIncidence.Refinement.FineParameters.StrategicCellCount ||
            _productiveGeography.SurfaceAtIndex(checked((int)target.Node.FineStrategicCellId.Value - 1)) != FoundationFineSurfaceKind.Land ||
            !_productiveGeography.CanTraverseOnFoot(checked((int)target.Node.FineStrategicCellId.Value - 1)))
            throw new InvalidOperationException("Productive funding requires signed traversable land and real investor.");
    }

    internal void ValidateFunding(WorldState state, EventId id, ulong expectedRevision,
        CivilizationId investor, PhysicalTacticalEdgeId edge, SimulationContext context)
    {
        if (id.Turn != context.Turn ||
            state.Revision != expectedRevision ||
            state.WorldBinding != _binding ||
            !state.Treasury.IsInitialized ||
            !state.Transport.HasMonetaryConstructionCosts ||
            !_actualEdges.Contains(edge) ||
            _blockedIceSeaEdges.Contains(edge) ||
            !state.Civilizations.Civilizations.Contains(investor))
            throw new InvalidOperationException(
                "Stale, unfinanced or foreign infrastructure investment.");
    }
}

/// <summary>
/// Accepted infrastructure investments are resolved only at the collective
/// all-ready turn boundary. Work is monetary and independent of worker
/// locations, occupations and physical commodity production.
/// </summary>
public sealed class FoundationMonetaryInfrastructureTurnPhase
    : ITurnResolutionPhase
{
    public const string StablePhaseId =
        "foundation.real-civilization-monetary-infrastructure-v14";
    private readonly FoundationMonetaryInfrastructureEventExecutor _executor;
    public string PhaseId => StablePhaseId;

    public FoundationMonetaryInfrastructureTurnPhase(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence)
    {
        _executor = new FoundationMonetaryInfrastructureEventExecutor(
            world, incidence);
    }

    public FoundationMonetaryInfrastructureTurnPhase(
        WorldGenerationResult world, PhysicalTacticalIncidenceMap incidence,
        FoundationFineFirstPhysicalGeography fine,
        FoundationFineFirstWorldgenIdentity signature)
    {
        _executor = new FoundationMonetaryInfrastructureEventExecutor(
            world, incidence, fine, signature);
    }

    public TurnResolutionResult Resolve(
        TurnResolutionInput input, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(stagedState);
        var commands = input.Commands
            .OfType<FoundationMonetaryInfrastructureCommand>()
            .OrderBy(c => c.Id.Sequence)
            .ToArray();
        if (commands.Length == 0)
            return new TurnResolutionResult(stagedState,
                Array.Empty<ISimulationEvent>(),
                new SimulationEventLog(Array.Empty<SimulationEventLogEntry>()));
        if (commands.Any(c => c.Id.Turn != input.Context.Turn) ||
            commands.Select(c => c.Id).Distinct().Count() != commands.Length)
            throw new InvalidOperationException(
                "Investment commands must be unique and belong to this turn.");

        var state = stagedState;
        var events = new List<ISimulationEvent>(commands.Length);
        var entries = new List<SimulationEventLogEntry>(commands.Length);
        foreach (var cmd in commands)
        {
            var ev = new FoundationMonetaryInfrastructureInvestedEvent(
                new EventId(cmd.Id, 1UL), cmd.Issuer, cmd.Edge,
                cmd.MoneyMicro, state.Revision);
            state = _executor.Execute(state, ev, input.Context);
            events.Add(ev);
            entries.Add(new SimulationEventLogEntry(
                checked((ulong)entries.Count + 1UL), ev,
                wasEligible: true, wasExecuted: true));
        }
        return new TurnResolutionResult(state, events,
            new SimulationEventLog(entries));
    }
}

/// <summary>
/// No worker-derived construction. The separate existing worker movement
/// executor handles worker events and this executor handles only cash spending.
/// </summary>
public sealed class FoundationMonetaryCollectiveReplayExecutor
    : ISimulationEventExecutor
{
    private readonly PhysicalTacticalWorkerMoveEventExecutor _workers;
    private readonly FoundationMonetaryInfrastructureEventExecutor _money;
    private readonly FoundationEconomicSalesSettlementEventExecutor? _sales;

    public FoundationMonetaryCollectiveReplayExecutor(
        PhysicalTacticalWorkerMoveEventExecutor workers,
        FoundationMonetaryInfrastructureEventExecutor money,
        FoundationEconomicSalesSettlementEventExecutor? sales = null)
    {
        _workers = workers ?? throw new ArgumentNullException(nameof(workers));
        _money = money ?? throw new ArgumentNullException(nameof(money));
        _sales = sales;
    }

    public WorldState Execute(WorldState state,
        ISimulationEvent ev, SimulationContext context) => ev switch
    {
        PhysicalTacticalWorkerMovedEvent => _workers.Execute(state, ev, context),
        FoundationMonetaryInfrastructureInvestedEvent =>
            _money.Execute(state, ev, context),
        FoundationEconomicSalesSettledEvent when _sales is not null =>
            _sales.Execute(state, ev, context),
        _ => throw new InvalidOperationException(
            "Unrecognized monetary/worker journal event.")
    };
}
