using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs09CanonicalPhysicalEconomyTests
{
    private static (CanonicalGameSession Session, EconomicPhysicalTacticalOfflineSession Economy)
        Start()
    {
        const ulong seed = 20261012UL;
        var game = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        var economy = new EconomicPhysicalTacticalOfflineSession(
            seed, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        return (game, economy);
    }

    [Fact]
    public void LegacyWorldHashV8RemainsUnchangedUntilPhysicalEconomyIsPublished()
    {
        var (game, simulation) = Start();
        var hasher = new CanonicalWorldStateHasher();
        var legacy = hasher.Compute(game.Snapshot);
        Assert.Equal(8U, legacy.FormatVersion);
        Assert.Null(game.Snapshot.PhysicalEconomy);

        var staged = PhysicalEconomyWorldStatePublisher.Stage(
            game.Snapshot, game.GeneratedWorld, simulation);
        Assert.Null(game.Snapshot.PhysicalEconomy);
        Assert.Equal(legacy, hasher.Compute(game.Snapshot));
        Assert.Equal(1UL, staged.Revision);
        Assert.Equal(9U, hasher.Compute(staged).FormatVersion);
        Assert.NotEqual(legacy, hasher.Compute(staged));

        game.AcceptSnapshot(staged);
        Assert.NotNull(game.Snapshot.PhysicalEconomy);
        Assert.Equal(8UL, game.Snapshot.PhysicalEconomy.TotalWorkers);
        Assert.Empty(game.Snapshot.Economy.EconomicPoints);
        Assert.False(game.Snapshot.Population.IsInitialized);
        Assert.Equal(new GoldbergParameters(18, 0),
            game.Snapshot.PhysicalEconomy.FineParameters);
        Assert.Equal(3, game.Snapshot.PhysicalEconomy.Workers.Count);
    }

    [Fact]
    public void PhysicalWorkerMoveAndSixthTurnStockHaveDistinctCanonicalHashes()
    {
        var (game, economy) = Start();
        var hasher = new CanonicalWorldStateHasher();
        var initial = PhysicalEconomyWorldStatePublisher.Stage(
            game.Snapshot, game.GeneratedWorld, economy);
        game.AcceptSnapshot(initial);
        var firstHash = hasher.Compute(initial);

        var origin = economy.Workers.InitialTile;
        var target = economy.Workers.AdjacentWorkable(origin)[0];
        economy.MoveWorkers(origin, target, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);

        var moved = PhysicalEconomyWorldStatePublisher.Stage(
            game.Snapshot, game.GeneratedWorld, economy);
        Assert.Equal(2UL, moved.Revision);
        Assert.NotEqual(firstHash, hasher.Compute(moved));
        Assert.Equal(5UL, moved.PhysicalEconomy!.Workers
            .Where(x => x.TileId == origin).Aggregate(
                0UL, (n, x) => checked(n + x.Workers)));
        Assert.Equal(3UL, moved.PhysicalEconomy.Workers
            .Where(x => x.TileId == target).Aggregate(
                0UL, (n, x) => checked(n + x.Workers)));
        Assert.Equal(8UL, moved.PhysicalEconomy.TotalWorkers);
        game.AcceptSnapshot(moved);

        for (var tick = 0; tick < 5; tick++)
            Assert.Null(economy.AdvanceMovementTurn());
        var fifth = PhysicalEconomyWorldStatePublisher.Stage(
            game.Snapshot, game.GeneratedWorld, economy);
        Assert.Equal(5UL, fifth.PhysicalEconomy!.MovementTurn);
        Assert.Equal(0UL, fifth.PhysicalEconomy.EconomicCycles);
        Assert.Empty(fifth.PhysicalEconomy.Stocks);
        game.AcceptSnapshot(fifth);

        Assert.NotNull(economy.AdvanceMovementTurn());
        var sixth = PhysicalEconomyWorldStatePublisher.Stage(
            game.Snapshot, game.GeneratedWorld, economy);
        Assert.Equal(6UL, sixth.PhysicalEconomy!.MovementTurn);
        Assert.Equal(1UL, sixth.PhysicalEconomy.EconomicCycles);
        Assert.NotEmpty(sixth.PhysicalEconomy.Stocks);
        Assert.NotEqual(hasher.Compute(fifth), hasher.Compute(sixth));
        Assert.Equal(8UL, sixth.PhysicalEconomy.TotalWorkers);
    }

    [Fact]
    public void SnapshotIsDefensivelyCopiedAndStableWhileProducerChanges()
    {
        var (game, economy) = Start();
        var snapshot = PhysicalEconomyWorldStatePublisher.Stage(
            game.Snapshot, game.GeneratedWorld, economy);
        var before = new CanonicalWorldStateHasher().Compute(snapshot);
        Assert.Equal(0UL, snapshot.PhysicalEconomy!.MovementTurn);
        for (var i = 0; i < 6; i++) economy.AdvanceMovementTurn();
        Assert.Equal(0UL, snapshot.PhysicalEconomy.MovementTurn);
        Assert.Empty(snapshot.PhysicalEconomy.Stocks);
        Assert.Equal(before, new CanonicalWorldStateHasher().Compute(snapshot));
        var next = PhysicalEconomyWorldStatePublisher.Stage(
            snapshot, game.GeneratedWorld, economy);
        Assert.NotEqual(before, new CanonicalWorldStateHasher().Compute(next));
        // All unrelated immutable domain mutations preserve physical state.
        Assert.Same(snapshot.PhysicalEconomy,
            snapshot.WithWarfare(WarfareRuntimeState.Empty).PhysicalEconomy);
    }

    [Fact]
    public void RejectsForeignWorldAndInvalidPhysicalSnapshotWithoutPublishing()
    {
        var (game, economy) = Start();
        var before = game.Snapshot;
        var foreign = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261013UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            PhysicalEconomyWorldStatePublisher.Stage(
                foreign.Snapshot, foreign.GeneratedWorld, economy));
        Assert.Same(before, game.Snapshot);

        Assert.Throws<InvalidOperationException>(() =>
            WorldState.CreateInitial().WithPhysicalEconomy(
                new PhysicalEconomyRuntimeState(
                    RuntimeWorldBinding.FromGeneratedWorld(game.GeneratedWorld),
                    new GoldbergParameters(3, 0), new GoldbergParameters(18, 0),
                    0UL, 0UL, 0UL,
                    economy.Workers.Assignments.Select(x =>
                        new PhysicalWorkerRuntimeAllocation(x.TileId, x.Activity, x.Workers)),
                    Array.Empty<PhysicalCommodityRuntimeStock>())));

        var binding = RuntimeWorldBinding.FromGeneratedWorld(game.GeneratedWorld);
        var duplicate = economy.Workers.Assignments.Select(x =>
            new PhysicalWorkerRuntimeAllocation(x.TileId, x.Activity, x.Workers)).ToList();
        duplicate.Add(duplicate[0]);
        Assert.Throws<ArgumentException>(() => new PhysicalEconomyRuntimeState(
            binding, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0),
            0UL, 0UL, 0UL, duplicate, Array.Empty<PhysicalCommodityRuntimeStock>()));
    }

    [Fact]
    public void WorldStateHashTracksPhysicalInventoryButNotPresentationOrder()
    {
        var (game, economy) = Start();
        for (var i = 0; i < 6; i++) economy.AdvanceMovementTurn();
        var baseSnapshot = PhysicalEconomyWorldStatePublisher.Stage(
            game.Snapshot, game.GeneratedWorld, economy);
        var physical = baseSnapshot.PhysicalEconomy!;
        var unordered = new PhysicalEconomyRuntimeState(
            physical.WorldBinding, physical.CoarseParameters, physical.FineParameters,
            physical.MovementTurn, physical.WorkerRevision, physical.EconomicCycles,
            physical.Workers.Reverse(), physical.Stocks.Reverse());
        var same = game.Snapshot.WithPhysicalEconomy(unordered).AdvanceRevision();
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(baseSnapshot), hasher.Compute(same));
        var first = physical.Stocks[0];
        var changed = physical.Stocks.Select(x =>
            x == first ? x with { RawQuantity = checked(x.RawQuantity + 1UL) } : x).ToArray();
        var altered = new PhysicalEconomyRuntimeState(
            physical.WorldBinding, physical.CoarseParameters, physical.FineParameters,
            physical.MovementTurn, physical.WorkerRevision, physical.EconomicCycles,
            physical.Workers, changed);
        var different = game.Snapshot.WithPhysicalEconomy(altered).AdvanceRevision();
        Assert.NotEqual(hasher.Compute(baseSnapshot), hasher.Compute(different));
    }
}
