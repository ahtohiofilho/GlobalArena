using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Minimal Neolithic production reference: food cultivation, wood gathering
/// and loose-stone/surface extraction require WORKERS and a caller-specified
/// positive natural productivity rate, but no built facility or stock input.
/// The caller's resource-specific geographic rates are NOT yet an
/// authoritative resource-specific worldgen field.
/// </summary>
public sealed class EconomicPrimitiveWorkerEconomyResolver
{
    private readonly EconomicProductionDemandResolver _flows = new();

    public EconomicProductionDemandResult Resolve(
        WorldGenerationResult generatedWorld,
        WorldState world,
        IEnumerable<EconomicProductionPotential> primitivePotentials,
        IEnumerable<EconomicDemandProfile> workerDemandProfiles)
    {
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(primitivePotentials);
        ArgumentNullException.ThrowIfNull(workerDemandProfiles);

        if (world.WorldBinding != RuntimeWorldBinding.FromGeneratedWorld(generatedWorld))
            throw new InvalidOperationException(
                "Primitive production requires the matching bound generated world.");

        var potentials = primitivePotentials.ToArray();
        if (potentials.Any(p => p is null))
            throw new ArgumentException("Primitive potentials must not contain null.", nameof(primitivePotentials));

        var points = world.Economy.EconomicPoints.ToDictionary(x => x.Id);
        var land = generatedWorld.StrategicPhysicalFields.LandWater;
        foreach (var potential in potentials)
        {
            if (potential.Activity != EconomicActivityKind.Agriculture &&
                potential.Activity != EconomicActivityKind.Forestry &&
                potential.Activity != EconomicActivityKind.Mining)
                throw new ArgumentException(
                    "Primitive production only supports cultivation, wood gathering and stone extraction.",
                    nameof(primitivePotentials));

            if (!points.TryGetValue(potential.EconomicPointId, out var point))
                throw new InvalidOperationException("Primitive production point is not in the world.");

            if (land.GetKind(point.StrategicCellId) != StrategicLandWaterKind.Land)
                throw new InvalidOperationException("Primitive land-based activity requires a land cell.");
        }

        // Existing worker-based resolver: all workers at a point contribute
        // to demand independently of their productive assignment. Passing
        // zero potentials is permitted; no natural resource is fabricated.
        // Nothing here requires a farm, quarry, logging camp, stock, terminal
        // or a distinct "residents" demand source.
        return _flows.Resolve(world.Economy, potentials, workerDemandProfiles);
    }
}
