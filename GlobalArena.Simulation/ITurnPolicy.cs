namespace GlobalArena.Simulation;

public interface ITurnPolicy
{
    bool ShouldClose(
        TurnPolicyInput input);
}
