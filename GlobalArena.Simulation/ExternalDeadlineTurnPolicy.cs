namespace GlobalArena.Simulation;

public sealed class ExternalDeadlineTurnPolicy
    : ITurnPolicy
{
    public bool ShouldClose(
        TurnPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input.ExternalDeadlineReached;
    }
}
