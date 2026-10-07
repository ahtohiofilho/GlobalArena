using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class MilitaryMoveCommandValidator
    : ISimulationCommandValidator
{
    private readonly StrategicSurfaceGraph _surfaceGraph;

    private readonly RuntimeWorldBinding _expectedWorldBinding;

    public MilitaryMoveCommandValidator(
        WorldGenerationResult generatedWorld)
    {
        ArgumentNullException.ThrowIfNull(
            generatedWorld);

        _surfaceGraph =
            generatedWorld.StrategicSurfaceGraph;

        _expectedWorldBinding =
            RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld);
    }

    public bool IsValid(
        WorldState worldState,
        ISimulationCommand command,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(
            worldState);

        ArgumentNullException.ThrowIfNull(
            command);

        if (command is not MilitaryMoveCommand militaryMove)
        {
            return false;
        }

        if (militaryMove.Id.Turn
            != context.Turn)
        {
            return false;
        }

        if (worldState.WorldBinding is null
            || worldState.WorldBinding
                != _expectedWorldBinding)
        {
            return false;
        }

        if (!_expectedWorldBinding.Contains(
            militaryMove.DestinationStrategicCellId))
        {
            return false;
        }

        MilitaryUnitRuntimeState? unit = null;

        foreach (var candidate in worldState.Warfare.Units)
        {
            if (candidate.Id
                == militaryMove.MilitaryUnitId)
            {
                unit = candidate;
                break;
            }
        }

        if (unit is null)
        {
            return false;
        }

        if (unit.Owner
            != militaryMove.IssuingCivilizationId)
        {
            return false;
        }

        if (!_expectedWorldBinding.Contains(
            unit.StrategicCellId))
        {
            return false;
        }

        var sourceIndex =
            _surfaceGraph.GetNodeIndex(
                unit.StrategicCellId);

        var destinationIndex =
            _surfaceGraph.GetNodeIndex(
                militaryMove.DestinationStrategicCellId);

        return _surfaceGraph
            .GetNeighborIndexes(
                sourceIndex)
            .Contains(
                destinationIndex);
    }
}