using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed class MilitaryMoveCommandProcessor
    : ISimulationCommandProcessor
{
    private readonly StrategicTopology _strategicTopology;

    private readonly RuntimeWorldBinding _expectedWorldBinding;

    private readonly MilitaryMoveCommandValidator _validator;

    public MilitaryMoveCommandProcessor(
        WorldGenerationResult generatedWorld)
    {
        ArgumentNullException.ThrowIfNull(
            generatedWorld);

        _strategicTopology =
            generatedWorld.StrategicTopology;

        _expectedWorldBinding =
            RuntimeWorldBinding.FromGeneratedWorld(
                generatedWorld);

        _validator =
            new MilitaryMoveCommandValidator(
                generatedWorld);
    }

    public IReadOnlyList<ISimulationEvent> Process(
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
            return Array.Empty<ISimulationEvent>();
        }

        if (!_validator.IsValid(
            worldState,
            militaryMove,
            context))
        {
            throw new InvalidOperationException(
                "Military move command must satisfy planning validation before event creation.");
        }

        if (worldState.WorldBinding
            != _expectedWorldBinding)
        {
            throw new InvalidOperationException(
                "Military move command processor requires the expected generated-world binding.");
        }

        var unit =
            worldState
                .Warfare
                .Units
                .Single(
                    candidate =>
                        candidate.Id
                        == militaryMove.MilitaryUnitId);

        var source =
            unit.StrategicCellId;

        var destination =
            militaryMove.DestinationStrategicCellId;

        var traversedEdge =
            FindTraversedEdge(
                source,
                destination);

        return Array.AsReadOnly(
            new ISimulationEvent[]
            {
                new MilitaryMoveEvent(
                    new EventId(
                        militaryMove.Id,
                        1UL),
                    militaryMove.IssuingCivilizationId,
                    militaryMove.MilitaryUnitId,
                    source,
                    destination,
                    traversedEdge)
            });
    }

    private StrategicEdgeId FindTraversedEdge(
        StrategicCellId source,
        StrategicCellId destination)
    {
        var sourceIndex =
            checked(
                (int)source.Value
                - 1);

        var sourceCell =
            _strategicTopology
                .Cells[
                    sourceIndex];

        foreach (var edgeId in sourceCell.IncidentEdgeIds)
        {
            var edgeIndex =
                checked(
                    (int)edgeId.Value
                    - 1);

            var edge =
                _strategicTopology
                    .Edges[
                        edgeIndex];

            if (edge.IncidentCellIds.Contains(
                destination))
            {
                return edge.Id;
            }
        }

        throw new InvalidOperationException(
            "Validated military movement does not resolve to one authoritative strategic edge.");
    }
}