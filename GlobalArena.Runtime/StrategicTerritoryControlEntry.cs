using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed class StrategicTerritoryControlEntry
{
    public StrategicCellId StrategicCellId { get; }

    public CivilizationId Controller { get; }

    public StrategicTerritoryControlEntry(
        StrategicCellId strategicCellId,
        CivilizationId controller)
    {
        if (!strategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Territorial control requires a valid strategic cell identity.",
                nameof(strategicCellId));
        }

        if (!controller.IsValid)
        {
            throw new ArgumentException(
                "Territorial control requires a valid civilization controller.",
                nameof(controller));
        }

        StrategicCellId = strategicCellId;
        Controller = controller;
    }
}
