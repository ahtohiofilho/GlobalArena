using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>
/// Physical worker facts are in the real v9 WorldState, movements have
/// command-originated events and replay. The separate economic clearing
/// engine still produces ONLY non-authoritative previews.
/// </summary>
public sealed class AuthoritativePhysicalWorkerJournalTests
{
    private static CanonicalGameSession Session() =>
        CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(20261115UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));

    private static (CanonicalGameSession Session, StrategicCellId A,
        StrategicCellId B, StrategicCellId Far) Materialized()
    {
        var session = Session();
        var graph = session.GeneratedWorld.StrategicSurfaceGraph;
        var a = graph.GetCellId(0);
        var b = graph.GetCellId(graph.GetNeighborIndexes(0)[0]);
        var far = graph.GetCellId(graph.NodeCount / 2);
        var roster = new CivilizationRuntimeState(
            new[] { new CivilizationId(1UL), new CivilizationId(2UL) });
        var workers = new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                new CivilizationId(2UL), far, new CommodityId(2U),
                2_000_000UL, PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                new CivilizationId(1UL), a, new CommodityId(1U),
                1_000_000UL, PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(3UL),
                new CivilizationId(1UL), b, new CommodityId(1U),
                0UL, PhysicalWorkerActivity.Military, 800_000U)
        });
        session.AcceptSnapshot(session.Snapshot.WithCivilizations(roster)
            .WithWorkers(workers));
        return (session, a, b, far);
    }

    private static PhysicalWorkerMoveCommand Move(
        ulong turn, ulong sequence, ulong worker, ulong owner,
        StrategicCellId destination, PhysicalWorkerActivity? next = null) =>
        new(new CommandId(new TurnNumber(turn), sequence),
            new CivilizationId(owner), new PhysicalWorkerId(worker),
            destination, next);

    private static SimulationContext Context(ulong turn) =>
        new(new TurnNumber(turn), new SimulationSeed(712UL));

    private static TurnResolutionResult Resolve(
        CanonicalGameSession session, ulong turn,
        params ISimulationCommand[] orders) =>
        new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
        {
            new PhysicalWorkerMovementTurnPhase(session.GeneratedWorld)
        }).Resolve(new TurnResolutionInput(session.Snapshot, orders,
            Context(turn)));

    [Fact]
    public void LegacyV8HashIsUnchangedAndInitializedWorkerStatesUseV9()
    {
        var old = Session().Snapshot;
        var oldHash = new CanonicalWorldStateHasher().Compute(old);
        Assert.Equal(8U, oldHash.FormatVersion);
        Assert.False(old.Workers.IsInitialized);
        var (session, a, b, _) = Materialized();
        var state = session.Snapshot;
        var version9 = new CanonicalWorldStateHasher().Compute(state);
        Assert.Equal(9U, version9.FormatVersion);
        Assert.NotEqual(oldHash.HexDigest, version9.HexDigest);
        Assert.Equal(new ulong[] { 1, 2, 3 },
            state.Workers.Workers.Select(worker => worker.Id.Value));
        var changed = state.WithWorkers(new PhysicalWorkerRuntimeState(
            state.Workers.Workers.Select(worker => worker.Id.Value == 1
                ? worker.WithLocationAndActivity(b, PhysicalWorkerActivity.Traveling)
                : worker)));
        Assert.NotEqual(version9.HexDigest,
            new CanonicalWorldStateHasher().Compute(changed).HexDigest);
        Assert.True(state.Workers.IsInitialized);
        Assert.Equal(a, state.Workers.Workers.Single(worker => worker.Id.Value == 1).CellId);
    }

    [Fact]
    public void WorkerRecordsAreImmutableCanonicalAndNoDuplicateIdentities()
    {
        var (session, _, _, _) = Materialized();
        var census = session.Snapshot.Workers;
        Assert.Equal(3, census.Workers.Count);
        Assert.True(census.IsInitialized);
        Assert.Throws<ArgumentException>(() => new PhysicalWorkerRuntimeState(
            new[] { census.Workers[0], census.Workers[0] }));
        Assert.Throws<InvalidOperationException>(() =>
            session.Snapshot.WithWorkers(PhysicalWorkerRuntimeState.Uninitialized));
        Assert.Throws<ArgumentException>(() => new PhysicalWorkerRuntimeRecord(
            new PhysicalWorkerId(4UL), new CivilizationId(1UL),
            new StrategicCellId(1UL), new CommodityId(1U),
            1UL, PhysicalWorkerActivity.Producing,
            demandMultiplierMicro: 0));
    }

    [Fact]
    public void WorldAndCivilizationInvariantsRejectInvalidWorkerCensus()
    {
        var (session, _, _, _) = Materialized();
        var state = session.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            state.WithWorkers(new PhysicalWorkerRuntimeState(
                new[] { new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId(9UL), new CivilizationId(3UL),
                    new StrategicCellId(1UL), new CommodityId(1U), 1UL,
                    PhysicalWorkerActivity.Producing) })));
        Assert.Throws<InvalidOperationException>(() =>
            state.WithWorkers(new PhysicalWorkerRuntimeState(
                new[] { new PhysicalWorkerRuntimeRecord(
                    new PhysicalWorkerId(9UL), new CivilizationId(1UL),
                    new StrategicCellId(93UL), new CommodityId(1U), 1UL,
                    PhysicalWorkerActivity.Producing) })));
        Assert.Throws<InvalidOperationException>(() =>
            WorldState.CreateInitial().WithWorkers(
                new PhysicalWorkerRuntimeState(Array.Empty<PhysicalWorkerRuntimeRecord>())));
    }

    [Fact]
    public void WorldStateClonesNeverDropInitializedIndividualWorkers()
    {
        var (session, _, _, _) = Materialized();
        var old = session.Snapshot;
        var next = old.AdvanceRevision()
            .WithEconomy(EconomyRuntimeState.Empty)
            .WithPopulation(PopulationRuntimeState.Uninitialized)
            .WithWarfare(WarfareRuntimeState.Empty);
        Assert.Same(old.Workers, next.Workers);
        Assert.Equal(1UL, next.Revision);
        Assert.Equal(9U, new CanonicalWorldStateHasher().Compute(next).FormatVersion);
        Assert.Equal(0UL, old.Revision);
    }

    [Fact]
    public void RealGoldbergWorkerMovementIsAnExecutedAuthoritativeJournalEvent()
    {
        var (session, a, b, _) = Materialized();
        var first = session.Snapshot;
        var order = Move(1, 1, 1, 1, b);
        var result = Resolve(session, 1, order);
        var ev = Assert.IsType<PhysicalWorkerMovedEvent>(Assert.Single(result.Events));
        Assert.Equal(a, ev.Source);
        Assert.Equal(b, ev.Destination);
        Assert.NotNull(ev.TraversedEdge);
        Assert.Equal(EventOriginKind.Command, ev.Id.OriginKind);
        Assert.Equal(order.Id, ev.Id.OriginCommandId);
        Assert.Single(result.EventLog.Entries);
        Assert.True(result.EventLog.Entries[0].WasExecuted);
        Assert.Equal(1UL, result.ResultingWorldState.Revision);
        var moved = result.ResultingWorldState.Workers.Workers.Single(x => x.Id.Value == 1UL);
        Assert.Equal(b, moved.CellId);
        Assert.Equal(PhysicalWorkerActivity.Traveling, moved.Activity);
        Assert.Equal(1_000_000UL, moved.OutputMicroPerTurn);
        Assert.Same(first, session.Snapshot); // private staging until commit gate
        Assert.Equal(a, first.Workers.Workers.Single(x => x.Id.Value == 1UL).CellId);
    }

    [Fact]
    public void JournalReplayReproducesV9CanonicalHashAndFinalWorkerLocations()
    {
        var (session, _, b, _) = Materialized();
        var initial = session.Snapshot;
        var order = Move(1, 2, 1, 1, b);
        var result = Resolve(session, 1, order);
        var replayed = new SimulationEventLogReplayer(
            new PhysicalWorkerMoveEventExecutor(session.GeneratedWorld))
            .Replay(new SimulationReplayInput(initial, result.EventLog, Context(1)));
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(hasher.Compute(result.ResultingWorldState),
            hasher.Compute(replayed));
        Assert.Equal(result.ResultingWorldState.Workers.Workers
            .Select(x => (x.Id, x.CellId, x.Activity)),
            replayed.Workers.Workers.Select(x => (x.Id, x.CellId, x.Activity)));
    }

    [Fact]
    public void RealTurnCommitGatePublishesExactlyOneWorkerMovement()
    {
        var (session, _, b, _) = Materialized();
        var initial = session.Snapshot;
        var engine = new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
        {
            new PhysicalWorkerMovementTurnPhase(session.GeneratedWorld)
        });
        var gate = new AuthoritativeTurnCommitGate(session,
            new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(), engine),
            new TurnNumber(1UL), 1U);
        var order = Move(1, 1, 1, 1, b);
        var committed = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
            new TurnPolicyInput(new TurnNumber(1UL), true, false),
            new TurnResolutionInput(initial, new[] { order }, Context(1))));
        Assert.True(committed.WasResolved);
        Assert.NotSame(initial, session.Snapshot);
        Assert.Equal(1UL, session.Snapshot.Revision);
        Assert.Equal(b, session.Snapshot.Workers.Workers.Single(x => x.Id.Value == 1).CellId);
        Assert.Equal(new TurnNumber(2UL), gate.NextTurn);
        Assert.Equal(9U, new CanonicalWorldStateHasher()
            .Compute(session.Snapshot).FormatVersion);
        Assert.Throws<InvalidOperationException>(() =>
            gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
                new TurnPolicyInput(new TurnNumber(1UL), true, false),
                new TurnResolutionInput(initial, new[] { order }, Context(1)))));
    }

    [Fact]
    public void ActivityChangeInPlaceIsJournaledAndLeavesPhysicalLocationIntact()
    {
        var (session, _, b, _) = Materialized();
        var result = Resolve(session, 1, Move(1, 1, 3, 1, b,
            PhysicalWorkerActivity.Producing));
        var ev = Assert.IsType<PhysicalWorkerMovedEvent>(Assert.Single(result.Events));
        Assert.Null(ev.TraversedEdge);
        Assert.Equal(b, ev.Source);
        Assert.Equal(b, ev.Destination);
        var worker = result.ResultingWorldState.Workers.Workers
            .Single(w => w.Id.Value == 3UL);
        Assert.Equal(PhysicalWorkerActivity.Producing, worker.Activity);
        Assert.Equal(b, worker.CellId);
        Assert.Equal(800_000U, worker.DemandMultiplierMicro);
    }

    [Fact]
    public void InvalidOwnershipTeleportOrDuplicateWorkerOrderCannotPartiallyCommit()
    {
        var (session, a, b, far) = Materialized();
        var before = session.Snapshot;
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, 1, Move(1, 1, 1, 2, b)));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, 1, Move(1, 1, 1, 1, far)));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, 1, Move(1, 1, 1, 1, b),
                Move(1, 2, 1, 1, a)));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, 1, Move(1, 1, 1, 1, a)));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, 2, Move(1, 1, 1, 1, b)));
        Assert.Same(before, session.Snapshot);
        Assert.Equal(0UL, session.Snapshot.Revision);
        Assert.Equal(a, session.Snapshot.Workers.Workers[0].CellId);
    }

    [Fact]
    public void ForgedJournalEdgeAndStaleSourceAreRejectedOnReplay()
    {
        var (session, a, b, _) = Materialized();
        var bad = new PhysicalWorkerMovedEvent(
            new EventId(new CommandId(new TurnNumber(1UL), 1UL), 1UL),
            new CivilizationId(1UL), new PhysicalWorkerId(1UL),
            a, b, new StrategicEdgeId(270UL), PhysicalWorkerActivity.Traveling);
        var executor = new PhysicalWorkerMoveEventExecutor(session.GeneratedWorld);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(session.Snapshot, bad, Context(1)));

        var actual = Assert.IsType<PhysicalWorkerMovedEvent>(Assert.Single(
            Resolve(session, 1, Move(1, 1, 1, 1, b)).Events));
        var advanced = executor.Execute(session.Snapshot, actual, Context(1));
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(advanced, actual, Context(1)));
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(session.Snapshot, actual, Context(2)));
    }

    [Fact]
    public void DifferentMovementCommandsProduceDifferentCanonicalWorkerHashes()
    {
        var (session, _, b, _) = Materialized();
        var moving = Resolve(session, 1, Move(1, 1, 1, 1, b))
            .ResultingWorldState;
        var working = Resolve(session, 1,
            Move(1, 1, 3, 1, b, PhysicalWorkerActivity.Producing))
            .ResultingWorldState;
        Assert.NotEqual(new CanonicalWorldStateHasher().Compute(moving),
            new CanonicalWorldStateHasher().Compute(working));
    }

    [Fact]
    public void PreviewReadsNewAuthoritativeWorkersAndRejectsStaleOverlayAfterCommit()
    {
        var (session, a, b, _) = Materialized();
        var demand = new Dictionary<int, double> { [1] = 1d, [2] = 0.5d };
        var preview = GoldbergFoundationSession.FromAuthoritativeWorkers(
            session, demand, maximumAdjustmentPasses: 3);
        Assert.Equal(3, preview.Workers.Count);
        Assert.Equal(1, preview.Workers.Single(w => w.Id == 1).Tile);
        var initial = preview.Advance();
        Assert.Equal(4.2d, initial.Demand.Values.Sum(), 9);
        Assert.Equal(1.0d + 1.0d + 0.8d, initial.Demand
            .Where(x => x.Key.Commodity == 1).Sum(x => x.Value), 9);
        var movedState = Resolve(session, 1, Move(1, 1, 1, 1, b))
            .ResultingWorldState;
        session.AcceptSnapshot(movedState);
        Assert.Throws<InvalidOperationException>(() => preview.Advance());

        var refreshed = GoldbergFoundationSession.FromAuthoritativeWorkers(
            session, demand, maximumAdjustmentPasses: 3);
        Assert.Equal((int)b.Value, refreshed.Workers.Single(w => w.Id == 1).Tile);
        var tick = refreshed.Advance();
        Assert.Equal(2.8d, tick.Demand.Where(x => x.Key.Commodity == 1)
            .Sum(x => x.Value), 9);
        Assert.Equal(2.8d, tick.Demand
            .Where(x => x.Key.Commodity == 1).Sum(x => x.Value), 9);
        Assert.Equal(4.2d, tick.Demand.Values.Sum(), 9);
        Assert.Equal(a, session.GeneratedWorld.StrategicSurfaceGraph.GetCellId(0));
    }
}
