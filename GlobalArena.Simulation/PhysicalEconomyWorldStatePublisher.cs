using GlobalArena.Runtime;
using GlobalArena.World;

namespace GlobalArena.Simulation;

/// <summary>
/// Opt-in migration bridge from VS07's replay-verified physical simulation to
/// a single typed WorldState snapshot. The simulation still owns turn mechanics;
/// WorldState owns immutable, hash-covered authoritative visible data.
/// No strategic economic points, separate resident census, cities or
/// infrastructure requirements are synthesized.
/// </summary>
public static class PhysicalEconomyWorldStatePublisher
{
    public static WorldState Stage(
        WorldState current,
        WorldGenerationResult generatedWorld,
        EconomicPhysicalTacticalOfflineSession economy)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(generatedWorld);
        ArgumentNullException.ThrowIfNull(economy);
        if (current.WorldBinding is null)
            throw new InvalidOperationException("Cannot publish physical economy into unbound world.");
        var expected = RuntimeWorldBinding.FromGeneratedWorld(generatedWorld);
        var producerBinding = RuntimeWorldBinding.FromGeneratedWorld(economy.Workers.CoarseWorld);
        if (current.WorldBinding != expected || producerBinding != expected)
            throw new InvalidOperationException(
                "Physical economic producer and canonical generated world have different signatures.");
        if (generatedWorld.Request.StrategicParameters !=
            economy.Workers.CoarseWorldParameters)
            throw new InvalidOperationException("Physical economy coarse topology mismatch.");
        if (current.Economy.EconomicPoints.Count != 0 || current.Population.IsInitialized)
            throw new InvalidOperationException(
                "Refusing competing strategic and physical workforce authorities.");

        var physical = new PhysicalEconomyRuntimeState(
            expected,
            economy.Workers.CoarseWorldParameters,
            economy.Workers.FineWorldParameters,
            economy.MovementTurn,
            economy.Workers.Revision,
            economy.EconomicCycles,
            economy.Workers.Assignments.Select(x =>
                new PhysicalWorkerRuntimeAllocation(x.TileId, x.Activity, x.Workers)),
            economy.Stocks.Select(x =>
                new PhysicalCommodityRuntimeStock(x.TileId, x.Commodity, x.RawQuantity)));

        // A complete world revision is published, or nothing changes.
        return current.WithPhysicalEconomy(physical).AdvanceRevision();
    }
}
