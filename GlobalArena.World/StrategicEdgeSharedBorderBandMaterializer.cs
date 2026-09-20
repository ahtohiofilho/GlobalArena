namespace GlobalArena.World;

public static class StrategicEdgeSharedBorderBandMaterializer
{
    public static IReadOnlyList<SharedBorderBand> Materialize(
        StrategicTopology strategicTopology)
    {
        ArgumentNullException.ThrowIfNull(
            strategicTopology);

        var bands =
            strategicTopology.Edges
                .Select(
                    edge =>
                        new SharedBorderBand(
                            edge.Id,
                            new[]
                            {
                                new SharedBorderElement(
                                    new SharedBorderElementId(
                                        edge.Id,
                                        1UL))
                            }))
                .ToArray();

        return Array.AsReadOnly(
            bands);
    }
}
