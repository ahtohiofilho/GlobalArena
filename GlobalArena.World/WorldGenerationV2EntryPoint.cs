namespace GlobalArena.World;

/// <summary>
/// Explicit opt-in for V2 physical fields. This does not construct a V1
/// WorldGenerationResult and does not activate dependent canonical systems.
/// </summary>
public static class WorldGenerationV2EntryPoint
{
    public static WorldGenerationV2PhysicalComposition GeneratePhysical(
        WorldGenerationRequest request, int landTargetPercent = 35)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version != WorldGenerationVersion.ProceduralBiomesV2)
            throw new NotSupportedException("V2 physical entry requires ProceduralBiomesV2.");

        var topology = GoldbergStrategicTopologyGenerator.Generate(request.StrategicParameters);
        var geometry = StrategicSphericalGeometryGenerator.Generate(topology);
        return WorldGenerationV2PhysicalComposition.Generate(
            geometry, request.Seed.Value, landTargetPercent);
    }
}