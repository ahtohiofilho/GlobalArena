using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>Full v10 Human + AI collective turn, using real canonical
/// fine Goldberg tiles and their edges (no strategic movement fallback).</summary>
public sealed class FoundationTacticalHumanAiV10Tests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);

    private static WorldGenerationRequest Request(ulong seed = 20261116UL) =>
        new(new WorldSeed(seed), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0));

    private static FoundationTacticalHumanAiTurnHost Host() =>
        FoundationTacticalDemoScenario.CreateLocalHumanAiPreview(Request());

    private static PhysicalTacticalTileId Neighbor(
        FoundationTacticalHumanAiTurnHost host, ulong workerId)
    {
        var worker = host.Snapshot.Workers.Workers.Single(w => w.Id.Value == workerId);
        var source = worker.TacticalTileId!.Value;
        var next = host.Incidence.FineTopology.Cells[
            checked((int)source.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds[0];
        return new PhysicalTacticalTileId(
            host.Incidence.Refinement.FineParameters, next);
    }

    private sealed class FakePolicy : IFoundationTacticalAiTurnPolicy
    {
        private readonly Func<CivilizationId, WorldState,
            PhysicalTacticalIncidenceMap, TurnNumber,
            IReadOnlyList<FoundationTacticalAiDecision>> _plan;
        public int Calls { get; private set; }

        public FakePolicy(Func<CivilizationId, WorldState,
            PhysicalTacticalIncidenceMap, TurnNumber,
            IReadOnlyList<FoundationTacticalAiDecision>> plan) =>
            _plan = plan;

        public IReadOnlyList<FoundationTacticalAiDecision> Plan(
            CivilizationId civ, WorldState snapshot,
            PhysicalTacticalIncidenceMap incidence, TurnNumber turn)
        {
            Calls++;
            return _plan(civ, snapshot, incidence, turn);
        }
    }

    [Fact]
    public void DemoDeploymentPreservesAllSixWorkerFactsOnDistinctFineTiles()
    {
        var a = Host();
        var b = Host();
        Assert.Equal(10U, a.StateHash.FormatVersion);
        Assert.Equal(a.StateHash, b.StateHash);
        Assert.Equal(6, a.Snapshot.Workers.Workers.Count);
        Assert.Equal(new ulong[] { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL },
            a.Snapshot.Workers.Workers.Select(w => w.Id.Value));
        Assert.Equal(6, a.Snapshot.Workers.Workers
            .Select(w => w.TacticalTileId!.Value).Distinct().Count());
        Assert.All(a.Snapshot.Workers.Workers, w =>
        {
            var fine = w.TacticalTileId!.Value;
            Assert.Equal(new GoldbergParameters(18, 0),
                fine.FineGoldbergParameters);
            Assert.Contains(w.CellId, a.Incidence.TileIncidences[
                checked((int)fine.FineStrategicCellId.Value - 1)]
                .IncidentCoarseCellIds);
        });
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            a.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL).Activity);
        Assert.Equal(new ulong[] { 1UL }, a.HumanCivilizations.Select(x => x.Value));
        Assert.Equal(new ulong[] { 2UL }, a.AiCivilizations.Select(x => x.Value));
        Assert.Equal(new TurnNumber(1UL), a.NextTurn);
        Assert.Equal(0UL, a.Snapshot.Revision);
    }

    [Fact]
    public void UnreadyHumanDoesNotPublishEvenAfterAiFinishesPlanning()
    {
        var host = Host();
        var before = host.Snapshot;
        var hash = host.StateHash;
        host.PrepareAiCivilizations();
        Assert.Equal(new ulong[] { 2UL }, host.ReadyCivilizations.Select(x => x.Value));
        Assert.Single(host.Snapshot.Workers.Workers, w =>
            w.Id.Value == 4UL && w.Activity == PhysicalWorkerActivity.Traveling);
        Assert.Null(host.TryCloseTurn());
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(0UL, host.Snapshot.Revision);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
    }

    [Fact]
    public void HumanFineStepAndAiProductionCommitAtomicallyWithReplay()
    {
        var host = Host();
        var before = host.Snapshot;
        var hash = host.StateHash;
        var worker = before.Workers.Workers.Single(w => w.Id.Value == 1UL);
        var origin = worker.TacticalTileId!.Value;
        var target = Neighbor(host, 1UL);
        host.QueueHumanMove(Human, worker.Id, target);
        Assert.Equal(origin, before.Workers.Workers.Single(w =>
            w.Id == worker.Id).TacticalTileId);
        Assert.Single(host.GetHumanOrders(Human));
        Assert.Equal(hash, host.StateHash);
        host.PrepareAiCivilizations();
        Assert.Same(before, host.Snapshot);
        host.SetHumanReady(Human);
        var receipt = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.Equal(new TurnNumber(1UL), receipt.Turn);
        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.Equal(new ulong[] { 1UL, 4UL },
            receipt.Events.Select(e => e.WorkerId.Value));
        Assert.All(receipt.Events, e =>
            Assert.Equal(new GoldbergParameters(18, 0),
                e.Destination.FineGoldbergParameters));
        Assert.Equal(origin, receipt.Events[0].Source);
        Assert.Equal(target, receipt.Events[0].Destination);
        Assert.NotNull(receipt.Events[0].TraversedEdge);
        Assert.Equal(EventOriginKind.Command, receipt.Events[0].Id.OriginKind);
        Assert.Null(receipt.Events[1].TraversedEdge);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            receipt.Events[1].NextActivity);
        Assert.Equal(target, host.Snapshot.Workers.Workers.Single(w =>
            w.Id == worker.Id).TacticalTileId);
        Assert.Equal(PhysicalWorkerActivity.Traveling,
            host.Snapshot.Workers.Workers.Single(w =>
                w.Id == worker.Id).Activity);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL).Activity);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
        Assert.Equal(2UL, host.Snapshot.Revision);
        Assert.Equal(10U, receipt.Hash.FormatVersion);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        Assert.Empty(host.ReadyCivilizations);
        Assert.Equal(0, host.PendingOrderCount);
        Assert.Equal(hash, new CanonicalWorldStateHasher().Compute(before));
    }

    [Fact]
    public void SubsequentEmptyCollectiveTurnStillAdvancesClockWithoutNewWorker()
    {
        var host = Host();
        host.SetHumanReady(Human);
        var first = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.Single(first.Events); // AI #4 resumes production
        host.SetHumanReady(Human);
        var second = Assert.IsType<FoundationTacticalCollectiveReceipt>(
            host.TryCloseTurn());
        Assert.Empty(second.Events);
        Assert.True(second.ReplayMatched);
        Assert.Equal(new TurnNumber(3UL), host.NextTurn);
        Assert.Equal(first.Revision, second.Revision);
        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
    }

    [Fact]
    public void ReadyHumanMustReopenBeforeEditingAndAiCannotIssueManualOrders()
    {
        var host = Host();
        host.SetHumanReady(Human);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Human, new PhysicalWorkerId(1UL), Neighbor(host, 1UL)));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanActivity(Ai, new PhysicalWorkerId(4UL),
                PhysicalWorkerActivity.Producing));
        Assert.Throws<InvalidOperationException>(() => host.SetHumanReady(Ai));
        Assert.Throws<InvalidOperationException>(() => host.GetHumanOrders(Ai));
        host.ReopenHuman(Human);
        host.QueueHumanMove(Human, new PhysicalWorkerId(1UL), Neighbor(host, 1UL));
        Assert.Single(host.GetHumanOrders(Human));
        Assert.True(host.CancelHumanOrder(Human, new PhysicalWorkerId(1UL)));
        Assert.Empty(host.GetHumanOrders(Human));
    }

    [Fact]
    public void AiPlansOncePerTurnAndCannotPartiallyQueueForeignOrDuplicateWorkers()
    {
        var initial = Host();
        var session = initial.Session;
        var map = initial.Incidence;
        var bad = new FakePolicy((_, state, incidence, _) =>
        {
            var worker = state.Workers.Workers.Single(w => w.Id.Value == 4UL);
            var source = worker.TacticalTileId!.Value;
            var next = incidence.FineTopology.Cells[
                checked((int)source.FineStrategicCellId.Value - 1)]
                .AdjacentCellIds[0];
            return new[]
            {
                new FoundationTacticalAiDecision(worker.Id,
                    new PhysicalTacticalTileId(incidence.Refinement.FineParameters, next),
                    PhysicalWorkerActivity.Traveling, true),
                new FoundationTacticalAiDecision(
                    new PhysicalWorkerId(1UL),
                    state.Workers.Workers.Single(w => w.Id.Value == 1UL)
                        .TacticalTileId!.Value,
                    PhysicalWorkerActivity.Producing, false)
            };
        });
        var host = new FoundationTacticalHumanAiTurnHost(
            session, map, new[] { Human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            { [Ai] = bad });
        var original = host.Snapshot;
        Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
        Assert.Equal(1, bad.Calls);
        Assert.Equal(0, host.PendingOrderCount);
        Assert.Empty(host.ReadyCivilizations);
        Assert.Same(original, host.Snapshot);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);

        var good = new FakePolicy((_, state, _, _) =>
        {
            var worker = state.Workers.Workers.Single(w => w.Id.Value == 4UL);
            return worker.Activity == PhysicalWorkerActivity.Traveling
                ? new[] { new FoundationTacticalAiDecision(
                    worker.Id, worker.TacticalTileId!.Value,
                    PhysicalWorkerActivity.Producing, false) }
                : Array.Empty<FoundationTacticalAiDecision>();
        });
        var proper = new FoundationTacticalHumanAiTurnHost(
            session, map, new[] { Human },
            new Dictionary<CivilizationId, IFoundationTacticalAiTurnPolicy>
            { [Ai] = good });
        proper.PrepareAiCivilizations();
        proper.PrepareAiCivilizations();
        Assert.Null(proper.TryCloseTurn());
        Assert.Equal(1, good.Calls);
        proper.SetHumanReady(Human);
        Assert.NotNull(proper.TryCloseTurn());
        Assert.Equal(1, good.Calls);
        proper.PrepareAiCivilizations();
        Assert.Equal(2, good.Calls);
    }

    [Fact]
    public void NoTeleportCoarseCommandsOrForeignWorldAreAccepted()
    {
        var host = Host();
        var before = host.Snapshot;
        var hash = host.StateHash;
        var worker = before.Workers.Workers.Single(w => w.Id.Value == 1UL);
        var origin = worker.TacticalTileId!.Value;
        var neighboringIds = host.Incidence.FineTopology.Cells[
            checked((int)origin.FineStrategicCellId.Value - 1)]
            .AdjacentCellIds;
        var remote = host.Incidence.TileIncidences.First(tile =>
            tile.PhysicalTacticalTileId != origin &&
            !neighboringIds.Contains(
                tile.PhysicalTacticalTileId.FineStrategicCellId))
            .PhysicalTacticalTileId;
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Human, worker.Id, remote));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Human, new PhysicalWorkerId(4UL), Neighbor(host, 4UL)));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Human, worker.Id, origin));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Human, worker.Id,
                new PhysicalTacticalTileId(new GoldbergParameters(12, 0),
                    new StrategicCellId(1UL))));
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Empty(host.GetHumanOrders(Human));

        host.Session.AcceptSnapshot(before.AdvanceRevision());
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Human, worker.Id, Neighbor(host, 1UL)));
    }

    [Fact]
    public void InitialFinePlacementRejectsMissingForeignOrNonincidentTile()
    {
        var source = FoundationHumanAiLocalPreview.CreateLocalPreview(Request());
        var session = source.Session;
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(
                new GoldbergParameters(3, 0), new GoldbergParameters(18, 0)));
        var original = session.Snapshot;
        var positions = session.Snapshot.Workers.Workers.ToDictionary(
            worker => worker.Id,
            worker => map.TileIncidences.First(tile =>
                tile.IncidentCoarseCellIds.Count == 1 &&
                tile.IncidentCoarseCellIds[0] == worker.CellId)
                .PhysicalTacticalTileId);
        var missing = new Dictionary<PhysicalWorkerId, PhysicalTacticalTileId>(
            positions);
        missing.Remove(new PhysicalWorkerId(6UL));
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTacticalInitialDeployment.Deploy(session, map, missing));
        var invalid = new Dictionary<PhysicalWorkerId, PhysicalTacticalTileId>(
            positions);
        var home = session.Snapshot.Workers.Workers[0];
        invalid[home.Id] = map.TileIncidences.First(tile =>
            !tile.IncidentCoarseCellIds.Contains(home.CellId))
            .PhysicalTacticalTileId;
        Assert.Throws<InvalidOperationException>(() =>
            FoundationTacticalInitialDeployment.Deploy(session, map, invalid));
        Assert.Same(original, session.Snapshot);
        Assert.Equal(9U, new CanonicalWorldStateHasher()
            .Compute(session.Snapshot).FormatVersion);
    }
}
