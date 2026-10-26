namespace GlobalArena.Simulation;

/// <summary>
/// Exhaustive is the existing canonical reference behavior.
/// DependencyIndex re-evaluates only candidates that share the changed
/// supply or destination demand balance. Both use identical freight quotes,
/// marginal commodity value functions and settlement arithmetic.
/// </summary>
public enum EconomicTurnFreightCandidateSelectionMode : byte
{
    Exhaustive = 0,
    DependencyIndex = 1
}
