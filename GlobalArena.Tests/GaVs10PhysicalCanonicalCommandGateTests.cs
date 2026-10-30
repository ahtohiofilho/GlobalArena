using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs10PhysicalCanonicalCommandGateTests
{
    private static (
        CanonicalGameSession Game,
        EconomicPhysicalTacticalOfflineSession Initial,
        PhysicalEconomyCanonicalCommandGate Gate) Start()
    {
        const ulong seed = 20261012UL;
        var game = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        var initial = new EconomicPhysicalTacticalOfflineSession(
            seed, new GoldbergParameters(3, 0), new GoldbergParameters(18, 0));
        var gate = new PhysicalEconomyCanonicalCommandGate(game, initial);
        return (game, initial, gate);
    }

    [Fact]
    public void ConsecutiveTicksCommitOnceAndSettleOnlyOnSixthTick()
    {
        var (game, _, gate) = Start();
        Assert.Equal(1UL, game.Snapshot.Revision);
        Assert.Equal(1UL, gate.NextSequence);
        Assert.Equal(9U, new CanonicalWorldStateHasher()
            .Compute(game.Snapshot).FormatVersion);

        for (ulong tick = 1UL; tick <= 6UL; tick++)
        {
            var receipt = gate.AdvanceMovementTurn(tick, tick);
            Assert.Equal(tick, receipt.Sequence);
            Assert.Equal(tick, receipt.PreviousRevision);
            Assert.Equal(tick + 1UL, receipt.ResultingRevision);
            Assert.Equal(tick, receipt.MovementTurn);
            Assert.Equal(tick == 6UL ? 1UL : 0UL, receipt.EconomicCycles);
            if (tick < 6UL) Assert.Null(receipt.Settlement);
            else Assert.NotNull(receipt.Settlement);
        }

        Assert.Equal(6UL, game.Snapshot.PhysicalEconomy!.MovementTurn);
        Assert.Equal(1UL, game.Snapshot.PhysicalEconomy.EconomicCycles);
        Assert.NotEmpty(game.Snapshot.PhysicalEconomy.Stocks);
        Assert.Equal(6, gate.Journal.Count);
        Assert.Equal(7UL, gate.NextSequence);
        Assert.Throws<InvalidOperationException>(() =>
            gate.AdvanceMovementTurn(6UL, 7UL));
        Assert.Equal(6UL, game.Snapshot.PhysicalEconomy.MovementTurn);
        Assert.Equal(1UL, game.Snapshot.PhysicalEconomy.EconomicCycles);
    }

    [Fact]
    public void InvalidOrderAndStaleRevisionDoNotMutateCanonicalOrProducer()
    {
        var (game, initial, gate) = Start();
        var first = game.Snapshot;
        var firstHash = new CanonicalWorldStateHasher().Compute(first);
        var source = initial.Workers.InitialTile;

        Assert.Throws<InvalidOperationException>(() =>
            gate.MoveWorkers(1UL, 1UL, source, source,
                EconomicActivityKind.Agriculture,
                EconomicActivityKind.Forestry, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            gate.AdvanceMovementTurn(2UL, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            gate.AdvanceMovementTurn(1UL, 0UL));

        Assert.Same(first, game.Snapshot);
        Assert.Equal(firstHash, new CanonicalWorldStateHasher().Compute(game.Snapshot));
        Assert.Empty(gate.Journal);
        Assert.Equal(1UL, gate.NextSequence);
        Assert.Equal(0UL, gate.CopyForPresentation().MovementTurn);

        // The object originally supplied to the gate is no longer an owner.
        initial.AdvanceMovementTurn();
        Assert.Equal(0UL, gate.CopyForPresentation().MovementTurn);

        // Nor can presentation copies write back into the accepted state.
        var spectator = gate.CopyForPresentation();
        spectator.AdvanceMovementTurn();
        Assert.Equal(0UL, gate.CopyForPresentation().MovementTurn);
        Assert.Equal(firstHash, new CanonicalWorldStateHasher().Compute(game.Snapshot));
    }

    [Fact]
    public void MoveIsCommittedWithWorkerConservationAndHashChange()
    {
        var (game, initial, gate) = Start();
        var source = initial.Workers.InitialTile;
        var target = initial.Workers.AdjacentWorkable(source)[0];
        var receipt = gate.MoveWorkers(1UL, 1UL, source, target,
            EconomicActivityKind.Agriculture, EconomicActivityKind.Forestry, 3UL);

        Assert.Equal("move", receipt.CommandKind);
        Assert.NotEqual(receipt.PreviousHash, receipt.ResultingHash);
        Assert.Equal(8UL, game.Snapshot.PhysicalEconomy!.TotalWorkers);
        Assert.Equal(3UL, game.Snapshot.PhysicalEconomy.Workers
            .Where(x => x.TileId == target)
            .Aggregate(0UL, (total, x) => checked(total + x.Workers)));
        Assert.Equal(1UL, gate.CopyForPresentation().Workers.Revision);
        Assert.Equal(2UL, game.Snapshot.Revision);
    }

    [Fact]
    public void RestoreCreatesExactlyOneNewCanonicalRevisionWithoutExtraSettlement()
    {
        var (game, _, gate) = Start();
        var zeroTurnSave = gate.ExportOfflineSaveJson();
        for (ulong tick = 1; tick <= 6; tick++)
            gate.AdvanceMovementTurn(tick, tick);
        var beforeRestore = game.Snapshot;
        var restored = gate.RestoreOfflineSave(7UL, 7UL, zeroTurnSave);

        Assert.Equal("restore", restored.CommandKind);
        Assert.Null(restored.Settlement);
        Assert.Equal(8UL, restored.ResultingRevision);
        Assert.Equal(0UL, game.Snapshot.PhysicalEconomy!.MovementTurn);
        Assert.Equal(0UL, game.Snapshot.PhysicalEconomy.EconomicCycles);
        Assert.Empty(game.Snapshot.PhysicalEconomy.Stocks);
        Assert.NotSame(beforeRestore, game.Snapshot);
        Assert.Throws<InvalidOperationException>(() =>
            gate.RestoreOfflineSave(7UL, 8UL, zeroTurnSave));

        var resumedTurn = gate.AdvanceMovementTurn(8UL, 8UL);
        Assert.Null(resumedTurn.Settlement);
        Assert.Equal(1UL, resumedTurn.MovementTurn);
        Assert.Equal(0UL, resumedTurn.EconomicCycles);
    }

    [Fact]
    public void ForeignWorldAndExternalSessionMutationFailClosed()
    {
        var (game, initial, gate) = Start();
        var foreign = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261013UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        Assert.Throws<InvalidOperationException>(() =>
            new PhysicalEconomyCanonicalCommandGate(foreign, initial));
        Assert.Equal(0UL, foreign.Snapshot.Revision);

        var external = game.Snapshot.AdvanceRevision();
        game.AcceptSnapshot(external);
        Assert.Throws<InvalidOperationException>(() =>
            gate.AdvanceMovementTurn(1UL, 1UL));
        Assert.Same(external, game.Snapshot);
        Assert.Empty(gate.Journal);
    }
}
