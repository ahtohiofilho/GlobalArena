namespace GlobalArena.Simulation;

public sealed class TurnPolicyResolutionGate
    : ITurnPolicyResolutionGate
{
    private readonly ITurnPolicy _turnPolicy;
    private readonly TurnResolver _turnResolver;

    public TurnPolicyResolutionGate(
        ITurnPolicy turnPolicy,
        TurnResolver turnResolver)
    {
        ArgumentNullException.ThrowIfNull(turnPolicy);
        ArgumentNullException.ThrowIfNull(turnResolver);

        _turnPolicy = turnPolicy;
        _turnResolver = turnResolver;
    }

    public TurnPolicyResolutionGateResult Resolve(
        TurnPolicyResolutionGateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!_turnPolicy.ShouldClose(
            input.PolicyInput))
        {
            return TurnPolicyResolutionGateResult.Open();
        }

        var resolutionResult =
            _turnResolver.Resolve(
                input.ResolutionInput);

        return TurnPolicyResolutionGateResult.Resolved(
            resolutionResult);
    }
}
