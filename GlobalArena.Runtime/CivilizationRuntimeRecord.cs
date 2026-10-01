using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed class CivilizationRuntimeRecord
{
    public CivilizationId Id { get; }

    public StrategicCellId? StartCellId { get; }

    public bool IsMaterialized =>
        StartCellId.HasValue;

    public CivilizationRuntimeRecord(
        CivilizationId id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Civilization runtime record requires a valid civilization identity.",
                nameof(id));
        }

        Id = id;
        StartCellId = null;
    }

    public CivilizationRuntimeRecord(
        CivilizationId id,
        StrategicCellId startCellId)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException(
                "Civilization runtime record requires a valid civilization identity.",
                nameof(id));
        }

        if (!startCellId.IsValid)
        {
            throw new ArgumentException(
                "Materialized civilization requires a valid strategic start cell.",
                nameof(startCellId));
        }

        Id = id;
        StartCellId = startCellId;
    }
}
