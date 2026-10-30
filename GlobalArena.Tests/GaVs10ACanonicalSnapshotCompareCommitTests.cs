using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class GaVs10ACanonicalSnapshotCompareCommitTests
{
    [Fact]
    public void CompareAndCommitRejectsStaleReferenceEvenWithValidRevision()
    {
        var game = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261012UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        var baseSnapshot = game.Snapshot;
        var winner = baseSnapshot.AdvanceRevision();
        var staleCandidate = baseSnapshot.AdvanceRevision();

        game.AcceptSnapshotIfCurrent(baseSnapshot, winner);
        Assert.Same(winner, game.Snapshot);
        Assert.Throws<InvalidOperationException>(() =>
            game.AcceptSnapshotIfCurrent(baseSnapshot, staleCandidate));
        Assert.Same(winner, game.Snapshot);

        Assert.Throws<InvalidOperationException>(() =>
            game.AcceptSnapshotIfCurrent(winner, winner));
        Assert.Same(winner, game.Snapshot);
    }

    [Fact]
    public void CanonicalPhysicalResolverFailsClosedAfterExternalSnapshotAdvance()
    {
        var physical = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0));
        var stateBefore = physical.Game.Snapshot;
        var initialJournal = physical.Events.Count;
        var initialHash = new CanonicalWorldStateHasher().Compute(stateBefore);
        var external = stateBefore.AdvanceRevision();
        physical.Game.AcceptSnapshot(external);

        Assert.Throws<InvalidOperationException>(() =>
            physical.AdvanceMovementTurn());
        Assert.Throws<InvalidOperationException>(() =>
            physical.MoveWorkers(
                physical.InitialTile,
                new PhysicalTacticalTileId(
                    new GoldbergParameters(18, 0),
                    new StrategicCellId(1UL)),
                EconomicActivityKind.Agriculture,
                EconomicActivityKind.Forestry, 1UL));
        Assert.Throws<InvalidOperationException>(() =>
            physical.ExportSaveJson());

        Assert.Same(external, physical.Game.Snapshot);
        Assert.Equal(initialJournal, physical.Events.Count);
        Assert.Equal(0UL, external.PhysicalEconomy!.MovementTurn);
        Assert.Equal(initialHash.FormatVersion,
            new CanonicalWorldStateHasher().Compute(external).FormatVersion);
    }

    [Fact]
    public void NormalPhysicalTurnAndReplayStillCommitAndRestoreIdenticalHashes()
    {
        var physical = CanonicalPhysicalTacticalSession.Start(
            20261012UL, new GoldbergParameters(3, 0),
            new GoldbergParameters(18, 0));
        for (var i = 0; i < 12; i++)
            physical.AdvanceMovementTurn();
        var priorHash = physical.CurrentHash;
        var save = physical.ExportSaveJson();
        var reopened = CanonicalPhysicalTacticalSession.RestoreSaveJson(save);

        Assert.Equal(priorHash, reopened.CurrentHash);
        Assert.Equal(save, reopened.ExportSaveJson());
        Assert.Equal(12UL, reopened.State.MovementTurn);
        Assert.Equal(2, reopened.State.Cycles.Count);
        Assert.Equal(13UL, reopened.Game.Snapshot.Revision);
    }
}
