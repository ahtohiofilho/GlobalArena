using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class MilitaryMoveEventRevalidator
    : ISimulationEventRevalidator
{
    private readonly StrategicTopology _strategicTopology;

    private readonly StrategicSurfaceGraph _surfaceGraph;

    private readonly RuntimeWorldBinding _expectedWorldBinding;

    public MilitaryMoveEventRevalidator(
        WorldGenerationResult generatedWorld)
    {
        ArgumentNullException.ThrowIfNull(
            generatedWorld);

        _strategicTopology =
            generatedWorld.StrategicTopology;

        _surfaceGraph =
            generatedWorld.StrategicSurfaceGraph;

        _expectedWorldBinding =
            RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld);
    }

    public bool CanExecute(
        WorldState worldState,
        ISimulationEvent simulationEvent,
        SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(
            worldState);

        ArgumentNullException.ThrowIfNull(
            simulationEvent);

        if (simulationEvent is not MilitaryMoveEvent militaryMove)
        {
            return false;
        }

        if (militaryMove.Id.OriginCommandId.Turn
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
            militaryMove.ExpectedSourceStrategicCellId)
            || !_expectedWorldBinding.Contains(
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

        if (unit.StrategicCellId
            != militaryMove.ExpectedSourceStrategicCellId)
        {
            return false;
        }

        var edgeOrdinal =
            militaryMove
                .TraversedStrategicEdgeId
                .Value;

        if (edgeOrdinal
            > (ulong)_strategicTopology.Edges.Count)
        {
            return false;
        }

        var edge =
            _strategicTopology
                .Edges[
                    checked(
                        (int)edgeOrdinal
                        - 1)];

        if (!edge.IncidentCellIds.Contains(
            militaryMove.ExpectedSourceStrategicCellId)
            || !edge.IncidentCellIds.Contains(
                militaryMove.DestinationStrategicCellId))
        {
            return false;
        }

        var sourceIndex =
            _surfaceGraph.GetNodeIndex(
                militaryMove.ExpectedSourceStrategicCellId);

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