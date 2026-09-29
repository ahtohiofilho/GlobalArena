namespace GlobalArena.World;

public static class WorldGenerationRandomStreamFactory
{
    private const ulong WorldGenerationSalt =
        0xA0761D6478BD642FUL;

    public static WorldGenerationRandomStream Create(
        WorldGenerationRequest request,
        WorldGenerationRandomDomain domain)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (!Enum.IsDefined(
            typeof(WorldGenerationRandomDomain),
            domain))
        {
            throw new ArgumentOutOfRangeException(
                nameof(domain),
                "World-generation random domain must be a defined domain.");
        }

        var streamSeed =
            DeriveStreamSeed(
                request,
                domain);

        return new WorldGenerationRandomStream(
            streamSeed);
    }

    private static ulong DeriveStreamSeed(
        WorldGenerationRequest request,
        WorldGenerationRandomDomain domain)
    {
        unchecked
        {
            var state =
                Mix(
                    request.Seed.Value
                    ^ WorldGenerationSalt);

            state =
                Mix(
                    state
                    ^ (ulong)(uint)request.Version.Value);

            var parameterKey =
                ((ulong)(uint)request.StrategicParameters.M << 32)
                | (uint)request.StrategicParameters.N;

            state =
                Mix(
                    state
                    ^ parameterKey);

            state =
                Mix(
                    state
                    ^ (ulong)domain);

            return state;
        }
    }

    private static ulong Mix(
        ulong value)
    {
        unchecked
        {
            value +=
                0x9E3779B97F4A7C15UL;

            value =
                (value ^ (value >> 30))
                * 0xBF58476D1CE4E5B9UL;

            value =
                (value ^ (value >> 27))
                * 0x94D049BB133111EBUL;

            return value
                ^ (value >> 31);
        }
    }
}
