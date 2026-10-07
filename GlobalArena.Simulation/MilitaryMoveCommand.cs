using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

public sealed record MilitaryMoveCommand
    : ISimulationCommand
{
    public CommandId Id { get; }

    public CivilizationId IssuingCivilizationId { get; }

    public MilitaryUnitId MilitaryUnitId { get; }

    public StrategicCellId DestinationStrategicCellId { get; }

    public MilitaryMoveCommand(
        CommandId id,
        CivilizationId issuingCivilizationId,
        MilitaryUnitId militaryUnitId,
        StrategicCellId destinationStrategicCellId)
    {
        if (id.Turn.Value == 0UL
            || id.Sequence == 0UL)
        {
            throw new ArgumentException(
                "Military move command ID must contain a valid turn and sequence.",
                nameof(id));
        }

        if (!issuingCivilizationId.IsValid)
        {
            throw new ArgumentException(
                "Military move command issuer must be a valid civilization identity.",
                nameof(issuingCivilizationId));
        }

        if (!militaryUnitId.IsValid)
        {
            throw new ArgumentException(
                "Military move command unit must be a valid military unit identity.",
                nameof(militaryUnitId));
        }

        if (!destinationStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Military move command destination must be a valid strategic cell identity.",
                nameof(destinationStrategicCellId));
        }

        Id = id;
        IssuingCivilizationId = issuingCivilizationId;
        MilitaryUnitId = militaryUnitId;
        DestinationStrategicCellId = destinationStrategicCellId;
    }
}