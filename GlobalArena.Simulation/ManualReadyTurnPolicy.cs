namespace GlobalArena.Simulation;

public sealed class ManualReadyTurnPolicy
    : ITurnPolicy
{
    public bool ShouldClose(
        TurnPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input.AllRequiredParticipantsReady;
    }
}
