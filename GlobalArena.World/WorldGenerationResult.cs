namespace GlobalArena.World;

public sealed class WorldGenerationResult
{
    public WorldGenerationRequest Request { get; }

    public StrategicTopology StrategicTopology { get; }

    public StrategicSphericalGeometry StrategicSphericalGeometry { get; }

    public StrategicSurfaceGraph StrategicSurfaceGraph { get; }

    public StrategicPhysicalFieldSet StrategicPhysicalFields { get; }

    public StrategicClimateFieldSet StrategicClimateFields { get; }

    public StrategicHydrologyFieldSet StrategicHydrologyFields { get; }

    public StrategicBiomeMap StrategicBiomes { get; }

    public StrategicResourcePotentialFieldSet StrategicResourcePotentialFields { get; }

    public StrategicScalarField StrategicHabitability { get; }

    public StrategicScalarField StrategicCivilizationPlacementSuitability { get; }

    public WorldGenerationResult(
        WorldGenerationRequest request,
        StrategicTopology strategicTopology)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(strategicTopology);

        if (strategicTopology.Parameters
            != request.StrategicParameters)
        {
            throw new ArgumentException(
                "Strategic topology parameters must match the generation request.",
                nameof(strategicTopology));
        }

        Request = request;
        StrategicTopology = strategicTopology;
        StrategicSphericalGeometry =
            StrategicSphericalGeometryGenerator.Generate(
                strategicTopology);
        StrategicSurfaceGraph =
            new StrategicSurfaceGraph(
                strategicTopology);
        StrategicPhysicalFields =
            StrategicPhysicalFieldSet.Generate(
                request,
                StrategicSurfaceGraph);
        StrategicClimateFields =
            StrategicClimateFieldSet.Generate(
                request,
                StrategicSurfaceGraph,
                StrategicPhysicalFields);
        StrategicHydrologyFields =
            StrategicHydrologyFieldGenerator.Generate(
                StrategicPhysicalFields,
                StrategicClimateFields);
        StrategicBiomes =
            StrategicBiomeClassifier.Generate(
                StrategicPhysicalFields,
                StrategicClimateFields,
                StrategicHydrologyFields);
        StrategicResourcePotentialFields =
            StrategicResourcePotentialFieldSet.Generate(
                request,
                StrategicSurfaceGraph);
        StrategicHabitability =
            StrategicHabitabilityFieldGenerator.Generate(
                StrategicPhysicalFields,
                StrategicClimateFields);
        StrategicCivilizationPlacementSuitability =
            StrategicCivilizationPlacementSuitabilityGenerator.Generate(
                StrategicPhysicalFields,
                StrategicHabitability,
                StrategicResourcePotentialFields);
    }
}
