using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationLocalGameHostTests
{
    private static WorldGenerationRequest Request(ulong seed = 20261012UL) =>
        new(new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0));

    [Fact]
    public void PreviewHasRealCanonicalWorkersFromNewGameNotInventedGodotObjects()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        Assert.True(host.IsPreviewScenario);
        Assert.True(host.Snapshot.Workers.IsInitialized);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
        Assert.Equal(new ulong[] { 1, 2, 3, 4, 5, 6 },
            host.Snapshot.Workers.Workers.Select(w => w.Id.Value));
        Assert.Equal(3, host.ControllableWorkers.Count);
        Assert.Equal(new CivilizationId(1UL), host.PlayerCivilization);
        Assert.Equal(92, host.Session.GeneratedWorld.StrategicSurfaceGraph.NodeCount);
        Assert.Equal(270, host.Session.GeneratedWorld.StrategicTopology.Edges.Count);
        Assert.Equal(9U, host.StateHash.FormatVersion);
        Assert.Equal(0UL, host.Snapshot.Revision);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
        Assert.Equal(4, host.Snapshot.Workers.Workers.Count(w =>
            w.Activity == PhysicalWorkerActivity.Producing));
        Assert.All(host.Snapshot.Workers.Workers,
            w => Assert.True(host.Snapshot.WorldBinding!.Contains(w.CellId)));
    }

    [Fact]
    public void LocalPreviewPopulationIsDeterministicAndWorldSeedIsRespected()
    {
        var one = FoundationLocalGameHost.CreateLocalPreview(Request());
        var two = FoundationLocalGameHost.CreateLocalPreview(Request());
        Assert.Equal(one.StateHash, two.StateHash);
        Assert.Equal(one.ControllableWorkers.Select(x => x.Id),
            two.ControllableWorkers.Select(x => x.Id));
        var different = FoundationLocalGameHost.CreateLocalPreview(Request(20261013UL));
        Assert.NotEqual(one.Snapshot.WorldBinding, different.Snapshot.WorldBinding);
        Assert.NotEqual(one.StateHash, different.StateHash);
    }

    [Fact]
    public void GeneralStartAcceptsExplicitWorkerCensusInsteadOfFixedPreviewNumbers()
    {
        var one = new CivilizationId(71UL);
        var host = FoundationLocalGameHost.StartNew(
            Request(),
            new[] { new CivilizationRuntimeRecord(one, new StrategicCellId(10UL)) },
            new[]
            {
                new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(99UL), one,
                    new StrategicCellId(10UL), new CommodityId(4U),
                    7_000_000UL, PhysicalWorkerActivity.Producing)
            }, one);
        Assert.False(host.IsPreviewScenario);
        Assert.Single(host.ControllableWorkers);
        Assert.Equal(99UL, host.Snapshot.Workers.Workers.Single().Id.Value);
        Assert.Equal(4U, host.Snapshot.Workers.Workers.Single().Commodity.Value);
        Assert.Equal(9U, host.StateHash.FormatVersion);
    }

    [Fact]
    public void GeneralStartRejectsUnknownPlayerAndBadWorkerLocation()
    {
        var roster = new[]
        {
            new CivilizationRuntimeRecord(new CivilizationId(1UL))
        };
        var worker = new PhysicalWorkerRuntimeRecord(
            new PhysicalWorkerId(1UL), new CivilizationId(1UL),
            new StrategicCellId(1UL), new CommodityId(1U),
            1_000_000UL, PhysicalWorkerActivity.Producing);

        Assert.Throws<ArgumentException>(() =>
            FoundationLocalGameHost.StartNew(Request(), roster,
                new[] { worker }, new CivilizationId(2UL)));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationLocalGameHost.StartNew(Request(), roster,
                new[] { worker.WithLocationAndActivity(
                    new StrategicCellId(93UL), PhysicalWorkerActivity.Traveling) },
                new CivilizationId(1UL)));
    }

    [Fact]
    public void CanMoveChecksRealGoldbergAdjacencyAndOwnership()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var worker = host.ControllableWorkers[0];
        var origin = graph.GetNodeIndex(worker.CellId);
        var neighbor = graph.GetCellId(graph.GetNeighborIndexes(origin)[0]);
        var far = graph.GetCellId(graph.NodeCount / 2);

        Assert.True(host.CanMove(worker.Id, neighbor));
        Assert.False(host.CanMove(worker.Id, worker.CellId));
        Assert.False(host.CanMove(worker.Id, far));
        Assert.False(host.CanMove(new PhysicalWorkerId(4UL), neighbor)); // foreign civ
        Assert.False(host.CanMove(new PhysicalWorkerId(999UL), neighbor));
        Assert.False(host.CanMove(worker.Id, new StrategicCellId(93UL)));
    }

    [Fact]
    public void PlayerMoveCommitsOneRealTurnAndReplaysTheExactV9State()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        var original = host.Snapshot;
        var worker = host.ControllableWorkers[0];
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var neighbor = graph.GetCellId(
            graph.GetNeighborIndexes(graph.GetNodeIndex(worker.CellId))[0]);
        var beforeHash = host.StateHash;

        var receipt = host.MoveWorker(worker.Id, neighbor);

        Assert.Equal(new TurnNumber(1UL), receipt.Turn);
        Assert.Equal(worker.CellId, receipt.Source);
        Assert.Equal(neighbor, receipt.Destination);
        Assert.Equal(worker.Id, receipt.WorkerId);
        Assert.Equal(PhysicalWorkerActivity.Traveling, receipt.Activity);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(1, receipt.ExecutedEvents);
        Assert.Equal(1UL, receipt.ResultingRevision);
        Assert.Equal(9U, receipt.ResultingHash.FormatVersion);
        Assert.Equal(host.StateHash, receipt.ResultingHash);
        Assert.NotEqual(beforeHash, receipt.ResultingHash);
        Assert.NotSame(original, host.Snapshot);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        var currentWorker = host.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id);
        Assert.Equal(neighbor, currentWorker.CellId);
        Assert.Equal(worker.OutputMicroPerTurn, currentWorker.OutputMicroPerTurn);
        Assert.Equal(PhysicalWorkerActivity.Traveling, currentWorker.Activity);
        Assert.Equal(worker.CellId,
            original.Workers.Workers.Single(w => w.Id == worker.Id).CellId);
    }

    [Fact]
    public void MovementUpdatesInspectorDemandLocationWithoutExtraWorkersOrProduction()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        var initial = host.Snapshot;
        var worker = host.ControllableWorkers[0];
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var neighbor = graph.GetCellId(
            graph.GetNeighborIndexes(graph.GetNodeIndex(worker.CellId))[0]);
        var atOldTile = StrategicPhysicalWorkerInspector.Read(
            host.Session.GeneratedWorld, worker.CellId, initial);
        host.MoveWorker(worker.Id, neighbor);
        var afterOrigin = StrategicPhysicalWorkerInspector.Read(
            host.Session.GeneratedWorld, worker.CellId, host.Snapshot);
        var afterDestination = StrategicPhysicalWorkerInspector.Read(
            host.Session.GeneratedWorld, neighbor, host.Snapshot);

        Assert.Equal(atOldTile.DemandingWorkerCount - 1,
            afterOrigin.DemandingWorkerCount);
        Assert.Equal(1, afterDestination.Workers.Count(w => w.Id == worker.Id));
        Assert.Equal(1, afterDestination.TravelingWorkerCount);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
        Assert.Equal(6, host.Snapshot.Workers.Workers
            .Sum(w => w.DemandMultiplierMicro / 1_000_000m));
    }

    [Fact]
    public void ProducerCanReenterProductionAtItsDestinationInNextAuthoritativeTurn()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        var worker = host.ControllableWorkers[0];
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var neighbor = graph.GetCellId(
            graph.GetNeighborIndexes(graph.GetNodeIndex(worker.CellId))[0]);

        var one = host.MoveWorker(worker.Id, neighbor);
        Assert.True(host.CanChangeActivity(worker.Id,
            PhysicalWorkerActivity.Producing));
        var two = host.ChangeWorkerActivity(worker.Id,
            PhysicalWorkerActivity.Producing);

        Assert.Equal(new TurnNumber(2UL), two.Turn);
        Assert.Equal(new TurnNumber(3UL), host.NextTurn);
        Assert.Equal(2UL, host.Snapshot.Revision);
        Assert.Equal(neighbor, two.Source);
        Assert.Equal(neighbor, two.Destination);
        Assert.Equal(PhysicalWorkerActivity.Producing, two.Activity);
        Assert.True(two.ReplayMatched);
        Assert.NotEqual(one.ResultingHash, two.ResultingHash);
        Assert.False(host.CanChangeActivity(worker.Id,
            PhysicalWorkerActivity.Producing));
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
    }

    [Fact]
    public void MilitaryWorkerWithNoOutputCannotBeMarkedAsProducing()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        var military = host.ControllableWorkers.Single(
            w => w.Activity == PhysicalWorkerActivity.Military);
        var before = host.Snapshot;
        Assert.Equal(0UL, military.OutputMicroPerTurn);
        Assert.False(host.CanChangeActivity(military.Id,
            PhysicalWorkerActivity.Producing));
        Assert.Throws<InvalidOperationException>(() =>
            host.ChangeWorkerActivity(military.Id,
                PhysicalWorkerActivity.Producing));
        Assert.Same(before, host.Snapshot);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
    }

    [Fact]
    public void InvalidOrdersAreRejectedBeforeAnyTurnOrHashChanges()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        var before = host.Snapshot;
        var hash = host.StateHash;
        var worker = host.ControllableWorkers[0];
        var far = host.Session.GeneratedWorld.StrategicSurfaceGraph.GetCellId(46);

        Assert.Throws<InvalidOperationException>(() =>
            host.MoveWorker(worker.Id, far));
        Assert.Throws<InvalidOperationException>(() =>
            host.MoveWorker(new PhysicalWorkerId(4UL), worker.CellId));
        Assert.Throws<InvalidOperationException>(() =>
            host.MoveWorker(worker.Id, worker.CellId));
        Assert.Throws<InvalidOperationException>(() =>
            host.MoveWorker(worker.Id, new StrategicCellId(93UL)));
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
    }

    [Fact]
    public void NoMarketSettlementOrPopulationArtifactsArePublishedByWorkerCommands()
    {
        var host = FoundationLocalGameHost.CreateLocalPreview(Request());
        var worker = host.ControllableWorkers[0];
        var neighbor = host.Session.GeneratedWorld.StrategicSurfaceGraph
            .GetCellId(host.Session.GeneratedWorld.StrategicSurfaceGraph
                .GetNeighborIndexes(0)[0]);
        host.MoveWorker(worker.Id, neighbor);
        Assert.Empty(host.Snapshot.Economy.EconomicPoints);
        Assert.False(host.Snapshot.Population.IsInitialized);
        Assert.Empty(host.Snapshot.Warfare.Units);
        Assert.True(host.Snapshot.Workers.IsInitialized);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
    }

    [Fact]
    public void TwoLocalSessionsCommitTheSameSequenceAndGetIdenticalReplayHashes()
    {
        var one = FoundationLocalGameHost.CreateLocalPreview(Request());
        var two = FoundationLocalGameHost.CreateLocalPreview(Request());
        var graph = one.Session.GeneratedWorld.StrategicSurfaceGraph;
        var first = graph.GetCellId(0);
        var neighbor = graph.GetCellId(graph.GetNeighborIndexes(0)[0]);

        var left1 = one.MoveWorker(new PhysicalWorkerId(1UL), neighbor);
        var right1 = two.MoveWorker(new PhysicalWorkerId(1UL), neighbor);
        var left2 = one.ChangeWorkerActivity(
            new PhysicalWorkerId(1UL), PhysicalWorkerActivity.Producing);
        var right2 = two.ChangeWorkerActivity(
            new PhysicalWorkerId(1UL), PhysicalWorkerActivity.Producing);
        var left3 = one.MoveWorker(new PhysicalWorkerId(1UL), first);
        var right3 = two.MoveWorker(new PhysicalWorkerId(1UL), first);

        Assert.Equal(left1.ResultingHash, right1.ResultingHash);
        Assert.Equal(left2.ResultingHash, right2.ResultingHash);
        Assert.Equal(left3.ResultingHash, right3.ResultingHash);
        Assert.Equal(one.StateHash, two.StateHash);
        Assert.Equal(new TurnNumber(4UL), one.NextTurn);
        Assert.Equal(new TurnNumber(4UL), two.NextTurn);
    }
}
