namespace GlobalArena.World;

public sealed class WorldGenerationResult
{
    public WorldGenerationRequest Request { get; }

    public StrategicTopology StrategicTopology { get; }

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
    }
}
