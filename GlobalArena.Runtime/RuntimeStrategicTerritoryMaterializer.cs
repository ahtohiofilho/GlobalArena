namespace GlobalArena.Runtime;

public static class RuntimeStrategicTerritoryMaterializer
{
    public static StrategicTerritoryRuntimeState MaterializeInitial(
        CivilizationRuntimeState civilizations)
    {
        ArgumentNullException.ThrowIfNull(
            civilizations);

        if (civilizations.Records.Count == 0)
        {
            return StrategicTerritoryRuntimeState.Empty;
        }

        var controls =
            new StrategicTerritoryControlEntry[
                civilizations.Records.Count];

        for (var index = 0;
             index < civilizations.Records.Count;
             index++)
        {
            var civilization =
                civilizations.Records[index];

            if (civilization.StartCellId is not GlobalArena.World.StrategicCellId startCellId)
            {
                throw new InvalidOperationException(
                    $"Civilization {civilization.Id.Value} must be materialized before initial territorial control can be created.");
            }

            controls[index] =
                new StrategicTerritoryControlEntry(
                    startCellId,
                    civilization.Id);
        }

        return new StrategicTerritoryRuntimeState(
            controls);
    }
}
