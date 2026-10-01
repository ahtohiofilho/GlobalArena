using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TurnPolicyResolutionGateTests
{
    [Fact]
    public void NullPolicyIsRejected()
    {
        var pipeline =
            new TrackingPipeline();

        Assert.Throws<ArgumentNullException>(
            () => new TurnPolicyResolutionGate(
                null!,
                CreateResolver(
                    pipeline)));
    }

    [Fact]
    public void NullResolverIsRejected()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnPolicyResolutionGate(
                new TrackingTurnPolicy(
                    shouldClose: false),
                null!));
    }

    [Fact]
    public void NullInputIsRejected()
    {
        var gate =
            new TurnPolicyResolutionGate(
                new TrackingTurnPolicy(
                    shouldClose: false),
                CreateResolver(
                    new TrackingPipeline()));

        Assert.Throws<ArgumentNullException>(
            () => gate.Resolve(
                null!));
    }

    [Fact]
    public void OpenPolicyReturnsOpenWithoutInvokingResolverPipeline()
    {
        var turn =
            new TurnNumber(21UL);

        var pipeline =
            new TrackingPipeline();

        var policy =
            new TrackingTurnPolicy(
                shouldClose: false);

        var gate =
            new TurnPolicyResolutionGate(
                policy,
                CreateResolver(
                    pipeline));

        var input =
            CreateGateInput(
                turn,
                allRequiredParticipantsReady: false,
                externalDeadlineReached: false);

        var result =
            gate.Resolve(
                input);

        Assert.Equal(
            1,
            policy.CallCount);

        Assert.Same(
            input.PolicyInput,
            policy.Input);

        Assert.False(
            result.WasResolved);

        Assert.Null(
            result.ResolutionResult);

        Assert.Equal(
            0,
            pipeline.ValidatorCallCount);

        Assert.Equal(
            0,
            pipeline.ProcessorCallCount);

        Assert.Equal(
            0,
            pipeline.OrdererCallCount);

        Assert.Equal(
            0,
            pipeline.RevalidatorCallCount);

        Assert.Equal(
            0,
            pipeline.ExecutorCallCount);
    }

    [Fact]
    public void ClosedPolicyResolvesThroughExistingTurnResolver()
    {
        var turn =
            new TurnNumber(22UL);

        var pipeline =
            new TrackingPipeline();

        var policy =
            new TrackingTurnPolicy(
                shouldClose: true);

        var gate =
            new TurnPolicyResolutionGate(
                policy,
                CreateResolver(
                    pipeline));

        var input =
            CreateGateInput(
                turn,
                allRequiredParticipantsReady: true,
                externalDeadlineReached: false);

        var result =
            gate.Resolve(
                input);

        Assert.Equal(
            1,
            policy.CallCount);

        Assert.True(
            result.WasResolved);

        var resolutionResult =
            Assert.IsType<TurnResolutionResult>(
                result.ResolutionResult);

        Assert.Equal(
            1UL,
            resolutionResult.ResultingWorldState.Revision);

        Assert.Single(
            resolutionResult.Events);

        var logEntry =
            Assert.Single(
                resolutionResult.EventLog.Entries);

        Assert.True(
            logEntry.WasEligible);

        Assert.True(
            logEntry.WasExecuted);

        Assert.Equal(
            1,
            pipeline.ValidatorCallCount);

        Assert.Equal(
            1,
            pipeline.ProcessorCallCount);

        Assert.Equal(
            1,
            pipeline.OrdererCallCount);

        Assert.Equal(
            1,
            pipeline.RevalidatorCallCount);

        Assert.Equal(
            1,
            pipeline.ExecutorCallCount);
    }

    [Fact]
    public void ConcretePoliciesCanShareSameTurnResolver()
    {
        var pipeline =
            new TrackingPipeline();

        var resolver =
            CreateResolver(
                pipeline);

        var manualGate =
            new TurnPolicyResolutionGate(
                new ManualReadyTurnPolicy(),
                resolver);

        var deadlineGate =
            new TurnPolicyResolutionGate(
                new ExternalDeadlineTurnPolicy(),
                resolver);

        var manualResult =
            manualGate.Resolve(
                CreateGateInput(
                    new TurnNumber(23UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false));

        var deadlineResult =
            deadlineGate.Resolve(
                CreateGateInput(
                    new TurnNumber(24UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true));

        Assert.True(
            manualResult.WasResolved);

        Assert.True(
            deadlineResult.WasResolved);

        Assert.Equal(
            2,
            pipeline.ValidatorCallCount);

        Assert.Equal(
            2,
            pipeline.ProcessorCallCount);

        Assert.Equal(
            2,
            pipeline.OrdererCallCount);

        Assert.Equal(
            2,
            pipeline.RevalidatorCallCount);

        Assert.Equal(
            2,
            pipeline.ExecutorCallCount);

        Assert.Equal(
            1UL,
            manualResult.ResolutionResult!
                .ResultingWorldState.Revision);

        Assert.Equal(
            1UL,
            deadlineResult.ResolutionResult!
                .ResultingWorldState.Revision);
    }

    private static TurnPolicyResolutionGateInput CreateGateInput(
        TurnNumber turn,
        bool allRequiredParticipantsReady,
        bool externalDeadlineReached)
    {
        var command =
            new TestCommand(
                new CommandId(
                    turn,
                    1UL));

        return new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(
                turn,
                allRequiredParticipantsReady,
                externalDeadlineReached),
            new TurnResolutionInput(
                WorldState.CreateInitial(),
                new ISimulationCommand[]
                {
                    command
                },
                new SimulationContext(
                    turn,
                    new SimulationSeed(123UL))));
    }

    private static TurnResolver CreateResolver(
        TrackingPipeline pipeline)
    {
        return new TurnResolver(
            pipeline,
            pipeline,
            pipeline,
            pipeline,
            pipeline);
    }

    private sealed record TestCommand(
        CommandId Id) : ISimulationCommand;

    private sealed record TestEvent(
        EventId Id) : ISimulationEvent;

    private sealed class TrackingTurnPolicy
        : ITurnPolicy
    {
        private readonly bool _shouldClose;

        public TrackingTurnPolicy(
            bool shouldClose)
        {
            _shouldClose = shouldClose;
        }

        public int CallCount { get; private set; }

        public TurnPolicyInput? Input { get; private set; }

        public bool ShouldClose(
            TurnPolicyInput input)
        {
            CallCount++;
            Input = input;

            return _shouldClose;
        }
    }

    private sealed class TrackingPipeline
        : ISimulationCommandValidator,
          ISimulationCommandProcessor,
          ISimulationEventOrderer,
          ISimulationEventRevalidator,
          ISimulationEventExecutor
    {
        public int ValidatorCallCount { get; private set; }

        public int ProcessorCallCount { get; private set; }

        public int OrdererCallCount { get; private set; }

        public int RevalidatorCallCount { get; private set; }

        public int ExecutorCallCount { get; private set; }

        public bool IsValid(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            ValidatorCallCount++;

            return true;
        }

        public IReadOnlyList<ISimulationEvent> Process(
            WorldState worldState,
            ISimulationCommand command,
            SimulationContext context)
        {
            ProcessorCallCount++;

            return new ISimulationEvent[]
            {
                new TestEvent(
                    new EventId(
                        command.Id,
                        1UL))
            };
        }

        public IReadOnlyList<ISimulationEvent> Order(
            IReadOnlyList<ISimulationEvent> events,
            SimulationContext context)
        {
            OrdererCallCount++;

            return events;
        }

        public bool CanExecute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            RevalidatorCallCount++;

            return true;
        }

        public WorldState Execute(
            WorldState worldState,
            ISimulationEvent simulationEvent,
            SimulationContext context)
        {
            ExecutorCallCount++;

            return worldState.AdvanceRevision();
        }
    }
}
