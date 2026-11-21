using System.Globalization;
using GlobalArena.Simulation.Foundation;
using GlobalArena.Runtime;
using GlobalArena.World;
using GlobalArena.Kernel;
using GlobalArena.Simulation;

// A real executable headless slice of the redesigned foundation. The legacy
// M4 simulator and Godot UI remain untouched, and this is not authoritative.
if (args.Length != 1 ||
    (args[0] != "--foundation-slice" &&
     args[0] != "--foundation-calibration-ab" &&
     args[0] != "--foundation-goldberg" &&
     args[0] != "--foundation-worker-journal" &&
     args[0] != "--foundation-local-game" &&
     args[0] != "--foundation-batched-turn" &&
     args[0] != "--foundation-collective-turn" &&
     args[0] != "--foundation-human-ai-turn" &&
     args[0] != "--foundation-crossing-depth-report" &&
     args[0] != "--foundation-shallow-depth-routing-ab"))
{
    Console.WriteLine("Global Arena — executable foundation preview");
    Console.WriteLine("Run: dotnet run --project GlobalArena.Console -- --foundation-slice");
    Console.WriteLine("A/B: dotnet run --project GlobalArena.Console -- --foundation-calibration-ab");
    Console.WriteLine("Goldberg: dotnet run --project GlobalArena.Console -- --foundation-goldberg");
    Console.WriteLine("Authority: dotnet run --project GlobalArena.Console -- --foundation-worker-journal");
    Console.WriteLine("Local game: dotnet run --project GlobalArena.Console -- --foundation-local-game");
    Console.WriteLine("Batch turn: dotnet run --project GlobalArena.Console -- --foundation-batched-turn");
    Console.WriteLine("Collective turn: dotnet run --project GlobalArena.Console -- --foundation-collective-turn");
    Console.WriteLine("Human + AI turn: dotnet run --project GlobalArena.Console -- --foundation-human-ai-turn");
    Console.WriteLine("R118 depth-only QA: dotnet run --project GlobalArena.Console -- --foundation-crossing-depth-report");
    Console.WriteLine("R119 shallow-depth route A/B: dotnet run --project GlobalArena.Console -- --foundation-shallow-depth-routing-ab");
    return;
}

// Isolated R119 weighted graph A/B; never settles real economic turns.
if (args[0] == "--foundation-shallow-depth-routing-ab")
{
    FoundationShallowDepthRoutingAb.Run();
    return;
}

// Isolated R118 physical-depth diagnostics; does not create a market turn.
if (args[0] == "--foundation-crossing-depth-report")
{
    FoundationCrossingDepthReport.Run();
    return;
}

// Real Goldberg single-computer Human + AI demonstration. Civilization #2
// has a worker already traveling in the INITIALIZED scenario; a deliberately
// narrow AI policy resumes production at that same node. This is NOT an
// autonomous geopolitical or economic strategy.
if (args[0] == "--foundation-human-ai-turn")
{
    var local = FoundationLocalGameHost.CreateLocalPreview(
        new WorldGenerationRequest(new WorldSeed(20261012UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
    var canonical = local.Session;
    var census = new PhysicalWorkerRuntimeState(
        canonical.Snapshot.Workers.Workers.Select(worker =>
            worker.Id.Value == 4UL
                ? worker.WithLocationAndActivity(worker.CellId,
                    PhysicalWorkerActivity.Traveling)
                : worker));
    canonical.AcceptSnapshot(canonical.Snapshot.WithWorkers(census));

    var human = new CivilizationId(1UL);
    var ai = new CivilizationId(2UL);
    var game = new FoundationHumanAiTurnHost(
        canonical, new[] { human },
        new Dictionary<CivilizationId, IFoundationAiTurnPolicy>
        {
            [ai] = new FoundationAiResumeProductionPolicy()
        });
    var graph = game.Session.GeneratedWorld.StrategicSurfaceGraph;
    var worker = game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL);
    var target = graph.GetCellId(graph.GetNeighborIndexes(
        graph.GetNodeIndex(worker.CellId))[0]);
    var before = game.Snapshot;
    var oldHash = game.StateHash;
    game.QueueHumanMove(human, worker.Id, target);
    if (game.TryCloseTurn() is not null ||
        game.Snapshot.Revision != 0UL ||
        !ReferenceEquals(before, game.Snapshot) ||
        oldHash != game.StateHash)
        throw new InvalidOperationException(
            "AI readiness bypassed human turn decision or changed authority.");
    game.SetHumanReady(human);
    var closed = game.TryCloseTurn();
    if (closed is null || closed.ExecutedEvents != 2 ||
        closed.Events[0].Issuer != human ||
        closed.Events[1].Issuer != ai ||
        closed.Turn.Value != 1UL ||
        closed.Revision != 2UL ||
        game.NextTurn.Value != 2UL ||
        game.Snapshot.Workers.Workers.Count != 6 ||
        game.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL)
            .Activity != PhysicalWorkerActivity.Producing ||
        !closed.ReplayMatched ||
        closed.Hash.FormatVersion != 9U)
        throw new InvalidOperationException(
            "Human+AI collective turn did not commit both worker activities.");

    Console.WriteLine($"GA_FOUNDATION_HUMAN_AI_V1 " +
        $"humans={game.HumanCivilizations.Count} ais={game.AiCivilizations.Count} " +
        $"turn={closed.Turn.Value} events={closed.ExecutedEvents} " +
        $"revision={closed.Revision} workers={game.Snapshot.Workers.Workers.Count} " +
        $"replay_match={closed.ReplayMatched} hash_v={closed.Hash.FormatVersion} " +
        $"state_hash={closed.Hash.HexDigest}");
    foreach (var action in closed.Events)
        Console.WriteLine($"  civ={action.Issuer.Value} worker={action.WorkerId.Value} " +
            $"from={action.Source.Value} to={action.Destination.Value} " +
            $"activity={action.Activity} edge={action.TraversedEdge?.Value}");

    game.SetHumanReady(human);
    var empty = game.TryCloseTurn();
    if (empty is null || empty.ExecutedEvents != 0 ||
        empty.Revision != closed.Revision ||
        game.NextTurn.Value != 3UL)
        throw new InvalidOperationException(
            "Human+AI empty turn incorrectly created worker events.");
    Console.WriteLine($"  empty_turn={empty.Turn.Value} " +
        $"events={empty.ExecutedEvents} revision={empty.Revision} " +
        $"replay_match={empty.ReplayMatched}");
    Console.WriteLine("  ai_policy=resume_existing_production " +
        "human_ready_required=true multiplayer=false combat=false " +
        "economic_settlement=false certified_F072=false");
    return;
}

// Two different civilizations deliberately plan on the SAME canonical world.
// No client can close this collective turn until every required owner is
// ready; the session then receives one journaled and replay-verified snapshot.
if (args[0] == "--foundation-collective-turn")
{
    var start = FoundationLocalGameHost.CreateLocalPreview(
        new WorldGenerationRequest(new WorldSeed(20261012UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
    var collective = new FoundationCollectiveTurnCoordinator(start.Session);
    var graph = collective.Session.GeneratedWorld.StrategicSurfaceGraph;
    var first = collective.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL);
    var fourth = collective.Snapshot.Workers.Workers.Single(w => w.Id.Value == 4UL);
    var firstNeighbor = graph.GetCellId(graph.GetNeighborIndexes(
        graph.GetNodeIndex(first.CellId))[0]);
    var fourthNeighbor = graph.GetCellId(graph.GetNeighborIndexes(
        graph.GetNodeIndex(fourth.CellId))[0]);
    var initialSnapshot = collective.Snapshot;
    var initialHash = collective.StateHash;

    collective.QueueMove(new CivilizationId(2UL), fourth.Id, fourthNeighbor);
    collective.QueueMove(new CivilizationId(1UL), first.Id, firstNeighbor);
    collective.SetReady(new CivilizationId(2UL));
    if (collective.TryCloseReadyTurn() is not null ||
        !ReferenceEquals(initialSnapshot, collective.Snapshot) ||
        collective.StateHash != initialHash ||
        collective.NextTurn.Value != 1UL)
        throw new InvalidOperationException(
            "A single ready civilization incorrectly advanced collective time.");

    collective.SetReady(new CivilizationId(1UL));
    var closed = collective.CloseReadyTurn();
    if (closed.Turn.Value != 1UL ||
        closed.ExecutedEvents != 2 || closed.Revision != 2UL ||
        !closed.ReplayMatched ||
        collective.NextTurn.Value != 2UL ||
        collective.Snapshot.Workers.Workers.Count != 6 ||
        collective.PendingOrderCount != 0 ||
        collective.ReadyCivilizations.Count != 0)
        throw new InvalidOperationException(
            "Two-civilization collective journal or resolution failed.");

    Console.WriteLine($"GA_FOUNDATION_COLLECTIVE_V1 " +
        $"participants={collective.Participants.Count} " +
        $"world_nodes={graph.NodeCount} " +
        $"turn={closed.Turn.Value} events={closed.ExecutedEvents} " +
        $"revision={closed.Revision} workers={collective.Snapshot.Workers.Workers.Count} " +
        $"replay_match={closed.ReplayMatched} hash_v={closed.Hash.FormatVersion} " +
        $"state_hash={closed.Hash.HexDigest}");
    foreach (var ev in closed.Events)
        Console.WriteLine($"  civ={ev.Issuer.Value} worker={ev.WorkerId.Value} " +
            $"from={ev.Source.Value} to={ev.Destination.Value} " +
            $"activity={ev.Activity} edge={ev.TraversedEdge?.Value}");

    collective.SetReady(new CivilizationId(1UL));
    if (collective.TryCloseReadyTurn() is not null)
        throw new InvalidOperationException("Unready civilization was skipped.");
    collective.SetReady(new CivilizationId(2UL));
    var idle = collective.CloseReadyTurn();
    if (idle.ExecutedEvents != 0 || idle.Turn.Value != 2UL ||
        idle.Revision != closed.Revision ||
        collective.NextTurn.Value != 3UL)
        throw new InvalidOperationException("Empty collective turn changed workers.");
    Console.WriteLine($"  idle_collective_turn={idle.Turn.Value} " +
        $"events={idle.ExecutedEvents} revision={idle.Revision} replay_match={idle.ReplayMatched}");
    Console.WriteLine("  authority=worldstate_v9 synchronized_civ_readiness=true " +
        "single_publication_per_turn=true multiplayer_network=false " +
        "combat=false economic_settlement=false certified_F072=false");
    return;
}

// One closed turn with two independently queued worker orders, followed by
// an empty turn and a mixed activity/movement batch. Identical host to Godot.
if (args[0] == "--foundation-batched-turn")
{
    var host = FoundationLocalGameHost.CreateLocalPreview(
        new WorldGenerationRequest(new WorldSeed(20261012UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
    var graph = host.Session.GeneratedWorld.StrategicSurfaceGraph;
    var source = host.ControllableWorkers.Single(w => w.Id.Value == 1UL).CellId;
    var next = graph.GetCellId(graph.GetNeighborIndexes(
        graph.GetNodeIndex(source))[0]);
    var before = host.StateHash;
    var original = host.Snapshot;
    host.QueueMove(new PhysicalWorkerId(2UL), next);
    host.QueueMove(new PhysicalWorkerId(1UL), next);
    if (host.PendingOrderCount != 2 || !ReferenceEquals(original, host.Snapshot) ||
        before != host.StateHash)
        throw new InvalidOperationException("Queue changed a canonical worker state.");
    var first = host.EndTurn();
    if (first.Turn.Value != 1UL || first.ExecutedEvents != 2 ||
        host.NextTurn.Value != 2UL || host.Snapshot.Revision != 2UL ||
        host.PendingOrderCount != 0 || !first.ReplayMatched)
        throw new InvalidOperationException("Multi-worker first turn was not atomic.");
    Console.WriteLine($"GA_FOUNDATION_BATCHED_TURN_V1 turn={first.Turn.Value} " +
        $"events={first.ExecutedEvents} revision={first.ResultingRevision} " +
        $"workers={host.Snapshot.Workers.Workers.Count} " +
        $"queued_after_commit={host.PendingOrderCount} replay_match={first.ReplayMatched} " +
        $"hash={first.ResultingHash.HexDigest}");

    var idle = host.EndTurn();
    if (idle.Turn.Value != 2UL || idle.ExecutedEvents != 0 ||
        host.Snapshot.Revision != 2UL || !idle.ReplayMatched)
        throw new InvalidOperationException("Empty turn invented an event.");
    Console.WriteLine($"  idle_turn={idle.Turn.Value} events={idle.ExecutedEvents} " +
        $"revision={idle.ResultingRevision} replay_match={idle.ReplayMatched}");

    host.QueueActivity(new PhysicalWorkerId(1UL), PhysicalWorkerActivity.Producing);
    host.QueueMove(new PhysicalWorkerId(2UL), source);
    var third = host.EndTurn();
    if (third.Turn.Value != 3UL || third.ExecutedEvents != 2 ||
        host.NextTurn.Value != 4UL || host.Snapshot.Revision != 4UL ||
        !third.ReplayMatched)
        throw new InvalidOperationException("Mixed activity/move turn was not committed.");
    Console.WriteLine($"  mixed_turn={third.Turn.Value} events={third.ExecutedEvents} " +
        $"revision={third.ResultingRevision} replay_match={third.ReplayMatched} " +
        $"hash={third.ResultingHash.HexDigest}");
    Console.WriteLine("  authority=worldstate_v9 batches=3 executed_events=4 " +
        "elapsed_game_turns=3 no_production_settlement=true certified_F072=false");
    return;
}

// Same gameplay host that the Godot Main uses: explicit small local scenario,
// live canonical workers and journaled G/P-style commands. No financial
// settlement, combat, AI, simultaneous multiplayer or price certification.
if (args[0] == "--foundation-local-game")
{
    var local = FoundationLocalGameHost.CreateLocalPreview(
        new WorldGenerationRequest(new WorldSeed(20261012UL),
            WorldGenerationVersion.Initial, new GoldbergParameters(3, 0)));
    var graph = local.Session.GeneratedWorld.StrategicSurfaceGraph;
    var worker = local.ControllableWorkers.Single(w => w.Id.Value == 1UL);
    var initialCell = worker.CellId;
    var nextCell = graph.GetCellId(
        graph.GetNeighborIndexes(graph.GetNodeIndex(initialCell))[0]);
    var initialHash = local.StateHash;
    Console.WriteLine($"GA_FOUNDATION_LOCAL_GAME_V1 nodes={graph.NodeCount} " +
        $"edges={local.Session.GeneratedWorld.StrategicTopology.Edges.Count} " +
        $"workers={local.Snapshot.Workers.Workers.Count} " +
        $"player_civ={local.PlayerCivilization.Value} " +
        $"initial_hash_v={initialHash.FormatVersion} " +
        "preview_scenario=true final_world_population_policy=false");
    var move = local.MoveWorker(worker.Id, nextCell);
    var produce = local.ChangeWorkerActivity(worker.Id,
        PhysicalWorkerActivity.Producing);
    var returnHome = local.MoveWorker(worker.Id, initialCell);
    foreach (var receipt in new[] { move, produce, returnHome })
    {
        if (!receipt.ReplayMatched || receipt.ExecutedEvents != 1 ||
            receipt.ResultingHash.FormatVersion != 9U)
            throw new InvalidOperationException("Local gameplay journal failed.");
        Console.WriteLine($"  local_turn={receipt.Turn.Value} " +
            $"worker={receipt.WorkerId.Value} from={receipt.Source.Value} " +
            $"to={receipt.Destination.Value} activity={receipt.Activity} " +
            $"revision={receipt.ResultingRevision} replay_match={receipt.ReplayMatched} " +
            $"state_hash={receipt.ResultingHash.HexDigest}");
    }
    if (local.NextTurn != new TurnNumber(4UL) ||
        local.Snapshot.Workers.Workers.Count != 6 ||
        local.Snapshot.Workers.Workers.Single(w => w.Id == worker.Id)
            .CellId != initialCell ||
        local.StateHash == initialHash)
        throw new InvalidOperationException(
            "Local gameplay state failed to commit three distinct turns.");
    Console.WriteLine("  authority=worldstate_v9 ui_intent=worker_order " +
        "financial_settlement=NOT_IMPLEMENTED " +
        "combat=NOT_IMPLEMENTED certified_F072=false");
    return;
}

// Authoritative worker lifecycle: v9 physical census, real Goldberg move,
// domain event, turn commit, replay SHA256 equality and read-only economy.
if (args[0] == "--foundation-worker-journal")
{
    var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
        new WorldSeed(20261115UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0)));
    var graph = canonical.GeneratedWorld.StrategicSurfaceGraph;
    var start = graph.GetCellId(0);
    var neighbor = graph.GetCellId(graph.GetNeighborIndexes(0)[0]);
    var remote = graph.GetCellId(graph.NodeCount / 2);

    canonical.AcceptSnapshot(canonical.Snapshot
        .WithCivilizations(new CivilizationRuntimeState(new[]
        {
            new CivilizationId(1UL), new CivilizationId(2UL)
        }))
        .WithWorkers(new PhysicalWorkerRuntimeState(new[]
        {
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(1UL),
                new CivilizationId(1UL), start, new CommodityId(1U),
                2_000_000UL, PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(2UL),
                new CivilizationId(2UL), remote, new CommodityId(2U),
                2_000_000UL, PhysicalWorkerActivity.Producing),
            new PhysicalWorkerRuntimeRecord(new PhysicalWorkerId(3UL),
                new CivilizationId(1UL), neighbor, new CommodityId(1U),
                0UL, PhysicalWorkerActivity.Military)
        })));
    var before = canonical.Snapshot;
    var hasher = new CanonicalWorldStateHasher();
    var hashBefore = hasher.Compute(before);
    if (hashBefore.FormatVersion != 9U)
        throw new InvalidOperationException("Worker-initialized state must use v9 hash.");

    var turn = new TurnNumber(1UL);
    var context = new SimulationContext(turn, new SimulationSeed(712UL));
    var movement = new PhysicalWorkerMoveCommand(
        new CommandId(turn, 1UL), new CivilizationId(1UL),
        new PhysicalWorkerId(1UL), neighbor);
    var phase = new PhysicalWorkerMovementTurnPhase(canonical.GeneratedWorld);
    var gate = new AuthoritativeTurnCommitGate(canonical,
        new TurnPolicyResolutionGate(new ManualReadyTurnPolicy(),
            new PhasedTurnResolutionEngine(new ITurnResolutionPhase[] { phase })),
        firstTurn: turn, rulesetVersion: 1U);
    var committed = gate.ResolveAndCommit(new TurnPolicyResolutionGateInput(
        new TurnPolicyInput(turn, true, false),
        new TurnResolutionInput(before,
            new ISimulationCommand[] { movement }, context)));
    if (!committed.WasResolved ||
        committed.ResolutionResult is not { } result)
        throw new InvalidOperationException("Canonical movement turn was not committed.");

    var replayed = new SimulationEventLogReplayer(
        new PhysicalWorkerMoveEventExecutor(canonical.GeneratedWorld))
        .Replay(new SimulationReplayInput(before, result.EventLog, context));
    var finalHash = hasher.Compute(canonical.Snapshot);
    if (hasher.Compute(replayed) != finalHash ||
        finalHash.FormatVersion != 9U ||
        canonical.Snapshot.Revision != 1UL ||
        canonical.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1UL)
            .CellId != neighbor ||
        gate.NextTurn != new TurnNumber(2UL))
        throw new InvalidOperationException(
            "Canonical physical-worker movement journal or replay drift.");

    var economy = GoldbergFoundationSession.FromAuthoritativeWorkers(
        canonical, new Dictionary<int, double> { [1] = 1d, [2] = 0.5d },
        maximumAdjustmentPasses: 5);
    var preview = economy.Advance();
    if (preview.TotalProduction != 2d ||
        Math.Abs(preview.TotalDelivered - 2d) > 1e-9 ||
        economy.InspectTile(neighbor).Workers.Count != 2)
        throw new InvalidOperationException(
            "Read-only economic preview disagrees with canonical worker census.");

    Console.WriteLine($"GA_FOUNDATION_WORKER_AUTHORITY_V9 nodes={graph.NodeCount} " +
        $"edges={canonical.GeneratedWorld.StrategicTopology.Edges.Count} " +
        $"before_revision={before.Revision} after_revision={canonical.Snapshot.Revision} " +
        $"events={result.EventLog.Entries.Count} replay_hash_match=true " +
        $"hash_version={finalHash.FormatVersion}");
    Console.WriteLine($"  worker=1 from={start.Value} to={neighbor.Value} " +
        $"activity={canonical.Snapshot.Workers.Workers.Single(w => w.Id.Value == 1).Activity} " +
        $"remaining_workers={canonical.Snapshot.Workers.Workers.Count}");
    Console.WriteLine($"  preview_production={F(preview.TotalProduction)} " +
        $"preview_delivered={F(preview.TotalDelivered)} " +
        $"preview_opportunity_gap={F(preview.MaxRelativeOpportunity)} " +
        "preview_authoritative=false certified_F072=false");
    return;
}

// First actual generated-world integration: use EXISTING canonical sphere nodes,
// edges and world binding with actual physical workers and movement orders.
// The prototype deliberately does not publish into authoritative WorldState.
if (args[0] == "--foundation-goldberg")
{
    var canonical = CanonicalGameSession.Start(new WorldGenerationRequest(
        new WorldSeed(20261115UL), WorldGenerationVersion.Initial,
        new GoldbergParameters(3, 0)));
    var graph = canonical.GeneratedWorld.StrategicSurfaceGraph;
    var c0 = checked((int)graph.GetCellId(0).Value);
    var c1Index = graph.GetNeighborIndexes(0)[0];
    var c1 = checked((int)graph.GetCellId(c1Index).Value);
    var c2Index = graph.GetNeighborIndexes(c1Index)
        .First(index => index != 0);
    var c2 = checked((int)graph.GetCellId(c2Index).Value);
    var remote = checked((int)graph.GetCellId(graph.NodeCount / 2).Value);

    var economy = new GoldbergFoundationSession(canonical,
        new[]
        {
            new FoundationWorker(1, 1, c0, 0, 2d,
                FoundationWorkerActivity.Producing),
            new FoundationWorker(2, 2, remote, 1, 2d,
                FoundationWorkerActivity.Producing),
            new FoundationWorker(3, 1, c1, 0, 0d,
                FoundationWorkerActivity.Military),
            new FoundationWorker(4, 2, c2, 1, 1d,
                FoundationWorkerActivity.Producing),
            new FoundationWorker(5, 1, c0, 1, 1d,
                FoundationWorkerActivity.Traveling),
            new FoundationWorker(6, 2, remote, 0, 1d,
                FoundationWorkerActivity.Producing)
        },
        new Dictionary<int, double> { [0] = 1d, [1] = 0.7d },
        maximumAdjustmentPasses: 8);

    Console.WriteLine($"GA_FOUNDATION_GOLDBERG_V1 " +
        $"nodes={economy.WorldNodeCount} edges={economy.WorldEdgeCount} " +
        $"world_sha256={economy.WorldBinding.WorldSignatureSha256Hex} " +
        "all_tiles_same_laws=true authoritative=false");
    ShowGoldberg(economy.Advance());
    ShowGoldberg(economy.Advance(new[]
    {
        new FoundationWorkerOrder(5, NextTile: c1)
    }));
    ShowGoldberg(economy.Advance(new[]
    {
        new FoundationWorkerOrder(5, NextActivity: FoundationWorkerActivity.Producing)
    }));
    var inspected = economy.InspectTile(new StrategicCellId(checked((ulong)c1)));
    Console.WriteLine($"  selected_canonical_tile={inspected.CellId.Value} " +
        $"workers={inspected.Workers.Count} " +
        $"demanded_commodities={inspected.Demand.Count} " +
        $"priced_commodities={inspected.Prices.Count}");
    Console.WriteLine("  worldstate=UNCHANGED financial_settlement=NOT_IMPLEMENTED " +
        "battles=NOT_IMPLEMENTED certified_F072=false");
    return;
}

// A single controlled experiment, identical inputs and solver budget.
// "Fake" calibration creates ONLY the reference trade allocation; it never
// publishes a game turn. Its result is not assumed to be more accurate.
if (args[0] == "--foundation-calibration-ab")
{
    var control = CreateWorld();
    var controlTurn = control.Advance();

    var candidate = CreateWorld();
    var preview = candidate.CalibrateInitialMarket();
    if (candidate.CurrentTurn != 0)
        throw new InvalidOperationException("Calibration incorrectly advanced time.");
    var candidateTurn = candidate.Advance();

    if (controlTurn.Turn != 1 || candidateTurn.Turn != 1 ||
        Math.Abs(controlTurn.TotalProduction - candidateTurn.TotalProduction) > 1e-10 ||
        Math.Abs(controlTurn.TotalDelivered - candidateTurn.TotalDelivered) > 1e-10 ||
        !controlTurn.Workers.SequenceEqual(candidateTurn.Workers) ||
        !controlTurn.Demand.OrderBy(x => x.Key.Tile).ThenBy(x => x.Key.Commodity)
            .SequenceEqual(candidateTurn.Demand.OrderBy(x => x.Key.Tile)
                .ThenBy(x => x.Key.Commodity)))
        throw new InvalidOperationException("Calibration changed physical turn invariants.");

    Console.WriteLine("GA_FOUNDATION_CALIBRATION_AB_V1 same_workers=true " +
        "same_demand=true same_production=true same_time=true");
    Console.WriteLine($"  cold_turn=1 passes={controlTurn.AdjustmentPasses} " +
        $"opportunity_gap={F(controlTurn.MaxRelativeOpportunity)}");
    Console.WriteLine($"  fake_turn=0 passes={preview.AdjustmentPasses} " +
        $"opportunity_gap={F(preview.MaxRelativeOpportunity)}");
    Console.WriteLine($"  calibrated_real_turn=1 passes={candidateTurn.AdjustmentPasses} " +
        $"opportunity_gap={F(candidateTurn.MaxRelativeOpportunity)}");
    Console.WriteLine($"  delta_gap={F(candidateTurn.MaxRelativeOpportunity -
        controlTurn.MaxRelativeOpportunity)} " +
        "certified_F072=false production_adoption=false");
    return;
}

var world = CreateWorld();

static PhysicalWorkerTradeCycle CreateWorld() => new(
    new[]
    {
        new FoundationEdge(1, 0, 1, 0.40d, 2.0d, OwnerCivilization: 1,
            ExternalTariff: 0.025d),
        new FoundationEdge(2, 1, 2, 0.40d, 2.0d, OwnerCivilization: 2,
            ExternalTariff: 0.025d)
    },
    new[]
    {
        new FoundationWorker(1, 1, 0, 0, 1d, FoundationWorkerActivity.Producing),
        new FoundationWorker(2, 2, 2, 1, 1d, FoundationWorkerActivity.Producing),
        new FoundationWorker(3, 1, 1, 0, 0d, FoundationWorkerActivity.Military),
        new FoundationWorker(4, 2, 2, 0, 1d, FoundationWorkerActivity.Producing),
        new FoundationWorker(5, 1, 0, 1, 1d, FoundationWorkerActivity.Traveling)
    },
    new Dictionary<int, double> { [0] = 1d, [1] = 1d },
    maximumAdjustmentPasses: 128,
    relativeOpportunityTolerance: 0.001d);

Show(world.Advance());
Show(world.Advance(new[]
{
    new FoundationWorkerOrder(5, NextTile: 1)
}));
Show(world.Advance(new[]
{
    new FoundationWorkerOrder(5, NextActivity: FoundationWorkerActivity.Producing)
}));

static void ShowGoldberg(FoundationTurn turn)
{
    var allDemandTiles = turn.Demand.Keys.Select(x => x.Tile).Distinct().Count();
    var producingTiles = turn.Workers
        .Where(x => x.Activity == FoundationWorkerActivity.Producing &&
            x.OutputPerTurn > 0d)
        .Select(x => x.Tile).Distinct().Count();
    Console.WriteLine($"  goldberg_turn={turn.Turn} " +
        $"workers={turn.Workers.Count} producing_tiles={producingTiles} " +
        $"demand_tiles={allDemandTiles} " +
        $"produced={F(turn.TotalProduction)} delivered={F(turn.TotalDelivered)} " +
        $"trades={turn.Trades.Count} loaded_edges={turn.EdgeLoads.Count} " +
        $"passes={turn.AdjustmentPasses} opportunity_gap={F(turn.MaxRelativeOpportunity)}");
}

static void Show(FoundationTurn turn)
{
    Console.WriteLine(
        $"turn={turn.Turn} workers={turn.Workers.Count} " +
        $"produced={F(turn.TotalProduction)} delivered={F(turn.TotalDelivered)} " +
        $"passes={turn.AdjustmentPasses} opportunity_gap={F(turn.MaxRelativeOpportunity)} " +
        $"certified_F072={turn.CertifiedGlobalEquilibrium.ToString().ToLowerInvariant()}");
    foreach (var (key, demand) in turn.Demand
                 .OrderBy(x => x.Key.Tile).ThenBy(x => x.Key.Commodity))
    {
        var q = turn.Delivered.GetValueOrDefault(key);
        var price = turn.Prices.TryGetValue(key, out var p) ? F(p) : "UNPRICED_Q_ZERO";
        Console.WriteLine($"  tile={key.Tile} item={key.Commodity} demand={F(demand)} " +
                          $"quantity={F(q)} price={price}");
    }
    foreach (var (edge, load) in turn.EdgeLoads.OrderBy(x => x.Key))
        Console.WriteLine($"  edge={edge} physical_load={F(load)}");
    Console.WriteLine("  money=NOT_IMPLEMENTED  military_battles=NOT_IMPLEMENTED");
}

static string F(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
