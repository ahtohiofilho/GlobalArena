using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TurnPolicyDeterminismTests
{
    [Fact]
    public void ManualReadyOpenDecisionIsDeterministicAcrossEquivalentInputs()
    {
        var resolver =
            CreateResolver();

        var gate =
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                resolver);

        var first =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(31UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true,
                    seed: 987654321UL));

        var second =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(31UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true,
                    seed: 987654321UL));

        AssertEquivalent(
            first,
            second);

        Assert.False(
            first.WasResolved);
    }

    [Fact]
    public void ManualReadyClosedResolutionIsDeterministicAcrossEquivalentInputs()
    {
        var resolver =
            CreateResolver();

        var gate =
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                resolver);

        var first =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(32UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false,
                    seed: 987654321UL));

        var second =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(32UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false,
                    seed: 987654321UL));

        AssertEquivalent(
            first,
            second);

        Assert.True(
            first.WasResolved);

        Assert.Equal(
            3UL,
            first.ResolutionResult!
                .ResultingWorldState.Revision);
    }

    [Fact]
    public void ExternalDeadlineOpenDecisionIsDeterministicAcrossEquivalentInputs()
    {
        var resolver =
            CreateResolver();

        var gate =
            new TurnPolicyResolutionGate(
                new ExternalDeadlineTurnPolicy(),
                resolver);

        var first =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(33UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false,
                    seed: 987654321UL));

        var second =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(33UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false,
                    seed: 987654321UL));

        AssertEquivalent(
            first,
            second);

        Assert.False(
            first.WasResolved);
    }

    [Fact]
    public void ExternalDeadlineClosedResolutionIsDeterministicAcrossEquivalentInputs()
    {
        var resolver =
            CreateResolver();

        var gate =
            new TurnPolicyResolutionGate(
                new ExternalDeadlineTurnPolicy(),
                resolver);

        var first =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(34UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true,
                    seed: 987654321UL));

        var second =
            gate.Resolve(
                CreateInput(
                    new TurnNumber(34UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true,
                    seed: 987654321UL));

        AssertEquivalent(
            first,
            second);

        Assert.True(
            first.WasResolved);

        Assert.Equal(
            3UL,
            first.ResolutionResult!
                .ResultingWorldState.Revision);
    }

    private static TurnResolver CreateResolver()
    {
        return new TurnResolver(
            new AlwaysValidCommandValidator(),
            new OneEventCommandProcessor(),
            new SeededSimulationEventOrderer(),
            new AlwaysEligibleEventRevalidator(),
            new RevisionAdvancingEventExecutor());
    }

    private static TurnPolicyResolutionGateInput CreateInput(
        TurnNumber turn,
        bool allRequiredParticipantsReady,
        bool externalDeadlineReached,
        ulong seed)
    {
        var commands =
            new ISimulationCommand[]
            {
                new TestCommand(
                    new CommandId(
                        turn,
                        3UL)),
                new TestCommand(
                    new CommandId(
                        turn,
                        1UL)),
                new TestCommand(
                    new CommandId(
                        turn,
                        2UL))
            };

        return new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(
                turn,
                allRequiredParticipantsReady,
                externalDeadlineReached),
            new TurnResolutionInput(
                WorldState.CreateInitial(),
                commands,
                new SimulationContext(
                    turn,
                    new SimulationSeed(
                        seed))));
    }

    private static void AssertEquivalent(
        TurnPolicyResolutionGateResult first,
        TurnPolicyResolutionGateResult second)
    {
        Assert.Equal(
            first.WasResolved,
            second.WasResolved);

        if (!first.WasResolved)
        {
            Assert.Null(
                first.ResolutionResult);

            Assert.Null(
                second.ResolutionResult);

            return;
        }

        var firstResolution =
            Assert.IsType<TurnResolutionResult>(
                first.ResolutionResult);

        var secondResolution =
            Assert.IsType<TurnResolutionResult>(
                second.ResolutionResult);

        Assert.Equal(
            firstResolution.ResultingWorldState.Revision,
            secondResolution.ResultingWorldState.Revision);

        Assert.Equal(
            firstResolution.Events.Count,
            secondResolution.Events.Count);

        for (var index = 0;
             index < firstResolution.Events.Count;
             index++)
        {
            Assert.Equal(
                firstResolution.Events[index].Id,
                secondResolution.Events[index].Id);
        }

        Assert.Equal(
            firstResolution.EventLog.Entries.Count,
            secondResolution.EventLog.Entries.Count);

        for (var index = 0;
             index < firstResolution.EventLog.Entries.Count;
             index++)
        {
            var firstEntry =
                firstResolution.EventLog.Entries[index];

            var secondEntry =
                secondResolution.EventLog.Entries[index];

            Assert.Equal(
                firstEntry.ResolutionSequence,
                secondEntry.ResolutionSequence);

            Assert.Equal(
                firstEntry.EventId,
                secondEntry.EventId);

            Assert.Equal(
                firstEntry.WasEligible,
                secondEntry.WasEligible);

            Assert.Equal(
                firstEntry.WasExecuted,
                secondEntry.WasExecuted);
        }
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class AlwaysValidCommandValidator
        : ISimulationCommandValidator
    {
        public bool IsValid(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            return true;
        }
    }

    private sealed class OneEventCommandProcessor
        : ISimulationCommandProcessor
    {
        public IReadOnlyList<ISimulationEvent> Process(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            return new ISimulationEvent[]
            {
                new TestEvent(
                    new EventId(
                        command.Id,
                        1UL))
            };
        }
    }

    private sealed class AlwaysEligibleEventRevalidator
        : ISimulationEventRevalidator
    {
        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            return true;
        }
    }

    private sealed class RevisionAdvancingEventExecutor
        : ISimulationEventExecutor
    {
        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            return worldState.AdvanceRevision();
        }
    }
}
