namespace GlobalArena.Simulation;

public sealed class TurnPolicyResolutionGateInput
{
    public TurnPolicyInput PolicyInput { get; }

    public TurnResolutionInput ResolutionInput { get; }

    public TurnPolicyResolutionGateInput(
        TurnPolicyInput policyInput,
        TurnResolutionInput resolutionInput)
    {
        ArgumentNullException.ThrowIfNull(policyInput);
        ArgumentNullException.ThrowIfNull(resolutionInput);

        if (!policyInput.Turn.Equals(
            resolutionInput.Context.Turn))
        {
            throw new ArgumentException(
                "Policy and resolution inputs must target the same turn.",
                nameof(resolutionInput));
        }

        PolicyInput = policyInput;
        ResolutionInput = resolutionInput;
    }
}
