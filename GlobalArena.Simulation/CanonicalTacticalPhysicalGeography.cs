using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Deterministic fine-grid economic view of the exact 01C physical terrain
/// used by the tactical renderer. The strategic V1 world remains intact.
/// </summary>
public sealed class CanonicalTacticalPhysicalGeography
{
    public const int LandTargetPercent = 35;
    private const long D = 1_000_000L;

    private readonly WorldGenerationResult _world;
    public WorldGenerationV2PhysicalComposition Terrain { get; }
    public StrategicCellId InitialWorkableTile { get; }

    public CanonicalTacticalPhysicalGeography(WorldGenerationResult fineWorld, ulong seed)
    {
        _world = fineWorld ?? throw new ArgumentNullException(nameof(fineWorld));
        if (_world.Request.Seed.Value != seed)
            throw new InvalidOperationException("Canonical tactical geography seed differs from the fine world.");
        Terrain = WorldGenerationV2PhysicalComposition.Generate(
            fineWorld.StrategicSphericalGeometry, seed, LandTargetPercent);
        if (Terrain.Biomes.Count != fineWorld.StrategicTopology.Cells.Count)
            throw new InvalidOperationException("01C physical geography does not share the fine tile index.");

        // A new settlement must be on displayed land, be free of visible ice,
        // and have a legal adjacent land tile. Never pick a visual ocean tile.
        var candidate = fineWorld.StrategicTopology.Cells
            .Where(c => IsWorkableLand(c.Id) && c.AdjacentCellIds.Any(IsWorkableLand))
            .OrderByDescending(c => Productivity(c.Id, EconomicActivityKind.Agriculture))
            .ThenBy(c => c.Id.Value)
            .FirstOrDefault();
        InitialWorkableTile = candidate?.Id ??
            throw new InvalidOperationException("No connected workable 01C tactical land found.");
    }

    public bool IsWorkableLand(StrategicCellId tile)
    {
        var index = CheckedIndex(tile);
        var type = Terrain.Biomes[index];
        return !type.IsWater &&
               type.VisibleBiome != ExperimentalProceduralBiomeClassifier.Biome.Ice;
    }

    public long Productivity(StrategicCellId tile, EconomicActivityKind activity)
    {
        var i = CheckedIndex(tile);
        if (!IsWorkableLand(tile)) return 0L;

        var biome = Terrain.Biomes[i].UnderlyingBiome;
        long moisture = Math.Clamp(Terrain.Moisture[i], -D, D);
        long temperature = Math.Clamp(Terrain.Temperature[i], -D, D);
        long aboveSea = Math.Clamp(Terrain.Elevation[i] - Terrain.SeaLevel, 0L, D);
        // Every colonizable land tile has nonzero rudimentary agriculture.
        // Climate/moisture/relief coefficients are provisional, not geology.
        long humidity = Math.Max(100_000L, (moisture + D) / 2L);
        long comfort = Math.Max(100_000L, D - Math.Abs(temperature));
        long flatness = Math.Max(200_000L, D - aboveSea);
        return activity switch
        {
            EconomicActivityKind.Agriculture =>
                Math.Max(1L, checked((long)((Int128)humidity * comfort * flatness / ((Int128)D * D)))),
            EconomicActivityKind.Forestry =>
                biome == ExperimentalProceduralBiomeClassifier.Biome.Forest
                    ? Math.Max(1L, humidity) : 0L,
            EconomicActivityKind.Mining =>
                Math.Clamp(250_000L + aboveSea / 2L + (D - humidity) / 8L, 1L, D),
            _ => throw new ArgumentOutOfRangeException(nameof(activity))
        };
    }

    private int CheckedIndex(StrategicCellId tile)
    {
        if (!tile.IsValid || tile.Value > (ulong)_world.StrategicTopology.Cells.Count)
            throw new ArgumentOutOfRangeException(nameof(tile), "Unknown canonical fine tile.");
        return checked((int)tile.Value - 1);
    }
}
