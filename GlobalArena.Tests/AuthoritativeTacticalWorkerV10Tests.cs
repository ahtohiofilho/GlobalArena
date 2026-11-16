using GlobalArena.Kernel;
using GlobalArena.Runtime;
using GlobalArena.Simulation;
using GlobalArena.Simulation.Foundation;
using GlobalArena.World;

namespace GlobalArena.Tests;

/// <summary>Opt-in v10 domain vertical slice. Existing real game is still
/// v9 until a separately validated fine Human+AI coordinator is connected.</summary>
public sealed class AuthoritativeTacticalWorkerV10Tests
{
    private static (CanonicalGameSession Session, PhysicalTacticalIncidenceMap Map,
        WorldState Original, WorldState Fine) Materialized()
    {
        var preview = FoundationHumanAiLocalPreview.CreateLocalPreview(
            new WorldGenerationRequest(new WorldSeed(20261012UL),
                WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
        var session = preview.Session;
        var map = PhysicalTacticalIncidenceMapper.Materialize(
            new GoldbergScaledRefinement(new GoldbergParameters(3, 0),
                new GoldbergParameters(18, 0)));
        var original = session.Snapshot;
        var workers = original.Workers.Workers.Select(w =>
        {
            // Explicit TEST-SCENARIO placements: each exists and is fully
            // incident to its original coarse region. Production worldgen
            // must supply its own deterministic per-worker positioning.
            var tile = map.TileIncidences.First(incidence =>
                incidence.IncidentCoarseCellIds.Count == 1 &&
                incidence.IncidentCoarseCellIds[0] == w.CellId)
                .PhysicalTacticalTileId;
            return new PhysicalWorkerRuntimeRecord(w.Id, w.Owner, w.CellId,
                w.Commodity, w.OutputMicroPerTurn, w.Activity,
                w.DemandMultiplierMicro, tile);
        }).ToArray();
        var fine = original.DeployTacticalWorkers(
            new PhysicalWorkerRuntimeState(workers, tacticalLocationsAuthoritative: true),
            session.GeneratedWorld, map);
        return (session, map, original, fine);
    }

    private static SimulationContext Context(ulong turn) =>
        new(new TurnNumber(turn), new SimulationSeed(712UL));

    private static PhysicalTacticalWorkerMoveCommand Command(
        ulong turn, ulong sequence, ulong worker, ulong civ,
        PhysicalTacticalTileId target, PhysicalWorkerActivity? activity = null) =>
        new(new CommandId(new TurnNumber(turn), sequence),
            new CivilizationId(civ), new PhysicalWorkerId(worker),
            target, activity);

    private static TurnResolutionResult Resolve(CanonicalGameSession session,
        PhysicalTacticalIncidenceMap map, WorldState initial,
        params ISimulationCommand[] commands) =>
        new PhasedTurnResolutionEngine(new ITurnResolutionPhase[]
        {
            new PhysicalTacticalWorkerMovementTurnPhase(session.GeneratedWorld, map)
        }).Resolve(new TurnResolutionInput(initial, commands, Context(1)));

    [Fact]
    public void FormatV10IsOptInAndPreservesLegacyHashBytes()
    {
        var (session, _, v9, fine) = Materialized();
        var hasher = new CanonicalWorldStateHasher();
        Assert.Equal(9U, hasher.Compute(v9).FormatVersion);
        Assert.Equal(10U, hasher.Compute(fine).FormatVersion);
        Assert.Equal(9U, hasher.Compute(session.Snapshot).FormatVersion);
        Assert.NotEqual(hasher.Compute(v9).HexDigest, hasher.Compute(fine).HexDigest);
        Assert.Equal(v9.Workers.Workers.Select(w => w.Id),
            fine.Workers.Workers.Select(w => w.Id));
        Assert.True(fine.Workers.HasAuthoritativeTacticalLocations);
        Assert.All(fine.Workers.Workers,
            w => Assert.Equal(new GoldbergParameters(18, 0),
                w.TacticalTileId!.Value.FineGoldbergParameters));
        Assert.Equal(0UL, fine.Revision);
    }

    [Fact]
    public void PartialOrImplicitFineDeploymentIsRejected()
    {
        var (session, map, v9, fine) = Materialized();
        var fineWorkers = fine.Workers.Workers.ToArray();
        Assert.Throws<ArgumentException>(() => new PhysicalWorkerRuntimeState(fineWorkers));
        Assert.Throws<ArgumentException>(() => new PhysicalWorkerRuntimeState(
            fineWorkers.Select((w, index) => index == 0
                ? v9.Workers.Workers[0] : w), true));
        Assert.Throws<InvalidOperationException>(() => v9.WithWorkers(fine.Workers));
        Assert.Throws<InvalidOperationException>(() =>
            fine.WithWorkers(v9.Workers));
        Assert.Throws<InvalidOperationException>(() =>
            fine.DeployTacticalWorkers(fine.Workers, session.GeneratedWorld, map));
        Assert.Throws<InvalidOperationException>(() =>
            fineWorkers[0].WithLocationAndActivity(
                fineWorkers[0].CellId, PhysicalWorkerActivity.Traveling));
    }

    [Fact]
    public void FineDeploymentCannotInventWorkersOrRewriteFactsOrCoarseIncidence()
    {
        var (session, map, v9, fine) = Materialized();
        var worker = fine.Workers.Workers[0];
        var changed = fine.Workers.Workers.Select(w => w.Id == worker.Id
            ? new PhysicalWorkerRuntimeRecord(w.Id, w.Owner, w.CellId,
                w.Commodity, w.OutputMicroPerTurn + 1UL, w.Activity,
                w.DemandMultiplierMicro, w.TacticalTileId)
            : w).ToArray();
        Assert.Throws<InvalidOperationException>(() => v9.DeployTacticalWorkers(
            new PhysicalWorkerRuntimeState(changed, true),
            session.GeneratedWorld, map));

        var foreignFine = map.TileIncidences.First(x =>
            !x.IncidentCoarseCellIds.Contains(worker.CellId)).PhysicalTacticalTileId;
        var misplaced = fine.Workers.Workers.Select(w => w.Id == worker.Id
            ? new PhysicalWorkerRuntimeRecord(w.Id, w.Owner, w.CellId,
                w.Commodity, w.OutputMicroPerTurn, w.Activity,
                w.DemandMultiplierMicro, foreignFine)
            : w).ToArray();
        Assert.Throws<InvalidOperationException>(() => v9.DeployTacticalWorkers(
            new PhysicalWorkerRuntimeState(misplaced, true),
            session.GeneratedWorld, map));

        var otherWorld = CanonicalGameSession.Start(new WorldGenerationRequest(
            new WorldSeed(1UL), WorldGenerationVersion.Initial,
            new GoldbergParameters(3, 0)));
        Assert.Throws<InvalidOperationException>(() => v9.DeployTacticalWorkers(
            fine.Workers, otherWorld.GeneratedWorld, map));
        Assert.Equal(9U, new CanonicalWorldStateHasher().Compute(v9).FormatVersion);
    }

    [Fact]
    public void OneRealFineEdgeProducesReplayableV10JournalHash()
    {
        var (session, map, v9, fine) = Materialized();
        var w = fine.Workers.Workers[0];
        var origin = w.TacticalTileId!.Value;
        var dest = new PhysicalTacticalTileId(map.Refinement.FineParameters,
            map.FineTopology.Cells[checked((int)origin.FineStrategicCellId.Value - 1)]
                .AdjacentCellIds[0]);
        var evResult = Resolve(session, map, fine,
            Command(1, 1, w.Id.Value, w.Owner.Value, dest));
        var ev = Assert.IsType<PhysicalTacticalWorkerMovedEvent>(
            Assert.Single(evResult.Events));
        Assert.Equal(origin, ev.Source);
        Assert.Equal(dest, ev.Destination);
        Assert.NotNull(ev.TraversedEdge);
        Assert.Equal(EventOriginKind.Command, ev.Id.OriginKind);
        Assert.Equal(1UL, evResult.ResultingWorldState.Revision);
        Assert.Same(v9, session.Snapshot);
        var moved = evResult.ResultingWorldState.Workers.Workers
            .Single(item => item.Id == w.Id);
        Assert.Equal(dest, moved.TacticalTileId);
        Assert.Equal(PhysicalWorkerActivity.Traveling, moved.Activity);
        Assert.Equal(w.OutputMicroPerTurn, moved.OutputMicroPerTurn);
        Assert.Equal(w.DemandMultiplierMicro, moved.DemandMultiplierMicro);
        Assert.Single(evResult.EventLog.Entries);

        var replayed = new SimulationEventLogReplayer(
            new PhysicalTacticalWorkerMoveEventExecutor(
                session.GeneratedWorld, map)).Replay(
                    new SimulationReplayInput(fine, evResult.EventLog, Context(1)));
        var hash = new CanonicalWorldStateHasher();
        Assert.Equal(hash.Compute(evResult.ResultingWorldState),
            hash.Compute(replayed));
        Assert.Equal(10U, hash.Compute(replayed).FormatVersion);
        Assert.NotEqual(hash.Compute(fine), hash.Compute(replayed));
    }

    [Fact]
    public void FineExecutorRejectsTeleportForeignWorkerAndForgedEdge()
    {
        var (session, map, _, fine) = Materialized();
        var worker = fine.Workers.Workers[0];
        var origin = worker.TacticalTileId!.Value;
        var adjacent = new PhysicalTacticalTileId(map.Refinement.FineParameters,
            map.FineTopology.Cells[checked((int)origin.FineStrategicCellId.Value - 1)]
                .AdjacentCellIds[0]);
        var far = map.TileIncidences.First(i =>
            !i.PhysicalTacticalTileId.Equals(origin) &&
            !map.FineTopology.Cells[
                checked((int)origin.FineStrategicCellId.Value - 1)]
                .AdjacentCellIds.Contains(i.PhysicalTacticalTileId.FineStrategicCellId))
            .PhysicalTacticalTileId;
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, map, fine,
                Command(1, 1, worker.Id.Value, worker.Owner.Value, far)));
        var foreignCiv = fine.Workers.Workers
            .First(w => w.Owner != worker.Owner).Owner;
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, map, fine,
                Command(1, 1, worker.Id.Value, foreignCiv.Value, adjacent)));
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, map, fine,
                Command(1, 1, worker.Id.Value, worker.Owner.Value, adjacent),
                Command(1, 2, worker.Id.Value, worker.Owner.Value, adjacent)));

        var proper = Assert.IsType<PhysicalTacticalWorkerMovedEvent>(
            Assert.Single(Resolve(session, map, fine,
                Command(1, 1, worker.Id.Value, worker.Owner.Value, adjacent)).Events));
        var forged = new PhysicalTacticalWorkerMovedEvent(
            proper.Id, proper.Issuer, proper.WorkerId,
            proper.Source, proper.Destination,
            new PhysicalTacticalEdgeId(map.Refinement.FineParameters,
                new StrategicEdgeId(map.FineTopology.Edges[^1].Id.Value)),
            proper.ResultingPresentationRegion, proper.NextActivity);
        var executor = new PhysicalTacticalWorkerMoveEventExecutor(
            session.GeneratedWorld, map);
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(fine, forged, Context(1)));
        var moved = executor.Execute(fine, proper, Context(1));
        Assert.Throws<InvalidOperationException>(() =>
            executor.Execute(moved, proper, Context(1)));
        Assert.Equal(0UL, fine.Revision);
    }

    [Fact]
    public void V9CoarseCommandsCannotActOnV10Workers()
    {
        var (session, map, _, fine) = Materialized();
        var worker = fine.Workers.Workers[0];
        var neighbor = session.GeneratedWorld.StrategicTopology.Cells[
            checked((int)worker.CellId.Value - 1)].AdjacentCellIds[0];
        var old = new PhysicalWorkerMoveCommand(
            new CommandId(new TurnNumber(1UL), 1UL),
            worker.Owner, worker.Id, neighbor);
        Assert.Throws<InvalidOperationException>(() =>
            new PhysicalWorkerMovementTurnPhase(session.GeneratedWorld).Resolve(
                new TurnResolutionInput(fine, new[] { old }, Context(1)), fine));
        var fineId = worker.TacticalTileId!.Value;
        Assert.Throws<InvalidOperationException>(() =>
            Resolve(session, map, session.Snapshot,
                Command(1, 1, worker.Id.Value, worker.Owner.Value, fineId)));
        session.AcceptSnapshot(fine);
        Assert.Throws<InvalidOperationException>(() =>
            new FoundationCollectiveTurnCoordinator(session));
        Assert.Throws<InvalidOperationException>(() =>
            GoldbergFoundationSession.FromAuthoritativeWorkers(session,
                new Dictionary<int, double> { [1] = 1d }));
    }

    [Fact]
    public void LocalFineActivityChangeKeepsExactTileAndDemandMultiplier()
    {
        var (session, map, _, fine) = Materialized();
        var worker = fine.Workers.Workers.First(
            w => w.Activity == PhysicalWorkerActivity.Producing);
        var command = Command(1, 1, worker.Id.Value, worker.Owner.Value,
            worker.TacticalTileId!.Value, PhysicalWorkerActivity.Other);
        var result = Resolve(session, map, fine, command);
        var ev = Assert.IsType<PhysicalTacticalWorkerMovedEvent>(
            Assert.Single(result.Events));
        Assert.Null(ev.TraversedEdge);
        var next = result.ResultingWorldState.Workers.Workers
            .Single(w => w.Id == worker.Id);
        Assert.Equal(worker.TacticalTileId, next.TacticalTileId);
        Assert.Equal(worker.DemandMultiplierMicro, next.DemandMultiplierMicro);
        Assert.Equal(PhysicalWorkerActivity.Other, next.Activity);
    }
}
