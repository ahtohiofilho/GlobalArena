namespace GlobalArena.Simulation;

public sealed class TurnPolicyResolutionGateResult
{
    public bool WasResolved { get; }

    public TurnResolutionResult? ResolutionResult { get; }

    private TurnPolicyResolutionGateResult(
        bool wasResolved,
        TurnResolutionResult? resolutionResult)
    {
        WasResolved = wasResolved;
        ResolutionResult = resolutionResult;
    }

    public static TurnPolicyResolutionGateResult Open()
    {
        return new TurnPolicyResolutionGateResult(
            wasResolved: false,
            resolutionResult: null);
    }

    public static TurnPolicyResolutionGateResult Resolved(
        TurnResolutionResult resolutionResult)
    {
        ArgumentNullException.ThrowIfNull(resolutionResult);

        return new TurnPolicyResolutionGateResult(
            wasResolved: true,
            resolutionResult);
    }
}
