namespace GlobalArena.World;

public static class MinimalTacticalRegionGraphGenerator
{
    public static TacticalRegion Generate(
        StrategicCellId parentStrategicCellId)
    {
        if (!parentStrategicCellId.IsValid)
        {
            throw new ArgumentException(
                "Parent strategic cell ID must be valid.",
                nameof(parentStrategicCellId));
        }

        var firstId =
            new TacticalCellId(
                parentStrategicCellId,
                1UL);

        var secondId =
            new TacticalCellId(
                parentStrategicCellId,
                2UL);

        var thirdId =
            new TacticalCellId(
                parentStrategicCellId,
                3UL);

        var cells =
            new[]
            {
                new TacticalCell(
                    firstId,
                    new[]
                    {
                        secondId
                    }),
                new TacticalCell(
                    secondId,
                    new[]
                    {
                        thirdId,
                        firstId
                    }),
                new TacticalCell(
                    thirdId,
                    new[]
                    {
                        secondId
                    })
            };

        return new TacticalRegion(
            parentStrategicCellId,
            cells);
    }
}
