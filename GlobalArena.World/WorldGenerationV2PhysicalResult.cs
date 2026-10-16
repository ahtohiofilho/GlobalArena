namespace GlobalArena.World;

/// <summary>
/// Opt-in V2 physical result. Does not claim canonical V1-derived fields
/// such as hydrology, resources, or civilization suitability.
/// </summary>
public sealed class WorldGenerationV2PhysicalResult
{
    public WorldGenerationRequest Request { get; }
    public int LandTargetPercent { get; }
    public WorldGenerationV2PhysicalComposition PhysicalFields { get; }

    private WorldGenerationV2PhysicalResult(
        WorldGenerationRequest request, int landTargetPercent,
        WorldGenerationV2PhysicalComposition physicalFields)
    {
        Request = request;
        LandTargetPercent = landTargetPercent;
        PhysicalFields = physicalFields;
    }

    public static WorldGenerationV2PhysicalResult Generate(
        WorldGenerationRequest request, int landTargetPercent = 35)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version != WorldGenerationVersion.ProceduralBiomesV2)
            throw new NotSupportedException("Physical V2 result requires ProceduralBiomesV2.");

        // Delegate generation to the single approved V2 entry point.
        var fields = WorldGenerationV2EntryPoint.GeneratePhysical(request, landTargetPercent);
        return new WorldGenerationV2PhysicalResult(request, landTargetPercent, fields);
    }
}