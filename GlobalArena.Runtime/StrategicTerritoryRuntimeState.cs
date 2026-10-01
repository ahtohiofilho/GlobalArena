using GlobalArena.World;

namespace GlobalArena.Runtime;

public sealed class StrategicTerritoryRuntimeState
{
    private static readonly StrategicTerritoryRuntimeState EmptyInstance =
        new(
            Array.Empty<StrategicTerritoryControlEntry>());

    private readonly StrategicTerritoryControlEntry[] _controls;

    private readonly IReadOnlyList<StrategicTerritoryControlEntry> _readOnlyControls;

    private readonly Dictionary<StrategicCellId, CivilizationId> _controllersByCell;

    public static StrategicTerritoryRuntimeState Empty =>
        EmptyInstance;

    public IReadOnlyList<StrategicTerritoryControlEntry> Controls =>
        _readOnlyControls;

    public StrategicTerritoryRuntimeState(
        IEnumerable<StrategicTerritoryControlEntry> controls)
    {
        ArgumentNullException.ThrowIfNull(
            controls);

        var canonical =
            controls.ToArray();

        if (canonical.Any(
            control =>
                control is null))
        {
            throw new ArgumentException(
                "Strategic territorial control cannot contain null entries.",
                nameof(controls));
        }

        canonical =
            canonical
                .OrderBy(
                    control =>
                        control.StrategicCellId.Value)
                .ToArray();

        for (var index = 1;
             index < canonical.Length;
             index++)
        {
            if (canonical[index - 1].StrategicCellId
                == canonical[index].StrategicCellId)
            {
                throw new ArgumentException(
                    "A strategic cell cannot have more than one controlling civilization.",
                    nameof(controls));
            }
        }

        _controls =
            canonical;

        _readOnlyControls =
            Array.AsReadOnly(
                _controls);

        _controllersByCell =
            canonical.ToDictionary(
                control =>
                    control.StrategicCellId,
                control =>
                    control.Controller);
    }

    public bool TryGetController(
        StrategicCellId strategicCellId,
        out CivilizationId controller)
    {
        if (!strategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Territorial lookup requires a valid strategic cell identity.",
                nameof(strategicCellId));
        }

        return _controllersByCell.TryGetValue(
            strategicCellId,
            out controller);
    }
}
