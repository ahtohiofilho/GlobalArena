using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

public sealed class FoundationHumanAiTurnHostTests
{
    private static readonly CivilizationId Human = new(1UL);
    private static readonly CivilizationId Ai = new(2UL);

    private static CanonicalGameSession NewSession(bool startAiTraveling = false)
    {
        var preview = FoundationLocalGameHost.CreateLocalPreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var session = preview.Session;
        if (startAiTraveling)
        {
            var census = new PhysicalWorkerRuntimeState(
                session.Snapshot.Workers.Workers.Select(worker =>
                    worker.Id.Value == 4UL
                        ? worker.WithLocationAndActivity(worker.CellId,
                            PhysicalWorkerActivity.Traveling)
                        : worker));
            session.AcceptSnapshot(session.Snapshot.WithWorkers(census));
        }
        return session;
    }

    private static FoundationHumanAiTurnHost Host(
        IFoundationAiTurnPolicy? policy = null,
        bool aiTraveling = false,
        CanonicalGameSession? session = null)
    {
        return new FoundationHumanAiTurnHost(
            session ?? NewSession(aiTraveling),
            new[] { Human },
            new Dictionary<CivilizationId, IFoundationAiTurnPolicy>
            {
                [Ai] = policy ?? new FoundationAiHoldPositionPolicy()
            });
    }

    private static StrategicCellId Neighbor(
        FoundationHumanAiTurnHost host, ulong workerId)
    {
        var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
        var w = host.Snapshot.Workers.Workers.Single(x => x.Id.Value == workerId);
        return graph.GetCellId(graph.GetNeighborIndexes(
            graph.GetNodeIndex(w.CellId))[0]);
    }

    private sealed class FakePolicy : IFoundationAiTurnPolicy
    {
        private readonly Func<CivilizationId, WorldState, WorldGenerationResult,
            TurnNumber, IReadOnlyList<FoundationAiWorkerDecision>> _plan;
        public int Calls { get; private set; }

        public FakePolicy(Func<CivilizationId, WorldState, WorldGenerationResult,
            TurnNumber, IReadOnlyList<FoundationAiWorkerDecision>> plan) =>
            _plan = plan;

        public IReadOnlyList<FoundationAiWorkerDecision> Plan(
            CivilizationId civ, WorldState state, WorldGenerationResult world,
            TurnNumber turn)
        {
            Calls++;
            return _plan(civ, state, world, turn);
        }
    }

    [Fact]
    public void EveryCivilizationMustReceiveExactlyOneExplicitRole()
    {
        var session = NewSession();
        Assert.Throws<ArgumentException>(() =>
            new FoundationHumanAiTurnHost(session,
                new[] { Human }, new Dictionary<CivilizationId, IFoundationAiTurnPolicy>()));
        Assert.Throws<ArgumentException>(() =>
            new FoundationHumanAiTurnHost(session,
                new[] { Human, Human },
                new Dictionary<CivilizationId, IFoundationAiTurnPolicy>
                { [Ai] = new FoundationAiHoldPositionPolicy() }));
        Assert.Throws<ArgumentException>(() =>
            new FoundationHumanAiTurnHost(session,
                new[] { Human, Ai },
                new Dictionary<CivilizationId, IFoundationAiTurnPolicy>
                { [Ai] = new FoundationAiHoldPositionPolicy() }));
        Assert.Throws<ArgumentException>(() =>
            new FoundationHumanAiTurnHost(session,
                new[] { Human },
                new Dictionary<CivilizationId, IFoundationAiTurnPolicy>
                {
                    [Ai] = new FoundationAiHoldPositionPolicy(),
                    [new CivilizationId(3UL)] = new FoundationAiHoldPositionPolicy()
                }));
    }

    [Fact]
    public void AiHoldingDoesNotAdvanceWhenHumanHasNotConfirmed()
    {
        var host = Host();
        var initial = host.Snapshot;
        var hash = host.StateHash;
        host.PrepareAiCivilizations();
        Assert.Equal(new ulong[] { 2UL },
            host.ReadyCivilizations.Select(c => c.Value));
        Assert.False(host.AllRequiredReady);
        Assert.Null(host.TryCloseTurn());
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Equal(0UL, host.Snapshot.Revision);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
        Assert.Empty(host.GetHumanOrders(Human));
    }

    [Fact]
    public void OneHumanMoveAndAiHoldCloseAsSingleCanonicalTurn()
    {
        var host = Host();
        var before = host.StateHash;
        var next = Neighbor(host, 1UL);
        host.QueueHumanMove(Human, new PhysicalWorkerId(1UL), next);
        host.PrepareAiCivilizations();
        Assert.Single(host.GetHumanOrders(Human));
        Assert.Equal(before, host.StateHash);
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(new TurnNumber(1UL), receipt.Turn);
        Assert.Single(receipt.Events);
        Assert.Equal(Human, receipt.Events[0].Issuer);
        Assert.Equal(next, host.Snapshot.Workers.Workers
            .Single(w => w.Id.Value == 1UL).CellId);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        Assert.Empty(host.ReadyCivilizations);
        Assert.Equal(1UL, host.Snapshot.Revision);
        Assert.NotEqual(before, receipt.Hash);
        Assert.Equal(receipt.Hash, host.StateHash);
    }

    [Fact]
    public void AiResumeProductionIsOneRealActivityEventAlongsideHumanMove()
    {
        var host = Host(new FoundationAiResumeProductionPolicy(), aiTraveling: true);
        var initial = host.Snapshot;
        var next = Neighbor(host, 1UL);
        host.QueueHumanMove(Human, new PhysicalWorkerId(1UL), next);
        host.PrepareAiCivilizations();
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(0UL, host.Snapshot.Revision);
        Assert.Equal(new ulong[] { 2UL }, host.ReadyCivilizations.Select(x => x.Value));
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Equal(2, receipt.ExecutedEvents);
        Assert.Equal(new ulong[] { 1UL, 4UL },
            receipt.Events.Select(x => x.WorkerId.Value));
        Assert.Equal(new ulong[] { 1UL, 2UL },
            receipt.Events.Select(x => x.Issuer.Value));
        Assert.True(receipt.Events[0].TraversedEdge.HasValue);
        Assert.Null(receipt.Events[1].TraversedEdge);
        Assert.Equal(PhysicalWorkerActivity.Producing,
            host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL).Activity);
        Assert.Equal(2UL, host.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        Assert.True(receipt.ReplayMatched);
        Assert.Equal(receipt.Hash, host.StateHash);
        Assert.Equal(6, host.Snapshot.Workers.Workers.Count);
    }

    [Fact]
    public void AiPolicyOnlyRunsOncePerTurnDespiteMultiplePollingCalls()
    {
        var policy = new FakePolicy((_, _, _, _) =>
            Array.Empty<FoundationAiWorkerDecision>());
        var host = Host(policy);
        host.PrepareAiCivilizations();
        host.PrepareAiCivilizations();
        Assert.Null(host.TryCloseTurn());
        Assert.Equal(1, policy.Calls);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
        host.SetHumanReady(Human);
        Assert.NotNull(host.TryCloseTurn());
        Assert.Equal(1, policy.Calls);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        host.PrepareAiCivilizations();
        Assert.Equal(2, policy.Calls);
    }

    [Fact]
    public void AReadyHumanCannotQueueUntilReopeningAndAiCannotUseHumanMethods()
    {
        var host = Host();
        host.SetHumanReady(Human);
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Human, new PhysicalWorkerId(1UL),
                Neighbor(host, 1UL)));
        Assert.Throws<InvalidOperationException>(() =>
            host.QueueHumanMove(Ai, new PhysicalWorkerId(4UL),
                Neighbor(host, 4UL)));
        Assert.Throws<InvalidOperationException>(() =>
            host.SetHumanReady(Ai));
        host.ReopenHuman(Human);
        host.QueueHumanMove(Human, new PhysicalWorkerId(1UL),
            Neighbor(host, 1UL));
        Assert.Single(host.GetHumanOrders(Human));
        Assert.Throws<InvalidOperationException>(() =>
            host.GetHumanOrders(Ai));
    }

    [Fact]
    public void InvalidAiMoveRejectedWithoutAnyPartialAiOrderOrWorkerMutation()
    {
        var policy = new FakePolicy((_, snapshot, world, _) =>
        {
            var aiWorker = snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL);
            var graph = world.StrategicSurfaceGraph;
            var neighbor = graph.GetCellId(graph.GetNeighborIndexes(
                graph.GetNodeIndex(aiWorker.CellId))[0]);
            return new[]
            {
                new FoundationAiWorkerDecision(aiWorker.Id, neighbor,
                    PhysicalWorkerActivity.Traveling, true),
                new FoundationAiWorkerDecision(new PhysicalWorkerId(1UL),
                    new StrategicCellId(1UL),
                    PhysicalWorkerActivity.Producing, false)
            };
        });
        var host = Host(policy);
        var initial = host.Snapshot;
        var hash = host.StateHash;
        host.QueueHumanMove(Human, new PhysicalWorkerId(1UL),
            Neighbor(host, 1UL));
        Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
        Assert.Equal(0UL, host.Snapshot.Revision);
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(hash, host.StateHash);
        Assert.Empty(host.ReadyCivilizations);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
        Assert.Single(host.GetHumanOrders(Human));
    }

    [Fact]
    public void DuplicateAiWorkerProposalIsRejectedBeforeAnyTurnCommit()
    {
        var policy = new FakePolicy((_, snapshot, world, _) =>
        {
            var worker = snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL);
            var graph = world.StrategicSurfaceGraph;
            var next = graph.GetCellId(graph.GetNeighborIndexes(
                graph.GetNodeIndex(worker.CellId))[0]);
            return new[]
            {
                new FoundationAiWorkerDecision(worker.Id, next,
                    PhysicalWorkerActivity.Traveling, true),
                new FoundationAiWorkerDecision(worker.Id, next,
                    PhysicalWorkerActivity.Traveling, true)
            };
        });
        var host = Host(policy);
        var initial = host.Snapshot;
        Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
        Assert.Empty(host.ReadyCivilizations);
        Assert.Same(initial, host.Snapshot);
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
    }

    [Fact]
    public void AiPolicyCannotPublishAStaleStateBehindHumanDecisions()
    {
        var session = NewSession();
        var host = Host(new FoundationAiHoldPositionPolicy(), session: session);
        session.AcceptSnapshot(session.Snapshot.AdvanceRevision());
        Assert.Throws<InvalidOperationException>(() => host.PrepareAiCivilizations());
        Assert.Throws<InvalidOperationException>(() => host.TryCloseTurn());
        Assert.Equal(new TurnNumber(1UL), host.NextTurn);
        Assert.Equal(1UL, host.Snapshot.Revision);
        Assert.Empty(host.ReadyCivilizations);
    }

    [Fact]
    public void AiPolicyConstructorCopiesMutableRoleDictionary()
    {
        var source = new Dictionary<CivilizationId, IFoundationAiTurnPolicy>
        { [Ai] = new FoundationAiHoldPositionPolicy() };
        var host = new FoundationHumanAiTurnHost(
            NewSession(), new[] { Human }, source);
        source.Clear();
        host.PrepareAiCivilizations();
        Assert.Equal(new ulong[] { 2UL },
            host.ReadyCivilizations.Select(x => x.Value));
    }

    [Fact]
    public void EmptyHumanAndAiTurnAdvancesOnlyClockAndDoesNotCreateWork()
    {
        var host = Host();
        var before = host.Snapshot;
        var hash = host.StateHash;
        host.SetHumanReady(Human);
        var first = host.TryCloseTurn();
        Assert.NotNull(first);
        Assert.Equal(0, first.ExecutedEvents);
        Assert.Same(before, host.Snapshot);
        Assert.Equal(hash, first.Hash);
        Assert.Equal(0UL, host.Snapshot.Revision);
        Assert.Equal(new TurnNumber(2UL), host.NextTurn);
        Assert.Empty(host.ReadyCivilizations);
        host.SetHumanReady(Human);
        Assert.NotNull(host.TryCloseTurn());
        Assert.Equal(new TurnNumber(3UL), host.NextTurn);
    }

    [Fact]
    public void AiResumeProductionDoesNotManufactureProductivityForMilitaryWorkers()
    {
        var host = Host(new FoundationAiResumeProductionPolicy());
        host.PrepareAiCivilizations();
        host.SetHumanReady(Human);
        var receipt = host.TryCloseTurn();
        Assert.NotNull(receipt);
        Assert.Empty(receipt.Events);
        Assert.Equal(0UL, receipt.Revision);
        Assert.Equal(PhysicalWorkerActivity.Military,
            host.Snapshot.Workers.Workers.Single(w => w.Id.Value == 6UL).Activity);
    }

    [Fact]
    public void TwoIndependentSessionsReturnIdenticalAiAndHumanCommitHashes()
    {
        var a = Host(new FoundationAiResumeProductionPolicy(), aiTraveling: true);
        var b = Host(new FoundationAiResumeProductionPolicy(), aiTraveling: true);
        a.QueueHumanMove(Human, new PhysicalWorkerId(1UL), Neighbor(a, 1UL));
        a.PrepareAiCivilizations();
        a.SetHumanReady(Human);
        var left = a.TryCloseTurn();

        b.QueueHumanMove(Human, new PhysicalWorkerId(1UL), Neighbor(b, 1UL));
        b.SetHumanReady(Human);
        var right = b.TryCloseTurn();
        Assert.NotNull(left);
        Assert.NotNull(right);
        Assert.Equal(left.Hash, right.Hash);
        Assert.Equal(left.Events, right.Events);
        Assert.Equal(left.Revision, right.Revision);
    }
}
