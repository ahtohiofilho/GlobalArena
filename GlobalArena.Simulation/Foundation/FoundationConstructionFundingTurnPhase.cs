using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation.Foundation;

public sealed record FoundationConstructionFundedEvent(
    EventId Id, CivilizationId Investor, PhysicalTacticalEdgeId Edge,
    ulong MoneyMicro, ulong RequiredWorkMicro, ulong ExpectedRevision) : ISimulationEvent;

public sealed class FoundationConstructionFundingExecutor : ISimulationEventExecutor
{
    private readonly FoundationMonetaryInfrastructureEventExecutor _admission;
    private readonly ulong _moneyMicroPerWholeWork;

    public FoundationConstructionFundingExecutor(
        FoundationMonetaryInfrastructureEventExecutor admission, ulong moneyMicroPerWholeWork)
    {
        _admission = admission ?? throw new ArgumentNullException(nameof(admission));
        if (moneyMicroPerWholeWork == 0UL)
            throw new ArgumentOutOfRangeException(nameof(moneyMicroPerWholeWork));
        _moneyMicroPerWholeWork = moneyMicroPerWholeWork;
    }

    internal ulong Quote(ulong moneyMicro) =>
        FoundationConstructionWorkPolicy.RequiredWorkMicro(moneyMicro, _moneyMicroPerWholeWork);

    public WorldState Execute(WorldState state, ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (simulationEvent is not FoundationConstructionFundedEvent ev ||
            ev.Id.OriginKind != EventOriginKind.Command ||
            ev.RequiredWorkMicro != Quote(ev.MoneyMicro))
            throw new InvalidOperationException("Invalid construction funding event or work quote.");
        _admission.ValidateFunding(state, ev.Id, ev.ExpectedRevision, ev.Investor, ev.Edge, context);
        return state.FundConstruction(ev.Investor, ev.Edge, ev.MoneyMicro,
            ev.RequiredWorkMicro).AdvanceRevision();
    }
}

/// <summary>Explicit alternative to immediate monetary capital investment.
/// Not installed in the interactive host until the remaining construction gates.</summary>
public sealed class FoundationConstructionFundingTurnPhase : ITurnResolutionPhase
{
    private readonly FoundationConstructionFundingExecutor _executor;
    public string PhaseId => "foundation.construction-funding-v18";

    public FoundationConstructionFundingTurnPhase(FoundationConstructionFundingExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public TurnResolutionResult Resolve(TurnResolutionInput input, WorldState stagedState)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(stagedState);
        var commands = input.Commands.OfType<FoundationMonetaryInfrastructureCommand>()
            .OrderBy(c => c.Id.Sequence).ToArray();
        if (commands.Any(c => c.Id.Turn != input.Context.Turn) ||
            commands.Select(c => c.Id).Distinct().Count() != commands.Length)
            throw new InvalidOperationException("Construction commands must be unique and current.");
        var state = stagedState;
        var events = new List<ISimulationEvent>();
        var entries = new List<SimulationEventLogEntry>();
        foreach (var command in commands)
        {
            var ev = new FoundationConstructionFundedEvent(new EventId(command.Id, 1UL),
                command.Issuer, command.Edge, command.MoneyMicro,
                _executor.Quote(command.MoneyMicro), state.Revision);
            state = _executor.Execute(state, ev, input.Context);
            events.Add(ev);
            entries.Add(new SimulationEventLogEntry(checked((ulong)entries.Count + 1UL),
                ev, wasEligible: true, wasExecuted: true));
        }
        return new TurnResolutionResult(state, events, new SimulationEventLog(entries));
    }
}
