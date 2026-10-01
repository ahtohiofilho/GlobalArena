using GlobalArena.Runtime;
using GlobalArena.Kernel;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class TurnPolicyResolutionGateContractTests
{
    [Fact]
    public void InputCarriesPolicyAndResolutionInputs()
    {
        var turn =
            new TurnNumber(11UL);

        var policyInput =
            CreatePolicyInput(
                turn);

        var resolutionInput =
            CreateResolutionInput(
                turn);

        var input =
            new TurnPolicyResolutionGateInput(
                policyInput,
                resolutionInput);

        Assert.Same(
            policyInput,
            input.PolicyInput);

        Assert.Same(
            resolutionInput,
            input.ResolutionInput);
    }

    [Fact]
    public void InputRejectsNullPolicyInput()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnPolicyResolutionGateInput(
                null!,
                CreateResolutionInput(
                    new TurnNumber(12UL))));
    }

    [Fact]
    public void InputRejectsNullResolutionInput()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TurnPolicyResolutionGateInput(
                CreatePolicyInput(
                    new TurnNumber(13UL)),
                null!));
    }

    [Fact]
    public void InputRejectsDifferentTurns()
    {
        Assert.Throws<ArgumentException>(
            () => new TurnPolicyResolutionGateInput(
                CreatePolicyInput(
                    new TurnNumber(14UL)),
                CreateResolutionInput(
                    new TurnNumber(15UL))));
    }

    [Fact]
    public void OpenResultExplicitlyRepresentsNoResolution()
    {
        var result =
            TurnPolicyResolutionGateResult.Open();

        Assert.False(
            result.WasResolved);

        Assert.Null(
            result.ResolutionResult);
    }

    [Fact]
    public void ResolvedResultCarriesResolutionResult()
    {
        var resolutionResult =
            CreateResolutionResult();

        var result =
            TurnPolicyResolutionGateResult.Resolved(
                resolutionResult);

        Assert.True(
            result.WasResolved);

        Assert.Same(
            resolutionResult,
            result.ResolutionResult);
    }

    [Fact]
    public void ResolvedResultRejectsNullResolutionResult()
    {
        Assert.Throws<ArgumentNullException>(
            () => TurnPolicyResolutionGateResult.Resolved(
                null!));
    }

    [Fact]
    public void GateContractReceivesInputAndReturnsResult()
    {
        var turn =
            new TurnNumber(16UL);

        var input =
            new TurnPolicyResolutionGateInput(
                CreatePolicyInput(
                    turn),
                CreateResolutionInput(
                    turn));

        var expected =
            TurnPolicyResolutionGateResult.Open();

        var gate =
            new TestTurnPolicyResolutionGate(
                expected);

        var actual =
            gate.Resolve(
                input);

        Assert.Same(
            input,
            gate.Input);

        Assert.Same(
            expected,
            actual);
    }

    private static TurnPolicyInput CreatePolicyInput(
        TurnNumber turn)
    {
        return new TurnPolicyInput(
            turn,
            allRequiredParticipantsReady: false,
            externalDeadlineReached: false);
    }

    private static TurnResolutionInput CreateResolutionInput(
        TurnNumber turn)
    {
        return new TurnResolutionInput(
            WorldState.CreateInitial(),
            Array.Empty<ISimulationCommand>(),
            new SimulationContext(
                turn,
                new SimulationSeed(123UL)));
    }

    private static TurnResolutionResult CreateResolutionResult()
    {
        return new TurnResolutionResult(
            WorldState.CreateInitial(),
            Array.Empty<ISimulationEvent>(),
            new SimulationEventLog(
                Array.Empty<SimulationEventLogEntry>()));
    }

    private sealed class TestTurnPolicyResolutionGate
        : ITurnPolicyResolutionGate
    {
        private readonly TurnPolicyResolutionGateResult _result;

        public TestTurnPolicyResolutionGate(
            TurnPolicyResolutionGateResult result)
        {
            _result = result;
        }

        public TurnPolicyResolutionGateInput? Input { get; private set; }

        public TurnPolicyResolutionGateResult Resolve(
            TurnPolicyResolutionGateInput input)
        {
            Input = input;

            return _result;
        }
    }
}
