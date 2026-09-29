namespace GlobalArena.World;

public interface IWorldGenerator
{
    WorldGenerationResult Generate(
        WorldGenerationRequest request);
}
