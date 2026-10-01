using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed class MilitaryUnitRuntimeState
{
    public MilitaryUnitId Id { get; }

    public CivilizationId Owner { get; }

    public StrategicCellId StrategicCellId { get; }

    public MilitaryUnitRuntimeState(
        MilitaryUnitId id,
        CivilizationId owner,
        StrategicCellId strategicCellId)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Military unit ID must be valid.",
                nameof(id));
        }

        if (!owner.IsValid)
        {
            throw new ArgumentException(
                "Military unit owner must be a valid civilization identity.",
                nameof(owner));
        }

        if (!strategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Military unit strategic cell must be valid.",
                nameof(strategicCellId));
        }

        Id = id;
        Owner = owner;
        StrategicCellId = strategicCellId;
    }
}
