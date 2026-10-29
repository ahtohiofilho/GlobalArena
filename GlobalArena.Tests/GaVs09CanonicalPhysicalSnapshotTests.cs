using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs09CanonicalPhysicalSnapshotTests
{
    private static EconomicPhysicalTacticalOfflineSession NewGame() =>
        new(20261012UL, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));

    private static WorldState PhysicalState(EconomicPhysicalTacticalOfflineSession game) =>
        WorldState.CreateBound(game.Workers.CoarseWorld)
            .WithPhysicalEconomy(game.BuildPhysicalRuntimeSnapshot())
            .AdvanceRevision();

    [Fact]
    public void PhysicalWorkersAndStockBecomeBoundCanonicalWorldStateWithoutStrategicDuplicates()
    {
        var game = NewGame();
        var session = new CanonicalGameSession(game.Workers.CoarseWorld);
        var initial = session.Snapshot;
        Assert.Equal(CanonicalWorldStateHasher.FormatVersion,
            new CanonicalWorldStateHasher().Compute(initial).FormatVersion);

        var projected = initial.WithPhysicalEconomy(game.BuildPhysicalRuntimeSnapshot())
            .AdvanceRevision();
        session.AcceptSnapshot(projected);
        Assert.Same(projected, session.Snapshot);
        Assert.Equal(8UL, projected.PhysicalEconomy!.TotalWorkers);
        Assert.Equal(0UL, projected.PhysicalEconomy.MovementTurn);
        Assert.Empty(projected.Economy.EconomicPoints);
        Assert.False(projected.Population.IsInitialized);
        Assert.Empty(projected.Territory.Controls);
        Assert.Equal(CanonicalWorldStateHasher.PhysicalEconomyFormatVersion,
            new CanonicalWorldStateHasher().Compute(projected).FormatVersion);

        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        var after = projected.WithPhysicalEconomy(game.BuildPhysicalRuntimeSnapshot())
            .AdvanceRevision();
        session.AcceptSnapshot(after);
        Assert.Equal(6UL, after.PhysicalEconomy!.MovementTurn);
        Assert.Single(after.PhysicalEconomy.Cycles);
        Assert.NotEmpty(after.PhysicalEconomy.Stocks);
        Assert.NotEqual(new CanonicalWorldStateHasher().Compute(projected),
            new CanonicalWorldStateHasher().Compute(after));
        Assert.Empty(after.Economy.EconomicPoints);
        Assert.False(after.Population.IsInitialized);
    }

    [Fact]
    public void ImmutableProjectionDoesNotChangeWhenOfflineWorkersAndStocksChange()
    {
        var game = NewGame();
        var projected = PhysicalState(game);
        var frozen = projected.PhysicalEconomy!;
        var from = game.Workers.InitialTile;
        var to = game.Workers.AdjacentWorkable(from).First();
        game.MoveWorkers(from, to, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();

        Assert.Equal(0UL, frozen.MovementTurn);
        Assert.Equal(0UL, frozen.WorkerRevision);
        Assert.Empty(frozen.Stocks);
        Assert.Empty(frozen.Cycles);
        Assert.Equal(8UL, frozen.TotalWorkers);
        Assert.Single(frozen.Allocations.Select(x => x.TileId).Distinct());
        Assert.Equal(1UL, game.Workers.Revision);
    }

    [Fact]
    public void ForeignWorldBindingIsRejectedWithoutChangingLiveSession()
    {
        var source = NewGame();
        var other = new CanonicalGameSession(
            new DeterministicWorldGenerator().Generate(
                new WorldGenerationRequest(new WorldSeed(20261013UL),
                    WorldGenerationVersion.Initial, new GoldbergParameters(3, 0))));
        var before = other.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            other.Snapshot.WithPhysicalEconomy(source.BuildPhysicalRuntimeSnapshot()));
        Assert.Same(before, other.Snapshot);
    }

    [Fact]
    public void V9HashIsDeterministicAfterOfflineSaveReplay()
    {
        var game = NewGame();
        var from = game.Workers.InitialTile;
        var to = game.Workers.AdjacentWorkable(from).First();
        game.MoveWorkers(from, to, EconomicActivityKind.Agriculture,
            EconomicActivityKind.Forestry, 3UL);
        for (var i = 0; i < 12; i++) game.AdvanceMovementTurn();
        var restored = EconomicPhysicalTacticalOfflineSession.RestoreSaveJson(
            game.ExportSaveJson());

        var hasher = new CanonicalWorldStateHasher();
        var before = hasher.Compute(PhysicalState(game));
        var after = hasher.Compute(PhysicalState(restored));
        Assert.Equal(CanonicalWorldStateHasher.PhysicalEconomyFormatVersion,
            before.FormatVersion);
        Assert.Equal(before, after);
        Assert.Equal(2, PhysicalState(restored).PhysicalEconomy!.Cycles.Count);
    }

    [Fact]
    public void DuplicateAllocationsAndMismatchedCadenceAreRejected()
    {
        var game = NewGame();
        var original = game.BuildPhysicalRuntimeSnapshot();
        Assert.Throws<ArgumentException>(() => new PhysicalTacticalEconomyRuntimeState(
            original.CoarseWorldBinding, original.CoarseParameters,
            original.FineParameters, 0UL, 0UL,
            original.Allocations.Concat(original.Allocations.Take(1)),
            original.Stocks, original.Cycles));
        Assert.Throws<ArgumentException>(() => new PhysicalTacticalEconomyRuntimeState(
            original.CoarseWorldBinding, original.CoarseParameters,
            original.FineParameters, 6UL, 0UL,
            original.Allocations, original.Stocks, Array.Empty<PhysicalEconomicCycle>()));
    }

    [Fact]
    public void OrderOfEquivalentPhysicalRowsDoesNotChangeV9Hash()
    {
        var game = NewGame();
        for (var i = 0; i < 6; i++) game.AdvanceMovementTurn();
        var physical = game.BuildPhysicalRuntimeSnapshot();
        var reordered = new PhysicalTacticalEconomyRuntimeState(
            physical.CoarseWorldBinding, physical.CoarseParameters,
            physical.FineParameters, physical.MovementTurn, physical.WorkerRevision,
            physical.Allocations.Reverse(), physical.Stocks.Reverse(),
            physical.Cycles);
        var first = PhysicalState(game);
        var second = WorldState.CreateBound(game.Workers.CoarseWorld)
            .WithPhysicalEconomy(reordered).AdvanceRevision();
        Assert.Equal(new CanonicalWorldStateHasher().Compute(first),
            new CanonicalWorldStateHasher().Compute(second));
    }
}
