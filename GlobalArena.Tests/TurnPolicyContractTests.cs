using GlobalArena.Kernel;
using GlobalArena.Simulation;

namespace GlobalArena.Tests;

public sealed class TurnPolicyContractTests
{
    [Fact]
    public void InputCarriesTurnReadinessAndDeadlineSignal()
    {
        var turn =
            new TurnNumber(7UL);

        var input =
            new TurnPolicyInput(
                turn,
                allRequiredParticipantsReady: true,
                externalDeadlineReached: false);

        Assert.Equal(
            turn,
            input.Turn);

        Assert.True(
            input.AllRequiredParticipantsReady);

        Assert.False(
            input.ExternalDeadlineReached);
    }

    [Fact]
    public void PolicyContractReceivesInputAndReturnsDecision()
    {
        var input =
            new TurnPolicyInput(
                new TurnNumber(8UL),
                allRequiredParticipantsReady: false,
                externalDeadlineReached: false);

        var policy =
            new TestTurnPolicy(
                decision: false);

        var shouldClose =
            policy.ShouldClose(input);

        Assert.Same(
            input,
            policy.Input);

        Assert.False(
            shouldClose);
    }

    [Fact]
    public void ContractSupportsReadinessDrivenClosure()
    {
        var policy =
            new PredicateTurnPolicy(
                input =>
                    input.AllRequiredParticipantsReady);

        var openDecision =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(9UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: false));

        var closedDecision =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(9UL),
                    allRequiredParticipantsReady: true,
                    externalDeadlineReached: false));

        Assert.False(
            openDecision);

        Assert.True(
            closedDecision);
    }

    [Fact]
    public void ContractSupportsExternallySuppliedDeadlineClosure()
    {
        var policy =
            new PredicateTurnPolicy(
                input =>
                    input.ExternalDeadlineReached);

        var openDecision =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(10UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: false));

        var closedDecision =
            policy.ShouldClose(
                new TurnPolicyInput(
                    new TurnNumber(10UL),
                    allRequiredParticipantsReady: false,
                    externalDeadlineReached: true));

        Assert.False(
            openDecision);

        Assert.True(
            closedDecision);
    }

    private sealed class TestTurnPolicy
        : ITurnPolicy
    {
        private readonly bool _decision;

        public TestTurnPolicy(
            bool decision)
        {
            _decision = decision;
        }

        public TurnPolicyInput? Input { get; private set; }

        public bool ShouldClose(
            TurnPolicyInput input)
        {
            Input = input;

            return _decision;
        }
    }

    private sealed class PredicateTurnPolicy
        : ITurnPolicy
    {
        private readonly Func<TurnPolicyInput, bool> _predicate;

        public PredicateTurnPolicy(
            Func<TurnPolicyInput, bool> predicate)
        {
            _predicate = predicate;
        }

        public bool ShouldClose(
            TurnPolicyInput input)
        {
            return _predicate(input);
        }
    }
}
