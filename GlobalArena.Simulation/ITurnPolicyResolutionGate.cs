namespace GlobalArena.Simulation;

public interface ITurnPolicyResolutionGate
{
    TurnPolicyResolutionGateResult Resolve(
        TurnPolicyResolutionGateInput input);
}
