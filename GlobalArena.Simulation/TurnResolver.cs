using GlobalArena.Kernel;

namespace GlobalArena.Simulation;

public sealed class TurnResolver
{
    public TurnResolutionResult Resolve(
        TurnResolutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Commands.Count != 0)
        {
            throw new NotSupportedException(
                "Command resolution is not implemented yet.");
        }

        return new TurnResolutionResult(
            input.WorldState,
            Array.Empty<ISimulationEvent>());
    }
}
