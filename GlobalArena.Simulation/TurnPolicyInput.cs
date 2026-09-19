using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class TurnPolicyInput
{
    public TurnNumber Turn { get; }

    public bool AllRequiredParticipantsReady { get; }

    public bool ExternalDeadlineReached { get; }

    public TurnPolicyInput(
        TurnNumber turn,
        bool allRequiredParticipantsReady,
        bool externalDeadlineReached)
    {
        Turn = turn;
        AllRequiredParticipantsReady =
            allRequiredParticipantsReady;
        ExternalDeadlineReached =
            externalDeadlineReached;
    }
}
