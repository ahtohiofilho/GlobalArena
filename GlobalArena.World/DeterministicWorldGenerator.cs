namespace GlobalArena.World;

public sealed class DeterministicWorldGenerator
    : IWorldGenerator
{
    public WorldGenerationResult Generate(
        WorldGenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (request.Version
            != WorldGenerationVersion.Initial)
        {
            throw new NotSupportedException(
                $"World generation version {request.Version.Value} is not supported by this generator.");
        }

        var strategicTopology =
            GoldbergStrategicTopologyGenerator.Generate(
                request.StrategicParameters);

        return new WorldGenerationResult(
            request,
            strategicTopology);
    }
}
