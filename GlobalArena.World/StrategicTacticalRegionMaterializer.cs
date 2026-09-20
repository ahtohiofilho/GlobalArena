namespace GlobalArena.World;

public static class StrategicTacticalRegionMaterializer
{
    public static IReadOnlyList<TacticalRegion> Materialize(
        StrategicTopology strategicTopology)
    {
        ArgumentNullException.ThrowIfNull(
            strategicTopology);

        var regions =
            strategicTopology.Cells
                .Select(
                    strategicCell =>
                        MinimalTacticalRegionGraphGenerator.Generate(
                            strategicCell.Id))
                .ToArray();

        return Array.AsReadOnly(
            regions);
    }
}
