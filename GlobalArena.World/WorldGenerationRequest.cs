namespace GlobalArena.World;

public sealed record WorldGenerationRequest
{
    public WorldSeed Seed { get; }

    public WorldGenerationVersion Version { get; }

    public GoldbergParameters StrategicParameters { get; }

    public WorldGenerationRequest(
        WorldSeed seed,
        WorldGenerationVersion version,
        GoldbergParameters strategicParameters)
    {
        if (!version.IsValid)
        {
            throw new ArgumentException(
                "World generation version must be valid.",
                nameof(version));
        }

        if (!strategicParameters.IsValid)
        {
            throw new ArgumentException(
                "Strategic Goldberg parameters must be valid.",
                nameof(strategicParameters));
        }

        Seed = seed;
        Version = version;
        StrategicParameters = strategicParameters;
    }
}
